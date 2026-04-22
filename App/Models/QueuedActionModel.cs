namespace CKAN.App.Models
{
    public enum QueuedActionKind
    {
        Download,
        Install,
        Update,
        Remove,
    }

    public sealed class QueuedActionModel
    {
        public string Identifier { get; init; } = "";

        public string Name { get; init; } = "";

        public string TargetVersion { get; init; } = "";

        public QueuedActionKind ActionKind { get; init; }

        public string ActionText { get; init; } = "";

        public string DetailText { get; init; } = "";
    }
}
