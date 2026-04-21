namespace CKAN.App.Models
{
    public sealed class FilterState
    {
        public string SearchText { get; init; } = "";

        public string AuthorText { get; init; } = "";

        public string CompatibilityText { get; init; } = "";

        public ModSortOption SortOption { get; init; } = ModSortOption.Name;

        public bool? SortDescending { get; init; }

        public bool InstalledOnly { get; init; }

        public bool NotInstalledOnly { get; init; }

        public bool UpdatableOnly { get; init; }

        public bool NewOnly { get; init; }

        public bool CompatibleOnly { get; init; }

        public bool CachedOnly { get; init; }

        public bool UncachedOnly { get; init; }

        public bool IncompatibleOnly { get; init; }

        public bool HasReplacementOnly { get; init; }
    }
}
