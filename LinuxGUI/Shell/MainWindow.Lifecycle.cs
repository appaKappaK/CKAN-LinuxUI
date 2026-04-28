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
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Types;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public partial class MainWindow : Window
    {
        private void OnOpened(object? sender,
                              EventArgs e)
        {
            ObserveViewModel(DataContext as MainWindowViewModel);
            StartLaunchUpdateCheck();
            ConfigureRepositoryRefreshTimer();

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
            StopRepositoryRefreshTimer();
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

        private void StartLaunchUpdateCheck()
        {
            if (launchUpdateCheckStarted)
            {
                return;
            }

            launchUpdateCheckStarted = true;
            _ = CheckForUpdatesOnLaunchAsync();
        }

        private async Task CheckForUpdatesOnLaunchAsync()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            await Task.Delay(750);
            for (var attempt = 0; attempt < 24 && viewModel.CurrentInstance == null; ++attempt)
            {
                await Task.Delay(250);
            }

            var instance = viewModel.CurrentInstance;
            if (!SettingsWindow.CheckForUpdatesOnLaunchEnabled(instance))
            {
                return;
            }

            try
            {
                var useDevBuilds = viewModel.CurrentConfiguration?.DevBuilds ?? false;
                var update = await Task.Run(() => new AutoUpdate().GetUpdate(useDevBuilds));
                if (update.Version is not CkanModuleVersion latestVersion
                    || latestVersion.SameClientVersion(Meta.ReleaseVersion))
                {
                    return;
                }

                var channel = useDevBuilds ? "dev build" : "release";
                var choice = await ShowOwnedDialogAsync<int>(
                    new SimplePromptWindow(
                        $"A newer CKAN {channel} is available.\n\nCurrent version: {Meta.ReleaseVersion}\nLatest version: {latestVersion}\n\nThis Linux GUI build will not install updates automatically. Use your package/source workflow for this build, or open the CKAN releases page.",
                        Array.Empty<string>(),
                        "Open Releases",
                        "Dismiss"));

                if (choice == 0)
                {
                    Utilities.ProcessStartURL(CkanReleasesUrl);
                }
            }
            catch
            {
                // Launch update checks are opportunistic; failures should not interrupt startup.
            }
        }

        private void ConfigureRepositoryRefreshTimer()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                StopRepositoryRefreshTimer();
                return;
            }

            var minutes = viewModel.CurrentConfiguration.RefreshRate;
            if (minutes <= 0)
            {
                StopRepositoryRefreshTimer();
                return;
            }

            repositoryRefreshTimer ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMinutes(minutes),
            };
            repositoryRefreshTimer.Tick -= RepositoryRefreshTimer_OnTick;
            repositoryRefreshTimer.Tick += RepositoryRefreshTimer_OnTick;
            repositoryRefreshTimer.Interval = TimeSpan.FromMinutes(minutes);
            repositoryRefreshTimer.Start();
        }

        private void StopRepositoryRefreshTimer()
        {
            if (repositoryRefreshTimer == null)
            {
                return;
            }

            repositoryRefreshTimer.Stop();
            repositoryRefreshTimer.Tick -= RepositoryRefreshTimer_OnTick;
        }

        private async void RepositoryRefreshTimer_OnTick(object? sender,
                                                         EventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel
                || viewModel.IsRefreshing
                || viewModel.IsApplyingChanges
                || viewModel.IsCatalogLoading
                || SettingsWindow.RefreshPausedEnabled(viewModel.CurrentInstance))
            {
                return;
            }

            await viewModel.RefreshRepositoriesAndCatalogAsync();
        }

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
    }
}
