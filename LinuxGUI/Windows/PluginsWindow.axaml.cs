using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class PluginsWindow : Window
    {
        private readonly LinuxGuiPluginController controller;
        private readonly WindowViewModel viewModel;

        public PluginsWindow()
        {
            InitializeComponent();
            controller = null!;
            viewModel = new WindowViewModel("");
            DataContext = viewModel;
        }

        public PluginsWindow(LinuxGuiPluginController controller) : this()
        {
            this.controller = controller;
            viewModel = new WindowViewModel(controller.PluginsPath);
            DataContext = viewModel;
            viewModel.Refresh(controller);
        }

        private void ActivateButton_OnClick(object? sender,
                                            Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedDormantPlugin != null)
            {
                controller.ActivatePlugin(viewModel.SelectedDormantPlugin);
                viewModel.Refresh(controller);
            }
        }

        private void DeactivateButton_OnClick(object? sender,
                                              Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedActivePlugin != null)
            {
                controller.DeactivatePlugin(viewModel.SelectedActivePlugin);
                viewModel.Refresh(controller);
            }
        }

        private void ReloadButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedActivePlugin != null)
            {
                controller.ReloadPlugin(viewModel.SelectedActivePlugin);
                viewModel.Refresh(controller);
            }
        }

        private void UnloadButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedDormantPlugin != null)
            {
                controller.UnloadPlugin(viewModel.SelectedDormantPlugin);
                viewModel.Refresh(controller);
            }
        }

        private async void AddPluginButton_OnClick(object? sender,
                                                   Avalonia.Interactivity.RoutedEventArgs e)
            => await AddPluginAsync();

        private async Task AddPluginAsync()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Add Plugin Assembly",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Plugin assemblies")
                    {
                        Patterns = new[] { "*.dll" },
                    }
                }
            });

            if (files.Count == 0)
            {
                return;
            }

            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                viewModel.Message = "Selected plugin file is not available on the local filesystem.";
                return;
            }

            try
            {
                controller.AddNewAssemblyToPluginsPath(path);
                viewModel.Message = $"Imported {System.IO.Path.GetFileName(path)}.";
                viewModel.Refresh(controller);
            }
            catch (System.Exception ex)
            {
                viewModel.Message = ex.Message;
            }
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel : ReactiveObject
        {
            private PluginLoadRecord? selectedActivePlugin;
            private PluginLoadRecord? selectedDormantPlugin;
            private string message = "";

            public WindowViewModel(string pluginsPath)
            {
                PluginsPathLabel = pluginsPath;
                SummaryText = "Manage active and dormant plugin assemblies for the current game instance.";
                ActivePlugins = new ObservableCollection<PluginLoadRecord>();
                DormantPlugins = new ObservableCollection<PluginLoadRecord>();
                LoadFailures = new ObservableCollection<string>();
            }

            public ObservableCollection<PluginLoadRecord> ActivePlugins { get; }

            public ObservableCollection<PluginLoadRecord> DormantPlugins { get; }

            public ObservableCollection<string> LoadFailures { get; }

            public string SummaryText { get; }

            public string PluginsPathLabel { get; }

            public string ActiveHeading
                => ActivePlugins.Count switch
                {
                    0 => "Active Plugins",
                    1 => "Active Plugins (1)",
                    _ => $"Active Plugins ({ActivePlugins.Count})",
                };

            public string DormantHeading
                => DormantPlugins.Count switch
                {
                    0 => "Dormant Plugins",
                    1 => "Dormant Plugins (1)",
                    _ => $"Dormant Plugins ({DormantPlugins.Count})",
                };

            public PluginLoadRecord? SelectedActivePlugin
            {
                get => selectedActivePlugin;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedActivePlugin, value);
                    this.RaisePropertyChanged(nameof(HasSelectedActivePlugin));
                }
            }

            public PluginLoadRecord? SelectedDormantPlugin
            {
                get => selectedDormantPlugin;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedDormantPlugin, value);
                    this.RaisePropertyChanged(nameof(HasSelectedDormantPlugin));
                }
            }

            public bool HasSelectedActivePlugin => SelectedActivePlugin != null;

            public bool HasSelectedDormantPlugin => SelectedDormantPlugin != null;

            public string Message
            {
                get => message;
                set
                {
                    this.RaiseAndSetIfChanged(ref message, value);
                    this.RaisePropertyChanged(nameof(ShowMessage));
                }
            }

            public bool ShowMessage => !string.IsNullOrWhiteSpace(Message);

            public bool HasLoadFailures => LoadFailures.Count > 0;

            public string LoadFailureHeading
                => LoadFailures.Count == 1
                    ? "1 plugin failed to load"
                    : $"{LoadFailures.Count} plugins failed to load";

            public void Refresh(LinuxGuiPluginController controller)
            {
                ActivePlugins.Clear();
                foreach (var record in controller.ActivePlugins)
                {
                    ActivePlugins.Add(record);
                }

                DormantPlugins.Clear();
                foreach (var record in controller.DormantPlugins)
                {
                    DormantPlugins.Add(record);
                }

                LoadFailures.Clear();
                foreach (var failure in controller.LoadFailures)
                {
                    LoadFailures.Add(failure);
                }

                SelectedActivePlugin = ActivePlugins.FirstOrDefault();
                SelectedDormantPlugin = DormantPlugins.FirstOrDefault();
                this.RaisePropertyChanged(nameof(ActiveHeading));
                this.RaisePropertyChanged(nameof(DormantHeading));
                this.RaisePropertyChanged(nameof(HasLoadFailures));
                this.RaisePropertyChanged(nameof(LoadFailureHeading));
            }
        }
    }
}
