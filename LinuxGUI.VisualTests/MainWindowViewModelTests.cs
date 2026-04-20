using System;
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

                Assert.That(viewModel.MoreFiltersLabel, Is.EqualTo("More Filters ▾"));

                viewModel.AdvancedAuthorFilter = "Nertea";
                viewModel.AdvancedCompatibilityFilter = "1.12";
                viewModel.FilterHasReplacementOnly = true;

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.ActiveAdvancedFilterCount, Is.EqualTo(3));
                    Assert.That(viewModel.MoreFiltersLabel, Is.EqualTo("More Filters (3)"));
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
    }
}
