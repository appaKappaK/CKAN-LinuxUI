using System.Collections.Generic;

using Newtonsoft.Json;

namespace CKAN.App.Models
{
    public sealed class CatalogIndex
    {
        [JsonProperty("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonProperty("source")]
        public string Source { get; init; } = "";

        [JsonProperty("modules")]
        public List<CatalogIndexModule> Modules { get; init; } = new List<CatalogIndexModule>();
    }

    public sealed class CatalogIndexModule
    {
        [JsonProperty("identifier")]
        public string Identifier { get; init; } = "";

        [JsonProperty("name")]
        public string Name { get; init; } = "";

        [JsonProperty("version")]
        public string? Version { get; init; }

        [JsonProperty("abstract_text")]
        public string? AbstractText { get; init; }

        [JsonProperty("description")]
        public string? Description { get; init; }

        [JsonProperty("authors")]
        public List<string> Authors { get; init; } = new List<string>();

        [JsonProperty("licenses")]
        public List<string> Licenses { get; init; } = new List<string>();

        [JsonProperty("kind")]
        public string? Kind { get; init; }

        [JsonProperty("release_date")]
        public string? ReleaseDate { get; init; }

        [JsonProperty("download_size")]
        public long? DownloadSize { get; init; }

        [JsonProperty("download_count")]
        public int? DownloadCount { get; init; }

        [JsonProperty("ksp_version")]
        public string? KspVersion { get; init; }

        [JsonProperty("ksp_version_min")]
        public string? KspVersionMin { get; init; }

        [JsonProperty("ksp_version_max")]
        public string? KspVersionMax { get; init; }

        [JsonProperty("dependency_names")]
        public List<string> DependencyNames { get; init; } = new List<string>();

        [JsonProperty("recommendation_names")]
        public List<string> RecommendationNames { get; init; } = new List<string>();

        [JsonProperty("suggestion_names")]
        public List<string> SuggestionNames { get; init; } = new List<string>();

        [JsonProperty("conflict_names")]
        public List<string> ConflictNames { get; init; } = new List<string>();

        [JsonProperty("is_latest")]
        public bool IsLatest { get; init; }
    }
}
