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
            viewModel.RefreshVisibleVersionDisplaySettings();
            ConfigureRepositoryRefreshTimer();
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

    }
}
