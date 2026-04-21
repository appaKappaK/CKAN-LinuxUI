using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

using CKAN.App.Models;

namespace CKAN.LinuxGUI
{
    public partial class ManageInstancesWindow : Window
    {
        private readonly WindowViewModel viewModel;

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

        public string? SelectedInstanceName => viewModel.SelectedInstance?.Name;

        private void OpenDirectoryButton_OnClick(object? sender,
                                                 Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.SelectedInstance?.GameDir is string path)
            {
                Utilities.OpenFileBrowser(path);
            }
        }

        private void UseSelectedButton_OnClick(object? sender,
                                               Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private sealed class WindowViewModel : ReactiveObject
        {
            private InstanceSummary? selectedInstance;

            public WindowViewModel()
                : this(new List<InstanceSummary>(), null)
            {
            }

            public WindowViewModel(IReadOnlyCollection<InstanceSummary> instances,
                                   string?                             currentInstanceName)
            {
                Instances = new ObservableCollection<InstanceSummary>(instances);
                SelectedInstance = Instances.FirstOrDefault(inst => inst.Name == currentInstanceName)
                                   ?? Instances.FirstOrDefault();
            }

            public ObservableCollection<InstanceSummary> Instances { get; }

            public InstanceSummary? SelectedInstance
            {
                get => selectedInstance;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedInstance, value);
                    this.RaisePropertyChanged(nameof(HasSelection));
                    this.RaisePropertyChanged(nameof(CanUseSelection));
                }
            }

            public bool HasSelection => SelectedInstance != null;

            public bool CanUseSelection => SelectedInstance != null && !SelectedInstance.IsCurrent;

            public string SummaryText
                => Instances.Count switch
                {
                    0 => "No registered game instances.",
                    1 => "1 registered game instance.",
                    _ => $"{Instances.Count} registered game instances.",
                };
        }
    }
}
