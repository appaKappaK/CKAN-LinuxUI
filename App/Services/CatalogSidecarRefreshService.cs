using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CKAN.App.Services
{
    public sealed class CatalogSidecarRefreshService : ICatalogSidecarRefreshService
    {
        public CatalogSidecarRefreshService(CatalogIndexService catalogIndexService)
        {
            this.catalogIndexService = catalogIndexService;
        }

        public async Task<CatalogSidecarRefreshResult> RefreshIfNeededAsync(
            IReadOnlyList<string> repositoryCachePaths,
            CancellationToken     cancellationToken)
        {
            if (repositoryCachePaths == null)
            {
                throw new ArgumentNullException(nameof(repositoryCachePaths));
            }

            await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var cachePaths = repositoryCachePaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (cachePaths.Length == 0)
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Skipped,
                        "No repository cache files are available.");
                }

                var missingPath = cachePaths.FirstOrDefault(path => !File.Exists(path));
                if (missingPath != null)
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Failed,
                        $"Repository cache file does not exist: {missingPath}");
                }

                string sourceFingerprint;
                string outputPath;
                try
                {
                    sourceFingerprint = CatalogIndexService.ComputeSourceFingerprint(cachePaths);
                    outputPath = catalogIndexService.ResolveRefreshOutputPath();
                }
                catch (Exception ex)
                {
                    return new CatalogSidecarRefreshResult(CatalogSidecarRefreshStatus.Failed,
                                                           ex.Message);
                }

                var currentIndex = catalogIndexService.TryLoad(outputPath);
                if (currentIndex != null
                    && !string.IsNullOrWhiteSpace(currentIndex.SourceFingerprint)
                    && string.Equals(currentIndex.SourceFingerprint,
                                     sourceFingerprint,
                                     StringComparison.OrdinalIgnoreCase))
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Current,
                        "Rust catalog sidecar already matches the repository cache.");
                }

                var executablePath = FindExecutable();
                if (executablePath == null)
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Unavailable,
                        "ckan-meta-rs is not installed or could not be found.");
                }

                return await RunRefreshAsync(executablePath,
                                             cachePaths,
                                             outputPath,
                                             sourceFingerprint,
                                             cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                refreshGate.Release();
            }
        }

        private async Task<CatalogSidecarRefreshResult> RunRefreshAsync(
            string                executablePath,
            IReadOnlyList<string> repositoryCachePaths,
            string                outputPath,
            string                sourceFingerprint,
            CancellationToken     cancellationToken)
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            startInfo.ArgumentList.Add("refresh-sidecar");
            foreach (var cachePath in repositoryCachePaths)
            {
                startInfo.ArgumentList.Add("--repository-cache");
                startInfo.ArgumentList.Add(cachePath);
            }
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--source-fingerprint");
            startInfo.ArgumentList.Add(sourceFingerprint);
            startInfo.ArgumentList.Add("--json");

            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Failed,
                        "ckan-meta-rs did not start.");
                }
            }
            catch (Exception ex)
            {
                return new CatalogSidecarRefreshResult(CatalogSidecarRefreshStatus.Failed,
                                                       ex.Message);
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError  = process.StandardError.ReadToEndAsync();
            using var timeoutSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(RefreshTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return new CatalogSidecarRefreshResult(
                    CatalogSidecarRefreshStatus.Failed,
                    "ckan-meta-rs timed out after two minutes.");
            }

            var output = (await standardOutput.ConfigureAwait(false)).Trim();
            var error  = (await standardError.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0)
            {
                return new CatalogSidecarRefreshResult(
                    CatalogSidecarRefreshStatus.Failed,
                    Summarize(string.IsNullOrWhiteSpace(error) ? output : error,
                              $"ckan-meta-rs exited with code {process.ExitCode}."));
            }

            try
            {
                var currentFingerprint = CatalogIndexService
                    .ComputeSourceFingerprint(repositoryCachePaths);
                if (!string.Equals(currentFingerprint,
                                   sourceFingerprint,
                                   StringComparison.OrdinalIgnoreCase))
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Failed,
                        "Repository cache files changed while the Rust catalog was being generated.");
                }

                if (catalogIndexService.TryLoad(outputPath,
                                                repositoryCachePaths,
                                                sourceFingerprint) == null)
                {
                    return new CatalogSidecarRefreshResult(
                        CatalogSidecarRefreshStatus.Failed,
                        "ckan-meta-rs did not produce a valid catalog sidecar.");
                }
            }
            catch (Exception ex)
            {
                return new CatalogSidecarRefreshResult(CatalogSidecarRefreshStatus.Failed,
                                                       ex.Message);
            }

            return new CatalogSidecarRefreshResult(
                CatalogSidecarRefreshStatus.Updated,
                Summarize(output, "Rust catalog sidecar updated."));
        }

        private static string? FindExecutable()
        {
            var executableName = OperatingSystem.IsWindows()
                ? "ckan-meta-rs.exe"
                : "ckan-meta-rs";
            var configuredPath = Environment.GetEnvironmentVariable("CKAN_META_RS_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return File.Exists(configuredPath)
                    ? Path.GetFullPath(configuredPath)
                    : null;
            }

            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, executableName),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                                              "..", "..", "bin", executableName)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                                              "..", "libexec", executableName)),
            };
            var xdgBinHome = Environment.GetEnvironmentVariable("XDG_BIN_HOME");
            if (!string.IsNullOrWhiteSpace(xdgBinHome))
            {
                candidates.Add(Path.Combine(xdgBinHome, executableName));
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                candidates.Add(Path.Combine(userProfile, ".local", "bin", executableName));
            }

            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, executableName)));
            return candidates.FirstOrDefault(File.Exists);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // The process may have exited between the check and the kill.
            }
        }

        private static string Summarize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }
            const int maxLength = 2000;
            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }

        private static readonly TimeSpan RefreshTimeout = TimeSpan.FromMinutes(2);

        private readonly CatalogIndexService catalogIndexService;
        private readonly SemaphoreSlim       refreshGate = new SemaphoreSlim(1, 1);
    }
}
