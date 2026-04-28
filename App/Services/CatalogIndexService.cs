using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using CKAN.App.Models;
using CKAN.IO;

namespace CKAN.App.Services
{
    public sealed class CatalogIndexService
    {
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

        public CatalogIndex? TryLoad(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                var index = JsonConvert.DeserializeObject<CatalogIndex>(File.ReadAllText(path));
                return index?.SchemaVersion == 1 && index.Modules.Count > 0
                    ? index
                    : null;
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
    }
}
