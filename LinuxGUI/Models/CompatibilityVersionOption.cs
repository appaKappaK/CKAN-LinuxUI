using CKAN.Versioning;

using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public sealed class CompatibilityVersionOption : ReactiveObject
    {
        private bool isSelected;

        public CompatibilityVersionOption(GameVersion version,
                                          bool        isSelected)
        {
            Version = version;
            Label = version.ToString() ?? "";
            this.isSelected = isSelected;
        }

        public GameVersion Version { get; }

        public string Label { get; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (this.RaiseAndSetIfChanged(ref isSelected, value))
                {
                    this.RaisePropertyChanged(nameof(DisplayLabel));
                    this.RaisePropertyChanged(nameof(Background));
                    this.RaisePropertyChanged(nameof(BorderBrush));
                }
            }
        }

        public string DisplayLabel
            => IsSelected ? $"✓ {Label}" : Label;

        public string Background
            => IsSelected ? "#355779" : "#1B2128";

        public string BorderBrush
            => IsSelected ? "#5A86B4" : "#2B323C";
    }
}
