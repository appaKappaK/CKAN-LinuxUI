using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class InstallationHistoryWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public InstallationHistoryWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel(null);
            DataContext = viewModel;
        }

        public InstallationHistoryWindow(GameInstance? instance)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(instance);
            DataContext = viewModel;
        }

        private void OpenSnapshotFolderButton_OnClick(object? sender,
                                                      Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedSnapshot?.Path is string path)
            {
                Utilities.OpenFileBrowser(path);
            }
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel : ReactiveObject
        {
            private HistorySnapshotEntry? selectedSnapshot;

            public WindowViewModel(GameInstance? instance)
            {
                Instance = instance;
                Snapshots = new ObservableCollection<HistorySnapshotEntry>(
                    instance?.InstallHistoryFiles()
                             .Select(file => new HistorySnapshotEntry(file))
                    ?? Enumerable.Empty<HistorySnapshotEntry>());
                Modules = new ObservableCollection<HistoryModuleEntry>();
                SelectedSnapshot = Snapshots.FirstOrDefault();
            }

            public GameInstance? Instance { get; }

            public ObservableCollection<HistorySnapshotEntry> Snapshots { get; }

            public ObservableCollection<HistoryModuleEntry> Modules { get; }

            public HistorySnapshotEntry? SelectedSnapshot
            {
                get => selectedSnapshot;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedSnapshot, value);
                    RebuildModules();
                    this.RaisePropertyChanged(nameof(HasSelectedSnapshot));
                    this.RaisePropertyChanged(nameof(SelectedSnapshotTitle));
                    this.RaisePropertyChanged(nameof(SelectedSnapshotSubtitle));
                }
            }

            public bool HasSelectedSnapshot => SelectedSnapshot != null;

            public string SummaryText
                => Instance == null
                    ? "No current instance is available."
                    : $"Browse saved installation snapshots for {Instance.Name}.";

            public string SelectedSnapshotTitle
                => SelectedSnapshot == null ? "Select a snapshot" : SelectedSnapshot.TimestampText;

            public string SelectedSnapshotSubtitle
                => SelectedSnapshot == null
                    ? "Pick a history entry to inspect the modules recorded in that snapshot."
                    : Modules.Count switch
                    {
                        0 => "No module data was recorded in this snapshot.",
                        1 => "1 module recorded in this snapshot.",
                        _ => $"{Modules.Count} modules recorded in this snapshot.",
                    };

            private void RebuildModules()
            {
                Modules.Clear();
                if (SelectedSnapshot == null)
                {
                    return;
                }

                try
                {
                    var module = CkanModule.FromFile(SelectedSnapshot.Path);
                    var deps = module.depends?.OfType<ModuleRelationshipDescriptor>()
                                           .Select(rel => new HistoryModuleEntry(
                                               rel.name,
                                               rel.version?.ToString() ?? "Any",
                                               rel.name))
                               ?? Enumerable.Empty<HistoryModuleEntry>();
                    foreach (var dep in deps.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        Modules.Add(dep);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class HistorySnapshotEntry
        {
            public HistorySnapshotEntry(FileInfo file)
            {
                Path = file.FullName;
                FileName = file.Name;
                TimestampText = file.CreationTime.ToString("g");
            }

            public string Path { get; }

            public string FileName { get; }

            public string TimestampText { get; }
        }

        private sealed class HistoryModuleEntry
        {
            public HistoryModuleEntry(string name,
                                      string version,
                                      string identifier)
            {
                Name = name;
                Version = version;
                Identifier = identifier;
            }

            public string Name { get; }

            public string Version { get; }

            public string Identifier { get; }
        }
    }
}
