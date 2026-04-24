using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.Configuration;
using CKAN.Extensions;

namespace CKAN.LinuxGUI
{
    public partial class SettingsWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public SettingsWindow()
            : this(null)
        {
        }

        public SettingsWindow(MainWindowViewModel? mainWindowViewModel)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(mainWindowViewModel);
            DataContext = viewModel;
            viewModel.RefreshCacheSummary();
        }

        private async void CheckForUpdatesButton_OnClick(object? sender,
                                                         Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.CheckForUpdatesAsync();

        private async void ChangeCachePathButton_OnClick(object? sender,
                                                         Avalonia.Interactivity.RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose Download Cache Folder",
                AllowMultiple = false,
            });

            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                await viewModel.TryChangeCachePathAsync(path);
            }
        }

        private async void PurgeCacheButton_OnClick(object? sender,
                                                    Avalonia.Interactivity.RoutedEventArgs e)
        {
            var choice = await new SimplePromptWindow(
                $"Delete all files from the download cache?\n\n{viewModel.DownloadCacheSummary}",
                new[] { "Delete", "Cancel" },
                "OK",
                "Cancel").ShowDialog<int>(this);
            if (choice == 0)
            {
                viewModel.PurgeCache();
            }
        }

        private void OpenCacheButton_OnClick(object? sender,
                                             Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.OpenCacheFolder();

        private sealed class WindowViewModel : ReactiveObject
        {
            private readonly IConfiguration? configuration;
            private readonly GameInstanceManager? manager;
            private readonly Registry? registry;
            private readonly GameInstance? instance;
            private string latestVersion = "Not checked";
            private string downloadCacheSummary = "Calculating...";
            private string cacheErrorMessage = "";
            private string cacheSizeLimitMiBText = "";
            private string refreshRateText = "0";
            private string? selectedLanguage;
            private ReleaseStatusOption? selectedStabilityTolerance;

            public WindowViewModel(MainWindowViewModel? mainWindowViewModel)
            {
                configuration = mainWindowViewModel?.CurrentConfiguration;
                manager = mainWindowViewModel?.CurrentManager;
                registry = mainWindowViewModel?.CurrentRegistry;
                instance = mainWindowViewModel?.CurrentInstance;

                RepositoryRows = registry?.Repositories.Values
                    .OrderBy(repo => repo.priority)
                    .Select(repo => new RepositoryRow(repo.name, repo.uri?.ToString() ?? ""))
                    .ToList()
                    ?? new List<RepositoryRow>();
                AuthTokenRows = configuration?.GetAuthTokenHosts()
                    .OrderBy(host => host, StringComparer.OrdinalIgnoreCase)
                    .Select(host => new AuthTokenRow(host,
                                                     configuration.TryGetAuthToken(host, out var token)
                                                         ? token ?? ""
                                                         : ""))
                    .ToList()
                    ?? new List<AuthTokenRow>();
                LocalVersion = Meta.ReleaseVersion.ToString();
                DownloadCachePath = configuration?.DownloadCacheDir
                                    ?? GameInstanceManager.DefaultDownloadCacheDir;
                cacheSizeLimitMiBText = configuration?.CacheSizeLimit is long bytes
                    ? (bytes / 1024 / 1024).ToString()
                    : "";
                refreshRateText = configuration?.RefreshRate.ToString() ?? "0";
                LanguageOptions = Utilities.AvailableLanguages;
                selectedLanguage = configuration?.Language;
                StabilityToleranceOptions = Enum.GetValues(typeof(ReleaseStatus))
                    .OfType<ReleaseStatus>()
                    .OrderBy(status => (int)status)
                    .Select(status => new ReleaseStatusOption(status))
                    .ToList();
                selectedStabilityTolerance = StabilityToleranceOptions.FirstOrDefault(
                    option => option.Value == instance?.StabilityToleranceConfig.OverallStabilityTolerance);
                if (selectedStabilityTolerance == null && StabilityToleranceOptions.Count > 0)
                {
                    selectedStabilityTolerance = StabilityToleranceOptions[0];
                }
            }

            public IReadOnlyList<RepositoryRow> RepositoryRows { get; }

            public IReadOnlyList<AuthTokenRow> AuthTokenRows { get; }

            public string LocalVersion { get; }

            public string LatestVersion
            {
                get => latestVersion;
                private set => this.RaiseAndSetIfChanged(ref latestVersion, value);
            }

            public bool UseDevBuilds
            {
                get => configuration?.DevBuilds ?? false;
                set
                {
                    if (configuration != null)
                    {
                        configuration.DevBuilds = value;
                        this.RaisePropertyChanged();
                    }
                }
            }

            public string DownloadCachePath { get; private set; }

            public string DownloadCacheSummary
            {
                get => downloadCacheSummary;
                private set => this.RaiseAndSetIfChanged(ref downloadCacheSummary, value);
            }

            public string CacheSizeLimitMiBText
            {
                get => cacheSizeLimitMiBText;
                set
                {
                    this.RaiseAndSetIfChanged(ref cacheSizeLimitMiBText, value);
                    if (configuration == null)
                    {
                        return;
                    }
                    configuration.CacheSizeLimit = long.TryParse(value, out var mib) && mib > 0
                        ? mib * 1024 * 1024
                        : null;
                }
            }

            public string CacheErrorMessage
            {
                get => cacheErrorMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref cacheErrorMessage, value);
                    this.RaisePropertyChanged(nameof(ShowCacheError));
                }
            }

            public bool ShowCacheError => !string.IsNullOrWhiteSpace(CacheErrorMessage);

            public string RefreshRateText
            {
                get => refreshRateText;
                set
                {
                    this.RaiseAndSetIfChanged(ref refreshRateText, value);
                    if (configuration != null)
                    {
                        configuration.RefreshRate = int.TryParse(value, out var minutes) && minutes > 0
                            ? minutes
                            : 0;
                    }
                }
            }

            public IReadOnlyList<string> LanguageOptions { get; }

            public string? SelectedLanguage
            {
                get => selectedLanguage;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedLanguage, value);
                    if (configuration != null)
                    {
                        configuration.Language = value;
                    }
                }
            }

            public IReadOnlyList<ReleaseStatusOption> StabilityToleranceOptions { get; }

            public ReleaseStatusOption? SelectedStabilityTolerance
            {
                get => selectedStabilityTolerance;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedStabilityTolerance, value);
                    if (instance != null && value != null)
                    {
                        instance.StabilityToleranceConfig.OverallStabilityTolerance = value.Value;
                    }
                }
            }

            public bool CanEditStabilityTolerance => instance != null;

            public async Task CheckForUpdatesAsync()
            {
                try
                {
                    LatestVersion = "Checking...";
                    var useDevBuilds = UseDevBuilds;
                    var update = await Task.Run(() => new AutoUpdate().GetUpdate(useDevBuilds));
                    LatestVersion = update.Version?.ToString() ?? "Unavailable";
                }
                catch (Exception ex)
                {
                    LatestVersion = "Unavailable";
                    CacheErrorMessage = ex.Message;
                }
            }

            public async Task TryChangeCachePathAsync(string path)
            {
                if (manager == null)
                {
                    CacheErrorMessage = "No game instance manager is available.";
                    return;
                }

                CacheErrorMessage = "";
                DownloadCacheSummary = "Moving cache...";
                var changed = await Task.Run(() => manager.TrySetupCache(path,
                                                                         new Progress<int>(_ => { }),
                                                                         out var failureReason)
                                             ? ""
                                             : failureReason ?? "Cache location was not changed.");
                if (changed.Length > 0)
                {
                    CacheErrorMessage = changed;
                }
                DownloadCachePath = configuration?.DownloadCacheDir
                                    ?? GameInstanceManager.DefaultDownloadCacheDir;
                this.RaisePropertyChanged(nameof(DownloadCachePath));
                RefreshCacheSummary();
            }

            public void PurgeCache()
            {
                try
                {
                    manager?.Cache?.RemoveAll();
                    RefreshCacheSummary();
                }
                catch (Exception ex)
                {
                    CacheErrorMessage = ex.Message;
                }
            }

            public void OpenCacheFolder()
            {
                try
                {
                    Directory.CreateDirectory(DownloadCachePath);
                    Utilities.OpenFileBrowser(DownloadCachePath);
                }
                catch (Exception ex)
                {
                    CacheErrorMessage = ex.Message;
                }
            }

            public void RefreshCacheSummary()
            {
                if (manager?.Cache == null)
                {
                    DownloadCacheSummary = "Cache is not available.";
                    return;
                }

                Task.Run(() =>
                {
                    try
                    {
                        manager.Cache.GetSizeInfo(out var fileCount,
                                                  out var bytes,
                                                  out var freeBytes);
                        var summary = freeBytes.HasValue
                            ? $"{fileCount} files, {CkanModule.FmtSize(bytes)}, {CkanModule.FmtSize(freeBytes.Value)} free"
                            : $"{fileCount} files, {CkanModule.FmtSize(bytes)}, free space unknown";
                        Dispatcher.UIThread.Post(() =>
                        {
                            CacheErrorMessage = "";
                            DownloadCacheSummary = summary;
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            CacheErrorMessage = ex.Message;
                            DownloadCacheSummary = "Cache summary unavailable.";
                        });
                    }
                });
            }
        }

        private sealed record RepositoryRow(string Name, string Url);

        private sealed record AuthTokenRow(string Host, string Token);

        private sealed class ReleaseStatusOption
        {
            public ReleaseStatusOption(ReleaseStatus value)
            {
                Value = value;
            }

            public ReleaseStatus Value { get; }

            public override string ToString()
                => $"{Value.LocalizeName()} - {Value.LocalizeDescription()}";
        }
    }
}
