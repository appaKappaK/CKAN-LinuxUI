using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using CKAN.App.Services;
using Tests.Data;

namespace Tests.App.Services
{
    [TestFixture]
    [NonParallelizable]
    public sealed class CatalogSidecarRefreshServiceTests
    {
        [Test]
        public async Task RefreshIfNeededAsync_WithGenerator_UpdatesOnceAndPassesOrderedCaches()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("The fake ckan-meta-rs executable is a POSIX shell script.");
                return;
            }

            var dir = TestData.NewTempDir();
            var oldExecutable = Environment.GetEnvironmentVariable("CKAN_META_RS_PATH");
            var oldIndex      = Environment.GetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH");
            var oldArgs       = Environment.GetEnvironmentVariable("FAKE_CKAN_META_ARGS");
            var oldRuns       = Environment.GetEnvironmentVariable("FAKE_CKAN_META_RUNS");
            try
            {
                var firstCache  = Path.Combine(dir, "first repository.json");
                var secondCache = Path.Combine(dir, "second repository.json");
                var outputPath  = Path.Combine(dir, "catalog-index-latest.json");
                var argsPath    = Path.Combine(dir, "args.txt");
                var runsPath    = Path.Combine(dir, "runs.txt");
                var executable  = Path.Combine(dir, "ckan-meta-rs");
                File.WriteAllText(firstCache, "first repository");
                File.WriteAllText(secondCache, "second repository");
                File.WriteAllText(executable, @"#!/bin/sh
printf '%s\n' ""$@"" > ""$FAKE_CKAN_META_ARGS""
printf 'run\n' >> ""$FAKE_CKAN_META_RUNS""
output=''
fingerprint=''
while [ ""$#"" -gt 0 ]; do
    case ""$1"" in
        --output)
            shift
            output=""$1""
            ;;
        --source-fingerprint)
            shift
            fingerprint=""$1""
            ;;
    esac
    shift
done
sleep 0.1
printf '{""schema_version"":2,""source"":""fixture"",""source_fingerprint"":""%s"",""modules"":[{""identifier"":""Example"",""name"":""Example"",""is_latest"":true}]}' ""$fingerprint"" > ""$output""
printf '{""status"":""updated""}\n'
");
                File.SetUnixFileMode(executable,
                                     UnixFileMode.UserRead
                                     | UnixFileMode.UserWrite
                                     | UnixFileMode.UserExecute);
                Environment.SetEnvironmentVariable("CKAN_META_RS_PATH", executable);
                Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", outputPath);
                Environment.SetEnvironmentVariable("FAKE_CKAN_META_ARGS", argsPath);
                Environment.SetEnvironmentVariable("FAKE_CKAN_META_RUNS", runsPath);

                var service = new CatalogSidecarRefreshService(new CatalogIndexService());
                var refreshes = await Task.WhenAll(
                    service.RefreshIfNeededAsync(new[] { firstCache, secondCache },
                                                 CancellationToken.None),
                    service.RefreshIfNeededAsync(new[] { firstCache, secondCache },
                                                 CancellationToken.None));

                var index = new CatalogIndexService().TryLoad(outputPath);
                var arguments = File.ReadAllLines(argsPath);
                Assert.Multiple(() =>
                {
                    Assert.That(refreshes.Select(result => result.Status),
                                Is.EquivalentTo(new[]
                                {
                                    CatalogSidecarRefreshStatus.Updated,
                                    CatalogSidecarRefreshStatus.Current,
                                }));
                    Assert.That(File.ReadAllLines(runsPath), Has.Length.EqualTo(1));
                    Assert.That(arguments, Is.EqualTo(new[]
                    {
                        "refresh-sidecar",
                        "--repository-cache", firstCache,
                        "--repository-cache", secondCache,
                        "--output", outputPath,
                        "--source-fingerprint",
                        CatalogIndexService.ComputeSourceFingerprint(
                            new[] { firstCache, secondCache }),
                        "--json",
                    }));
                    Assert.That(index, Is.Not.Null);
                    Assert.That(index!.SourceFingerprint,
                                Is.EqualTo(CatalogIndexService.ComputeSourceFingerprint(
                                    new[] { firstCache, secondCache })));
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("CKAN_META_RS_PATH", oldExecutable);
                Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", oldIndex);
                Environment.SetEnvironmentVariable("FAKE_CKAN_META_ARGS", oldArgs);
                Environment.SetEnvironmentVariable("FAKE_CKAN_META_RUNS", oldRuns);
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task RefreshIfNeededAsync_WithNoCaches_SkipsWithoutGenerator()
        {
            var result = await new CatalogSidecarRefreshService(new CatalogIndexService())
                .RefreshIfNeededAsync(Array.Empty<string>(), CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(CatalogSidecarRefreshStatus.Skipped));
        }
    }
}
