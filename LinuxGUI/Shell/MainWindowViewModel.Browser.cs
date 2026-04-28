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
            if (IsDevSmokeConflict(conflict))
            {
                ResolveDevSmokePreviewConflict(conflict);
                return;
            }

            var leftSide = ConflictLeftSide(conflict);
            var rightSide = ConflictRightSide(conflict);
            var identifiers = ConflictBrowserIdentifiers(leftSide, rightSide);
            if (identifiers.Count == 0)
            {
                StatusMessage = "No browser-visible mods were found for that conflict.";
                return;
            }

            relationshipBrowserScopeIdentifiers = identifiers;
            relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeReturnsToPreview = true;
            RelationshipBrowserScopeText = $"Conflict: {DisplayConflictTarget(leftSide)} vs {DisplayConflictTarget(rightSide)}";
            pendingModListScrollReset = true;
            ShowBrowseSurfaceTab();
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems(identifiers.FirstOrDefault());
            }
            PublishRelationshipBrowserScopeState();
        }

        private void ResolveDevSmokePreviewConflict(string conflict)
        {
            if (!PreviewConflicts.Remove(conflict))
            {
                return;
            }

            selectedPreviewConflicts.Remove(conflict);
            SelectedPreviewConflict = selectedPreviewConflicts.FirstOrDefault();
            SelectedPreviewConflictCount = selectedPreviewConflicts.Count;
            if (!HasPreviewConflicts)
            {
                PreviewCanApply = lastSolverPreviewCanApply;
            }

            StatusMessage = "Cleared dev smoke conflict fixture.";
            PublishPreviewStateLabels();
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
                    RememberRemovedQueuedActions(new[] { queued });
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
                changesetService.QueueInstall(mod, sourceText: QueueSourceForScopedBrowserMod(mod));
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

        private string QueueSourceForScopedBrowserMod(ModListItem mod)
            => relationshipBrowserScopeQueueSources.TryGetValue(mod.Identifier, out var source)
                ? source
                : "";

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
                    if (!wasReady
                        && !hasRunStartupRepositoryRefresh
                        && SettingsWindow.RefreshOnStartupEnabled(CurrentInstance))
                    {
                        hasRunStartupRepositoryRefresh = true;
                        await UpdateRepositoriesForCurrentInstanceAsync(forceFullRefresh: false);
                        RefreshCurrentRegistryReference();
                    }
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

        private async Task UpdateRepositoriesForCurrentInstanceAsync(bool forceFullRefresh)
        {
            var instance = CurrentInstance;
            var registry = CurrentRegistry;
            if (instance == null || registry == null)
            {
                return;
            }

            var repositories = registry.Repositories.Values.ToArray();
            if (repositories.Length == 0)
            {
                return;
            }

            StatusMessage = "Updating metadata repositories...";
            CatalogStatusMessage = "Checking repository metadata for updates...";
            try
            {
                var result = await Task.Run(() =>
                {
                    var downloader = new NetAsyncDownloader(user, () => null, Net.UserAgentString);
                    return gameInstanceService.RepositoryData.Update(repositories,
                                                                     instance.Game,
                                                                     forceFullRefresh,
                                                                     downloader,
                                                                     user,
                                                                     Net.UserAgentString);
                });

                StatusMessage = result == RepositoryDataManager.UpdateResult.Updated
                    ? "Metadata repositories updated."
                    : "Metadata repositories are already current.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Repository update failed; using cached metadata.";
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
                ReloadInstances(loadCatalog: false);
                if (IsReady)
                {
                    await LoadModCatalogAsync();
                }
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

            if (CanReuseRecentCatalogLoad())
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
                    await Task.Delay(CatalogLoadSettleDelayMs);
                    if (!IsReady)
                    {
                        break;
                    }

                    int activeRequestId = catalogLoadRequestId;
                    var previousSelection = SelectedMod?.Identifier;

                    if (CanReuseRecentCatalogLoad())
                    {
                        break;
                    }

                    using var loadingIndicatorCts = new CancellationTokenSource();
                    var loadingIndicatorTask = ShowCatalogLoadingAfterDelayAsync(activeRequestId,
                                                                                 loadingIndicatorCts.Token);

                    try
                    {
                        var totalWatch = Stopwatch.StartNew();
                        var serviceWatch = Stopwatch.StartNew();
                        var items = await modCatalogService.GetAllModListAsync(CancellationToken.None);
                        serviceWatch.Stop();
                        if (activeRequestId != catalogLoadRequestId)
                        {
                            continue;
                        }

                        loadingIndicatorCts.Cancel();
                        var uiWatch = Stopwatch.StartNew();
                        ApplyVersionDisplaySettings(items);
                        allCatalogItems = items;
                        ClearDefaultInstalledFilterWhenEmpty(items);
                        ApplyCatalogFilterToLoadedItems(previousSelection);
                        PruneQueuedAutodetectedRemovals(items);
                        PruneQueuedAutodetectedDownloads(items);
                        SeedDevQueueSmoke(items);
                        uiWatch.Stop();
                        totalWatch.Stop();
                        var summary = $"Last catalog load: {items.Count} mods in {totalWatch.ElapsedMilliseconds} ms (service {serviceWatch.ElapsedMilliseconds} ms, UI {uiWatch.ElapsedMilliseconds} ms).";
                        Diagnostics = summary;
                        lastCatalogLoadCompletedUtc = DateTime.UtcNow;
                        Trace.TraceInformation(
                            $"LinuxGUI catalog load request={activeRequestId} items={items.Count} service_ms={serviceWatch.ElapsedMilliseconds} ui_ms={uiWatch.ElapsedMilliseconds} total_ms={totalWatch.ElapsedMilliseconds}");
                    }
                    catch (Exception ex)
                    {
                        loadingIndicatorCts.Cancel();
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
                        loadingIndicatorCts.Cancel();
                        try
                        {
                            await loadingIndicatorTask;
                        }
                        catch (TaskCanceledException)
                        {
                        }

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

        private bool CanReuseRecentCatalogLoad()
            => allCatalogItems.Count > 0
               && !IsCatalogLoading
               && DateTime.UtcNow - lastCatalogLoadCompletedUtc
                    < TimeSpan.FromMilliseconds(RecentCatalogReloadSuppressionMs);

        private async Task ShowCatalogLoadingAfterDelayAsync(int               requestId,
                                                             CancellationToken cancellationToken)
        {
            await Task.Delay(CatalogLoadingIndicatorDelayMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested
                && requestId == catalogLoadRequestId)
            {
                IsCatalogLoading = true;
                CatalogStatusMessage = "Loading mods from the current CKAN registry and repository cache…";
                PublishCatalogStateLabels();
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
                ReloadInstances(loadCatalog: false);
                this.RaisePropertyChanged(nameof(CurrentInstance));
                this.RaisePropertyChanged(nameof(CurrentRegistry));
                this.RaisePropertyChanged(nameof(CurrentRegistryManager));
            });
        }

        private void OnQueueChanged()
        {
            if (!suppressQueueSnapshotPersistence)
            {
                SaveQueuedActionsForCurrentInstance();
            }

            if (suppressQueueChangedRefresh)
            {
                pendingQueueChangedRefresh = true;
                return;
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
                NameText            = AdvancedNameFilter,
                IdentifierText      = AdvancedIdentifierFilter,
                AuthorText          = AdvancedAuthorFilter,
                SummaryText         = AdvancedSummaryFilter,
                DescriptionText     = AdvancedDescriptionFilter,
                LicenseText         = AdvancedLicenseFilter,
                LanguageText        = AdvancedLanguageFilter,
                DependsText         = AdvancedDependsFilter,
                RecommendsText      = AdvancedRecommendsFilter,
                SuggestsText        = AdvancedSuggestsFilter,
                ConflictsText       = AdvancedConflictsFilter,
                SupportsText        = AdvancedSupportsFilter,
                TagText             = AdvancedTagsFilter,
                LabelText           = AdvancedLabelsFilter,
                CompatibilityText   = AdvancedCompatibilityFilter,
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
            advancedNameFilter = filter.NameText ?? "";
            advancedIdentifierFilter = filter.IdentifierText ?? "";
            advancedAuthorFilter = filter.AuthorText ?? "";
            advancedSummaryFilter = filter.SummaryText ?? "";
            advancedDescriptionFilter = filter.DescriptionText ?? "";
            advancedLicenseFilter = filter.LicenseText ?? "";
            advancedLanguageFilter = filter.LanguageText ?? "";
            advancedDependsFilter = filter.DependsText ?? "";
            advancedRecommendsFilter = filter.RecommendsText ?? "";
            advancedSuggestsFilter = filter.SuggestsText ?? "";
            advancedConflictsFilter = filter.ConflictsText ?? "";
            advancedSupportsFilter = filter.SupportsText ?? "";
            advancedTagsFilter = SerializeFilterValues(SplitFilterValues(filter.TagText));
            advancedLabelsFilter = SerializeFilterValues(SplitFilterValues(filter.LabelText));
            advancedCompatibilityFilter = filter.CompatibilityText ?? "";
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

        private void ClearDefaultInstalledFilterWhenEmpty(IReadOnlyList<ModListItem> items)
        {
            if (!IsInstalledOnlyFilter(CurrentFilter())
                || items.Any(item => item.IsInstalled))
            {
                return;
            }

            suppressFilterAutoRefresh = true;
            try
            {
                SetFilterBackingField(ref filterInstalledOnly, false, nameof(FilterInstalledOnly));
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            modSearchService.SetCurrent(CurrentFilter());
            PublishFilterStateLabels();
            StatusMessage = "No installed mods were detected; showing all available mods.";
        }

        private static bool IsInstalledOnlyFilter(FilterState filter)
            => string.IsNullOrWhiteSpace(filter.SearchText)
               && string.IsNullOrWhiteSpace(filter.NameText)
               && string.IsNullOrWhiteSpace(filter.IdentifierText)
               && string.IsNullOrWhiteSpace(filter.AuthorText)
               && string.IsNullOrWhiteSpace(filter.SummaryText)
               && string.IsNullOrWhiteSpace(filter.DescriptionText)
               && string.IsNullOrWhiteSpace(filter.LicenseText)
               && string.IsNullOrWhiteSpace(filter.LanguageText)
               && string.IsNullOrWhiteSpace(filter.DependsText)
               && string.IsNullOrWhiteSpace(filter.RecommendsText)
               && string.IsNullOrWhiteSpace(filter.SuggestsText)
               && string.IsNullOrWhiteSpace(filter.ConflictsText)
               && string.IsNullOrWhiteSpace(filter.SupportsText)
               && string.IsNullOrWhiteSpace(filter.TagText)
               && string.IsNullOrWhiteSpace(filter.LabelText)
               && string.IsNullOrWhiteSpace(filter.CompatibilityText)
               && filter.InstalledOnly
               && !filter.NotInstalledOnly
               && !filter.UpdatableOnly
               && !filter.NotUpdatableOnly
               && !filter.NewOnly
               && !filter.CompatibleOnly
               && !filter.CachedOnly
               && !filter.UncachedOnly
               && !filter.IncompatibleOnly
               && !filter.HasReplacementOnly
               && !filter.NoReplacementOnly;

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
            var hasRightTarget = !string.IsNullOrWhiteSpace(rightTarget);
            var leftInfo = ResolveConflictSideInfo(leftTarget);
            var rightInfo = ResolveConflictSideInfo(rightTarget);
            if (string.IsNullOrWhiteSpace(leftTarget))
            {
                leftTarget = leftInfo.DisplayName;
            }

            var displayLeftTarget = leftInfo.DisplayName;
            var displayRightTarget = hasRightTarget
                ? rightInfo.DisplayName
                : "";
            var actionText = hasRightTarget
                ? $"{displayLeftTarget} conflicts with {displayRightTarget}"
                : displayLeftTarget;

            return new PreviewConflictChoiceItem
            {
                ConflictText = conflict,
                ActionText = actionText,
                DetailText = hasRightTarget
                    ? BuildConflictChoiceDetail(leftInfo, rightInfo)
                    : BuildConflictIssueDetail(leftInfo),
                ActionButtonText = IsDevSmokeConflict(conflict) ? "Clear" : "Review",
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
            var leftState = BuildConflictSideState(left);
            var rightState = BuildConflictSideState(right);
            return $"State: {leftState} vs {rightState}. Keep one; remove the other before applying.";
        }

        private string BuildConflictIssueDetail(ConflictSideInfo issue)
            => $"State: {BuildConflictSideState(issue)}. Clear this issue before applying.";

        private static string BuildConflictSideState(ConflictSideInfo side)
            => side.QueuedAction?.ActionKind switch
            {
                QueuedActionKind.Install => "queued install",
                QueuedActionKind.Update => "queued update",
                QueuedActionKind.Remove => "queued removal",
                QueuedActionKind.Download => "queued download",
                _ when side.DisplayName.Contains("[Dev Smoke]", StringComparison.OrdinalIgnoreCase) => "dev smoke fixture",
                _ when side.Mod?.IsInstalled == true => "installed",
                _ when side.Mod != null => "available",
                _ => "unknown",
            };

        private static bool IsDevSmokeConflict(string conflict)
            => conflict.Contains("[Dev Smoke]", StringComparison.OrdinalIgnoreCase);

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
            await ContinuePreviewApplyFlowAsync();
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
                    RememberRemovedQueuedActions(new[] { queued });
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
            var totalWatch = Stopwatch.StartNew();
            var currentFilter = CurrentFilter();
            var scopeWatch = Stopwatch.StartNew();
            var sourceItems = ShowRelationshipBrowserScope
                ? allCatalogItems.Where(item => relationshipBrowserScopeIdentifiers.Contains(item.Identifier)).ToList()
                : allCatalogItems;
            scopeWatch.Stop();
            var filterWatch = Stopwatch.StartNew();
            var visibleItems = ShowRelationshipBrowserScope
                ? modCatalogService.ApplyFilter(sourceItems, CurrentSortOnlyFilter())
                : modCatalogService.ApplyFilter(sourceItems, currentFilter);
            filterWatch.Stop();

            var countWatch = Stopwatch.StartNew();
            filterOptionCounts = modCatalogService.GetFilterOptionCounts(sourceItems, currentFilter);
            hasFilterOptionCounts = true;
            countWatch.Stop();

            var applyWatch = Stopwatch.StartNew();
            ReplaceVisibleMods(visibleItems);
            ReplaceAvailableTagOptions(BuildAvailableTagOptions(sourceItems, currentFilter));
            ReplaceAvailableLabelOptions(BuildAvailableLabelOptions(sourceItems, currentFilter.LabelText));
            PublishVisibleModQueueState();
            applyWatch.Stop();

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
            totalWatch.Stop();
            Trace.TraceInformation(
                $"LinuxGUI catalog filter scope={(ShowRelationshipBrowserScope ? RelationshipBrowserScopeText : "all")} source={sourceItems.Count} visible={visibleItems.Count} scope_ms={scopeWatch.ElapsedMilliseconds} filter_ms={filterWatch.ElapsedMilliseconds} counts_ms={countWatch.ElapsedMilliseconds} apply_ms={applyWatch.ElapsedMilliseconds} total_ms={totalWatch.ElapsedMilliseconds}");
        }

        private void ApplyVersionDisplaySettings(IEnumerable<ModListItem> items)
        {
            bool hideEpochs = SettingsWindow.HideEpochsEnabled(CurrentInstance);
            bool hideV = SettingsWindow.HideVEnabled(CurrentInstance);
            foreach (var item in items)
            {
                item.DisplayLatestVersion = FormatVersionForList(item.LatestVersion, hideEpochs, hideV);
                item.DisplayInstalledVersion = FormatVersionForList(item.InstalledVersion, hideEpochs, hideV);
            }
        }

        private static string FormatVersionForList(string version,
                                                   bool hideEpochs,
                                                   bool hideV)
        {
            if (string.IsNullOrWhiteSpace(version) || (!hideEpochs && !hideV))
            {
                return version;
            }

            try
            {
                return new ModuleVersion(version).ToString(hideEpochs, hideV);
            }
            catch
            {
                return version;
            }
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

    }
}
