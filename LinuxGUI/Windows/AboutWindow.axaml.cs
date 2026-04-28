using System.Collections.ObjectModel;

using Avalonia.Controls;

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
                Utilities.ProcessStartURL(url);
            }
        }

        private sealed class WindowViewModel
        {
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
        }
    }

    public sealed record AboutLinkItem(string Label, string Url);
}
