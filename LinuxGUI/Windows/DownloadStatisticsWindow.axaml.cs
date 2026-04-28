using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Controls;

namespace CKAN.LinuxGUI
{
    public partial class DownloadStatisticsWindow : Window
    {
        public DownloadStatisticsWindow()
        {
            InitializeComponent();
            DataContext = new WindowViewModel(new Dictionary<string, long>());
        }

        public DownloadStatisticsWindow(NetModuleCache? cache,
                                        Registry?       registry)
        {
            InitializeComponent();
            var data = cache == null || registry == null
                ? new Dictionary<string, long>()
                : cache.CachedFileSizeByHost(registry.GetDownloadUrlsByHash());
            DataContext = new WindowViewModel(data);
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel
        {
            public WindowViewModel(IReadOnlyDictionary<string, long> bytesPerHost)
            {
                Rows = new ObservableCollection<HostDownloadRow>(
                    bytesPerHost.OrderByDescending(kvp => kvp.Value)
                                .Select(kvp => new HostDownloadRow(kvp.Key, kvp.Value)));
            }

            public ObservableCollection<HostDownloadRow> Rows { get; }

            public string SummaryText
                => Rows.Count switch
                {
                    0 => "No cached download statistics are available yet.",
                    1 => "1 host has cached downloads.",
                    _ => $"{Rows.Count} hosts have cached downloads.",
                };
        }

        private sealed class HostDownloadRow
        {
            public HostDownloadRow(string host,
                                   long   sizeBytes)
            {
                Host = host;
                SizeLabel = CkanModule.FmtSize(sizeBytes);
            }

            public string Host { get; }

            public string SizeLabel { get; }
        }
    }
}
