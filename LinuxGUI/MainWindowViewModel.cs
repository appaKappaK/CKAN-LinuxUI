using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI
{
    public sealed class MainWindowViewModel : ReactiveObject
    {
        private readonly IGameInstanceService gameInstanceService;
        private readonly IModCatalogService   modCatalogService;
        private readonly AvaloniaUser         user;
        private readonly ObservableAsPropertyHelper<bool> canUseSelectedInstance;

        private string? currentInstanceName = "Loading…";
        private string  statusMessage       = "Preparing the Linux shell…";
        private string  diagnostics         = "No diagnostic messages yet.";
        private string  selectedInstanceSummary = "Choose an instance to inspect its details.";
        private string  stageTitle          = "Preparing CKAN Linux";
        private string  stageDescription    = "Loading known game instances and startup state.";
        private string  selectedActionLabel = "Use Selected Instance";
        private string  selectedActionHint  = "Choose the instance that should drive the new shell.";
        private string  modSearchText       = "";
        private string  catalogStatusMessage = "Select an instance to load the mod catalog.";
        private string  selectedModTitle     = "No mod selected";
        private string  selectedModSubtitle  = "Choose a mod to inspect its details.";
        private string  selectedModAuthors   = "";
        private string  selectedModVersions  = "";
        private string  selectedModCompatibility = "";
        private string  selectedModModuleKind = "";
        private string  selectedModLicense = "";
        private string  selectedModReleaseDate = "";
        private string  selectedModDownloadSize = "";
        private string  selectedModRelationships = "";
        private string  selectedModBody      = "The details pane will show summary, description, compatibility, and install state.";
        private int     progressPercent;
        private int     instanceCount;
        private bool    filterInstalledOnly;
        private bool    filterNotInstalledOnly;
        private bool    filterUpdatableOnly;
        private bool    filterCachedOnly;
        private bool    filterIncompatibleOnly;
        private bool    isRefreshing;
        private bool    hasSelectedInstance;
        private bool    isCatalogLoading;
        private bool    selectedModIsInstalled;
        private bool    selectedModHasUpdate;
        private bool    selectedModIsCached;
        private bool    selectedModIsIncompatible;
        private bool    selectedModHasReplacement;
        private InstanceSummary? selectedInstance;
        private ModListItem?     selectedMod;
        private StartupStage     startupStage = StartupStage.Loading;

        public MainWindowViewModel(IGameInstanceService gameInstanceService,
                                   IModCatalogService   modCatalogService,
                                   AvaloniaUser         user)
        {
            this.gameInstanceService = gameInstanceService;
            this.modCatalogService   = modCatalogService;
            this.user                = user;

            Instances = new ObservableCollection<InstanceSummary>();
            Mods = new ObservableCollection<ModListItem>();

            var canRefresh = this.WhenAnyValue(vm => vm.IsRefreshing)
                                 .Select(refreshing => !refreshing);
            var canUseSelected = this.WhenAnyValue(vm => vm.SelectedInstance,
                                                   vm => vm.IsRefreshing,
                                                   (inst, refreshing) => inst != null && !refreshing);

            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
            SetCurrentInstanceCommand = ReactiveCommand.CreateFromTask(
                SetCurrentInstanceAsync,
                canUseSelected);
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
                    if (busy)
                    {
                        StatusMessage = string.IsNullOrWhiteSpace(user.LastMessage)
                            ? "Working…"
                            : user.LastMessage;
                    }
                });

            this.WhenAnyValue(vm => vm.ModSearchText,
                              vm => vm.FilterInstalledOnly,
                              vm => vm.FilterNotInstalledOnly,
                              vm => vm.FilterUpdatableOnly,
                              vm => vm.FilterCachedOnly,
                              vm => vm.FilterIncompatibleOnly)
                .Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(200), RxApp.MainThreadScheduler)
                .Subscribe(_filters =>
                {
                    if (IsReady)
                    {
                        _ = LoadModCatalogAsync();
                    }
                });

            gameInstanceService.CurrentInstanceChanged += OnCurrentInstanceChanged;

            Footer = "Stage 2: the browser now exposes real CKAN data with stronger detail hierarchy, clearer status badges, and visible filters.";
            _ = RefreshAsync();
        }

        public ObservableCollection<InstanceSummary> Instances { get; }

        public ObservableCollection<ModListItem> Mods { get; }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public ReactiveCommand<Unit, Unit> SetCurrentInstanceCommand { get; }

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
            private set => this.RaiseAndSetIfChanged(ref statusMessage, value);
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

        public string ModSearchText
        {
            get => modSearchText;
            set => this.RaiseAndSetIfChanged(ref modSearchText, value);
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

        public string SelectedModRelationships
        {
            get => selectedModRelationships;
            private set => this.RaiseAndSetIfChanged(ref selectedModRelationships, value);
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

        public string Footer { get; }

        public bool IsRefreshing
        {
            get => isRefreshing;
            private set => this.RaiseAndSetIfChanged(ref isRefreshing, value);
        }

        public bool IsLoading => StartupStage == StartupStage.Loading;
        public bool IsEmpty => StartupStage == StartupStage.Empty;
        public bool NeedsSelection => StartupStage == StartupStage.SelectionRequired;
        public bool IsReady => StartupStage == StartupStage.Ready;
        public bool HasError => StartupStage == StartupStage.Error;

        public bool HasInstances => InstanceCount > 0;

        public bool HasCurrentInstance => !string.IsNullOrWhiteSpace(gameInstanceService.CurrentInstance?.Name);

        public bool HasSelectedInstance
        {
            get => hasSelectedInstance;
            private set => this.RaiseAndSetIfChanged(ref hasSelectedInstance, value);
        }

        public bool CanUseSelectedInstance => canUseSelectedInstance.Value;

        public bool IsCatalogLoading
        {
            get => isCatalogLoading;
            private set
            {
                this.RaiseAndSetIfChanged(ref isCatalogLoading, value);
                this.RaisePropertyChanged(nameof(ShowEmptyModResults));
            }
        }

        public bool HasMods => Mods.Count > 0;

        public bool ShowEmptyModResults => !IsCatalogLoading && Mods.Count == 0;

        public bool HasSelectedMod => SelectedMod != null;

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

        public bool FilterInstalledOnly
        {
            get => filterInstalledOnly;
            set
            {
                this.RaiseAndSetIfChanged(ref filterInstalledOnly, value);
                if (value && FilterNotInstalledOnly)
                {
                    filterNotInstalledOnly = false;
                    this.RaisePropertyChanged(nameof(FilterNotInstalledOnly));
                }
            }
        }

        public bool FilterNotInstalledOnly
        {
            get => filterNotInstalledOnly;
            set
            {
                this.RaiseAndSetIfChanged(ref filterNotInstalledOnly, value);
                if (value && FilterInstalledOnly)
                {
                    filterInstalledOnly = false;
                    this.RaisePropertyChanged(nameof(FilterInstalledOnly));
                }
            }
        }

        public bool FilterUpdatableOnly
        {
            get => filterUpdatableOnly;
            set => this.RaiseAndSetIfChanged(ref filterUpdatableOnly, value);
        }

        public bool FilterCachedOnly
        {
            get => filterCachedOnly;
            set => this.RaiseAndSetIfChanged(ref filterCachedOnly, value);
        }

        public bool FilterIncompatibleOnly
        {
            get => filterIncompatibleOnly;
            set => this.RaiseAndSetIfChanged(ref filterIncompatibleOnly, value);
        }

        public string InstanceCountLabel
            => InstanceCount switch
            {
                0 => "No registered instances",
                1 => "1 registered instance",
                _ => $"{InstanceCount} registered instances",
            };

        public string ModCountLabel
            => IsCatalogLoading
                ? "Loading catalog…"
                : Mods.Count switch
                {
                    0 => "0 mods shown",
                    1 => "1 mod shown",
                    _ => $"{Mods.Count} mods shown",
                };

        public InstanceSummary? SelectedInstance
        {
            get => selectedInstance;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedInstance, value);
                HasSelectedInstance = value != null;
                UpdateSelectedInstanceSummary(value);
            }
        }

        public ModListItem? SelectedMod
        {
            get => selectedMod;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedMod, value);
                this.RaisePropertyChanged(nameof(HasSelectedMod));
                _ = LoadModDetailsAsync(value?.Identifier);
            }
        }

        private async Task RefreshAsync()
        {
            IsRefreshing = true;
            StartupStage = StartupStage.Loading;
            Diagnostics = "Loading game instances through CKAN.App.GameInstanceService.";
            StageTitle = "Loading Instances";
            StageDescription = "Inspecting configured installs and determining the preferred startup instance.";
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
                StageDescription = "The shell could not load CKAN instance state. Check diagnostics, then retry.";
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
            StageTitle = "Switching Instance";
            StageDescription = "Reloading CKAN state for the selected install.";
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
                StageDescription = "The selected instance could not be activated.";
                StartupStage = StartupStage.Error;
            }
            finally
            {
                IsRefreshing = false;
            }
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

            if (Instances.Count == 0)
            {
                ClearCatalogState();
                StartupStage = StartupStage.Empty;
                StageTitle = "No Instances Found";
                StageDescription = "CKAN Linux did not find a registered KSP install. Stage 1 is instance-first, so the app stops here instead of guessing.";
                StatusMessage = "No known instances were found.";
                SelectedActionLabel = "Use Selected Instance";
                SelectedActionHint = "Add or register an instance before continuing.";
                PublishInstanceStateLabels();
                return;
            }

            if (gameInstanceService.CurrentInstance != null)
            {
                StartupStage = StartupStage.Ready;
                StageTitle = "Instance Ready";
                StageDescription = "The selected install is loaded. The shell now shows the first real mod browser surface over CKAN core data.";
                StatusMessage = $"Loaded {Instances.Count} instance{(Instances.Count == 1 ? "" : "s")} and activated {gameInstanceService.CurrentInstance.Name}.";
                SelectedActionLabel = "Switch to Selected Instance";
                SelectedActionHint = "You can swap the active install here before the mod browser is added.";
                _ = LoadModCatalogAsync();
            }
            else
            {
                ClearCatalogState();
                StartupStage = StartupStage.SelectionRequired;
                StageTitle = "Choose an Instance";
                StageDescription = "Multiple installs are known, but none is preferred. Pick one to enter the Linux shell.";
                StatusMessage = $"Loaded {Instances.Count} instance{(Instances.Count == 1 ? "" : "s")}. Select one to continue.";
                SelectedActionLabel = "Continue With Selected Instance";
                SelectedActionHint = "The chosen install becomes the active CKAN instance for this session.";
            }

            PublishInstanceStateLabels();
        }

        private async Task LoadModCatalogAsync()
        {
            if (!IsReady)
            {
                return;
            }

            IsCatalogLoading = true;
            CatalogStatusMessage = "Loading mods from the current CKAN registry and repository cache…";
            var previousSelection = SelectedMod?.Identifier;
            try
            {
                var items = await modCatalogService.GetModListAsync(CurrentFilter(), CancellationToken.None);
                Mods.Clear();
                foreach (var item in items)
                {
                    Mods.Add(item);
                }

                SelectedMod = previousSelection != null
                    ? Mods.FirstOrDefault(mod => mod.Identifier == previousSelection) ?? Mods.FirstOrDefault()
                    : Mods.FirstOrDefault();

                CatalogStatusMessage = Mods.Count == 0
                    ? "No mods matched the current search and filter state."
                    : $"Showing {Mods.Count} mod{(Mods.Count == 1 ? "" : "s")} for {CurrentInstanceName}.";
                PublishCatalogStateLabels();
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                CatalogStatusMessage = "Failed to load the mod catalog.";
            }
            finally
            {
                IsCatalogLoading = false;
                PublishCatalogStateLabels();
            }
        }

        private async Task LoadModDetailsAsync(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !IsReady)
            {
                ResetSelectedModDetails();
                return;
            }

            try
            {
                var details = await modCatalogService.GetModDetailsAsync(identifier, CancellationToken.None);
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
                SelectedModCompatibility = details.Compatibility;
                SelectedModModuleKind = details.ModuleKind;
                SelectedModLicense = details.License;
                SelectedModReleaseDate = details.ReleaseDate;
                SelectedModDownloadSize = details.DownloadSize;
                SelectedModRelationships = $"{details.DependencyCount} depends • {details.RecommendationCount} recommends • {details.SuggestionCount} suggests";
                SelectedModIsInstalled = details.IsInstalled;
                SelectedModHasUpdate = details.HasUpdate;
                SelectedModIsCached = details.IsCached;
                SelectedModIsIncompatible = details.IsIncompatible;
                SelectedModHasReplacement = details.HasReplacement;
                SelectedModBody = string.IsNullOrWhiteSpace(details.Description)
                    ? "No extended description is available for this mod."
                    : details.Description;
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                SelectedModTitle = "Could not load details";
                SelectedModSubtitle = identifier;
                SelectedModAuthors = "";
                SelectedModVersions = "";
                SelectedModCompatibility = "";
                SelectedModModuleKind = "";
                SelectedModLicense = "";
                SelectedModReleaseDate = "";
                SelectedModDownloadSize = "";
                SelectedModRelationships = "";
                SelectedModIsInstalled = false;
                SelectedModHasUpdate = false;
                SelectedModIsCached = false;
                SelectedModIsIncompatible = false;
                SelectedModHasReplacement = false;
                SelectedModBody = "The selected mod failed to load its details.";
            }
        }

        private void OnCurrentInstanceChanged(GameInstance? current)
        {
            Dispatcher.UIThread.Post(() =>
            {
                CurrentInstanceName = current?.Name ?? "No instance selected";
                ReloadInstances();
            });
        }

        private FilterState CurrentFilter()
            => new FilterState
            {
                SearchText       = ModSearchText,
                InstalledOnly    = FilterInstalledOnly,
                NotInstalledOnly = FilterNotInstalledOnly,
                UpdatableOnly    = FilterUpdatableOnly,
                CachedOnly       = FilterCachedOnly,
                IncompatibleOnly = FilterIncompatibleOnly,
            };

        private void UpdateSelectedInstanceSummary(InstanceSummary? instance)
        {
            if (instance == null)
            {
                SelectedInstanceSummary = "Choose an instance to inspect its details.";
                return;
            }

            SelectedInstanceSummary = $"{instance.Name} ({instance.GameName})\n{instance.GameDir}";
        }

        private void PublishInstanceStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasInstances));
            this.RaisePropertyChanged(nameof(HasCurrentInstance));
            this.RaisePropertyChanged(nameof(InstanceCountLabel));
        }

        private void ClearCatalogState()
        {
            Mods.Clear();
            ResetSelectedModDetails();
            SelectedMod = null;
            CatalogStatusMessage = "Select an active instance to view its mod catalog.";
            PublishCatalogStateLabels();
        }

        private void ResetSelectedModDetails()
        {
            SelectedModTitle = "No mod selected";
            SelectedModSubtitle = "Choose a mod to inspect its details.";
            SelectedModAuthors = "";
            SelectedModVersions = "";
            SelectedModCompatibility = "";
            SelectedModModuleKind = "";
            SelectedModLicense = "";
            SelectedModReleaseDate = "";
            SelectedModDownloadSize = "";
            SelectedModRelationships = "";
            SelectedModIsInstalled = false;
            SelectedModHasUpdate = false;
            SelectedModIsCached = false;
            SelectedModIsIncompatible = false;
            SelectedModHasReplacement = false;
            SelectedModBody = "The details pane will show summary, description, compatibility, and install state.";
        }

        private void PublishCatalogStateLabels()
        {
            this.RaisePropertyChanged(nameof(HasMods));
            this.RaisePropertyChanged(nameof(ModCountLabel));
            this.RaisePropertyChanged(nameof(ShowEmptyModResults));
        }
    }
}
