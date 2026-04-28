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
                lastSolverPreviewCanApply = false;
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
                ReplacePreviewCollection(PreviewSupporters, Array.Empty<string>());
                ReplacePreviewCollection(PreviewConflicts, Array.Empty<string>());
                PublishPreviewStateLabels();
                return;
            }

            IsPreviewLoading = true;
            try
            {
                var preview = await modActionService.PreviewChangesAsync(CancellationToken.None);
                var devSmokePreview = DevQueueSmokeEnabled();
                var previewAutoRemovals = devSmokePreview
                    ? BuildDevSmokePreviewAutoRemovals(preview.AutoRemovals)
                    : preview.AutoRemovals;
                var previewConflicts = devSmokePreview
                    ? BuildDevSmokePreviewConflicts(preview.Conflicts)
                    : preview.Conflicts;
                lastSolverPreviewCanApply = preview.CanApply;
                PreviewSummary = preview.SummaryText;
                PreviewCanApply = preview.CanApply
                                  && (!devSmokePreview
                                      || previewConflicts.Count <= preview.Conflicts.Count);
                ReplacePreviewCollection(PreviewDownloadsRequired, preview.DownloadsRequired);
                ReplacePreviewCollection(PreviewDependencies, preview.DependencyInstalls);
                ReplacePreviewCollection(PreviewAutoRemovals, previewAutoRemovals);
                ReplacePreviewCollection(PreviewAttentionNotes, preview.AttentionNotes);
                ReplacePreviewCollection(PreviewRecommendations, FilterPreviewOptionalEntries(preview.Recommendations));
                ReplacePreviewCollection(PreviewSuggestions, FilterPreviewOptionalEntries(preview.Suggestions));
                ReplacePreviewCollection(PreviewSupporters, FilterPreviewOptionalEntries(preview.Supporters));
                ReplacePreviewCollection(PreviewConflicts, previewConflicts);
                PublishPreviewStateLabels();
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                PreviewSummary = "Preview generation failed.";
                lastSolverPreviewCanApply = false;
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
                ReplacePreviewCollection(PreviewSupporters, Array.Empty<string>());
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
                    await LoadCatalogAfterAppliedChangesAsync();
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

        private async Task ContinuePreviewApplyFlowAsync()
        {
            await ApplyQueuedChangesAsync(promptForRecommendations: false);
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

                    await LoadCatalogAfterAppliedChangesAsync();
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

                    await LoadCatalogAfterAppliedChangesAsync();
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
                    await LoadCatalogAfterAppliedChangesAsync();
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

        private async Task LoadCatalogAfterAppliedChangesAsync()
        {
            ClearRelationshipBrowserScopeForCatalogReload();
            await LoadModCatalogAsync();
        }

        private void ClearRelationshipBrowserScopeForCatalogReload()
        {
            if (!ShowRelationshipBrowserScope)
            {
                return;
            }

            relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeReturnsToPreview = false;
            RelationshipBrowserScopeText = "";
            pendingModListScrollReset = true;
            PublishRelationshipBrowserScopeState();
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
            this.RaisePropertyChanged(nameof(HasQueuedActionUndo));
            this.RaisePropertyChanged(nameof(ShowQueueFooterActions));
            this.RaisePropertyChanged(nameof(SurfaceViewToggleCompact));
            this.RaisePropertyChanged(nameof(QueuedChangeActionCount));
            this.RaisePropertyChanged(nameof(QueuedDownloadActionCount));
            this.RaisePropertyChanged(nameof(QueuedRecommendationActionCount));
            this.RaisePropertyChanged(nameof(QueuedSuggestionActionCount));
            this.RaisePropertyChanged(nameof(QueuedSupporterActionCount));
            this.RaisePropertyChanged(nameof(HasQueuedRecommendationActions));
            this.RaisePropertyChanged(nameof(HasQueuedSuggestionActions));
            this.RaisePropertyChanged(nameof(HasQueuedSupporterActions));
            this.RaisePropertyChanged(nameof(QueuedRecommendationCountLabel));
            this.RaisePropertyChanged(nameof(QueuedSuggestionCountLabel));
            this.RaisePropertyChanged(nameof(QueuedSupporterCountLabel));
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
            PublishPreviewActionStateLabels();
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
            this.RaisePropertyChanged(nameof(HasPreviewSupporters));
            this.RaisePropertyChanged(nameof(HasPreviewRecommendationsOrSuggestions));
            this.RaisePropertyChanged(nameof(HasPreviewOptionalExtras));
            this.RaisePropertyChanged(nameof(HasPreviewDependenciesOrOptional));
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
            PublishPreviewActionStateLabels();
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
            lastSolverPreviewCanApply = false;
            PreviewSummary = "Queue installs, updates, removals, or downloads from Browse to review them before running anything.";
            PreviewCanApply = false;
            ReplacePreviewCollection(PreviewDownloadsRequired, Array.Empty<string>());
            ReplacePreviewCollection(PreviewDependencies, Array.Empty<string>());
            ReplacePreviewCollection(PreviewAutoRemovals, Array.Empty<string>());
            ReplacePreviewCollection(PreviewAttentionNotes, Array.Empty<string>());
            ReplacePreviewCollection(PreviewRecommendations, Array.Empty<string>());
            ReplacePreviewCollection(PreviewSuggestions, Array.Empty<string>());
            ReplacePreviewCollection(PreviewSupporters, Array.Empty<string>());
            ReplacePreviewCollection(PreviewConflicts, Array.Empty<string>());
            PublishPreviewStateLabels();
        }
    }
}
