namespace CKAN.LinuxGUI
{
    public sealed class CatalogSkeletonRow
    {
        public string AccentBrush { get; init; } = "#39424E";

        public double TitleWidth { get; init; }

        public double AuthorWidth { get; init; }

        public bool HasAuthor => AuthorWidth > 0;

        public double SummaryWidth { get; init; }

        public double DownloadsWidth { get; init; }

        public double CompatibilityWidth { get; init; }

        public double ReleaseWidth { get; init; }

        public double VersionPrimaryWidth { get; init; }

        public double VersionSecondaryWidth { get; init; }

        public double Opacity { get; init; } = 1.0;

        public double PrimaryBadgeWidth { get; init; }

        public string PrimaryBadgeBackground { get; init; } = "#3B4653";

        public bool HasPrimaryBadge => PrimaryBadgeWidth > 0;

        public bool HasCachedBadge { get; init; }

        public double SecondaryBadgeWidth { get; init; }

        public string SecondaryBadgeBackground { get; init; } = "#39424E";

        public string SecondaryBadgeBorderBrush { get; init; } = "#607286";

        public bool HasSecondaryBadge => SecondaryBadgeWidth > 0;

        public double TertiaryBadgeWidth { get; init; }

        public string TertiaryBadgeBackground { get; init; } = "#31424F";

        public string TertiaryBadgeBorderBrush { get; init; } = "#4C6A86";

        public bool HasTertiaryBadge => TertiaryBadgeWidth > 0;

        public double QueueBadgeWidth { get; init; }

        public string QueueBadgeBackground { get; init; } = "#00000000";

        public string QueueBadgeBorderBrush { get; init; } = "#00000000";

        public bool HasQueueBadge => QueueBadgeWidth > 0;
    }
}
