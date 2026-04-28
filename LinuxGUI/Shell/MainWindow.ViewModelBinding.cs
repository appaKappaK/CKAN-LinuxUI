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
                observedViewModel.ConfirmClearQueueAsync = null;
                observedViewModel.ConfirmQueueRemoveAllInstalledModsAsync = null;
                observedViewModel.ConfirmCleanupMissingInstalledModsAsync = null;
                observedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            }

            observedViewModel = viewModel;

            if (observedViewModel != null)
            {
                observedViewModel.RecommendationSelectionPromptAsync = ShowRecommendationSelectionAsync;
                observedViewModel.ConfirmIncompatibleLaunchAsync = ConfirmIncompatibleLaunchAsync;
                observedViewModel.ConfirmClearQueueAsync = ConfirmClearQueueAsync;
                observedViewModel.ConfirmQueueRemoveAllInstalledModsAsync = ConfirmQueueRemoveAllInstalledModsAsync;
                observedViewModel.ConfirmCleanupMissingInstalledModsAsync = ConfirmCleanupMissingInstalledModsAsync;
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

        private async Task<bool> ConfirmClearQueueAsync(string prompt)
        {
            var dialog = new SimplePromptWindow(prompt,
                                                Array.Empty<string>(),
                                                "Clear Queue",
                                                "Cancel");
            return await ShowOwnedDialogAsync<int>(dialog) == 0;
        }

        private async Task<bool> ConfirmQueueRemoveAllInstalledModsAsync(string prompt)
        {
            var dialog = new SimplePromptWindow(prompt,
                                                Array.Empty<string>(),
                                                "Queue Removals",
                                                "Cancel");
            return await ShowOwnedDialogAsync<int>(dialog) == 0;
        }

        private async Task<bool> ConfirmCleanupMissingInstalledModsAsync(string prompt)
        {
            var dialog = new SimplePromptWindow(prompt,
                                                Array.Empty<string>(),
                                                "Clean Up",
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
    }
}
