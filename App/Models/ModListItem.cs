namespace CKAN.App.Models
{
    public sealed class ModListItem
    {
        public string Identifier { get; init; } = "";

        public string Name { get; init; } = "";

        public string Author { get; init; } = "";

        public string Summary { get; init; } = "";

        public string LatestVersion { get; init; } = "";

        public string InstalledVersion { get; init; } = "";

        public bool IsInstalled { get; init; }

        public bool HasUpdate { get; init; }

        public bool IsIncompatible { get; init; }

        public bool IsCached { get; init; }

        public bool HasReplacement { get; init; }

        public string Compatibility { get; init; } = "";
    }
}
