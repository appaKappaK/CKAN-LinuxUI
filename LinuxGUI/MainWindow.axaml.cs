using System;

using Avalonia.Controls;
using Avalonia;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI
{
    public partial class MainWindow : Window
    {
        private readonly IAppSettingsService? appSettings;

        public MainWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
            Closing += OnClosing;
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
    }
}
