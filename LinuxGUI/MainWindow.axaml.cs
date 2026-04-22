using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI
{
    public partial class MainWindow : Window
    {
        private readonly IAppSettingsService? appSettings;
        private bool suppressHeaderInstanceSelection;
        private MainWindowViewModel? observedViewModel;
        private ContextMenu? activeModRowMenu;
        private LinuxGuiPluginController? pluginController;
        private string? pluginControllerInstanceDir;

        public MainWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
            Closing += OnClosing;
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
            return await dialog.ShowDialog<int>(this) == 0;
        }

        private void AdvancedFiltersPopup_OnOpened(object? sender,
                                                   EventArgs e)
        {
            AdvancedAuthorFilterTextBox.Focus();
            AdvancedAuthorFilterTextBox.SelectAll();
        }

        private async void CompatibleGameVersionsMenuItem_OnClick(object? sender,
                                                                  Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new CompatibleGameVersionsWindow(instance);
            if (await dialog.ShowDialog<bool>(this))
            {
                await viewModel.ApplyCompatibleGameVersionsAsync(dialog.SelectedVersions);
            }
        }

        private async void DisplayScaleMenuItem_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new DisplayScaleWindow(viewModel);
            await dialog.ShowDialog(this);
        }

        private async void GameCommandLinesMenuItem_OnClick(object? sender,
                                                            Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new GameCommandLinesWindow(instance,
                                                    viewModel.CurrentSteamLibrary);
            await dialog.ShowDialog<bool>(this);
        }

        private async void PluginsMenuItem_OnClick(object? sender,
                                                   Avalonia.Interactivity.RoutedEventArgs e)
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
            await dialog.ShowDialog(this);
        }

        private async void PreferredHostsMenuItem_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentRegistry == null)
            {
                return;
            }

            var dialog = new PreferredHostsWindow(viewModel.CurrentConfiguration,
                                                  viewModel.CurrentRegistry);
            await dialog.ShowDialog<bool>(this);
        }

        private async void InstallationFiltersMenuItem_OnClick(object? sender,
                                                               Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.CurrentInstance is not GameInstance instance)
            {
                return;
            }

            var dialog = new InstallationFiltersWindow(viewModel.CurrentConfiguration,
                                                       instance);
            if (await dialog.ShowDialog<bool>(this)
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
                                                   viewModel.CurrentInstance?.Name);
            if (await dialog.ShowDialog<bool>(this)
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
            await dialog.ShowDialog(this);
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
            await dialog.ShowDialog(this);
        }

        private async void PlayTimeMenuItem_OnClick(object? sender,
                                                    Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var dialog = new PlayTimeWindow(viewModel.KnownGameInstances);
            await dialog.ShowDialog(this);
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
            await dialog.ShowDialog(this);
        }

        private void ExitMenuItem_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
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
                observedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            }

            observedViewModel = viewModel;

            if (observedViewModel != null)
            {
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
                var scrollViewer = ModsListBox.GetVisualDescendants()
                                              .OfType<ScrollViewer>()
                                              .FirstOrDefault();
                if (scrollViewer != null)
                {
                    scrollViewer.Offset = new Vector(scrollViewer.Offset.X, 0);
                }
            }, DispatcherPriority.Background);
        }

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
