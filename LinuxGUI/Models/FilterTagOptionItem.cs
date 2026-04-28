using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public sealed class FilterTagOptionItem : ReactiveObject
    {
        private bool isSelected;

        public FilterTagOptionItem(string name,
                                   int    count,
                                   bool   isSelected)
        {
            Name = name;
            Count = count;
            this.isSelected = isSelected;
        }

        public string Name { get; }

        public int Count { get; }

        public string CountLabel => Count.ToString("N0");

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (this.RaiseAndSetIfChanged(ref isSelected, value))
                {
                    this.RaisePropertyChanged(nameof(BackgroundBrush));
                    this.RaisePropertyChanged(nameof(BorderBrush));
                    this.RaisePropertyChanged(nameof(NameForeground));
                    this.RaisePropertyChanged(nameof(CountForeground));
                }
            }
        }

        public string BackgroundBrush => IsSelected ? "#355779" : "#161B21";

        public string BorderBrush => IsSelected ? "#5A86B4" : "#34404C";

        public string NameForeground => IsSelected ? "#F4F8FC" : "#DCE5EF";

        public string CountForeground => IsSelected ? "#D7E7F8" : "#92A4B8";
    }
}
