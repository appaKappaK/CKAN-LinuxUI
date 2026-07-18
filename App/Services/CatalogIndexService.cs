using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

using Newtonsoft.Json;

using CKAN.App.Models;
using CKAN.Configuration;
using CKAN.IO;
using CKAN.Versioning;

namespace CKAN.App.Services
{
    public sealed class CatalogIndexService
    {
        private readonly object cacheLock = new object();
        private string?         cachedPath;
        private DateTime        cachedLastWriteUtc;
        private long            cachedLength;
        private CatalogIndex?   cachedIndex;

        public CatalogIndex? TryLoad()
        {
            foreach (var path in CandidatePaths())
            {
                var index = TryLoad(path, DefaultRepositoryCachePath());
                if (index != null)
                {
                    return index;
                }
            }
            return null;
        }

        public bool HasCandidateFile()
            => CandidatePaths().Any(path => !string.IsNullOrWhiteSpace(path)
                                            && File.Exists(path)
                                            && IsCurrentWithRepositoryCache(path,
                                                                            DefaultRepositoryCachePath()));

        public CatalogIndex? TryLoad(string path,
                                     string repositoryCachePath)
            => IsCurrentWithRepositoryCache(path, repositoryCachePath)
                ? TryLoad(path)
                : null;

        public CatalogIndex? TryLoad(IReadOnlyList<string> repositoryCachePaths,
                                     string                expectedSourceFingerprint)
        {
            foreach (var path in CandidatePaths())
            {
                var index = TryLoad(path,
                                    repositoryCachePaths,
                                    expectedSourceFingerprint);
                if (index != null)
                {
                    return index;
                }
            }
            return null;
        }

        public CatalogIndex? TryLoad(string                path,
                                     IReadOnlyList<string> repositoryCachePaths,
                                     string                expectedSourceFingerprint)
        {
            var index = TryLoad(path);
            if (index == null)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(index.SourceFingerprint)
                ? IsCurrentWithRepositoryCache(path, repositoryCachePaths)
                    ? index
                    : null
                : string.Equals(index.SourceFingerprint,
                                expectedSourceFingerprint,
                                StringComparison.OrdinalIgnoreCase)
                    ? index
                    : null;
        }

        public CatalogIndex? TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    return null;
                }

                lock (cacheLock)
                {
                    if (cachedIndex != null
                        && string.Equals(cachedPath, info.FullName, StringComparison.Ordinal)
                        && cachedLastWriteUtc == info.LastWriteTimeUtc
                        && cachedLength == info.Length)
                    {
                        return cachedIndex;
                    }

                    var index = JsonConvert.DeserializeObject<CatalogIndex>(File.ReadAllText(info.FullName));
                    if (index?.SchemaVersion is 1 or 2 && index.Modules.Count > 0)
                    {
                        cachedPath         = info.FullName;
                        cachedLastWriteUtc = info.LastWriteTimeUtc;
                        cachedLength       = info.Length;
                        cachedIndex        = index;
                        return index;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public string ResolveRefreshOutputPath()
        {
            var envPath = Environment.GetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return ResolveRefreshOutputPath(envPath);
            }

            var candidates = CandidatePaths().ToArray();
            var path = candidates.FirstOrDefault(File.Exists)
                       ?? candidates.First();
            return ResolveRefreshOutputPath(path);
        }

        public static string ResolveRefreshOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A catalog index path is required.", nameof(path));
            }

