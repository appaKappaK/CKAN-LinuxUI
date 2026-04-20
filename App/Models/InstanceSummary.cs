namespace CKAN.App.Models
{
    public sealed class InstanceSummary
    {
        public string Name { get; init; } = "";

        public string GameDir { get; init; } = "";

        public string GameName { get; init; } = "";

        public bool IsCurrent { get; init; }
    }
}
