namespace CKAN.App.Models
{
    public sealed class ModDetailsModel
    {
        public string Identifier { get; init; } = "";

        public string Title { get; init; } = "";

        public string Description { get; init; } = "";

        public string Summary { get; init; } = "";

        public string Authors { get; init; } = "";

        public string LatestVersion { get; init; } = "";

        public string InstalledVersion { get; init; } = "";

        public string Compatibility { get; init; } = "";

        public string ModuleKind { get; init; } = "";

        public string License { get; init; } = "";

        public string ReleaseDate { get; init; } = "";

        public string DownloadSize { get; init; } = "";

        public int? DownloadCount { get; init; }

        public int DependencyCount { get; init; }

        public int RecommendationCount { get; init; }

        public int SuggestionCount { get; init; }

        public bool IsInstalled { get; init; }

        public bool HasUpdate { get; init; }

        public bool HasVersionUpdate { get; init; }

        public bool IsCached { get; init; }

        public bool IsIncompatible { get; init; }

        public bool HasReplacement { get; init; }
    }
}
