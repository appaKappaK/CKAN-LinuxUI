using System.Globalization;
using System.Linq;

using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public sealed class RecommendationAuditItem : ReactiveObject
    {
        private bool isSelected;
        private bool isDetailSelected;

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

        public string NameAndVersion
            => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name} {Version}";

        public string Summary => Module.@abstract ?? "";

        public string DetailDescription
            => !string.IsNullOrWhiteSpace(Module.description)
                ? Module.description!
                : Summary;

        public string AuthorsText
            => Module.author is { Count: > 0 }
                ? string.Join(", ", Module.author)
                : "Unknown";

        public string LicenseText
            => Module.license is { Count: > 0 }
                ? string.Join(", ", Module.license.Select(license => license.ToString()))
                : "Unknown";

        public string DownloadSizeText
            => Module.download_size > 0
                ? CkanModule.FmtSize(Module.download_size)
                : "Unknown";

        public string ReleaseDateText
            => Module.release_date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                ?? "Unknown";

        public string ResourceText
        {
            get
            {
                var resource = Module.resources?.homepage
                               ?? Module.resources?.spacedock
                               ?? Module.resources?.curse
                               ?? Module.resources?.repository
                               ?? Module.resources?.bugtracker
                               ?? Module.resources?.discussions;
                return resource?.ToString() ?? "No resource link";
            }
        }

        public string Kind { get; }

        public string Source { get; }

        public string RelatedMods
        {
            get
            {
                var separator = Source.IndexOf(':');
                return separator >= 0 && separator + 1 < Source.Length
                    ? Source[(separator + 1)..].Trim()
                    : Source;
            }
        }

        public bool CanQueue => !Module.IsDLC;

        public bool IsSelected
        {
            get => isSelected;
            set => this.RaiseAndSetIfChanged(ref isSelected, value && CanQueue);
        }

        public bool IsDetailSelected
        {
            get => isDetailSelected;
            set
            {
                if (isDetailSelected == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref isDetailSelected, value);
                this.RaisePropertyChanged(nameof(DetailRowBackground));
                this.RaisePropertyChanged(nameof(DetailRowBorderBrush));
            }
        }

        public string DetailRowBackground
            => IsDetailSelected ? "#1D2630" : "#111820";

        public string DetailRowBorderBrush
            => IsDetailSelected ? "#D35AEC" : "#242B33";

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

    public sealed class RecommendationAuditGroupHeader
    {
        public RecommendationAuditGroupHeader(string title)
        {
            Title = title;
        }

        public string Title { get; }
    }
}
