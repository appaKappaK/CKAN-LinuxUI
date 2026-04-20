namespace CKAN.App.Models
{
    public sealed class AppWindowState
    {
        public double? Width { get; set; }

        public double? Height { get; set; }

        public int? PositionX { get; set; }

        public int? PositionY { get; set; }

        public bool IsMaximized { get; set; }

        public bool? ShowDetailsPane { get; set; }

        public AppWindowState Clone()
            => new AppWindowState
            {
                Width       = Width,
                Height      = Height,
                PositionX   = PositionX,
                PositionY   = PositionY,
                IsMaximized = IsMaximized,
                ShowDetailsPane = ShowDetailsPane,
            };
    }
}
