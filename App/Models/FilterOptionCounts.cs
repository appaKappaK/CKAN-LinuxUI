namespace CKAN.App.Models
{
    public sealed class FilterOptionCounts
    {
        public int Compatible { get; init; }

        public int Installed { get; init; }

        public int Updatable { get; init; }

        public int Replaceable { get; init; }

        public int Cached { get; init; }

        public int Uncached { get; init; }

        public int NotInstalled { get; init; }

        public int Incompatible { get; init; }
    }
}
