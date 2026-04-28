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

        [JsonProperty("kind")]
        public string? Kind { get; init; }

        [JsonProperty("is_latest")]
        public bool IsLatest { get; init; }
    }
}
