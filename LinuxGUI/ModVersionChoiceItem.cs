using System.Linq;

namespace CKAN.LinuxGUI
{
    public sealed class ModVersionChoiceItem
    {
        public string VersionText { get; init; } = "";

        public string CompatibilityText { get; init; } = "";

        public string ReleaseDateText { get; init; } = "";

        public string BadgeText { get; init; } = "";

        public string BadgeForeground { get; init; } = "#AEB8C6";

        public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

        public string SelectionBadgeText
            => IsInstalledVersion ? "Installed" : "";

        public bool HasSelectionBadge => !string.IsNullOrWhiteSpace(SelectionBadgeText);

        public bool IsInstalledVersion { get; init; }

        public bool IsCompatible { get; init; }

        public int VersionComparisonToInstalled { get; init; }

        public CkanModule Module { get; init; } = null!;

        public string VersionKey => Module.version.ToString();

        public string MetaText
            => string.Join(" • ", new[] { CompatibilityText, ReleaseDateText }
                .Where(value => !string.IsNullOrWhiteSpace(value)
                                && !string.Equals(value, "Unknown", System.StringComparison.OrdinalIgnoreCase)));

        public bool HasMetaText => !string.IsNullOrWhiteSpace(MetaText);

        public override string ToString()
            => VersionText;
    }
}
