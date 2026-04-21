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
                    Assert.That(viewModel.ShowCollapsedApplyResultStub, Is.True);
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
                    Assert.That(prompt, Does.Contain("queued change"));
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
        public async Task QueueDownload_AppearsAsSecondaryAction_AndCanBeCanceled()
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
                    Assert.That(viewModel.ShowSecondarySelectedModAction, Is.True);
                    Assert.That(viewModel.SecondarySelectedModActionLabel, Is.EqualTo("Queue Download"));
                });

                viewModel.QueueDownloadCommand.Execute().Subscribe(_ => { });
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.True);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Cancel Download"));
                    Assert.That(viewModel.ShowSecondarySelectedModAction, Is.False);
                });

                viewModel.PrimarySelectedModActionCommand.Execute().Subscribe(_ => { });
                await Task.Delay(75);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.HasSelectedModQueuedAction, Is.False);
                    Assert.That(viewModel.PrimarySelectedModActionLabel, Is.EqualTo("Queue Install"));
                    Assert.That(viewModel.ShowSecondarySelectedModAction, Is.True);
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
                    Assert.That(viewModel.CachedFilterLabel, Is.EqualTo("Downloaded (2)"));
                    Assert.That(viewModel.NotInstalledFilterLabel, Is.EqualTo("Not Installed (3)"));
                    Assert.That(viewModel.IncompatibleFilterLabel, Is.EqualTo("Incompatible (1)"));
                });

                viewModel.FilterCachedOnly = true;
                await Task.Delay(250);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.CachedFilterLabel, Is.EqualTo("Downloaded (2)"));
                    Assert.That(viewModel.InstalledFilterLabel, Is.EqualTo("Installed (1)"));
                    Assert.That(viewModel.NotInstalledFilterLabel, Is.EqualTo("Not Installed (2)"));
                    Assert.That(viewModel.UncachedFilterLabel, Is.EqualTo("Not Downloaded (1)"));
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
                viewModel.ActivateModFromBrowser(initiallySelected);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(initiallySelected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.False);
                });

                viewModel.ActivateModFromBrowser(initiallySelected);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(initiallySelected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
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
                    IsCached         = item.IsCached,
                    IsIncompatible   = item.IsIncompatible,
                    HasReplacement   = item.HasReplacement,
                });
            }
        }
    }
}
