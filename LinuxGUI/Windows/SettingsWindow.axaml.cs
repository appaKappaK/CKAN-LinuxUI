using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        public bool RepositoryAdded => viewModel.RepositoryAdded;

        public bool RepositoryRemoved => viewModel.RepositoryRemoved;

        public bool RepositoryMoved => viewModel.RepositoryMoved;

        public static bool CheckForUpdatesOnLaunchEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.CheckForUpdatesOnLaunch,
                             false);

        public static bool RefreshOnStartupEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.RefreshOnStartup,
                             true);

        public static bool RefreshPausedEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.RefreshPaused,
                             false);

        public static bool HideEpochsEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.HideEpochs,
                             true);

        public static bool HideVEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.HideV,
                             false);

        public static bool AutoSortByUpdateEnabled(GameInstance? instance)
            => GetGuiSetting(instance,
                             configuration => configuration.AutoSortByUpdate,
                             true);

        private static bool GetGuiSetting(GameInstance? instance,
                                          Func<LinuxGuiConfiguration, bool> getValue,
                                          bool defaultValue)
        {
            if (instance == null)
            {
                return defaultValue;
            }

            try
            {
                return getValue(LinuxGuiConfiguration.LoadOrCreate(instance));
            }
            catch
            {
                return defaultValue;
            }
        }

        private async void CheckForUpdatesButton_OnClick(object? sender,
                                                         Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.CheckForUpdatesAsync();

        private async void AddRepositoryButton_OnClick(object? sender,
                                                       Avalonia.Interactivity.RoutedEventArgs e)
        {
            viewModel.SetRepositoryStatus("Loading official repositories...");
            var repositories = await viewModel.LoadOfficialRepositoriesAsync();
            viewModel.SetRepositoryStatus("");

            var dialog = new AddRepositoryWindow(repositories);
            if (await dialog.ShowDialog<bool>(this)
                && dialog.Selection is Repository repo)
            {
                await viewModel.AddRepositoryAsync(repo);
            }
        }

        private async void RemoveRepositoryButton_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedRepositoryRow == null)
            {
                return;
            }

            var choice = await new SimplePromptWindow(
                $"Remove metadata repository \"{viewModel.SelectedRepositoryRow.Name}\"?",
                new[] { "Remove", "Cancel" },
                "Remove",
                "Cancel").ShowDialog<int>(this);
            if (choice == 0)
            {
                await viewModel.RemoveSelectedRepositoryAsync();
            }
        }

        private async void MoveRepositoryUpButton_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.MoveSelectedRepositoryAsync(-1);

        private async void MoveRepositoryDownButton_OnClick(object? sender,
                                                            Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.MoveSelectedRepositoryAsync(1);

        private async void AddAuthTokenButton_OnClick(object? sender,
                                                      Avalonia.Interactivity.RoutedEventArgs e)
        {
            var dialog = new AddAuthTokenWindow(viewModel.AuthTokenHosts);
            if (await dialog.ShowDialog<bool>(this))
            {
                viewModel.AddAuthToken(dialog.ResultHost, dialog.ResultToken);
            }
        }

        private void RemoveAuthTokenButton_OnClick(object? sender,
                                                   Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.RemoveSelectedAuthToken();

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

        private async void ResetCachePathButton_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.TryChangeCachePathAsync(GameInstanceManager.DefaultDownloadCacheDir);

        private void OpenCacheButton_OnClick(object? sender,
                                             Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.OpenCacheFolder();

        private sealed class WindowViewModel : ReactiveObject
        {
            private readonly IConfiguration? configuration;
            private readonly MainWindowViewModel? mainWindowViewModel;
            private readonly GameInstanceManager? manager;
            private RegistryManager? registryManager;
            private Registry? registry;
            private readonly GameInstance? instance;
            private readonly LinuxGuiConfiguration? guiConfiguration;
            private string latestVersion = "Not checked";
            private string downloadCacheSummary = "Calculating...";
            private string cacheErrorMessage = "";
            private string repositoryStatusMessage = "";
            private string authTokenStatusMessage = "";
            private string cacheSizeLimitMiBText = "";
            private string refreshRateText = "0";
            private string? selectedLanguage;
            private RepositoryRow? selectedRepositoryRow;
            private AuthTokenRow? selectedAuthTokenRow;
            private ReleaseStatusOption? selectedStabilityTolerance;

            public WindowViewModel(MainWindowViewModel? mainWindowViewModel)
            {
                this.mainWindowViewModel = mainWindowViewModel;
                configuration = mainWindowViewModel?.CurrentConfiguration;
                manager = mainWindowViewModel?.CurrentManager;
                registryManager = mainWindowViewModel?.CurrentRegistryManager;
                registry = registryManager?.registry ?? mainWindowViewModel?.CurrentRegistry;
                instance = mainWindowViewModel?.CurrentInstance;
                guiConfiguration = instance != null
                    ? LinuxGuiConfiguration.LoadOrCreate(instance)
                    : null;

                RepositoryRows = new ObservableCollection<RepositoryRow>();
                RefreshRepositoryRows();
                AuthTokenRows = new ObservableCollection<AuthTokenRow>();
                RefreshAuthTokenRows();
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

            public ObservableCollection<RepositoryRow> RepositoryRows { get; }

            public RepositoryRow? SelectedRepositoryRow
            {
                get => selectedRepositoryRow;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedRepositoryRow, value);
                    RaiseRepositoryButtonStateChanged();
                }
            }

            public bool RepositoryAdded { get; private set; }

            public bool RepositoryRemoved { get; private set; }

            public bool RepositoryMoved { get; private set; }

            public bool CanAddRepository => instance != null && registry != null && mainWindowViewModel != null;

            public bool CanRemoveRepository => instance != null
                                            && SelectedRepositoryRow != null
                                            && RepositoryRows.Count > 1;

            public bool CanMoveRepositoryUp => instance != null
                                            && SelectedRepositoryRow != null
                                            && RepositoryRows.IndexOf(SelectedRepositoryRow) > 0;

            public bool CanMoveRepositoryDown => instance != null
                                              && SelectedRepositoryRow != null
                                              && RepositoryRows.IndexOf(SelectedRepositoryRow) >= 0
                                              && RepositoryRows.IndexOf(SelectedRepositoryRow) < RepositoryRows.Count - 1;

            public string RepositoryStatusMessage
            {
                get => repositoryStatusMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref repositoryStatusMessage, value);
                    this.RaisePropertyChanged(nameof(ShowRepositoryStatus));
                }
            }

            public bool ShowRepositoryStatus => !string.IsNullOrWhiteSpace(RepositoryStatusMessage);

            public ObservableCollection<AuthTokenRow> AuthTokenRows { get; }

            public IReadOnlyCollection<string> AuthTokenHosts
                => AuthTokenRows.Select(row => row.Host).ToList();

            public AuthTokenRow? SelectedAuthTokenRow
            {
                get => selectedAuthTokenRow;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedAuthTokenRow, value);
                    this.RaisePropertyChanged(nameof(CanRemoveAuthToken));
                }
            }

            public bool CanAddAuthToken => configuration != null;

            public bool CanRemoveAuthToken => configuration != null && SelectedAuthTokenRow != null;

            public string AuthTokenStatusMessage
            {
                get => authTokenStatusMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref authTokenStatusMessage, value);
                    this.RaisePropertyChanged(nameof(ShowAuthTokenStatus));
                }
            }

            public bool ShowAuthTokenStatus => !string.IsNullOrWhiteSpace(AuthTokenStatusMessage);

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

            public bool CheckForUpdatesOnLaunch
            {
                get => guiConfiguration?.CheckForUpdatesOnLaunch ?? false;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.CheckForUpdatesOnLaunch = value;
                        SaveGuiConfiguration();
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
                        if (configuration.RefreshRate == 0)
                        {
                            RefreshPaused = false;
                        }
                        this.RaisePropertyChanged(nameof(CanPauseRefresh));
                    }
                }
            }

            public bool CanPauseRefresh => (configuration?.RefreshRate ?? 0) > 0;

            public bool RefreshPaused
            {
                get => guiConfiguration?.RefreshPaused ?? false;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.RefreshPaused = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                    }
                }
            }

            public bool EnableTrayIcon
            {
                get => guiConfiguration?.EnableTrayIcon ?? false;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.EnableTrayIcon = value;
                        if (!value)
                        {
                            guiConfiguration.MinimizeToTray = false;
                        }
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                        this.RaisePropertyChanged(nameof(MinimizeToTray));
                    }
                }
            }

            public bool MinimizeToTray
            {
                get => guiConfiguration?.MinimizeToTray ?? false;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.MinimizeToTray = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
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

            public bool RefreshOnStartup
            {
                get => guiConfiguration?.RefreshOnStartup ?? true;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.RefreshOnStartup = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                    }
                }
            }

            public bool HideEpochs
            {
                get => guiConfiguration?.HideEpochs ?? true;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.HideEpochs = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                    }
                }
            }

            public bool HideV
            {
                get => guiConfiguration?.HideV ?? false;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.HideV = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                    }
                }
            }

            public bool AutoSortByUpdate
            {
                get => guiConfiguration?.AutoSortByUpdate ?? true;
                set
                {
                    if (guiConfiguration != null)
                    {
                        guiConfiguration.AutoSortByUpdate = value;
                        SaveGuiConfiguration();
                        this.RaisePropertyChanged();
                    }
                }
            }

            public bool PreselectRecommendedMods
            {
                get => mainWindowViewModel?.PreselectRecommendedMods ?? false;
                set
                {
                    if (mainWindowViewModel != null)
                    {
                        mainWindowViewModel.PreselectRecommendedMods = value;
                        this.RaisePropertyChanged();
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

            public void SetRepositoryStatus(string message)
                => RepositoryStatusMessage = message;

            public Task<IReadOnlyList<Repository>> LoadOfficialRepositoriesAsync()
                => Task.Run<IReadOnlyList<Repository>>(() =>
                {
                    if (instance == null)
                    {
                        return Array.Empty<Repository>();
                    }

                    var repositories = RepositoryList.DefaultRepositories(instance.Game,
                                                                          Net.UserAgentString)
                                                     ?.repositories;
                    if (repositories is { Length: > 0 })
                    {
                        return repositories;
                    }

                    return FallbackOfficialRepositories(instance);
                });

            public async Task AddRepositoryAsync(Repository repo)
            {
                var writeManager = AcquireRepositoryWriteManager(out var disposeWhenDone);
                if (writeManager == null)
                {
                    RepositoryStatusMessage = "Repository changes are unavailable for the current instance.";
                    return;
                }

                try
                {
                    var writeRegistry = writeManager.registry;
                    if (writeRegistry.Repositories.Values.Any(other => other.uri == repo.uri))
                    {
                        RepositoryStatusMessage = $"A repository already uses {repo.uri}.";
                        return;
                    }

                    if (writeRegistry.Repositories.TryGetValue(repo.name, out Repository? existing))
                    {
                        repo.priority = existing.priority;
                        writeRegistry.RepositoriesRemove(repo.name);
                    }
                    else
                    {
                        repo.priority = writeRegistry.Repositories.Count;
                    }
                    writeRegistry.RepositoriesAdd(repo);
                    NormalizeRepositoryPriorities(writeRegistry);
                    await SaveRepositoriesAsync(writeManager);
                    RepositoryAdded = true;
                    RefreshRepositoryState(repo.name);
                    RepositoryStatusMessage = $"Added {repo.name}. The catalog will refresh when settings closes.";
                }
                catch (Exception ex)
                {
                    RepositoryStatusMessage = ex.Message;
                    RefreshRepositoryState(SelectedRepositoryRow?.Name);
                }
                finally
                {
                    DisposeTransientRegistryManager(writeManager, disposeWhenDone);
                }
            }

            public async Task RemoveSelectedRepositoryAsync()
            {
                if (SelectedRepositoryRow == null)
                {
                    return;
                }

                var writeManager = AcquireRepositoryWriteManager(out var disposeWhenDone);
                if (writeManager == null)
                {
                    RepositoryStatusMessage = "Repository changes are unavailable for the current instance.";
                    return;
                }

                var writeRegistry = writeManager.registry;
                if (writeRegistry.Repositories.Count <= 1)
                {
                    RepositoryStatusMessage = "Add another repository before removing the last one.";
                    DisposeTransientRegistryManager(writeManager, disposeWhenDone);
                    return;
                }

                var removedName = SelectedRepositoryRow.Name;
                try
                {
                    writeRegistry.RepositoriesRemove(removedName);
                    NormalizeRepositoryPriorities(writeRegistry);
                    await SaveRepositoriesAsync(writeManager);
                    RepositoryRemoved = true;
                    RefreshRepositoryState(null);
                    RepositoryStatusMessage = $"Removed {removedName}.";
                }
                catch (Exception ex)
                {
                    RepositoryStatusMessage = ex.Message;
                    RefreshRepositoryState(removedName);
                }
                finally
                {
                    DisposeTransientRegistryManager(writeManager, disposeWhenDone);
                }
            }

            public async Task MoveSelectedRepositoryAsync(int direction)
            {
                if (SelectedRepositoryRow == null)
                {
                    return;
                }

                var writeManager = AcquireRepositoryWriteManager(out var disposeWhenDone);
                if (writeManager == null)
                {
                    RepositoryStatusMessage = "Repository changes are unavailable for the current instance.";
                    return;
                }

                var writeRegistry = writeManager.registry;
                var ordered = OrderedRepositories(writeRegistry).ToList();
                var index = ordered.FindIndex(repo => string.Equals(repo.name,
                                                                    SelectedRepositoryRow.Name,
                                                                    StringComparison.Ordinal));
                var newIndex = index + direction;
                if (index < 0 || newIndex < 0 || newIndex >= ordered.Count)
                {
                    DisposeTransientRegistryManager(writeManager, disposeWhenDone);
                    return;
                }

                var selected = ordered[index];
                ordered.RemoveAt(index);
                ordered.Insert(newIndex, selected);
                for (var i = 0; i < ordered.Count; ++i)
                {
                    ordered[i].priority = i;
                }

                try
                {
                    await SaveRepositoriesAsync(writeManager);
                    RepositoryMoved = true;
                    RefreshRepositoryState(selected.name);
                    RepositoryStatusMessage = $"Moved {selected.name}.";
                }
                catch (Exception ex)
                {
                    RepositoryStatusMessage = ex.Message;
                    RefreshRepositoryState(selected.name);
                }
                finally
                {
                    DisposeTransientRegistryManager(writeManager, disposeWhenDone);
                }
            }

            public void AddAuthToken(string host,
                                     string token)
            {
                if (configuration == null)
                {
                    AuthTokenStatusMessage = "Authentication tokens are unavailable.";
                    return;
                }

                if (configuration.TryGetAuthToken(host, out _))
                {
                    AuthTokenStatusMessage = $"A token already exists for {host}.";
                    return;
                }

                configuration.SetAuthToken(host, token);
                RefreshAuthTokenRows(host);
                AuthTokenStatusMessage = "";
            }

            public void RemoveSelectedAuthToken()
            {
                if (configuration == null || SelectedAuthTokenRow == null)
                {
                    return;
                }

                var host = SelectedAuthTokenRow.Host;
                configuration.SetAuthToken(host, null);
                RefreshAuthTokenRows();
                AuthTokenStatusMessage = "";
            }

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
                this.RaisePropertyChanged(nameof(CanResetCachePath));
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

            public bool CanResetCachePath
                => DownloadCachePath != GameInstanceManager.DefaultDownloadCacheDir;

            private void RefreshRepositoryRows(string? selectName = null)
            {
                var fallbackName = selectName ?? SelectedRepositoryRow?.Name;
                RepositoryRows.Clear();
                foreach (var repo in OrderedRepositories())
                {
                    RepositoryRows.Add(new RepositoryRow(repo));
                }

                SelectedRepositoryRow = !string.IsNullOrWhiteSpace(fallbackName)
                    ? RepositoryRows.FirstOrDefault(row => string.Equals(row.Name,
                                                                         fallbackName,
                                                                         StringComparison.Ordinal))
                      ?? RepositoryRows.FirstOrDefault()
                    : RepositoryRows.FirstOrDefault();
                RaiseRepositoryButtonStateChanged();
            }

            private void RefreshAuthTokenRows(string? selectHost = null)
            {
                var fallbackHost = selectHost ?? SelectedAuthTokenRow?.Host;
                AuthTokenRows.Clear();
                if (configuration != null)
                {
                    foreach (var host in configuration.GetAuthTokenHosts()
                                                      .OrderBy(host => host, StringComparer.OrdinalIgnoreCase))
                    {
                        if (configuration.TryGetAuthToken(host, out var token))
                        {
                            AuthTokenRows.Add(new AuthTokenRow(host, token ?? ""));
                        }
                    }
                }

                SelectedAuthTokenRow = !string.IsNullOrWhiteSpace(fallbackHost)
                    ? AuthTokenRows.FirstOrDefault(row => string.Equals(row.Host,
                                                                        fallbackHost,
                                                                        StringComparison.OrdinalIgnoreCase))
                      ?? AuthTokenRows.FirstOrDefault()
                    : AuthTokenRows.FirstOrDefault();
                this.RaisePropertyChanged(nameof(CanAddAuthToken));
                this.RaisePropertyChanged(nameof(CanRemoveAuthToken));
            }

            private IEnumerable<Repository> OrderedRepositories()
                => registry?.Repositories.Values
                          .OrderBy(repo => repo.priority)
                          .ThenBy(repo => repo.name, StringComparer.OrdinalIgnoreCase)
                   ?? Enumerable.Empty<Repository>();

            private static IEnumerable<Repository> OrderedRepositories(Registry registry)
                => registry.Repositories.Values
                          .OrderBy(repo => repo.priority)
                          .ThenBy(repo => repo.name, StringComparer.OrdinalIgnoreCase);

            private static void NormalizeRepositoryPriorities(Registry registry)
            {
                var index = 0;
                foreach (var repo in registry.Repositories.Values
                                             .OrderBy(repo => repo.priority)
                                             .ThenBy(repo => repo.name, StringComparer.OrdinalIgnoreCase))
                {
                    repo.priority = index++;
                }
            }

            private RegistryManager? AcquireRepositoryWriteManager(out bool disposeWhenDone)
            {
                disposeWhenDone = false;
                var writeManager = mainWindowViewModel?.AcquireWriteRegistryManager();
                if (writeManager == null)
                {
                    return null;
                }

                disposeWhenDone = !ReferenceEquals(writeManager, mainWindowViewModel?.CurrentRegistryManager);
                return writeManager;
            }

            private static Repository[] FallbackOfficialRepositories(GameInstance instance)
            {
                if (string.Equals(instance.Game.ShortName, "KSP", StringComparison.OrdinalIgnoreCase))
                {
                    return new[]
                    {
                        new Repository("KSP-default",
                                       "https://github.com/KSP-CKAN/CKAN-meta/archive/master.tar.gz"),
                        new Repository("MechJeb2-dev",
                                       "https://ksp.sarbian.com/ckan/MechJeb2-ci.tar.gz"),
                        new Repository("Kopernicus_BE",
                                       "https://github.com/R-T-B/CKAN-meta-dev-Kopernicus_BE/archive/master.tar.gz"),
                        new Repository("Sol",
                                       "https://github.com/RSS-Reborn/CKAN-meta/archive/main.tar.gz"),
                    };
                }

                return new[] { Repository.DefaultGameRepo(instance.Game) };
            }

            private async Task SaveRepositoriesAsync(RegistryManager writeManager)
            {
                await Task.Run(() => writeManager.Save());
            }

            private void RefreshRepositoryState(string? selectName)
            {
                mainWindowViewModel?.RefreshCurrentRegistryReference();
                registryManager = mainWindowViewModel?.CurrentRegistryManager;
                registry = registryManager?.registry ?? mainWindowViewModel?.CurrentRegistry;
                RefreshRepositoryRows(selectName);
            }

            private static void DisposeTransientRegistryManager(RegistryManager writeManager,
                                                                bool            disposeWhenDone)
            {
                if (disposeWhenDone)
                {
                    writeManager.Dispose();
                }
            }

            private void RaiseRepositoryButtonStateChanged()
            {
                this.RaisePropertyChanged(nameof(CanAddRepository));
                this.RaisePropertyChanged(nameof(CanRemoveRepository));
                this.RaisePropertyChanged(nameof(CanMoveRepositoryUp));
                this.RaisePropertyChanged(nameof(CanMoveRepositoryDown));
            }

            private void SaveGuiConfiguration()
            {
                if (guiConfiguration != null)
                {
                    guiConfiguration.Save();
                }
            }
        }

        private sealed class RepositoryRow
        {
            public RepositoryRow(Repository repository)
            {
                Repository = repository;
            }

            public Repository Repository { get; }

            public string Name => Repository.name;

            public string Url => Repository.uri?.ToString() ?? "";
        }

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

        private sealed class LinuxGuiConfiguration
        {
            private LinuxGuiConfiguration(GameInstance instance, JsonObject root)
            {
                this.instance = instance;
                this.root = root;
            }

            public static LinuxGuiConfiguration LoadOrCreate(GameInstance instance)
                => new LinuxGuiConfiguration(
                    instance,
                    Utilities.DefaultIfThrows(() =>
                        JsonNode.Parse(File.ReadAllText(ConfigPath(instance))) as JsonObject)
                    ?? new JsonObject());

            public bool CheckForUpdatesOnLaunch
            {
                get => GetBool(nameof(CheckForUpdatesOnLaunch), false);
                set => SetBool(nameof(CheckForUpdatesOnLaunch), value);
            }

            public bool EnableTrayIcon
            {
                get => GetBool(nameof(EnableTrayIcon), false);
                set => SetBool(nameof(EnableTrayIcon), value);
            }

            public bool MinimizeToTray
            {
                get => GetBool(nameof(MinimizeToTray), false);
                set => SetBool(nameof(MinimizeToTray), value);
            }

            public bool HideEpochs
            {
                get => GetBool(nameof(HideEpochs), true);
                set => SetBool(nameof(HideEpochs), value);
            }

            public bool HideV
            {
                get => GetBool(nameof(HideV), false);
                set => SetBool(nameof(HideV), value);
            }

            public bool RefreshOnStartup
            {
                get => GetBool(nameof(RefreshOnStartup), true);
                set => SetBool(nameof(RefreshOnStartup), value);
            }

            public bool RefreshPaused
            {
                get => GetBool(nameof(RefreshPaused), false);
                set => SetBool(nameof(RefreshPaused), value);
            }

            public bool AutoSortByUpdate
            {
                get => GetBool(nameof(AutoSortByUpdate), true);
                set => SetBool(nameof(AutoSortByUpdate), value);
            }

            public void Save()
                => root.ToJsonString(new JsonSerializerOptions
                   {
                       WriteIndented = true,
                   }).WriteThroughTo(ConfigPath(instance));

            private bool GetBool(string key, bool defaultValue)
            {
                try
                {
                    return root[key]?.GetValue<bool>() ?? defaultValue;
                }
                catch (InvalidOperationException)
                {
                    return defaultValue;
                }
            }

            private void SetBool(string key, bool value)
                => root[key] = value;

            private static string ConfigPath(GameInstance instance)
                => Path.Combine(instance.CkanDir, "GUIConfig.json");

            private readonly GameInstance instance;
            private readonly JsonObject root;
        }
    }
}
