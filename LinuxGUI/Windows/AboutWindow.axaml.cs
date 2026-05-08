using System.Collections.ObjectModel;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            DataContext = new WindowViewModel();
        }

        private void AboutLinkButton_OnClick(object? sender,
                                             Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button { Tag: string url })
            {
                if (DataContext is WindowViewModel viewModel)
                {
                    viewModel.StatusMessage = Utilities.ProcessStartURL(url)
                        ? $"Opened {url}."
                        : $"Could not open {url}.";
                }
            }
        }

        private sealed class WindowViewModel : ReactiveObject
        {
            private string statusMessage = "";

            public WindowViewModel()
            {
                VersionText = $"Version {Meta.GetVersion()}";
                Links = new ObservableCollection<AboutLinkItem>
                {
                    new("License",      "https://github.com/KSP-CKAN/CKAN/blob/master/LICENSE.md"),
                    new("Authors",      "https://github.com/KSP-CKAN/CKAN/graphs/contributors"),
                    new("Source",       "https://github.com/KSP-CKAN/CKAN/"),
                    new("Forum Thread", "http://forum.kerbalspaceprogram.com/index.php?/topic/197082-ckan"),
                    new("Homepage",     "http://ksp-ckan.space"),
                };
            }

            public string VersionText { get; }

            public ObservableCollection<AboutLinkItem> Links { get; }

            public string StatusMessage
            {
                get => statusMessage;
                set
                {
                    this.RaiseAndSetIfChanged(ref statusMessage, value);
                    this.RaisePropertyChanged(nameof(ShowStatusMessage));
                }
            }

            public bool ShowStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
        }
    }

    public sealed record AboutLinkItem(string Label, string Url);
}
