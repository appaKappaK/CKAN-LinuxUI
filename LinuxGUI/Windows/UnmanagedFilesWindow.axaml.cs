using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class UnmanagedFilesWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public UnmanagedFilesWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel(null, null);
            DataContext = viewModel;
        }

        public UnmanagedFilesWindow(GameInstance? instance,
                                    Registry?     registry)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(instance, registry);
            DataContext = viewModel;
        }

        private void RefreshButton_OnClick(object? sender,
                                           Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.Refresh();

        private void OpenSelectedButton_OnClick(object? sender,
                                                Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.Instance != null
                && viewModel.SelectedPath is string path)
            {
                viewModel.OpenSelected(path);
            }
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel : ReactiveObject
        {
            private string? selectedPath;
            private string statusMessage = "";

            public WindowViewModel(GameInstance? instance,
                                   Registry?     registry)
            {
                Instance = instance;
                Registry = registry;
                Paths = new ObservableCollection<string>();
                Refresh();
            }

            public GameInstance? Instance { get; }

            public Registry? Registry { get; }

            public ObservableCollection<string> Paths { get; }

            public string? SelectedPath
            {
                get => selectedPath;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedPath, value);
                    this.RaisePropertyChanged(nameof(HasSelection));
                }
            }

            public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedPath);

            public string StatusMessage
            {
                get => statusMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref statusMessage, value);
                    this.RaisePropertyChanged(nameof(ShowStatusMessage));
                }
            }

            public bool ShowStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

            public string SummaryText
                => Instance == null
                    ? "No current instance is available."
                    : $"Browse files in {Instance.Name} that were not installed by CKAN.";

            public string CountLabel
                => Paths.Count switch
                {
                    0 => "No unmanaged files found",
                    1 => "1 unmanaged file",
                    _ => $"{Paths.Count} unmanaged files",
                };

            public void Refresh()
            {
                StatusMessage = "";
                Paths.Clear();
                if (Instance == null || Registry == null)
                {
                    SelectedPath = null;
                    this.RaisePropertyChanged(nameof(CountLabel));
                    return;
                }

                foreach (var path in Instance.UnmanagedFiles(Registry)
                                             .OrderBy(path => path, Platform.PathComparer))
                {
                    Paths.Add(path);
                }

                SelectedPath = Paths.FirstOrDefault();
                this.RaisePropertyChanged(nameof(CountLabel));
            }

            public void OpenSelected(string path)
            {
                if (Instance == null)
                {
                    StatusMessage = "No game instance is available.";
                    return;
                }

                var target = Instance.ToAbsoluteGameDir(path);
                StatusMessage = Utilities.OpenFileBrowser(target)
                    ? $"Opened {path} in your file manager."
                    : $"Could not open {path} in your file manager.";
            }
        }
    }
}
