namespace CKAN.App.Models
{
    public sealed class FilterState
    {
        public string SearchText { get; init; } = "";

        public bool InstalledOnly { get; init; }

        public bool NotInstalledOnly { get; init; }

        public bool UpdatableOnly { get; init; }

        public bool NewOnly { get; init; }

        public bool CachedOnly { get; init; }

        public bool IncompatibleOnly { get; init; }
    }
}
