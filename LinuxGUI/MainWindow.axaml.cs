using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Types;

namespace CKAN.LinuxGUI
{
    public partial class MainWindow : Window
    {
        private enum BrowserColumnResizeTarget
        {
            None,
            Metadata,
            Downloads,
            Released,
            Installed,
        }

        private readonly IAppSettingsService? appSettings;
        private bool suppressHeaderInstanceSelection;
        private MainWindowViewModel? observedViewModel;
        private ContextMenu? activeModRowMenu;
        private Window? activeOwnedDialog;
        private LinuxGuiPluginController? pluginController;
        private string? pluginControllerInstanceDir;
        private BrowserColumnResizeTarget activeBrowserColumnResizeTarget;
        private double browserColumnResizeStartX;
        private double browserColumnResizeStartMetadataWidth;
        private double browserColumnResizeStartDownloadsWidth;
        private double browserColumnResizeStartReleasedWidth;
        private double browserColumnResizeStartInstalledWidth;
        private double browserColumnResizeMaxMetadataWidth;
        private const double OverlayWheelScrollPixels = 48;
        private static readonly IBrush PreviewConflictRowBackground = Brush.Parse("#2A1820");
        private static readonly IBrush PreviewConflictRowBorder = Brush.Parse("#3C212B");
        private static readonly IBrush PreviewConflictSelectedRowBackground = Brush.Parse("#361B24");
        private static readonly IBrush PreviewConflictSelectedRowBorder = Brush.Parse("#D95A72");

        public MainWindow()
        {
            InitializeComponent();
            AddHandler(InputElement.PointerWheelChangedEvent,
                       Window_OnPointerWheelChanged,
                       RoutingStrategies.Tunnel);
            SurfaceViewToggle.AddHandler(InputElement.PointerPressedEvent,
                                         SurfaceViewToggle_OnPointerPressed,
                                         RoutingStrategies.Tunnel,
                                         true);
            Opened += OnOpened;
            Closing += OnClosing;
            PositionChanged += OnPositionChanged;
            Activated += OnActivated;
            DataContextChanged += OnDataContextChanged;
        }

        public MainWindow(MainWindowViewModel viewModel) : this(viewModel, null)
        {
        }

        public MainWindow(MainWindowViewModel viewModel,
                          IAppSettingsService? appSettingsService) : this()
        {
            DataContext = viewModel;
            appSettings = appSettingsService;
        }

