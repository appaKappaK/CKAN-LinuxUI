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

        private void PreviewQueueRow_OnPointerPressed(object? sender,
                                                      PointerPressedEventArgs e)
        {
            if (sender is not Control control
                || control.DataContext is not QueuedActionModel action
                || DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var updateKind = e.GetCurrentPoint(control).Properties.PointerUpdateKind;
            if (updateKind != PointerUpdateKind.RightButtonPressed)
            {
                return;
            }

            viewModel.SelectedQueuedAction = action;
            PreviewQueueList.Focus();
            CloseActiveModRowMenu();
            e.Handled = true;

            if (!viewModel.ShowRemoveQueuedActionContextAction(action))
            {
                return;
            }

            var menu = new ContextMenu
            {
                Placement = PlacementMode.Pointer,
            };
            menu.Classes.Add("mod-row-menu");
            menu.Closed += ModRowMenu_OnClosed;

            var removeItem = new MenuItem
            {
                Header = "Remove from Queue",
            };
            removeItem.Click += (_, _) => viewModel.RemoveQueuedActionFromPreview(action);
            removeItem.Classes.Add("mod-row-menu-item");
            menu.Items.Add(removeItem);

            activeModRowMenu = menu;
            menu.Open(control);
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

        private void ModsListBox_OnPointerWheelChanged(object? sender,
                                                       PointerWheelEventArgs e)
            => ScrollListBoxByWheel(ModsListBox, e, BrowserWheelScrollPixels);

        private void PreviewQueueList_OnPointerWheelChanged(object? sender,
                                                            PointerWheelEventArgs e)
            => ScrollListBoxByWheel(PreviewQueueList, e, QueueWheelScrollPixels);

        private void PreviewDependenciesScrollViewer_OnPointerWheelChanged(object? sender,
                                                                           PointerWheelEventArgs e)
            => ScrollPreviewSectionByWheel(sender as ScrollViewer,
                                           e,
                                           allowPageScrollUp: true,
                                           allowPageScrollDown: true);

        private void PreviewRecommendationsScrollViewer_OnPointerWheelChanged(object? sender,
                                                                              PointerWheelEventArgs e)
            => ScrollPreviewSectionByWheel(sender as ScrollViewer,
                                           e,
                                           allowPageScrollUp: false,
                                           allowPageScrollDown: false);

        private void PreviewSuggestionsScrollViewer_OnPointerWheelChanged(object? sender,
                                                                          PointerWheelEventArgs e)
            => ScrollPreviewSectionByWheel(sender as ScrollViewer,
                                           e,
                                           allowPageScrollUp: true,
                                           allowPageScrollDown: true);

        private void PreviewSupportersScrollViewer_OnPointerWheelChanged(object? sender,
                                                                         PointerWheelEventArgs e)
            => ScrollPreviewSectionByWheel(sender as ScrollViewer,
                                           e,
                                           allowPageScrollUp: true,
                                           allowPageScrollDown: true);

        private void ApplyResultFollowUpScrollViewer_OnPointerWheelChanged(object? sender,
                                                                           PointerWheelEventArgs e)
            => ScrollPreviewSectionByWheel(sender as ScrollViewer,
                                           e,
                                           allowPageScrollUp: true,
                                           allowPageScrollDown: true);

        private static void ScrollPreviewSectionByWheel(ScrollViewer?         scrollViewer,
                                                        PointerWheelEventArgs e,
                                                        bool                  allowPageScrollUp,
                                                        bool                  allowPageScrollDown)
        {
            if (scrollViewer == null)
            {
                return;
            }

            double maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            bool scrollsUp = e.Delta.Y > 0;
            bool allowPageScroll = scrollsUp ? allowPageScrollUp : allowPageScrollDown;
            if (maxOffsetY <= 0)
            {
                e.Handled = !allowPageScroll;
                return;
            }

            double offsetDelta = scrollsUp ? -PreviewSectionWheelScrollPixels : PreviewSectionWheelScrollPixels;
            double offsetY = Math.Clamp(scrollViewer.Offset.Y + offsetDelta,
                                        0,
                                        maxOffsetY);
            if (Math.Abs(offsetY - scrollViewer.Offset.Y) < 0.01)
            {
                e.Handled = !allowPageScroll;
                return;
            }

            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, offsetY);
            e.Handled = true;
        }

        private static void ScrollListBoxByWheel(ListBox               listBox,
                                                 PointerWheelEventArgs e,
                                                 double                scrollPixels)
        {
            var scrollViewer = listBox.GetVisualDescendants()
                                      .OfType<ScrollViewer>()
                                      .FirstOrDefault();
            if (scrollViewer == null)
            {
                return;
            }

            double maxOffsetY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            if (maxOffsetY <= 0)
            {
                return;
            }

            var direction = e.Delta.Y > 0 ? -1 : 1;
            double offsetY = Math.Clamp(scrollViewer.Offset.Y + (direction * scrollPixels),
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
    }
}
