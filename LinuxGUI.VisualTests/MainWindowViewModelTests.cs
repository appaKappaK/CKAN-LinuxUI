using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Headless.NUnit;

using NUnit.Framework;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    [TestFixture]
    public sealed class MainWindowViewModelTests
    {
        [AvaloniaTest]
        public async Task PrimarySelectedModAction_FollowsQueuedActionPrecedence()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");

                Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Update"));

                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.True);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Cancel Update"));
                });

                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.False);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Update"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task QueueDrawer_AutoExpandsAndKeepsManualCollapseSticky()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.False);
                    Assert.That(viewModel.ShowEmptyQueueStub, Is.True);
                });

                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.True);
                    Assert.That(viewModel.ShowExpandedQueuePanel, Is.True);
                });

                viewModel.ToggleQueueDrawerCommand.Execute().Subscribe(_ => { });
                await Task.Delay(25);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.False);
                    Assert.That(viewModel.ShowCollapsedQueuedActionsStub, Is.True);
                });

                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "parallax");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.False);
                    Assert.That(viewModel.ShowCollapsedQueuedActionsStub, Is.True);
                });

                viewModel.ClearQueueCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.False);
                    Assert.That(viewModel.ShowEmptyQueueStub, Is.True);
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task SuccessfulApply_CollapsesToApplyResultStubAfterQueueClears()
        {
            var applyResult = new ApplyChangesResult
            {
                Kind = ApplyResultKind.Warning,
                Success = true,
                Title = "Apply Completed with Follow-Up",
                Message = "Applied 1 queued action. Kept 1 config-only directory for manual review.",
                SummaryLines = new[] { "1 queued action" },
                FollowUpLines = new[] { "Review leftover config-only directory." },
            };
            var (viewModel, service) = CreateViewModel(applyResult);

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(200);
                viewModel.ApplyChangesCommand.Execute().Subscribe(_ => { });
                await Task.Delay(300);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasApplyResult, Is.True);
                    Assert.That(viewModel.HasQueuedActions, Is.False);
                    Assert.That(viewModel.IsQueueDrawerExpanded, Is.False);
                    Assert.That(viewModel.ShowExecutionResultOverlay, Is.True);
                    Assert.That(viewModel.ShowCollapsedApplyResultStub, Is.False);
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AcknowledgeExecutionResult_SuccessReturnsToBrowseAndClearsResult()
        {
            var applyResult = new ApplyChangesResult
            {
                Kind = ApplyResultKind.Success,
                Success = true,
                Title = "Apply Completed",
                Message = "Applied 1 queued action.",
            };
            var (viewModel, service) = CreateViewModel(applyResult);

            try
            {
                await Task.Delay(150);
                viewModel.ShowPreviewSurfaceCommand.Execute().Subscribe(_ => { });
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(200);
                viewModel.ApplyChangesCommand.Execute().Subscribe(_ => { });
                await Task.Delay(300);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ShowExecutionResultOverlay, Is.True);
                    Assert.That(viewModel.ShowPreviewSurface, Is.True);
                    Assert.That(viewModel.HasApplyResult, Is.True);
                });

                viewModel.AcknowledgeExecutionResultCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ShowExecutionResultOverlay, Is.False);
                    Assert.That(viewModel.ShowBrowseSurface, Is.True);
                    Assert.That(viewModel.HasApplyResult, Is.False);
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AutodetectedMod_ShowsExternalStateAndBlocksRemoval()
        {
            var (viewModel, service) = CreateViewModel(catalog: new AutodetectedCatalogService());

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 1);
                var mod = viewModel.Mods.Single();
                viewModel.SelectedMod = mod;
                await Task.Delay(200);

                Assert.Multiple(() =>
                {
                    Assert.That(mod.PrimaryStateLabel, Is.EqualTo("Installed"));
                    Assert.That(mod.SecondaryStateLabel, Is.EqualTo("External"));
                    Assert.That(viewModel.ShowRemoveAction, Is.False);
                    Assert.That(viewModel.ShowSelectedModActionUnavailableNote, Is.True);
                    Assert.That(viewModel.SelectedModIsAutodetected, Is.True);
                    Assert.That(viewModel.SelectedModActionUnavailableNote, Does.Contain("managed outside CKAN"));
                    Assert.That(viewModel.SelectedModInstallState, Is.EqualTo("Managed outside CKAN"));
                    Assert.That(viewModel.SelectedModVersions, Does.Contain("Installed version unknown"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AutodetectedQueuedRemoval_IsPrunedAfterCatalogLoad()
        {
            var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            changes.QueueRemove(AutodetectedCatalogService.Item);
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, new AutodetectedCatalogService(), search, changes, actions, user);

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 1);
                await WaitForAsync(() => !viewModel.HasQueuedActions);

                Assert.That(viewModel.StatusMessage, Does.Contain("detected outside CKAN"));
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AutodetectedIncompatibleMod_ShowsDependencyStateInsteadOfIncompatible()
        {
            var (viewModel, service) = CreateViewModel(catalog: new AutodetectedDependencyCatalogService());

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 1);
                var mod = viewModel.Mods.Single();
                viewModel.SelectedMod = mod;
                await Task.Delay(200);

                Assert.Multiple(() =>
                {
                    Assert.That(mod.PrimaryStateLabel, Is.EqualTo("Installed"));
                    Assert.That(mod.SecondaryStateLabel, Is.EqualTo("External"));
                    Assert.That(mod.TertiaryStateLabel, Is.EqualTo("Dependency"));
                    Assert.That(mod.IsIncompatible, Is.True);
                    Assert.That(viewModel.SelectedModShowsDependencyState, Is.True);
                    Assert.That(viewModel.SelectedModShowsIncompatibleState, Is.False);
                    Assert.That(viewModel.SelectedModInstallState, Is.EqualTo("Managed outside CKAN"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task UninstalledIncompatibleMod_StatusOmitsNotInstalled()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count > 0);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "kerbalism");
                await Task.Delay(200);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedModIsInstalled, Is.False);
                    Assert.That(viewModel.SelectedModIsIncompatible, Is.True);
                    Assert.That(viewModel.SelectedModInstallState, Is.EqualTo("Currently incompatible"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task UninstalledCompatibleMod_HidesEmptyBadgeStrip_AndVersionsStayVersionOnly()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count > 0);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "parallax");
                await Task.Delay(200);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ShowSelectedModStateBadges, Is.False);
                    Assert.That(viewModel.SelectedModInstallState, Is.EqualTo("Not installed"));
                    Assert.That(viewModel.SelectedModVersions, Is.EqualTo("Latest 2.1.0"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task TrySwitchSelectedInstanceAsync_CancelsOrConfirmsQueueDiscard()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                viewModel.SelectedInstance = viewModel.Instances.First(inst => !inst.IsCurrent);
                string? prompt = null;
                bool canceled = await viewModel.TrySwitchSelectedInstanceAsync(message =>
                {
                    prompt = message;
                    return Task.FromResult(false);
                });

                Assert.Multiple(() =>
                {
                    Assert.That(canceled, Is.False);
                    Assert.That(prompt, Does.Contain("discard"));
                    Assert.That(prompt, Does.Contain("queued item"));
                    Assert.That(viewModel.CurrentInstanceName, Is.EqualTo("Career Save"));
                    Assert.That(viewModel.HasQueuedActions, Is.True);
                    Assert.That(viewModel.SelectedInstance?.IsCurrent, Is.True);
                });

                viewModel.SelectedInstance = viewModel.Instances.First(inst => !inst.IsCurrent);
                bool switched = await viewModel.TrySwitchSelectedInstanceAsync(_ => Task.FromResult(true));

                Assert.Multiple(() =>
                {
                    Assert.That(switched, Is.True);
                    Assert.That(viewModel.CurrentInstanceName, Is.EqualTo("RSS Sandbox"));
                    Assert.That(viewModel.HasQueuedActions, Is.False);
                    Assert.That(viewModel.SelectedInstance?.Name, Is.EqualTo("RSS Sandbox"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task MoreFiltersLabel_TracksHiddenAdvancedFilterCount()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);

                Assert.That(viewModel.MoreFiltersLabel, Is.EqualTo("Filters ▾"));

                viewModel.AdvancedAuthorFilter = "Nertea";
                viewModel.AdvancedCompatibilityFilter = "1.12";
                viewModel.FilterHasReplacementOnly = true;

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ActiveFilterCount, Is.EqualTo(3));
                    Assert.That(viewModel.MoreFiltersLabel, Is.EqualTo("Active Filters (3) ▾"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task SortChanges_DoNotCountAsFilters_AndClearFiltersPreservesSort()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);

                viewModel.SelectPopularitySortCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasActiveFilters, Is.False);
                    Assert.That(viewModel.ActiveFilterCount, Is.EqualTo(0));
                    Assert.That(viewModel.MoreFiltersLabel, Is.EqualTo("Filters ▾"));
                    Assert.That(viewModel.AdvancedFilterSummary, Is.EqualTo("All mods are shown."));
                    Assert.That(viewModel.SelectedSortOption?.Value, Is.EqualTo(ModSortOption.Popularity));
                });

                viewModel.FilterCachedOnly = true;
                await Task.Delay(50);
                viewModel.ClearFiltersCommand.Execute().Subscribe(_ => { });
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasActiveFilters, Is.False);
                    Assert.That(viewModel.FilterCachedOnly, Is.False);
                    Assert.That(viewModel.SelectedSortOption?.Value, Is.EqualTo(ModSortOption.Popularity));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task DownloadOnly_IsContextualAndDoesNotReplacePrimaryAction()
        {
            var catalog = new DownloadReadyCatalogService();
            var (viewModel, service) = CreateViewModel(catalog: catalog);

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.Single();
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Install"));
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.False);
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.False);
                    Assert.That(viewModel.DownloadOnlyContextLabel(viewModel.SelectedMod!), Is.EqualTo("Download Only"));
                });

                viewModel.ToggleDownloadOnlyFromBrowser(viewModel.SelectedMod!);
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.False);
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.True);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Install"));
                    Assert.That(viewModel.SelectedModQueueStatus, Does.Contain("Download Only queued"));
                    Assert.That(viewModel.SelectedMod!.QueueStateLabel, Is.EqualTo("Queued Download"));
                });

                viewModel.ToggleDownloadOnlyFromBrowser(viewModel.SelectedMod!);
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.False);
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.False);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Install"));
                });

                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.True);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Cancel Install"));
                    Assert.That(viewModel.ShowDownloadOnlyContextAction(viewModel.SelectedMod!), Is.False);
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task InstalledUncachedMod_OffersAddToCache()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "mechjeb2");
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ShowDownloadOnlyContextAction(viewModel.SelectedMod!), Is.True);
                    Assert.That(viewModel.DownloadOnlyContextLabel(viewModel.SelectedMod!), Is.EqualTo("Add to Cache"));
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Remove"));
                });

                viewModel.ToggleDownloadOnlyFromBrowser(viewModel.SelectedMod!);
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.True);
                    Assert.That(viewModel.SelectedModQueueStatus, Does.Contain("Add to Cache queued"));
                    Assert.That(viewModel.StatusMessage, Does.Contain("add to cache"));
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Remove"));
                    Assert.That(viewModel.SelectedMod!.QueueStateLabel, Is.EqualTo("Queued Cache"));
                });

                viewModel.ToggleDownloadOnlyFromBrowser(viewModel.SelectedMod!);
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.False);
                    Assert.That(viewModel.DownloadOnlyContextLabel(viewModel.SelectedMod!), Is.EqualTo("Add to Cache"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AutodetectedMod_DoesNotOfferAddToCache()
        {
            var (viewModel, service) = CreateViewModel(catalog: new AutodetectedCatalogService());

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 1);
                viewModel.SelectedMod = viewModel.Mods.Single();
                await Task.Delay(100);

                Assert.That(viewModel.ShowDownloadOnlyContextAction(viewModel.SelectedMod!), Is.False);

                viewModel.ToggleDownloadOnlyFromBrowser(viewModel.SelectedMod!);
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedDownload, Is.False);
                    Assert.That(viewModel.StatusMessage, Does.Contain("cannot be added to the cache"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task AutodetectedQueuedCacheAction_IsPrunedAfterCatalogLoad()
        {
            var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            changes.QueueDownload(AutodetectedCatalogService.Item);
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, new AutodetectedCatalogService(), search, changes, actions, user);

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 1);
                await WaitForAsync(() => !viewModel.HasQueuedActions);

                Assert.That(viewModel.StatusMessage, Does.Contain("cannot be added to the cache"));
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task ApplyChanges_LeavesDownloadOnlyQueuedUntilDownloadRuns()
        {
            var catalog = new MixedQueueCatalogService();
            var applyResult = new ApplyChangesResult
            {
                Kind = ApplyResultKind.Success,
                Success = true,
                Title = "Apply Completed",
                Message = "Applied queued changes.",
            };
            var (viewModel, service) = CreateViewModel(applyResult, catalog);

            try
            {
                await Task.Delay(150);

                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(75);

                var downloadOnlyMod = viewModel.Mods.First(mod => mod.Identifier == "download-ready");
                viewModel.ToggleDownloadOnlyFromBrowser(downloadOnlyMod);
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasQueuedActions, Is.True);
                    Assert.That(viewModel.HasQueuedChangeActions, Is.True);
                    Assert.That(viewModel.HasQueuedDownloadActions, Is.True);
                    Assert.That(viewModel.PreviewQueuedCountLabel, Is.EqualTo("1 Direct Change"));
                    Assert.That(viewModel.PreviewDownloadQueueCountLabel, Is.EqualTo("1 Queued Download"));
                });

                viewModel.ApplyChangesCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasQueuedChangeActions, Is.False);
                    Assert.That(viewModel.HasQueuedDownloadActions, Is.True);
                    Assert.That(viewModel.QueuedActions.Single().Identifier, Is.EqualTo("download-ready"));
                    Assert.That(viewModel.PreviewStatusLabel, Is.EqualTo("Queued downloads ready"));
                });

                viewModel.DownloadQueuedCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasQueuedActions, Is.False);
                    Assert.That(viewModel.HasQueuedDownloadActions, Is.False);
                    Assert.That(viewModel.ApplyResultTitle, Is.EqualTo("Downloads Completed"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task FilterLabels_ShowPreclickCountsForCurrentScope()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(200);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.CompatibleFilterLabel, Is.EqualTo("Compatible (4)"));
                    Assert.That(viewModel.InstalledFilterLabel, Is.EqualTo("Installed (2)"));
                    Assert.That(viewModel.UpdatableFilterLabel, Is.EqualTo("Updatable (1)"));
                    Assert.That(viewModel.CachedFilterLabel, Is.EqualTo("Cached (2)"));
                    Assert.That(viewModel.NotInstalledFilterLabel, Is.EqualTo("Not Installed (3)"));
                    Assert.That(viewModel.IncompatibleFilterLabel, Is.EqualTo("Incompatible (1)"));
                });

                viewModel.FilterCachedOnly = true;
                await Task.Delay(250);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.CachedFilterLabel, Is.EqualTo("Cached (2)"));
                    Assert.That(viewModel.InstalledFilterLabel, Is.EqualTo("Installed (1)"));
                    Assert.That(viewModel.NotInstalledFilterLabel, Is.EqualTo("Not Installed (2)"));
                    Assert.That(viewModel.UncachedFilterLabel, Is.EqualTo("Not Cached (1)"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task Search_FindsWordsFromDescriptions()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);

                viewModel.ModSearchText = "tessellation";
                await WaitForAsync(() => !viewModel.IsCatalogLoading
                                         && viewModel.Mods.Count == 1
                                         && viewModel.Mods.Single().Identifier == "parallax",
                                  timeoutMs: 1500);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.Mods.Single().Identifier, Is.EqualTo("parallax"));
                    Assert.That(viewModel.ModCountLabel, Is.EqualTo("1 mod"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task Sorting_ReordersVisibleModsWithoutReloadingCatalog()
        {
            var catalog = new DelayedModCatalogService(listDelayMs: 250);
            var (viewModel, service) = CreateViewModel(catalog: catalog);

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count > 0 && !viewModel.IsCatalogLoading, timeoutMs: 1500);
                Assert.That(catalog.ModListRequestCount, Is.EqualTo(1));
                Assert.That(viewModel.Mods.First().Identifier, Is.EqualTo("kerbalism"));

                viewModel.SelectPopularitySortCommand.Execute().Subscribe(_ => { });
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(catalog.ModListRequestCount, Is.EqualTo(1));
                    Assert.That(viewModel.IsCatalogLoading, Is.False);
                    Assert.That(viewModel.ModCountLabel, Is.EqualTo("5 mods"));
                    Assert.That(viewModel.ModListScrollResetRequestId, Is.EqualTo(0));
                    Assert.That(viewModel.Mods.First().Identifier, Is.EqualTo("parallax"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task ActivateModFromBrowser_OpensDetailsAndTogglesSameSelection()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await WaitForAsync(() => viewModel.SelectedMod != null);

                var initiallySelected = viewModel.SelectedMod!;
                Assert.That(viewModel.ShowDetailsPane, Is.False);

                viewModel.ActivateModFromBrowser(initiallySelected);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(initiallySelected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
                });

                viewModel.ActivateModFromBrowser(initiallySelected);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(initiallySelected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.False);
                });

                var otherMod = viewModel.Mods.First(mod => mod.Identifier != initiallySelected.Identifier);
                viewModel.ShowDetailsPane = false;
                viewModel.ActivateModFromBrowser(otherMod);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(otherMod.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task ReadyHeader_HidesStatusSurfaceWhenIdle()
        {
            var (viewModel, service) = CreateViewModel();

            try
            {
                await Task.Delay(150);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.StatusMessage, Does.Contain("Loaded"));
                    Assert.That(viewModel.ShowReadyStatusSurface, Is.False);
                    Assert.That(viewModel.PreviewSurfaceButtonBackground, Is.EqualTo("#181D23"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task ReadyHeader_ShowsStatusSurfaceForBusyAndProblemStates()
        {
            var (viewModel, service, user) = CreateViewModelWithUser();

            try
            {
                await Task.Delay(150);
                Assert.That(viewModel.ShowReadyStatusSurface, Is.False);

                user.RaiseProgress("Downloading metadata…", 35);
                await WaitForAsync(() => viewModel.ShowReadyStatusSurface);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.StatusMessage, Is.EqualTo("Downloading metadata…"));
                    Assert.That(viewModel.ShowReadyStatusSurface, Is.True);
                });

                user.RaiseProgress("Downloads complete.", 100);
                await WaitForAsync(() => !viewModel.ShowReadyStatusSurface);

                user.RaiseError("Apply failed.");
                await WaitForAsync(() => viewModel.ShowReadyStatusSurface);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.StatusMessage, Is.EqualTo("Apply failed."));
                    Assert.That(viewModel.ShowReadyStatusSurface, Is.True);
                    Assert.That(viewModel.StatusSurfaceBackground, Is.EqualTo("#4A232A"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        [AvaloniaTest]
        public async Task SelectedMod_ShowsLoadingStateUntilDetailsResolve()
        {
            var (viewModel, service) = CreateViewModel(catalog: new DelayedModCatalogService(detailsDelayMs: 250));

            try
            {
                await Task.Delay(150);
                viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "parallax");
                await Task.Delay(25);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsSelectedModLoading, Is.True);
                    Assert.That(viewModel.ShowSelectedModLoadingState, Is.True);
                    Assert.That(viewModel.ShowSelectedModContent, Is.False);
                });

                await Task.Delay(300);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.IsSelectedModLoading, Is.False);
                    Assert.That(viewModel.ShowSelectedModLoadingState, Is.False);
                    Assert.That(viewModel.ShowSelectedModContent, Is.True);
                    Assert.That(viewModel.SelectedModTitle, Is.EqualTo("Parallax"));
                });
            }
            finally
            {
                service.Dispose();
            }
        }

        private static (MainWindowViewModel ViewModel, FakeGameInstanceService Service) CreateViewModel(ApplyChangesResult? applyResult = null,
                                                                                                        IModCatalogService?  catalog = null)
        {
            var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes, applyResult);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog ?? new FakeModCatalogService(), search, changes, actions, user);
            return (viewModel, service);
        }

        private static (MainWindowViewModel ViewModel, FakeGameInstanceService Service, AvaloniaUser User) CreateViewModelWithUser(
            ApplyChangesResult? applyResult = null,
            IModCatalogService? catalog = null)
        {
            var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes, applyResult);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog ?? new FakeModCatalogService(), search, changes, actions, user);
            return (viewModel, service, user);
        }

        private static async Task WaitForAsync(Func<bool> condition,
                                               int        timeoutMs = 1000)
        {
            int waited = 0;
            while (!condition() && waited < timeoutMs)
            {
                await Task.Delay(20);
                waited += 20;
            }

            Assert.That(condition(), Is.True, "Timed out waiting for the expected state.");
        }

        private sealed class DownloadReadyCatalogService : IModCatalogService
        {
            private static readonly ModListItem item = new ModListItem
            {
                Identifier         = "download-ready",
                Name               = "Download Ready",
                Author             = "Test Author",
                Summary            = "A compatible uncached mod that can be queued for download.",
                LatestVersion      = "1.0.0",
                InstalledVersion   = "",
                DownloadCount      = 42,
                DownloadCountLabel = "42",
                IsInstalled        = false,
                HasUpdate          = false,
                IsCached           = false,
                IsIncompatible     = false,
                HasReplacement     = false,
                Compatibility      = "KSP 1.12.5",
            };

            public Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                    System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult((IReadOnlyList<ModListItem>)new[] { item });
            }

            public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                       System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new FilterOptionCounts
                {
                    Compatible   = 1,
                    Installed    = 0,
                    Updatable    = 0,
                    Replaceable  = 0,
                    Cached       = 0,
                    Uncached     = 1,
                    NotInstalled = 1,
                    Incompatible = 0,
                });
            }

            public Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                             System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ModDetailsModel?>(new ModDetailsModel
                {
                    Identifier       = item.Identifier,
                    Title            = item.Name,
                    Summary          = item.Summary,
                    Description      = "Download-ready mod details for action-state tests.",
                    Authors          = item.Author,
                    LatestVersion    = item.LatestVersion,
                    InstalledVersion = "Not installed",
                    Compatibility    = item.Compatibility,
                    ModuleKind       = "Package",
                    License          = "MIT",
                    ReleaseDate      = "2026-04-20",
                    DownloadSize     = "1 MiB",
                    DownloadCount    = item.DownloadCount,
                    IsInstalled      = item.IsInstalled,
                    HasUpdate        = item.HasUpdate,
                    HasVersionUpdate = item.HasVersionUpdate,
                    IsCached         = item.IsCached,
                    IsIncompatible   = item.IsIncompatible,
                    HasReplacement   = item.HasReplacement,
                });
            }
        }

        private sealed class AutodetectedCatalogService : IModCatalogService
        {
            public static readonly ModListItem Item = new ModListItem
            {
                Identifier         = "finalfrontier",
                Name               = "Final Frontier",
                Author             = "Nereid",
                Summary            = "Detected from loose files in GameData rather than a CKAN-managed install.",
                LatestVersion      = "0.5.9-177",
                InstalledVersion   = "Autodetected",
                DownloadCount      = 144823,
                DownloadCountLabel = "144,823",
                IsInstalled        = true,
                IsAutodetected     = true,
                HasUpdate          = false,
                HasVersionUpdate   = false,
                IsCached           = false,
                IsIncompatible     = false,
                HasReplacement     = false,
                Compatibility      = "KSP 1.12.5",
                PrimaryStateLabel  = "Installed",
                PrimaryStateColor  = "#2B6A98",
                SecondaryStateLabel = "External",
                SecondaryStateBackground = "#5A4322",
                SecondaryStateBorderBrush = "#9F7A40",
                StatusSummary      = "",
                HasStatusSummary   = false,
            };

            public Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                    System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult((IReadOnlyList<ModListItem>)new[] { Item });
            }

            public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                       System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new FilterOptionCounts
                {
                    Compatible   = 1,
                    Installed    = 1,
                    Updatable    = 0,
                    Replaceable  = 0,
                    Cached       = 0,
                    Uncached     = 0,
                    NotInstalled = 0,
                    Incompatible = 0,
                });
            }

            public Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                             System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ModDetailsModel?>(new ModDetailsModel
                {
                    Identifier          = Item.Identifier,
                    Title               = Item.Name,
                    Summary             = Item.Summary,
                    Description         = "This entry was detected from existing GameData contents. CKAN can use it for dependency resolution, but it cannot safely remove it automatically.",
                    Authors             = Item.Author,
                    LatestVersion       = Item.LatestVersion,
                    InstalledVersion    = "Autodetected",
                    Compatibility       = Item.Compatibility,
                    ModuleKind          = "Package",
                    License             = "Unknown",
                    ReleaseDate         = "Unknown",
                    DownloadSize        = "Unknown",
                    DownloadCount       = Item.DownloadCount,
                    DependencyCount     = 0,
                    RecommendationCount = 0,
                    SuggestionCount     = 0,
                    IsInstalled         = true,
                    IsAutodetected      = true,
                    HasUpdate           = false,
                    HasVersionUpdate    = false,
                    IsCached            = false,
                    IsIncompatible      = false,
                    HasReplacement      = false,
                });
            }
        }

        private sealed class AutodetectedDependencyCatalogService : IModCatalogService
        {
            public static readonly ModListItem Item = new ModListItem
            {
                Identifier         = "finalfrontier",
                Name               = "Final Frontier",
                Author             = "Nereid",
                Summary            = "Detected from loose files in GameData and reused for dependency checks.",
                LatestVersion      = "1.10.0-3485",
                InstalledVersion   = "Autodetected",
                DownloadCount      = 322298,
                DownloadCountLabel = "322,298",
                IsInstalled        = true,
                IsAutodetected     = true,
                HasUpdate          = false,
                HasVersionUpdate   = false,
                IsCached           = false,
                IsIncompatible     = true,
                HasReplacement     = false,
                Compatibility      = "1.10.0",
                PrimaryStateLabel  = "Installed",
                PrimaryStateColor  = "#2B6A98",
                SecondaryStateLabel = "External",
                SecondaryStateBackground = "#5A4322",
                SecondaryStateBorderBrush = "#9F7A40",
                TertiaryStateLabel = "Dependency",
                TertiaryStateBackground = "#31424F",
                TertiaryStateBorderBrush = "#4C6A86",
                StatusSummary      = "",
                HasStatusSummary   = false,
            };

            public Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                    System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult((IReadOnlyList<ModListItem>)new[] { Item });
            }

            public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                       System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new FilterOptionCounts
                {
                    Compatible   = 0,
                    Installed    = 1,
                    Updatable    = 0,
                    Replaceable  = 0,
                    Cached       = 0,
                    Uncached     = 0,
                    NotInstalled = 0,
                    Incompatible = 1,
                });
            }

            public Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                             System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<ModDetailsModel?>(new ModDetailsModel
                {
                    Identifier          = Item.Identifier,
                    Title               = Item.Name,
                    Summary             = Item.Summary,
                    Description         = "This entry is managed outside CKAN. CKAN can still use it to satisfy dependency checks, but it will not manage or remove it.",
                    Authors             = Item.Author,
                    LatestVersion       = Item.LatestVersion,
                    InstalledVersion    = "Autodetected",
                    Compatibility       = Item.Compatibility,
                    ModuleKind          = "Package",
                    License             = "BSD-2-clause",
                    ReleaseDate         = "2020-07-06",
                    DownloadSize        = "1.8 MiB",
                    DownloadCount       = Item.DownloadCount,
                    DependencyCount     = 0,
                    RecommendationCount = 0,
                    SuggestionCount     = 0,
                    IsInstalled         = true,
                    IsAutodetected      = true,
                    HasUpdate           = false,
                    HasVersionUpdate    = false,
                    IsCached            = false,
                    IsIncompatible      = true,
                    HasReplacement      = false,
                });
            }
        }

        private sealed class MixedQueueCatalogService : IModCatalogService
        {
            private static readonly IReadOnlyList<ModListItem> items = new[]
            {
                new ModListItem
                {
                    Identifier         = "restock",
                    Name               = "Restock",
                    Author             = "Nertea",
                    Summary            = "Refreshes stock parts with a consistent art pass.",
                    LatestVersion      = "1.5.2",
                    InstalledVersion   = "1.5.1",
                    DownloadCount      = 452318,
                    DownloadCountLabel = "452,318",
                    IsInstalled        = true,
                    HasUpdate          = true,
                    HasVersionUpdate   = true,
                    IsCached           = true,
                    IsIncompatible     = false,
                    HasReplacement     = false,
                    Compatibility      = "KSP 1.12.5",
                },
                new ModListItem
                {
                    Identifier         = "download-ready",
                    Name               = "Download Ready",
                    Author             = "Test Author",
                    Summary            = "A compatible uncached mod that can be queued for download.",
                    LatestVersion      = "1.0.0",
                    InstalledVersion   = "",
                    DownloadCount      = 42,
                    DownloadCountLabel = "42",
                    IsInstalled        = false,
                    HasUpdate          = false,
                    IsCached           = false,
                    IsIncompatible     = false,
                    HasReplacement     = false,
                    Compatibility      = "KSP 1.12.5",
                },
            };

            public Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                    System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(items);
            }

            public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                       System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new FilterOptionCounts
                {
                    Compatible   = 2,
                    Installed    = 1,
                    Updatable    = 1,
                    Replaceable  = 0,
                    Cached       = 1,
                    Uncached     = 1,
                    NotInstalled = 1,
                    Incompatible = 0,
                });
            }

            public Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                             System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Task.FromResult<ModDetailsModel?>(identifier switch
                {
                    "restock" => new ModDetailsModel
                    {
                        Identifier          = "restock",
                        Title               = "Restock",
                        Summary             = "Refreshes stock parts with a consistent art pass.",
                        Description         = "Restock details for mixed-queue tests.",
                        Authors             = "Nertea",
                        LatestVersion       = "1.5.2",
                        InstalledVersion    = "1.5.1",
                        Compatibility       = "KSP 1.12.5",
                        ModuleKind          = "Package",
                        License             = "CC-BY-NC-SA-4.0",
                        ReleaseDate         = "2025-01-14",
                        DownloadSize        = "128 MiB",
                        DownloadCount       = 452318,
                        DependencyCount     = 2,
                        RecommendationCount = 1,
                        SuggestionCount     = 0,
                        IsInstalled         = true,
                        HasUpdate           = true,
                        HasVersionUpdate    = true,
                        IsCached            = true,
                        IsIncompatible      = false,
                        HasReplacement      = false,
                    },
                    "download-ready" => new ModDetailsModel
                    {
                        Identifier          = "download-ready",
                        Title               = "Download Ready",
                        Summary             = "A compatible uncached mod that can be queued for download.",
                        Description         = "Download-ready mod details for mixed-queue tests.",
                        Authors             = "Test Author",
                        LatestVersion       = "1.0.0",
                        InstalledVersion    = "Not installed",
                        Compatibility       = "KSP 1.12.5",
                        ModuleKind          = "Package",
                        License             = "MIT",
                        ReleaseDate         = "2026-04-20",
                        DownloadSize        = "1 MiB",
                        DownloadCount       = 42,
                        DependencyCount     = 0,
                        RecommendationCount = 0,
                        SuggestionCount     = 0,
                        IsInstalled         = false,
                        HasUpdate           = false,
                        IsCached            = false,
                        IsIncompatible      = false,
                        HasReplacement      = false,
                    },
                    _ => null,
                });
            }
        }
    }
}
