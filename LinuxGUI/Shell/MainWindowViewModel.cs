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
    public sealed partial class MainWindowViewModel : ReactiveObject
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
        private const int    DevQueueSmokePreviewConflictCount = 8;
        private const int    DevQueueSmokePreviewAutoRemovalCount = 6;
        private const int    CatalogLoadSettleDelayMs = 75;
        private const int    CatalogLoadingIndicatorDelayMs = 175;
        private const int    RecentCatalogReloadSuppressionMs = 500;

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
        private DateTime lastCatalogLoadCompletedUtc = DateTime.MinValue;
        private bool    pendingModListScrollReset;
        private bool    preserveSelectedModDuringSortReorder;
        private bool    suppressFilterAutoRefresh;
        private bool    hasRestoredQueuedActionSnapshot;
        private bool    hasSeededDevQueueSmoke;
        private bool    suppressQueueSnapshotPersistence;
        private bool    suppressQueueChangedRefresh;
        private bool    pendingQueueChangedRefresh;
        private bool    hasRunStartupRepositoryRefresh;
        private bool    previewConflictPopupDismissed;
        private bool    lastSolverPreviewCanApply;
        private string  dismissedPreviewConflictKey = "";
        private int     selectedPreviewConflictCount;
        private string? selectedPreviewConflict;
        private Func<IReadOnlyList<RecommendationAuditItem>, Task<IReadOnlyList<RecommendationAuditItem>?>>?
            recommendationSelectionPromptAsync;
        private readonly HashSet<string> selectedPreviewConflicts = new(StringComparer.Ordinal);
        private IReadOnlyList<QueuedActionModel> lastRemovedQueuedActions = Array.Empty<QueuedActionModel>();
        private IReadOnlyList<ModListItem> allCatalogItems = Array.Empty<ModListItem>();
        private IReadOnlyList<CatalogSkeletonRow> catalogSkeletonRows = Array.Empty<CatalogSkeletonRow>();
        private IReadOnlySet<string> relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, string> relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool relationshipBrowserScopeReturnsToPreview;
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
            PreviewSupporters = new ObservableCollection<string>();
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
            ApplyStoredFilterState(modSearchService.Current);

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
            var canQueueRemoveAllInstalled = this.WhenAnyValue(vm => vm.HasCurrentInstance,
                                                               vm => vm.IsApplyingChanges,
                                                               vm => vm.IsCatalogLoading,
                                                               (hasCurrent, applying, loading)
                                                                   => hasCurrent && !applying && !loading);
            var canUndoQueuedActionRemoval = this.WhenAnyValue(vm => vm.HasQueuedActionUndo,
                                                               vm => vm.IsApplyingChanges,
                                                               (hasUndo, applying) => hasUndo && !applying);
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
            ClearQueueCommand = ReactiveCommand.CreateFromTask(ClearQueuedActionsAsync, canClearQueue);
            QueueRemoveAllInstalledModsCommand = ReactiveCommand.CreateFromTask(QueueRemoveAllInstalledModsAsync,
                                                                                canQueueRemoveAllInstalled);
            CleanupMissingInstalledModsCommand = ReactiveCommand.CreateFromTask(CleanupMissingInstalledModsAsync,
                                                                                canQueueRemoveAllInstalled);
            UndoQueuedActionRemovalCommand = ReactiveCommand.Create(UndoQueuedActionRemoval, canUndoQueuedActionRemoval);
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
            ApplyChangesCommand = ReactiveCommand.CreateFromTask(ContinuePreviewApplyFlowAsync,
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
            ViewPreviewDependenciesInBrowserCommand = ReactiveCommand.Create(() => ShowPreviewEntriesInBrowser("dependencies", PreviewDependencies));
            ViewPreviewRecommendationsInBrowserCommand = ReactiveCommand.Create(() => ShowPreviewEntriesInBrowser("recommendations", PreviewRecommendations));
            ViewPreviewSuggestionsInBrowserCommand = ReactiveCommand.Create(() => ShowPreviewEntriesInBrowser("suggestions", PreviewSuggestions));
            ViewPreviewSupportersInBrowserCommand = ReactiveCommand.Create(() => ShowPreviewEntriesInBrowser("supporters", PreviewSupporters));
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

        public ObservableCollection<string> PreviewSupporters { get; }

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

        public ReactiveCommand<Unit, Unit> QueueRemoveAllInstalledModsCommand { get; }

        public ReactiveCommand<Unit, Unit> CleanupMissingInstalledModsCommand { get; }

        public ReactiveCommand<Unit, Unit> UndoQueuedActionRemovalCommand { get; }

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

        public ReactiveCommand<Unit, Unit> ViewPreviewDependenciesInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewPreviewRecommendationsInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewPreviewSuggestionsInBrowserCommand { get; }

        public ReactiveCommand<Unit, Unit> ViewPreviewSupportersInBrowserCommand { get; }

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

        public string RelationshipBrowserScopeBackground
            => ShowConflictBrowserScope ? "#24121A" : "#1A2530";

        public string RelationshipBrowserScopeBorderBrush
            => ShowConflictBrowserScope ? "#D95A72" : "#31516C";

        public string RelationshipBrowserScopeFrameBorderBrush
            => ShowConflictBrowserScope ? "#7D3446" : "#2C455B";

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
            RecommendationSelectionPromptAsync
        {
            get => recommendationSelectionPromptAsync;
            set
            {
                if (recommendationSelectionPromptAsync == value)
                {
                    return;
                }

                recommendationSelectionPromptAsync = value;
                PublishPreviewActionStateLabels();
            }
        }

        public Func<string, Task<bool>>? ConfirmIncompatibleLaunchAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmClearQueueAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmQueueRemoveAllInstalledModsAsync { get; set; }

        public Func<string, Task<bool>>? ConfirmCleanupMissingInstalledModsAsync { get; set; }

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

        public bool HasQueuedActionUndo => lastRemovedQueuedActions.Count > 0;

        public bool ShowQueueFooterActions => HasQueuedActions || HasQueuedActionUndo;

        public int QueuedChangeActionCount
            => QueuedActions.Count(action => action.ActionKind != QueuedActionKind.Download);

        public int QueuedDownloadActionCount
            => QueuedActions.Count(action => action.ActionKind == QueuedActionKind.Download);

        public int QueuedRecommendationActionCount
            => QueuedActions.Count(action => action.SourceText.StartsWith("Recommended", StringComparison.OrdinalIgnoreCase));

        public int QueuedSuggestionActionCount
            => QueuedActions.Count(action => action.SourceText.StartsWith("Suggested", StringComparison.OrdinalIgnoreCase));

        public int QueuedSupporterActionCount
            => QueuedActions.Count(action => action.SourceText.StartsWith("Supported", StringComparison.OrdinalIgnoreCase));

        public bool HasQueuedRecommendationActions => QueuedRecommendationActionCount > 0;

        public bool HasQueuedSuggestionActions => QueuedSuggestionActionCount > 0;

        public bool HasQueuedSupporterActions => QueuedSupporterActionCount > 0;

        public string QueuedRecommendationCountLabel
            => CountLabel(QueuedRecommendationActionCount, "queued", "queued");

        public string QueuedSuggestionCountLabel
            => CountLabel(QueuedSuggestionActionCount, "queued", "queued");

        public string QueuedSupporterCountLabel
            => CountLabel(QueuedSupporterActionCount, "queued", "queued");

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

        public bool HasPreviewSupporters => PreviewSupporters.Count > 0;

        public bool HasPreviewRecommendationsOrSuggestions
            => HasPreviewRecommendations || HasPreviewSuggestions;

        public bool HasPreviewOptionalExtras
            => HasPreviewRecommendations || HasPreviewSuggestions || HasPreviewSupporters;

        public bool PreviewExtrasSelectionAvailable
            => HasQueuedChangeActions
               && PreviewCanApply
               && !IsPreviewLoading
               && !IsApplyingChanges
               && HasPreviewOptionalExtras;

        public bool ShowPreviewExtrasActionNotice => PreviewExtrasSelectionAvailable;

        public string PreviewExtrasActionNotice
            => "Optional extras are listed in Preview. Use each section's View button to inspect and queue extras. When Browse opens, click Close in the notice above the mod list to return to Preview. Required dependencies are automatic.";

        public bool HasPreviewDependenciesOrOptional
            => HasPreviewDependencies || HasPreviewOptionalExtras;

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
            => false;

        public bool ShowPreviewActiveWorkspace
            => !ShowExecutionOverlay;

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
                this.RaisePropertyChanged(nameof(ApplyChangesButtonLabel));
                this.RaisePropertyChanged(nameof(PreviewExtrasSelectionAvailable));
                this.RaisePropertyChanged(nameof(ShowPreviewExtrasActionNotice));
                this.RaisePropertyChanged(nameof(PreviewExtrasActionNotice));
            }
        }

        public bool PreviewCanApply
        {
            get => previewCanApply;
            private set
            {
                if (this.RaiseAndSetIfChanged(ref previewCanApply, value))
                {
                    PublishPreviewActionStateLabels();
                }
            }
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
                PublishPreviewActionStateLabels();
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
                    if (HasPreviewDependencies && HasPreviewAutoRemovals)
                    {
                        return "Direct actions are shown on the left. CKAN will install required dependencies and remove unused auto-installed dependencies during Apply.";
                    }

                    if (HasPreviewDependencies)
                    {
                        return "Direct actions are shown on the left. Required dependency installs are handled automatically during Apply.";
                    }

                    return HasPreviewAutoRemovals
                        ? "Direct actions are shown on the left. Unused auto-installed dependencies listed below will be removed during Apply."
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
                                : HasPreviewAutoRemovals
                                    ? "Apply changes will update GameData and remove unused auto-installed dependencies."
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
                    ? HasPreviewDependencies && HasPreviewAutoRemovals
                        ? "These are the direct install/update/remove actions you selected. CKAN will also install required mods and remove unused auto-installed dependencies listed below."
                        : HasPreviewDependencies
                            ? "These are the direct install/update/remove actions you selected. CKAN will also install the required mods listed below."
                            : HasPreviewAutoRemovals
                                ? "These are the direct install/update/remove actions you selected. CKAN will also remove unused auto-installed dependencies listed below."
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

        public string ApplyChangesButtonLabel
            => "Apply Changes";

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





    }
}
