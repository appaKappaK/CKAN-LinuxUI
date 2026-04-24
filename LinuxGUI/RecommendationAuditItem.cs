using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public sealed class RecommendationAuditItem : ReactiveObject
    {
        private bool isSelected;

        public RecommendationAuditItem(CkanModule module,
                                       string     kind,
                                       string     source,
                                       bool       isSelected)
        {
            Module = module;
            Kind = kind;
            Source = string.IsNullOrWhiteSpace(source) ? "No source recorded" : source;
            this.isSelected = isSelected && !module.IsDLC;
        }

        public CkanModule Module { get; }

        public string Identifier => Module.identifier;

        public string Name => Module.name;

        public string Version => Module.version?.ToString() ?? "";

        public string Summary => Module.@abstract ?? "";

        public string Kind { get; }

        public string Source { get; }

        public bool CanQueue => !Module.IsDLC;

        public bool IsSelected
        {
            get => isSelected;
            set => this.RaiseAndSetIfChanged(ref isSelected, value && CanQueue);
        }

        public string KindBadgeBackground
            => Kind switch
            {
                "Recommendation" => "#244031",
                "Suggestion"     => "#24384D",
                "Supporter"      => "#47371F",
                _                => "#28303A",
            };

        public string KindBadgeBorderBrush
            => Kind switch
            {
                "Recommendation" => "#3E7A58",
                "Suggestion"     => "#40648B",
                "Supporter"      => "#8A6A2E",
                _                => "#3C4754",
            };
    }
}
