namespace CKAN.App.Models
{
    public sealed class ReviewCountSettings
    {
        public bool Enabled { get; set; } = true;
        public bool Dependencies { get; set; } = true;
        public bool AutoRemovals { get; set; } = true;
        public bool Recommendations { get; set; } = true;
        public bool Suggestions { get; set; } = true;
        public bool Supported { get; set; } = true;

        public ReviewCountSettings Clone() => (ReviewCountSettings)MemberwiseClone();
    }
}
