using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;

using NUnit.Framework;

using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    [TestFixture]
    public sealed class StartupVisualTests
    {
        [AvaloniaTest]
        public Task LoadingShell_Renders()
            => RenderScenarioAsync(VisualScenario.Loading, "startup-loading");

        [AvaloniaTest]
        public Task EmptyShell_Renders()
            => RenderScenarioAsync(VisualScenario.Empty, "startup-empty");

        [AvaloniaTest]
        public Task SelectionRequiredShell_Renders()
            => RenderScenarioAsync(VisualScenario.SelectionRequired, "startup-selection-required");

        [AvaloniaTest]
        public Task ReadyShell_Renders()
            => RenderScenarioAsync(VisualScenario.Ready, "startup-ready");

        [AvaloniaTest]
        public async Task ReadyShell_DoesNotAutoScrollToSelectedMod()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            window.Show();

            try
            {
                var listBox = window.FindControl<ListBox>("ModsListBox");
                Assert.That(listBox, Is.Not.Null);
                Assert.That(listBox!.AutoScrollToSelectedItem, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTest]
        public Task ReadyShell_NarrowWindow_Renders()
            => RenderScenarioAsync(VisualScenario.Ready, "startup-ready-narrow", 1040, 700);

        [AvaloniaTest]
        public Task ErrorShell_Renders()
            => RenderScenarioAsync(VisualScenario.Error, "startup-error");

        private static async Task RenderScenarioAsync(VisualScenario scenario,
                                                      string         snapshotName,
                                                      double         width = 1200,
                                                      double         height = 760)
        {
            using var service = new FakeGameInstanceService(scenario);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(
                settings,
                scenario == VisualScenario.Error
                    ? new ErrorGameInstanceServiceWrapper(service)
                    : service,
                catalog,
                search,
                changes,
                actions,
                user);
            var window = new MainWindow(viewModel, settings);

            window.Width = width;
            window.Height = height;

            await Task.Delay(scenario == VisualScenario.Loading ? 40 : 150);
            VisualTestSupport.CaptureAndAssert(window, snapshotName);
        }

        [AvaloniaTest]
        public async Task FilteredBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.ModSearchText = "planet";
            viewModel.FilterCachedOnly = true;
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "browser-filtered");
        }

        [AvaloniaTest]
        public async Task AdvancedFilteredBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.ShowAdvancedFilters = true;
            viewModel.AdvancedAuthorFilter = "Nertea";
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "browser-advanced-filters");
        }

        [AvaloniaTest]
        public async Task SortedBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedSortOption = viewModel.SortOptions.First(opt => opt.Value == CKAN.App.Models.ModSortOption.UpdatesFirst);
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "browser-sorted");
        }

        [AvaloniaTest]
        public async Task QueuedBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            viewModel.QueueUpdateCommand.Execute().Subscribe();
            await Task.Delay(200);

            VisualTestSupport.CaptureAndAssert(window, "browser-queued");
        }

        [AvaloniaTest]
        public async Task EmptyPreview_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.ShowPreviewSurfaceCommand.Execute().Subscribe();
            await Task.Delay(200);

            VisualTestSupport.CaptureAndAssert(window, "preview-empty");
        }

        [AvaloniaTest]
        public async Task QueuedPreview_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            viewModel.QueueUpdateCommand.Execute().Subscribe();
            viewModel.ShowPreviewSurfaceCommand.Execute().Subscribe();
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "preview-queued");
        }

        [AvaloniaTest]
        public async Task ApplyingPreview_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(
                changes,
                new CKAN.App.Models.ApplyChangesResult
                {
                    Kind = CKAN.App.Models.ApplyResultKind.Success,
                    Success = true,
                    Title = "Apply Completed",
                    Message = "Applied 1 queued action.",
                    SummaryLines = new[]
                    {
                        "1 queued action",
                        "1 direct removal",
                    },
                },
                applyDelayMs: 1200);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            viewModel.QueueRemoveCommand.Execute().Subscribe();
            viewModel.ShowPreviewSurfaceCommand.Execute().Subscribe();
            await Task.Delay(300);
            viewModel.ApplyChangesCommand.Execute().Subscribe();
            await Task.Delay(120);

            VisualTestSupport.CaptureAndAssert(window, "preview-applying");

            await Task.Delay(1300);
        }

        [AvaloniaTest]
        public async Task AppliedBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(
                changes,
                new CKAN.App.Models.ApplyChangesResult
                {
                    Kind = CKAN.App.Models.ApplyResultKind.Warning,
                    Success = true,
                    Title = "Apply Completed with Follow-Up",
                    Message = "Applied 1 queued action. Kept 1 config-only directory for manual review.",
                    SummaryLines = new[]
                    {
                        "1 queued action",
                        "1 direct update",
                        "1 dependency install",
                    },
                    FollowUpLines = new[]
                    {
                        "Review leftover config-only directory: GameData/Restock/PluginData",
                    },
                });
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            viewModel.QueueUpdateCommand.Execute().Subscribe();
            await Task.Delay(200);
            viewModel.ApplyChangesCommand.Execute().Subscribe();
            await Task.Delay(300);

            VisualTestSupport.CaptureAndAssert(window, "browser-applied");
        }

        [AvaloniaTest]
        public async Task AppliedPreview_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(
                changes,
                new CKAN.App.Models.ApplyChangesResult
                {
                    Kind = CKAN.App.Models.ApplyResultKind.Warning,
                    Success = true,
                    Title = "Apply Completed with Follow-Up",
                    Message = "Applied 1 queued action. Kept 1 config-only directory for manual review.",
                    SummaryLines = new[]
                    {
                        "1 queued action",
                        "1 direct update",
                        "1 dependency install",
                    },
                    FollowUpLines = new[]
                    {
                        "Review leftover config-only directory: GameData/Restock/PluginData",
                    },
                });
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            viewModel.QueueUpdateCommand.Execute().Subscribe();
            viewModel.ShowPreviewSurfaceCommand.Execute().Subscribe();
            await Task.Delay(300);
            viewModel.ApplyChangesCommand.Execute().Subscribe();
            await Task.Delay(300);

            VisualTestSupport.CaptureAndAssert(window, "preview-applied");
        }

        [AvaloniaTest]
        public async Task DisplayScaleSettings_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.ShowDisplaySettings = true;
            viewModel.PendingUiScalePercent = 90;
            await Task.Delay(250);

            VisualTestSupport.CaptureAndAssert(window, "browser-display-scale");
        }

        [AvaloniaTest]
        public async Task CatalogLoadingSkeleton_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new DelayedModCatalogService(listDelayMs: 1500);
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await WaitForAsync(() => viewModel.ShowCatalogSkeleton);

            VisualTestSupport.CaptureAndAssert(window, "browser-loading");
        }

        [AvaloniaTest]
        public async Task DetailsLoadingState_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new DelayedModCatalogService(detailsDelayMs: 300);
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(180);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "parallax");
            await WaitForAsync(() => viewModel.IsSelectedModLoading);

            VisualTestSupport.CaptureAndAssert(window, "browser-details-loading");
        }

        [AvaloniaTest]
        public async Task DownloadsSortToggle_KeepsSelectedModAndDetailsPaneOpen()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            window.Show();

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count > 0 && viewModel.SelectedMod != null);

                var selected = viewModel.SelectedMod!;
                viewModel.ActivateModFromBrowser(selected);
                await Task.Delay(50);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(selected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
                });

                viewModel.SelectPopularitySortCommand.Execute().Subscribe();
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(selected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
                });

                viewModel.SelectPopularitySortCommand.Execute().Subscribe();
                await Task.Delay(100);

                Assert.Multiple(() =>
                {
                    Assert.That(viewModel.SelectedMod?.Identifier, Is.EqualTo(selected.Identifier));
                    Assert.That(viewModel.ShowDetailsPane, Is.True);
                });
            }
            finally
            {
                window.Close();
            }
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

            Assert.That(condition(), Is.True, "Timed out waiting for the expected visual state.");
        }

        private sealed class ErrorGameInstanceServiceWrapper : CKAN.App.Services.IGameInstanceService
        {
            private readonly FakeGameInstanceService inner;

            public ErrorGameInstanceServiceWrapper(FakeGameInstanceService inner)
            {
                this.inner = inner;
            }

            public GameInstanceManager Manager => inner.Manager;
            public RepositoryDataManager RepositoryData => inner.RepositoryData;
            public CKAN.Configuration.IConfiguration Configuration => inner.Configuration;
            public GameInstance? CurrentInstance => inner.CurrentInstance;
            public RegistryManager? CurrentRegistryManager => inner.CurrentRegistryManager;
            public System.Collections.Generic.IReadOnlyList<CKAN.App.Models.InstanceSummary> Instances => inner.Instances;
            public event Action<GameInstance?>? CurrentInstanceChanged
            {
                add    => inner.CurrentInstanceChanged += value;
                remove => inner.CurrentInstanceChanged -= value;
            }
            public Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
                => inner.InitializeErrorAsync(cancellationToken);
            public Task SetCurrentInstanceAsync(string name, System.Threading.CancellationToken cancellationToken)
                => inner.SetCurrentInstanceAsync(name, cancellationToken);
            public void Dispose() => inner.Dispose();
        }
    }
}
