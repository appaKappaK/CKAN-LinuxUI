namespace CKAN.App.Models
{
    public sealed class ModBrowserColumnLayout
    {
        public const double DefaultMetadataColumnWidth  = 320;
        public const double DefaultDownloadsColumnWidth = 84;
        public const double DefaultReleasedColumnWidth  = 92;
        public const double DefaultInstalledColumnWidth = 92;

        public double MetadataColumnWidth { get; set; } = DefaultMetadataColumnWidth;

        public double DownloadsColumnWidth { get; set; } = DefaultDownloadsColumnWidth;

        public double ReleasedColumnWidth { get; set; } = DefaultReleasedColumnWidth;

        public double InstalledColumnWidth { get; set; } = DefaultInstalledColumnWidth;

        public ModBrowserColumnLayout Clone()
            => new ModBrowserColumnLayout
            {
                MetadataColumnWidth  = MetadataColumnWidth,
                DownloadsColumnWidth = DownloadsColumnWidth,
                ReleasedColumnWidth  = ReleasedColumnWidth,
                InstalledColumnWidth = InstalledColumnWidth,
            };
    }
}
