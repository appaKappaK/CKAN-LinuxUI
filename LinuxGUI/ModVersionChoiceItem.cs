namespace CKAN.LinuxGUI
{
    public sealed class ModVersionChoiceItem
    {
        public string VersionText { get; init; } = "";

        public string CompatibilityText { get; init; } = "";

        public string ReleaseDateText { get; init; } = "";

        public string BadgeText { get; init; } = "";

        public string BadgeBackground { get; init; } = "#31424F";

        public string BadgeBorderBrush { get; init; } = "#4C6A86";

        public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

        public bool IsInstalledVersion { get; init; }

        public bool IsCompatible { get; init; }

        public int VersionComparisonToInstalled { get; init; }

        public CkanModule Module { get; init; } = null!;

        public string VersionKey => Module.version.ToString();

        public string MetaText => $"{CompatibilityText} • {ReleaseDateText}";

        public override string ToString()
            => VersionText;
    }
}
