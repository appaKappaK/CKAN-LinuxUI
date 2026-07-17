using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

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
        public void ProviderChoicePrompt_RendersReadableSurface()
        {
            var window = new SimplePromptWindow(
                "KSPBurst Compiler requires dependency: KSPBurst\n\nPick one compatible provider to install.",
                new[]
                {
                    "KSPBurst (KSPBurst Compiler)\nVersion 1.5.5 | cached | 42,100 downloads | direct identifier match\nOptimized Burst compiler support for high-performance KSP mods.",
                    "KSPBurst-Lite (KSPBurst Lite)\nVersion 1.5.5-lite | not cached | 9,875 downloads\nSmaller provider package for installs that only need the core runtime.",
                })
            {
                Width = 660,
            };

            VisualTestSupport.CaptureAndAssert(window, "prompt-provider-choice");
        }

        [AvaloniaTest]
        public void OverwritePrompt_KeepsActionsVisible()
        {
            var fileList = string.Join(Environment.NewLine,
                                       Enumerable.Range(1, 18)
                                                 .Select(index => $"- GameData/ExamplePack/Parts/Part-{index:00}.cfg  ({(index % 3 == 0 ? "DIFFERENT" : "same")})"));
            var window = new SimplePromptWindow(
                $"Module Example Pack wants to overwrite the following manually installed files:{Environment.NewLine}{Environment.NewLine}{fileList}{Environment.NewLine}{Environment.NewLine}Overwrite?",
                Array.Empty<string>(),
                "Yes",
                "No")
            {
                Width = 660,
            };

            VisualTestSupport.CaptureAndAssert(window, "prompt-overwrite-files");
        }

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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
                Assert.That(listBox.SelectionMode.HasFlag(SelectionMode.Multiple), Is.True);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTest]
        public async Task ModBrowser_SupportsShiftRangeAndControlToggleSelection()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var viewModel = new MainWindowViewModel(settings,
                                                    service,
                                                    catalog,
                                                    search,
                                                    changes,
                                                    actions,
                                                    new FakeDisabledModService(),
                                                    new AvaloniaUser());
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            viewModel.FilterInstalledState = null;
            window.Show();

            try
            {
                await WaitForAsync(() => viewModel.Mods.Count == 5);
                var listBox = window.FindControl<ListBox>("ModsListBox")!;
                await WaitForAsync(() => listBox.ContainerFromIndex(4) != null);

                ClickListItem(window, listBox, 0, RawInputModifiers.None);
                ClickListItem(window, listBox, 3, RawInputModifiers.Shift);

                Assert.Multiple(() =>
                {
                    Assert.That(listBox.SelectedItems, Has.Count.EqualTo(4));
                    Assert.That(viewModel.SelectedModCount, Is.EqualTo(4));
                });

                ClickListItem(window, listBox, 1, RawInputModifiers.Control);

                Assert.Multiple(() =>
                {
                    Assert.That(listBox.SelectedItems, Has.Count.EqualTo(3));
                    Assert.That(viewModel.SelectedModCount, Is.EqualTo(3));
                    Assert.That(listBox.SelectedItems, Does.Not.Contain(viewModel.Mods[1]));
                });

                var selectedIdentifiers = listBox.SelectedItems!
                                                 .OfType<CKAN.App.Models.ModListItem>()
                                                 .Select(mod => mod.Identifier)
                                                 .ToArray();
                viewModel.SelectedSortOption = viewModel.SortOptions.First(option =>
                    option.Value == CKAN.App.Models.ModSortOption.UpdatesFirst);
                await Task.Delay(100);

                Assert.That(listBox.SelectedItems!
                                       .OfType<CKAN.App.Models.ModListItem>()
                                       .Select(mod => mod.Identifier),
                            Is.EquivalentTo(selectedIdentifiers));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTest]
        public async Task MoreFiltersPopup_AllowsModListWheelScrolling()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 700,
            };

            window.Show();
            await Task.Delay(300);
            viewModel.FilterInstalledState = null;
            await WaitForAsync(() => viewModel.Mods.Count == 5);
            viewModel.ShowAdvancedFilters = true;
            await Task.Delay(100);

            try
            {
                var listBox = window.FindControl<ListBox>("ModsListBox");
                Assert.That(listBox, Is.Not.Null);
                var modsListBox = listBox!;
                modsListBox.Height = 160;
                await Task.Delay(50);

                var scrollViewer = modsListBox.GetVisualDescendants()
                                              .OfType<ScrollViewer>()
                                              .FirstOrDefault();
                Assert.That(scrollViewer, Is.Not.Null);
                Assert.That(scrollViewer!.Extent.Height, Is.GreaterThan(scrollViewer.Viewport.Height));

                double initialOffset = scrollViewer.Offset.Y;
                var listPoint = new Point(modsListBox.Bounds.Width * 0.25,
                                          Math.Min(160, modsListBox.Bounds.Height - 20));
                var windowPoint = modsListBox.TranslatePoint(listPoint, window)
                                  ?? throw new InvalidOperationException("Could not translate list point to window.");

                window.MouseWheel(windowPoint, new Vector(0, -1), RawInputModifiers.None);
                await Task.Delay(50);

                Assert.That(scrollViewer.Offset.Y, Is.GreaterThan(initialOffset));
                Assert.That(viewModel.ShowAdvancedFilters, Is.True);
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
        public async Task ReadyShell_NarrowWindow_WithSelectedModDetails_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var settings = new FakeAppSettingsService();
            var catalog = new FakeModCatalogService();
            var search = new ModSearchService(settings);
            var changes = new ChangesetService();
            var actions = new FakeModActionService(changes);
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1040,
                Height = 700,
            };

            await Task.Delay(150);
            viewModel.ShowDetailsPane = true;
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "browser-details-narrow");
        }

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
                new FakeDisabledModService(),
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.FilterInstalledState = null;
            await WaitForAsync(() => viewModel.Mods.Count == 5);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.FilterInstalledState = null;
            await WaitForAsync(() => viewModel.Mods.Count == 5);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
                    LeftoverConfigDirectories = new[]
                    {
                        "/tmp/visual-test/GameData/Restock/PluginData",
                    },
                });
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
                    LeftoverConfigDirectories = new[]
                    {
                        "/tmp/visual-test/GameData/Restock/PluginData",
                    },
                });
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
            var window = new MainWindow(viewModel, settings)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(180);
            viewModel.SelectedMod = viewModel.Mods.First(mod => mod.Identifier == "restock");
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
            var viewModel = new MainWindowViewModel(settings, service, catalog, search, changes, actions, new FakeDisabledModService(), user);
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

        private static void ClickListItem(Window            window,
                                          ListBox           listBox,
                                          int               index,
                                          RawInputModifiers modifiers)
        {
            var item = (Control)listBox.ContainerFromIndex(index)!;
            var point = item.TranslatePoint(new Point(20, item.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left, modifiers | RawInputModifiers.LeftMouseButton);
            window.MouseUp(point, MouseButton.Left, modifiers);
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
            public Registry? CurrentRegistry => inner.CurrentRegistry;
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
            public RegistryManager? AcquireWriteRegistryManager()
                => inner.AcquireWriteRegistryManager();
            public void RefreshCurrentRegistry()
                => inner.RefreshCurrentRegistry();
            public void ReloadCurrentRegistry()
                => inner.ReloadCurrentRegistry();
            public void Dispose() => inner.Dispose();
        }
    }
}
