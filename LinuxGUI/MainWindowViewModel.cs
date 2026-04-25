using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.Exporters;
using CKAN.IO;
using CKAN.Types;
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

        private enum GameLaunchMode
        {
            Direct,
            Steam,
        }

        private sealed class ConflictSideInfo
        {
            public string DisplayName { get; init; } = "";

            public QueuedActionModel? QueuedAction { get; init; }

            public ModListItem? Mod { get; init; }
        }

        private const double MinBrowserMetadataColumnWidth  = 196;
        private const double MaxBrowserMetadataColumnWidth  = 1040;
        private const double MinBrowserNameColumnWidth      = 140;
        private const double MinBrowserDownloadsColumnWidth = 64;
        private const double MaxBrowserDownloadsColumnWidth = MaxBrowserMetadataColumnWidth;
        private const double MinBrowserReleasedColumnWidth  = 64;
        private const double MaxBrowserReleasedColumnWidth  = MaxBrowserMetadataColumnWidth;
        private const double MinBrowserInstalledColumnWidth = 64;
        private const double MaxBrowserInstalledColumnWidth = MaxBrowserMetadataColumnWidth;
        private const double MinBrowserVersionColumnWidth   = 58;
        private const int    DevQueueSmokeTargetActionCount = 20;

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
        private string  advancedNameFilter = "";
        private string  advancedIdentifierFilter = "";
        private string  advancedAuthorFilter = "";
        private string  advancedSummaryFilter = "";
        private string  advancedDescriptionFilter = "";
        private string  advancedLicenseFilter = "";
        private string  advancedLanguageFilter = "";
        private string  advancedDependsFilter = "";
        private string  advancedRecommendsFilter = "";
        private string  advancedSuggestsFilter = "";
        private string  advancedConflictsFilter = "";
        private string  advancedSupportsFilter = "";
        private string  advancedTagsFilter = "";
        private string  advancedLabelsFilter = "";
        private string  advancedCompatibilityFilter = "";
        private double  browserMetadataColumnWidth = ModBrowserColumnLayout.DefaultMetadataColumnWidth;
        private double  browserDownloadsColumnWidth = ModBrowserColumnLayout.DefaultDownloadsColumnWidth;
        private double  browserReleasedColumnWidth = ModBrowserColumnLayout.DefaultReleasedColumnWidth;
        private double  browserInstalledColumnWidth = ModBrowserColumnLayout.DefaultInstalledColumnWidth;
        private SortOptionItem? selectedSortOption;
        private bool    sortDescending;
        private string  catalogStatusMessage = "Select an instance to load the mod catalog.";
        private string  selectedModTitle     = "No mod selected";
        private string  selectedModSubtitle  = "Choose a mod to inspect its details.";
        private string  selectedModAuthors   = "";
        private string  selectedModVersions  = "";
        private string  selectedModInstallState = "";
        private string  selectedModCacheState = "";
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
        private string? selectedModCachedArchivePath;
        private string  relationshipBrowserScopeText = "";
        private ModVersionChoiceItem? selectedModVersionChoice;
        private string  selectedModBody      = "The details pane will show summary, description, compatibility, and install state.";
        private string  previewSummary = "Queue install, update, or remove actions to build an apply preview. Right-click a mod to add it to cache.";
        private string  currentExecutionTitle = "Applying Changes";
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
        private bool    filterNotUpdatableOnly;
        private bool    filterCompatibleOnly;
        private bool    filterCachedOnly;
        private bool    filterUncachedOnly;
        private bool    filterIncompatibleOnly;
        private bool    filterHasReplacementOnly;
        private bool    filterNoReplacementOnly;
        private bool    isRefreshing;
        private bool    showExecutionResultOverlay;
        private bool    returnToBrowseAfterExecutionResult;
        private bool    hasSelectedInstance;
        private bool    isCatalogLoading;
        private bool    isSelectedModLoading;
        private bool    isPreviewLoading;
        private bool    isApplyingChanges;
        private bool    isUserBusy;
        private bool    showSortOptions;
        private bool    showAdvancedFilters;
        private bool    showAdvancedFilterEditor;
        private bool    showTagFilterPicker;
        private bool    showLabelFilterPicker;
        private bool    showDisplaySettings;
        private bool    showDetailsPane = true;
        private bool    showPreviewSurface;
        private bool    surfaceViewTogglePinned;
        private bool    uiScaleRestartStripDismissed;
        private bool    isQueueDrawerExpanded;
        private bool    queueDrawerStickyCollapsed;
        private bool    previewCanApply;
        private bool    selectedModIsInstalled;
        private bool    selectedModIsAutodetected;
        private bool    selectedModHasUpdate;
        private bool    selectedModIsCached;
        private bool    selectedModIsIncompatible;
        private bool    selectedModHasReplacement;
        private bool    selectedModDependenciesExpanded;
        private bool    selectedModRecommendationsExpanded;
        private bool    selectedModSuggestionsExpanded;
        private bool    hasFilterOptionCounts;
        private int     appliedUiScalePercent;
        private double  pendingUiScalePercent;
        private int     catalogLoadRequestId;
        private int     selectedModLoadRequestId;
        private int     modListScrollResetRequestId;
        private bool    pendingModListScrollReset;
        private bool    preserveSelectedModDuringSortReorder;
        private bool    suppressFilterAutoRefresh;
        private bool    hasRestoredQueuedActionSnapshot;
        private bool    hasSeededDevQueueSmoke;
        private bool    suppressQueueSnapshotPersistence;
        private bool    previewConflictPopupDismissed;
        private string  dismissedPreviewConflictKey = "";
        private int     selectedPreviewConflictCount;
        private string? selectedPreviewConflict;
        private readonly HashSet<string> selectedPreviewConflicts = new(StringComparer.Ordinal);
        private IReadOnlyList<ModListItem> allCatalogItems = Array.Empty<ModListItem>();
        private IReadOnlyList<CatalogSkeletonRow> catalogSkeletonRows = Array.Empty<CatalogSkeletonRow>();
        private IReadOnlySet<string> relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ModDetailsModel? selectedModDetails;
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
            AvailableTagOptions = new ObservableCollection<FilterTagOptionItem>();
            AvailableLabelOptions = new ObservableCollection<FilterTagOptionItem>();
            QueuedActions = new ObservableCollection<QueuedActionModel>();
            CompatibleGameVersionOptions = new ObservableCollection<CompatibilityVersionOption>();
            SelectedModAvailableVersions = new ObservableCollection<ModVersionChoiceItem>();
            SelectedModResourceLinks = new ObservableCollection<ModResourceLinkItem>();
            SelectedModDependencies = new ObservableCollection<ModRelationshipItem>();
            SelectedModRecommendations = new ObservableCollection<ModRelationshipItem>();
            SelectedModSuggestions = new ObservableCollection<ModRelationshipItem>();
            PreviewDownloadsRequired = new ObservableCollection<string>();
            PreviewDependencies = new ObservableCollection<string>();
            PreviewAutoRemovals = new ObservableCollection<string>();
            PreviewAttentionNotes = new ObservableCollection<string>();
            PreviewRecommendations = new ObservableCollection<string>();
            PreviewSuggestions = new ObservableCollection<string>();
            PreviewConflicts = new ObservableCollection<string>();
            PreviewConflictChoices = new ObservableCollection<PreviewConflictChoiceItem>();
            ApplyResultSummaryLines = new ObservableCollection<string>();
            ApplyResultFollowUpLines = new ObservableCollection<string>();
            ApplyBrowserColumnLayout(appSettingsService.ModBrowserColumnLayout);
            CatalogSkeletonRows = BuildCatalogSkeletonRows(appSettingsService.CatalogSkeletonRows);
            SortOptions = new[]
            {
                new SortOptionItem { Value = ModSortOption.Name, Label = "Name" },
                new SortOptionItem { Value = ModSortOption.Author, Label = "Author" },
                new SortOptionItem { Value = ModSortOption.Popularity, Label = "Downloads" },
                new SortOptionItem { Value = ModSortOption.Compatibility, Label = "Compatibility" },
                new SortOptionItem { Value = ModSortOption.ReleaseDate, Label = "Released" },
                new SortOptionItem { Value = ModSortOption.InstallDate, Label = "Install Date" },
                new SortOptionItem { Value = ModSortOption.Version, Label = "Version" },
                new SortOptionItem { Value = ModSortOption.InstalledFirst, Label = "Installed First" },
                new SortOptionItem { Value = ModSortOption.UpdatesFirst, Label = "Updates First" },
            };
            selectedSortOption = SortOptions[0];
            sortDescending = DefaultSortDescending(ModSortOption.Name);
            appliedUiScalePercent = UiScaleSettings.NormalizePercent(appSettingsService.UiScalePercent);
            pendingUiScalePercent = appliedUiScalePercent;
            showDetailsPane = false;

            var canRefresh = this.WhenAnyValue(vm => vm.IsRefreshing,
                                               vm => vm.IsApplyingChanges,
                                               vm => vm.IsCatalogLoading,
                                               (refreshing, applying, loading) => !refreshing && !applying && !loading);
            var canUseSelected = this.WhenAnyValue(vm => vm.SelectedInstance,
                                                   vm => vm.IsRefreshing,
                                                   vm => vm.IsApplyingChanges,
                                                   vm => vm.IsCatalogLoading,
                                                   (inst, refreshing, applying, loading) => inst != null && !refreshing && !applying && !loading);
            var canQueueInstall = this.WhenAnyValue(vm => vm.SelectedMod,
                                                    vm => vm.IsApplyingChanges,
                                                    (mod, applying) => mod != null
                                                                       && !applying
                                                                       && !mod.IsInstalled
                                                                       && !mod.IsIncompatible);
            var canQueueUpdate = this.WhenAnyValue(vm => vm.SelectedMod,
                                                   vm => vm.IsApplyingChanges,
                                                   (mod, applying) => mod?.IsInstalled == true
                                                                      && mod.HasVersionUpdate
                                                                      && !applying);
            var canQueueRemove = this.WhenAnyValue(vm => vm.SelectedMod,
                                                   vm => vm.IsApplyingChanges,
                                                   (mod, applying) => mod?.IsInstalled == true
                                                                      && !applying);
            var canRemoveQueuedAction = this.WhenAnyValue(vm => vm.SelectedQueuedAction,
                                                          vm => vm.IsApplyingChanges,
                                                          (action, applying) => action != null && !applying);
            var canRemoveSelectedPreviewConflict = this.WhenAnyValue(vm => vm.SelectedPreviewConflictCount,
                                                                     vm => vm.IsApplyingChanges,
                                                                     (count, applying) => count > 0 && !applying);
            var canClearQueue = this.WhenAnyValue(vm => vm.HasQueuedActions,
                                                  vm => vm.IsApplyingChanges,
                                                  (hasActions, applying) => hasActions && !applying);
            var canToggleAdvancedFilters = this.WhenAnyValue(vm => vm.IsApplyingChanges,
                                                             vm => vm.IsCatalogLoading,
                                                             (applying, loading) => !applying && !loading);
            var canClearFilters = this.WhenAnyValue(vm => vm.HasActiveFilters,
                                                    vm => vm.IsApplyingChanges,
                                                    (hasFilters, applying) => hasFilters && !applying);
            var canClearAdvancedText = this.WhenAnyValue(vm => vm.HasAdvancedFilterText,
                                                         vm => vm.IsApplyingChanges,
                                                         (hasText, applying) => hasText && !applying);
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
            var canPlayDirect = this.WhenAnyValue(vm => vm.CanPlayDirect);
            var canPlayViaSteam = this.WhenAnyValue(vm => vm.CanPlayViaSteam);
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
            SetCurrentInstanceCommand = ReactiveCommand.CreateFromTask(
                SetCurrentInstanceAsync,
                canUseSelected);
            QueueInstallCommand = ReactiveCommand.Create(QueueInstallSelected, canQueueInstall);
            QueueUpdateCommand = ReactiveCommand.Create(QueueUpdateSelected, canQueueUpdate);
            QueueRemoveCommand = ReactiveCommand.Create(QueueRemoveSelected, canQueueRemove);
            RemoveQueuedActionCommand = ReactiveCommand.Create(RemoveSelectedQueuedAction, canRemoveQueuedAction);
            RemoveSelectedPreviewConflictCommand = ReactiveCommand.Create(RemoveSelectedPreviewConflict,
                                                                          canRemoveSelectedPreviewConflict);
            ClearQueueCommand = ReactiveCommand.Create(ClearQueuedActions, canClearQueue);
            ToggleAdvancedFiltersCommand = ReactiveCommand.Create(ToggleAdvancedFilters, canToggleAdvancedFilters);
            ToggleAdvancedFilterEditorCommand = ReactiveCommand.Create(ToggleAdvancedFilterEditor);
            ToggleTagFilterPickerCommand = ReactiveCommand.Create(ToggleTagFilterPicker);
            ToggleLabelFilterPickerCommand = ReactiveCommand.Create(ToggleLabelFilterPicker);
            ToggleSortOptionsCommand = ReactiveCommand.Create(ToggleSortOptions);
            ClearAdvancedFiltersCommand = ReactiveCommand.Create(ClearAdvancedFilters, canClearAdvancedText);
            ClearFiltersCommand = ReactiveCommand.Create(ClearAllFilters, canClearFilters);
            ClearPopupFiltersCommand = ReactiveCommand.Create(ClearPopupFilters);
            ClearTagFilterCommand = ReactiveCommand.Create(ClearTagFilter);
            SelectTagFilterCommand = ReactiveCommand.Create<FilterTagOptionItem?>(SelectTagFilter);
            ClearLabelFilterCommand = ReactiveCommand.Create(ClearLabelFilter);
            SelectLabelFilterCommand = ReactiveCommand.Create<FilterTagOptionItem?>(SelectLabelFilter);
            ToggleDisplaySettingsCommand = ReactiveCommand.Create(ToggleDisplaySettings);
            ToggleDetailsPaneCommand = ReactiveCommand.Create(ToggleDetailsPane);
            ShowBrowseSurfaceCommand = ReactiveCommand.Create(ShowBrowseSurfaceTab);
            ShowPreviewSurfaceCommand = ReactiveCommand.Create(ShowPreviewSurfaceTab);
            DismissPreviewConflictPopupCommand = ReactiveCommand.Create(DismissPreviewConflictPopup);
            ResetUiScaleCommand = ReactiveCommand.Create(ResetUiScale);
            DismissUiScaleRestartStripCommand = ReactiveCommand.Create(DismissUiScaleRestartStrip);
            RestartToApplyUiScaleCommand = ReactiveCommand.CreateFromTask(RestartToApplyUiScaleAsync,
                                                                          canRestartForUiScale);
            ApplyChangesCommand = ReactiveCommand.CreateFromTask(() => ApplyQueuedChangesAsync(),
                                                                 canApplyChanges);
            DownloadQueuedCommand = ReactiveCommand.CreateFromTask(DownloadQueuedAsync, canDownloadQueued);
            ToggleQueueDrawerCommand = ReactiveCommand.Create(ToggleQueueDrawer);
            ApplyChangesFromCollapsedQueueCommand = ReactiveCommand.CreateFromTask(ApplyChangesFromCollapsedQueueAsync,
                                                                                   canApplyChanges);
            DismissApplyResultCommand = ReactiveCommand.Create(DismissApplyResult);
            AcknowledgeExecutionResultCommand = ReactiveCommand.Create(AcknowledgeExecutionResult);
            OpenCurrentGameDirectoryCommand = ReactiveCommand.Create(OpenCurrentGameDirectory,
                                                                    this.WhenAnyValue(vm => vm.HasCurrentInstance));
            PlayDirectCommand = ReactiveCommand.CreateFromTask(() => PlayGameAsync(GameLaunchMode.Direct),
                                                               canPlayDirect);
            PlayViaSteamCommand = ReactiveCommand.CreateFromTask(() => PlayGameAsync(GameLaunchMode.Steam),
                                                                 canPlayViaSteam);
            OpenSelectedModCacheLocationCommand = ReactiveCommand.Create(
                OpenSelectedModCacheLocation,
                this.WhenAnyValue(vm => vm.ShowOpenSelectedModCacheLocationAction));
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
            ToggleSelectedModDependenciesCommand = ReactiveCommand.Create(ToggleSelectedModDependenciesExpanded);
            ToggleSelectedModRecommendationsCommand = ReactiveCommand.Create(ToggleSelectedModRecommendationsExpanded);
            ToggleSelectedModSuggestionsCommand = ReactiveCommand.Create(ToggleSelectedModSuggestionsExpanded);
            ViewSelectedModDependenciesInBrowserCommand = ReactiveCommand.Create(() => ShowRelationshipsInBrowser("dependencies", SelectedModDependencies));
            ViewSelectedModRecommendationsInBrowserCommand = ReactiveCommand.Create(() => ShowRelationshipsInBrowser("recommendations", SelectedModRecommendations));
            ViewSelectedModSuggestionsInBrowserCommand = ReactiveCommand.Create(() => ShowRelationshipsInBrowser("suggestions", SelectedModSuggestions));
            ClearRelationshipBrowserScopeCommand = ReactiveCommand.Create(ClearRelationshipBrowserScope);
            ShowOverviewDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Overview));
            ShowMetadataDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Metadata));
            ShowRelationshipsDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Relationships));
            ShowDescriptionDetailsCommand = ReactiveCommand.Create(() => SetSelectedModDetailsSection(ModDetailsSection.Description));
            SelectNameSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Name));
            SelectAuthorSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Author));
            SelectPopularitySortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Popularity));
            SelectCompatibilitySortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.Compatibility));
            SelectReleaseDateSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.ReleaseDate));
            SelectInstallDateSortCommand = ReactiveCommand.Create(() => SelectSortOption(ModSortOption.InstallDate));
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
                    this.WhenAnyValue(vm => vm.AdvancedNameFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedIdentifierFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedAuthorFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedSummaryFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedDescriptionFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedLicenseFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedLanguageFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedDependsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedRecommendsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedSuggestsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedConflictsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedSupportsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedTagsFilter).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.AdvancedLabelsFilter).Select(_ => Unit.Default),
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
                    this.WhenAnyValue(vm => vm.FilterNotUpdatableOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterCompatibleOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterCachedOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterUncachedOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterIncompatibleOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterHasReplacementOnly).Select(_ => Unit.Default),
                    this.WhenAnyValue(vm => vm.FilterNoReplacementOnly).Select(_ => Unit.Default))
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

        public ObservableCollection<FilterTagOptionItem> AvailableTagOptions { get; }

        public ObservableCollection<FilterTagOptionItem> AvailableLabelOptions { get; }

        public ObservableCollection<QueuedActionModel> QueuedActions { get; }

        public ObservableCollection<CompatibilityVersionOption> CompatibleGameVersionOptions { get; }

        public ObservableCollection<ModVersionChoiceItem> SelectedModAvailableVersions { get; }

        public ObservableCollection<ModResourceLinkItem> SelectedModResourceLinks { get; }

        public ObservableCollection<ModRelationshipItem> SelectedModDependencies { get; }

        public ObservableCollection<ModRelationshipItem> SelectedModRecommendations { get; }

        public ObservableCollection<ModRelationshipItem> SelectedModSuggestions { get; }

        public ObservableCollection<string> PreviewDownloadsRequired { get; }

        public ObservableCollection<string> PreviewDependencies { get; }

        public ObservableCollection<string> PreviewAutoRemovals { get; }

        public ObservableCollection<string> PreviewAttentionNotes { get; }

        public ObservableCollection<string> PreviewRecommendations { get; }

        public ObservableCollection<string> PreviewSuggestions { get; }

        public ObservableCollection<string> PreviewConflicts { get; }

        public ObservableCollection<PreviewConflictChoiceItem> PreviewConflictChoices { get; }

        public IReadOnlyList<CatalogSkeletonRow> CatalogSkeletonRows
        {
            get => catalogSkeletonRows;
            private set => this.RaiseAndSetIfChanged(ref catalogSkeletonRows, value);
        }

        public double BrowserMetadataColumnWidth => browserMetadataColumnWidth;

        public double BrowserDownloadsColumnWidth => browserDownloadsColumnWidth;

        public double BrowserReleasedColumnWidth => browserReleasedColumnWidth;

        public double BrowserInstalledColumnWidth => browserInstalledColumnWidth;

        public GridLength BrowserMetadataColumnGridLength
            => new GridLength(browserMetadataColumnWidth);

        public GridLength BrowserDownloadsColumnGridLength
            => new GridLength(browserDownloadsColumnWidth);

        public GridLength BrowserReleasedColumnGridLength
            => new GridLength(browserReleasedColumnWidth);

        public GridLength BrowserInstalledColumnGridLength
            => new GridLength(browserInstalledColumnWidth);

        public double BrowserColumnResizeMaxMetadataWidth(double tableWidth)
            => MaxBrowserMetadataColumnWidthForTable(tableWidth);

        public void ResizeBrowserNameDownloadsDivider(double startMetadataWidth,
                                                       double startDownloadsWidth,
                                                       double startReleasedWidth,
                                                       double startInstalledWidth,
                                                       double maxMetadataWidth,
                                                       double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var minimumMetadataWidth = MinimumBrowserMetadataColumnWidth();
            var actualDelta = ClampDimension(startMetadataWidth - delta,
                                             minimumMetadataWidth,
                                             maxMetadataWidth);
            actualDelta = startMetadataWidth - actualDelta;

            var metadataWidth = startMetadataWidth - actualDelta;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (actualDelta > 0)
            {
                var remainingShrink = actualDelta;
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
            }
            else if (actualDelta < 0)
            {
                var remainingGrowth = -actualDelta;
                GrowColumn(ref downloadsWidth, MaxBrowserDownloadsColumnWidth, ref remainingGrowth);
                GrowColumn(ref releasedWidth, MaxBrowserReleasedColumnWidth, ref remainingGrowth);
                GrowColumn(ref installedWidth, MaxBrowserInstalledColumnWidth, ref remainingGrowth);
            }

            SetBrowserColumnLayout(metadataWidth, downloadsWidth, releasedWidth, installedWidth, maxMetadataWidth);
        }

        public void ResizeBrowserDownloadsReleasedDivider(double startMetadataWidth,
                                                          double startDownloadsWidth,
                                                          double startReleasedWidth,
                                                          double startInstalledWidth,
                                                          double maxMetadataWidth,
                                                          double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserDownloadsColumnWidth - startDownloadsWidth,
                                                     availableGrowth));
                downloadsWidth = startDownloadsWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startDownloadsWidth - MinBrowserDownloadsColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth,
                                            0,
                                            Math.Min(MaxBrowserReleasedColumnWidth - startReleasedWidth,
                                                     availableGrowth));
                releasedWidth = startReleasedWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth, downloadsWidth, releasedWidth, installedWidth, maxMetadataWidth);
        }

        public void ResizeBrowserReleasedInstalledDivider(double startMetadataWidth,
                                                          double startDownloadsWidth,
                                                          double startReleasedWidth,
                                                          double startInstalledWidth,
                                                          double maxMetadataWidth,
                                                          double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserReleasedColumnWidth - startReleasedWidth,
                                                     availableGrowth));
                releasedWidth = startReleasedWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth,
                                            0,
                                            Math.Min(MaxBrowserInstalledColumnWidth - startInstalledWidth,
                                                     availableGrowth));
                installedWidth = startInstalledWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth,
                                   startDownloadsWidth,
                                   releasedWidth,
                                   installedWidth,
                                   maxMetadataWidth);
        }

        public void ResizeBrowserInstalledVersionDivider(double startMetadataWidth,
                                                         double startDownloadsWidth,
                                                         double startReleasedWidth,
                                                         double startInstalledWidth,
                                                         double maxMetadataWidth,
                                                         double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserInstalledColumnWidth - startInstalledWidth,
                                                     availableGrowth));
                installedWidth = startInstalledWidth + growth;
                var remainingShrink = growth;
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (startDownloadsWidth - MinBrowserDownloadsColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth, 0, availableGrowth);
                var remainingShrink = growth;
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth,
                                   downloadsWidth,
                                   releasedWidth,
                                   installedWidth,
                                   maxMetadataWidth);
        }

        public void ResizeBrowserMetadataColumn(double requestedWidth)
        {
            var width = ClampBrowserMetadataColumnWidth(requestedWidth);
            var downloadsWidth = browserDownloadsColumnWidth;
            var releasedWidth = browserReleasedColumnWidth;
            var installedWidth = browserInstalledColumnWidth;
            FitBrowserFixedColumns(width, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            if (Math.Abs(browserMetadataColumnWidth - width) < 0.1
                && Math.Abs(browserDownloadsColumnWidth - downloadsWidth) < 0.1
                && Math.Abs(browserReleasedColumnWidth - releasedWidth) < 0.1
                && Math.Abs(browserInstalledColumnWidth - installedWidth) < 0.1)
            {
                return;
            }

            browserMetadataColumnWidth = width;
            browserDownloadsColumnWidth = downloadsWidth;
            browserReleasedColumnWidth = releasedWidth;
            browserInstalledColumnWidth = installedWidth;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserDownloadsColumn(double requestedWidth)
        {
            var width = ClampBrowserDownloadsColumnWidth(requestedWidth);
            if (Math.Abs(browserDownloadsColumnWidth - width) < 0.1)
            {
                return;
            }

            browserDownloadsColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserReleasedColumn(double requestedWidth)
        {
            var width = ClampBrowserReleasedColumnWidth(requestedWidth);
            if (Math.Abs(browserReleasedColumnWidth - width) < 0.1)
            {
                return;
            }

            browserReleasedColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserInstalledColumn(double requestedWidth)
        {
            var width = ClampBrowserInstalledColumnWidth(requestedWidth);
            if (Math.Abs(browserInstalledColumnWidth - width) < 0.1)
            {
                return;
            }

            browserInstalledColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void CommitBrowserColumnLayout()
            => appSettingsService.SaveModBrowserColumnLayout(CurrentBrowserColumnLayout());

        private void ApplyBrowserColumnLayout(ModBrowserColumnLayout? layout)
        {
            var normalized = NormalizeBrowserColumnLayout(layout);
            browserMetadataColumnWidth  = normalized.MetadataColumnWidth;
            browserDownloadsColumnWidth = normalized.DownloadsColumnWidth;
            browserReleasedColumnWidth  = normalized.ReleasedColumnWidth;
            browserInstalledColumnWidth = normalized.InstalledColumnWidth;
        }

        private ModBrowserColumnLayout CurrentBrowserColumnLayout()
            => new ModBrowserColumnLayout
            {
                MetadataColumnWidth  = Math.Round(browserMetadataColumnWidth),
                DownloadsColumnWidth = Math.Round(browserDownloadsColumnWidth),
                ReleasedColumnWidth  = Math.Round(browserReleasedColumnWidth),
                InstalledColumnWidth = Math.Round(browserInstalledColumnWidth),
            };

        private static ModBrowserColumnLayout NormalizeBrowserColumnLayout(ModBrowserColumnLayout? layout)
        {
            var safeLayout = layout ?? new ModBrowserColumnLayout();
            var metadataWidth = ClampDimension(safeLayout.MetadataColumnWidth,
                                               MinBrowserMetadataColumnWidth,
                                               MaxBrowserMetadataColumnWidth);
            var downloadsWidth = ClampDimension(safeLayout.DownloadsColumnWidth,
                                                MinBrowserDownloadsColumnWidth,
                                                MaxBrowserDownloadsColumnWidth);
            var releasedWidth = ClampDimension(safeLayout.ReleasedColumnWidth,
                                               MinBrowserReleasedColumnWidth,
                                               MaxBrowserReleasedColumnWidth);
            var installedWidth = ClampDimension(safeLayout.InstalledColumnWidth,
                                                MinBrowserInstalledColumnWidth,
                                                MaxBrowserInstalledColumnWidth);
            FitBrowserFixedColumns(metadataWidth, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            return new ModBrowserColumnLayout
            {
                MetadataColumnWidth  = metadataWidth,
                DownloadsColumnWidth = downloadsWidth,
                ReleasedColumnWidth  = releasedWidth,
                InstalledColumnWidth = installedWidth,
            };
        }

        private double ClampBrowserMetadataColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserMetadataColumnWidth,
                              MaxBrowserMetadataColumnWidth);

        private double ClampBrowserDownloadsColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserDownloadsColumnWidth,
                              Math.Min(MaxBrowserDownloadsColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserReleasedColumnWidth
                                       - browserInstalledColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private double ClampBrowserReleasedColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserReleasedColumnWidth,
                              Math.Min(MaxBrowserReleasedColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserDownloadsColumnWidth
                                       - browserInstalledColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private double ClampBrowserInstalledColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserInstalledColumnWidth,
                              Math.Min(MaxBrowserInstalledColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserDownloadsColumnWidth
                                       - browserReleasedColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private static double ClampDimension(double value,
                                             double minimum,
                                             double maximum)
        {
            var safeValue = double.IsFinite(value) ? value : minimum;
            var safeMaximum = Math.Max(minimum, maximum);
            return Math.Clamp(safeValue, minimum, safeMaximum);
        }

        private static void FitBrowserFixedColumns(double metadataWidth,
                                                   ref double downloadsWidth,
                                                   ref double releasedWidth,
                                                   ref double installedWidth)
        {
            var availableWidth = Math.Max(MinBrowserDownloadsColumnWidth
                                          + MinBrowserReleasedColumnWidth
                                          + MinBrowserInstalledColumnWidth,
                                          metadataWidth - MinBrowserVersionColumnWidth);
            var combinedWidth = downloadsWidth + releasedWidth + installedWidth;
            if (combinedWidth <= availableWidth)
            {
                return;
            }

            var downloadsFlex = Math.Max(0, downloadsWidth - MinBrowserDownloadsColumnWidth);
            var releasedFlex = Math.Max(0, releasedWidth - MinBrowserReleasedColumnWidth);
            var installedFlex = Math.Max(0, installedWidth - MinBrowserInstalledColumnWidth);
            var totalFlex = downloadsFlex + releasedFlex + installedFlex;
            var excessWidth = combinedWidth - availableWidth;

            if (totalFlex <= 0)
            {
                downloadsWidth = MinBrowserDownloadsColumnWidth;
                releasedWidth = MinBrowserReleasedColumnWidth;
                installedWidth = MinBrowserInstalledColumnWidth;
                return;
            }

            downloadsWidth = Math.Max(MinBrowserDownloadsColumnWidth,
                                      downloadsWidth - (excessWidth * downloadsFlex / totalFlex));
            releasedWidth = Math.Max(MinBrowserReleasedColumnWidth,
                                     releasedWidth - (excessWidth * releasedFlex / totalFlex));
            installedWidth = Math.Max(MinBrowserInstalledColumnWidth,
                                      installedWidth - (excessWidth * installedFlex / totalFlex));
        }

        private static double MinimumBrowserMetadataColumnWidth()
            => Math.Max(MinBrowserMetadataColumnWidth,
                        MinBrowserDownloadsColumnWidth
                        + MinBrowserReleasedColumnWidth
                        + MinBrowserInstalledColumnWidth
                        + MinBrowserVersionColumnWidth);

        private static double BrowserVersionColumnWidth(double metadataWidth,
                                                        double downloadsWidth,
                                                        double releasedWidth,
                                                        double installedWidth)
            => Math.Max(MinBrowserVersionColumnWidth,
                        metadataWidth - downloadsWidth - releasedWidth - installedWidth);

        private static double MaxBrowserMetadataColumnWidthForTable(double tableWidth)
        {
            var minimum = MinimumBrowserMetadataColumnWidth();
            if (!double.IsFinite(tableWidth) || tableWidth <= 0)
            {
                return MaxBrowserMetadataColumnWidth;
            }

            return ClampDimension(tableWidth - MinBrowserNameColumnWidth,
                                  minimum,
                                  MaxBrowserMetadataColumnWidth);
        }

        private static double NormalizeBrowserMaxMetadataWidth(double maxMetadataWidth)
            => ClampDimension(maxMetadataWidth,
                              MinimumBrowserMetadataColumnWidth(),
                              MaxBrowserMetadataColumnWidth);

        private static void ShrinkColumn(ref double width,
                                         double     minimum,
                                         ref double remainingShrink)
        {
            if (remainingShrink <= 0)
            {
                return;
            }

            var shrink = Math.Min(remainingShrink, Math.Max(0, width - minimum));
            width -= shrink;
            remainingShrink -= shrink;
        }

        private static void GrowColumn(ref double width,
                                       double     maximum,
                                       ref double remainingGrowth)
        {
            if (remainingGrowth <= 0)
            {
                return;
            }

            var growth = Math.Min(remainingGrowth, Math.Max(0, maximum - width));
            width += growth;
            remainingGrowth -= growth;
        }

        private void SetBrowserColumnLayout(double metadataWidth,
                                            double downloadsWidth,
                                            double releasedWidth,
                                            double installedWidth,
                                            double maxMetadataWidth = MaxBrowserMetadataColumnWidth)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            metadataWidth = ClampDimension(metadataWidth,
                                           MinimumBrowserMetadataColumnWidth(),
                                           maxMetadataWidth);
            downloadsWidth = ClampDimension(downloadsWidth,
                                            MinBrowserDownloadsColumnWidth,
                                            MaxBrowserDownloadsColumnWidth);
            releasedWidth = ClampDimension(releasedWidth,
                                           MinBrowserReleasedColumnWidth,
                                           MaxBrowserReleasedColumnWidth);
            installedWidth = ClampDimension(installedWidth,
                                            MinBrowserInstalledColumnWidth,
                                            MaxBrowserInstalledColumnWidth);
            FitBrowserFixedColumns(metadataWidth, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            if (Math.Abs(browserMetadataColumnWidth - metadataWidth) < 0.1
                && Math.Abs(browserDownloadsColumnWidth - downloadsWidth) < 0.1
                && Math.Abs(browserReleasedColumnWidth - releasedWidth) < 0.1
                && Math.Abs(browserInstalledColumnWidth - installedWidth) < 0.1)
            {
                return;
            }

            browserMetadataColumnWidth = metadataWidth;
            browserDownloadsColumnWidth = downloadsWidth;
            browserReleasedColumnWidth = releasedWidth;
            browserInstalledColumnWidth = installedWidth;
            RaiseBrowserColumnLayoutChanged();
        }

        private void RaiseBrowserColumnLayoutChanged()
        {
            this.RaisePropertyChanged(nameof(BrowserMetadataColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserDownloadsColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserReleasedColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserInstalledColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserMetadataColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserDownloadsColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserReleasedColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserInstalledColumnGridLength));
        }

        public ObservableCollection<string> ApplyResultSummaryLines { get; }

        public ObservableCollection<string> ApplyResultFollowUpLines { get; }

        public IReadOnlyList<SortOptionItem> SortOptions { get; }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public ReactiveCommand<Unit, Unit> SetCurrentInstanceCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueInstallCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueUpdateCommand { get; }

        public ReactiveCommand<Unit, Unit> QueueRemoveCommand { get; }

        public ReactiveCommand<Unit, Unit> RemoveQueuedActionCommand { get; }

        public ReactiveCommand<Unit, Unit> RemoveSelectedPreviewConflictCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearQueueCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleAdvancedFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleAdvancedFilterEditorCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleTagFilterPickerCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleLabelFilterPickerCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSortOptionsCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearAdvancedFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearPopupFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearTagFilterCommand { get; }

        public ReactiveCommand<FilterTagOptionItem?, Unit> SelectTagFilterCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearLabelFilterCommand { get; }

        public ReactiveCommand<FilterTagOptionItem?, Unit> SelectLabelFilterCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleDisplaySettingsCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleDetailsPaneCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowBrowseSurfaceCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowPreviewSurfaceCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissPreviewConflictPopupCommand { get; }

        public ReactiveCommand<Unit, Unit> ResetUiScaleCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissUiScaleRestartStripCommand { get; }

        public ReactiveCommand<Unit, Unit> RestartToApplyUiScaleCommand { get; }

        public ReactiveCommand<Unit, Unit> ApplyChangesCommand { get; }

        public ReactiveCommand<Unit, Unit> DownloadQueuedCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleQueueDrawerCommand { get; }

        public ReactiveCommand<Unit, Unit> ApplyChangesFromCollapsedQueueCommand { get; }

        public ReactiveCommand<Unit, Unit> DismissApplyResultCommand { get; }

        public ReactiveCommand<Unit, Unit> AcknowledgeExecutionResultCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenCurrentGameDirectoryCommand { get; }

        public ReactiveCommand<Unit, Unit> PlayDirectCommand { get; }

        public ReactiveCommand<Unit, Unit> PlayViaSteamCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenSelectedModCacheLocationCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenUserGuideCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenGameSupportCommand { get; }

        public ReactiveCommand<Unit, Unit> ReportClientIssueCommand { get; }

        public ReactiveCommand<Unit, Unit> ReportMetadataIssueCommand { get; }

        public ReactiveCommand<Unit, Unit> PrimarySelectedModActionCommand { get; }

        public ReactiveCommand<Unit, Unit> InstallNowSelectedModCommand { get; }

        public ReactiveCommand<Unit, Unit> RemoveNowSelectedModCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSelectedModDependenciesCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSelectedModRecommendationsCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSelectedModSuggestionsCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewSelectedModDependenciesInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewSelectedModRecommendationsInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewSelectedModSuggestionsInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearRelationshipBrowserScopeCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowOverviewDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowMetadataDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowRelationshipsDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> ShowDescriptionDetailsCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectNameSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectAuthorSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectPopularitySortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectCompatibilitySortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectReleaseDateSortCommand { get; }

        public ReactiveCommand<Unit, Unit> SelectInstallDateSortCommand { get; }

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
                this.RaisePropertyChanged(nameof(CanSwitchInstances));
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
                this.RaisePropertyChanged(nameof(ExecutionDialogMessage));
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

        public string AdvancedNameFilter
        {
            get => advancedNameFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedNameFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedIdentifierFilter
        {
            get => advancedIdentifierFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedIdentifierFilter, value);
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

        public string AdvancedSummaryFilter
        {
            get => advancedSummaryFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedSummaryFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedDescriptionFilter
        {
            get => advancedDescriptionFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedDescriptionFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedLicenseFilter
        {
            get => advancedLicenseFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedLicenseFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedLanguageFilter
        {
            get => advancedLanguageFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedLanguageFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedDependsFilter
        {
            get => advancedDependsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedDependsFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedRecommendsFilter
        {
            get => advancedRecommendsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedRecommendsFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedSuggestsFilter
        {
            get => advancedSuggestsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedSuggestsFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedConflictsFilter
        {
            get => advancedConflictsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedConflictsFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedSupportsFilter
        {
            get => advancedSupportsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedSupportsFilter, value);
                PublishFilterStateLabels();
            }
        }

        public string AdvancedTagsFilter
        {
            get => advancedTagsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedTagsFilter, value);
                UpdateAvailableTagOptionSelection();
                PublishFilterStateLabels();
            }
        }

        public string AdvancedLabelsFilter
        {
            get => advancedLabelsFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref advancedLabelsFilter, value);
                UpdateAvailableLabelOptionSelection();
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

        public string SelectedModCacheState
        {
            get => selectedModCacheState;
            private set => this.RaiseAndSetIfChanged(ref selectedModCacheState, value);
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

        public ModVersionChoiceItem? SelectedModVersionChoice
        {
            get => selectedModVersionChoice;
            set
            {
                if (!EqualityComparer<ModVersionChoiceItem?>.Default.Equals(selectedModVersionChoice, value))
                {
                    this.RaiseAndSetIfChanged(ref selectedModVersionChoice, value);
                    ApplySelectedVersionDetails();
                    this.RaisePropertyChanged(nameof(ShowSelectedModVersionPicker));
                    this.RaisePropertyChanged(nameof(SelectedModSelectedVersionMatchesInstalled));
                    this.RaisePropertyChanged(nameof(SelectedModSelectedVersionIsCompatible));
                    PublishSelectedModActionState();
                }
            }
        }

        public bool ShowSelectedModVersionPicker => SelectedModAvailableVersions.Count > 0;

        public bool ShowSelectedModResourceLinks => SelectedModResourceLinks.Count > 0;

        public string SelectedModVersionPickerLabel
            => SelectedModAvailableVersions.Any(choice => choice.IsCompatible)
                ? "Available versions"
                : "Version history";

        public bool SelectedModSelectedVersionMatchesInstalled
            => SelectedModVersionChoice?.IsInstalledVersion == true;

        public bool SelectedModSelectedVersionIsCompatible
            => SelectedModVersionChoice?.IsCompatible ?? !SelectedModIsIncompatible;

        public bool ShowSelectedModDependenciesExpanded
        {
            get => selectedModDependenciesExpanded && SelectedModDependencies.Count > 0;
            private set => this.RaiseAndSetIfChanged(ref selectedModDependenciesExpanded, value);
        }

        public bool ShowSelectedModRecommendationsExpanded
        {
            get => selectedModRecommendationsExpanded && SelectedModRecommendations.Count > 0;
            private set => this.RaiseAndSetIfChanged(ref selectedModRecommendationsExpanded, value);
        }

        public bool ShowSelectedModSuggestionsExpanded
        {
            get => selectedModSuggestionsExpanded && SelectedModSuggestions.Count > 0;
            private set => this.RaiseAndSetIfChanged(ref selectedModSuggestionsExpanded, value);
        }

        public bool HasSelectedModDependencies => SelectedModDependencies.Count > 0;

        public bool HasSelectedModRecommendations => SelectedModRecommendations.Count > 0;

        public bool HasSelectedModSuggestions => SelectedModSuggestions.Count > 0;

        public bool ShowRelationshipBrowserScope => relationshipBrowserScopeIdentifiers.Count > 0;

        private bool ShowConflictBrowserScope
            => ShowRelationshipBrowserScope
               && relationshipBrowserScopeText.StartsWith("Conflict:", StringComparison.Ordinal);

        public string RelationshipBrowserScopeText
        {
            get => relationshipBrowserScopeText;
            private set => this.RaiseAndSetIfChanged(ref relationshipBrowserScopeText, value);
        }

        public string SelectedModDependencyChevron
            => HasSelectedModDependencies
                ? ShowSelectedModDependenciesExpanded ? "▾" : "▸"
                : "";

        public string SelectedModRecommendationChevron
            => HasSelectedModRecommendations
                ? ShowSelectedModRecommendationsExpanded ? "▾" : "▸"
                : "";

        public string SelectedModSuggestionChevron
            => HasSelectedModSuggestions
                ? ShowSelectedModSuggestionsExpanded ? "▾" : "▸"
                : "";

        public string SelectedModBody
        {
            get => selectedModBody;
            private set => this.RaiseAndSetIfChanged(ref selectedModBody, value);
        }

        public int ProgressPercent
        {
            get => progressPercent;
            private set
            {
                this.RaiseAndSetIfChanged(ref progressPercent, value);
                this.RaisePropertyChanged(nameof(HasExecutionProgressValue));
                this.RaisePropertyChanged(nameof(IsExecutionProgressIndeterminate));
                this.RaisePropertyChanged(nameof(ExecutionProgressValue));
            }
        }

        public int InstanceCount
        {
            get => instanceCount;
            private set
            {
                this.RaiseAndSetIfChanged(ref instanceCount, value);
                this.RaisePropertyChanged(nameof(CanSwitchInstances));
            }
        }

        public bool IsRefreshing
        {
            get => isRefreshing;
            private set
            {
                this.RaiseAndSetIfChanged(ref isRefreshing, value);
                this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(CanSwitchInstances));
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

        public Registry? CurrentRegistry => gameInstanceService.CurrentRegistry;

        public RegistryManager? CurrentRegistryManager => gameInstanceService.CurrentRegistryManager;

        public NetModuleCache? CurrentCache => gameInstanceService.Manager.Cache;

        public RegistryManager? AcquireWriteRegistryManager()
            => gameInstanceService.AcquireWriteRegistryManager();

        public Func<IReadOnlyList<RecommendationAuditItem>, Task<IReadOnlyList<RecommendationAuditItem>?>>?
            RecommendationSelectionPromptAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmIncompatibleLaunchAsync { get; set; }

        public bool CanPlayDirect => FindLaunchCommand(GameLaunchMode.Direct) != null;

        public bool CanPlayViaSteam => FindLaunchCommand(GameLaunchMode.Steam) != null;

        public bool PreselectRecommendedMods
        {
            get => appSettingsService.PreselectRecommendedMods;
            set
            {
                if (appSettingsService.PreselectRecommendedMods == value)
                {
                    return;
                }

                appSettingsService.SavePreselectRecommendedMods(value);
                this.RaisePropertyChanged();
            }
        }

        public void RefreshCurrentRegistryReference()
        {
            gameInstanceService.RefreshCurrentRegistry();
            this.RaisePropertyChanged(nameof(CurrentRegistry));
            this.RaisePropertyChanged(nameof(CurrentRegistryManager));
        }

        public void RefreshLaunchCommandState()
            => PublishLaunchCommandState();

        public IReadOnlyList<GameInstance> KnownGameInstances
            => gameInstanceService.Manager.Instances.Values.ToList();

        public void RefreshInstanceSummaries()
            => ReloadInstances(loadCatalog: false);

        public Task RefreshCurrentStateAsync()
            => RefreshAsync();

        public async Task InstallFromCkanFilesAsync(IEnumerable<string> paths)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Select an instance before installing from .ckan files.";
                return;
            }

            var queued = 0;
            var skipped = 0;
            ClearApplyResult();

            foreach (var path in paths)
            {
                try
                {
                    var module = CkanModule.FromFile(path);
                    if (module.IsDLC)
                    {
                        skipped++;
                        continue;
                    }

                    queued += QueueInstallCandidate(module) ? 1 : 0;
                    skipped += changesetService.FindQueuedApplyAction(module.identifier) == null ? 1 : 0;

                    if (module.IsMetapackage && module.depends != null)
                    {
                        foreach (var dependency in module.depends)
                        {
                            var match = dependency.LatestAvailableWithProvides(CurrentRegistry,
                                                                               CurrentInstance.StabilityToleranceConfig,
                                                                               CurrentInstance.VersionCriteria())
                                                  .FirstOrDefault();
                            if (match == null)
                            {
                                skipped++;
                                continue;
                            }

                            queued += QueueInstallCandidate(match) ? 1 : 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    Diagnostics = ex.Message;
                }
            }

            if (queued > 0)
            {
                ShowPreviewSurface = true;
                StatusMessage = skipped == 0
                    ? $"Queued {queued} item{(queued == 1 ? "" : "s")} from .ckan file{(queued == 1 ? "" : "s")}."
                    : $"Queued {queued} item{(queued == 1 ? "" : "s")} from .ckan files; skipped {skipped}.";
            }
            else
            {
                StatusMessage = "No installable modules from the selected .ckan files could be queued.";
            }

            await Task.CompletedTask;
        }

        public async Task ImportDownloadedModsAsync(IEnumerable<string> paths)
        {
            if (CurrentInstance == null || CurrentRegistry == null || CurrentCache == null)
            {
                StatusMessage = "Select an instance before importing downloaded mods.";
                return;
            }

            var files = paths.Select(path => new FileInfo(path))
                             .Where(file => file.Exists)
                             .ToHashSet();
            if (files.Count == 0)
            {
                StatusMessage = "No downloaded mod archives were selected.";
                return;
            }

            IsUserBusy = true;
            StatusMessage = "Importing downloaded mods into the cache...";
            var installable = new List<CkanModule>();
            try
            {
                var imported = await Task.Run(() =>
                    ModuleImporter.ImportFiles(files,
                                               user,
                                               mod => installable.Add(mod),
                                               CurrentRegistry,
                                               CurrentInstance,
                                               CurrentCache));

                if (imported)
                {
                    var queued = 0;
                    foreach (var module in installable)
                    {
                        queued += QueueInstallCandidate(module) ? 1 : 0;
                    }

                    if (queued > 0)
                    {
                        ShowPreviewSurface = true;
                        StatusMessage = $"Imported files and queued {queued} install{(queued == 1 ? "" : "s")}.";
                    }
                    else
                    {
                        StatusMessage = "Imported files into the cache.";
                    }

                    await RefreshCurrentStateAsync();
                }
                else
                {
                    StatusMessage = "No matching CKAN mods were found in the selected files.";
                }
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Import downloaded mods failed.";
            }
            finally
            {
                IsUserBusy = false;
            }
        }

        public async Task<string> DeduplicateInstalledFilesAsync()
        {
            IsUserBusy = true;
            StatusMessage = "Scanning for duplicate installed files...";
            var previousMessage = user.LastMessage;
            try
            {
                await Task.Run(() =>
                {
                    var deduper = new InstalledFilesDeduplicator(CurrentManager.Instances.Values,
                                                                 gameInstanceService.RepositoryData);
                    deduper.DeduplicateAll(user);
                });
                var result = string.IsNullOrWhiteSpace(user.LastMessage) || user.LastMessage == previousMessage
                    ? "Deduplicate installed files finished."
                    : user.LastMessage;
                StatusMessage = result;
                return result;
            }
            catch (CancelledActionKraken)
            {
                StatusMessage = "Deduplicate installed files canceled.";
                return StatusMessage;
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Deduplicate installed files failed.";
                return $"{StatusMessage}\n\n{ex.Message}";
            }
            finally
            {
                IsUserBusy = false;
            }
        }

        public async Task ExportInstalledModListAsync(string         path,
                                                      ExportFileType exportFileType)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Select an instance before exporting the installed mod list.";
                return;
            }

            try
            {
                using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                    ? gameInstanceService.AcquireWriteRegistryManager()
                    : null;
                var manager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
                if (manager == null)
                {
                    StatusMessage = "Could not open the current registry for export.";
                    return;
                }

                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                new Exporter(exportFileType).Export(manager, manager.registry, stream);
                StatusMessage = $"Saved installed mod list to {path}.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Save installed mod list failed.";
            }
        }

        public async Task ExportModpackAsync(string path)
        {
            if (CurrentInstance == null)
            {
                StatusMessage = "Select an instance before exporting a modpack.";
                return;
            }

            try
            {
                using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                    ? gameInstanceService.AcquireWriteRegistryManager()
                    : null;
                var manager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
                if (manager == null)
                {
                    StatusMessage = "Could not open the current registry for modpack export.";
                    return;
                }

                var json = manager.GenerateModpack(false, true).ToJson();
                await File.WriteAllTextAsync(path, json);
                StatusMessage = $"Exported re-importable modpack to {path}.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Export modpack failed.";
            }
        }

        public async Task<IReadOnlyList<RecommendationAuditItem>> AuditRecommendationsAsync()
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Recommendation audit is unavailable until an instance and registry are loaded.";
                return Array.Empty<RecommendationAuditItem>();
            }

            var instance = CurrentInstance;
            var registry = CurrentRegistry;
            var preselectRecommendations = PreselectRecommendedMods;
            StatusMessage = "Auditing recommendations for installed mods...";

            try
            {
                var items = await Task.Run(() =>
                {
                    var installedModules = registry.InstalledModules
                                                   .Select(installed => installed.Module)
                                                   .ToHashSet();

                    if (!ModuleInstaller.FindRecommendations(instance,
                                                             installedModules,
                                                             Array.Empty<CkanModule>(),
                                                             Array.Empty<CkanModule>(),
                                                             Array.Empty<CkanModule>(),
                                                             registry,
                                                             out var recommendations,
                                                             out var suggestions,
                                                             out var supporters))
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    return BuildRecommendationAuditItems(recommendations,
                                                         suggestions,
                                                         supporters,
                                                         preselectRecommendations,
                                                         module => gameInstanceService.RepositoryData.GetDownloadCount(
                                                             registry.Repositories.Values,
                                                             module.identifier));
                });

                StatusMessage = items.Count == 0
                    ? "No recommended, suggested, or supporting mods found."
                    : $"Found {items.Count} recommendation audit item{(items.Count == 1 ? "" : "s")}.";
                return items;
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Recommendation audit failed.";
                return Array.Empty<RecommendationAuditItem>();
            }
        }

        public void QueueRecommendationAuditSelections(IEnumerable<RecommendationAuditItem> selections)
        {
            var selected = selections.Where(item => item.CanQueue)
                                     .ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "No recommendation audit items were selected.";
                return;
            }

            ClearApplyResult();

            var queued = 0;
            var skipped = 0;
            foreach (var item in selected)
            {
                var catalogItem = allCatalogItems.FirstOrDefault(mod =>
                    string.Equals(mod.Identifier, item.Identifier, StringComparison.OrdinalIgnoreCase));
                if (catalogItem == null
                    || catalogItem.IsInstalled
                    || catalogItem.IsIncompatible)
                {
                    skipped++;
                    continue;
                }

                changesetService.QueueInstall(catalogItem, item.Version);
                queued++;
            }

            if (queued > 0)
            {
                ShowPreviewSurface = true;
                StatusMessage = skipped == 0
                    ? $"Queued {queued} recommendation audit item{(queued == 1 ? "" : "s")}."
                    : $"Queued {queued} recommendation audit item{(queued == 1 ? "" : "s")}; skipped {skipped} unavailable item{(skipped == 1 ? "" : "s")}.";
            }
            else
            {
                StatusMessage = "No selected recommendation audit items could be queued.";
            }
        }

        private async Task<bool> PromptForQueuedRecommendationsAsync()
        {
            if (RecommendationSelectionPromptAsync == null
                || CurrentInstance == null
                || CurrentRegistry == null
                || !HasQueuedChangeActions)
            {
                return true;
            }

            var shown = new List<CkanModule>();
            while (true)
            {
                var items = await BuildQueuedRecommendationAuditItemsAsync(shown);
                if (items.Count == 0)
                {
                    return true;
                }

                shown.AddRange(items.Select(item => item.Module));

                var selections = await RecommendationSelectionPromptAsync(items);
                if (selections == null)
                {
                    StatusMessage = "Apply canceled before optional recommendations were installed.";
                    return false;
                }

                var selected = selections.Where(item => item.CanQueue)
                                         .ToList();
                if (selected.Count == 0)
                {
                    StatusMessage = "Continuing without optional recommendations.";
                    return true;
                }

                QueueRecommendationAuditSelections(selected);
            }
        }

        private async Task<IReadOnlyList<RecommendationAuditItem>> BuildQueuedRecommendationAuditItemsAsync(
            IReadOnlyList<CkanModule> shown)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                return Array.Empty<RecommendationAuditItem>();
            }

            var instance = CurrentInstance;
            var registry = CurrentRegistry;
            var actions = changesetService.CurrentApplyQueue.ToList();
            var preselectRecommendations = PreselectRecommendedMods;
            if (actions.Count == 0)
            {
                return Array.Empty<RecommendationAuditItem>();
            }

            var queuedIdentifiers = actions.Select(action => action.Identifier)
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                return await Task.Run<IReadOnlyList<RecommendationAuditItem>>(() =>
                {
                    var requestedInstalls = new List<CkanModule>();
                    var requestedUpdates = new List<CkanModule>();
                    var requestedRemovals = new List<InstalledModule>();

                    foreach (var action in actions)
                    {
                        switch (action.ActionKind)
                        {
                            case QueuedActionKind.Install:
                                if (ResolveQueuedModule(registry, instance, action) is CkanModule installMod)
                                {
                                    requestedInstalls.Add(installMod);
                                }
                                break;
                            case QueuedActionKind.Update:
                                if (ResolveQueuedModule(registry, instance, action) is CkanModule updateMod)
                                {
                                    requestedUpdates.Add(updateMod);
                                }
                                break;
                            case QueuedActionKind.Remove:
                                if (registry.InstalledModule(action.Identifier) is InstalledModule removeMod)
                                {
                                    requestedRemovals.Add(removeMod);
                                }
                                break;
                        }
                    }

                    var combinedInstalls = DistinctRecommendationModules(requestedInstalls.Concat(requestedUpdates));
                    if (combinedInstalls.Count == 0)
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    var allRemoving = DistinctRecommendationModules(requestedRemovals.Select(module => module.Module));
                    var resolver = new RelationshipResolver(combinedInstalls,
                                                            allRemoving,
                                                            RelationshipResolverOptions.ConflictsOpts(instance.StabilityToleranceConfig),
                                                            registry,
                                                            instance.Game,
                                                            instance.VersionCriteria());
                    var resolvedInstalls = DistinctRecommendationModules(resolver.ModList(false));

                    if (!ModuleInstaller.FindRecommendations(instance,
                                                             resolvedInstalls,
                                                             resolvedInstalls,
                                                             allRemoving,
                                                             shown,
                                                             registry,
                                                             out var recommendations,
                                                             out var suggestions,
                                                             out var supporters))
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    return BuildRecommendationAuditItems(recommendations,
                                                         suggestions,
                                                         supporters,
                                                         preselectRecommendations,
                                                         module => gameInstanceService.RepositoryData.GetDownloadCount(
                                                             registry.Repositories.Values,
                                                             module.identifier))
                           .Where(item => !queuedIdentifiers.Contains(item.Identifier))
                           .ToList();
                });
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Recommendation check failed; continuing with queued changes only.";
                return Array.Empty<RecommendationAuditItem>();
            }
        }

        public bool ShowStartupInstancePanel => !IsReady;

        public bool ShowReadyInstancePanel => IsReady;

        public bool ShowReadyStatusSurface
            => IsReady
               && !ShowExecutionOverlay
               && !IsCatalogLoading
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
                this.RaisePropertyChanged(nameof(SurfaceViewToggleCompact));
                this.RaisePropertyChanged(nameof(BrowseSurfaceButtonBackground));
                this.RaisePropertyChanged(nameof(BrowseSurfaceButtonBorderBrush));
                this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBackground));
                this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBorderBrush));
                RefreshPreviewConflictPopupState();
            }
        }

        public bool ShowBrowseSurface => !ShowPreviewSurface;

        public bool SurfaceViewTogglePinned
        {
            get => surfaceViewTogglePinned;
            private set
            {
                this.RaiseAndSetIfChanged(ref surfaceViewTogglePinned, value);
                this.RaisePropertyChanged(nameof(SurfaceViewToggleCompact));
            }
        }

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
               && !IsApplyingChanges
               && !IsCatalogLoading;

        public bool CanSwitchInstances
            => IsReady
               && InstanceCount > 1
               && !IsRefreshing
               && !IsApplyingChanges
               && !IsCatalogLoading;

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
                if (value)
                {
                    ShowTagFilterPicker = false;
                    ShowLabelFilterPicker = false;
                    ShowSortOptions = false;
                }

                this.RaisePropertyChanged(nameof(CanInteractWithCatalog));
                this.RaisePropertyChanged(nameof(CanSwitchInstances));
                this.RaisePropertyChanged(nameof(CanUseCatalogPanel));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(ModCountLabel));
                this.RaisePropertyChanged(nameof(ShowCatalogSkeleton));
                this.RaisePropertyChanged(nameof(ShowModList));
                this.RaisePropertyChanged(nameof(ShowEmptyModResults));
            }
        }

        public bool CanInteractWithCatalog => !IsCatalogLoading;

        public bool CanUseCatalogPanel => !IsCatalogLoading || ShowAdvancedFilters;

        public bool HasMods => Mods.Count > 0;

        public bool ShowCatalogSkeleton => IsCatalogLoading;

        private static IReadOnlyList<CatalogSkeletonRow> BuildCatalogSkeletonRows(IReadOnlyList<CatalogSkeletonSnapshotRow>? savedRows)
            => savedRows is { Count: > 0 }
                ? savedRows.Select(ToCatalogSkeletonRow).ToList()
                : BuildDefaultCatalogSkeletonRows();

        private static IReadOnlyList<CatalogSkeletonRow> BuildCatalogSkeletonRows(IEnumerable<ModListItem> items)
        {
            var opacityCycle = new[] { 1.00, 0.98, 0.96, 0.94, 0.97, 0.95, 0.93, 0.91 };
            return items.Take(26)
                        .Select((item, index) => new CatalogSkeletonRow
                        {
                            AccentBrush             = string.IsNullOrWhiteSpace(item.PrimaryStateColor) ? "#39424E" : item.PrimaryStateColor,
                            TitleWidth              = SkeletonTextWidth(item.Name, 120, 320, 6.4),
                            AuthorWidth             = SkeletonTextWidth(item.Author, 54, 180, 4.8),
                            SummaryWidth            = SkeletonTextWidth(item.Summary, 180, 420, 5.4),
                            DownloadsWidth          = SkeletonTextWidth(item.DownloadCountLabel, 42, 72, 5.8),
                            CompatibilityWidth      = SkeletonTextWidth(item.Compatibility, 24, 40, 4.2),
                            ReleaseWidth            = SkeletonTextWidth(item.ReleaseDate, 36, 68, 4.8),
                            VersionPrimaryWidth     = SkeletonTextWidth(item.LatestVersion, 36, 132, 5.6),
                            VersionSecondaryWidth   = item.IsInstalled
                                ? SkeletonTextWidth(item.InstalledVersion, 28, 118, 5.2)
                                : 0,
                            Opacity                 = opacityCycle[index % opacityCycle.Length],
                            PrimaryBadgeWidth       = PillWidth(item.PrimaryStateLabel),
                            PrimaryBadgeBackground  = string.IsNullOrWhiteSpace(item.PrimaryStateColor) ? "#3B4653" : item.PrimaryStateColor,
                            HasCachedBadge          = item.IsCached,
                            SecondaryBadgeWidth     = item.HasSecondaryState ? PillWidth(item.SecondaryStateLabel) : 0,
                            SecondaryBadgeBackground = item.SecondaryStateBackground,
                            SecondaryBadgeBorderBrush = item.SecondaryStateBorderBrush,
                            TertiaryBadgeWidth      = item.HasTertiaryState ? PillWidth(item.TertiaryStateLabel) : 0,
                            TertiaryBadgeBackground = item.TertiaryStateBackground,
                            TertiaryBadgeBorderBrush = item.TertiaryStateBorderBrush,
                            QueueBadgeWidth         = item.HasQueueState ? PillWidth(item.QueueStateLabel) : 0,
                            QueueBadgeBackground    = item.QueueStateBackground,
                            QueueBadgeBorderBrush   = item.QueueStateBorderBrush,
                        })
                        .ToList();
        }

        private static IReadOnlyList<CatalogSkeletonRow> BuildDefaultCatalogSkeletonRows()
        {
            string[] accents =
            {
                "#24588A",
                "#39424E",
                "#2E7C59",
                "#9B4559",
            };

            (double TitleWidth,
             double AuthorWidth,
             double SummaryWidth,
             double DownloadsWidth,
             double CompatibilityWidth,
             double ReleaseWidth,
             double VersionPrimaryWidth,
             double VersionSecondaryWidth,
             double PrimaryBadgeWidth,
             bool   HasCachedBadge,
             double SecondaryBadgeWidth,
             double TertiaryBadgeWidth,
             double QueueBadgeWidth,
             double Opacity)[] patterns =
            {
                (182, 92, 286, 66, 42, 76, 98, 74, 54, true,  0,  0,  0, 1.00),
                (208, 108, 254, 60, 36, 68, 90, 66, 48, false, 0,  0,  0, 0.98),
                (196, 96, 308, 64, 40, 72, 94, 70, 58, false, 0,  0,  0, 0.96),
                (176, 88, 244, 58, 34, 64, 86, 62, 74, false, 0,  0,  0, 0.94),
                (214, 112, 322, 68, 44, 80, 102, 76, 54, true,  0,  0, 66, 0.97),
                (188, 94, 266, 62, 38, 70, 92, 68, 58, false, 54, 0,  0, 0.95),
                (202, 104, 296, 66, 40, 74, 96, 72, 48, false, 0,  60, 0, 0.93),
                (172, 84, 236, 56, 32, 62, 84, 60, 74, false, 0,  0,  0, 0.91),
            };

            return Enumerable.Range(0, 26)
                             .Select(index =>
                             {
                                 var pattern = patterns[index % patterns.Length];
                                 var accent = accents[index % accents.Length];
                                 return new CatalogSkeletonRow
                                 {
                                     AccentBrush              = accent,
                                     TitleWidth               = pattern.TitleWidth,
                                     AuthorWidth              = pattern.AuthorWidth,
                                     SummaryWidth             = pattern.SummaryWidth,
                                     DownloadsWidth           = pattern.DownloadsWidth,
                                     CompatibilityWidth       = pattern.CompatibilityWidth,
                                     ReleaseWidth             = pattern.ReleaseWidth,
                                     VersionPrimaryWidth      = pattern.VersionPrimaryWidth,
                                     VersionSecondaryWidth    = pattern.VersionSecondaryWidth,
                                     PrimaryBadgeWidth        = pattern.PrimaryBadgeWidth,
                                     PrimaryBadgeBackground   = accent,
                                     HasCachedBadge           = pattern.HasCachedBadge,
                                     SecondaryBadgeWidth      = pattern.SecondaryBadgeWidth,
                                     SecondaryBadgeBackground = "#39424E",
                                     SecondaryBadgeBorderBrush = "#607286",
                                     TertiaryBadgeWidth       = pattern.TertiaryBadgeWidth,
                                     TertiaryBadgeBackground  = "#31424F",
                                     TertiaryBadgeBorderBrush = "#4C6A86",
                                     QueueBadgeWidth          = pattern.QueueBadgeWidth,
                                     QueueBadgeBackground     = "#4B5A69",
                                     QueueBadgeBorderBrush    = "#4B5A69",
                                     Opacity                  = pattern.Opacity,
                                 };
                             })
                             .ToList();
        }

        private static CatalogSkeletonRow ToCatalogSkeletonRow(CatalogSkeletonSnapshotRow row)
            => new CatalogSkeletonRow
            {
                AccentBrush              = row.AccentBrush,
                TitleWidth               = row.TitleWidth,
                AuthorWidth              = row.AuthorWidth,
                SummaryWidth             = row.SummaryWidth,
                DownloadsWidth           = row.DownloadsWidth,
                CompatibilityWidth       = row.CompatibilityWidth,
                ReleaseWidth             = row.ReleaseWidth,
                VersionPrimaryWidth      = row.VersionPrimaryWidth,
                VersionSecondaryWidth    = row.VersionSecondaryWidth,
                Opacity                  = row.Opacity,
                PrimaryBadgeWidth        = row.PrimaryBadgeWidth,
                PrimaryBadgeBackground   = row.PrimaryBadgeBackground,
                HasCachedBadge           = row.HasCachedBadge,
                SecondaryBadgeWidth      = row.SecondaryBadgeWidth,
                SecondaryBadgeBackground = row.SecondaryBadgeBackground,
                SecondaryBadgeBorderBrush = row.SecondaryBadgeBorderBrush,
                TertiaryBadgeWidth       = row.TertiaryBadgeWidth,
                TertiaryBadgeBackground  = row.TertiaryBadgeBackground,
                TertiaryBadgeBorderBrush = row.TertiaryBadgeBorderBrush,
                QueueBadgeWidth          = row.QueueBadgeWidth,
                QueueBadgeBackground     = row.QueueBadgeBackground,
                QueueBadgeBorderBrush    = row.QueueBadgeBorderBrush,
            };

        private static CatalogSkeletonSnapshotRow ToCatalogSkeletonSnapshotRow(CatalogSkeletonRow row)
            => new CatalogSkeletonSnapshotRow
            {
                AccentBrush              = row.AccentBrush,
                TitleWidth               = row.TitleWidth,
                AuthorWidth              = row.AuthorWidth,
                SummaryWidth             = row.SummaryWidth,
                DownloadsWidth           = row.DownloadsWidth,
                CompatibilityWidth       = row.CompatibilityWidth,
                ReleaseWidth             = row.ReleaseWidth,
                VersionPrimaryWidth      = row.VersionPrimaryWidth,
                VersionSecondaryWidth    = row.VersionSecondaryWidth,
                Opacity                  = row.Opacity,
                PrimaryBadgeWidth        = row.PrimaryBadgeWidth,
                PrimaryBadgeBackground   = row.PrimaryBadgeBackground,
                HasCachedBadge           = row.HasCachedBadge,
                SecondaryBadgeWidth      = row.SecondaryBadgeWidth,
                SecondaryBadgeBackground = row.SecondaryBadgeBackground,
                SecondaryBadgeBorderBrush = row.SecondaryBadgeBorderBrush,
                TertiaryBadgeWidth       = row.TertiaryBadgeWidth,
                TertiaryBadgeBackground  = row.TertiaryBadgeBackground,
                TertiaryBadgeBorderBrush = row.TertiaryBadgeBorderBrush,
                QueueBadgeWidth          = row.QueueBadgeWidth,
                QueueBadgeBackground     = row.QueueBadgeBackground,
                QueueBadgeBorderBrush    = row.QueueBadgeBorderBrush,
            };

        private static double SkeletonTextWidth(string? text,
                                                double  min,
                                                double  max,
                                                double  perCharacter)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var width = min + (text.Trim().Length * perCharacter);
            return Math.Max(min, Math.Min(max, width));
        }

        private static double PillWidth(string? text)
            => string.IsNullOrWhiteSpace(text)
                ? 0
                : Math.Max(40, Math.Min(112, 18 + (text.Trim().Length * 5.4)));

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

        public bool ShowEmptyQueueStub => !HasQueuedActions && !ShowInlineApplyResult;

        public bool ShowCollapsedQueuedActionsStub
            => !IsQueueDrawerExpanded && HasQueuedActions;

        public bool ShowCollapsedApplyResultStub
            => !IsQueueDrawerExpanded && !HasQueuedActions && ShowInlineApplyResult;

        public bool ShowExpandedQueuePanel
            => IsQueueDrawerExpanded && (HasQueuedActions || ShowInlineApplyResult);

        public bool HasPreviewDownloadsRequired => PreviewDownloadsRequired.Count > 0;

        public bool HasPreviewDependencies => PreviewDependencies.Count > 0;

        public bool HasPreviewAutoRemovals => PreviewAutoRemovals.Count > 0;

        public bool HasPreviewAttentionNotes => PreviewAttentionNotes.Count > 0;

        public bool HasPreviewRecommendations => PreviewRecommendations.Count > 0;

        public bool HasPreviewSuggestions => PreviewSuggestions.Count > 0;

        public bool HasPreviewConflicts => PreviewConflicts.Count > 0;

        public bool ShowPreviewConflictPopup
            => ShowPreviewSurface
               && HasPreviewConflicts
               && !IsPreviewLoading
               && !previewConflictPopupDismissed
               && !ShowExecutionOverlay;

        public string PreviewConflictPopupTitle
            => CountLabel(PreviewConflictChoices.Count > 0
                               ? PreviewConflictChoices.Count
                               : PreviewConflicts.Count,
                          "Conflict Found",
                          "Conflicts Found");

        public bool PreviewShowsEmptyCard => !HasQueuedActions;

        public bool PreviewShowsLoadingCard => HasQueuedChangeActions && IsPreviewLoading;

        public bool PreviewShowsReadyCard
            => HasQueuedActions
               && !IsPreviewLoading
               && ((HasQueuedChangeActions && PreviewCanApply)
                   || (!HasQueuedChangeActions && HasQueuedDownloadActions));

        public bool PreviewShowsBlockedCard
            => HasQueuedChangeActions && !IsPreviewLoading && !PreviewCanApply;

        public bool ShowPreviewEmptyWorkspace
            => !ShowExecutionOverlay && !HasQueuedActions && !ShowInlineApplyResult;

        public bool ShowPreviewActiveWorkspace
            => !ShowExecutionOverlay && (HasQueuedActions || ShowInlineApplyResult);

        public bool ShowAdvancedFilters
        {
            get => showAdvancedFilters;
            set
            {
                this.RaiseAndSetIfChanged(ref showAdvancedFilters, value);
                if (!value)
                {
                    ShowAdvancedFilterEditor = false;
                    ShowTagFilterPicker = false;
                    ShowLabelFilterPicker = false;
                }
                modSearchService.SetShowAdvancedFilters(value);
                this.RaisePropertyChanged(nameof(CanUseCatalogPanel));
            }
        }

        public bool ShowAdvancedFilterEditor
        {
            get => showAdvancedFilterEditor;
            set
            {
                this.RaiseAndSetIfChanged(ref showAdvancedFilterEditor, value);
                if (!value)
                {
                    ShowTagFilterPicker = false;
                    ShowLabelFilterPicker = false;
                }
                this.RaisePropertyChanged(nameof(ShowSimpleFilterMenu));
                this.RaisePropertyChanged(nameof(FiltersPopupTitle));
                this.RaisePropertyChanged(nameof(AdvancedFilterToggleLabel));
                this.RaisePropertyChanged(nameof(FiltersPopupWidth));
            }
        }

        public bool ShowSimpleFilterMenu => !ShowAdvancedFilterEditor;

        public bool ShowTagFilterPicker
        {
            get => showTagFilterPicker;
            set => this.RaiseAndSetIfChanged(ref showTagFilterPicker, value);
        }

        public bool ShowLabelFilterPicker
        {
            get => showLabelFilterPicker;
            set => this.RaiseAndSetIfChanged(ref showLabelFilterPicker, value);
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

        public bool SurfaceViewToggleCompact
            => ShowBrowseSurface && !HasQueuedActions && !HasApplyResult && !SurfaceViewTogglePinned;

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
            => HasAdvancedFilterText
               || FilterInstalledState.HasValue
               || FilterUpdatableState.HasValue
               || FilterCompatibleState.HasValue
               || FilterCachedState.HasValue
               || FilterReplaceableState.HasValue;

        public bool HasAdvancedFilterText
            => EnumerateAdvancedTextFilters().Any(filter => !string.IsNullOrWhiteSpace(filter.Value));

        public bool HasAvailableTagOptions => AvailableTagOptions.Count > 0;

        public int SelectedCategoryCount => SelectedFilterValues(AdvancedTagsFilter).Count;

        public bool HasSelectedTagFilter => SelectedCategoryCount > 0;

        public string TagFilterPickerSummary
            => HasAvailableTagOptions
                ? $"{AvailableTagOptions.Count} categor{(AvailableTagOptions.Count == 1 ? "y" : "ies")} in the loaded catalog"
                : "No categories were found in the loaded catalog.";

        public bool HasAvailableLabelOptions => AvailableLabelOptions.Count > 0;

        public bool HasSelectedLabelFilter => !string.IsNullOrWhiteSpace(AdvancedLabelsFilter);

        public string LabelFilterPickerSummary
            => HasAvailableLabelOptions
                ? $"{AvailableLabelOptions.Count} label{(AvailableLabelOptions.Count == 1 ? "" : "s")} in the loaded catalog"
                : "No labels were found in the loaded catalog.";

        public double ClearFiltersButtonOpacity => HasActiveFilters ? 1.0 : 0.0;

        public double ClearAdvancedTextButtonOpacity => HasAdvancedFilterText ? 1.0 : 0.0;

        public int ActiveFilterCount
            => EnumerateAdvancedTextFilters().Count(filter => filter.Label != "Category"
                                                              && !string.IsNullOrWhiteSpace(filter.Value))
               + SelectedCategoryCount
               + CountTriStateFilter(FilterInstalledState)
               + CountTriStateFilter(FilterUpdatableState)
               + CountTriStateFilter(FilterCompatibleState)
               + CountTriStateFilter(FilterCachedState)
               + CountTriStateFilter(FilterReplaceableState);

        public string MoreFiltersLabel
            => ActiveFilterCount > 0
                ? $"Active Filters ({ActiveFilterCount}) ▾"
                : "Filters ▾";

        public string MoreFiltersButtonBackground
            => HasActiveFilters ? "#5C376D" : "#3E648A";

        public string MoreFiltersButtonBorderBrush => MoreFiltersButtonBackground;

        public string FiltersPopupTitle => ShowAdvancedFilterEditor ? "Advanced Filters" : "Filters";

        public string AdvancedFilterToggleLabel => ShowAdvancedFilterEditor ? "Simple Filters" : "Advanced Filters";

        public double FiltersPopupWidth => 404;

        public string ClearFiltersButtonBackground => "#6B2B2B";

        public string ClearFiltersButtonBorderBrush => ClearFiltersButtonBackground;

        public bool PopupFiltersAreClear => !HasActiveAdvancedFilters;

        public double ClearPopupFiltersButtonOpacity => HasActiveAdvancedFilters ? 1.0 : 0.0;

        public string AllFilterLabel
            => FormatFilterOptionLabel("All", filterOptionCounts.Installed + filterOptionCounts.NotInstalled);

        public string AllFilterButtonBackground => PopupFiltersAreClear ? "#8A1BB6" : "#5B6068";

        public string AllFilterButtonBorderBrush => PopupFiltersAreClear ? "#B61BD1" : "#6A7079";

        public string CompatibleFilterLabel => FormatFilterOptionLabel("Compatible", filterOptionCounts.Compatible);

        public string InstalledFilterLabel => FormatFilterOptionLabel("Installed", filterOptionCounts.Installed);

        public string UpdatableFilterLabel => FormatFilterOptionLabel("Updatable", filterOptionCounts.Updatable);

        public string ReplaceableFilterLabel => FormatFilterOptionLabel("Replaceable", filterOptionCounts.Replaceable);

        public string CachedFilterLabel => FormatFilterOptionLabel("Cached", filterOptionCounts.Cached);

        public string UncachedFilterLabel => FormatFilterOptionLabel("Not Cached", filterOptionCounts.Uncached);

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

        public string ReleaseDateSortLabel
            => SortOptionLabel(ModSortOption.ReleaseDate, "Released");

        public string InstallDateSortLabel
            => SortOptionLabel(ModSortOption.InstallDate, "Install Date");

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

        public bool ReleaseDateSortSelected => SelectedSortOption?.Value == ModSortOption.ReleaseDate;

        public bool InstallDateSortSelected => SelectedSortOption?.Value == ModSortOption.InstallDate;

        public bool VersionSortSelected => SelectedSortOption?.Value == ModSortOption.Version;

        public bool InstalledFirstSortSelected => SelectedSortOption?.Value == ModSortOption.InstalledFirst;

        public bool UpdatesFirstSortSelected => SelectedSortOption?.Value == ModSortOption.UpdatesFirst;

        public bool HasActiveFilters
            => !string.IsNullOrWhiteSpace(ModSearchText)
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
                foreach ((string label, string value) in EnumerateAdvancedTextFilters())
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        parts.Add($"{label}: {value.Trim()}");
                    }
                }

                AddTriStateSummary(parts, "Installed", FilterInstalledState);
                AddTriStateSummary(parts, "Updatable", FilterUpdatableState);
                AddTriStateSummary(parts, "Compatible", FilterCompatibleState);
                AddTriStateSummary(parts, "Cached", FilterCachedState);
                AddTriStateSummary(parts, "Replaceable", FilterReplaceableState);

                if (parts.Count == 0)
                {
                    return "All mods are shown.";
                }

                return parts.Count > 0
                    ? string.Join(" • ", parts)
                    : "All mods are shown.";
            }
        }

        public bool ShowInstallAction => SelectedMod != null
                                         && !IsSelectedModLoading
                                         && !SelectedMod.IsInstalled
                                         && (SelectedModVersionChoice != null
                                             ? SelectedModSelectedVersionIsCompatible
                                             : !SelectedModIsIncompatible);

        public bool ShowUpdateAction => SelectedMod?.IsInstalled == true
                                        && !IsSelectedModLoading
                                        && (SelectedModVersionChoice != null
                                            ? !SelectedModSelectedVersionMatchesInstalled
                                            : SelectedMod.HasVersionUpdate);

        public bool ShowRemoveAction => SelectedMod?.IsInstalled == true
                                        && SelectedMod?.IsAutodetected != true
                                        && !IsSelectedModLoading
                                        && (SelectedModVersionChoice != null
                                            ? SelectedModSelectedVersionMatchesInstalled
                                            : SelectedMod?.HasVersionUpdate != true);

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

        public bool ShowSelectedModActionRow
            => ShowInstallNowAction
               || ShowRemoveNowAction
               || ShowPrimarySelectedModAction;

        public bool ShowOpenSelectedModCacheLocationAction
            => !IsSelectedModLoading
               && !string.IsNullOrWhiteSpace(selectedModCachedArchivePath);

        public bool ShowSelectedModActionUnavailableNote
            => !IsSelectedModLoading
               && !ShowInstallNowAction
               && !ShowPrimarySelectedModAction
               && (SelectedMod?.IsAutodetected == true
               || !SelectedModSelectedVersionIsCompatible);

        public string SelectedModActionUnavailableNote
            => SelectedMod?.IsAutodetected == true
                ? "This mod is managed outside CKAN. CKAN can use it for dependency checks, but removal must be done manually from GameData."
                : SelectedModVersionChoice == null
                    ? "This mod cannot be installed with the current compatibility settings. Adjust Compatible game versions in Settings if you want to allow it."
                    : $"{SelectedModVersionChoice.VersionText} cannot be installed with the current compatibility settings. Pick a different version or adjust Compatible game versions in Settings.";

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
                    if (SelectedModVersionChoice == null)
                    {
                        return "Queue Update";
                    }

                    return SelectedModVersionChoice?.VersionComparisonToInstalled switch
                    {
                        > 0 => "Queue Update",
                        < 0 => "Queue Downgrade",
                        _ => "Queue Version Change",
                    };
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
                    return "#4B5A69";
                }

                if (ShowInstallAction)
                {
                    return "#2B6A98";
                }
                if (ShowUpdateAction)
                {
                    if (SelectedModVersionChoice == null)
                    {
                        return "#6A952B";
                    }

                    return SelectedModVersionChoice?.VersionComparisonToInstalled switch
                    {
                        < 0 => "#8C6432",
                        _ => "#6A952B",
                    };
                }
                if (ShowRemoveAction)
                {
                    return "#9A485C";
                }

                return "#4B5A69";
            }
        }

        public string PrimarySelectedModActionBorderBrush => PrimarySelectedModActionBackground;

        public bool SelectedModIsInstalled
        {
            get => selectedModIsInstalled;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModIsInstalled, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
            }
        }

        public bool SelectedModHasUpdate
        {
            get => selectedModHasUpdate;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModHasUpdate, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
            }
        }

        public bool SelectedModIsAutodetected
        {
            get => selectedModIsAutodetected;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModIsAutodetected, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
                this.RaisePropertyChanged(nameof(SelectedModShowsDependencyState));
                this.RaisePropertyChanged(nameof(SelectedModShowsIncompatibleState));
            }
        }

        public bool SelectedModIsCached
        {
            get => selectedModIsCached;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModIsCached, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
            }
        }

        public bool SelectedModIsIncompatible
        {
            get => selectedModIsIncompatible;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModIsIncompatible, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
                this.RaisePropertyChanged(nameof(SelectedModShowsDependencyState));
                this.RaisePropertyChanged(nameof(SelectedModShowsIncompatibleState));
            }
        }

        public bool SelectedModHasReplacement
        {
            get => selectedModHasReplacement;
            private set
            {
                this.RaiseAndSetIfChanged(ref selectedModHasReplacement, value);
                this.RaisePropertyChanged(nameof(ShowSelectedModStateBadges));
            }
        }

        public bool SelectedModShowsDependencyState
            => SelectedModIsAutodetected && SelectedModIsIncompatible;

        public bool SelectedModShowsIncompatibleState
            => SelectedModIsIncompatible && !SelectedModIsAutodetected;

        public bool ShowSelectedModStateBadges
            => SelectedModIsInstalled
               || SelectedModIsCached
               || SelectedModHasUpdate
               || SelectedModIsAutodetected
               || SelectedModShowsIncompatibleState
               || SelectedModShowsDependencyState
               || SelectedModHasReplacement;

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

        public bool ShowInlineApplyResult => HasApplyResult && !ShowExecutionResultOverlay;

        public bool ShowExecutionOverlay
            => ShowExecutionProgressOverlay || ShowExecutionResultOverlay;

        public bool ShowExecutionProgressOverlay => IsApplyingChanges;

        public bool ShowExecutionResultOverlay
        {
            get => showExecutionResultOverlay;
            private set
            {
                this.RaiseAndSetIfChanged(ref showExecutionResultOverlay, value);
                this.RaisePropertyChanged(nameof(ShowExecutionOverlay));
            }
        }

        public string ExecutionDialogTitle => currentExecutionTitle;

        public string ExecutionDialogMessage
            => IsUserBusy && !string.IsNullOrWhiteSpace(StatusMessage)
                ? StatusMessage
                : currentExecutionStatusLabel;

        public bool HasExecutionProgressValue
            => ProgressPercent > 0 && ProgressPercent < 100;

        public bool IsExecutionProgressIndeterminate => !HasExecutionProgressValue;

        public double ExecutionProgressValue => ProgressPercent;

        public string ExecutionResultAcknowledgeLabel => "OK";

        public bool IsPreviewLoading
        {
            get => isPreviewLoading;
            private set
            {
                this.RaiseAndSetIfChanged(ref isPreviewLoading, value);
                this.RaisePropertyChanged(nameof(PreviewStatusLabel));
                this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
                this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
                this.RaisePropertyChanged(nameof(PreviewFooterNote));
                this.RaisePropertyChanged(nameof(PreviewImpactSummary));
                this.RaisePropertyChanged(nameof(PreviewShowsLoadingCard));
                this.RaisePropertyChanged(nameof(PreviewShowsReadyCard));
                this.RaisePropertyChanged(nameof(PreviewShowsBlockedCard));
                this.RaisePropertyChanged(nameof(ShowPreviewConflictPopup));
                this.RaisePropertyChanged(nameof(ApplyChangesButtonBackground));
                this.RaisePropertyChanged(nameof(ApplyChangesButtonBorderBrush));
            }
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
                this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
                this.RaisePropertyChanged(nameof(PreviewFooterNote));
                this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(CanSwitchInstances));
                PublishExecutionOverlayState();
            }
        }

        private bool IsUserBusy
        {
            get => isUserBusy;
            set
            {
                this.RaiseAndSetIfChanged(ref isUserBusy, value);
                this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
                this.RaisePropertyChanged(nameof(ExecutionDialogMessage));
            }
        }

        public string PreviewStatusLabel
            => !HasQueuedActions && ShowInlineApplyResult
                ? ApplyResultMessage
                : IsPreviewLoading
                    ? "Resolving dependencies and downloads…"
                    : !HasQueuedChangeActions && HasQueuedDownloadActions
                        ? "Queued downloads ready"
                        : PreviewCanApply
                            ? HasPreviewAttentionNotes
                                ? "Apply is ready, but prompts will appear"
                                : "Apply is ready"
                            : HasPreviewAttentionNotes && !HasPreviewConflicts
                                ? "Required steps must be cleared before apply"
                                : "Conflicts must be cleared before apply";

        public string PreviewOutcomeTitle
            => !HasQueuedActions && ShowInlineApplyResult
                ? ApplyResultTitle
                : PreviewShowsLoadingCard
                    ? "Analyzing Queued Changes"
                    : PreviewShowsReadyCard
                        ? !HasQueuedChangeActions && HasQueuedDownloadActions
                            ? "Ready to Download Files"
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
                    return "Queue install, update, or remove actions from Browse to build an apply preview. Right-click a mod to add it to cache.";
                }

                if (!HasQueuedActions && ShowInlineApplyResult)
                {
                    return "Review the latest apply result here, then dismiss it when you are ready to build another queue.";
                }

                if (PreviewShowsLoadingCard)
                {
                    return "CKAN Linux is resolving install order, dependency closure, downloads, and apply blockers.";
                }

                if (!HasQueuedChangeActions && HasQueuedDownloadActions)
                {
                    return "Queued downloads will fill the cache without changing GameData.";
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

        public string PreviewFooterNote
            => !HasQueuedActions && ShowInlineApplyResult
                ? "Review the result, then dismiss it when you are ready to build another changeset."
                : PreviewShowsEmptyCard
                    ? "Queue install, update, or remove actions from Browse to build a changeset preview."
                    : !HasQueuedChangeActions && HasQueuedDownloadActions
                        ? "Download Files stores archives in the cache without changing GameData."
                        : PreviewCanApply
                            ? HasPreviewAttentionNotes
                                ? "Apply changes is ready. You may still need to confirm prompts during install."
                                : "Apply changes will update GameData after the required downloads finish."
                            : HasPreviewConflicts
                                ? "Apply stays disabled until the conflicts above are resolved."
                                : "Apply stays disabled until the required setup steps above are cleared.";

        public string PreviewImpactSummary
        {
            get
            {
                if (PreviewShowsEmptyCard)
                {
                    return "Queue install, update, or remove actions to see downloads, dependencies, auto-removals, and conflicts before applying. Right-click a mod to add it to cache.";
                }

                if (!HasQueuedActions && ShowInlineApplyResult)
                {
                    return ApplyResultMessage;
                }

                var parts = new List<string>();

                if (QueuedChangeActionCount > 0)
                {
                    parts.Add(CountLabel(QueuedChangeActionCount, "queued change", "queued changes"));
                }
                if (QueuedDownloadActionCount > 0)
                {
                    parts.Add(CountLabel(QueuedDownloadActionCount, "queued download", "queued downloads"));
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

        public bool ShowPreviewQueuedMetric => QueuedChangeActionCount > 0;

        public string PreviewDownloadQueueCountLabel
            => CountLabel(QueuedDownloadActionCount, "Queued Download", "Queued Downloads");

        public string PreviewDownloadMetricTitle => "Queued Downloads";

        public bool ShowPreviewQueuedDownloadMetric => QueuedDownloadActionCount > 0;

        public string PreviewDownloadCountLabel
            => CountLabel(PreviewDownloadsRequired.Count, "Required Download", "Required Downloads");

        public bool ShowPreviewDownloadCountMetric => PreviewDownloadsRequired.Count > 0;

        public string PreviewDependencyCountLabel
            => CountLabel(PreviewDependencies.Count, "Auto Install", "Auto Installs");

        public bool ShowPreviewDependencyMetric => PreviewDependencies.Count > 0;

        public bool ShowPreviewQueuedActions
            => HasQueuedActions;

        public string PreviewQueuedGuidance
            => HasQueuedChangeActions && HasQueuedDownloadActions
                ? "Install, update, and remove actions are listed together with queued downloads. Download Files runs separately and does not change GameData."
                : HasQueuedChangeActions
                    ? HasPreviewDependencies
                        ? "These are the direct install/update/remove actions you selected. CKAN will also install the required mods listed below."
                        : "These are the direct install/update/remove actions you selected."
                    : "These items will be downloaded into the cache for later use. They do not change GameData.";

        public bool ShowPreviewDownloadQueueGuidance
            => HasQueuedDownloadActions && HasQueuedChangeActions;

        public string PreviewDownloadQueueGuidanceTitle => "Queued Downloads";

        public string PreviewDownloadQueueGuidance
            => "These queued downloads run separately from Apply Changes. Download Files will cache them without changing GameData.";

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

        public bool ShowPreviewAutoRemovalMetric => PreviewAutoRemovals.Count > 0;

        public string PreviewConflictCountLabel
            => CountLabel(PreviewConflicts.Count, "Conflict", "Conflicts");

        public bool ShowPreviewConflictMetric => PreviewConflicts.Count > 0;

        public string PreviewAttentionCountLabel
            => CountLabel(PreviewAttentionNotes.Count, "Required Step", "Required Steps");

        public bool ShowPreviewAttentionMetric => PreviewAttentionNotes.Count > 0;

        public string ApplyChangesButtonBackground
            => !HasQueuedChangeActions
                ? "#39424E"
                : PreviewCanApply && !IsPreviewLoading && !IsApplyingChanges
                    ? "#2A6B4A"
                    : "#39424E";

        public string ApplyChangesButtonBorderBrush => ApplyChangesButtonBackground;

        public string DownloadQueuedButtonBackground
            => !HasQueuedDownloadActions || IsApplyingChanges
                ? "#39424E"
                : "#2B5C88";

        public string DownloadQueuedButtonBorderBrush => DownloadQueuedButtonBackground;

        public string DownloadQueuedButtonLabel => "Download Files";

        public string CollapsedQueueStubTitle
            => HasQueuedActions
                ? QueueCountLabel
                : ShowInlineApplyResult
                    ? ApplyResultTitle
                    : "No pending changes";

        public string CollapsedQueueStubSummary
            => HasQueuedActions
                ? $"{PreviewStatusLabel} • {PreviewImpactSummary}"
                : ShowInlineApplyResult
                    ? ApplyResultMessage
                    : "Queue install, update, or remove actions to preview changes. Right-click a mod to add it to cache.";

        public string CollapsedQueueStubBackground
            => HasQueuedActions
                ? "#161B21"
                : ShowInlineApplyResult
                    ? ApplyResultBackground
                    : "#161B21";

        public string CollapsedQueueStubBorderBrush
            => HasQueuedActions
                ? "#2B323C"
                : ShowInlineApplyResult
                    ? ApplyResultBorderBrush
                    : "#2B323C";

        public string SelectedModQueueStatus
        {
            get
            {
                if (SelectedMod == null)
                {
                    return "Choose a mod to queue an install, update, or removal. Right-click a mod to add it to cache.";
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
                    return "No queued item for this mod yet. Queue the install or right-click to add it to cache.";
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
                    ClearFilter(ref filterNotUpdatableOnly, nameof(FilterNotUpdatableOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterNotUpdatableOnly
        {
            get => filterNotUpdatableOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterNotUpdatableOnly, value) && value)
                {
                    ClearFilter(ref filterUpdatableOnly, nameof(FilterUpdatableOnly));
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
                if (this.RaiseAndSetIfChanged(ref filterHasReplacementOnly, value) && value)
                {
                    ClearFilter(ref filterNoReplacementOnly, nameof(FilterNoReplacementOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool FilterNoReplacementOnly
        {
            get => filterNoReplacementOnly;
            set
            {
                if (this.RaiseAndSetIfChanged(ref filterNoReplacementOnly, value) && value)
                {
                    ClearFilter(ref filterHasReplacementOnly, nameof(FilterHasReplacementOnly));
                }
                PublishFilterStateLabels();
            }
        }

        public bool? FilterInstalledState
        {
            get => GetTriStateFilterValue(filterInstalledOnly, filterNotInstalledOnly);
            set => SetInstalledFilterState(value);
        }

        public bool? FilterUpdatableState
        {
            get => GetTriStateFilterValue(filterUpdatableOnly, filterNotUpdatableOnly);
            set => SetUpdatableFilterState(value);
        }

        public bool? FilterCompatibleState
        {
            get => GetTriStateFilterValue(filterCompatibleOnly, filterIncompatibleOnly);
            set => SetExclusiveTriStateFilter(value,
                                              ref filterCompatibleOnly,
                                              nameof(FilterCompatibleOnly),
                                              ref filterIncompatibleOnly,
                                              nameof(FilterIncompatibleOnly),
                                              nameof(FilterCompatibleState));
        }

        public bool? FilterCachedState
        {
            get => GetTriStateFilterValue(filterCachedOnly, filterUncachedOnly);
            set => SetExclusiveTriStateFilter(value,
                                              ref filterCachedOnly,
                                              nameof(FilterCachedOnly),
                                              ref filterUncachedOnly,
                                              nameof(FilterUncachedOnly),
                                              nameof(FilterCachedState));
        }

        public bool? FilterReplaceableState
        {
            get => GetTriStateFilterValue(filterHasReplacementOnly, filterNoReplacementOnly);
            set => SetExclusiveTriStateFilter(value,
                                              ref filterHasReplacementOnly,
                                              nameof(FilterHasReplacementOnly),
                                              ref filterNoReplacementOnly,
                                              nameof(FilterNoReplacementOnly),
                                              nameof(FilterReplaceableState));
        }

        public int FilterInstalledTriStateIndex
        {
            get => TriStateFilterToIndex(FilterInstalledState);
            set => FilterInstalledState = TriStateIndexToFilter(value);
        }

        public int FilterUpdatableTriStateIndex
        {
            get => TriStateFilterToIndex(FilterUpdatableState);
            set => FilterUpdatableState = TriStateIndexToFilter(value);
        }

        public int FilterCompatibleTriStateIndex
        {
            get => TriStateFilterToIndex(FilterCompatibleState);
            set => FilterCompatibleState = TriStateIndexToFilter(value);
        }

        public int FilterCachedTriStateIndex
        {
            get => TriStateFilterToIndex(FilterCachedState);
            set => FilterCachedState = TriStateIndexToFilter(value);
        }

        public int FilterReplaceableTriStateIndex
        {
            get => TriStateFilterToIndex(FilterReplaceableState);
            set => FilterReplaceableState = TriStateIndexToFilter(value);
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

        private static int CountTriStateFilter(bool? value)
            => value.HasValue ? 1 : 0;

        private static int TriStateFilterToIndex(bool? value)
            => value switch
            {
                true  => 1,
                false => 2,
                _     => 0,
            };

        private static bool? TriStateIndexToFilter(int value)
            => value switch
            {
                1 => true,
                2 => false,
                _ => null,
            };

        private static bool? GetTriStateFilterValue(bool includeOnly, bool excludeOnly)
            => includeOnly == excludeOnly ? (bool?)null : includeOnly;

        private static void AddTriStateSummary(ICollection<string> parts, string label, bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            parts.Add($"{label}: {(value.Value ? "Yes" : "No")}");
        }

        private IReadOnlyList<FilterTagOptionItem> BuildAvailableTagOptions(IEnumerable<ModListItem> items,
                                                                            FilterState              currentFilter)
        {
            var sourceItems = items.ToList();
            var selectedValues = SelectedFilterValues(currentFilter.TagText);
            var allTags = sourceItems.SelectMany(item => SplitListValues(item.Tags))
                                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                                     .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
                                     .ToList();

            return allTags.Select(tag =>
                          {
                              var previewValues = selectedValues.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
                              previewValues.Add(tag);
                              int count = modCatalogService.ApplyFilter(sourceItems,
                                                                        currentFilter with
                                                                        {
                                                                            TagText = SerializeFilterValues(previewValues),
                                                                        })
                                                           .Count;
                              return new FilterTagOptionItem(tag,
                                                             count,
                                                             selectedValues.Contains(tag));
                          })
                          .ToList();
        }

        private static IReadOnlyList<FilterTagOptionItem> BuildAvailableLabelOptions(IEnumerable<ModListItem> items,
                                                                                     string                  selectedLabel)
            => BuildAvailableFilterOptions(items, selectedLabel, item => item.Labels);

        private static IReadOnlyList<FilterTagOptionItem> BuildAvailableFilterOptions(IEnumerable<ModListItem> items,
                                                                                      string                  selectedValue,
                                                                                      Func<ModListItem, string> selector)
        {
            var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var item in items)
            {
                var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (string value in selector(item).Split(',',
                                                             StringSplitOptions.RemoveEmptyEntries
                                                             | StringSplitOptions.TrimEntries))
                {
                    if (!seen.Add(value))
                    {
                        continue;
                    }

                    counts[value] = counts.TryGetValue(value, out int current)
                        ? current + 1
                        : 1;
                }
            }

            var selectedValues = SelectedFilterValues(selectedValue);
            return counts.OrderBy(kvp => kvp.Key, StringComparer.CurrentCultureIgnoreCase)
                         .Select(kvp => new FilterTagOptionItem(kvp.Key,
                                                                kvp.Value,
                                                                selectedValues.Contains(kvp.Key)))
                         .ToList();
        }

        private static HashSet<string> SelectedFilterValues(string? text)
            => SplitFilterValues(text).ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        private static IEnumerable<string> SplitFilterValues(string? text)
            => (text ?? "").Split(new[] { ',', ';', '\n', '\r' },
                                  StringSplitOptions.RemoveEmptyEntries
                                  | StringSplitOptions.TrimEntries)
                           .Where(value => !string.IsNullOrWhiteSpace(value));

        private static IEnumerable<string> SplitListValues(string? text)
            => (text ?? "").Split(',',
                                  StringSplitOptions.RemoveEmptyEntries
                                  | StringSplitOptions.TrimEntries)
                           .Where(value => !string.IsNullOrWhiteSpace(value));

        private static string SerializeFilterValues(IEnumerable<string> values)
            => string.Join("; ",
                           values.Where(value => !string.IsNullOrWhiteSpace(value))
                                 .Distinct(StringComparer.CurrentCultureIgnoreCase)
                                 .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase));

        private IEnumerable<(string Label, string Value)> EnumerateAdvancedTextFilters()
        {
            yield return ("Name", AdvancedNameFilter);
            yield return ("Identifier", AdvancedIdentifierFilter);
            yield return ("Author", AdvancedAuthorFilter);
            yield return ("Summary", AdvancedSummaryFilter);
            yield return ("Description", AdvancedDescriptionFilter);
            yield return ("License", AdvancedLicenseFilter);
            yield return ("Language", AdvancedLanguageFilter);
            yield return ("Depends", AdvancedDependsFilter);
            yield return ("Recommends", AdvancedRecommendsFilter);
            yield return ("Suggests", AdvancedSuggestsFilter);
            yield return ("Conflicts", AdvancedConflictsFilter);
            yield return ("Supports", AdvancedSupportsFilter);
            yield return ("Category", AdvancedTagsFilter);
            yield return ("Labels", AdvancedLabelsFilter);
            yield return ("Compatibility", AdvancedCompatibilityFilter);
        }

        private bool SetFilterBackingField(ref bool field, bool value, string propertyName)
        {
            if (field == value)
            {
                return false;
            }

            field = value;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        private void SetExclusiveTriStateFilter(bool?  value,
                                                ref bool includeOnlyField,
                                                string includeOnlyPropertyName,
                                                ref bool excludeOnlyField,
                                                string excludeOnlyPropertyName,
                                                string triStatePropertyName)
        {
            bool includeOnly = value == true;
            bool excludeOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref includeOnlyField, includeOnly, includeOnlyPropertyName);
                changed |= SetFilterBackingField(ref excludeOnlyField, excludeOnly, excludeOnlyPropertyName);
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(triStatePropertyName);
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void SetInstalledFilterState(bool? value)
        {
            bool installedOnly = value == true;
            bool notInstalledOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref filterInstalledOnly, installedOnly, nameof(FilterInstalledOnly));
                changed |= SetFilterBackingField(ref filterNotInstalledOnly, notInstalledOnly, nameof(FilterNotInstalledOnly));
                if (notInstalledOnly)
                {
                    changed |= SetFilterBackingField(ref filterUpdatableOnly, false, nameof(FilterUpdatableOnly));
                }
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(nameof(FilterInstalledState));
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void SetUpdatableFilterState(bool? value)
        {
            bool updatableOnly = value == true;
            bool notUpdatableOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref filterUpdatableOnly, updatableOnly, nameof(FilterUpdatableOnly));
                changed |= SetFilterBackingField(ref filterNotUpdatableOnly, notUpdatableOnly, nameof(FilterNotUpdatableOnly));
                if (updatableOnly)
                {
                    changed |= SetFilterBackingField(ref filterNotInstalledOnly, false, nameof(FilterNotInstalledOnly));
                }
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(nameof(FilterUpdatableState));
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void NormalizeStoredFilterFlags()
        {
            if (filterInstalledOnly && filterNotInstalledOnly)
            {
                filterNotInstalledOnly = false;
            }
            if (filterNotInstalledOnly && filterUpdatableOnly)
            {
                filterUpdatableOnly = false;
            }
            if (filterUpdatableOnly && filterNotUpdatableOnly)
            {
                filterNotUpdatableOnly = false;
            }
            if (filterCompatibleOnly && filterIncompatibleOnly)
            {
                filterIncompatibleOnly = false;
            }
            if (filterCachedOnly && filterUncachedOnly)
            {
                filterUncachedOnly = false;
            }
            if (filterHasReplacementOnly && filterNoReplacementOnly)
            {
                filterNoReplacementOnly = false;
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
                if (ShouldKeepCurrentSelectedMod(value))
                {
                    return;
                }

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

        public string? SelectedPreviewConflict
        {
            get => selectedPreviewConflict;
            set => this.RaiseAndSetIfChanged(ref selectedPreviewConflict, value);
        }

        public int SelectedPreviewConflictCount
        {
            get => selectedPreviewConflictCount;
            private set => this.RaiseAndSetIfChanged(ref selectedPreviewConflictCount, value);
        }

        public bool TogglePreviewConflictSelection(string conflict)
        {
            if (string.IsNullOrWhiteSpace(conflict))
            {
                return false;
            }

            var selected = selectedPreviewConflicts.Add(conflict);
            if (!selected)
            {
                selectedPreviewConflicts.Remove(conflict);
            }

            SelectedPreviewConflict = selectedPreviewConflicts.FirstOrDefault();
            SelectedPreviewConflictCount = selectedPreviewConflicts.Count;
            return selected;
        }

        public void ViewPreviewConflictInBrowser(PreviewConflictChoiceItem choice)
        {
            var conflict = choice.ConflictText;
            var leftSide = ConflictLeftSide(conflict);
            var rightSide = ConflictRightSide(conflict);
            var identifiers = ConflictBrowserIdentifiers(leftSide, rightSide);
            if (identifiers.Count == 0)
            {
                StatusMessage = "No browser-visible mods were found for that conflict.";
                return;
            }

            relationshipBrowserScopeIdentifiers = identifiers;
            RelationshipBrowserScopeText = $"Conflict: {DisplayConflictTarget(leftSide)} vs {DisplayConflictTarget(rightSide)}";
            pendingModListScrollReset = true;
            ShowBrowseSurfaceTab();
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems(identifiers.FirstOrDefault());
            }
            PublishRelationshipBrowserScopeState();
        }

        public void ActivateModFromBrowser(ModListItem mod)
        {
            if (SelectedMod != null
                && ShowDetailsPane
                && string.Equals(SelectedMod.Identifier, mod.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                SelectedMod = null;
                ShowDetailsPane = false;
                return;
            }

            ShowDetailsPane = true;
            SelectedMod = mod;
        }

        public bool ShowQueueContextAction(ModListItem mod)
            => !IsApplyingChanges
               && (changesetService.FindQueuedApplyAction(mod.Identifier) != null
               || CanQueueInstall(mod)
               || CanQueueUpdate(mod)
               || CanQueueRemove(mod));

        public string QueueContextLabel(ModListItem mod)
        {
            var queued = changesetService.FindQueuedApplyAction(mod.Identifier);
            if (queued != null)
            {
                return $"Cancel {queued.ActionText}";
            }

            if (CanQueueInstall(mod))
            {
                return "Queue Install";
            }
            if (CanQueueUpdate(mod))
            {
                return "Queue Update";
            }
            if (CanQueueRemove(mod))
            {
                return "Queue Remove";
            }

            return "";
        }

        public void ToggleQueueActionFromBrowser(ModListItem mod)
        {
            if (IsApplyingChanges)
            {
                return;
            }

            var queued = changesetService.FindQueuedApplyAction(mod.Identifier);
            ClearApplyResult();

            if (queued != null)
            {
                var returnToPreview = ShowConflictBrowserScope;
                if (changesetService.Remove(queued.Identifier))
                {
                    StatusMessage = $"Removed queued {queued.ActionText.ToLowerInvariant()} for {mod.Name}.";
                    if (returnToPreview)
                    {
                        _ = ReturnToPreviewAfterConflictQueueChangeAsync();
                    }
                }
                return;
            }

            if (CanQueueInstall(mod))
            {
                changesetService.QueueInstall(mod);
                StatusMessage = $"Queued install for {mod.Name}.";
            }
            else if (CanQueueUpdate(mod))
            {
                changesetService.QueueUpdate(mod);
                StatusMessage = $"Queued update for {mod.Name}.";
            }
            else if (CanQueueRemove(mod))
            {
                changesetService.QueueRemove(mod);
                StatusMessage = $"Queued removal for {mod.Name}.";
            }
        }

        public bool ShowDownloadOnlyContextAction(ModListItem mod)
        {
            if (mod.IsAutodetected)
            {
                return false;
            }

            if (changesetService.FindQueuedDownloadAction(mod.Identifier) != null)
            {
                return true;
            }

            return changesetService.FindQueuedApplyAction(mod.Identifier) == null
                   && !mod.IsCached
                   && !mod.IsIncompatible;
        }

        public bool ShowPurgeCacheContextAction(ModListItem mod)
            => !IsApplyingChanges
               && mod.IsCached
               && CurrentCache != null;

        public string DownloadOnlyContextLabel(ModListItem mod)
            => changesetService.FindQueuedDownloadAction(mod.Identifier) != null
                ? "Cancel Add to Cache"
                : "Add to Cache";

        public string PurgeCacheContextLabel(ModListItem mod)
            => "Purge from cache";

        public void ToggleDownloadOnlyFromBrowser(ModListItem mod)
        {
            var queuedDownload = changesetService.FindQueuedDownloadAction(mod.Identifier);
            ClearApplyResult();

            if (queuedDownload != null)
            {
                if (changesetService.Remove(queuedDownload.Identifier))
                {
                    StatusMessage = $"Removed queued add-to-cache action for {mod.Name}.";
                }
                return;
            }

            if (mod.IsAutodetected)
            {
                StatusMessage = $"{mod.Name} is managed outside CKAN and cannot be added to the cache.";
                return;
            }

            var queuedApply = changesetService.FindQueuedApplyAction(mod.Identifier);
            if (queuedApply != null)
            {
                StatusMessage = mod.IsInstalled
                    ? $"{queuedApply.ActionText} is already queued for {mod.Name}. Cancel it before adding it to the cache."
                    : $"{queuedApply.ActionText} is already queued for {mod.Name}. Cancel it before adding it to the cache.";
                return;
            }

            changesetService.QueueDownload(mod);
            StatusMessage = $"Queued add to cache for {mod.Name}.";
        }

        public void PurgeCacheFromBrowser(ModListItem mod)
        {
            if (IsApplyingChanges
                || CurrentCache == null
                || CurrentRegistry == null)
            {
                return;
            }

            var modules = Enumerable.Repeat(CurrentRegistry.InstalledModule(mod.Identifier)?.Module, 1)
                                    .Concat(Utilities.DefaultIfThrows(
                                                () => CurrentRegistry.AvailableByIdentifier(mod.Identifier))
                                            ?? Enumerable.Empty<CkanModule>())
                                    .OfType<CkanModule>()
                                    .Distinct()
                                    .ToArray();

            if (modules.Length == 0)
            {
                StatusMessage = $"No cached archive was found for {mod.Name}.";
                return;
            }

            if (CurrentCache.Purge(modules))
            {
                ClearApplyResult();
                StatusMessage = $"Purged cached archive for {mod.Name}.";
                _ = LoadModCatalogAsync();
            }
            else
            {
                StatusMessage = $"No cached archive was removed for {mod.Name}.";
            }
        }

        private async Task RefreshAsync()
        {
            var wasReady = IsReady;
            IsRefreshing = true;
            ClearApplyResult();
            Diagnostics = "Loading instance metadata.";
            if (wasReady)
            {
                IsCatalogLoading = true;
                CatalogStatusMessage = "Reloading mods from the current CKAN registry and repository cache…";
                StatusMessage = "Reloading the current instance…";
            }
            else
            {
                StartupStage = StartupStage.Loading;
                StageTitle = "Loading Instances";
                StageDescription = "Inspecting your configured installs and preparing the browser.";
                StatusMessage = "Checking CKAN game instances…";
            }

            try
            {
                await gameInstanceService.InitializeAsync(CancellationToken.None);
                ReloadInstances(loadCatalog: false, updateReadyStatus: !wasReady);
                if (IsReady)
                {
                    await LoadModCatalogAsync();
                }
                else if (wasReady)
                {
                    IsCatalogLoading = false;
                }
            }
            catch (Exception ex)
            {
                if (wasReady)
                {
                    IsCatalogLoading = false;
                }
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
            if (SelectedInstance == null || IsCatalogLoading)
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
            if (IsCatalogLoading || IsRefreshing || IsApplyingChanges)
            {
                SelectedInstance = Instances.FirstOrDefault(inst => inst.IsCurrent) ?? target;
                return false;
            }

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

        private void ReloadInstances(bool loadCatalog = true,
                                     bool updateReadyStatus = true)
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
                if (updateReadyStatus)
                {
                    StatusMessage = $"Loaded {Instances.Count} instance{(Instances.Count == 1 ? "" : "s")} and activated {gameInstanceService.CurrentInstance.Name}.";
                }
                SelectedActionLabel = "Open Selected Install";
                SelectedActionHint = "Choose a different install here if you want to switch contexts.";
                RestorePersistedQueuedActions();
                if (loadCatalog)
                {
                    _ = LoadModCatalogAsync();
                }
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
                        var items = await modCatalogService.GetAllModListAsync(CancellationToken.None);
                        if (activeRequestId != catalogLoadRequestId)
                        {
                            continue;
                        }

                        allCatalogItems = items;
                        ApplyCatalogFilterToLoadedItems(previousSelection);
                        PruneQueuedAutodetectedRemovals(items);
                        PruneQueuedAutodetectedDownloads(items);
                        SeedDevQueueSmoke(items);
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

                selectedModDetails = details;
                SelectedModTitle = details.Title;
                SelectedModSubtitle = string.IsNullOrWhiteSpace(details.Summary)
                    ? details.Identifier
                    : $"{details.Identifier} • {details.Summary}";
                SelectedModAuthors = string.IsNullOrWhiteSpace(details.Authors)
                    ? "Author information unavailable"
                    : $"By {details.Authors}";
                SelectedModVersions = BuildSelectedModVersions(details);
                SelectedModInstallState = BuildInstallState(details);
                SelectedModDownloadCount = details.DownloadCount?.ToString("N0") ?? "Unknown";
                SelectedModIsInstalled = details.IsInstalled;
                SelectedModIsAutodetected = details.IsAutodetected;
                SelectedModHasUpdate = details.HasVersionUpdate;
                SelectedModHasReplacement = details.HasReplacement;
                SelectedModBody = string.IsNullOrWhiteSpace(details.Description)
                    ? "No extended description is available for this mod."
                    : details.Description;
                PopulateSelectedModVersionChoices(details);
                ApplySelectedVersionDetails();
                SetSelectedModDetailsSection(ModDetailsSection.Overview);
            }
            catch (Exception ex)
            {
                if (!IsCurrentSelectedModRequest(identifier, requestId))
                {
                    return;
                }

                Diagnostics = ex.Message;
                selectedModDetails = null;
                SelectedModTitle = "Could not load details";
                SelectedModSubtitle = identifier;
                SelectedModAuthors = "";
                SelectedModVersions = "";
                SelectedModInstallState = "";
                SelectedModCompatibility = "";
                SelectedModCacheState = "";
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
                SelectedModIsAutodetected = false;
                SelectedModHasUpdate = false;
                SelectedModIsCached = false;
                SelectedModIsIncompatible = false;
                SelectedModHasReplacement = false;
                SelectedModBody = "The selected mod failed to load its details.";
                SelectedModVersionChoice = null;
                SelectedModAvailableVersions.Clear();
                ReplaceSelectedModResourceLinks(Array.Empty<ModResourceLinkItem>());
                ReplaceSelectedModCollection(SelectedModDependencies, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModRecommendations, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModSuggestions, Array.Empty<ModRelationshipItem>());
                ShowSelectedModDependenciesExpanded = false;
                ShowSelectedModRecommendationsExpanded = false;
                ShowSelectedModSuggestionsExpanded = false;
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
        {
            if (!suppressQueueSnapshotPersistence)
            {
                SaveQueuedActionsForCurrentInstance();
            }

            Dispatcher.UIThread.Post(() =>
            {
                RefreshQueuedActions();
                _ = LoadPreviewAsync();
            });
        }

        private FilterState CurrentFilter()
            => new FilterState
            {
                SearchText          = ModSearchText,
                TagText             = AdvancedTagsFilter,
                SortOption          = SelectedSortOption?.Value ?? ModSortOption.Name,
                SortDescending      = SortDescending,
                InstalledOnly       = FilterInstalledOnly,
                NotInstalledOnly    = FilterNotInstalledOnly,
                UpdatableOnly       = FilterUpdatableOnly,
                NotUpdatableOnly    = FilterNotUpdatableOnly,
                CompatibleOnly      = FilterCompatibleOnly,
                CachedOnly          = FilterCachedOnly,
                UncachedOnly        = FilterUncachedOnly,
                IncompatibleOnly    = FilterIncompatibleOnly,
                HasReplacementOnly  = FilterHasReplacementOnly,
                NoReplacementOnly   = FilterNoReplacementOnly,
            };

        private FilterState CurrentSortOnlyFilter()
            => new FilterState
            {
                SortOption     = SelectedSortOption?.Value ?? ModSortOption.Name,
                SortDescending = SortDescending,
                InstalledOnly  = false,
            };

        private void RefreshCatalogForFilterChange()
        {
            if (suppressFilterAutoRefresh)
            {
                return;
            }

            modSearchService.SetCurrent(CurrentFilter());
            PublishFilterStateLabels();
            if (IsReady)
            {
                pendingModListScrollReset = true;
                if (allCatalogItems.Count > 0)
                {
                    ApplyCatalogFilterToLoadedItems();
                }
                else
                {
                    _ = LoadModCatalogAsync();
                }
            }
        }

        private void ApplyStoredFilterState(FilterState filter)
        {
            modSearchText = filter.SearchText ?? "";
            advancedNameFilter = "";
            advancedIdentifierFilter = "";
            advancedAuthorFilter = "";
            advancedSummaryFilter = "";
            advancedDescriptionFilter = "";
            advancedLicenseFilter = "";
            advancedLanguageFilter = "";
            advancedDependsFilter = "";
            advancedRecommendsFilter = "";
            advancedSuggestsFilter = "";
            advancedConflictsFilter = "";
            advancedSupportsFilter = "";
            advancedTagsFilter = SerializeFilterValues(SplitFilterValues(filter.TagText));
            advancedLabelsFilter = "";
            advancedCompatibilityFilter = "";
            filterInstalledOnly = filter.InstalledOnly;
            filterNotInstalledOnly = filter.NotInstalledOnly;
            filterUpdatableOnly = filter.UpdatableOnly;
            filterNotUpdatableOnly = filter.NotUpdatableOnly;
            filterCompatibleOnly = filter.CompatibleOnly;
            filterCachedOnly = filter.CachedOnly;
            filterUncachedOnly = filter.UncachedOnly;
            filterIncompatibleOnly = filter.IncompatibleOnly;
            filterHasReplacementOnly = filter.HasReplacementOnly;
            filterNoReplacementOnly = filter.NoReplacementOnly;
            NormalizeStoredFilterFlags();
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
        {
            if (!ShowAdvancedFilters)
            {
                ShowAdvancedFilterEditor = false;
            }

            ShowAdvancedFilters = !ShowAdvancedFilters;
        }

        private void ToggleAdvancedFilterEditor()
            => ShowAdvancedFilterEditor = !ShowAdvancedFilterEditor;

        private void ToggleTagFilterPicker()
        {
            if (!HasAvailableTagOptions)
            {
                return;
            }

            ShowLabelFilterPicker = false;
            ShowTagFilterPicker = !ShowTagFilterPicker;
        }

        private void ToggleLabelFilterPicker()
        {
            if (!HasAvailableLabelOptions)
            {
                return;
            }

            ShowTagFilterPicker = false;
            ShowLabelFilterPicker = !ShowLabelFilterPicker;
        }

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
        {
            ShowPreviewSurface = true;
            RefreshPreviewConflictPopupState();
        }

        private void DismissPreviewConflictPopup()
        {
            dismissedPreviewConflictKey = CurrentPreviewConflictKey();
            previewConflictPopupDismissed = true;
            PublishPreviewConflictPopupState();
        }

        private void RefreshPreviewConflictPopupState()
        {
            RefreshPreviewConflictChoices();

            if (!HasPreviewConflicts)
            {
                previewConflictPopupDismissed = false;
                dismissedPreviewConflictKey = "";
                ClearPreviewConflictSelection();
                PublishPreviewConflictPopupState();
                return;
            }

            RemoveStalePreviewConflictSelections();

            var currentKey = CurrentPreviewConflictKey();
            if (!string.Equals(dismissedPreviewConflictKey, currentKey, StringComparison.Ordinal))
            {
                previewConflictPopupDismissed = false;
            }

            PublishPreviewConflictPopupState();
        }

        private void RefreshPreviewConflictChoices()
        {
            var seenConflictPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PreviewConflictChoices.Clear();

            foreach (var conflict in PreviewConflicts)
            {
                var pairKey = ConflictPairKey(conflict);
                if (!string.IsNullOrWhiteSpace(pairKey)
                    && !seenConflictPairs.Add(pairKey))
                {
                    continue;
                }

                PreviewConflictChoices.Add(BuildPreviewConflictChoice(conflict));
            }

            this.RaisePropertyChanged(nameof(PreviewConflictChoices));
        }

        private void RemoveStalePreviewConflictSelections()
        {
            var validConflicts = PreviewConflicts.ToHashSet(StringComparer.Ordinal);
            if (selectedPreviewConflicts.RemoveWhere(conflict => !validConflicts.Contains(conflict)) > 0)
            {
                SelectedPreviewConflict = selectedPreviewConflicts.FirstOrDefault();
                SelectedPreviewConflictCount = selectedPreviewConflicts.Count;
            }
        }

        private void ClearPreviewConflictSelection()
        {
            if (selectedPreviewConflicts.Count == 0
                && SelectedPreviewConflict == null
                && SelectedPreviewConflictCount == 0)
            {
                return;
            }

            selectedPreviewConflicts.Clear();
            SelectedPreviewConflict = null;
            SelectedPreviewConflictCount = 0;
        }

        private PreviewConflictChoiceItem BuildPreviewConflictChoice(string conflict)
        {
            var leftTarget = ConflictLeftSide(conflict);
            var rightTarget = ConflictRightSide(conflict);
            var leftInfo = ResolveConflictSideInfo(leftTarget);
            var rightInfo = ResolveConflictSideInfo(rightTarget);
            if (string.IsNullOrWhiteSpace(leftTarget))
            {
                leftTarget = leftInfo.DisplayName;
            }

            var displayLeftTarget = leftInfo.DisplayName;
            var displayRightTarget = rightInfo.DisplayName;
            var actionText = string.IsNullOrWhiteSpace(displayRightTarget)
                ? $"Review {displayLeftTarget}"
                : $"{displayLeftTarget} conflicts with {displayRightTarget}";

            return new PreviewConflictChoiceItem
            {
                ConflictText = conflict,
                ActionText = actionText,
                DetailText = BuildConflictChoiceDetail(leftInfo, rightInfo),
            };
        }

        private ConflictSideInfo ResolveConflictSideInfo(string side)
        {
            var queuedAction = QueuedActionFromConflictSide(side);
            var mod = ModFromConflictSide(side);
            var displayName = queuedAction?.Name
                              ?? mod?.Name
                              ?? StripConflictVersionSuffix(side);
            return new ConflictSideInfo
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? "selected mod"
                    : displayName,
                QueuedAction = queuedAction,
                Mod = mod,
            };
        }

        private string BuildConflictChoiceDetail(ConflictSideInfo left,
                                                 ConflictSideInfo right)
        {
            var leftQueued = left.QueuedAction?.ActionKind is QueuedActionKind.Install
                                                      or QueuedActionKind.Update;
            var rightQueued = right.QueuedAction?.ActionKind is QueuedActionKind.Install
                                                       or QueuedActionKind.Update;
            var leftRemoving = left.QueuedAction?.ActionKind == QueuedActionKind.Remove;
            var rightRemoving = right.QueuedAction?.ActionKind == QueuedActionKind.Remove;
            var leftInstalled = left.Mod?.IsInstalled == true;
            var rightInstalled = right.Mod?.IsInstalled == true;

            if (leftRemoving || rightRemoving)
            {
                var removing = leftRemoving ? left.DisplayName : right.DisplayName;
                return $"{removing} is already queued for removal. Review the other mod before applying.";
            }

            if (leftQueued && rightQueued)
            {
                return "Both mods are queued. Unqueue one of them to continue.";
            }

            if (leftQueued && rightInstalled)
            {
                return $"{left.DisplayName} is queued. Unqueue it or queue removal for {right.DisplayName}.";
            }

            if (rightQueued && leftInstalled)
            {
                return $"{right.DisplayName} is queued. Unqueue it or queue removal for {left.DisplayName}.";
            }

            if (leftInstalled && rightInstalled)
            {
                return "Both mods are installed. Queue removal for one of them to continue.";
            }

            if (leftQueued || rightQueued)
            {
                var queued = leftQueued ? left.DisplayName : right.DisplayName;
                return $"{queued} is queued. Unqueue it or review the other mod.";
            }

            if (leftInstalled || rightInstalled)
            {
                var installed = leftInstalled ? left.DisplayName : right.DisplayName;
                return $"{installed} is installed. Queue removal if it should not stay.";
            }

            return "Review both mods in Browse, then unqueue a pending action or queue removal.";
        }

        private static string ConflictPairKey(string conflict)
        {
            var left = NormalizeConflictPairSide(ConflictLeftSide(conflict));
            var right = NormalizeConflictPairSide(ConflictRightSide(conflict));
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return "";
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) <= 0
                ? $"{left}|{right}"
                : $"{right}|{left}";
        }

        private static string NormalizeConflictPairSide(string side)
            => StripConflictVersionSuffix(side).Trim().ToUpperInvariant();

        private string CurrentPreviewConflictKey()
            => string.Join("\n",
                           PreviewConflicts.OrderBy(conflict => conflict,
                                                    StringComparer.Ordinal));

        private void PublishPreviewConflictPopupState()
        {
            this.RaisePropertyChanged(nameof(ShowPreviewConflictPopup));
            this.RaisePropertyChanged(nameof(PreviewConflictPopupTitle));
        }

        internal void ToggleSurfaceViewTogglePinned()
            => SurfaceViewTogglePinned = !SurfaceViewTogglePinned;

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
                var returnToPreview = ShowConflictBrowserScope;
                ClearApplyResult();
                if (changesetService.Remove(queued.Identifier))
                {
                    StatusMessage = $"Removed queued {queued.ActionText.ToLowerInvariant()} for {SelectedMod.Name}.";
                    if (returnToPreview)
                    {
                        _ = ReturnToPreviewAfterConflictQueueChangeAsync();
                    }
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
            AdvancedNameFilter = "";
            AdvancedIdentifierFilter = "";
            AdvancedAuthorFilter = "";
            AdvancedSummaryFilter = "";
            AdvancedDescriptionFilter = "";
            AdvancedLicenseFilter = "";
            AdvancedLanguageFilter = "";
            AdvancedDependsFilter = "";
            AdvancedRecommendsFilter = "";
            AdvancedSuggestsFilter = "";
            AdvancedConflictsFilter = "";
            AdvancedSupportsFilter = "";
            AdvancedTagsFilter = "";
            AdvancedLabelsFilter = "";
            AdvancedCompatibilityFilter = "";
            ShowTagFilterPicker = false;
            ShowLabelFilterPicker = false;
        }

        private void ClearAllFilters()
        {
            pendingModListScrollReset = true;
            ClearRelationshipBrowserScope();
            ModSearchText = "";
            ClearPopupFilters();
        }

        private void ClearPopupFilters()
        {
            pendingModListScrollReset = true;
            FilterInstalledOnly = false;
            FilterNotInstalledOnly = false;
            FilterUpdatableOnly = false;
            FilterNotUpdatableOnly = false;
            FilterCompatibleOnly = false;
            FilterCachedOnly = false;
            FilterUncachedOnly = false;
            FilterIncompatibleOnly = false;
            FilterHasReplacementOnly = false;
            FilterNoReplacementOnly = false;
            ClearAdvancedFilters();
        }

        private void ClearTagFilter()
        {
            AdvancedTagsFilter = "";
            ShowTagFilterPicker = false;
        }

        private void SelectTagFilter(FilterTagOptionItem? option)
        {
            if (option == null)
            {
                return;
            }

            var selectedValues = SelectedFilterValues(AdvancedTagsFilter);
            if (!selectedValues.Add(option.Name))
            {
                selectedValues.Remove(option.Name);
            }

            AdvancedTagsFilter = SerializeFilterValues(selectedValues);
            ShowTagFilterPicker = false;
        }

        private void ClearLabelFilter()
        {
            AdvancedLabelsFilter = "";
            ShowLabelFilterPicker = false;
        }

        private void SelectLabelFilter(FilterTagOptionItem? option)
        {
            if (option == null)
            {
                return;
            }

            AdvancedLabelsFilter = option.Name;
            ShowLabelFilterPicker = false;
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

            var preservedSelection = SelectedMod;
            preserveSelectedModDuringSortReorder = preservedSelection != null;

            var sortedItems = SortItems(Mods).ToList();
            try
            {
                for (int targetIndex = 0; targetIndex < sortedItems.Count; targetIndex++)
                {
                    int currentIndex = Mods.IndexOf(sortedItems[targetIndex]);
                    if (currentIndex >= 0 && currentIndex != targetIndex)
                    {
                        Mods.Move(currentIndex, targetIndex);
                    }
                }
            }
            finally
            {
                preserveSelectedModDuringSortReorder = false;
            }

            if (preservedSelection != null && Mods.Contains(preservedSelection))
            {
                this.RaisePropertyChanged(nameof(SelectedMod));
            }

            ConsumePendingModListScrollReset();
        }

        private void ApplyCatalogFilterToLoadedItems(string? preferredSelectionIdentifier = null)
        {
            var currentFilter = CurrentFilter();
            var sourceItems = ShowRelationshipBrowserScope
                ? allCatalogItems.Where(item => relationshipBrowserScopeIdentifiers.Contains(item.Identifier)).ToList()
                : allCatalogItems;
            var visibleItems = ShowRelationshipBrowserScope
                ? modCatalogService.ApplyFilter(sourceItems, CurrentSortOnlyFilter())
                : modCatalogService.ApplyFilter(sourceItems, currentFilter);

            filterOptionCounts = modCatalogService.GetFilterOptionCounts(sourceItems, currentFilter);
            hasFilterOptionCounts = true;

            ReplaceVisibleMods(visibleItems);
            ReplaceAvailableTagOptions(BuildAvailableTagOptions(sourceItems, currentFilter));
            ReplaceAvailableLabelOptions(BuildAvailableLabelOptions(sourceItems, currentFilter.LabelText));
            PublishVisibleModQueueState();

            string? selectedIdentifier = preferredSelectionIdentifier ?? SelectedMod?.Identifier;
            SelectedMod = selectedIdentifier != null
                ? Mods.FirstOrDefault(mod => mod.Identifier.Equals(selectedIdentifier, StringComparison.OrdinalIgnoreCase))
                  ?? Mods.FirstOrDefault()
                : Mods.FirstOrDefault();

            if (pendingModListScrollReset)
            {
                pendingModListScrollReset = false;
                ModListScrollResetRequestId++;
            }

            CatalogStatusMessage = ShowRelationshipBrowserScope
                ? Mods.Count == 0
                    ? $"No loaded mods matched {RelationshipBrowserScopeText}."
                    : $"Showing {Mods.Count} mod{(Mods.Count == 1 ? "" : "s")} from {RelationshipBrowserScopeText}."
                : Mods.Count == 0
                    ? "No mods matched the current search and filter state."
                    : $"Showing {Mods.Count} mod{(Mods.Count == 1 ? "" : "s")} for {CurrentInstanceName}.";
            PublishCatalogStateLabels();
            PublishFilterOptionCountLabels();
        }

        private void ReplaceVisibleMods(IEnumerable<ModListItem> items)
        {
            var visibleItems = items.ToList();
            Mods.Clear();
            foreach (var item in visibleItems)
            {
                Mods.Add(item);
            }

            var skeletonRows = BuildCatalogSkeletonRows(visibleItems);
            if (skeletonRows.Count > 0)
            {
                CatalogSkeletonRows = skeletonRows;
                appSettingsService.SaveCatalogSkeletonRows(skeletonRows.Select(ToCatalogSkeletonSnapshotRow).ToList());
            }
        }

        private void ReplaceAvailableTagOptions(IEnumerable<FilterTagOptionItem> items)
        {
            AvailableTagOptions.Clear();
            foreach (var item in items)
            {
                AvailableTagOptions.Add(item);
            }

            this.RaisePropertyChanged(nameof(HasAvailableTagOptions));
            this.RaisePropertyChanged(nameof(TagFilterPickerSummary));
            this.RaisePropertyChanged(nameof(SelectedCategoryCount));
            this.RaisePropertyChanged(nameof(HasSelectedTagFilter));
        }

        private void ReplaceAvailableLabelOptions(IEnumerable<FilterTagOptionItem> items)
        {
            AvailableLabelOptions.Clear();
            foreach (var item in items)
            {
                AvailableLabelOptions.Add(item);
            }

            this.RaisePropertyChanged(nameof(HasAvailableLabelOptions));
            this.RaisePropertyChanged(nameof(LabelFilterPickerSummary));
            this.RaisePropertyChanged(nameof(HasSelectedLabelFilter));
        }

        private void UpdateAvailableTagOptionSelection()
        {
            var selectedTags = SelectedFilterValues(AdvancedTagsFilter);
            foreach (var item in AvailableTagOptions)
            {
                item.IsSelected = selectedTags.Contains(item.Name);
            }

            this.RaisePropertyChanged(nameof(SelectedCategoryCount));
            this.RaisePropertyChanged(nameof(HasSelectedTagFilter));
        }

        private void UpdateAvailableLabelOptionSelection()
        {
            string selectedLabel = AdvancedLabelsFilter.Trim();
            foreach (var item in AvailableLabelOptions)
            {
                item.IsSelected = string.Equals(item.Name,
                                                selectedLabel,
                                                StringComparison.CurrentCultureIgnoreCase);
            }

            this.RaisePropertyChanged(nameof(HasSelectedLabelFilter));
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

        private async Task PlayGameAsync(GameLaunchMode mode)
        {
            if (CurrentInstance is not GameInstance instance)
            {
                StatusMessage = "Select a game instance before launching.";
                return;
            }

            string? command = FindLaunchCommand(mode);
            if (command == null)
            {
                StatusMessage = mode == GameLaunchMode.Steam
                    ? "No Steam launch command was detected for this instance."
                    : "No direct launch command was detected for this instance.";
                return;
            }

            if (!await ConfirmLaunchAgainstIncompatibleModsAsync(instance))
            {
                StatusMessage = "Launch cancelled.";
                return;
            }

            instance.PlayGame(command,
                              () => Dispatcher.UIThread.Post(() =>
                              {
                                  RefreshInstanceSummaries();
                                  if (mode == GameLaunchMode.Direct)
                                  {
                                      StatusMessage = $"{instance.Name} launch process exited.";
                                  }
                              }));

            StatusMessage = mode == GameLaunchMode.Steam
                ? $"Launching {instance.Name} via Steam..."
                : $"Launching {instance.Name} directly...";
        }

        private async Task<bool> ConfirmLaunchAgainstIncompatibleModsAsync(GameInstance instance)
        {
            if (CurrentRegistry == null)
            {
                return true;
            }

            var suppressedIdentifiers = instance.GetSuppressedCompatWarningIdentifiers;
            var incompatible = CurrentRegistry.IncompatibleInstalled(instance.VersionCriteria())
                                              .Where(module => !module.Module.IsDLC
                                                               && !suppressedIdentifiers.Contains(module.identifier))
                                              .ToList();
            if (incompatible.Count == 0)
            {
                return true;
            }

            if (ConfirmIncompatibleLaunchAsync == null)
            {
                return true;
            }

            string details = string.Join(Environment.NewLine,
                                         incompatible.Select(module =>
                                             $"- {module.Module} ({module.Module.CompatibleGameVersions(instance.Game)})"));
            string prompt = "Some installed modules are incompatible with this game version. "
                            + "It might not be safe to launch the game."
                            + Environment.NewLine
                            + Environment.NewLine
                            + details
                            + Environment.NewLine
                            + Environment.NewLine
                            + "Launch anyway?";
            return await ConfirmIncompatibleLaunchAsync(prompt);
        }

        private string? FindLaunchCommand(GameLaunchMode mode)
        {
            if (CurrentInstance is not GameInstance instance)
            {
                return null;
            }

            try
            {
                return GameCommandLineConfigStore.Load(instance, CurrentSteamLibrary)
                                                 .FirstOrDefault(command =>
                                                     mode == GameLaunchMode.Steam
                                                         ? SteamLibrary.IsSteamCmdLine(command)
                                                         : !SteamLibrary.IsSteamCmdLine(command));
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                return null;
            }
        }

        private void OpenSelectedModCacheLocation()
        {
            if (ResolveSelectedModCachedArchivePath() is not string path)
            {
                StatusMessage = "No cached archive is available for this mod.";
                return;
            }

            Utilities.OpenFileBrowser(path);
            StatusMessage = "Opened cached archive location in your file manager.";
        }

        private void OpenSelectedModResourceLink(ModResourceLinkItem? link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.Url))
            {
                StatusMessage = "No link is available for this resource.";
                return;
            }

            LaunchExternal(link.Url,
                           $"Opened {link.Label.ToLowerInvariant()}.",
                           $"Could not open {link.Label.ToLowerInvariant()}.");
        }

        internal void OpenSelectedModResourceLinkFromUi(ModResourceLinkItem? link)
            => OpenSelectedModResourceLink(link);

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
            PublishLaunchCommandState();
            this.RaisePropertyChanged(nameof(InstanceCountLabel));
            PublishCompatibleGameVersionState();
            this.RaisePropertyChanged(nameof(ShowHeaderInstanceSwitcher));
            this.RaisePropertyChanged(nameof(ShowPassiveHeaderInstanceLabel));
            this.RaisePropertyChanged(nameof(ShowStartupInstancePanel));
            this.RaisePropertyChanged(nameof(ShowReadyInstancePanel));
            this.RaisePropertyChanged(nameof(SelectedInstanceIsCurrent));
            this.RaisePropertyChanged(nameof(ShowSwitchSelectedInstanceAction));
            this.RaisePropertyChanged(nameof(SelectedInstanceContextTitle));
            UpdateSelectedModCachedArchivePath();
        }

        private void PublishLaunchCommandState()
        {
            this.RaisePropertyChanged(nameof(CanPlayDirect));
            this.RaisePropertyChanged(nameof(CanPlayViaSteam));
        }

        private void ClearCatalogState()
        {
            allCatalogItems = Array.Empty<ModListItem>();
            Mods.Clear();
            AvailableTagOptions.Clear();
            AvailableLabelOptions.Clear();
            ShowTagFilterPicker = false;
            ShowLabelFilterPicker = false;
            filterOptionCounts = new FilterOptionCounts();
            hasFilterOptionCounts = false;
            ResetSelectedModDetails();
            SelectedMod = null;
            CatalogStatusMessage = "Select an active instance to view its mod catalog.";
            PublishCatalogStateLabels();
            PublishFilterOptionCountLabels();
            this.RaisePropertyChanged(nameof(HasAvailableTagOptions));
            this.RaisePropertyChanged(nameof(TagFilterPickerSummary));
            this.RaisePropertyChanged(nameof(SelectedCategoryCount));
            this.RaisePropertyChanged(nameof(HasSelectedTagFilter));
            this.RaisePropertyChanged(nameof(HasAvailableLabelOptions));
            this.RaisePropertyChanged(nameof(LabelFilterPickerSummary));
            this.RaisePropertyChanged(nameof(HasSelectedLabelFilter));
        }

        private void QueueInstallSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueInstall(SelectedMod, SelectedModVersionChoice?.VersionKey);
            StatusMessage = SelectedModVersionChoice == null
                ? $"Queued install for {SelectedMod.Name}."
                : $"Queued install of {SelectedMod.Name} {SelectedModVersionChoice.VersionText}.";
        }

        private bool QueueInstallCandidate(CkanModule module)
        {
            if (module.IsDLC)
            {
                return false;
            }

            var catalogItem = allCatalogItems.FirstOrDefault(mod =>
                string.Equals(mod.Identifier, module.identifier, StringComparison.OrdinalIgnoreCase));
            if (catalogItem == null || catalogItem.IsInstalled || catalogItem.IsIncompatible)
            {
                return false;
            }

            changesetService.QueueInstall(catalogItem, module.version?.ToString());
            return true;
        }

        private void QueueUpdateSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueUpdate(SelectedMod, SelectedModVersionChoice?.VersionKey);
            StatusMessage = SelectedModVersionChoice == null
                ? $"Queued update for {SelectedMod.Name}."
                : $"Queued version change for {SelectedMod.Name} to {SelectedModVersionChoice.VersionText}.";
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

        private void SeedDevQueueSmoke(IReadOnlyList<ModListItem> catalogItems)
        {
            if (hasSeededDevQueueSmoke
                || !DevQueueSmokeEnabled()
                || catalogItems.Count == 0)
            {
                return;
            }

            hasSeededDevQueueSmoke = true;
            suppressQueueSnapshotPersistence = true;
            SaveEmptyQueuedActionSnapshotForCurrentInstance();
            ClearApplyResult();
            changesetService.Clear();

            var usedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int seeded = 0;

            seeded += QueueDevSmokeActions(catalogItems,
                                           usedIdentifiers,
                                           CanQueueInstall,
                                           mod => changesetService.QueueInstall(mod),
                                           InstallSmokeScore,
                                           10);
            seeded += QueueDevSmokeActions(catalogItems,
                                           usedIdentifiers,
                                           CanQueueUpdate,
                                           mod => changesetService.QueueUpdate(mod),
                                           _ => 0,
                                           3);
            seeded += QueueDevSmokeActions(catalogItems,
                                           usedIdentifiers,
                                           CanQueueRemove,
                                           mod => changesetService.QueueRemove(mod),
                                           _ => 0,
                                           3);
            seeded += QueueDevSmokeActions(catalogItems,
                                           usedIdentifiers,
                                           CanQueueDownloadForDevSmoke,
                                           mod => changesetService.QueueDownload(mod),
                                           DownloadSmokeScore,
                                           DevQueueSmokeTargetActionCount - seeded);

            while (seeded < DevQueueSmokeTargetActionCount)
            {
                var added = 0;
                added += QueueDevSmokeActions(catalogItems,
                                              usedIdentifiers,
                                              CanQueueInstall,
                                              mod => changesetService.QueueInstall(mod),
                                              InstallSmokeScore,
                                              1);
                added += QueueDevSmokeActions(catalogItems,
                                              usedIdentifiers,
                                              CanQueueUpdate,
                                              mod => changesetService.QueueUpdate(mod),
                                              _ => 0,
                                              1);
                added += QueueDevSmokeActions(catalogItems,
                                              usedIdentifiers,
                                              CanQueueRemove,
                                              mod => changesetService.QueueRemove(mod),
                                              _ => 0,
                                              1);
                added += QueueDevSmokeActions(catalogItems,
                                              usedIdentifiers,
                                              CanQueueDownloadForDevSmoke,
                                              mod => changesetService.QueueDownload(mod),
                                              DownloadSmokeScore,
                                              1);
                if (added == 0)
                {
                    break;
                }

                seeded += added;
            }

            if (seeded > 0)
            {
                ShowPreviewSurface = true;
                IsQueueDrawerExpanded = true;
                PublishQueueStateLabels();
            }
        }

        private static int QueueDevSmokeActions(IReadOnlyList<ModListItem> catalogItems,
                                                HashSet<string>            usedIdentifiers,
                                                Func<ModListItem, bool>    canQueue,
                                                Action<ModListItem>        queue,
                                                Func<ModListItem, int>     score,
                                                int                        count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var mods = catalogItems.Where(item => !usedIdentifiers.Contains(item.Identifier)
                                                  && canQueue(item))
                                   .OrderByDescending(score)
                                   .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                                   .Take(count)
                                   .ToList();
            foreach (var mod in mods)
            {
                usedIdentifiers.Add(mod.Identifier);
                queue(mod);
            }

            return mods.Count;
        }

        private static bool CanQueueDownloadForDevSmoke(ModListItem mod)
            => !mod.IsAutodetected
               && !mod.IsCached
               && !mod.IsIncompatible;

        private static int InstallSmokeScore(ModListItem mod)
            => (!string.IsNullOrWhiteSpace(mod.Depends) ? 4 : 0)
               + (!string.IsNullOrWhiteSpace(mod.Recommends) ? 3 : 0)
               + (!string.IsNullOrWhiteSpace(mod.Suggests) ? 2 : 0)
               + (!mod.IsCached ? 1 : 0);

        private static int DownloadSmokeScore(ModListItem mod)
            => !mod.IsInstalled ? 1 : 0;

        private static bool DevQueueSmokeEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("CKAN_LINUX_DEV_QUEUE_SMOKE"),
                             "1",
                             StringComparison.Ordinal);

        private static IReadOnlyList<RecommendationAuditItem> BuildRecommendationAuditItems(
            Dictionary<CkanModule, Tuple<bool, List<string>>> recommendations,
            Dictionary<CkanModule, List<string>>              suggestions,
            Dictionary<CkanModule, HashSet<string>>           supporters,
            bool                                              preselectRecommendations,
            Func<CkanModule, int?>                            downloadCountForModule)
            => recommendations
                   .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                              "Recommendation",
                                                              FormatRecommendationSource("Recommended by", kvp.Value.Item2),
                                                              preselectRecommendations && kvp.Value.Item1,
                                                              downloadCountForModule(kvp.Key)))
                   .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                   .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                   .Concat(suggestions
                       .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                                  "Suggestion",
                                                                  FormatRecommendationSource("Suggested by", kvp.Value),
                                                                  false,
                                                                  downloadCountForModule(kvp.Key)))
                       .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase))
                   .Concat(supporters
                       .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                                  "Supporter",
                                                                  FormatRecommendationSource("Supports", kvp.Value),
                                                                  false,
                                                                  downloadCountForModule(kvp.Key)))
                       .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase))
                   .ToList();

        private static string FormatRecommendationSource(string              label,
                                                         IEnumerable<string> sources)
        {
            var sourceText = string.Join(", ", sources.Where(source => !string.IsNullOrWhiteSpace(source))
                                                      .OrderBy(source => source, StringComparer.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(sourceText)
                ? label
                : $"{label}: {sourceText}";
        }

        private static CkanModule? ResolveQueuedModule(IRegistryQuerier  registry,
                                                       GameInstance      instance,
                                                       QueuedActionModel action)
        {
            if (!string.IsNullOrWhiteSpace(action.TargetVersion))
            {
                var exact = TryQueuedVersion(registry, action);
                if (exact != null)
                {
                    return exact;
                }

                return null;
            }

            return TryLatestCompatible(registry, instance, action.Identifier);
        }

        private static CkanModule? TryQueuedVersion(IRegistryQuerier  registry,
                                                    QueuedActionModel action)
        {
            var targetVersion = action.TargetVersion.Trim();
            var exact = Utilities.DefaultIfThrows(() => registry.GetModuleByVersion(action.Identifier,
                                                                                    targetVersion));
            if (exact != null)
            {
                return exact;
            }

            if (registry.InstalledModule(action.Identifier)?.Module is CkanModule installed
                && VersionTextMatches(installed.version.ToString(), targetVersion))
            {
                return installed;
            }

            return Utilities.DefaultIfThrows(() => registry.AvailableByIdentifier(action.Identifier)
                .FirstOrDefault(module => VersionTextMatches(module.version.ToString(), targetVersion)));
        }

        private static CkanModule? TryLatestCompatible(IRegistryQuerier registry,
                                                       GameInstance    instance,
                                                       string          identifier)
        {
            try
            {
                var latest = registry.LatestAvailable(identifier,
                                                       instance.StabilityToleranceConfig,
                                                       instance.VersionCriteria());
                if (latest != null)
                {
                    return latest;
                }
            }
            catch
            {
            }

            var versionCriteria = instance.VersionCriteria();
            return Utilities.DefaultIfThrows(() => registry.AvailableByIdentifier(identifier)
                .Where(module => module.IsCompatible(versionCriteria))
                .OrderByDescending(module => module.version)
                .FirstOrDefault());
        }

        private static bool VersionTextMatches(string left, string right)
            => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase)
               || string.Equals(NormalizeVersionText(left),
                                NormalizeVersionText(right),
                                StringComparison.OrdinalIgnoreCase);

        private static string NormalizeVersionText(string version)
            => version.Trim().TrimStart('v', 'V');

        private static List<CkanModule> DistinctRecommendationModules(IEnumerable<CkanModule> modules)
            => modules.GroupBy(module => module.identifier, StringComparer.OrdinalIgnoreCase)
                      .Select(group => group.First())
                      .ToList();

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

        private void RemoveSelectedPreviewConflict()
        {
            if (selectedPreviewConflicts.Count == 0)
            {
                return;
            }

            var targets = selectedPreviewConflicts
                .Select(QueuedActionFromConflict)
                .Where(target => target != null)
                .Select(target => target!)
                .GroupBy(target => target.Identifier, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (targets.Count == 0)
            {
                StatusMessage = "Select conflicts for queued mods before removing them.";
                return;
            }

            ClearApplyResult();
            var removedNames = new List<string>();
            foreach (var target in targets)
            {
                if (changesetService.Remove(target.Identifier))
                {
                    removedNames.Add(target.Name);
                }
            }

            ClearPreviewConflictSelection();
            if (removedNames.Count > 0)
            {
                StatusMessage = removedNames.Count == 1
                    ? $"Removed queued action for {removedNames[0]}."
                    : $"Removed {removedNames.Count} queued actions.";
            }
        }

        private QueuedActionModel? QueuedActionFromConflict(string conflict)
            => QueuedActionFromConflictSide(ConflictLeftSide(conflict))
               ?? QueuedActions.OrderByDescending(action => action.Identifier.Length)
                               .FirstOrDefault(action => ConflictMentionsQueuedAction(conflict, action));

        private QueuedActionModel? QueuedActionFromConflictSide(string side)
            => QueuedActions.OrderByDescending(action => action.Identifier.Length)
                            .FirstOrDefault(action => ConflictSideStartsWith(side, action.Identifier)
                                                   || ConflictSideStartsWith(side, action.Name));

        private ModListItem? ModFromConflictSide(string side)
        {
            var directMatch = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ConflictSideStartsWith(side, item.Identifier)
                                     || ConflictSideStartsWith(side, item.Name));
            if (directMatch != null)
            {
                return directMatch;
            }

            var displayName = StripConflictVersionSuffix(side);
            return allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ContainsText(item.Identifier, displayName)
                                     || ContainsText(item.Name, displayName)
                                     || ContainsText(displayName, item.Identifier)
                                     || ContainsText(displayName, item.Name));
        }

        private static string ConflictLeftSide(string conflict)
        {
            var parts = conflict.Split(new[] { " conflicts with " },
                                       StringSplitOptions.None);
            return parts.Length > 0
                ? parts[0].Trim()
                : conflict.Trim();
        }

        private static string ConflictRightSide(string conflict)
        {
            var parts = conflict.Split(new[] { " conflicts with " },
                                       StringSplitOptions.None);
            return parts.Length > 1
                ? parts[1].Trim()
                : "";
        }

        private static string DisplayConflictTarget(string side,
                                                    QueuedActionModel? queuedTarget = null)
        {
            if (queuedTarget != null
                && (ConflictSideStartsWith(side, queuedTarget.Identifier)
                    || ConflictSideStartsWith(side, queuedTarget.Name)))
            {
                return queuedTarget.Name;
            }

            return StripConflictVersionSuffix(side);
        }

        private static string StripConflictVersionSuffix(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }

            var withoutVersion = Regex.Replace(trimmed,
                                               @"\s+(?:\d+:)?v?\d+(?:[.\-_]\w+)+(?:\+\S*)?$",
                                               "",
                                               RegexOptions.IgnoreCase);
            return withoutVersion.Length > 0
                ? withoutVersion
                : trimmed;
        }

        private static bool ConflictMentionsQueuedAction(string conflict,
                                                        QueuedActionModel action)
            => ContainsText(conflict, action.Identifier)
               || ContainsText(conflict, action.Name);

        private static bool ConflictSideStartsWith(string side,
                                                   string candidate)
        {
            if (string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            var trimmedSide = side.Trim();
            var trimmedCandidate = candidate.Trim();
            if (!trimmedSide.StartsWith(trimmedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmedSide.Length == trimmedCandidate.Length)
            {
                return true;
            }

            var next = trimmedSide[trimmedCandidate.Length];
            return char.IsWhiteSpace(next)
                   || next == ':'
                   || next == '('
                   || next == '[';
        }

        private static bool ContainsText(string text,
                                         string value)
            => !string.IsNullOrWhiteSpace(value)
               && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

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

        private void RestorePersistedQueuedActions()
        {
            if (hasRestoredQueuedActionSnapshot)
            {
                return;
            }

            var instanceName = gameInstanceService.CurrentInstance?.Name;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                return;
            }

            hasRestoredQueuedActionSnapshot = true;
            var snapshot = appSettingsService.QueuedActionSnapshot;
            if (snapshot.Actions.Count == 0)
            {
                return;
            }

            if (!string.Equals(snapshot.InstanceName, instanceName, StringComparison.Ordinal))
            {
                SaveQueuedActionsForCurrentInstance();
                return;
            }

            var actions = snapshot.Actions
                                  .Select(ToQueuedActionModel)
                                  .Where(action => !string.IsNullOrWhiteSpace(action.Identifier))
                                  .ToList();
            if (actions.Count == 0)
            {
                SaveQueuedActionsForCurrentInstance();
                return;
            }

            changesetService.Restore(actions);
        }

        private void SaveQueuedActionsForCurrentInstance()
        {
            appSettingsService.SaveQueuedActionSnapshot(new QueuedActionSnapshot
            {
                InstanceName = gameInstanceService.CurrentInstance?.Name ?? "",
                Actions = changesetService.CurrentQueue
                    .Select(ToQueuedActionSnapshotItem)
                    .ToList(),
            });
        }

        private void SaveEmptyQueuedActionSnapshotForCurrentInstance()
            => appSettingsService.SaveQueuedActionSnapshot(new QueuedActionSnapshot
            {
                InstanceName = gameInstanceService.CurrentInstance?.Name ?? "",
                Actions = new List<QueuedActionSnapshotItem>(),
            });

        private static QueuedActionSnapshotItem ToQueuedActionSnapshotItem(QueuedActionModel action)
            => new QueuedActionSnapshotItem
            {
                Identifier    = action.Identifier,
                Name          = action.Name,
                TargetVersion = action.TargetVersion,
                ActionKind    = action.ActionKind,
                ActionText    = action.ActionText,
                DetailText    = action.DetailText,
            };

        private static QueuedActionModel ToQueuedActionModel(QueuedActionSnapshotItem item)
            => new QueuedActionModel
            {
                Identifier    = item.Identifier ?? "",
                Name          = item.Name ?? item.Identifier ?? "",
                TargetVersion = item.TargetVersion ?? "",
                ActionKind    = item.ActionKind,
                ActionText    = string.IsNullOrWhiteSpace(item.ActionText)
                    ? DefaultQueuedActionText(item.ActionKind)
                    : item.ActionText,
                DetailText    = string.IsNullOrWhiteSpace(item.DetailText)
                    ? DefaultQueuedDetailText(item)
                    : item.DetailText,
            };

        private static string DefaultQueuedActionText(QueuedActionKind kind)
            => kind switch
            {
                QueuedActionKind.Download => "Add to Cache",
                QueuedActionKind.Install  => "Install",
                QueuedActionKind.Update   => "Update",
                QueuedActionKind.Remove   => "Remove",
                _                         => "Queue",
            };

        private static string DefaultQueuedDetailText(QueuedActionSnapshotItem item)
            => item.ActionKind switch
            {
                QueuedActionKind.Download => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Cache latest available version for later install"
                    : $"Cache {item.TargetVersion} for later install",
                QueuedActionKind.Install => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Install latest available version"
                    : $"Install {item.TargetVersion}",
                QueuedActionKind.Update => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Update to latest available version"
                    : $"Update to {item.TargetVersion}",
                QueuedActionKind.Remove => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Remove installed module"
                    : $"Remove {item.TargetVersion}",
                _ => "Queued action",
            };

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

            PublishVisibleModQueueState();
            PublishQueueStateLabels();
            PublishSelectedModActionState();
        }

        private void PublishVisibleModQueueState()
        {
            foreach (var mod in Mods)
            {
                PublishModQueueState(mod);
            }
        }

        private void PublishModQueueState(ModListItem mod)
        {
            var queued = changesetService.FindQueuedApplyAction(mod.Identifier)
                        ?? changesetService.FindQueuedDownloadAction(mod.Identifier);

            if (queued == null)
            {
                mod.QueueStateLabel = "";
                mod.QueueStateBackground = "#00000000";
                mod.QueueStateBorderBrush = "#00000000";
                mod.QueueRowAccentBrush = "#00000000";
                return;
            }

            mod.QueueStateLabel = queued.ActionKind switch
            {
                QueuedActionKind.Install => "Queued Install",
                QueuedActionKind.Update => "Queued Update",
                QueuedActionKind.Remove => "Queued Remove",
                QueuedActionKind.Download when string.Equals(queued.ActionText, "Add to Cache", StringComparison.Ordinal)
                    => "Queued Cache",
                QueuedActionKind.Download => "Queued Download",
                _ => "Queued",
            };

            (mod.QueueStateBackground, mod.QueueStateBorderBrush, mod.QueueRowAccentBrush) = queued.ActionKind switch
            {
                QueuedActionKind.Install => ("#1A2027", "#4B7B5E", "#664B7B5E"),
                QueuedActionKind.Update => ("#1A2027", "#7C8F55", "#667C8F55"),
                QueuedActionKind.Remove => ("#1A2027", "#8A5665", "#668A5665"),
                QueuedActionKind.Download => ("#1A2027", "#5F7DA0", "#665F7DA0"),
                _ => ("#1A2027", "#667487", "#66667487"),
            };
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
                IsPreviewLoading = false;
                PreviewSummary = QueuedDownloadActionCount == 1
                    ? "1 queued download is ready."
                    : $"{QueuedDownloadActionCount} queued downloads are ready.";
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

        private async Task ApplyQueuedChangesAsync(bool   promptForRecommendations = true,
                                                   string executionTitle = "Applying Changes",
                                                   string executionStatus = "Applying changes…")
        {
            if (promptForRecommendations && !await PromptForQueuedRecommendationsAsync())
            {
                return;
            }

            SetExecutionState(executionTitle, executionStatus);
            IsApplyingChanges = true;
            ApplyChangesResult? result = null;
            try
            {
                result = await modActionService.ApplyChangesAsync(CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Apply Failed",
                    Message = ex.Message,
                };
                SetApplyResult(result);
                Diagnostics = ex.Message;
                StatusMessage = "Apply failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }

            if (result != null)
            {
                ShowExecutionResultDialog(result.Success);
            }
        }

        private async Task InstallNowSelectedModAsync()
        {
            if (SelectedMod == null)
            {
                return;
            }

            var targetMod = SelectedMod;
            if (!HasQueuedChangeActions)
            {
                ClearApplyResult();
                changesetService.QueueInstall(targetMod, SelectedModVersionChoice?.VersionKey);
                if (!await PromptForQueuedRecommendationsAsync())
                {
                    changesetService.ClearApplyQueue();
                    return;
                }

                await ApplyQueuedChangesAsync(promptForRecommendations: false,
                                              executionTitle: $"Installing {targetMod.Name}",
                                              executionStatus: $"Installing {targetMod.Name}…");
                return;
            }

            ClearApplyResult();
            SetExecutionState($"Installing {targetMod.Name}", $"Installing {targetMod.Name}…");
            IsApplyingChanges = true;
            ApplyChangesResult? result = null;
            try
            {
                result = await modActionService.InstallNowAsync(targetMod,
                                                               CancellationToken.None,
                                                               SelectedModVersionChoice?.VersionKey);
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
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Install Failed",
                    Message = ex.Message,
                };
                SetApplyResult(result);
                Diagnostics = ex.Message;
                StatusMessage = "Install failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }

            if (result != null)
            {
                ShowExecutionResultDialog(result.Success);
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
            SetExecutionState($"Removing {targetMod.Name}", $"Removing {targetMod.Name}…");
            IsApplyingChanges = true;
            ApplyChangesResult? result = null;
            try
            {
                result = await modActionService.RemoveNowAsync(targetMod, CancellationToken.None);
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
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Removal Failed",
                    Message = ex.Message,
                };
                SetApplyResult(result);
                Diagnostics = ex.Message;
                StatusMessage = "Removal failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }

            if (result != null)
            {
                ShowExecutionResultDialog(result.Success);
            }
        }

        private async Task DownloadQueuedAsync()
        {
            SetExecutionState("Downloading Queued Files", "Downloading queued files…");
            IsApplyingChanges = true;
            ApplyChangesResult? result = null;
            try
            {
                result = await modActionService.DownloadQueuedAsync(CancellationToken.None);
                SetApplyResult(result);
                StatusMessage = result.Message;

                if (result.Success)
                {
                    await LoadModCatalogAsync();
                }
            }
            catch (Exception ex)
            {
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Downloads Failed",
                    Message = ex.Message,
                };
                SetApplyResult(result);
                Diagnostics = ex.Message;
                StatusMessage = "Downloads failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }

            if (result != null)
            {
                ShowExecutionResultDialog(result.Success);
            }
        }

        private void SetExecutionState(string title,
                                       string status)
        {
            currentExecutionTitle = title;
            currentExecutionStatusLabel = status;
            ProgressPercent = 0;
            PublishExecutionOverlayState();
            this.RaisePropertyChanged(nameof(PreviewStatusLabel));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewFooterNote));
        }

        private void ShowExecutionResultDialog(bool returnToBrowse)
        {
            returnToBrowseAfterExecutionResult = returnToBrowse;
            ShowExecutionResultOverlay = true;
            PublishExecutionOverlayState();
        }

        private void HideExecutionResultDialog()
        {
            returnToBrowseAfterExecutionResult = false;
            ShowExecutionResultOverlay = false;
            PublishExecutionOverlayState();
        }

        private void AcknowledgeExecutionResult()
        {
            bool returnToBrowse = returnToBrowseAfterExecutionResult;
            HideExecutionResultDialog();

            if (!returnToBrowse)
            {
                return;
            }

            ClearApplyResult();
            ShowBrowseSurfaceTab();
            SelectedMod = null;
        }

        private void ResetSelectedModDetails()
        {
            selectedModDetails = null;
            selectedModCachedArchivePath = null;
            SelectedModTitle = "No mod selected";
            SelectedModSubtitle = "Choose a mod to inspect its details.";
            SelectedModAuthors = "";
            SelectedModVersions = "";
            SelectedModInstallState = "";
            SelectedModCompatibility = "";
            SelectedModCacheState = "";
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
            SelectedModIsAutodetected = false;
            SelectedModHasUpdate = false;
            SelectedModIsCached = false;
            SelectedModIsIncompatible = false;
            SelectedModHasReplacement = false;
            SelectedModBody = "The details pane will show summary, description, compatibility, and install state.";
            SelectedModVersionChoice = null;
            SelectedModAvailableVersions.Clear();
            ReplaceSelectedModResourceLinks(Array.Empty<ModResourceLinkItem>());
            ReplaceSelectedModCollection(SelectedModDependencies, Array.Empty<ModRelationshipItem>());
            ReplaceSelectedModCollection(SelectedModRecommendations, Array.Empty<ModRelationshipItem>());
            ReplaceSelectedModCollection(SelectedModSuggestions, Array.Empty<ModRelationshipItem>());
            ShowSelectedModDependenciesExpanded = false;
            ShowSelectedModRecommendationsExpanded = false;
            ShowSelectedModSuggestionsExpanded = false;
            this.RaisePropertyChanged(nameof(ShowOpenSelectedModCacheLocationAction));
            PublishSelectedModRelationshipState();
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
            this.RaisePropertyChanged(nameof(HasAdvancedFilterText));
            this.RaisePropertyChanged(nameof(SelectedCategoryCount));
            this.RaisePropertyChanged(nameof(HasSelectedTagFilter));
            this.RaisePropertyChanged(nameof(ActiveFilterCount));
            this.RaisePropertyChanged(nameof(HasActiveFilters));
            this.RaisePropertyChanged(nameof(AdvancedFilterSummary));
            this.RaisePropertyChanged(nameof(MoreFiltersLabel));
            this.RaisePropertyChanged(nameof(MoreFiltersButtonBackground));
            this.RaisePropertyChanged(nameof(MoreFiltersButtonBorderBrush));
            this.RaisePropertyChanged(nameof(FiltersPopupTitle));
            this.RaisePropertyChanged(nameof(AdvancedFilterToggleLabel));
            this.RaisePropertyChanged(nameof(FiltersPopupWidth));
            this.RaisePropertyChanged(nameof(ShowSimpleFilterMenu));
            this.RaisePropertyChanged(nameof(ClearFiltersButtonBackground));
            this.RaisePropertyChanged(nameof(ClearFiltersButtonBorderBrush));
            this.RaisePropertyChanged(nameof(ClearFiltersButtonOpacity));
            this.RaisePropertyChanged(nameof(ClearPopupFiltersButtonOpacity));
            this.RaisePropertyChanged(nameof(ClearAdvancedTextButtonOpacity));
            this.RaisePropertyChanged(nameof(PopupFiltersAreClear));
            this.RaisePropertyChanged(nameof(AllFilterButtonBackground));
            this.RaisePropertyChanged(nameof(AllFilterButtonBorderBrush));
            this.RaisePropertyChanged(nameof(FilterInstalledState));
            this.RaisePropertyChanged(nameof(FilterUpdatableState));
            this.RaisePropertyChanged(nameof(FilterCompatibleState));
            this.RaisePropertyChanged(nameof(FilterCachedState));
            this.RaisePropertyChanged(nameof(FilterReplaceableState));
            this.RaisePropertyChanged(nameof(FilterInstalledTriStateIndex));
            this.RaisePropertyChanged(nameof(FilterUpdatableTriStateIndex));
            this.RaisePropertyChanged(nameof(FilterCompatibleTriStateIndex));
            this.RaisePropertyChanged(nameof(FilterCachedTriStateIndex));
            this.RaisePropertyChanged(nameof(FilterReplaceableTriStateIndex));
            this.RaisePropertyChanged(nameof(SortMenuLabel));
            this.RaisePropertyChanged(nameof(NameSortLabel));
            this.RaisePropertyChanged(nameof(AuthorSortLabel));
            this.RaisePropertyChanged(nameof(PopularitySortLabel));
            this.RaisePropertyChanged(nameof(CompatibilitySortLabel));
            this.RaisePropertyChanged(nameof(ReleaseDateSortLabel));
            this.RaisePropertyChanged(nameof(InstallDateSortLabel));
            this.RaisePropertyChanged(nameof(VersionSortLabel));
            this.RaisePropertyChanged(nameof(InstalledFirstSortLabel));
            this.RaisePropertyChanged(nameof(UpdatesFirstSortLabel));
            this.RaisePropertyChanged(nameof(NameSortSelected));
            this.RaisePropertyChanged(nameof(AuthorSortSelected));
            this.RaisePropertyChanged(nameof(PopularitySortSelected));
            this.RaisePropertyChanged(nameof(CompatibilitySortSelected));
            this.RaisePropertyChanged(nameof(ReleaseDateSortSelected));
            this.RaisePropertyChanged(nameof(InstallDateSortSelected));
            this.RaisePropertyChanged(nameof(VersionSortSelected));
            this.RaisePropertyChanged(nameof(InstalledFirstSortSelected));
            this.RaisePropertyChanged(nameof(UpdatesFirstSortSelected));
        }

        private void PublishFilterOptionCountLabels()
        {
            this.RaisePropertyChanged(nameof(AllFilterLabel));
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
               || sortOption == ModSortOption.ReleaseDate
               || sortOption == ModSortOption.InstallDate
               || sortOption == ModSortOption.InstalledFirst
               || sortOption == ModSortOption.UpdatesFirst;

        private bool ShouldKeepCurrentSelectedMod(ModListItem? candidate)
            => preserveSelectedModDuringSortReorder
               && selectedMod != null
               && Mods.Contains(selectedMod)
               && (candidate == null
               || !string.Equals(candidate.Identifier, selectedMod.Identifier, StringComparison.OrdinalIgnoreCase));

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
                ModSortOption.ReleaseDate
                    => descending
                        ? items.OrderByDescending(item => item.ReleaseDateValue.HasValue)
                               .ThenByDescending(item => item.ReleaseDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(item => item.ReleaseDateValue.HasValue)
                               .ThenBy(item => item.ReleaseDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.InstallDate
                    => descending
                        ? items.OrderByDescending(item => item.InstallDateValue.HasValue)
                               .ThenByDescending(item => item.InstallDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(item => item.InstallDateValue.HasValue)
                               .ThenBy(item => item.InstallDateValue)
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
                               .ThenByDescending(item => item.HasVersionUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.IsInstalled)
                               .ThenByDescending(item => item.HasVersionUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.UpdatesFirst
                    => descending
                        ? items.OrderByDescending(item => item.HasVersionUpdate)
                               .ThenByDescending(item => item.IsInstalled)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.HasVersionUpdate)
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
            this.RaisePropertyChanged(nameof(SurfaceViewToggleCompact));
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
            this.RaisePropertyChanged(nameof(ShowPreviewEmptyWorkspace));
            this.RaisePropertyChanged(nameof(ShowPreviewActiveWorkspace));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewFooterNote));
            this.RaisePropertyChanged(nameof(PreviewQueuedCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadMetricTitle));
            this.RaisePropertyChanged(nameof(ShowPreviewQueuedMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewQueuedDownloadMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewDownloadCountMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewDependencyMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewAutoRemovalMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewConflictMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewAttentionMetric));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBackground));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBorderBrush));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBackground));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonBorderBrush));
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonLabel));
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
            this.RaisePropertyChanged(nameof(ShowSelectedModActionRow));
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
            RefreshPreviewConflictPopupState();
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
            this.RaisePropertyChanged(nameof(ShowPreviewEmptyWorkspace));
            this.RaisePropertyChanged(nameof(ShowPreviewActiveWorkspace));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewFooterNote));
            this.RaisePropertyChanged(nameof(PreviewQueuedCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadQueueCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDownloadMetricTitle));
            this.RaisePropertyChanged(nameof(PreviewDownloadCountLabel));
            this.RaisePropertyChanged(nameof(PreviewDependencyCountLabel));
            this.RaisePropertyChanged(nameof(ShowPreviewQueuedMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewQueuedDownloadMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewDownloadCountMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewDependencyMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewAutoRemovalMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewConflictMetric));
            this.RaisePropertyChanged(nameof(ShowPreviewAttentionMetric));
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
            this.RaisePropertyChanged(nameof(DownloadQueuedButtonLabel));
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
                ApplyResultKind.Success => ("#14191F", "#3D7A57"),
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
            HideExecutionResultDialog();
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
            this.RaisePropertyChanged(nameof(SurfaceViewToggleCompact));
            this.RaisePropertyChanged(nameof(HasApplyResultSummaryLines));
            this.RaisePropertyChanged(nameof(HasApplyResultFollowUpLines));
            this.RaisePropertyChanged(nameof(ShowInlineApplyResult));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBackground));
            this.RaisePropertyChanged(nameof(PreviewSurfaceButtonBorderBrush));
            this.RaisePropertyChanged(nameof(ShowEmptyQueueStub));
            this.RaisePropertyChanged(nameof(ShowCollapsedApplyResultStub));
            this.RaisePropertyChanged(nameof(ShowExpandedQueuePanel));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubTitle));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubSummary));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBackground));
            this.RaisePropertyChanged(nameof(CollapsedQueueStubBorderBrush));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewFooterNote));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
        }

        private void PublishExecutionOverlayState()
        {
            this.RaisePropertyChanged(nameof(ShowExecutionOverlay));
            this.RaisePropertyChanged(nameof(ShowExecutionProgressOverlay));
            this.RaisePropertyChanged(nameof(ShowExecutionResultOverlay));
            this.RaisePropertyChanged(nameof(ShowInlineApplyResult));
            this.RaisePropertyChanged(nameof(ShowPreviewConflictPopup));
            this.RaisePropertyChanged(nameof(ShowReadyStatusSurface));
            this.RaisePropertyChanged(nameof(ShowEmptyQueueStub));
            this.RaisePropertyChanged(nameof(ShowCollapsedApplyResultStub));
            this.RaisePropertyChanged(nameof(ShowExpandedQueuePanel));
            this.RaisePropertyChanged(nameof(ShowPreviewEmptyWorkspace));
            this.RaisePropertyChanged(nameof(ShowPreviewActiveWorkspace));
            this.RaisePropertyChanged(nameof(PreviewStatusLabel));
            this.RaisePropertyChanged(nameof(PreviewOutcomeTitle));
            this.RaisePropertyChanged(nameof(PreviewPanelGuidance));
            this.RaisePropertyChanged(nameof(PreviewFooterNote));
            this.RaisePropertyChanged(nameof(PreviewImpactSummary));
            this.RaisePropertyChanged(nameof(ExecutionDialogTitle));
            this.RaisePropertyChanged(nameof(ExecutionDialogMessage));
            this.RaisePropertyChanged(nameof(HasExecutionProgressValue));
            this.RaisePropertyChanged(nameof(IsExecutionProgressIndeterminate));
            this.RaisePropertyChanged(nameof(ExecutionProgressValue));
            this.RaisePropertyChanged(nameof(ExecutionResultAcknowledgeLabel));
        }

        private void ResetPreviewState()
        {
            IsPreviewLoading = false;
            PreviewSummary = "Queue installs, updates, removals, or downloads from Browse to review them before running anything.";
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

        private static void ReplaceSelectedModCollection(ObservableCollection<ModRelationshipItem> target,
                                                         IEnumerable<ModRelationshipItem>         values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private void ReplaceSelectedModResourceLinks(IEnumerable<ModResourceLinkItem> values)
        {
            SelectedModResourceLinks.Clear();
            foreach (var value in values)
            {
                SelectedModResourceLinks.Add(value);
            }

            this.RaisePropertyChanged(nameof(ShowSelectedModResourceLinks));
        }

        private static IReadOnlyList<ModResourceLinkItem> BuildSelectedModResourceLinks(ResourcesDescriptor? resources)
        {
            if (resources == null)
            {
                return Array.Empty<ModResourceLinkItem>();
            }

            var links = new List<ModResourceLinkItem>();

            AddResourceLink(links, "Home page", resources.homepage);
            AddResourceLink(links, "Repository", resources.repository);
            AddResourceLink(links, "Bug tracker", resources.bugtracker);
            AddResourceLink(links, "SpaceDock", resources.spacedock);
            AddResourceLink(links, "Discussions", resources.discussions);
            AddResourceLink(links, "Manual", resources.manual);
            AddResourceLink(links, "License", resources.license);
            AddResourceLink(links, "Curse", resources.curse);
            AddResourceLink(links, "CI", resources.ci);
            AddResourceLink(links, "Metanetkan", resources.metanetkan);
            AddResourceLink(links, "Remote version info", resources.remoteSWInfo);
            AddResourceLink(links, "Remote version file", resources.remoteAvc);
            AddResourceLink(links, "Store", resources.store);
            AddResourceLink(links, "Steam", resources.steamstore);
            AddResourceLink(links, "GOG", resources.gogstore);
            AddResourceLink(links, "Epic", resources.epicstore);

            return links;
        }

        private static void AddResourceLink(ICollection<ModResourceLinkItem> target,
                                            string                           label,
                                            Uri?                             url)
        {
            if (url == null)
            {
                return;
            }

            target.Add(new ModResourceLinkItem
            {
                Label = label,
                Url   = url.ToString(),
            });
        }

        private void PopulateSelectedModVersionChoices(ModDetailsModel details)
        {
            SelectedModAvailableVersions.Clear();
            foreach (var choice in BuildSelectedModVersionChoices(details.Identifier))
            {
                SelectedModAvailableVersions.Add(choice);
            }

            var preferredChoice = SelectedModAvailableVersions.FirstOrDefault(choice =>
                                      choice.VersionText == details.LatestVersion)
                                  ?? SelectedModAvailableVersions.FirstOrDefault(choice =>
                                      choice.IsInstalledVersion && !details.HasVersionUpdate)
                                  ?? SelectedModAvailableVersions.FirstOrDefault();

            SelectedModVersionChoice = preferredChoice;
            this.RaisePropertyChanged(nameof(ShowSelectedModVersionPicker));
        }

        private IReadOnlyList<ModVersionChoiceItem> BuildSelectedModVersionChoices(string identifier)
        {
            if (CurrentRegistry == null || CurrentInstance == null)
            {
                return Array.Empty<ModVersionChoiceItem>();
            }

            var installedVersion = CurrentRegistry.InstalledModule(identifier)?.Module.version;
            var modules = Enumerable.Repeat(CurrentRegistry.InstalledModule(identifier)?.Module, 1)
                                    .Concat(Utilities.DefaultIfThrows(
                                                () => CurrentRegistry.AvailableByIdentifier(identifier))
                                            ?? Enumerable.Empty<CkanModule>())
                                    .OfType<CkanModule>()
                                    .Distinct()
                                    .OrderByDescending(module => module.version)
                                    .ToList();

            return modules.Select(module =>
                          {
                              var isCompatible = IsModuleInstallable(module,
                                                                     CurrentRegistry,
                                                                     CurrentInstance);
                              var comparison = installedVersion == null
                                  ? 0
                                  : module.version.CompareTo(installedVersion);
                              var badgeText = installedVersion?.Equals(module.version) == true
                                  ? "Installed"
                                  : "";
                              var badgeForeground = installedVersion?.Equals(module.version) == true
                                  ? "#8EC7F3"
                                  : "#AEB8C6";

                              return new ModVersionChoiceItem
                              {
                                  VersionText = module.version.ToString(),
                                  CompatibilityText = BuildVersionCompatibilityLabel(module, CurrentInstance),
                                  ReleaseDateText = module.release_date?.ToLocalTime().ToString("M/d/yyyy") ?? "Unknown",
                                  BadgeText = badgeText,
                                  BadgeForeground = badgeForeground,
                                  IsInstalledVersion = installedVersion?.Equals(module.version) == true,
                                  IsCompatible = isCompatible,
                                  VersionComparisonToInstalled = comparison,
                                  Module = module,
                              };
                          })
                          .ToList();
        }

        private void ApplySelectedVersionDetails()
        {
            if (selectedModDetails == null)
            {
                return;
            }

            if (SelectedModVersionChoice?.Module is not CkanModule module
                || CurrentInstance == null
                || CurrentRegistry == null)
            {
                SelectedModCompatibility = selectedModDetails.Compatibility;
                SelectedModCacheState = selectedModDetails.IsCached ? "Cached" : "Not cached";
                SelectedModModuleKind = selectedModDetails.ModuleKind;
                SelectedModLicense = selectedModDetails.License;
                SelectedModReleaseDate = selectedModDetails.ReleaseDate;
                SelectedModDownloadSize = selectedModDetails.DownloadSize;
                SelectedModRelationships = $"{selectedModDetails.DependencyCount} depends • {selectedModDetails.RecommendationCount} recommends • {selectedModDetails.SuggestionCount} suggests";
                SelectedModDependencyCountLabel = CountLabel(selectedModDetails.DependencyCount, "Dependency", "Dependencies");
                SelectedModRecommendationCountLabel = CountLabel(selectedModDetails.RecommendationCount, "Recommendation", "Recommendations");
                SelectedModSuggestionCountLabel = CountLabel(selectedModDetails.SuggestionCount, "Suggestion", "Suggestions");
                ReplaceSelectedModCollection(SelectedModDependencies, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModRecommendations, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModSuggestions, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModResourceLinks(BuildSelectedModResourceLinks(selectedModDetails.Resources));
                SelectedModIsCached = selectedModDetails.IsCached;
                SelectedModIsIncompatible = selectedModDetails.IsIncompatible;
                UpdateSelectedModCachedArchivePath();
                PublishSelectedModRelationshipState();
                PublishSelectedModActionState();
                return;
            }

            SelectedModCompatibility = BuildVersionCompatibilityLabel(module, CurrentInstance);
            SelectedModCacheState = CurrentCache?.IsMaybeCachedZip(module) == true ? "Cached" : "Not cached";
            SelectedModModuleKind = FormatModuleKind(module.kind);
            SelectedModLicense = FormatLicense(module);
            SelectedModReleaseDate = module.release_date?.ToString("yyyy-MM-dd") ?? "Unknown";
            SelectedModDownloadSize = module.download_size > 0
                ? CkanModule.FmtSize(module.download_size)
                : "Unknown";

            var dependencies = BuildRelationshipEntries(module.depends);
            var recommendations = BuildRelationshipEntries(module.recommends);
            var suggestions = BuildRelationshipEntries(module.suggests);

            ReplaceSelectedModCollection(SelectedModDependencies, dependencies);
            ReplaceSelectedModCollection(SelectedModRecommendations, recommendations);
            ReplaceSelectedModCollection(SelectedModSuggestions, suggestions);
            ReplaceSelectedModResourceLinks(BuildSelectedModResourceLinks(module.resources ?? selectedModDetails.Resources));

            SelectedModRelationships = $"{dependencies.Count} depends • {recommendations.Count} recommends • {suggestions.Count} suggests";
            SelectedModDependencyCountLabel = CountLabel(dependencies.Count, "Dependency", "Dependencies");
            SelectedModRecommendationCountLabel = CountLabel(recommendations.Count, "Recommendation", "Recommendations");
            SelectedModSuggestionCountLabel = CountLabel(suggestions.Count, "Suggestion", "Suggestions");
            SelectedModIsCached = CurrentCache?.IsMaybeCachedZip(module) == true;
            SelectedModIsIncompatible = !IsModuleInstallable(module,
                                                             CurrentRegistry,
                                                             CurrentInstance);
            UpdateSelectedModCachedArchivePath();
            PublishSelectedModRelationshipState();
            PublishSelectedModActionState();
        }

        private void UpdateSelectedModCachedArchivePath()
        {
            selectedModCachedArchivePath = ResolveSelectedModCachedArchivePath();
            this.RaisePropertyChanged(nameof(ShowOpenSelectedModCacheLocationAction));
        }

        private string? ResolveSelectedModCachedArchivePath()
        {
            if (SelectedMod == null || CurrentCache == null)
            {
                return null;
            }

            if (SelectedModVersionChoice?.Module is CkanModule selectedModule
                && CurrentCache.GetCachedFilename(selectedModule) is string selectedPath)
            {
                return selectedPath;
            }

            if (CurrentRegistry == null)
            {
                return null;
            }

            return Enumerable.Repeat(CurrentRegistry.InstalledModule(SelectedMod.Identifier)?.Module, 1)
                             .Concat(Utilities.DefaultIfThrows(
                                         () => CurrentRegistry.AvailableByIdentifier(SelectedMod.Identifier))
                                     ?? Enumerable.Empty<CkanModule>())
                             .OfType<CkanModule>()
                             .Distinct()
                             .Select(CurrentCache.GetCachedFilename)
                             .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }

        private List<ModRelationshipItem> BuildRelationshipEntries(IEnumerable<RelationshipDescriptor>? relationships)
            => (relationships ?? Enumerable.Empty<RelationshipDescriptor>())
                .Select(relationship => new ModRelationshipItem
                {
                    Text = CurrentRegistry != null && CurrentInstance != null
                        ? relationship.ToStringWithCompat(CurrentRegistry, CurrentInstance.Game)
                        : relationship.ToString() ?? "",
                    Identifiers = RelationshipIdentifiers(relationship).ToList(),
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .ToList();

        private static IEnumerable<string> RelationshipIdentifiers(RelationshipDescriptor relationship)
            => relationship switch
            {
                ModuleRelationshipDescriptor moduleRelationship
                    => Enumerable.Repeat(moduleRelationship.name, 1),
                AnyOfRelationshipDescriptor anyOfRelationship
                    => anyOfRelationship.any_of?.SelectMany(RelationshipIdentifiers)
                       ?? Enumerable.Empty<string>(),
                _ => Enumerable.Empty<string>(),
            };

        private void ShowRelationshipsInBrowser(string relationshipName,
                                                IEnumerable<ModRelationshipItem> relationships)
        {
            var identifiers = relationships.SelectMany(item => item.Identifiers)
                                           .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                                           .Distinct(StringComparer.OrdinalIgnoreCase)
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (identifiers.Count == 0)
            {
                StatusMessage = $"No browser-visible {relationshipName} were found for {SelectedModTitle}.";
                return;
            }

            relationshipBrowserScopeIdentifiers = identifiers;
            RelationshipBrowserScopeText = $"{relationshipName} for {SelectedModTitle}";
            pendingModListScrollReset = true;
            ApplyCatalogFilterToLoadedItems(identifiers.FirstOrDefault());
            PublishRelationshipBrowserScopeState();
        }

        private IReadOnlySet<string> ConflictBrowserIdentifiers(string leftSide,
                                                                string rightSide)
        {
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddConflictBrowserIdentifier(identifiers, leftSide);
            AddConflictBrowserIdentifier(identifiers, rightSide);
            return identifiers;
        }

        private void AddConflictBrowserIdentifier(HashSet<string> identifiers,
                                                  string          side)
        {
            if (string.IsNullOrWhiteSpace(side))
            {
                return;
            }

            var matchingItem = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ConflictSideStartsWith(side, item.Identifier)
                                     || ConflictSideStartsWith(side, item.Name));
            if (matchingItem != null)
            {
                identifiers.Add(matchingItem.Identifier);
                return;
            }

            var displayName = StripConflictVersionSuffix(side);
            matchingItem = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ContainsText(item.Identifier, displayName)
                                     || ContainsText(item.Name, displayName)
                                     || ContainsText(displayName, item.Identifier)
                                     || ContainsText(displayName, item.Name));
            if (matchingItem != null)
            {
                identifiers.Add(matchingItem.Identifier);
            }
        }

        private void ClearRelationshipBrowserScope()
        {
            if (!ShowRelationshipBrowserScope)
            {
                return;
            }

            relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RelationshipBrowserScopeText = "";
            pendingModListScrollReset = true;
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems();
            }
            PublishRelationshipBrowserScopeState();
        }

        private async Task ReturnToPreviewAfterConflictQueueChangeAsync()
        {
            relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RelationshipBrowserScopeText = "";
            ShowPreviewSurface = true;
            IsPreviewLoading = true;
            PublishRelationshipBrowserScopeState();
            PublishFilterStateLabels();
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems();
            }
            await Task.Delay(180);
            await LoadPreviewAsync();
        }

        private static bool IsModuleInstallable(CkanModule module,
                                                IRegistryQuerier registry,
                                                GameInstance instance)
        {
            var versionCriteria = instance.VersionCriteria();
            if (!module.IsCompatible(versionCriteria))
            {
                return false;
            }

            var stabilityTolerance = instance.StabilityToleranceConfig.ModStabilityTolerance(module.identifier)
                                     ?? instance.StabilityToleranceConfig.OverallStabilityTolerance;
            if ((module.release_status ?? ReleaseStatus.stable) > stabilityTolerance)
            {
                return false;
            }

            try
            {
                return registry.IdentifierCompatible(module.identifier,
                                                     instance.StabilityToleranceConfig,
                                                     versionCriteria);
            }
            catch
            {
                return false;
            }
        }

        private void PublishSelectedModRelationshipState()
        {
            this.RaisePropertyChanged(nameof(ShowSelectedModVersionPicker));
            this.RaisePropertyChanged(nameof(SelectedModVersionPickerLabel));
            this.RaisePropertyChanged(nameof(SelectedModSelectedVersionMatchesInstalled));
            this.RaisePropertyChanged(nameof(SelectedModSelectedVersionIsCompatible));
            this.RaisePropertyChanged(nameof(HasSelectedModDependencies));
            this.RaisePropertyChanged(nameof(HasSelectedModRecommendations));
            this.RaisePropertyChanged(nameof(HasSelectedModSuggestions));
            this.RaisePropertyChanged(nameof(ShowSelectedModResourceLinks));
            this.RaisePropertyChanged(nameof(ShowSelectedModDependenciesExpanded));
            this.RaisePropertyChanged(nameof(ShowSelectedModRecommendationsExpanded));
            this.RaisePropertyChanged(nameof(ShowSelectedModSuggestionsExpanded));
            this.RaisePropertyChanged(nameof(SelectedModDependencyChevron));
            this.RaisePropertyChanged(nameof(SelectedModRecommendationChevron));
            this.RaisePropertyChanged(nameof(SelectedModSuggestionChevron));
        }

        private void PublishRelationshipBrowserScopeState()
        {
            this.RaisePropertyChanged(nameof(ShowRelationshipBrowserScope));
            this.RaisePropertyChanged(nameof(RelationshipBrowserScopeText));
            this.RaisePropertyChanged(nameof(ModCountLabel));
        }

        private void ToggleSelectedModDependenciesExpanded()
        {
            if (HasSelectedModDependencies)
            {
                ShowSelectedModDependenciesExpanded = !ShowSelectedModDependenciesExpanded;
                this.RaisePropertyChanged(nameof(SelectedModDependencyChevron));
            }
        }

        private void ToggleSelectedModRecommendationsExpanded()
        {
            if (HasSelectedModRecommendations)
            {
                ShowSelectedModRecommendationsExpanded = !ShowSelectedModRecommendationsExpanded;
                this.RaisePropertyChanged(nameof(SelectedModRecommendationChevron));
            }
        }

        private void ToggleSelectedModSuggestionsExpanded()
        {
            if (HasSelectedModSuggestions)
            {
                ShowSelectedModSuggestionsExpanded = !ShowSelectedModSuggestionsExpanded;
                this.RaisePropertyChanged(nameof(SelectedModSuggestionChevron));
            }
        }

        private static string BuildVersionCompatibilityLabel(CkanModule module,
                                                             GameInstance instance)
        {
            var latest = module.LatestCompatibleGameVersion();
            if (latest.IsAny)
            {
                return "Any";
            }
            return FormatDisplayedCompatibilityVersion(latest);
        }

        private static string BuildSelectedModVersions(ModDetailsModel details)
            => details.IsAutodetected
                ? $"Latest {details.LatestVersion}\nInstalled version unknown"
                : details.IsInstalled
                    ? $"Latest {details.LatestVersion}\nInstalled {details.InstalledVersion}"
                    : $"Latest {details.LatestVersion}";

        private static string FormatDisplayedCompatibilityVersion(GameVersion? version)
        {
            if (version == null || version.IsAny)
            {
                return "Unknown";
            }

            var normalized = version.WithoutBuild;
            if (normalized.IsPatchDefined && normalized.Patch == 99)
            {
                normalized = new GameVersion(normalized.Major, normalized.Minor);
            }

            return normalized.ToString() ?? "Unknown";
        }

        private static string FormatModuleKind(ModuleKind kind)
            => kind switch
            {
                ModuleKind.metapackage => "Metapackage",
                ModuleKind.dlc => "DLC",
                _ => "Package",
            };

        private static string FormatLicense(CkanModule module)
            => module.license?.Count > 0 == true
                ? string.Join(", ", module.license)
                : "Unknown";

        private static string BuildInstallState(ModDetailsModel details)
        {
            var parts = new List<string>();

            if (details.IsAutodetected)
            {
                parts.Add("Managed outside CKAN");
            }
            else if (details.IsInstalled)
            {
                parts.Add($"Installed {details.InstalledVersion}");
            }
            else if (!details.IsIncompatible)
            {
                parts.Add("Not installed");
            }

            if (details.HasVersionUpdate)
            {
                parts.Add($"Update available to {details.LatestVersion}");
            }
            if (details.IsIncompatible && !details.IsAutodetected)
            {
                parts.Add("Currently incompatible");
            }
            if (details.HasReplacement)
            {
                parts.Add("Replacement available");
            }

            return string.Join(" • ", parts);
        }

        private void PruneQueuedAutodetectedRemovals(IReadOnlyList<ModListItem>? catalogItems = null)
        {
            var autodetectedIdentifiers = (catalogItems ?? Mods)
                                          .Where(mod => mod.IsAutodetected)
                                          .Select(mod => mod.Identifier)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (autodetectedIdentifiers.Count == 0)
            {
                return;
            }

            var invalidRemovals = changesetService.CurrentApplyQueue
                                                  .Where(action => action.ActionKind == QueuedActionKind.Remove
                                                                   && autodetectedIdentifiers.Contains(action.Identifier))
                                                  .ToList();
            if (invalidRemovals.Count == 0)
            {
                return;
            }

            foreach (var action in invalidRemovals)
            {
                changesetService.Remove(action.Identifier);
            }

            StatusMessage = invalidRemovals.Count == 1
                ? $"Removed queued removal for {invalidRemovals[0].Name}. It was detected outside CKAN and must be removed manually from GameData."
                : $"Removed {invalidRemovals.Count} queued removals for mods detected outside CKAN. They must be removed manually from GameData.";
        }

        private void PruneQueuedAutodetectedDownloads(IReadOnlyList<ModListItem>? catalogItems = null)
        {
            var autodetectedIdentifiers = (catalogItems ?? Mods)
                                          .Where(mod => mod.IsAutodetected)
                                          .Select(mod => mod.Identifier)
                                          .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (autodetectedIdentifiers.Count == 0)
            {
                return;
            }

            var invalidDownloads = changesetService.CurrentDownloadQueue
                                                   .Where(action => autodetectedIdentifiers.Contains(action.Identifier))
                                                   .ToList();
            if (invalidDownloads.Count == 0)
            {
                return;
            }

            foreach (var action in invalidDownloads)
            {
                changesetService.Remove(action.Identifier);
            }

            StatusMessage = invalidDownloads.Count == 1
                ? $"Removed queued cache action for {invalidDownloads[0].Name}. It is managed outside CKAN and cannot be added to the cache."
                : $"Removed {invalidDownloads.Count} queued cache actions for mods managed outside CKAN. External mods cannot be added to the cache.";
        }

        private bool IsCurrentSelectedModRequest(string identifier,
                                                 int    requestId)
            => requestId == selectedModLoadRequestId
               && string.Equals(identifier, SelectedMod?.Identifier, StringComparison.OrdinalIgnoreCase);

        private bool MessageContains(string value)
            => StatusMessage?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool CanQueueInstall(ModListItem mod)
            => !mod.IsInstalled && !mod.IsIncompatible;

        private static bool CanQueueUpdate(ModListItem mod)
            => mod.IsInstalled && mod.HasVersionUpdate;

        private static bool CanQueueRemove(ModListItem mod)
            => mod.IsInstalled && !mod.IsAutodetected && !mod.HasVersionUpdate;

        private static string CountLabel(int count,
                                         string singular,
                                         string plural)
            => count == 1
                ? $"1 {singular}"
                : $"{count} {plural}";
    }
}
