using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.IO;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public sealed class MainWindowViewModel : ReactiveObject
    {
        private enum ModDetailsSection
        {
            Overview,
            Metadata,
            Relationships,
            Description,
        }

        private readonly IAppSettingsService  appSettingsService;
        private readonly IGameInstanceService gameInstanceService;
        private readonly IModCatalogService   modCatalogService;
        private readonly IModSearchService    modSearchService;
        private readonly IChangesetService    changesetService;
        private readonly IModActionService    modActionService;
        private readonly AvaloniaUser         user;
        private readonly ObservableAsPropertyHelper<bool> canUseSelectedInstance;
        private readonly SemaphoreSlim        catalogLoadSemaphore = new SemaphoreSlim(1, 1);

        private string? currentInstanceName = "Loading…";
        private string  statusMessage       = "Preparing the Linux shell…";
        private string  currentInstanceContext = "Select an install to open the mod browser.";
        private string  diagnostics         = "No issues reported.";
        private string  selectedInstanceSummary = "Choose an install to inspect its path and game version.";
        private string  stageTitle          = "Loading";
        private string  stageDescription    = "Loading known game instances and startup state.";
        private string  selectedActionLabel = "Open Selected Install";
        private string  selectedActionHint  = "Choose the KSP install you want to manage.";
        private string  readyInstanceHint   = "Switch installs here without leaving the mod browser.";
        private string  modSearchText       = "";
        private string  advancedAuthorFilter = "";
        private string  advancedCompatibilityFilter = "";
        private SortOptionItem? selectedSortOption;
        private bool    sortDescending;
        private string  catalogStatusMessage = "Select an instance to load the mod catalog.";
        private string  selectedModTitle     = "No mod selected";
        private string  selectedModSubtitle  = "Choose a mod to inspect its details.";
        private string  selectedModAuthors   = "";
        private string  selectedModVersions  = "";
        private string  selectedModInstallState = "";
        private string  selectedModCompatibility = "";
        private string  selectedModModuleKind = "";
        private string  selectedModLicense = "";
        private string  selectedModReleaseDate = "";
        private string  selectedModDownloadSize = "";
        private string  selectedModDownloadCount = "";
        private string  selectedModRelationships = "";
        private string  selectedModDependencyCountLabel = "";
        private string  selectedModRecommendationCountLabel = "";
        private string  selectedModSuggestionCountLabel = "";
        private string  selectedModBody      = "The details pane will show summary, description, compatibility, and install state.";
        private string  previewSummary = "Queue install, update, or remove actions to build an apply preview. Right-click a mod for download-only.";
        private string  currentExecutionStatusLabel = "Applying changes…";
        private string  applyResultTitle = "";
        private string  applyResultMessage = "";
        private string  applyResultBackground = "#20262D";
        private string  applyResultBorderBrush = "#2F3741";
        private int     progressPercent;
        private int     instanceCount;
        private bool    filterInstalledOnly;
        private bool    filterNotInstalledOnly;
        private bool    filterUpdatableOnly;
        private bool    filterCompatibleOnly;
        private bool    filterCachedOnly;
        private bool    filterUncachedOnly;
        private bool    filterIncompatibleOnly;
        private bool    filterHasReplacementOnly;
        private bool    isRefreshing;
        private bool    hasSelectedInstance;
        private bool    isCatalogLoading;
        private bool    isSelectedModLoading;
        private bool    isPreviewLoading;
        private bool    isApplyingChanges;
        private bool    isUserBusy;
        private bool    showSortOptions;
        private bool    showAdvancedFilters;
        private bool    showDisplaySettings;
        private bool    showDetailsPane = true;
        private bool    showPreviewSurface;
        private bool    uiScaleRestartStripDismissed;
        private bool    isQueueDrawerExpanded;
        private bool    queueDrawerStickyCollapsed;
        private bool    previewCanApply;
        private bool    selectedModIsInstalled;
        private bool    selectedModHasUpdate;
        private bool    selectedModIsCached;
        private bool    selectedModIsIncompatible;
        private bool    selectedModHasReplacement;
        private bool    hasFilterOptionCounts;
        private int     appliedUiScalePercent;
        private double  pendingUiScalePercent;
        private int     catalogLoadRequestId;
        private int     selectedModLoadRequestId;
        private int     modListScrollResetRequestId;
        private bool    pendingModListScrollReset;
        private FilterOptionCounts filterOptionCounts = new FilterOptionCounts();
        private ModDetailsSection selectedModDetailsSection = ModDetailsSection.Overview;
        private InstanceSummary? selectedInstance;
        private ModListItem?     selectedMod;
        private QueuedActionModel? selectedQueuedAction;
        private StartupStage     startupStage = StartupStage.Loading;

        public MainWindowViewModel(IAppSettingsService  appSettingsService,
                                   IGameInstanceService gameInstanceService,
                                   IModCatalogService   modCatalogService,
                                   IModSearchService    modSearchService,
                                   IChangesetService    changesetService,
                                   IModActionService    modActionService,
                                   AvaloniaUser         user)
        {
            this.appSettingsService   = appSettingsService;
            this.gameInstanceService = gameInstanceService;
            this.modCatalogService   = modCatalogService;
            this.modSearchService    = modSearchService;
            this.changesetService    = changesetService;
            this.modActionService    = modActionService;
            this.user                = user;

            Instances = new ObservableCollection<InstanceSummary>();
            Mods = new ObservableCollection<ModListItem>();
            QueuedActions = new ObservableCollection<QueuedActionModel>();
            CompatibleGameVersionOptions = new ObservableCollection<CompatibilityVersionOption>();
            PreviewDownloadsRequired = new ObservableCollection<string>();
            PreviewDependencies = new ObservableCollection<string>();
            PreviewAutoRemovals = new ObservableCollection<string>();
            PreviewAttentionNotes = new ObservableCollection<string>();
            PreviewRecommendations = new ObservableCollection<string>();
            PreviewSuggestions = new ObservableCollection<string>();
            PreviewConflicts = new ObservableCollection<string>();
            ApplyResultSummaryLines = new ObservableCollection<string>();
            ApplyResultFollowUpLines = new ObservableCollection<string>();
            SortOptions = new[]
            {
                new SortOptionItem { Value = ModSortOption.Name, Label = "Name" },
                new SortOptionItem { Value = ModSortOption.Author, Label = "Author" },
                new SortOptionItem { Value = ModSortOption.Popularity, Label = "Downloads" },
                new SortOptionItem { Value = ModSortOption.Compatibility, Label = "Compatibility" },
                new SortOptionItem { Value = ModSortOption.Version, Label = "Version" },
                new SortOptionItem { Value = ModSortOption.InstalledFirst, Label = "Installed First" },
                new SortOptionItem { Value = ModSortOption.UpdatesFirst, Label = "Updates First" },
            };
            selectedSortOption = SortOptions[0];
            sortDescending = DefaultSortDescending(ModSortOption.Name);
            appliedUiScalePercent = UiScaleSettings.NormalizePercent(appSettingsService.UiScalePercent);
            pendingUiScalePercent = appliedUiScalePercent;
            showDetailsPane = false;

            var canRefresh = this.WhenAnyValue(vm => vm.IsRefreshing)
                                 .CombineLatest(this.WhenAnyValue(vm => vm.IsApplyingChanges),
                                                (refreshing, applying) => !refreshing && !applying);
            var canUseSelected = this.WhenAnyValue(vm => vm.SelectedInstance,
                                                   vm => vm.IsRefreshing,
                                                   vm => vm.IsApplyingChanges,
                                                   (inst, refreshing, applying) => inst != null && !refreshing && !applying);
            var canQueueInstall = this.WhenAnyValue(vm => vm.SelectedMod,
                                                    vm => vm.IsApplyingChanges,
                                                    (mod, applying) => mod != null
                                                                       && !applying
                                                                       && !mod.IsInstalled
                                                                       && !mod.IsIncompatible);
            var canQueueUpdate = this.WhenAnyValue(vm => vm.SelectedMod,
                                                   vm => vm.IsApplyingChanges,
                                                   (mod, applying) => mod?.IsInstalled == true
                                                                      && mod.HasUpdate
                                                                      && !applying);
            var canQueueRemove = this.WhenAnyValue(vm => vm.SelectedMod,
                                                   vm => vm.IsApplyingChanges,
                                                   (mod, applying) => mod?.IsInstalled == true
                                                                      && !applying);
            var canRemoveQueuedAction = this.WhenAnyValue(vm => vm.SelectedQueuedAction,
                                                          vm => vm.IsApplyingChanges,
                                                          (action, applying) => action != null && !applying);
            var canClearQueue = this.WhenAnyValue(vm => vm.HasQueuedActions,
                                                  vm => vm.IsApplyingChanges,
                                                  (hasActions, applying) => hasActions && !applying);
            var canToggleAdvancedFilters = this.WhenAnyValue(vm => vm.IsApplyingChanges,
                                                             applying => !applying);
            var canClearFilters = this.WhenAnyValue(vm => vm.HasActiveFilters,
                                                    vm => vm.IsApplyingChanges,
                                                    (hasFilters, applying) => hasFilters && !applying);
            var canRestartForUiScale = this.WhenAnyValue(vm => vm.UiScaleNeedsRestart,
                                                         vm => vm.IsRefreshing,
                                                         vm => vm.IsApplyingChanges,
                                                         (needsRestart, refreshing, applying)
                                                             => needsRestart && !refreshing && !applying);
            var canApplyChanges = this.WhenAnyValue(vm => vm.HasQueuedChangeActions,
                                                    vm => vm.PreviewCanApply,
                                                    vm => vm.IsPreviewLoading,
                                                    vm => vm.IsApplyingChanges,
                                                    (hasChanges, canApply, previewLoading, applying)
                                                        => hasChanges && canApply && !previewLoading && !applying);
            var canDownloadQueued = this.WhenAnyValue(vm => vm.HasQueuedDownloadActions,
                                                      vm => vm.IsApplyingChanges,
                                                      (hasDownloads, applying) => hasDownloads && !applying);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
            SetCurrentInstanceCommand = ReactiveCommand.CreateFromTask(
                SetCurrentInstanceAsync,
                canUseSelected);
            QueueInstallCommand = ReactiveCommand.Create(QueueInstallSelected, canQueueInstall);
            QueueUpdateCommand = ReactiveCommand.Create(QueueUpdateSelected, canQueueUpdate);
            QueueRemoveCommand = ReactiveCommand.Create(QueueRemoveSelected, canQueueRemove);
            RemoveQueuedActionCommand = ReactiveCommand.Create(RemoveSelectedQueuedAction, canRemoveQueuedAction);
            ClearQueueCommand = ReactiveCommand.Create(ClearQueuedActions, canClearQueue);
            ToggleAdvancedFiltersCommand = ReactiveCommand.Create(ToggleAdvancedFilters, canToggleAdvancedFilters);
            ToggleSortOptionsCommand = ReactiveCommand.Create(ToggleSortOptions);
            ClearAdvancedFiltersCommand = ReactiveCommand.Create(ClearAdvancedFilters, canClearFilters);
            ClearFiltersCommand = ReactiveCommand.Create(ClearAllFilters, canClearFilters);
            ToggleDisplaySettingsCommand = ReactiveCommand.Create(ToggleDisplaySettings);
            ToggleDetailsPaneCommand = ReactiveCommand.Create(ToggleDetailsPane);
            ShowBrowseSurfaceCommand = ReactiveCommand.Create(ShowBrowseSurfaceTab);
            ShowPreviewSurfaceCommand = ReactiveCommand.Create(ShowPreviewSurfaceTab);
            ResetUiScaleCommand = ReactiveCommand.Create(ResetUiScale);
            DismissUiScaleRestartStripCommand = ReactiveCommand.Create(DismissUiScaleRestartStrip);
            RestartToApplyUiScaleCommand = ReactiveCommand.CreateFromTask(RestartToApplyUiScaleAsync,
                                                                          canRestartForUiScale);
            ApplyChangesCommand = ReactiveCommand.CreateFromTask(ApplyQueuedChangesAsync, canApplyChanges);
            DownloadQueuedCommand = ReactiveCommand.CreateFromTask(DownloadQueuedAsync, canDownloadQueued);
            ToggleQueueDrawerCommand = ReactiveCommand.Create(ToggleQueueDrawer);
            ApplyChangesFromCollapsedQueueCommand = ReactiveCommand.CreateFromTask(ApplyChangesFromCollapsedQueueAsync,
                                                                                   canApplyChanges);
            DismissApplyResultCommand = ReactiveCommand.Create(DismissApplyResult);
            OpenCurrentGameDirectoryCommand = ReactiveCommand.Create(OpenCurrentGameDirectory,
                                                                    this.WhenAnyValue(vm => vm.HasCurrentInstance));
            OpenUserGuideCommand = ReactiveCommand.Create(OpenUserGuide);
            OpenDiscordCommand = ReactiveCommand.Create(OpenDiscord);
            OpenGameSupportCommand = ReactiveCommand.Create(OpenGameSupport,
                                                           this.WhenAnyValue(vm => vm.HasCurrentInstance));
            ReportClientIssueCommand = ReactiveCommand.Create(ReportClientIssue);
            ReportMetadataIssueCommand = ReactiveCommand.Create(ReportMetadataIssue,
                                                               this.WhenAnyValue(vm => vm.HasCurrentInstance));
            PrimarySelectedModActionCommand = ReactiveCommand.Create(
                ExecutePrimarySelectedModAction,
                this.WhenAnyValue(vm => vm.ShowPrimarySelectedModAction,
                                  vm => vm.IsApplyingChanges,
                                  (showAction, applying) => showAction && !applying));
            InstallNowSelectedModCommand = ReactiveCommand.CreateFromTask(
                InstallNowSelectedModAsync,
                this.WhenAnyValue(vm => vm.ShowInstallNowAction));
            RemoveNowSelectedModCommand = ReactiveCommand.CreateFromTask(
                RemoveNowSelectedModAsync,
                this.WhenAnyValue(vm => vm.ShowRemoveNowAction));
            ShowOverviewDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Overview));
            ShowMetadataDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Metadata));
            ShowRelationshipsDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Relationships));
            ShowDescriptionDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Description));
            SelectNameSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Name));
            SelectAuthorSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Author));
            SelectPopularitySortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Popularity));
            SelectCompatibilitySortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Compatibility));
            SelectVersionSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Version));
            SelectInstalledFirstSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.InstalledFirst));
            SelectUpdatesFirstSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.UpdatesFirst));
            canUseSelectedInstance = canUseSelected
                .ToProperty(this, vm => vm.CanUseSelectedInstance);

            user.WhenAnyValue(u => u.LastMessage)
                .Subscribe(msg =>
                {
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        StatusMessage = msg;
                    }
                });
            user.WhenAnyValue(u => u.LastError)
                .Subscribe(msg =>
                {
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        Diagnostics = msg;
                    }
                });
            user.WhenAnyValue(u => u.ProgressPercent)
                .Subscribe(pct => ProgressPercent = pct);
            user.WhenAnyValue(u => u.IsBusy)
                .Subscribe(busy =>
                {
                    IsUserBusy = busy;
                    if (busy)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(user.LastMessage)
                            ? "Working…"
                            : user.LastMessage;
                    }
                });

            Observable.Merge(
                    this.WhenAnyValue(vm => vm.ModSearchText).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedAuthorFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedCompatibilityFilter).Select(_ => Unit.Default))
                .Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                .Subscribe(__ =>
                {
                    RefreshCatalogForFilterChange();
                });

            Observable.Merge(
                    this.WhenAnyValue(vm => vm.FilterInstalledOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterNotInstalledOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterUpdatableOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterCompatibleOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterCachedOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterUncachedOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterIncompatibleOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterHasReplacementOnly).Select(_ => Unit.Default))
                .Skip(1)
                .Subscribe(__ => RefreshCatalogForFilterChange());

            this.WhenAnyValue(vm => vm.SelectedSortOption, vm => vm.SortDescending)
                .Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(50), RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    modSearchService.SetCurrent(CurrentFilter());
                    PublishFilterStateLabels();
                    if (IsReady)
                    {
                        ApplyCurrentSortToVisibleMods();
                    }
                });

            gameInstanceService.CurrentInstanceChanged += OnCurrentInstanceChanged;
            changesetService.QueueChanged += OnQueueChanged;

            ApplyStoredFilterState(modSearchService.Current);
            RefreshQueuedActions();
            _ = RefreshAsync();
        }

        public ObservableCollection<InstanceSummary> Instances { get; }

        public ObservableCollection<ModListItem> Mods { get; }

        public ObservableCollection<QueuedActionModel> QueuedActions { get; }

        public ObservableCollection<CompatibilityVersionOption> CompatibleGameVersionOptions { get; }

        public ObservableCollection<string> PreviewDownloadsRequired { get; }

        public ObservableCollection<string> PreviewDependencies { get; }

        public ObservableCollection<string> PreviewAutoRemovals { get; }

        public ObservableCollection<string> PreviewAttentionNotes { get; }

        public ObservableCollection<string> PreviewRecommendations { get; }

        public ObservableCollection<string> PreviewSuggestions { get; }

        public ObservableCollection<string> PreviewConflicts { get; }

        public ObservableCollection<string> ApplyResultSummaryLines { get; }

        public ObservableCollection<string> ApplyResultFollowUpLines { get; }

        public IReadOnlyList<SortOptionItem> SortOptions { get; }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public ReactiveCommand<Unit, Unit> SetCurrentInstanceCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueInstallCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueUpdateCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueRemoveCommand { get; }

        public ReactiveCommand<Unit, Unit> RemoveQueuedActionCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearQueueCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleAdvancedFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSortOptionsCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearAdvancedFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleDisplaySettingsCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleDetailsPaneCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowBrowseSurfaceCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowPreviewSurfaceCommand { get; }

        public ReactiveCommand<Unit, Unit> ResetUiScaleCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissUiScaleRestartStripCommand { get; }

        public ReactiveCommand<Unit, Unit> RestartToApplyUiScaleCommand { get; }

        public ReactiveCommand<Unit, Unit> ApplyChangesCommand { get; }

        public ReactiveCommand<Unit, Unit> DownloadQueuedCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleQueueDrawerCommand { get; }

        public ReactiveCommand<Unit, Unit> ApplyChangesFromCollapsedQueueCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissApplyResultCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenCurrentGameDirectoryCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenUserGuideCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenGameSupportCommand { get; }

        public ReactiveCommand<Unit, Unit> ReportClientIssueCommand { get; }

        public ReactiveCommand<Unit, Unit> ReportMetadataIssueCommand { get; }

        public ReactiveCommand<Unit, Unit> PrimarySelectedModActionCommand { get; }

        public ReactiveCommand<Unit, Unit> InstallNowSelectedModCommand { get; }

        public ReactiveCommand<Unit, Unit> RemoveNowSelectedModCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowOverviewDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowMetadataDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowRelationshipsDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowDescriptionDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectNameSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectAuthorSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectPopularitySortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectCompatibilitySortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectVersionSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectInstalledFirstSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectUpdatesFirstSortCommand { get; }

        public int ModListScrollResetRequestId
        {
            get => modListScrollResetRequestId;
            private set => this.RaiseAndSetIfChanged(ref modListScrollResetRequestId, value);
        }

        public StartupStage StartupStage
        {
            get => startupStage;
            private set
            {
                this.RaiseAndSetIfChanged(ref startupStage, value);
                this.RaisePropertyChanged(nameof(IsLoading));
                this.RaisePropertyChanged(nameof(IsEmpty));
                this.RaisePropertyChanged(nameof(NeedsSelection));
                this.RaisePropertyChanged(nameof(IsReady));
                this.RaisePropertyChanged(nameof(HasError));
                this.RaisePropertyChanged(nameof(ShowLegacyHeader));
                this.RaisePropertyChanged(nameof(ShowReadyHeader));
                this.RaisePropertyChanged(nameof(ShowHeaderStagePill));
                this.RaisePropertyChanged(nameof(ShowHeaderInstanceSwitcher));
                this.RaisePropertyChanged(nameof(ShowPassiveHeaderInstanceLabel));
                this.RaisePropertyChanged(nameof(ShowLegacyShell));
                this.RaisePropertyChanged(nameof(ShowReadyShell));
                this.RaisePropertyChanged(nameof(LegacySidebarWidth));
                this.RaisePropertyChanged(nameof(ShowStartupInstancePanel));
                this.RaisePropertyChanged(nameof(ShowReadyInstancePanel));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(StatusSurfaceBackground));
                this.RaisePropertyChanged(nameof(StatusSurfaceBorderBrush));
            }
        }

        public string? CurrentInstanceName
        {
            get => currentInstanceName;
            private set => this.RaiseAndSetIfChanged(ref currentInstanceName, value);
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set
            {
                this.RaiseAndSetIfChanged(ref statusMessage, value);
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(StatusSurfaceBackground));
                this.RaisePropertyChanged(nameof(StatusSurfaceBorderBrush));
            }
        }

        public string CurrentInstanceContext
        {
            get => currentInstanceContext;
            private set => this.RaiseAndSetIfChanged(ref currentInstanceContext, value);
        }

        public string Diagnostics
        {
            get => diagnostics;
            private set => this.RaiseAndSetIfChanged(ref diagnostics, value);
        }

        public string StageTitle
        {
            get => stageTitle;
            private set => this.RaiseAndSetIfChanged(ref stageTitle, value);
        }

        public string StageDescription
        {
            get => stageDescription;
            private set => this.RaiseAndSetIfChanged(ref stageDescription, value);
        }

        public string SelectedInstanceSummary
        {
            get => selectedInstanceSummary;
            private set => this.RaiseAndSetIfChanged(ref selectedInstanceSummary, value);
        }

        public string SelectedInstanceNameLabel
            => SelectedInstance?.Name ?? "No instance selected";

        public string SelectedInstanceGameNameLabel
            => SelectedInstance?.GameName ?? "";

        public string SelectedInstanceGameDirLabel
            => SelectedInstance?.GameDir ?? "";

        public string SelectedActionLabel
        {
            get => selectedActionLabel;
            private set => this.RaiseAndSetIfChanged(ref selectedActionLabel, value);
        }

        public string SelectedActionHint
        {
            get => selectedActionHint;
            private set => this.RaiseAndSetIfChanged(ref selectedActionHint, value);
        }

        public string ReadyInstanceHint
        {
            get => readyInstanceHint;
            private set => this.RaiseAndSetIfChanged(ref readyInstanceHint, value);
        }

        public string ModSearchText
        {
            get => modSearchText;
            set
            {
                this.RaiseAndSetIfChanged(ref modSearchText, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedAuthorFilter
        {
            get => advancedAuthorFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedAuthorFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedCompatibilityFilter
        {
            get => advancedCompatibilityFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedCompatibilityFilter, value);
                PublishFilterStateLabels();
            }
        }

        public SortOptionItem? SelectedSortOption
        {
            get => selectedSortOption;
            set
            {
                bool changed = selectedSortOption?.Value != value?.Value;
                this.RaiseAndSetIfChanged(ref selectedSortOption, value);
                if (changed && value != null)
                {
                    sortDescending = DefaultSortDescending(value.Value);
                    this.RaisePropertyChanged(nameof(SortDescending));
                }
                PublishFilterStateLabels();
            }
        }

        public bool SortDescending
        {
            get => sortDescending;
            set
            {
                this.RaiseAndSetIfChanged(ref sortDescending, value);
                PublishFilterStateLabels();
            }
        }

        public bool ShowSortOptions
        {
            get => showSortOptions;
            set => this.RaiseAndSetIfChanged(ref showSortOptions, value);
        }

        public string CatalogStatusMessage
        {
            get => catalogStatusMessage;
            private set => this.RaiseAndSetIfChanged(ref catalogStatusMessage, value);
        }

        public string SelectedModTitle
        {
            get => selectedModTitle;
            private set => this.RaiseAndSetIfChanged(ref selectedModTitle, value);
        }

        public string SelectedModSubtitle
        {
            get => selectedModSubtitle;
            private set => this.RaiseAndSetIfChanged(ref selectedModSubtitle, value);
        }

        public string SelectedModAuthors
        {
            get => selectedModAuthors;
            private set => this.RaiseAndSetIfChanged(ref selectedModAuthors, value);
        }

        public string SelectedModVersions
        {
            get => selectedModVersions;
            private set => this.RaiseAndSetIfChanged(ref selectedModVersions, value);
        }

        public string SelectedModInstallState
        {
            get => selectedModInstallState;
            private set => this.RaiseAndSetIfChanged(ref selectedModInstallState, value);
        }

        public string SelectedModCompatibility
        {
            get => selectedModCompatibility;
            private set => this.RaiseAndSetIfChanged(ref selectedModCompatibility, value);
        }

        public string SelectedModModuleKind
        {
            get => selectedModModuleKind;
            private set => this.RaiseAndSetIfChanged(ref selectedModModuleKind, value);
        }

        public string SelectedModLicense
        {
            get => selectedModLicense;
            private set => this.RaiseAndSetIfChanged(ref selectedModLicense, value);
        }

        public string SelectedModReleaseDate
        {
            get => selectedModReleaseDate;
            private set => this.RaiseAndSetIfChanged(ref selectedModReleaseDate, value);
        }

        public string SelectedModDownloadSize
        {
            get => selectedModDownloadSize;
            private set => this.RaiseAndSetIfChanged(ref selectedModDownloadSize, value);
        }

        public string SelectedModDownloadCount
        {
            get => selectedModDownloadCount;
            private set => this.RaiseAndSetIfChanged(ref selectedModDownloadCount, value);
        }

        public string SelectedModRelationships
        {
            get => selectedModRelationships;
            private set => this.RaiseAndSetIfChanged(ref selectedModRelationships, value);
        }

        public string SelectedModDependencyCountLabel
        {
            get => selectedModDependencyCountLabel;
            private set => this.RaiseAndSetIfChanged(ref selectedModDependencyCountLabel, value);
        }

        public string SelectedModRecommendationCountLabel
        {
            get => selectedModRecommendationCountLabel;
            private set => this.RaiseAndSetIfChanged(ref selectedModRecommendationCountLabel, value);
        }

        public string SelectedModSuggestionCountLabel
        {
            get => selectedModSuggestionCountLabel;
            private set => this.RaiseAndSetIfChanged(ref selectedModSuggestionCountLabel, value);
        }

        public string SelectedModBody
        {
            get => selectedModBody;
            private set => this.RaiseAndSetIfChanged(ref selectedModBody, value);
        }

        public int ProgressPercent
        {
            get => progressPercent;
            private set => this.RaiseAndSetIfChanged(ref progressPercent, value);
        }

        public int InstanceCount
        {
            get => instanceCount;
            private set => this.RaiseAndSetIfChanged(ref instanceCount, value);
        }

        public bool IsRefreshing
        {
            get => isRefreshing;
            private set
            {
                this.RaiseAndSetIfChanged(ref isRefreshing, value);
                this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
            }
        }

        public bool IsLoading => StartupStage == StartupStage.Loading;
        public bool IsEmpty => StartupStage == StartupStage.Empty;
        public bool NeedsSelection => StartupStage == StartupStage.SelectionRequired;
        public bool IsReady => StartupStage == StartupStage.Ready;
        public bool HasError => StartupStage == StartupStage.Error;

        public bool ShowLegacyHeader => !IsReady;

        public bool ShowReadyHeader => IsReady;

        public bool ShowHeaderStagePill => !IsReady;

        public bool ShowHeaderInstanceSwitcher => IsReady && InstanceCount > 1;

        public bool ShowPassiveHeaderInstanceLabel => IsReady && InstanceCount <= 1;

        public bool ShowLegacyShell => !IsReady;

        public bool ShowReadyShell => IsReady;

        public double LegacySidebarWidth => ShowLegacyShell ? 220 : 0;

        public bool HasInstances => InstanceCount > 0;

        public bool HasCurrentInstance => !string.IsNullOrWhiteSpace(gameInstanceService.CurrentInstance?.Name);

        public GameInstance? CurrentInstance => gameInstanceService.CurrentInstance;

        public IConfiguration CurrentConfiguration => gameInstanceService.Configuration;

        public GameInstanceManager CurrentManager => gameInstanceService.Manager;

        public IUser CurrentUser => user;

        public SteamLibrary CurrentSteamLibrary => gameInstanceService.Manager.SteamLibrary;

        public Registry? CurrentRegistry => gameInstanceService.CurrentRegistryManager?.registry;

        public NetModuleCache? CurrentCache => gameInstanceService.Manager.Cache;

        public IReadOnlyList<GameInstance> KnownGameInstances
            => gameInstanceService.Manager.Instances.Values.ToList();

        public Task RefreshCurrentStateAsync()
            => RefreshAsync();

        public bool ShowStartupInstancePanel => !IsReady;

        public bool ShowReadyInstancePanel => IsReady;

        public bool ShowReadyStatusSurface
            => IsReady
               && (IsRefreshing
                   || IsApplyingChanges
                   || IsUserBusy
                   || ReadyStatusNeedsAttention);

        public bool ShowPreviewSurface
        {
            get => showPreviewSurface;
            private set
            {
                this.RaiseAndSetIfChanged(ref showPreviewSurface, value);
                this.RaisePropertyChanged(nameof(ShowBrowseSurface));
                this.RaisePropertyChanged(nameof(BrowseSurfaceButtonBackground));
                this.RaisePropertyChanged(nameof(BrowseSurfaceButtonBorderBrush));
                this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBackground));
                this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBorderBrush));
            }
        }

        public bool ShowBrowseSurface => !ShowPreviewSurface;

        public bool HasSelectedInstance
        {
            get => hasSelectedInstance;
            private set => this.RaiseAndSetIfChanged(ref hasSelectedInstance, value);
        }

        public bool CanUseSelectedInstance => canUseSelectedInstance.Value;

        public bool SelectedInstanceIsCurrent => SelectedInstance?.IsCurrent == true;

        public bool ShowSwitchSelectedInstanceAction
            => SelectedInstance != null
               && !SelectedInstance.IsCurrent
               && !IsRefreshing
               && !IsApplyingChanges;

        public string SelectedInstanceContextTitle
            => SelectedInstanceIsCurrent
                ? "Current Install"
                : "Switch Target";

        public bool IsCatalogLoading
        {
            get => isCatalogLoading;
            private set
            {
                this.RaiseAndSetIfChanged(ref isCatalogLoading, value);
                this.RaisePropertyChanged(nameof(ShowCatalogSkeleton));
                this.RaisePropertyChanged(nameof(ShowModList));
                this.RaisePropertyChanged(nameof(ShowEmptyModResults));
            }
        }

        public bool HasMods => Mods.Count > 0;

        public bool ShowCatalogSkeleton => IsCatalogLoading;

        public bool ShowModList => !IsCatalogLoading && HasMods;

        public bool ShowEmptyModResults => !IsCatalogLoading && Mods.Count == 0;

        public bool HasSelectedMod => SelectedMod != null;

        public bool IsSelectedModLoading
        {
            get => isSelectedModLoading;
            private set
            {
                this.RaiseAndSetIfChanged(ref isSelectedModLoading, value);
                PublishSelectedModDisplayState();
                PublishSelectedModActionState();
            }
        }

        public bool ShowSelectedModPlaceholder => !HasSelectedMod && !IsSelectedModLoading;

        public bool ShowSelectedModLoadingState => HasSelectedMod && IsSelectedModLoading;

        public bool ShowSelectedModContent => HasSelectedMod && !IsSelectedModLoading;

        public bool ShowDetailsSidebar => ShowDetailsPane && HasSelectedMod;

        public bool ShowOverviewDetailsSection => selectedModDetailsSection == ModDetailsSection.Overview;

        public bool ShowMetadataDetailsSection => selectedModDetailsSection == ModDetailsSection.Metadata;

        public bool ShowRelationshipsDetailsSection => selectedModDetailsSection == ModDetailsSection.Relationships;

        public bool ShowDescriptionDetailsSection => selectedModDetailsSection == ModDetailsSection.Description;

        public bool OverviewDetailsSectionSelected => ShowOverviewDetailsSection;

        public bool MetadataDetailsSectionSelected => ShowMetadataDetailsSection;

        public bool RelationshipsDetailsSectionSelected => ShowRelationshipsDetailsSection;

        public bool DescriptionDetailsSectionSelected => ShowDescriptionDetailsSection;

        public string SelectedModLoadingTitle
            => SelectedMod == null
                ? "Loading mod details…"
                : $"Loading {SelectedMod.Name}…";

        public bool HasQueuedActions => QueuedActions.Count > 0;

        public int QueuedChangeActionCount
            => QueuedActions.Count(action => action.ActionKind != QueuedActionKind.Download);

        public int QueuedDownloadActionCount
            => QueuedActions.Count(action => action.ActionKind == QueuedActionKind.Download);

        public bool HasQueuedChangeActions => QueuedChangeActionCount > 0;

        public bool HasQueuedDownloadActions => QueuedDownloadActionCount > 0;

        public bool ShowEmptyQueueState => !HasQueuedActions;

        public bool IsQueueDrawerExpanded
        {
            get => isQueueDrawerExpanded;
            private set => this.RaiseAndSetIfChanged(ref isQueueDrawerExpanded, value);
        }

        public bool ShowEmptyQueueStub => !HasQueuedActions && !HasApplyResult;

        public bool ShowCollapsedQueuedActionsStub
            => !IsQueueDrawerExpanded && HasQueuedActions;

        public bool ShowCollapsedApplyResultStub
            => !IsQueueDrawerExpanded && !HasQueuedActions && HasApplyResult;

        public bool ShowExpandedQueuePanel
            => IsQueueDrawerExpanded && (HasQueuedActions || HasApplyResult);

        public bool HasPreviewDownloadsRequired => PreviewDownloadsRequired.Count > 0;

        public bool HasPreviewDependencies => PreviewDependencies.Count > 0;

        public bool HasPreviewAutoRemovals => PreviewAutoRemovals.Count > 0;

        public bool HasPreviewAttentionNotes => PreviewAttentionNotes.Count > 0;

        public bool HasPreviewRecommendations => PreviewRecommendations.Count > 0;

        public bool HasPreviewSuggestions => PreviewSuggestions.Count > 0;

        public bool HasPreviewConflicts => PreviewConflicts.Count > 0;

        public bool PreviewShowsEmptyCard => !HasQueuedActions;

        public bool PreviewShowsLoadingCard => HasQueuedChangeActions && IsPreviewLoading;

        public bool PreviewShowsReadyCard
            => HasQueuedActions
               && !IsPreviewLoading
               && ((HasQueuedChangeActions && PreviewCanApply)
                   || (!HasQueuedChangeActions && HasQueuedDownloadActions));

        public bool PreviewShowsBlockedCard
            => HasQueuedChangeActions && !IsPreviewLoading && !PreviewCanApply;

        public bool ShowAdvancedFilters
        {
            get => showAdvancedFilters;
            set
            {
                this.RaiseAndSetIfChanged(ref showAdvancedFilters, value);
                modSearchService.SetShowAdvancedFilters(value);
            }
        }

        public bool ShowDisplaySettings
        {
            get => showDisplaySettings;
            set
            {
                this.RaiseAndSetIfChanged(ref showDisplaySettings, value);
                this.RaisePropertyChanged(nameof(DisplaySettingsToggleLabel));
            }
        }

        public bool ShowDetailsPane
        {
            get => showDetailsPane;
            set
            {
                this.RaiseAndSetIfChanged(ref showDetailsPane, value);
                this.RaisePropertyChanged(nameof(DetailsPaneToggleLabel));
                this.RaisePropertyChanged(nameof(ShowDetailsSidebar));
            }
        }

        public int AppliedUiScalePercent => appliedUiScalePercent;

        public double AppliedUiScaleFactor => UiScaleSettings.ToFactor(appliedUiScalePercent);

        public double PendingUiScalePercent
        {
            get => pendingUiScalePercent;
            set
            {
                var normalized = UiScaleSettings.NormalizePercent((int)Math.Round(value));
                if (Math.Abs(pendingUiScalePercent - normalized) < 0.001)
                {
                    return;
                }

                uiScaleRestartStripDismissed = false;
                this.RaiseAndSetIfChanged(ref pendingUiScalePercent, normalized);
                appSettingsService.SaveUiScalePercent(normalized);
                this.RaisePropertyChanged(nameof(PendingUiScaleLabel));
                this.RaisePropertyChanged(nameof(DisplayScaleSummary));
                this.RaisePropertyChanged(nameof(DisplayScaleHint));
                this.RaisePropertyChanged(nameof(UiScaleNeedsRestart));
                this.RaisePropertyChanged(nameof(ShowUiScaleRestartStrip));
                this.RaisePropertyChanged(nameof(UiScaleRestartStripText));
                this.RaisePropertyChanged(nameof(ShowResetUiScaleAction));
            }
        }

        public int UiScaleMinimum => UiScaleSettings.MinPercent;

        public int UiScaleMaximum => UiScaleSettings.MaxPercent;

        public bool UiScaleNeedsRestart
            => (int)Math.Round(PendingUiScalePercent) != AppliedUiScalePercent;

        public bool ShowResetUiScaleAction
            => (int)Math.Round(PendingUiScalePercent) != UiScaleSettings.DefaultPercent;

        public string PendingUiScaleLabel => $"{(int)Math.Round(PendingUiScalePercent)}%";

        public string DisplayScaleSummary
            => UiScaleNeedsRestart
                ? $"Current scale {AppliedUiScalePercent}%. Next launch will use {PendingUiScaleLabel}."
                : $"Current scale {AppliedUiScalePercent}%.";

        public string DisplayScaleHint
            => UiScaleNeedsRestart
                ? "Saved for the next launch. Restart CKAN Linux to apply the new scale."
                : "Reduce the shell if the current layout feels oversized. Changes apply after restart.";

        public string CurrentCompatibleGameVersionLabel
            => gameInstanceService.CurrentInstance?.Version()?.ToString() ?? "<NONE>";

        public bool ShowCompatibleGameVersionOptions => CompatibleGameVersionOptions.Count > 0;

        public bool ShowCompatibleGameVersionWarning
            => gameInstanceService.CurrentInstance?.CompatibleVersionsAreFromDifferentGameVersion == true;

        public string CompatibleGameVersionsSummary
        {
            get
            {
                var selected = CompatibleGameVersionOptions.Where(opt => opt.IsSelected)
                                                          .Select(opt => opt.Label)
                                                          .ToList();
                return selected.Count == 0
                    ? $"Current game version {CurrentCompatibleGameVersionLabel}. No extra compatibility versions are enabled."
                    : $"Current game version {CurrentCompatibleGameVersionLabel}. Also treat {string.Join(", ", selected)} as compatible.";
            }
        }

        public string CompatibleGameVersionsHint
            => "Choose additional game versions CKAN should treat as compatible for this instance. The installed version is always included automatically.";

        public string CompatibleGameVersionsWarningText
        {
            get
            {
                if (gameInstanceService.CurrentInstance is not GameInstance instance
                    || !instance.CompatibleVersionsAreFromDifferentGameVersion)
                {
                    return "";
                }

                return instance.GameVersionWhenCompatibleVersionsWereStored == null
                    ? "Compatibility defaults were inferred automatically. Review them before trusting older mods."
                    : $"These compatibility selections were saved for {instance.GameVersionWhenCompatibleVersionsWereStored}. Review them for {CurrentCompatibleGameVersionLabel}.";
            }
        }

        public string DisplaySettingsToggleLabel
            => ShowDisplaySettings ? "Hide" : "Adjust";

        public string DetailsPaneToggleLabel
            => ShowDetailsPane ? "Hide Details" : "Show Details";

        public string PreviewSurfaceButtonLabel
            => HasQueuedActions ? $"Preview ({QueuedActions.Count})" : "Preview";

        public string BrowseSurfaceButtonBackground
            => ShowBrowseSurface ? "#355779" : "#181D23";

        public string BrowseSurfaceButtonBorderBrush
            => ShowBrowseSurface ? "#5A86B4" : "#2B323C";

        public string StatusSurfaceBackground
        {
            get
            {
                if (HasError || MessageContains("failed") || MessageContains("could not"))
                {
                    return "#4A232A";
                }

                if (MessageContains("no known instances") || MessageContains("unavailable"))
                {
                    return "#4A3920";
                }

                return "#24394A";
            }
        }

        public string StatusSurfaceBorderBrush
        {
            get
            {
                if (HasError || MessageContains("failed") || MessageContains("could not"))
                {
                    return "#934354";
                }

                if (MessageContains("no known instances") || MessageContains("unavailable"))
                {
                    return "#9A7B37";
                }

                return "#40647F";
            }
        }

        private bool ReadyStatusNeedsAttention
            => MessageContains("failed")
               || MessageContains("could not")
               || MessageContains("unavailable");

        public string PreviewSurfaceButtonBackground
            => ShowPreviewSurface
                ? "#5B456F"
                : HasQueuedActions || HasApplyResult
                    ? "#241C2C"
                    : "#181D23";

        public string PreviewSurfaceButtonBorderBrush
            => ShowPreviewSurface
                ? "#9B6BC3"
                : HasQueuedActions || HasApplyResult
                    ? "#694381"
                    : "#2B323C";

        public bool ShowUiScaleRestartStrip
            => UiScaleNeedsRestart && !uiScaleRestartStripDismissed;

        public string UiScaleRestartStripText
            => $"Current scale {AppliedUiScalePercent}%. Next launch scale {PendingUiScaleLabel}.";

        public bool HasActiveAdvancedFilters
            => !string.IsNullOrWhiteSpace(AdvancedAuthorFilter)
               || !string.IsNullOrWhiteSpace(AdvancedCompatibilityFilter)
               || FilterHasReplacementOnly;

        public int ActiveFilterCount
            => (FilterInstalledOnly ? 1 : 0)
               + (FilterNotInstalledOnly ? 1 : 0)
               + (FilterUpdatableOnly ? 1 : 0)
               + (FilterCompatibleOnly ? 1 : 0)
               + (FilterCachedOnly ? 1 : 0)
               + (FilterUncachedOnly ? 1 : 0)
               + (FilterIncompatibleOnly ? 1 : 0)
               + (FilterHasReplacementOnly ? 1 : 0)
               + (string.IsNullOrWhiteSpace(AdvancedAuthorFilter) ? 0 : 1)
               + (string.IsNullOrWhiteSpace(AdvancedCompatibilityFilter) ? 0 : 1);

        public string MoreFiltersLabel
            => ActiveFilterCount > 0
                ? $"Active Filters ({ActiveFilterCount}) ▾"
                : "Filters ▾";

        public string MoreFiltersButtonBackground
            => HasActiveFilters ? "#5C376D" : "#3E648A";

        public string MoreFiltersButtonBorderBrush => MoreFiltersButtonBackground;

        public string ClearFiltersButtonBackground => "#6B2B2B";

        public string ClearFiltersButtonBorderBrush => ClearFiltersButtonBackground;

        public string CompatibleFilterLabel => FormatFilterOptionLabel("Compatible", filterOptionCounts.Compatible);

        public string InstalledFilterLabel => FormatFilterOptionLabel("Installed", filterOptionCounts.Installed);

        public string UpdatableFilterLabel => FormatFilterOptionLabel("Updatable", filterOptionCounts.Updatable);

        public string ReplaceableFilterLabel => FormatFilterOptionLabel("Replaceable", filterOptionCounts.Replaceable);

        public string CachedFilterLabel => FormatFilterOptionLabel("Downloaded", filterOptionCounts.Cached);

        public string UncachedFilterLabel => FormatFilterOptionLabel("Not Downloaded", filterOptionCounts.Uncached);

        public string NotInstalledFilterLabel => FormatFilterOptionLabel("Not Installed", filterOptionCounts.NotInstalled);

        public string IncompatibleFilterLabel => FormatFilterOptionLabel("Incompatible", filterOptionCounts.Incompatible);

        public string SortMenuLabel
            => $"{SelectedSortOption?.Label ?? "Name"} ▾";

        public string NameSortLabel
            => SortOptionLabel(ModSortOption.Name, "Name");

        public string AuthorSortLabel
            => SortOptionLabel(ModSortOption.Author, "Author");

        public string PopularitySortLabel
            => SortOptionLabel(ModSortOption.Popularity, "Downloads");

        public string CompatibilitySortLabel
            => SortOptionLabel(ModSortOption.Compatibility, "Compat");

        public string VersionSortLabel
            => SortOptionLabel(ModSortOption.Version, "Version");

        public string InstalledFirstSortLabel
            => SortOptionLabel(ModSortOption.InstalledFirst, "Installed First");

        public string UpdatesFirstSortLabel
            => SortOptionLabel(ModSortOption.UpdatesFirst, "Updates First");

        public bool NameSortSelected => SelectedSortOption?.Value == ModSortOption.Name;

        public bool AuthorSortSelected => SelectedSortOption?.Value == ModSortOption.Author;

        public bool PopularitySortSelected => SelectedSortOption?.Value == ModSortOption.Popularity;

        public bool CompatibilitySortSelected => SelectedSortOption?.Value == ModSortOption.Compatibility;

        public bool VersionSortSelected => SelectedSortOption?.Value == ModSortOption.Version;

        public bool InstalledFirstSortSelected => SelectedSortOption?.Value == ModSortOption.InstalledFirst;

        public bool UpdatesFirstSortSelected => SelectedSortOption?.Value == ModSortOption.UpdatesFirst;

        public bool HasActiveFilters
            => !string.IsNullOrWhiteSpace(ModSearchText)
               || FilterInstalledOnly
               || FilterNotInstalledOnly
               || FilterUpdatableOnly
               || FilterCompatibleOnly
               || FilterCachedOnly
               || FilterUncachedOnly
               || FilterIncompatibleOnly
               || HasActiveAdvancedFilters;

        public string AdvancedFilterSummary
        {
            get
            {
                if (!HasActiveFilters)
                {
                    return "All mods are shown.";
                }

                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(ModSearchText))
                {
                    parts.Add($"Search: {ModSearchText.Trim()}");
                }
                if (!string.IsNullOrWhiteSpace(AdvancedAuthorFilter))
                {
                    parts.Add($"Author: {AdvancedAuthorFilter.Trim()}");
                }
                if (!string.IsNullOrWhiteSpace(AdvancedCompatibilityFilter))
                {
                    parts.Add($"Compatibility: {AdvancedCompatibilityFilter.Trim()}");
                }
                if (FilterHasReplacementOnly)
                {
                    parts.Add("Replacement only");
                }
                if (FilterCompatibleOnly)
                {
                    parts.Add("Compatible only");
                }
                if (FilterUncachedOnly)
                {
                    parts.Add("Not downloaded only");
                }
                if (FilterInstalledOnly)
                {
                    parts.Add("Installed only");
                }
                if (FilterNotInstalledOnly)
                {
                    parts.Add("Not installed only");
                }
                if (FilterUpdatableOnly)
                {
                    parts.Add("Updatable only");
                }
                if (FilterCachedOnly)
                {
                    parts.Add("Downloaded only");
                }
                if (FilterIncompatibleOnly)
                {
                    parts.Add("Incompatible only");
                }

                return parts.Count > 0
                    ? string.Join(" • ", parts)
                    : "All mods are shown.";
            }
        }

        public bool ShowInstallAction => SelectedMod != null
                                         && !IsSelectedModLoading
                                         && !SelectedMod.IsInstalled
                                         && !SelectedMod.IsIncompatible;

        public bool ShowUpdateAction => SelectedMod?.IsInstalled == true
                                        && !IsSelectedModLoading
                                        && SelectedMod.HasUpdate;

        public bool ShowRemoveAction => SelectedMod?.IsInstalled == true
                                        && !IsSelectedModLoading
                                        && !SelectedMod.HasUpdate;

        public bool HasSelectedModQueuedAction
            => SelectedMod != null
               && changesetService.FindQueuedApplyAction(SelectedMod.Identifier) != null;

        public bool HasSelectedModQueuedDownload
            => SelectedMod != null
               && changesetService.FindQueuedDownloadAction(SelectedMod.Identifier) != null;

        public bool ShowInstallNowAction
            => ShowInstallAction
               && !HasSelectedModQueuedAction
               && !IsApplyingChanges;

        public bool ShowRemoveNowAction
            => ShowRemoveAction
               && !HasSelectedModQueuedAction
               && !IsApplyingChanges;

        public bool ShowPrimarySelectedModAction
            => !IsSelectedModLoading
               && (HasSelectedModQueuedAction
               || ShowInstallAction
               || ShowUpdateAction
               || ShowRemoveAction);

        public bool ShowSelectedModActionUnavailableNote
            => SelectedMod?.IsInstalled == false
               && SelectedModIsIncompatible
               && !IsSelectedModLoading
               && !ShowInstallNowAction
               && !ShowPrimarySelectedModAction;

        public string SelectedModActionUnavailableNote
            => "This mod cannot be installed with the current compatibility settings. Adjust Compatible game versions in Settings if you want to allow it.";

        public string PrimarySelectedModActionLabel
        {
            get
            {
                var queued = SelectedMod == null
                    ? null
                    : changesetService.FindQueuedApplyAction(SelectedMod.Identifier);
                if (queued != null)
                {
                    return $"Cancel {queued.ActionText}";
                }

                if (ShowInstallAction)
                {
                    return "Queue Install";
                }
                if (ShowUpdateAction)
                {
                    return "Queue Update";
                }
                if (ShowRemoveAction)
                {
                    return "Queue Remove";
                }

                return "";
            }
        }

        public string PrimarySelectedModActionBackground
        {
            get
            {
                var queued = SelectedMod == null
                    ? null
                    : changesetService.FindQueuedApplyAction(SelectedMod.Identifier);
                if (queued != null)
                {
                    return "#39424E";
                }

                if (ShowInstallAction)
                {
                    return "#1B4D77";
                }
                if (ShowUpdateAction)
                {
                    return "#4B6C23";
                }
                if (ShowRemoveAction)
                {
                    return "#1B4D77";
                }

                return "#39424E";
            }
        }

        public string PrimarySelectedModActionBorderBrush => PrimarySelectedModActionBackground;

        public bool SelectedModIsInstalled
        {
            get => selectedModIsInstalled;
            private set => this.RaiseAndSetIfChanged(ref selectedModIsInstalled, value);
        }

        public bool SelectedModHasUpdate
        {
            get => selectedModHasUpdate;
            private set => this.RaiseAndSetIfChanged(ref selectedModHasUpdate, value);
        }

        public bool SelectedModIsCached
        {
            get => selectedModIsCached;
            private set => this.RaiseAndSetIfChanged(ref selectedModIsCached, value);
        }

        public bool SelectedModIsIncompatible
        {
            get => selectedModIsIncompatible;
            private set => this.RaiseAndSetIfChanged(ref selectedModIsIncompatible, value);
        }

        public bool SelectedModHasReplacement
        {
            get => selectedModHasReplacement;
            private set => this.RaiseAndSetIfChanged(ref selectedModHasReplacement, value);
        }

        public string QueueCountLabel
            => QueuedActions.Count switch
            {
                0 => "Queue empty",
                1 => "1 pending item",
                _ => $"{QueuedActions.Count} pending items",
            };

        public string PreviewSummary
        {
            get => previewSummary;
            private set => this.RaiseAndSetIfChanged(ref previewSummary, value);
        }

        public bool HasApplyResult => !string.IsNullOrWhiteSpace(ApplyResultTitle);

        public string ApplyResultTitle
        {
            get => applyResultTitle;
            private set => this.RaiseAndSetIfChanged(ref applyResultTitle, value);
        }

        public string ApplyResultMessage
        {
            get => applyResultMessage;
            private set => this.RaiseAndSetIfChanged(ref applyResultMessage, value);
        }

        public string ApplyResultBackground
        {
            get => applyResultBackground;
            private set => this.RaiseAndSetIfChanged(ref applyResultBackground, value);
        }

        public string ApplyResultBorderBrush
        {
            get => applyResultBorderBrush;
            private set => this.RaiseAndSetIfChanged(ref applyResultBorderBrush, value);
        }

        public bool HasApplyResultSummaryLines => ApplyResultSummaryLines.Count > 0;

        public bool HasApplyResultFollowUpLines => ApplyResultFollowUpLines.Count > 0;

        public bool IsPreviewLoading
        {
            get => isPreviewLoading;
            private set => this.RaiseAndSetIfChanged(ref isPreviewLoading, value);
        }

        public bool PreviewCanApply
        {
            get => previewCanApply;
            private set => this.RaiseAndSetIfChanged(ref previewCanApply, value);
        }

        public bool IsApplyingChanges
        {
            get => isApplyingChanges;
            private set
            {
                this.RaiseAndSetIfChanged(ref isApplyingChanges, value);
                PublishSelectedModActionState();
                this.RaisePropertyChanged(nameof(PreviewStatusLabel));
                this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
            }
        }

        private bool IsUserBusy
        {
            get => isUserBusy;
            set
            {
                this.RaiseAndSetIfChanged(ref isUserBusy, value);
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
            }
        }

        public string PreviewStatusLabel
            => IsApplyingChanges
                ? currentExecutionStatusLabel
                : IsPreviewLoading
                    ? "Resolving dependencies and downloads…"
                    : !HasQueuedChangeActions && HasQueuedDownloadActions
                        ? "Download queue ready"
                        : PreviewCanApply
                            ? HasPreviewAttentionNotes
                                ? "Apply is ready, but prompts will appear"
                                : "Apply is ready"
                            : HasPreviewAttentionNotes && !HasPreviewConflicts
                                ? "Required steps must be cleared before apply"
                                : "Conflicts must be cleared before apply";

        public string PreviewOutcomeTitle
            => PreviewShowsLoadingCard
                ? "Analyzing Queued Changes"
                : PreviewShowsReadyCard
                    ? !HasQueuedChangeActions && HasQueuedDownloadActions
                        ? "Ready to Download"
                        : HasPreviewAttentionNotes
                            ? "Ready with Prompts"
                            : "Ready to Apply"
                    : PreviewShowsBlockedCard
                        ? HasPreviewAttentionNotes && !HasPreviewConflicts
                            ? "Apply Blocked by Required Steps"
                            : "Apply Blocked by Conflicts"
                        : "No Preview Yet";

        public string PreviewPanelGuidance
        {
            get
            {
                if (PreviewShowsEmptyCard)
                {
                    return "Queue install, update, or remove actions from the browser to see downloads, auto-installs, removals, and blockers before applying. Right-click a mod for download-only.";
                }

                if (PreviewShowsLoadingCard)
                {
                    return "CKAN Linux is resolving install order, dependency closure, downloads, and apply blockers.";
                }

                if (!HasQueuedChangeActions && HasQueuedDownloadActions)
                {
                    return "Download-only items are staged separately. Use Download Only to fill the cache without changing GameData.";
                }

                if (PreviewShowsReadyCard)
                {
                    return HasPreviewDependencies
                        ? "Direct actions are shown on the left. Required dependency installs are handled automatically during Apply."
                        : "The queued actions are ready to apply as shown.";
                }

                return HasPreviewConflicts
                    ? "Review the conflicts and clear them before applying anything."
                    : "Review the required setup notes before applying anything.";
            }
        }

        public string PreviewImpactSummary
        {
            get
            {
                if (PreviewShowsEmptyCard)
                {
                    return "Queue install, update, or remove actions to see downloads, dependencies, auto-removals, and conflicts before applying. Right-click a mod for download-only.";
                }

                var parts = new List<string>();

                if (QueuedChangeActionCount > 0)
                {
                    parts.Add(CountLabel(QueuedChangeActionCount, "queued change", "queued changes"));
                }
                if (QueuedDownloadActionCount > 0)
                {
                    parts.Add(CountLabel(QueuedDownloadActionCount, "download-only item", "download-only items"));
                }

                if (PreviewDownloadsRequired.Count > 0)
                {
                    parts.Add(CountLabel(PreviewDownloadsRequired.Count, "required download", "required downloads"));
                }
                if (PreviewDependencies.Count > 0)
                {
                    parts.Add(CountLabel(PreviewDependencies.Count, "dependency install", "dependency installs"));
                }
                if (PreviewAutoRemovals.Count > 0)
                {
                    parts.Add(CountLabel(PreviewAutoRemovals.Count, "auto-removal", "auto-removals"));
                }
                if (PreviewConflicts.Count > 0)
                {
                    parts.Add(CountLabel(PreviewConflicts.Count, "conflict", "conflicts"));
                }

                return string.Join(" • ", parts);
            }
        }

        public string PreviewQueuedCountLabel
            => CountLabel(QueuedChangeActionCount, "Direct Change", "Direct Changes");

        public string PreviewDownloadQueueCountLabel
            => CountLabel(QueuedDownloadActionCount, "Download-Only Item", "Download-Only Items");

        public string PreviewDownloadCountLabel
            => CountLabel(PreviewDownloadsRequired.Count, "Required Download", "Required Downloads");

        public string PreviewDependencyCountLabel
            => CountLabel(PreviewDependencies.Count, "Auto Install", "Auto Installs");

        public bool ShowPreviewQueuedActions
            => HasQueuedActions;

        public string PreviewQueuedGuidance
            => HasQueuedChangeActions && HasQueuedDownloadActions
                ? "Install/update/remove actions and download-only staging items are listed together below. Apply Changes ignores the download-only items."
                : HasQueuedChangeActions
                    ? HasPreviewDependencies
                        ? "These are the direct install/update/remove actions you selected. CKAN will also install the required mods listed below."
                        : "These are the direct install/update/remove actions you selected."
                    : "These items stage archives in the cache for later install and are not part of Apply Changes.";

        public bool ShowPreviewDownloadQueueGuidance
            => HasQueuedDownloadActions;

        public string PreviewDownloadQueueGuidanceTitle => "Download-Only Queue";

        public string PreviewDownloadQueueGuidance
            => HasQueuedChangeActions
                ? "These items are staged separately. Use Download Only to cache them without changing GameData. Apply Changes will leave them queued."
                : "These items only download archives into the cache. They do not change GameData until you later queue an install or update.";

        public bool ShowPreviewDependencyGuidance
            => HasPreviewDependencies;

        public string PreviewDependencyGuidanceTitle
            => PreviewCanApply
                ? "Dependencies Will Install Automatically"
                : "Required Dependencies";

        public string PreviewDependencyGuidance
            => PreviewCanApply
                ? "CKAN Linux will install the required mods below as part of Apply. You do not need to queue them one by one."
                : "The mods below are required by your queued actions. Once blockers are cleared, CKAN Linux will install them automatically.";

        public string PreviewAutoRemovalCountLabel
            => CountLabel(PreviewAutoRemovals.Count, "Auto-Removal", "Auto-Removals");

        public string PreviewConflictCountLabel
            => CountLabel(PreviewConflicts.Count, "Conflict", "Conflicts");

        public string PreviewAttentionCountLabel
            => CountLabel(PreviewAttentionNotes.Count, "Required Step", "Required Steps");

        public string ApplyChangesButtonBackground
            => !HasQueuedChangeActions
                ? "#39424E"
                : PreviewCanApply && !IsPreviewLoading && !IsApplyingChanges
                    ? "#2A6B4A"
                    : "#5A4030";

        public string ApplyChangesButtonBorderBrush => ApplyChangesButtonBackground;

        public string DownloadQueuedButtonBackground
            => !HasQueuedDownloadActions || IsApplyingChanges
                ? "#39424E"
                : "#2B5C88";

        public string DownloadQueuedButtonBorderBrush => DownloadQueuedButtonBackground;

        public string CollapsedQueueStubTitle
            => HasQueuedActions
                ? QueueCountLabel
                : HasApplyResult
                    ? ApplyResultTitle
                    : "No pending changes";

        public string CollapsedQueueStubSummary
            => HasQueuedActions
                ? $"{PreviewStatusLabel} • {PreviewImpactSummary}"
                : HasApplyResult
                    ? ApplyResultMessage
                    : "Queue install, update, or remove actions to preview changes. Right-click a mod for download-only.";

        public string CollapsedQueueStubBackground
            => HasQueuedActions
                ? "#161B21"
                : ApplyResultBackground;

        public string CollapsedQueueStubBorderBrush
            => HasQueuedActions
                ? "#2B323C"
                : ApplyResultBorderBrush;

        public string SelectedModQueueStatus
        {
            get
            {
                if (SelectedMod == null)
                {
                    return "Choose a mod to queue an install, update, or removal. Right-click a mod for download-only.";
                }

                var queuedChange = changesetService.FindQueuedApplyAction(SelectedMod.Identifier);
                if (queuedChange != null)
                {
                    return $"{queuedChange.ActionText} queued: {queuedChange.DetailText}";
                }

                var queuedDownload = changesetService.FindQueuedDownloadAction(SelectedMod.Identifier);
                if (queuedDownload != null)
                {
                    return $"{queuedDownload.ActionText} queued: {queuedDownload.DetailText}";
                }

                if (ShowInstallAction)
                {
                    return "No queued item for this mod yet. Install now, queue it for later, or right-click for download-only.";
                }

                if (ShowRemoveAction)
                {
                    return "No queued item for this mod yet. Remove it now or queue the removal for later.";
                }

                return ShowUpdateAction
                    ? "No queued item for this mod yet. Queue the update when you are ready to review it."
                    : "No queued item for this mod yet.";
            }
        }

        public bool FilterInstalledOnly
        {
            get => filterInstalledOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterInstalledOnly, value) && value)
                {
                    ClearFilter(ref filterNotInstalledOnly, nameof(FilterNotInstalledOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterNotInstalledOnly
        {
            get => filterNotInstalledOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterNotInstalledOnly, value) && value)
                {
                    ClearFilter(ref filterInstalledOnly, nameof(FilterInstalledOnly));
                    ClearFilter(ref filterUpdatableOnly, nameof(FilterUpdatableOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterUpdatableOnly
        {
            get => filterUpdatableOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterUpdatableOnly, value) && value)
                {
                    ClearFilter(ref filterNotInstalledOnly, nameof(FilterNotInstalledOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterCompatibleOnly
        {
            get => filterCompatibleOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterCompatibleOnly, value) && value)
                {
                    ClearFilter(ref filterIncompatibleOnly, nameof(FilterIncompatibleOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterCachedOnly
        {
            get => filterCachedOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterCachedOnly, value) && value)
                {
                    ClearFilter(ref filterUncachedOnly, nameof(FilterUncachedOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterUncachedOnly
        {
            get => filterUncachedOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterUncachedOnly, value) && value)
                {
                    ClearFilter(ref filterCachedOnly, nameof(FilterCachedOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterIncompatibleOnly
        {
            get => filterIncompatibleOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterIncompatibleOnly, value) && value)
                {
                    ClearFilter(ref filterCompatibleOnly, nameof(FilterCompatibleOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterHasReplacementOnly
        {
            get => filterHasReplacementOnly;
            set
            {
                this.RaiseAndSetIfChanged(ref filterHasReplacementOnly, value);
                PublishFilterStateLabels();
            }
        }

        public string InstanceCountLabel
            => InstanceCount switch
            {
                0 => "No registered instances",
                1 => "1 registered instance",
                _ => $"{InstanceCount} registered instances",
            };

        private void ClearFilter(ref bool field, string propertyName)
        {
            if (field)
            {
                field = false;
                this.RaisePropertyChanged(propertyName);
            }
        }

        public string InstanceSwitchDiscardPrompt
            => $"You have {QueuedActions.Count} queued item{(QueuedActions.Count == 1 ? "" : "s")} for {CurrentInstanceName}. Switching installs will discard them.";

        public string ModCountLabel
            => IsCatalogLoading
                ? "Loading…"
                : Mods.Count switch
                {
                    0 => "No mods",
                    1 => "1 mod",
                    _ => $"{Mods.Count} mods",
                };

        public FilterState ActiveFilterState => CurrentFilter();

        public InstanceSummary? SelectedInstance
        {
            get => selectedInstance;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedInstance, value);
                HasSelectedInstance = value != null;
                UpdateSelectedInstanceSummary(value);
                this.RaisePropertyChanged(nameof(SelectedInstanceIsCurrent));
                this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
                this.RaisePropertyChanged(nameof(SelectedInstanceContextTitle));
                this.RaisePropertyChanged(nameof(SelectedInstanceNameLabel));
                this.RaisePropertyChanged(nameof(SelectedInstanceGameNameLabel));
                this.RaisePropertyChanged(nameof(SelectedInstanceGameDirLabel));
            }
        }

        public ModListItem? SelectedMod
        {
            get => selectedMod;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedMod, value);
                this.RaisePropertyChanged(nameof(HasSelectedMod));
                this.RaisePropertyChanged(nameof(ShowDetailsSidebar));
                PublishSelectedModDisplayState();
                PublishSelectedModActionState();
                _ = LoadModDetailsAsync(value?.Identifier);
            }
        }

        public QueuedActionModel? SelectedQueuedAction
        {
            get => selectedQueuedAction;
            set => this.RaiseAndSetIfChanged(ref selectedQueuedAction, value);
        }

        public void ActivateModFromBrowser(ModListItem mod)
        {
            if (SelectedMod != null
                && string.Equals(SelectedMod.Identifier, mod.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                ShowDetailsPane = !ShowDetailsPane;
                return;
            }

            ShowDetailsPane = true;
            SelectedMod = mod;
        }

        public bool ShowDownloadOnlyContextAction(ModListItem mod)
        {
            if (changesetService.FindQueuedDownloadAction(mod.Identifier) != null)
            {
                return true;
            }

            return changesetService.FindQueuedApplyAction(mod.Identifier) == null
                   && !mod.IsInstalled
                   && !mod.IsCached
                   && !mod.IsIncompatible;
        }

        public string DownloadOnlyContextLabel(ModListItem mod)
            => changesetService.FindQueuedDownloadAction(mod.Identifier) != null
                ? "Cancel Download Only"
                : "Download Only";

        public void ToggleDownloadOnlyFromBrowser(ModListItem mod)
        {
            var queuedDownload = changesetService.FindQueuedDownloadAction(mod.Identifier);
            ClearApplyResult();

            if (queuedDownload != null)
            {
                if (changesetService.Remove(queuedDownload.Identifier))
                {
                    StatusMessage = $"Removed queued download-only for {mod.Name}.";
                }
                return;
            }

            var queuedApply = changesetService.FindQueuedApplyAction(mod.Identifier);
            if (queuedApply != null)
            {
                StatusMessage = $"{queuedApply.ActionText} is already queued for {mod.Name}. Cancel it before staging download-only.";
                return;
            }

            changesetService.QueueDownload(mod);
            StatusMessage = $"Queued download-only for {mod.Name}.";
        }

        private async Task RefreshAsync()
        {
            IsRefreshing = true;
            ClearApplyResult();
            StartupStage = StartupStage.Loading;
            Diagnostics = "Loading instance metadata.";
            StageTitle = "Loading Instances";
            StageDescription = "Inspecting your configured installs and preparing the browser.";
            StatusMessage = "Checking CKAN game instances…";

            try
            {
                await gameInstanceService.InitializeAsync(CancellationToken.None);
                ReloadInstances();
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Instance startup failed.";
                StageTitle = "Could Not Load Instances";
                StageDescription = "CKAN Linux could not load your install data. Retry after checking the error details.";
                StartupStage = StartupStage.Error;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task SetCurrentInstanceAsync()
        {
            if (SelectedInstance == null)
            {
                return;
            }

            IsRefreshing = true;
            StatusMessage = $"Switching to {SelectedInstance.Name}…";
            StageTitle = "Switching Install";
            StageDescription = "Reloading the selected install and refreshing the browser.";
            try
            {
                await gameInstanceService.SetCurrentInstanceAsync(SelectedInstance.Name, CancellationToken.None);
                ReloadInstances();
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = $"Failed to switch to {SelectedInstance.Name}.";
                StageTitle = "Could Not Select Instance";
                StageDescription = "The selected install could not be activated.";
                StartupStage = StartupStage.Error;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public async Task<bool> TrySwitchSelectedInstanceAsync(Func<string, Task<bool>> confirmDiscardQueueAsync)
        {
            if (!IsReady || SelectedInstance == null || SelectedInstance.IsCurrent)
            {
                return false;
            }

            var target = SelectedInstance;
            if (HasQueuedActions)
            {
                bool confirmed = await confirmDiscardQueueAsync(InstanceSwitchDiscardPrompt);
                if (!confirmed)
                {
                    SelectedInstance = Instances.FirstOrDefault(inst => inst.IsCurrent) ?? target;
                    return false;
                }

                DiscardQueuedActionsForInstanceSwitch();
            }

            await SetCurrentInstanceAsync();
            return true;
        }

        private void ReloadInstances()
        {
            Instances.Clear();
            foreach (var inst in gameInstanceService.Instances)
            {
                Instances.Add(inst);
            }

            InstanceCount = Instances.Count;
            CurrentInstanceName = gameInstanceService.CurrentInstance?.Name ?? "No instance selected";
            var previousSelectionName = SelectedInstance?.Name;
            SelectedInstance = Instances.FirstOrDefault(inst => inst.IsCurrent);
            if (SelectedInstance == null && !string.IsNullOrWhiteSpace(previousSelectionName))
            {
                SelectedInstance = Instances.FirstOrDefault(inst => inst.Name == previousSelectionName);
            }
            SelectedInstance ??= Instances.FirstOrDefault();
            RefreshCompatibleGameVersionOptions();

            if (Instances.Count == 0)
            {
                ClearCatalogState();
                StartupStage = StartupStage.Empty;
                StageTitle = "No Instances Found";
                StageDescription = "No registered KSP installs were found for CKAN Linux.";
                StatusMessage = "No known instances were found.";
                SelectedActionLabel = "Open Selected Install";
                SelectedActionHint = "Add or register a KSP install before continuing.";
                PublishInstanceStateLabels();
                return;
            }

            if (gameInstanceService.CurrentInstance != null)
            {
                StartupStage = StartupStage.Ready;
                StageTitle = "Ready";
                StageDescription = "";
                StatusMessage = $"Loaded {Instances.Count} instance{(Instances.Count == 1 ? "" : "s")} and activated {gameInstanceService.CurrentInstance.Name}.";
                SelectedActionLabel = "Open Selected Install";
                SelectedActionHint = "Choose a different install here if you want to switch contexts.";
                _ = LoadModCatalogAsync();
            }
            else
            {
                ClearCatalogState();
                StartupStage = StartupStage.SelectionRequired;
                StageTitle = "Choose an Instance";
                StageDescription = "Multiple installs are known, but none is active yet.";
                StatusMessage = $"Loaded {Instances.Count} instance{(Instances.Count == 1 ? "" : "s")}. Select one to continue.";
                SelectedActionLabel = "Open Selected Install";
                SelectedActionHint = "Pick the install you want to browse and manage.";
            }

            PublishInstanceStateLabels();
        }

        private async Task LoadModCatalogAsync()
        {
            if (!IsReady)
            {
                return;
            }

            Interlocked.Increment(ref catalogLoadRequestId);
            if (!await catalogLoadSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                while (IsReady)
                {
                    int activeRequestId = catalogLoadRequestId;
                    IsCatalogLoading = true;
                    CatalogStatusMessage = "Loading mods from the current CKAN registry and repository cache…";
                    var previousSelection = SelectedMod?.Identifier;

                    try
                    {
                        var currentFilter = CurrentFilter();
                        var itemsTask = modCatalogService.GetModListAsync(currentFilter, CancellationToken.None);
                        var countsTask = modCatalogService.GetFilterOptionCountsAsync(currentFilter, CancellationToken.None);
                        await Task.WhenAll(itemsTask, countsTask);
                        var items = SortItems(itemsTask.Result).ToList();
                        if (activeRequestId != catalogLoadRequestId)
                        {
                            continue;
                        }

                        filterOptionCounts = countsTask.Result;
                        hasFilterOptionCounts = true;

                        ReplaceVisibleMods(items);

                        SelectedMod = previousSelection != null
                            ? Mods.FirstOrDefault(mod => mod.Identifier == previousSelection) ?? Mods.FirstOrDefault()
                            : Mods.FirstOrDefault();

                        if (pendingModListScrollReset)
                        {
                            pendingModListScrollReset = false;
                            ModListScrollResetRequestId++;
                        }

                        CatalogStatusMessage = Mods.Count == 0
                            ? "No mods matched the current search and filter state."
                            : $"Showing {Mods.Count} mod{(Mods.Count == 1 ? "" : "s")} for {CurrentInstanceName}.";
                        PublishCatalogStateLabels();
                        PublishFilterOptionCountLabels();
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError($"Mod catalog load failed: {ex}");
                        if (activeRequestId != catalogLoadRequestId)
                        {
                            continue;
                        }

                        Diagnostics = ex.Message;
                        CatalogStatusMessage = "Failed to load the mod catalog.";
                    }
                    finally
                    {
                        if (activeRequestId == catalogLoadRequestId)
                        {
                            IsCatalogLoading = false;
                            PublishCatalogStateLabels();
                        }
                    }

                    if (activeRequestId == catalogLoadRequestId)
                    {
                        break;
                    }
                }
            }
            finally
            {
                catalogLoadSemaphore.Release();
            }
        }

        private async Task LoadModDetailsAsync(string? identifier)
        {
            int requestId = ++selectedModLoadRequestId;
            if (string.IsNullOrWhiteSpace(identifier) || !IsReady)
            {
                IsSelectedModLoading = false;
                SetSelectedModDetailsSection(ModDetailsSection.Overview);
                ResetSelectedModDetails();
                return;
            }

            IsSelectedModLoading = true;
            try
            {
                var details = await modCatalogService.GetModDetailsAsync(identifier, CancellationToken.None);
                if (!IsCurrentSelectedModRequest(identifier, requestId))
                {
                    return;
                }

                if (details == null)
                {
                    ResetSelectedModDetails();
                    return;
                }

                SelectedModTitle = details.Title;
                SelectedModSubtitle = string.IsNullOrWhiteSpace(details.Summary)
                    ? details.Identifier
                    : $"{details.Identifier} • {details.Summary}";
                SelectedModAuthors = string.IsNullOrWhiteSpace(details.Authors)
                    ? "Author information unavailable"
                    : $"By {details.Authors}";
                SelectedModVersions = $"Latest {details.LatestVersion}\n{(details.IsInstalled ? $"Installed {details.InstalledVersion}" : "Not installed")}";
                SelectedModInstallState = BuildInstallState(details);
                SelectedModCompatibility = details.Compatibility;
                SelectedModModuleKind = details.ModuleKind;
                SelectedModLicense = details.License;
                SelectedModReleaseDate = details.ReleaseDate;
                SelectedModDownloadSize = details.DownloadSize;
                SelectedModDownloadCount = details.DownloadCount?.ToString("N0") ?? "Unknown";
                SelectedModRelationships = $"{details.DependencyCount} depends • {details.RecommendationCount} recommends • {details.SuggestionCount} suggests";
                SelectedModDependencyCountLabel = CountLabel(details.DependencyCount, "Dependency", "Dependencies");
                SelectedModRecommendationCountLabel = CountLabel(details.RecommendationCount, "Recommendation", "Recommendations");
                SelectedModSuggestionCountLabel = CountLabel(details.SuggestionCount, "Suggestion", "Suggestions");
                SelectedModIsInstalled = details.IsInstalled;
                SelectedModHasUpdate = details.HasUpdate;
                SelectedModIsCached = details.IsCached;
                SelectedModIsIncompatible = details.IsIncompatible;
                SelectedModHasReplacement = details.HasReplacement;
                SelectedModBody = string.IsNullOrWhiteSpace(details.Description)
                    ? "No extended description is available for this mod."
                    : details.Description;
                SetSelectedModDetailsSection(ModDetailsSection.Overview);
            }
            catch (Exception ex)
            {
                if (!IsCurrentSelectedModRequest(identifier, requestId))
                {
                    return;
                }

                Diagnostics = ex.Message;
                SelectedModTitle = "Could not load details";
                SelectedModSubtitle = identifier;
                SelectedModAuthors = "";
                SelectedModVersions = "";
                SelectedModInstallState = "";
                SelectedModCompatibility = "";
                SelectedModModuleKind = "";
                SelectedModLicense = "";
                SelectedModReleaseDate = "";
                SelectedModDownloadSize = "";
                SelectedModDownloadCount = "";
                SelectedModRelationships = "";
                SelectedModDependencyCountLabel = "";
                SelectedModRecommendationCountLabel = "";
                SelectedModSuggestionCountLabel = "";
                SelectedModIsInstalled = false;
                SelectedModHasUpdate = false;
                SelectedModIsCached = false;
                SelectedModIsIncompatible = false;
                SelectedModHasReplacement = false;
                SelectedModBody = "The selected mod failed to load its details.";
                SetSelectedModDetailsSection(ModDetailsSection.Overview);
            }
            finally
            {
                if (IsCurrentSelectedModRequest(identifier, requestId))
                {
                    IsSelectedModLoading = false;
                }
            }
        }

        private void OnCurrentInstanceChanged(GameInstance? current)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!string.Equals(CurrentInstanceName, current?.Name, StringComparison.Ordinal)
                    && HasQueuedActions)
                {
                    changesetService.Clear();
                }
                ClearApplyResult();
                CurrentInstanceName = current?.Name ?? "No instance selected";
                ReloadInstances();
            });
        }

        private void OnQueueChanged()
            => Dispatcher.UIThread.Post(() =>
            {
                RefreshQueuedActions();
                _ = LoadPreviewAsync();
            });

        private FilterState CurrentFilter()
            => new FilterState
            {
                SearchText          = ModSearchText,
                AuthorText          = AdvancedAuthorFilter,
                CompatibilityText   = AdvancedCompatibilityFilter,
                SortOption          = SelectedSortOption?.Value ?? ModSortOption.Name,
                SortDescending      = SortDescending,
                InstalledOnly       = FilterInstalledOnly,
                NotInstalledOnly    = FilterNotInstalledOnly,
                UpdatableOnly       = FilterUpdatableOnly,
                CompatibleOnly      = FilterCompatibleOnly,
                CachedOnly          = FilterCachedOnly,
                UncachedOnly        = FilterUncachedOnly,
                IncompatibleOnly    = FilterIncompatibleOnly,
                HasReplacementOnly  = FilterHasReplacementOnly,
            };

        private void RefreshCatalogForFilterChange()
        {
            modSearchService.SetCurrent(CurrentFilter());
            PublishFilterStateLabels();
            if (IsReady)
            {
                _ = LoadModCatalogAsync();
            }
        }

        private void ApplyStoredFilterState(FilterState filter)
        {
            modSearchText = filter.SearchText ?? "";
            advancedAuthorFilter = filter.AuthorText ?? "";
            advancedCompatibilityFilter = filter.CompatibilityText ?? "";
            filterInstalledOnly = filter.InstalledOnly;
            filterNotInstalledOnly = filter.NotInstalledOnly;
            filterUpdatableOnly = filter.UpdatableOnly;
            filterCompatibleOnly = filter.CompatibleOnly;
            filterCachedOnly = filter.CachedOnly;
            filterUncachedOnly = filter.UncachedOnly;
            filterIncompatibleOnly = filter.IncompatibleOnly;
            filterHasReplacementOnly = filter.HasReplacementOnly;
            selectedSortOption = SortOptions.FirstOrDefault(opt => opt.Value == filter.SortOption) ?? SortOptions[0];
            sortDescending = filter.SortDescending ?? DefaultSortDescending(filter.SortOption);
            showAdvancedFilters = false;
            this.RaisePropertyChanged(nameof(SelectedSortOption));
            this.RaisePropertyChanged(nameof(SortDescending));
            PublishFilterStateLabels();
        }

        private void UpdateSelectedInstanceSummary(InstanceSummary? instance)
        {
            if (instance == null)
            {
                SelectedInstanceSummary = "Choose an instance to inspect its details.";
                return;
            }

            SelectedInstanceSummary = $"{instance.Name} ({instance.GameName})\n{instance.GameDir}";
        }

        private void UpdateCurrentInstanceContext()
        {
            var current = Instances.FirstOrDefault(inst => inst.IsCurrent)
                          ?? SelectedInstance;
            CurrentInstanceContext = current == null
                ? "Select an install to open the mod browser."
                : $"{current.GameName} • {current.GameDir}";
        }

        private void UpdateReadyInstanceHint()
        {
            ReadyInstanceHint = InstanceCount switch
            {
                0 => "Add or register an install before the browser can load.",
                1 => "This is the only registered install right now.",
                _ => "Switch installs here without leaving the mod browser.",
            };
        }

        private void ToggleAdvancedFilters()
            => ShowAdvancedFilters = !ShowAdvancedFilters;

        private void SetSelectedModDetailsSection(ModDetailsSection section)
        {
            if (selectedModDetailsSection == section)
            {
                return;
            }

            selectedModDetailsSection = section;
            PublishSelectedModSectionState();
        }

        private void ToggleDisplaySettings()
            => ShowDisplaySettings = !ShowDisplaySettings;

        private void ToggleDetailsPane()
            => ShowDetailsPane = !ShowDetailsPane;

        private void ShowBrowseSurfaceTab()
            => ShowPreviewSurface = false;

        private void ShowPreviewSurfaceTab()
            => ShowPreviewSurface = true;

        private void ResetUiScale()
            => PendingUiScalePercent = UiScaleSettings.DefaultPercent;

        private void DismissUiScaleRestartStrip()
        {
            uiScaleRestartStripDismissed = true;
            this.RaisePropertyChanged(nameof(ShowUiScaleRestartStrip));
        }

        private void ToggleQueueDrawer()
        {
            if (!HasQueuedActions && !HasApplyResult)
            {
                return;
            }

            if (IsQueueDrawerExpanded)
            {
                if (HasQueuedActions)
                {
                    queueDrawerStickyCollapsed = true;
                }
                IsQueueDrawerExpanded = false;
            }
            else
            {
                queueDrawerStickyCollapsed = false;
                IsQueueDrawerExpanded = true;
            }

            PublishQueueStateLabels();
        }

        private async Task ApplyChangesFromCollapsedQueueAsync()
        {
            queueDrawerStickyCollapsed = false;
            IsQueueDrawerExpanded = true;
            PublishQueueStateLabels();
            await ApplyQueuedChangesAsync();
        }

        private void DismissApplyResult()
        {
            ClearApplyResult();
            if (!HasQueuedActions)
            {
                IsQueueDrawerExpanded = false;
                ShowPreviewSurface = false;
                PublishQueueStateLabels();
            }
        }

        private void ExecutePrimarySelectedModAction()
        {
            if (SelectedMod == null)
            {
                return;
            }

            var queued = changesetService.FindQueuedApplyAction(SelectedMod.Identifier);
            if (queued != null)
            {
                ClearApplyResult();
                if (changesetService.Remove(queued.Identifier))
                {
                    StatusMessage = $"Removed queued {queued.ActionText.ToLowerInvariant()} for {SelectedMod.Name}.";
                }
                return;
            }

            if (ShowInstallAction)
            {
                QueueInstallSelected();
            }
            else if (ShowUpdateAction)
            {
                QueueUpdateSelected();
            }
            else if (ShowRemoveAction)
            {
                QueueRemoveSelected();
            }
        }

        private void ClearAdvancedFilters()
        {
            AdvancedAuthorFilter = "";
            AdvancedCompatibilityFilter = "";
            FilterHasReplacementOnly = false;
            ShowAdvancedFilters = false;
        }

        private void ClearAllFilters()
        {
            pendingModListScrollReset = true;
            ModSearchText = "";
            FilterInstalledOnly = false;
            FilterNotInstalledOnly = false;
            FilterUpdatableOnly = false;
            FilterCompatibleOnly = false;
            FilterCachedOnly = false;
            FilterUncachedOnly = false;
            FilterIncompatibleOnly = false;
            ClearAdvancedFilters();
        }

        public async Task ApplyCompatibleGameVersionsAsync(IReadOnlyCollection<GameVersion> compatibleVersions)
        {
            if (gameInstanceService.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            instance.SetCompatibleVersions(compatibleVersions.Distinct()
                                                         .ToList());
            RefreshCompatibleGameVersionOptions();
            ClearApplyResult();
            StatusMessage = "Updated compatible game versions for the current instance.";

            await LoadModCatalogAsync();
            if (HasQueuedActions)
            {
                await LoadPreviewAsync();
            }
        }

        private void ToggleSortOptions()
            => ShowSortOptions = !ShowSortOptions;

        private void SelectSortOption(ModSortOption option)
        {
            if (SelectedSortOption?.Value == option)
            {
                SortDescending = !SortDescending;
            }
            else
            {
                SelectedSortOption = SortOptions.First(opt => opt.Value == option);
                SortDescending = DefaultSortDescending(option);
            }

            ShowSortOptions = false;
        }

        private void RefreshCompatibleGameVersionOptions()
        {
            CompatibleGameVersionOptions.Clear();

            if (gameInstanceService.CurrentInstance is not GameInstance instance)
            {
                PublishCompatibleGameVersionState();
                return;
            }

            foreach (var option in CompatibilityVersionOptionBuilder.Build(instance))
            {
                CompatibleGameVersionOptions.Add(option);
            }

            PublishCompatibleGameVersionState();
        }

        private void ApplyCurrentSortToVisibleMods()
        {
            if (Mods.Count <= 1)
            {
                ConsumePendingModListScrollReset();
                return;
            }

            var sortedItems = SortItems(Mods).ToList();
            for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
            {
                int currentIndex = Mods.IndexOf(sortedItems[targetIndex]);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    Mods.Move(currentIndex, targetIndex);
                }
            }

            ConsumePendingModListScrollReset();
        }

        private void ReplaceVisibleMods(IEnumerable<ModListItem> items)
        {
            Mods.Clear();
            foreach (var item in items)
            {
                Mods.Add(item);
            }
        }

        private Task RestartToApplyUiScaleAsync()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                Diagnostics = "The current executable path could not be determined for restart.";
                StatusMessage = "Restart is unavailable right now.";
                return Task.CompletedTask;
            }

            try
            {
                var startInfo = new ProcessStartInfo(processPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory,
                };

                foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
                {
                    startInfo.ArgumentList.Add(arg);
                }

                _ = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("The restart process did not launch.");

                StatusMessage = $"Restarting CKAN Linux to apply {PendingUiScaleLabel} display scale…";
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Restart failed.";
            }

            return Task.CompletedTask;
        }

        private void OpenCurrentGameDirectory()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.GameDir,
                           $"Opened {CurrentInstance.Name} in your file manager.",
                           "Could not open the current game directory.");
        }

        private void OpenUserGuide()
            => LaunchExternal(HelpURLs.UserGuide,
                              "Opened the CKAN user guide.",
                              "Could not open the CKAN user guide.");

        private void OpenDiscord()
            => LaunchExternal(HelpURLs.CKANDiscord,
                              "Opened the CKAN Discord invite.",
                              "Could not open the CKAN Discord invite.");

        private void OpenGameSupport()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.Game.ModSupportURL.ToString(),
                           "Opened the KSP mod support page.",
                           "Could not open the KSP mod support page.");
        }

        private void ReportClientIssue()
            => LaunchExternal(HelpURLs.CKANIssues,
                              "Opened the CKAN client issue tracker.",
                              "Could not open the CKAN client issue tracker.");

        private void ReportMetadataIssue()
        {
            if (CurrentInstance == null)
            {
                return;
            }

            LaunchExternal(CurrentInstance.Game.MetadataBugtrackerURL.ToString(),
                           "Opened the mod metadata issue tracker.",
                           "Could not open the mod metadata issue tracker.");
        }

        private void LaunchExternal(string target,
                                    string successMessage,
                                    string failureMessage)
        {
            if (Utilities.ProcessStartURL(target))
            {
                StatusMessage = successMessage;
            }
            else
            {
                Diagnostics = $"Failed to launch: {target}";
                StatusMessage = failureMessage;
            }
        }

        private void PublishInstanceStateLabels()
        {
            UpdateCurrentInstanceContext();
            UpdateReadyInstanceHint();
            this.RaisePropertyChanged(nameof(HasInstances));
            this.RaisePropertyChanged(nameof(HasCurrentInstance));
            this.RaisePropertyChanged(nameof(CurrentInstance));
            this.RaisePropertyChanged(nameof(CurrentRegistry));
            this.RaisePropertyChanged(nameof(CurrentCache));
            this.RaisePropertyChanged(nameof(InstanceCountLabel));
            PublishCompatibleGameVersionState();
            this.RaisePropertyChanged(nameof(ShowHeaderInstanceSwitcher));
            this.RaisePropertyChanged(nameof(ShowPassiveHeaderInstanceLabel));
            this.RaisePropertyChanged(nameof(ShowStartupInstancePanel));
            this.RaisePropertyChanged(nameof(ShowReadyInstancePanel));
            this.RaisePropertyChanged(nameof(SelectedInstanceIsCurrent));
            this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
            this.RaisePropertyChanged(nameof(SelectedInstanceContextTitle));
        }

        private void ClearCatalogState()
        {
            Mods.Clear();
            filterOptionCounts = new FilterOptionCounts();
            hasFilterOptionCounts = false;
            ResetSelectedModDetails();
            SelectedMod = null;
            CatalogStatusMessage = "Select an active instance to view its mod catalog.";
            PublishCatalogStateLabels();
            PublishFilterOptionCountLabels();
        }

        private void QueueInstallSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueInstall(SelectedMod);
            StatusMessage = $"Queued install for {SelectedMod.Name}.";
        }

        private void QueueUpdateSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueUpdate(SelectedMod);
            StatusMessage = $"Queued update for {SelectedMod.Name}.";
        }

        private void QueueRemoveSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueRemove(SelectedMod);
            StatusMessage = $"Queued removal for {SelectedMod.Name}.";
        }

        private void RemoveSelectedQueuedAction()
        {
            if (SelectedQueuedAction == null)
            {
                return;
            }

            ClearApplyResult();
            if (changesetService.Remove(SelectedQueuedAction.Identifier))
            {
                StatusMessage = $"Removed queued action for {SelectedQueuedAction.Name}.";
            }
        }

        private void ClearQueuedActions()
        {
            ClearApplyResult();
            changesetService.Clear();
            StatusMessage = "Cleared all pending items.";
        }

        private void DiscardQueuedActionsForInstanceSwitch()
        {
            ClearApplyResult();
            changesetService.Clear();
            RefreshQueuedActions();
            ResetPreviewState();
            ShowPreviewSurface = false;
        }

        private void RefreshQueuedActions()
        {
            var previousSelection = SelectedQueuedAction?.Identifier;
            var previousCount = QueuedActions.Count;

            QueuedActions.Clear();
            foreach (var item in changesetService.CurrentQueue)
            {
                QueuedActions.Add(item);
            }

            SelectedQueuedAction = previousSelection != null
                ? QueuedActions.FirstOrDefault(item => item.Identifier == previousSelection) ?? QueuedActions.FirstOrDefault()
                : QueuedActions.FirstOrDefault();

            if (previousCount == 0 && QueuedActions.Count > 0)
            {
                queueDrawerStickyCollapsed = false;
                IsQueueDrawerExpanded = true;
            }
            else if (QueuedActions.Count == 0)
            {
                queueDrawerStickyCollapsed = false;
                IsQueueDrawerExpanded = false;
            }
            else if (queueDrawerStickyCollapsed)
            {
                IsQueueDrawerExpanded = false;
            }

            PublishQueueStateLabels();
            PublishSelectedModActionState();
        }

        private async Task LoadPreviewAsync()
        {
            if (!HasQueuedActions)
            {
                ResetPreviewState();
                return;
            }

            if (!HasQueuedChangeActions)
            {
                PreviewSummary = QueuedDownloadActionCount == 1
                    ? "1 download-only item is staged in the cache queue."
                    : $"{QueuedDownloadActionCount} download-only items are staged in the cache queue.";
                PreviewCanApply = false;
                ReplacePreviewCollection(PreviewDownloadsRequired, Array.Empty<string>());
                ReplacePreviewCollection(PreviewDependencies, Array.Empty<string>());
                ReplacePreviewCollection(PreviewAutoRemovals, Array.Empty<string>());
                ReplacePreviewCollection(PreviewAttentionNotes, Array.Empty<string>());
                ReplacePreviewCollection(PreviewRecommendations, Array.Empty<string>());
                ReplacePreviewCollection(PreviewSuggestions, Array.Empty<string>());
                ReplacePreviewCollection(PreviewConflicts, Array.Empty<string>());
                PublishPreviewStateLabels();
                return;
            }

            IsPreviewLoading = true;
            try
            {
                var preview = await modActionService.PreviewChangesAsync(CancellationToken.None);
                PreviewSummary = preview.SummaryText;
                PreviewCanApply = preview.CanApply;
                ReplacePreviewCollection(PreviewDownloadsRequired, preview.DownloadsRequired);
                ReplacePreviewCollection(PreviewDependencies, preview.DependencyInstalls);
                ReplacePreviewCollection(PreviewAutoRemovals, preview.AutoRemovals);
                ReplacePreviewCollection(PreviewAttentionNotes, preview.AttentionNotes);
                ReplacePreviewCollection(PreviewRecommendations, preview.Recommendations);
                ReplacePreviewCollection(PreviewSuggestions, preview.Suggestions);
                ReplacePreviewCollection(PreviewConflicts, preview.Conflicts);
                PublishPreviewStateLabels();
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                PreviewSummary = "Preview generation failed.";
                PreviewCanApply = false;
                ReplacePreviewCollection(PreviewConflicts, new[]
                {
                    $"Preview failed: {ex.Message}",
                });
                ReplacePreviewCollection(PreviewDownloadsRequired, Array.Empty<string>());
                ReplacePreviewCollection(PreviewDependencies, Array.Empty<string>());
                ReplacePreviewCollection(PreviewAutoRemovals, Array.Empty<string>());
                ReplacePreviewCollection(PreviewAttentionNotes, Array.Empty<string>());
                ReplacePreviewCollection(PreviewRecommendations, Array.Empty<string>());
                ReplacePreviewCollection(PreviewSuggestions, Array.Empty<string>());
                PublishPreviewStateLabels();
            }
            finally
            {
                IsPreviewLoading = false;
                this.RaisePropertyChanged(nameof(PreviewStatusLabel));
            }
        }

        private async Task ApplyQueuedChangesAsync()
        {
            currentExecutionStatusLabel = "Applying changes…";
            IsApplyingChanges = true;
            try
            {
                var result = await modActionService.ApplyChangesAsync(CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                SetApplyResult(new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Apply Failed",
                    Message = ex.Message,
                });
                Diagnostics = ex.Message;
                StatusMessage = "Apply failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }
        }

        private async Task InstallNowSelectedModAsync()
        {
            if (SelectedMod == null)
            {
                return;
            }

            var targetMod = SelectedMod;
            ClearApplyResult();
            currentExecutionStatusLabel = $"Installing {targetMod.Name}…";
            IsApplyingChanges = true;
            try
            {
                var result = await modActionService.InstallNowAsync(targetMod, CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    if (changesetService.FindQueuedDownloadAction(targetMod.Identifier) != null)
                    {
                        changesetService.Remove(targetMod.Identifier);
                    }

                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                SetApplyResult(new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Install Failed",
                    Message = ex.Message,
                });
                Diagnostics = ex.Message;
                StatusMessage = "Install failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }
        }

        private async Task RemoveNowSelectedModAsync()
        {
            if (SelectedMod == null)
            {
                return;
            }

            var targetMod = SelectedMod;
            ClearApplyResult();
            currentExecutionStatusLabel = $"Removing {targetMod.Name}…";
            IsApplyingChanges = true;
            try
            {
                var result = await modActionService.RemoveNowAsync(targetMod, CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    if (changesetService.FindQueuedDownloadAction(targetMod.Identifier) != null)
                    {
                        changesetService.Remove(targetMod.Identifier);
                    }

                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                SetApplyResult(new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Removal Failed",
                    Message = ex.Message,
                });
                Diagnostics = ex.Message;
                StatusMessage = "Removal failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }
        }

        private async Task DownloadQueuedAsync()
        {
            currentExecutionStatusLabel = "Downloading queued files…";
            IsApplyingChanges = true;
            try
            {
                var result = await modActionService.DownloadQueuedAsync(CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                SetApplyResult(new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Downloads Failed",
                    Message = ex.Message,
                });
                Diagnostics = ex.Message;
                StatusMessage = "Downloads failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }
        }

        private void ResetSelectedModDetails()
        {
            SelectedModTitle = "No mod selected";
            SelectedModSubtitle = "Choose a mod to inspect its details.";
            SelectedModAuthors = "";
            SelectedModVersions = "";
            SelectedModInstallState = "";
            SelectedModCompatibility = "";
            SelectedModModuleKind = "";
            SelectedModLicense = "";
            SelectedModReleaseDate = "";
            SelectedModDownloadSize = "";
            SelectedModDownloadCount = "";
            SelectedModRelationships = "";
            SelectedModDependencyCountLabel = "";
            SelectedModRecommendationCountLabel = "";
            SelectedModSuggestionCountLabel = "";
            SelectedModIsInstalled = false;
            SelectedModHasUpdate = false;
            SelectedModIsCached = false;
            SelectedModIsIncompatible = false;
            SelectedModHasReplacement = false;
            SelectedModBody = "The details pane will show summary, description, compatibility, and install state.";
            SetSelectedModDetailsSection(ModDetailsSection.Overview);
        }

        private void PublishCatalogStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasMods));
            this.RaisePropertyChanged(nameof(ModCountLabel));
            this.RaisePropertyChanged(nameof(ShowCatalogSkeleton));
            this.RaisePropertyChanged(nameof(ShowModList));
            this.RaisePropertyChanged(nameof(ShowEmptyModResults));
        }

        private void PublishSelectedModDisplayState()
        {
            this.RaisePropertyChanged(nameof(ShowSelectedModPlaceholder));
            this.RaisePropertyChanged(nameof(ShowSelectedModLoadingState));
            this.RaisePropertyChanged(nameof(ShowSelectedModContent));
            this.RaisePropertyChanged(nameof(SelectedModLoadingTitle));
            PublishSelectedModSectionState();
        }

        private void PublishSelectedModSectionState()
        {
            this.RaisePropertyChanged(nameof(ShowOverviewDetailsSection));
            this.RaisePropertyChanged(nameof(ShowMetadataDetailsSection));
            this.RaisePropertyChanged(nameof(ShowRelationshipsDetailsSection));
            this.RaisePropertyChanged(nameof(ShowDescriptionDetailsSection));
            this.RaisePropertyChanged(nameof(OverviewDetailsSectionSelected));
            this.RaisePropertyChanged(nameof(MetadataDetailsSectionSelected));
            this.RaisePropertyChanged(nameof(RelationshipsDetailsSectionSelected));
            this.RaisePropertyChanged(nameof(DescriptionDetailsSectionSelected));
        }

        private void PublishFilterStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasActiveAdvancedFilters));
            this.RaisePropertyChanged(nameof(ActiveFilterCount));
            this.RaisePropertyChanged(nameof(HasActiveFilters));
            this.RaisePropertyChanged(nameof(AdvancedFilterSummary));
            this.RaisePropertyChanged(nameof(MoreFiltersLabel));
            this.RaisePropertyChanged(nameof(MoreFiltersButtonBackground));
            this.RaisePropertyChanged(nameof(MoreFiltersButtonBorderBrush));
            this.RaisePropertyChanged(nameof(ClearFiltersButtonBackground));
            this.RaisePropertyChanged(nameof(ClearFiltersButtonBorderBrush));
            this.RaisePropertyChanged(nameof(SortMenuLabel));
            this.RaisePropertyChanged(nameof(NameSortLabel));
            this.RaisePropertyChanged(nameof(AuthorSortLabel));
            this.RaisePropertyChanged(nameof(PopularitySortLabel));
            this.RaisePropertyChanged(nameof(CompatibilitySortLabel));
            this.RaisePropertyChanged(nameof(VersionSortLabel));
            this.RaisePropertyChanged(nameof(InstalledFirstSortLabel));
            this.RaisePropertyChanged(nameof(UpdatesFirstSortLabel));
            this.RaisePropertyChanged(nameof(NameSortSelected));
            this.RaisePropertyChanged(nameof(AuthorSortSelected));
            this.RaisePropertyChanged(nameof(PopularitySortSelected));
            this.RaisePropertyChanged(nameof(CompatibilitySortSelected));
            this.RaisePropertyChanged(nameof(VersionSortSelected));
            this.RaisePropertyChanged(nameof(InstalledFirstSortSelected));
            this.RaisePropertyChanged(nameof(UpdatesFirstSortSelected));
        }

        private void PublishFilterOptionCountLabels()
        {
            this.RaisePropertyChanged(nameof(CompatibleFilterLabel));
            this.RaisePropertyChanged(nameof(InstalledFilterLabel));
            this.RaisePropertyChanged(nameof(UpdatableFilterLabel));
            this.RaisePropertyChanged(nameof(ReplaceableFilterLabel));
            this.RaisePropertyChanged(nameof(CachedFilterLabel));
            this.RaisePropertyChanged(nameof(UncachedFilterLabel));
            this.RaisePropertyChanged(nameof(NotInstalledFilterLabel));
            this.RaisePropertyChanged(nameof(IncompatibleFilterLabel));
        }

        private static bool DefaultSortDescending(ModSortOption sortOption)
            => sortOption == ModSortOption.Popularity
               || sortOption == ModSortOption.InstalledFirst
               || sortOption == ModSortOption.UpdatesFirst;

        private IEnumerable<ModListItem> SortItems(IEnumerable<ModListItem> items)
        {
            ModSortOption sortOption = SelectedSortOption?.Value ?? ModSortOption.Name;
            bool descending = SortDescending;

            return sortOption switch
            {
                ModSortOption.Author
                    => descending
                        ? items.OrderByDescending(item => item.Author, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Author, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Popularity
                    => descending
                        ? items.OrderByDescending(item => item.DownloadCount ?? 0)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.DownloadCount ?? 0)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Compatibility
                    => descending
                        ? items.OrderByDescending(item => item.Compatibility, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Compatibility, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Version
                    => descending
                        ? items.OrderByDescending(item => item.LatestVersion, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.LatestVersion, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.InstalledFirst
                    => descending
                        ? items.OrderByDescending(item => item.IsInstalled)
                               .ThenByDescending(item => item.HasUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.IsInstalled)
                               .ThenByDescending(item => item.HasUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.UpdatesFirst
                    => descending
                        ? items.OrderByDescending(item => item.HasUpdate)
                               .ThenByDescending(item => item.IsInstalled)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.HasUpdate)
                               .ThenByDescending(item => item.IsInstalled)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                _
                    => descending
                        ? items.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
            };
        }

        private void ConsumePendingModListScrollReset()
        {
            if (pendingModListScrollReset)
            {
                pendingModListScrollReset = false;
                ModListScrollResetRequestId++;
            }
        }

        private string SortOptionLabel(ModSortOption sortOption, string baseLabel)
            => baseLabel;

        private string FormatFilterOptionLabel(string label, int count)
            => hasFilterOptionCounts
                ? $"{label} ({count})"
                : label;

        private void PublishQueueStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasQueuedActions));
            this.RaisePropertyChanged(nameof(QueuedChangeActionCount));
            this.RaisePropertyChanged(nameof(QueuedDownloadActionCount));
            this.RaisePropertyChanged(nameof(HasQueuedChangeActions));
            this.RaisePropertyChanged(nameof(HasQueuedDownloadActions));
            this.RaisePropertyChanged(nameof(IsQueueDrawerExpanded));
            this.RaisePropertyChanged(nameof(ShowEmptyQueueState));
            this.RaisePropertyChanged(nameof(ShowEmptyQueueStub));
            this.RaisePropertyChanged(nameof(ShowCollapsedQueuedActionsStub));
            this.RaisePropertyChanged(nameof(ShowCollapsedApplyResultStub));
            this.RaisePropertyChanged(nameof(ShowExpandedQueuePanel));
            this.RaisePropertyChanged(nameof(QueueCountLabel));
            this.RaisePropertyChanged(nameof(InstanceSwitchDiscardPrompt));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubTitle));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubSummary));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBackground));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBorderBrush));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonLabel));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBackground));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBorderBrush));
            this.RaisePropertyChanged(nameof(PreviewShowsEmptyCard));
            this.RaisePropertyChanged(nameof(PreviewShowsLoadingCard));
            this.RaisePropertyChanged(nameof(PreviewShowsReadyCard));
            this.RaisePropertyChanged(nameof(PreviewShowsBlockedCard));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewQueuedCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueCountLabel));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBackground));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBorderBrush));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBackground));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBorderBrush));
        }

        private void PublishSelectedModActionState()
        {
            this.RaisePropertyChanged(nameof(ShowInstallAction));
            this.RaisePropertyChanged(nameof(ShowUpdateAction));
            this.RaisePropertyChanged(nameof(ShowRemoveAction));
            this.RaisePropertyChanged(nameof(HasSelectedModQueuedAction));
            this.RaisePropertyChanged(nameof(HasSelectedModQueuedDownload));
            this.RaisePropertyChanged(nameof(ShowInstallNowAction));
            this.RaisePropertyChanged(nameof(ShowRemoveNowAction));
            this.RaisePropertyChanged(nameof(ShowPrimarySelectedModAction));
            this.RaisePropertyChanged(nameof(ShowSelectedModActionUnavailableNote));
            this.RaisePropertyChanged(nameof(SelectedModActionUnavailableNote));
            this.RaisePropertyChanged(nameof(PrimarySelectedModActionLabel));
            this.RaisePropertyChanged(nameof(PrimarySelectedModActionBackground));
            this.RaisePropertyChanged(nameof(PrimarySelectedModActionBorderBrush));
            this.RaisePropertyChanged(nameof(SelectedModQueueStatus));
        }

        private void PublishCompatibleGameVersionState()
        {
            this.RaisePropertyChanged(nameof(CurrentCompatibleGameVersionLabel));
            this.RaisePropertyChanged(nameof(ShowCompatibleGameVersionOptions));
            this.RaisePropertyChanged(nameof(ShowCompatibleGameVersionWarning));
            this.RaisePropertyChanged(nameof(CompatibleGameVersionsSummary));
            this.RaisePropertyChanged(nameof(CompatibleGameVersionsHint));
            this.RaisePropertyChanged(nameof(CompatibleGameVersionsWarningText));
        }

        private void PublishPreviewStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasPreviewDownloadsRequired));
            this.RaisePropertyChanged(nameof(HasPreviewDependencies));
            this.RaisePropertyChanged(nameof(HasPreviewAutoRemovals));
            this.RaisePropertyChanged(nameof(HasPreviewAttentionNotes));
            this.RaisePropertyChanged(nameof(HasPreviewRecommendations));
            this.RaisePropertyChanged(nameof(HasPreviewSuggestions));
            this.RaisePropertyChanged(nameof(HasPreviewConflicts));
            this.RaisePropertyChanged(nameof(PreviewStatusLabel));
            this.RaisePropertyChanged(nameof(PreviewShowsEmptyCard));
            this.RaisePropertyChanged(nameof(PreviewShowsLoadingCard));
            this.RaisePropertyChanged(nameof(PreviewShowsReadyCard));
            this.RaisePropertyChanged(nameof(PreviewShowsBlockedCard));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewQueuedCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDependencyCountLabel));
            this.RaisePropertyChanged(nameof(ShowPreviewQueuedActions));
            this.RaisePropertyChanged(nameof(PreviewQueuedGuidance));
            this.RaisePropertyChanged(nameof(ShowPreviewDownloadQueueGuidance));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueGuidanceTitle));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueGuidance));
            this.RaisePropertyChanged(nameof(ShowPreviewDependencyGuidance));
            this.RaisePropertyChanged(nameof(PreviewDependencyGuidanceTitle));
            this.RaisePropertyChanged(nameof(PreviewDependencyGuidance));
            this.RaisePropertyChanged(nameof(PreviewAutoRemovalCountLabel));
            this.RaisePropertyChanged(nameof(PreviewConflictCountLabel));
            this.RaisePropertyChanged(nameof(PreviewAttentionCountLabel));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBackground));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBorderBrush));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBackground));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBorderBrush));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubSummary));
        }

        private void SetApplyResult(ApplyChangesResult result)
        {
            ApplyResultTitle = result.Title;
            ApplyResultMessage = result.Message;
            ReplacePreviewCollection(ApplyResultSummaryLines, result.SummaryLines);
            ReplacePreviewCollection(ApplyResultFollowUpLines, result.FollowUpLines);

            (ApplyResultBackground, ApplyResultBorderBrush) = result.Kind switch
            {
                ApplyResultKind.Success => ("#1F3A2A", "#3D7A57"),
                ApplyResultKind.Warning => ("#4A3920", "#9A7B37"),
                ApplyResultKind.Blocked => ("#4A232A", "#934354"),
                ApplyResultKind.Canceled => ("#2E3540", "#566271"),
                ApplyResultKind.Error => ("#4A232A", "#934354"),
                _ => ("#20262D", "#2F3741"),
            };

            PublishApplyResultStateLabels();
        }

        private void ClearApplyResult()
        {
            ApplyResultTitle = "";
            ApplyResultMessage = "";
            ApplyResultBackground = "#20262D";
            ApplyResultBorderBrush = "#2F3741";
            ReplacePreviewCollection(ApplyResultSummaryLines, Array.Empty<string>());
            ReplacePreviewCollection(ApplyResultFollowUpLines, Array.Empty<string>());
            PublishApplyResultStateLabels();
        }

        private void PublishApplyResultStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasApplyResult));
            this.RaisePropertyChanged(nameof(HasApplyResultSummaryLines));
            this.RaisePropertyChanged(nameof(HasApplyResultFollowUpLines));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBackground));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBorderBrush));
            this.RaisePropertyChanged(nameof(ShowEmptyQueueStub));
            this.RaisePropertyChanged(nameof(ShowCollapsedApplyResultStub));
            this.RaisePropertyChanged(nameof(ShowExpandedQueuePanel));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubTitle));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubSummary));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBackground));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBorderBrush));
        }

        private void ResetPreviewState()
        {
            PreviewSummary = "Queue install, update, or remove actions to build an apply preview. Right-click a mod for download-only.";
            PreviewCanApply = false;
            ReplacePreviewCollection(PreviewDownloadsRequired, Array.Empty<string>());
            ReplacePreviewCollection(PreviewDependencies, Array.Empty<string>());
            ReplacePreviewCollection(PreviewAutoRemovals, Array.Empty<string>());
            ReplacePreviewCollection(PreviewAttentionNotes, Array.Empty<string>());
            ReplacePreviewCollection(PreviewRecommendations, Array.Empty<string>());
            ReplacePreviewCollection(PreviewSuggestions, Array.Empty<string>());
            ReplacePreviewCollection(PreviewConflicts, Array.Empty<string>());
            PublishPreviewStateLabels();
        }

        private static void ReplacePreviewCollection(ObservableCollection<string> target,
                                                     System.Collections.Generic.IEnumerable<string> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private static string BuildInstallState(ModDetailsModel details)
        {
            var parts = new List<string>();

            parts.Add(details.IsInstalled
                ? $"Installed {details.InstalledVersion}"
                : "Not installed");

            if (details.HasUpdate)
            {
                parts.Add($"Update available to {details.LatestVersion}");
            }
            if (details.IsIncompatible)
            {
                parts.Add("Currently incompatible");
            }
            if (details.HasReplacement)
            {
                parts.Add("Replacement available");
            }

            return string.Join(" • ", parts);
        }

        private bool IsCurrentSelectedModRequest(string identifier,
                                                 int    requestId)
            => requestId == selectedModLoadRequestId
               && string.Equals(identifier, SelectedMod?.Identifier, StringComparison.OrdinalIgnoreCase);

        private bool MessageContains(string value)
            => StatusMessage?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string CountLabel(int count,
                                         string singular,
                                         string plural)
            => count == 1
                ? $"1 {singular}"
                : $"{count} {plural}";
    }
}
