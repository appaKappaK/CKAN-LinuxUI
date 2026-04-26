using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.Games;

namespace CKAN.LinuxGUI
{
    public partial class ManageInstancesWindow : Window
    {
        private readonly WindowViewModel viewModel;
        private readonly GameInstanceManager? manager;
        private readonly IUser? user;

        public ManageInstancesWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel();
            DataContext = viewModel;
        }

        public ManageInstancesWindow(IReadOnlyCollection<InstanceSummary> instances,
                                     string?                             currentInstanceName)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(instances, currentInstanceName);
            DataContext = viewModel;
        }

        public ManageInstancesWindow(IReadOnlyCollection<InstanceSummary> instances,
                                     string?                             currentInstanceName,
                                     GameInstanceManager                 manager,
                                     IUser                               user)
        {
            InitializeComponent();
            this.manager = manager;
            this.user = user;
            viewModel = new WindowViewModel(instances,
                                            currentInstanceName,
                                            canManageInstances: true,
                                            canImportSteam: manager.SteamLibrary.Games.Length > 0);
            DataContext = viewModel;
        }

        public string? SelectedInstanceName => viewModel.SelectedInstance?.Name;

        public bool Changed => viewModel.HasChanges;

        private void UseSelectedButton_OnClick(object? sender,
                                               Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private void SetDefaultCheckBox_OnClick(object? sender,
                                                Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (manager == null || viewModel.SelectedInstance == null)
            {
                return;
            }

            try
            {
                if (viewModel.SelectedInstance.IsDefault)
                {
                    manager.ClearAutoStart();
                    viewModel.SetStatus("Default instance cleared.");
                }
                else
                {
                    manager.SetAutoStart(viewModel.SelectedInstance.Name);
                    viewModel.SetStatus($"{viewModel.SelectedInstance.Name} will be used by default.");
                }
                viewModel.MarkChanged();
                RefreshInstances(viewModel.SelectedInstance.Name);
            }
            catch (Exception ex)
            {
                viewModel.SetStatus(ex.Message);
                RefreshInstances(viewModel.SelectedInstance.Name);
            }
        }

        private void ForgetButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (manager == null || viewModel.SelectedInstance == null || viewModel.SelectedInstance.IsCurrent)
            {
                return;
            }

            var selected = viewModel.SelectedInstance;
            try
            {
                if (selected.IsDefault)
                {
                    manager.ClearAutoStart();
                }
                manager.RemoveInstance(selected.Name);
                viewModel.MarkChanged();
                viewModel.SetStatus($"Forgot {selected.Name}. The game folder was not deleted.");
                RefreshInstances();
            }
            catch (Exception ex)
            {
                viewModel.SetStatus(ex.Message);
                RefreshInstances(selected.Name);
            }
        }

        private async void RenameButton_OnClick(object? sender,
                                                Avalonia.Interactivity.RoutedEventArgs e)
            => await RenameSelectedAsync();

        private async Task RenameSelectedAsync()
        {
            if (manager == null || viewModel.SelectedInstance == null)
            {
                return;
            }

            var selected = viewModel.SelectedInstance;
            var dialog = new TextInputWindow("Rename Game Instance",
                                             "Instance name",
                                             selected.Name,
                                             "Rename");
            if (!await dialog.ShowDialog<bool>(this))
            {
                return;
            }

            var newName = dialog.ResultText;
            if (string.IsNullOrWhiteSpace(newName))
            {
                viewModel.SetStatus("Instance name cannot be empty.");
                return;
            }
            if (string.Equals(selected.Name, newName, StringComparison.Ordinal))
            {
                viewModel.SetStatus("Instance name unchanged.");
                return;
            }

            try
            {
                var wasDefault = selected.IsDefault;
                manager.RenameInstance(selected.Name, newName);
                if (wasDefault)
                {
                    manager.SetAutoStart(newName);
                }
                viewModel.MarkChanged();
                viewModel.SetStatus($"Renamed {selected.Name} to {newName}.");
                RefreshInstances(newName);
            }
            catch (Exception ex)
            {
                viewModel.SetStatus(ex.Message);
                RefreshInstances(selected.Name);
            }
        }

        private async void AddInstanceButton_OnClick(object? sender,
                                                     Avalonia.Interactivity.RoutedEventArgs e)
            => await AddInstanceAsync();

        private async Task AddInstanceAsync()
        {
            if (manager == null)
            {
                return;
            }

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Register game instance",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Game instance files")
                    {
                        Patterns = KnownGames.AllInstanceAnchorFiles.ToArray(),
                    },
                    FilePickerFileTypes.All,
                },
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var chosen = Path.GetFileName(path);
            if (!KnownGames.AllInstanceAnchorFiles.Contains(chosen, Platform.PathComparer))
            {
                viewModel.SetStatus($"Choose a game instance file such as {string.Join(", ", KnownGames.AllInstanceAnchorFiles.Take(3))}.");
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                viewModel.SetStatus("Could not determine the selected game folder.");
                return;
            }

            try
            {
                var baseName = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(baseName))
                {
                    baseName = directory;
                }
                var instanceName = manager.GetNextValidInstanceName(baseName);
                manager.AddInstance(directory, instanceName, user ?? new NullUser());
                viewModel.MarkChanged();
                viewModel.SetStatus($"Registered {instanceName}.");
                RefreshInstances(instanceName);
            }
            catch (Exception ex)
            {
                viewModel.SetStatus(ex.Message);
                RefreshInstances(viewModel.SelectedInstance?.Name);
            }
        }

        private void ImportSteamButton_OnClick(object? sender,
                                               Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (manager == null)
            {
                return;
            }

            try
            {
                var currentDirs = manager.Instances.Values
                                         .Select(inst => Path.GetFullPath(inst.GameDir))
                                         .ToHashSet(Platform.PathComparer);
                var toAdd = manager.FindDefaultInstances()
                                   .Where(inst => !currentDirs.Contains(Path.GetFullPath(inst.GameDir)))
                                   .ToList();
                foreach (var inst in toAdd)
                {
                    manager.AddInstance(inst);
                }

                if (toAdd.Count == 0)
                {
                    viewModel.SetStatus("No new Steam game instances were found.");
                    return;
                }

                viewModel.MarkChanged();
                viewModel.SetStatus($"Imported {toAdd.Count} Steam game instance{(toAdd.Count == 1 ? "" : "s")}.");
                RefreshInstances(toAdd[0].Name);
            }
            catch (Exception ex)
            {
                viewModel.SetStatus(ex.Message);
                RefreshInstances(viewModel.SelectedInstance?.Name);
            }
        }

        private void RefreshInstances(string? preferredSelectionName = null)
        {
            if (manager == null)
            {
                return;
            }

            var summaries = manager.Instances.Values
                                   .Select(inst => InstanceSummary.From(inst,
                                                                        manager.CurrentInstance?.Name,
                                                                        manager.Configuration.AutoStartInstance))
                                   .ToList();
            viewModel.ReplaceInstances(summaries, preferredSelectionName);
        }

        private sealed class WindowViewModel : ReactiveObject
        {
            private InstanceSummary? selectedInstance;
            private string? statusMessage;

            public WindowViewModel()
                : this(new List<InstanceSummary>(), null, false, false)
            {
            }

            public WindowViewModel(IReadOnlyCollection<InstanceSummary> instances,
                                   string?                             currentInstanceName)
                : this(instances, currentInstanceName, false, false)
            {
            }

            public WindowViewModel(IReadOnlyCollection<InstanceSummary> instances,
                                   string?                             currentInstanceName,
                                   bool                                canManageInstances,
                                   bool                                canImportSteam)
            {
                Instances = new ObservableCollection<InstanceSummary>(instances);
                CanManageInstances = canManageInstances;
                CanImportSteam = canImportSteam;
                SelectedInstance = Instances.FirstOrDefault(inst => inst.Name == currentInstanceName)
                                   ?? Instances.FirstOrDefault();
            }

            public ObservableCollection<InstanceSummary> Instances { get; }

            public bool HasChanges { get; private set; }

            public bool CanManageInstances { get; }

            public bool CanImportSteam { get; }

            public InstanceSummary? SelectedInstance
            {
                get => selectedInstance;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedInstance, value);
                    RaiseSelectionStateChanged();
                }
            }

            public void ReplaceInstances(IReadOnlyCollection<InstanceSummary> instances,
                                         string?                             preferredSelectionName)
            {
                var fallbackName = preferredSelectionName ?? SelectedInstance?.Name;
                Instances.Clear();
                foreach (var inst in instances)
                {
                    Instances.Add(inst);
                }

                SelectedInstance = !string.IsNullOrWhiteSpace(fallbackName)
                    ? Instances.FirstOrDefault(inst => inst.Name == fallbackName)
                      ?? Instances.FirstOrDefault(inst => inst.IsCurrent)
                      ?? Instances.FirstOrDefault()
                    : Instances.FirstOrDefault(inst => inst.IsCurrent) ?? Instances.FirstOrDefault();
                this.RaisePropertyChanged(nameof(SummaryText));
                RaiseSelectionStateChanged();
            }

            public void MarkChanged()
            {
                HasChanges = true;
            }

            public void SetStatus(string message)
            {
                statusMessage = message;
                this.RaisePropertyChanged(nameof(FooterText));
            }

            public bool CanUseSelection => SelectedInstance != null && !SelectedInstance.IsCurrent;

            public bool CanRenameSelection => CanManageInstances && SelectedInstance != null;

            public bool CanForgetSelection => CanManageInstances
                                           && SelectedInstance != null
                                           && !SelectedInstance.IsCurrent;

            public bool CanSetDefault => CanManageInstances && SelectedInstance != null;

            public bool IsSelectedDefault => SelectedInstance?.IsDefault == true;

            public string SelectedStatusText
                => SelectedInstance == null
                    ? "None"
                    : SelectedInstance.IsCurrent ? "Current" :
                      SelectedInstance.IsDefault ? "Default" : "Available";

            public string SelectedStatusBackground
                => SelectedInstance?.IsCurrent == true ? "#244031" :
                   SelectedInstance?.IsDefault == true ? "#332C48" : "#24384D";

            public string SelectedStatusBorderBrush
                => SelectedInstance?.IsCurrent == true ? "#3E7A58" :
                   SelectedInstance?.IsDefault == true ? "#654E91" : "#40648B";

            public string DetailText
                => SelectedInstance == null
                    ? "Select a registered install to review it."
                    : SelectedInstance.IsCurrent
                        ? "This install is already active. Choose another registered instance to switch the mod browser."
                        : "Switching instances reloads the mod catalog for the selected install.";

            public string FooterText
                => !string.IsNullOrWhiteSpace(statusMessage)
                    ? statusMessage
                    : SelectedInstance == null
                    ? "No instance selected."
                    : SelectedInstance.IsCurrent
                        ? "The selected instance is already active."
                        : $"Ready to switch to {SelectedInstance.Name}.";

            public string SummaryText
                => Instances.Count switch
                {
                    0 => "No registered game instances.",
                    1 => "1 registered game instance.",
                    _ => $"{Instances.Count} registered game instances.",
                };

            private void RaiseSelectionStateChanged()
            {
                this.RaisePropertyChanged(nameof(CanUseSelection));
                this.RaisePropertyChanged(nameof(CanRenameSelection));
                this.RaisePropertyChanged(nameof(CanForgetSelection));
                this.RaisePropertyChanged(nameof(CanSetDefault));
                this.RaisePropertyChanged(nameof(IsSelectedDefault));
                this.RaisePropertyChanged(nameof(SelectedStatusText));
                this.RaisePropertyChanged(nameof(SelectedStatusBackground));
                this.RaisePropertyChanged(nameof(SelectedStatusBorderBrush));
                this.RaisePropertyChanged(nameof(DetailText));
                this.RaisePropertyChanged(nameof(FooterText));
            }
        }
    }
}
