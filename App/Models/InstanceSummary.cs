namespace CKAN.App.Models
{
    public sealed class InstanceSummary
    {
        public static InstanceSummary From(CKAN.GameInstance instance,
                                           string?      currentInstanceName,
                                           string?      defaultInstanceName)
            => new InstanceSummary
            {
                Name          = instance.Name,
                GameDir       = instance.GameDir,
                GameName      = instance.Game.ShortName,
                VersionText   = FormatVersion(instance.Version()),
                PlayTimeHours = instance.playTime?.Time.TotalHours ?? 0d,
                IsCurrent     = string.Equals(currentInstanceName, instance.Name, System.StringComparison.Ordinal),
                IsDefault     = string.Equals(defaultInstanceName, instance.Name, System.StringComparison.Ordinal),
            };

        public string Name { get; init; } = "";

        public string GameDir { get; init; } = "";

        public string GameName { get; init; } = "";

        public string VersionText { get; init; } = "";

        public double PlayTimeHours { get; init; }

        public string PlayTimeValue => PlayTimeHours.ToString("N1");

        public string PlayTimeCompactLabel => $"{PlayTimeValue} h";

        public string PlayTimeLabel => $"{PlayTimeValue} h played";

        public bool IsCurrent { get; init; }

        public bool IsDefault { get; init; }

        private static string FormatVersion(CKAN.Versioning.GameVersion? version)
            => version == null
                ? "<NONE>"
                : new CKAN.Versioning.GameVersion(version.Major, version.Minor, version.Patch).ToString() ?? "";
    }
}
