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
                ShowDetailsPane = (DataContext as MainWindowViewModel)?.ShowDetailsPane,
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
            }
        }

        private void ViewModel_OnPropertyChanged(object? sender,
                                                 PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ModListScrollResetRequestId))
            {
                ResetModListScrollToTop();
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
    }
}
