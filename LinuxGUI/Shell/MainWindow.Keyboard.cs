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
    }
}