        private void OnOpened(object? sender,
                              EventArgs e)
        {
            ObserveViewModel(DataContext as MainWindowViewModel);

            if (appSettings == null)
            {
                return;
            }

            var saved = appSettings.WindowState;
            if (saved.Width is double width && width > 0 && !double.IsNaN(width))
            {
                Width = width;
            }
            if (saved.Height is double height && height > 0 && !double.IsNaN(height))
            {
                Height = height;
            }
            if (saved.PositionX is int x && saved.PositionY is int y)
            {
                Position = new PixelPoint(x, y);
            }
            if (saved.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void OnClosing(object? sender,
                               WindowClosingEventArgs e)
        {
            ObserveViewModel(null);
            CloseActiveModRowMenu();
            DisposePluginController();

            if (appSettings == null)
            {
                return;
            }

            SaveBrowserState();
            SaveWindowState();
        }

        private void OnPositionChanged(object? sender,
                                       PixelPointEventArgs e)
            => CloseActiveModRowMenu();

        private void OnActivated(object? sender,
                                 EventArgs e)
            => ActivateOwnedDialog();

        private void ActivateOwnedDialog()
        {
            if (activeOwnedDialog == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (activeOwnedDialog != null)
                {
                    activeOwnedDialog.Activate();
                }
            }, DispatcherPriority.Input);
        }

        private async Task ShowOwnedDialogAsync(Window dialog)
            => await TrackOwnedDialogAsync(dialog,
                                           () => dialog.ShowDialog(this));

        private async Task<TResult> ShowOwnedDialogAsync<TResult>(Window dialog)
            => await TrackOwnedDialogAsync(dialog,
                                           () => dialog.ShowDialog<TResult>(this));

        private async Task TrackOwnedDialogAsync(Window dialog,
                                                 Func<Task> showDialogAsync)
        {
            activeOwnedDialog = dialog;
            dialog.Opened += OwnedDialog_OnOpened;
            dialog.Closed += OwnedDialog_OnClosed;

            try
            {
                await showDialogAsync();
            }
            finally
            {
                dialog.Opened -= OwnedDialog_OnOpened;
                dialog.Closed -= OwnedDialog_OnClosed;
                if (ReferenceEquals(activeOwnedDialog, dialog))
                {
                    activeOwnedDialog = null;
                }
            }
        }

        private async Task<TResult> TrackOwnedDialogAsync<TResult>(Window dialog,
                                                                   Func<Task<TResult>> showDialogAsync)
        {
            activeOwnedDialog = dialog;
            dialog.Opened += OwnedDialog_OnOpened;
            dialog.Closed += OwnedDialog_OnClosed;

            try
            {
                return await showDialogAsync();
            }
            finally
            {
                dialog.Opened -= OwnedDialog_OnOpened;
                dialog.Closed -= OwnedDialog_OnClosed;
                if (ReferenceEquals(activeOwnedDialog, dialog))
                {
                    activeOwnedDialog = null;
                }
            }
        }

        private void OwnedDialog_OnOpened(object? sender,
                                          EventArgs e)
            => ActivateOwnedDialog();

        private void OwnedDialog_OnClosed(object? sender,
                                          EventArgs e)
        {
            if (ReferenceEquals(activeOwnedDialog, sender))
            {
                activeOwnedDialog = null;
            }
        }

        private void SaveBrowserState()
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                appSettings?.SaveBrowserState(viewModel.ActiveFilterState,
                                              viewModel.ShowAdvancedFilters);
            }
        }

        private void SaveWindowState()
        {
            if (appSettings == null)
            {
                return;
            }

            var existing = appSettings.WindowState;
            double? width = existing.Width;
            double? height = existing.Height;
            int? positionX = existing.PositionX;
            int? positionY = existing.PositionY;

            if (WindowState == WindowState.Normal)
            {
                if (Width > 0 && !double.IsNaN(Width))
                {
                    width = Width;
                }
                if (Height > 0 && !double.IsNaN(Height))
                {
                    height = Height;
                }
                positionX = Position.X;
                positionY = Position.Y;
            }

            appSettings.SaveWindowState(new AppWindowState
            {
                Width       = width,
                Height      = height,
                PositionX   = positionX,
                PositionY   = positionY,
                IsMaximized = WindowState == WindowState.Maximized,
            });
        }

        private async void HeaderInstanceSwitcher_OnSelectionChanged(object? sender,
                                                                     SelectionChangedEventArgs e)
        {
            if (suppressHeaderInstanceSelection || DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            try
            {
                suppressHeaderInstanceSelection = true;
                await viewModel.TrySwitchSelectedInstanceAsync(ConfirmDiscardQueueAndSwitchAsync);
            }
            finally
            {
                suppressHeaderInstanceSelection = false;
            }
        }

        private async Task<bool> ConfirmDiscardQueueAndSwitchAsync(string prompt)
        {
            var dialog = new SimplePromptWindow(prompt,
                                                Array.Empty<string>(),
                                                "Discard Queue and Switch",
                                                "Cancel");
            return await ShowOwnedDialogAsync<int>(dialog) == 0;
        }

        private async void CompatibleGameVersionsMenuItem_OnClick(object? sender,
                                                                  Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenCompatibleGameVersionsAsync();

        private async void AboutMenuItem_OnClick(object? sender,
                                                 Avalonia.Interactivity.RoutedEventArgs e)
            => await ShowOwnedDialogAsync(new AboutWindow());

        private async Task OpenCompatibleGameVersionsAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new CompatibleGameVersionsWindow(instance);
            if (await ShowOwnedDialogAsync<bool>(dialog))
            {
                await viewModel.ApplyCompatibleGameVersionsAsync(dialog.SelectedVersions);
            }
        }

        private async void DisplayScaleMenuItem_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenDisplayScaleAsync();

        private async void SettingsMenuItem_OnClick(object? sender,
                                                    Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenSettingsAsync();

        private async Task OpenDisplayScaleAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new DisplayScaleWindow(viewModel);
            await ShowOwnedDialogAsync(dialog);
        }

        private async Task OpenSettingsAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new SettingsWindow(viewModel);

            await ShowOwnedDialogAsync(dialog);
            if (dialog.RepositoryAdded || dialog.RepositoryRemoved || dialog.RepositoryMoved)
            {
                await viewModel.RefreshCurrentStateAsync();
            }
        }

