namespace CKAN.App.Models
{
    public sealed record FilterState
    {
        public string SearchText { get; init; } = "";

        public string NameText { get; init; } = "";

        public string IdentifierText { get; init; } = "";

        public string AuthorText { get; init; } = "";

        public string SummaryText { get; init; } = "";

        public string DescriptionText { get; init; } = "";

        public string LicenseText { get; init; } = "";

        public string LanguageText { get; init; } = "";

        public string DependsText { get; init; } = "";

        public string RecommendsText { get; init; } = "";

        public string SuggestsText { get; init; } = "";

        public string ConflictsText { get; init; } = "";

        public string SupportsText { get; init; } = "";

        public string TagText { get; init; } = "";

        public string LabelText { get; init; } = "";

        public string CompatibilityText { get; init; } = "";

        public ModSortOption SortOption { get; init; } = ModSortOption.Name;

        public bool? SortDescending { get; init; }

        public bool InstalledOnly { get; init; }

        public bool NotInstalledOnly { get; init; }

        public bool UpdatableOnly { get; init; }

        public bool NotUpdatableOnly { get; init; }

        public bool NewOnly { get; init; }

        public bool CompatibleOnly { get; init; }

        public bool CachedOnly { get; init; }

        public bool UncachedOnly { get; init; }

        public bool IncompatibleOnly { get; init; }

        public bool HasReplacementOnly { get; init; }

        public bool NoReplacementOnly { get; init; }
    }
}
