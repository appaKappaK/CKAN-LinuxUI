using CKAN.App.Models;

namespace CKAN.LinuxGUI
{
    public sealed class SortOptionItem
    {
        public ModSortOption Value { get; init; }

        public string Label { get; init; } = "";
    }
}
