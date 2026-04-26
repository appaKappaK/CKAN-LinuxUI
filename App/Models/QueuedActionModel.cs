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

        public string SourceText { get; init; } = "";

        public bool HasSourceText => !string.IsNullOrWhiteSpace(SourceText);

        public string VersionText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TargetVersion))
                {
                    return TargetVersion;
                }

                if (!string.IsNullOrWhiteSpace(ActionText)
                    && DetailText.StartsWith(ActionText + " ", System.StringComparison.OrdinalIgnoreCase))
                {
                    return DetailText[(ActionText.Length + 1)..].Trim();
                }

                var arrowIndex = DetailText.LastIndexOf(" -> ", System.StringComparison.Ordinal);
                return arrowIndex >= 0
                    ? DetailText[(arrowIndex + 4)..].Trim()
                    : DetailText;
            }
        }

        public string AccentBrush
            => ActionKind switch
            {
                QueuedActionKind.Download => "#4B7DB7",
                QueuedActionKind.Install  => "#3E9B6A",
                QueuedActionKind.Update   => "#B68B3A",
                QueuedActionKind.Remove   => "#B45A74",
                _                         => "#4B7DB7",
            };

        public string ActionBadgeBackground
            => ActionKind switch
            {
                QueuedActionKind.Download => "#24384D",
                QueuedActionKind.Install  => "#244031",
                QueuedActionKind.Update   => "#47371F",
                QueuedActionKind.Remove   => "#4B2734",
                _                         => "#28303A",
            };

        public string ActionBadgeBorderBrush
            => ActionKind switch
            {
                QueuedActionKind.Download => "#40648B",
                QueuedActionKind.Install  => "#3E7A58",
                QueuedActionKind.Update   => "#8A6A2E",
                QueuedActionKind.Remove   => "#8E4A5F",
                _                         => "#3C4754",
            };
    }
}