        private async void GameCommandLinesMenuItem_OnClick(object? sender,
                                                            Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenGameCommandLinesAsync();

        private async Task OpenGameCommandLinesAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new GameCommandLinesWindow(instance,
                                                    viewModel.CurrentSteamLibrary);
            if (await ShowOwnedDialogAsync<bool>(dialog))
            {
                viewModel.RefreshLaunchCommandState();
            }
        }

        private async void PluginsMenuItem_OnClick(object? sender,
                                                   Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenPluginsAsync();

        private async Task OpenPluginsAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance == null)
            {
                return;
            }

            RefreshPluginControllerForCurrentInstance(viewModel.CurrentInstance);
            if (pluginController == null)
            {
                return;
            }

            var dialog = new PluginsWindow(pluginController);
            await ShowOwnedDialogAsync(dialog);
        }

        private async void PreferredHostsMenuItem_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenPreferredHostsAsync();

        private async Task OpenPreferredHostsAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentRegistry == null)
            {
                return;
            }

            var dialog = new PreferredHostsWindow(viewModel.CurrentConfiguration,
                                                  viewModel.CurrentRegistry);
            await ShowOwnedDialogAsync<bool>(dialog);
        }

        private async void InstallationFiltersMenuItem_OnClick(object? sender,
                                                               Avalonia.Interactivity.RoutedEventArgs e)
            => await OpenInstallationFiltersAsync();

        private async Task OpenInstallationFiltersAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new InstallationFiltersWindow(viewModel.CurrentConfiguration,
                                                       instance);
            if (await ShowOwnedDialogAsync<bool>(dialog)
                && dialog.Changed)
            {
                await viewModel.RefreshCurrentStateAsync();
            }
        }

        private async void ManageGameInstancesMenuItem_OnClick(object? sender,
                                                               Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new ManageInstancesWindow(viewModel.Instances.ToList(),
                                                   viewModel.CurrentInstance?.Name,
                                                   viewModel.CurrentManager,
                                                   viewModel.CurrentUser);
            var shouldSwitch = await ShowOwnedDialogAsync<bool>(dialog);
            if (dialog.Changed)
            {
                viewModel.RefreshInstanceSummaries();
            }
            if (shouldSwitch
                && dialog.SelectedInstanceName is string instanceName)
            {
                viewModel.SelectedInstance = viewModel.Instances.FirstOrDefault(inst => inst.Name == instanceName);
                await viewModel.TrySwitchSelectedInstanceAsync(ConfirmDiscardQueueAndSwitchAsync);
            }
        }

        private async void InstallationHistoryMenuItem_OnClick(object? sender,
                                                               Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new InstallationHistoryWindow(viewModel.CurrentInstance);
            await ShowOwnedDialogAsync(dialog);
        }

        private async void InstallFromCkanMenuItem_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Install from .ckan",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("CKAN files")
                    {
                        Patterns = new[] { "*.ckan" },
                    },
                },
            });
            var paths = files.Select(file => file.TryGetLocalPath())
                             .Where(path => !string.IsNullOrWhiteSpace(path))
                             .OfType<string>()
                             .ToList();
            if (paths.Count > 0)
            {
                await viewModel.InstallFromCkanFilesAsync(paths);
            }
        }

        private async void ImportDownloadedModsMenuItem_OnClick(object? sender,
                                                                Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Downloaded Mods",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Mod archives")
                    {
                        Patterns = new[] { "*.zip" },
                    },
                },
            });
            var paths = files.Select(file => file.TryGetLocalPath())
                             .Where(path => !string.IsNullOrWhiteSpace(path))
                             .OfType<string>()
                             .ToList();
            if (paths.Count > 0)
            {
                await viewModel.ImportDownloadedModsAsync(paths);
            }
        }

        private async void DeduplicateInstalledFilesMenuItem_OnClick(object? sender,
                                                                     Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                var result = await viewModel.DeduplicateInstalledFilesAsync();
                await ShowOwnedDialogAsync(
                    new MessageDialogWindow("Deduplicate Installed Files", result));
            }
        }

        private async void ExportModListMenuItem_OnClick(object? sender,
                                                         Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Installed Mod List",
                SuggestedFileName = $"installed-{viewModel.CurrentInstance?.SanitizedName ?? "mods"}.txt",
                DefaultExtension = "txt",
                FileTypeChoices = InstalledModListFileTypes,
            });
            var path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await viewModel.ExportInstalledModListAsync(path, ExportTypeForPath(path));
            }
        }

        private async void ExportModpackMenuItem_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Re-importable Modpack",
                SuggestedFileName = $"modpack-{viewModel.CurrentInstance?.SanitizedName ?? "instance"}.ckan",
                DefaultExtension = "ckan",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CKAN modpack")
                    {
                        Patterns = new[] { "*.ckan" },
                    },
                },
            });
            var path = file?.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await viewModel.ExportModpackAsync(path);
            }
        }

        private async void AuditRecommendationsMenuItem_OnClick(object? sender,
                                                                Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance == null)
            {
                return;
            }

            var items = await viewModel.AuditRecommendationsAsync();
            if (items.Count == 0)
            {
                return;
            }

            var dialog = new RecommendationAuditWindow(items, viewModel.CurrentInstance.Name);
            if (await ShowOwnedDialogAsync<bool>(dialog))
            {
                viewModel.QueueRecommendationAuditSelections(dialog.SelectedItems);
            }
        }

        private async void DownloadStatisticsMenuItem_OnClick(object? sender,
                                                              Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new DownloadStatisticsWindow(viewModel.CurrentCache,
                                                      viewModel.CurrentRegistry);
            await ShowOwnedDialogAsync(dialog);
        }

        private async void PlayTimeMenuItem_OnClick(object? sender,
                                                    Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new PlayTimeWindow(viewModel.KnownGameInstances);
            await ShowOwnedDialogAsync(dialog);
        }

        private async void UnmanagedFilesMenuItem_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new UnmanagedFilesWindow(viewModel.CurrentInstance,
                                                  viewModel.CurrentRegistry);
            await ShowOwnedDialogAsync(dialog);
        }

        private void ExitMenuItem_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private static IReadOnlyList<FilePickerFileType> InstalledModListFileTypes { get; } = new[]
        {
            new FilePickerFileType("Plain text")
            {
                Patterns = new[] { "*.txt" },
            },
            new FilePickerFileType("Markdown")
            {
                Patterns = new[] { "*.md" },
            },
            new FilePickerFileType("BBCode")
            {
                Patterns = new[] { "*.bbcode", "*.txt" },
            },
            new FilePickerFileType("CSV")
            {
                Patterns = new[] { "*.csv" },
            },
            new FilePickerFileType("TSV")
            {
                Patterns = new[] { "*.tsv" },
            },
        };

        private static ExportFileType ExportTypeForPath(string path)
            => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".md"     => ExportFileType.Markdown,
                ".bbcode" => ExportFileType.BbCode,
                ".csv"    => ExportFileType.Csv,
                ".tsv"    => ExportFileType.Tsv,
                _         => ExportFileType.PlainText,
            };

        private void SelectedModResourceLinkButton_OnClick(object? sender,
                                                           Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Control control
                && control.DataContext is ModResourceLinkItem link
                && DataContext is MainWindowViewModel viewModel)
            {
                viewModel.OpenSelectedModResourceLinkFromUi(link);
            }
        }

        private void PreviewConflictRowButton_OnClick(object? sender,
                                                      RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.DataContext is PreviewConflictChoiceItem choice
                && DataContext is MainWindowViewModel viewModel)
            {
                var selected = viewModel.TogglePreviewConflictSelection(choice.ConflictText);
                button.Classes.Set("selected", selected);
                SetPreviewConflictRowVisual(button, selected);
            }
            e.Handled = true;
        }

        private void PreviewConflictViewButton_OnClick(object? sender,
                                                       RoutedEventArgs e)
        {
            if (sender is Control control
                && control.DataContext is PreviewConflictChoiceItem choice
                && DataContext is MainWindowViewModel viewModel)
            {
                viewModel.ViewPreviewConflictInBrowser(choice);
            }
            e.Handled = true;
        }

        private static void SetPreviewConflictRowVisual(Button button,
                                                        bool selected)
        {
            var row = button.GetVisualDescendants()
                            .OfType<Border>()
                            .FirstOrDefault(border => border.Classes.Contains("preview-conflict-row"));
            if (row == null)
            {
                return;
            }

            row.Background = selected
                ? PreviewConflictSelectedRowBackground
                : PreviewConflictRowBackground;
            row.BorderBrush = selected
                ? PreviewConflictSelectedRowBorder
                : PreviewConflictRowBorder;
        }

        private void ModRow_OnPointerPressed(object? sender,
                                             PointerPressedEventArgs e)
        {
            if (sender is not Control control
                || control.DataContext is not ModListItem mod
                || DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var updateKind = e.GetCurrentPoint(control).Properties.PointerUpdateKind;
            if (updateKind == PointerUpdateKind.LeftButtonPressed)
            {
                CloseActiveModRowMenu();
                viewModel.ActivateModFromBrowser(mod);
                ModsListBox.Focus();
                e.Handled = true;
                return;
            }

            if (updateKind == PointerUpdateKind.MiddleButtonPressed)
            {
                CloseActiveModRowMenu();
                viewModel.SelectedMod = null;
                ModsListBox.Focus();
                e.Handled = true;
                return;
            }

            if (updateKind != PointerUpdateKind.RightButtonPressed)
            {
                return;
            }

            viewModel.SelectedMod = mod;
            ModsListBox.Focus();
            CloseActiveModRowMenu();

            var menu = new ContextMenu
            {
                Placement = PlacementMode.Pointer,
            };
            menu.Classes.Add("mod-row-menu");
            menu.Closed += ModRowMenu_OnClosed;

            if (viewModel.ShowQueueContextAction(mod))
            {
                var queueItem = new MenuItem
                {
                    Header = viewModel.QueueContextLabel(mod),
                };
                queueItem.Click += (_, _) => viewModel.ToggleQueueActionFromBrowser(mod);
                queueItem.Classes.Add("mod-row-menu-item");
                menu.Items.Add(queueItem);
            }

            if (viewModel.ShowDownloadOnlyContextAction(mod))
            {
                var downloadOnlyItem = new MenuItem
                {
                    Header = viewModel.DownloadOnlyContextLabel(mod),
                };
                downloadOnlyItem.Click += (_, _) => viewModel.ToggleDownloadOnlyFromBrowser(mod);
                downloadOnlyItem.Classes.Add("mod-row-menu-item");
                menu.Items.Add(downloadOnlyItem);
            }

            if (viewModel.ShowPurgeCacheContextAction(mod))
            {
                var purgeCacheItem = new MenuItem
                {
                    Header = viewModel.PurgeCacheContextLabel(mod),
                };
                purgeCacheItem.Click += (_, _) => viewModel.PurgeCacheFromBrowser(mod);
                purgeCacheItem.Classes.Add("mod-row-menu-item");
                menu.Items.Add(purgeCacheItem);
            }

            var toggleDetailsItem = new MenuItem
            {
                Header = viewModel.DetailsPaneToggleLabel,
                Command = viewModel.ToggleDetailsPaneCommand,
            };
            toggleDetailsItem.Classes.Add("mod-row-menu-item");
            menu.Items.Add(toggleDetailsItem);

            activeModRowMenu = menu;
            menu.Open(control);
            e.Handled = true;
        }

        private void ModsListBox_OnPointerPressed(object? sender,
                                                  PointerPressedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var updateKind = e.GetCurrentPoint(ModsListBox).Properties.PointerUpdateKind;
            if (updateKind != PointerUpdateKind.MiddleButtonPressed)
            {
                return;
            }

            CloseActiveModRowMenu();
            viewModel.SelectedMod = null;
            ModsListBox.Focus();
            e.Handled = true;
        }

        private void SurfaceViewToggle_OnPointerPressed(object? sender,
                                                        PointerPressedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var source = sender as Control ?? this;
            var updateKind = e.GetCurrentPoint(source).Properties.PointerUpdateKind;
            if (updateKind != PointerUpdateKind.RightButtonPressed)
            {
                return;
            }

            viewModel.ToggleSurfaceViewTogglePinned();
            e.Handled = true;
        }

        private void BrowserColumnResizeHandle_OnPointerPressed(object? sender,
                                                                 PointerPressedEventArgs e)
        {
            if (sender is not Control control
                || DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var updateKind = e.GetCurrentPoint(control).Properties.PointerUpdateKind;
            if (updateKind != PointerUpdateKind.LeftButtonPressed)
            {
                return;
            }

            var target = BrowserColumnResizeTargetFor(control.Tag as string);
            if (target == BrowserColumnResizeTarget.None)
            {
                return;
            }

            activeBrowserColumnResizeTarget = target;
            browserColumnResizeStartX = e.GetPosition(this).X;
            browserColumnResizeStartMetadataWidth = viewModel.BrowserMetadataColumnWidth;
            browserColumnResizeStartDownloadsWidth = viewModel.BrowserDownloadsColumnWidth;
            browserColumnResizeStartReleasedWidth = viewModel.BrowserReleasedColumnWidth;
            browserColumnResizeStartInstalledWidth = viewModel.BrowserInstalledColumnWidth;
            browserColumnResizeMaxMetadataWidth = viewModel.BrowserColumnResizeMaxMetadataWidth(ModsListBox.Bounds.Width);
            e.Pointer.Capture(control);
            e.Handled = true;
        }

        private void BrowserColumnResizeHandle_OnPointerMoved(object? sender,
                                                              PointerEventArgs e)
        {
            if (activeBrowserColumnResizeTarget == BrowserColumnResizeTarget.None
                || DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var delta = e.GetPosition(this).X - browserColumnResizeStartX;
            switch (activeBrowserColumnResizeTarget)
            {
                case BrowserColumnResizeTarget.Metadata:
                    viewModel.ResizeBrowserNameDownloadsDivider(browserColumnResizeStartMetadataWidth,
                                                                 browserColumnResizeStartDownloadsWidth,
                                                                 browserColumnResizeStartReleasedWidth,
                                                                 browserColumnResizeStartInstalledWidth,
                                                                 browserColumnResizeMaxMetadataWidth,
                                                                 delta);
                    break;

                case BrowserColumnResizeTarget.Downloads:
                    viewModel.ResizeBrowserDownloadsReleasedDivider(browserColumnResizeStartMetadataWidth,
                                                                    browserColumnResizeStartDownloadsWidth,
                                                                    browserColumnResizeStartReleasedWidth,
                                                                    browserColumnResizeStartInstalledWidth,
                                                                    browserColumnResizeMaxMetadataWidth,
                                                                    delta);
                    break;

                case BrowserColumnResizeTarget.Released:
                    viewModel.ResizeBrowserReleasedInstalledDivider(browserColumnResizeStartMetadataWidth,
                                                                    browserColumnResizeStartDownloadsWidth,
                                                                    browserColumnResizeStartReleasedWidth,
                                                                    browserColumnResizeStartInstalledWidth,
                                                                    browserColumnResizeMaxMetadataWidth,
                                                                    delta);
                    break;

                case BrowserColumnResizeTarget.Installed:
                    viewModel.ResizeBrowserInstalledVersionDivider(browserColumnResizeStartMetadataWidth,
                                                                   browserColumnResizeStartDownloadsWidth,
                                                                   browserColumnResizeStartReleasedWidth,
                                                                   browserColumnResizeStartInstalledWidth,
                                                                   browserColumnResizeMaxMetadataWidth,
                                                                   delta);
                    break;
            }

            e.Handled = true;
        }

        private void BrowserColumnResizeHandle_OnPointerReleased(object? sender,
                                                                 PointerReleasedEventArgs e)
        {
            if (activeBrowserColumnResizeTarget == BrowserColumnResizeTarget.None)
            {
                return;
            }

            activeBrowserColumnResizeTarget = BrowserColumnResizeTarget.None;
            e.Pointer.Capture(null);
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.CommitBrowserColumnLayout();
            }

            e.Handled = true;
        }

        private static BrowserColumnResizeTarget BrowserColumnResizeTargetFor(string? tag)
            => tag switch
            {
                "Metadata"  => BrowserColumnResizeTarget.Metadata,
                "Downloads" => BrowserColumnResizeTarget.Downloads,
                "Released"  => BrowserColumnResizeTarget.Released,
                "Installed" => BrowserColumnResizeTarget.Installed,
                _           => BrowserColumnResizeTarget.None,
            };

        private void Window_OnPointerWheelChanged(object? sender,
                                                  PointerWheelEventArgs e)
        {
            if (DataContext is not MainWindowViewModel { ShowAdvancedFilters: true }
                || PointerIsInsideAdvancedFiltersPopup(e)
                || !PointerIsInsideModsList(e))
            {
                return;
            }

            var scrollViewer = GetModListScrollViewer();
            if (scrollViewer == null)
            {
                return;
            }

            double maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            if (maxOffsetY <= 0)
            {
                return;
            }

            double offsetY = Math.Clamp(scrollViewer.Offset.Y - (e.Delta.Y * OverlayWheelScrollPixels),
                                        0,
                                        maxOffsetY);
            if (Math.Abs(offsetY - scrollViewer.Offset.Y) < 0.01)
            {
                return;
            }

            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, offsetY);
            e.Handled = true;
        }

        private bool PointerIsInsideAdvancedFiltersPopup(PointerEventArgs e)
        {
            if (AdvancedFiltersPopup.Child is not Visual popupChild
                || e.Source is not Visual source)
            {
                return false;
            }

            return ReferenceEquals(source, popupChild)
                   || popupChild.IsVisualAncestorOf(source);
        }

        private bool PointerIsInsideModsList(PointerEventArgs e)
        {
            var point = e.GetPosition(ModsListBox);
            return new Rect(ModsListBox.Bounds.Size).Contains(point);
        }

        private void Window_OnKeyDown(object? sender,
                                      KeyEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            bool editableTextFocused = IsEditableTextFocused();

            if (e.Key == Key.Escape)
            {
                if (activeModRowMenu != null)
                {
                    CloseActiveModRowMenu();
                    e.Handled = true;
                    return;
                }

                if (viewModel.ShowAdvancedFilters)
                {
                    viewModel.ShowAdvancedFilters = false;
                    e.Handled = true;
                    return;
                }

                if (viewModel.ShowDisplaySettings)
                {
                    viewModel.ShowDisplaySettings = false;
                    e.Handled = true;
                    return;
                }

                if (!editableTextFocused && viewModel.SelectedMod != null)
                {
                    viewModel.SelectedMod = null;
                    e.Handled = true;
                }

                return;
            }

            if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
            {
                if (editableTextFocused)
                {
                    return;
                }

                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.B)
            {
                if (editableTextFocused)
                {
                    return;
                }

                viewModel.ToggleQueueDrawerCommand.Execute().Subscribe(_ => { });
                e.Handled = true;
            }
        }

        private void CloseActiveModRowMenu()
        {
            if (activeModRowMenu == null)
            {
                return;
            }

            var menu = activeModRowMenu;
            activeModRowMenu = null;
            menu.Closed -= ModRowMenu_OnClosed;
            menu.Close();
        }

        private void ModRowMenu_OnClosed(object? sender,
                                         EventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                menu.Closed -= ModRowMenu_OnClosed;
                if (ReferenceEquals(activeModRowMenu, menu))
                {
                    activeModRowMenu = null;
                }
            }
        }

        private bool IsEditableTextFocused()
            => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;

        private void OnDataContextChanged(object? sender,
                                          EventArgs e)
            => ObserveViewModel(DataContext as MainWindowViewModel);

        private void ObserveViewModel(MainWindowViewModel? viewModel)
        {
            if (ReferenceEquals(observedViewModel, viewModel))
            {
                return;
            }

            if (observedViewModel != null)
            {
                observedViewModel.RecommendationSelectionPromptAsync = null;
                observedViewModel.ConfirmIncompatibleLaunchAsync = null;
                observedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            }

            observedViewModel = viewModel;

            if (observedViewModel != null)
            {
                observedViewModel.RecommendationSelectionPromptAsync = ShowRecommendationSelectionAsync;
                observedViewModel.ConfirmIncompatibleLaunchAsync = ConfirmIncompatibleLaunchAsync;
                observedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
                CKAN.GUI.Main.SetInstance(observedViewModel.CurrentManager,
                                          observedViewModel.CurrentUser);
                RefreshPluginControllerForCurrentInstance(observedViewModel.CurrentInstance);
            }
            else
            {
                CKAN.GUI.Main.ClearInstance();
                DisposePluginController();
            }
        }

        private async Task<IReadOnlyList<RecommendationAuditItem>?> ShowRecommendationSelectionAsync(
            IReadOnlyList<RecommendationAuditItem> items)
        {
            if (items.Count == 0 || DataContext is not MainWindowViewModel viewModel)
            {
                return Array.Empty<RecommendationAuditItem>();
            }

            var dialog = new RecommendationAuditWindow(items,
                                                       viewModel.CurrentInstance?.Name ?? "current instance")
            {
                Title = "Choose Recommended Mods",
            };
            return await ShowOwnedDialogAsync<bool>(dialog)
                ? dialog.SelectedItems
                : null;
        }

        private async Task<bool> ConfirmIncompatibleLaunchAsync(string prompt)
        {
            var dialog = new SimplePromptWindow(prompt,
                                                new[] { "Launch anyway" },
                                                "Launch",
                                                "Cancel");
            return await ShowOwnedDialogAsync<int>(dialog) == 0;
        }

        private void ViewModel_OnPropertyChanged(object? sender,
                                                 PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ModListScrollResetRequestId))
            {
                ResetModListScrollToTop();
            }
            else if (e.PropertyName == nameof(MainWindowViewModel.CurrentInstance)
                     && sender is MainWindowViewModel viewModel)
            {
                CKAN.GUI.Main.SetInstance(viewModel.CurrentManager,
                                          viewModel.CurrentUser);
                RefreshPluginControllerForCurrentInstance(viewModel.CurrentInstance);
            }
        }

        private void ResetModListScrollToTop()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = GetModListScrollViewer();
                if (scrollViewer != null)
                {
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, 0);
                }
            }, DispatcherPriority.Background);
        }

        private ScrollViewer? GetModListScrollViewer()
            => ModsListBox.GetVisualDescendants()
                          .OfType<ScrollViewer>()
                          .FirstOrDefault();

        private void RefreshPluginControllerForCurrentInstance(GameInstance? instance)
        {
            if (instance == null)
            {
                DisposePluginController();
                return;
            }

            var instanceDir = instance.GameDir;
            if (string.IsNullOrWhiteSpace(instanceDir))
            {
                DisposePluginController();
                return;
            }

            if (pluginController != null
                && string.Equals(pluginControllerInstanceDir, instanceDir, StringComparison.Ordinal))
            {
                return;
            }

            DisposePluginController();
            pluginController = new LinuxGuiPluginController(instance);
            pluginControllerInstanceDir = instanceDir;
        }

        private void DisposePluginController()
        {
            pluginController?.Dispose();
            pluginController = null;
            pluginControllerInstanceDir = null;
        }
    }
}
