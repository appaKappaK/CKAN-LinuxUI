using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using CKAN.App.Models;
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
                var index = TryLoad(path);
                if (index != null)
                {
                    return index;
                }
            }
            return null;
        }

        public bool HasCandidateFile()
            => CandidatePaths().Any(path => !string.IsNullOrWhiteSpace(path)
                                            && File.Exists(path));

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
                    if (index?.SchemaVersion == 1 && index.Modules.Count > 0)
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
                    .ToList();

        private static CatalogIndexModule SelectLatestModule(IGrouping<string, CatalogIndexModule> group)
        {
            using var enumerator = group.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException("Catalog index module group was empty.");
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
