using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CKAN.App.Models
{
    public sealed class ModListItem : INotifyPropertyChanged
    {
        private string queueStateLabel       = "";
        private string queueStateBackground  = "#00000000";
        private string queueStateBorderBrush = "#00000000";
        private string queueRowAccentBrush   = "#00000000";

        public string Identifier { get; init; } = "";

        public string Name { get; init; } = "";

        public string Author { get; init; } = "";

        public string Summary { get; init; } = "";

        public string Description { get; init; } = "";

        public string License { get; init; } = "";

        public string Languages { get; init; } = "";

        public string Depends { get; init; } = "";

        public string Recommends { get; init; } = "";

        public string Suggests { get; init; } = "";

        public string Conflicts { get; init; } = "";

        public string Supports { get; init; } = "";

        public string Tags { get; init; } = "";

        public string Labels { get; init; } = "";

        public string LatestVersion { get; init; } = "";

        public string InstalledVersion { get; init; } = "";

        public bool ShowInstalledVersionInList
            => IsInstalled
               && !string.IsNullOrWhiteSpace(InstalledVersion)
               && !string.Equals(InstalledVersion, LatestVersion, StringComparison.Ordinal);

        public string ReleaseDate { get; init; } = "";

        public DateTime? ReleaseDateValue { get; init; }

        public int? DownloadCount { get; init; }

        public string DownloadCountLabel { get; init; } = "";

        public bool IsInstalled { get; init; }

        public bool IsAutodetected { get; init; }

        public bool HasUpdate { get; init; }

        public bool HasVersionUpdate { get; init; }

        public bool IsIncompatible { get; init; }

        public bool IsCached { get; init; }

        public bool HasReplacement { get; init; }

        public string Compatibility { get; init; } = "";

        public string PrimaryStateLabel { get; init; } = "";

        public string PrimaryStateColor { get; init; } = "#3B4653";

        public string SecondaryStateLabel { get; init; } = "";

        public string SecondaryStateBackground { get; init; } = "#39424E";

        public string SecondaryStateBorderBrush { get; init; } = "#607286";

        public bool HasSecondaryState => !string.IsNullOrWhiteSpace(SecondaryStateLabel);

        public string TertiaryStateLabel { get; init; } = "";

        public string TertiaryStateBackground { get; init; } = "#31424F";

        public string TertiaryStateBorderBrush { get; init; } = "#4C6A86";

        public bool HasTertiaryState => !string.IsNullOrWhiteSpace(TertiaryStateLabel);

        public string StatusSummary { get; init; } = "";

        public bool HasStatusSummary { get; init; }

        public string QueueStateLabel
        {
            get => queueStateLabel;
            set
            {
                if (queueStateLabel != value)
                {
                    queueStateLabel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasQueueState));
                }
            }
        }

        public string QueueStateBackground
        {
            get => queueStateBackground;
            set
            {
                if (queueStateBackground != value)
                {
                    queueStateBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        public string QueueStateBorderBrush
        {
            get => queueStateBorderBrush;
            set
            {
                if (queueStateBorderBrush != value)
                {
                    queueStateBorderBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public string QueueRowAccentBrush
        {
            get => queueRowAccentBrush;
            set
            {
                if (queueRowAccentBrush != value)
                {
                    queueRowAccentBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasQueueState => !string.IsNullOrWhiteSpace(QueueStateLabel);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