            var fullPath = Path.GetFullPath(path);
            #if NET8_0_OR_GREATER
            if (File.Exists(fullPath))
            {
                var target = File.ResolveLinkTarget(fullPath, returnFinalTarget: true);
                if (target != null)
                {
                    return target.FullName;
                }
            }
            #endif
            return fullPath;
        }

        public static string ComputeSourceFingerprint(
            IReadOnlyList<string> repositoryCachePaths)
        {
            if (repositoryCachePaths == null)
            {
                throw new ArgumentNullException(nameof(repositoryCachePaths));
            }

            using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var path in repositoryCachePaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    throw new ArgumentException("Repository cache paths cannot be blank.",
                                                nameof(repositoryCachePaths));
                }

                using var stream = File.OpenRead(path);
                using var fileHash = SHA256.Create();
                sourceHash.AppendData(fileHash.ComputeHash(stream));
            }
            return Convert.ToHexString(sourceHash.GetHashAndReset()).ToLowerInvariant();
        }

        private static IEnumerable<string> CandidatePaths()
        {
            var envPath = Environment.GetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                yield return envPath;
            }

            yield return Path.Combine(CKANPathUtils.AppDataPath, "catalog-index-latest.json");
            yield return Path.Combine(CKANPathUtils.AppDataPath, "catalog-index.json");
        }

        private static string DefaultRepositoryCachePath()
            => Path.Combine(CKANPathUtils.AppDataPath, "repos");

        private static bool IsCurrentWithRepositoryCache(string indexPath,
                                                         string repositoryCachePath)
        {
            try
            {
                var indexWriteUtc = LastWriteTimeUtcFollowingLink(indexPath);
                var etagsPath = Path.Combine(repositoryCachePath, "etags.json");
                var repositoryContentWriteUtc = File.Exists(etagsPath)
                    ? File.GetLastWriteTimeUtc(etagsPath)
                    : Directory.Exists(repositoryCachePath)
                        ? Directory.EnumerateFiles(repositoryCachePath, "*.json")
                                   .Select(File.GetLastWriteTimeUtc)
                                   .DefaultIfEmpty(DateTime.MinValue)
                                   .Max()
                        : DateTime.MinValue;
                return indexWriteUtc >= repositoryContentWriteUtc;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCurrentWithRepositoryCache(
            string                indexPath,
            IReadOnlyList<string> repositoryCachePaths)
        {
            try
            {
                var repositoryContentWriteUtc = repositoryCachePaths
                    .Where(File.Exists)
                    .Select(File.GetLastWriteTimeUtc)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();
                return LastWriteTimeUtcFollowingLink(indexPath) >= repositoryContentWriteUtc;
            }
            catch
            {
                return false;
            }
        }

        private static DateTime LastWriteTimeUtcFollowingLink(string path)
        {
            #if NET8_0_OR_GREATER
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            return (target ?? new FileInfo(path)).LastWriteTimeUtc;
            #else
            return File.GetLastWriteTimeUtc(path);
            #endif
        }

        public static IReadOnlyList<string> LatestIdentifiers(CatalogIndex index)
            => index.Modules
                    .Where(module => module.IsLatest)
                    .Where(module => !string.IsNullOrWhiteSpace(module.Identifier))
                    .Where(module => !string.Equals(module.Kind, "dlc", StringComparison.OrdinalIgnoreCase))
                    .Select(module => module.Identifier)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

        public static IReadOnlyList<CatalogIndexModule> LatestModules(CatalogIndex index)
            => index.Modules
                    .Where(module => module.IsLatest)
                    .Where(module => !string.IsNullOrWhiteSpace(module.Identifier))
                    .Where(module => !string.Equals(module.Kind, "dlc", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(module => module.Identifier, StringComparer.OrdinalIgnoreCase)
                    .Select(SelectLatestModule)
                    .OfType<CatalogIndexModule>()
                    .ToList();

        public static IReadOnlyList<CatalogIndexModule> LatestModules(
            CatalogIndex             index,
            StabilityToleranceConfig stabilityTolerance)
            => index.SchemaVersion < 2
                ? LatestModules(index)
                : index.Modules
                       .Where(module => !string.IsNullOrWhiteSpace(module.Identifier))
                       .Where(module => !string.Equals(module.Kind, "dlc", StringComparison.OrdinalIgnoreCase))
                       .GroupBy(module => module.Identifier, StringComparer.OrdinalIgnoreCase)
                       .Select(group => SelectLatestModule(
                                   group.Where(module => IsLatestForTolerance(
                                       module,
                                       stabilityTolerance.ModStabilityTolerance(group.Key)
                                           ?? stabilityTolerance.OverallStabilityTolerance))))
                       .OfType<CatalogIndexModule>()
                       .ToList();

        private static bool IsLatestForTolerance(CatalogIndexModule module,
                                                 ReleaseStatus      tolerance)
            => tolerance switch
            {
                ReleaseStatus.stable      => module.IsLatestStable,
                ReleaseStatus.testing     => module.IsLatestTesting,
                ReleaseStatus.development => module.IsLatestDevelopment || module.IsLatest,
                _                         => false,
            };

        private static CatalogIndexModule? SelectLatestModule(IEnumerable<CatalogIndexModule> modules)
        {
            using var enumerator = modules.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return null;
            }

            var best = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                return best;
            }

            var bestVersion = TryModuleVersion(best.Version);
            do
            {
                var candidate = enumerator.Current;
                var candidateVersion = TryModuleVersion(candidate.Version);
                int versionComparison = ModuleVersionComparer.Instance.Compare(candidateVersion, bestVersion);
                if (versionComparison > 0
                    || (versionComparison == 0
                        && string.Compare(candidate.ReleaseDate,
                                          best.ReleaseDate,
                                          StringComparison.OrdinalIgnoreCase) > 0))
                {
                    best = candidate;
                    bestVersion = candidateVersion;
                }
            }
            while (enumerator.MoveNext());

            return best;
        }

        private static ModuleVersion? TryModuleVersion(string? value)
        {
            try
            {
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : new ModuleVersion(value!);
            }
            catch
            {
                return null;
            }
        }

        private sealed class ModuleVersionComparer : IComparer<ModuleVersion?>
        {
            public static readonly ModuleVersionComparer Instance = new ModuleVersionComparer();

            public int Compare(ModuleVersion? x, ModuleVersion? y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                if (x == null)
                {
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                return x.CompareTo(y);
            }
        }
    }
}
