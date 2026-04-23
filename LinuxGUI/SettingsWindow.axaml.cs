using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class SettingsWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public SettingsWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel();
            DataContext = viewModel;
        }

        public Func<Task>? OpenDisplayScaleAsync
        {
            get => viewModel.DisplayScaleCard.Action;
            set => viewModel.DisplayScaleCard.Action = value;
        }

        public Func<Task>? OpenCompatibleGameVersionsAsync
        {
            get => viewModel.CompatibleGameVersionsCard.Action;
            set => viewModel.CompatibleGameVersionsCard.Action = value;
        }

        public Func<Task>? OpenGameCommandLinesAsync
        {
            get => viewModel.GameCommandLinesCard.Action;
            set => viewModel.GameCommandLinesCard.Action = value;
        }

        public Func<Task>? OpenPreferredHostsAsync
        {
            get => viewModel.PreferredHostsCard.Action;
            set => viewModel.PreferredHostsCard.Action = value;
        }

        public Func<Task>? OpenInstallationFiltersAsync
        {
            get => viewModel.InstallationFiltersCard.Action;
            set => viewModel.InstallationFiltersCard.Action = value;
        }

        public Func<Task>? OpenPluginsAsync
        {
            get => viewModel.PluginsCard.Action;
            set => viewModel.PluginsCard.Action = value;
        }

        private async void DisplayScaleButton_OnClick(object? sender,
                                                      Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.DisplayScaleCard.InvokeAsync();

        private async void CompatibleGameVersionsButton_OnClick(object? sender,
                                                                Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.CompatibleGameVersionsCard.InvokeAsync();

        private async void GameCommandLinesButton_OnClick(object? sender,
                                                          Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.GameCommandLinesCard.InvokeAsync();

        private async void PreferredHostsButton_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.PreferredHostsCard.InvokeAsync();

        private async void InstallationFiltersButton_OnClick(object? sender,
                                                             Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.InstallationFiltersCard.InvokeAsync();

        private async void PluginsButton_OnClick(object? sender,
                                                 Avalonia.Interactivity.RoutedEventArgs e)
            => await viewModel.PluginsCard.InvokeAsync();

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private sealed class WindowViewModel
        {
            public WindowViewModel()
            {
                DisplayScaleCard = new SettingsCardViewModel();
                CompatibleGameVersionsCard = new SettingsCardViewModel();
                GameCommandLinesCard = new SettingsCardViewModel();
                PreferredHostsCard = new SettingsCardViewModel();
                InstallationFiltersCard = new SettingsCardViewModel();
                PluginsCard = new SettingsCardViewModel();
            }

            public SettingsCardViewModel DisplayScaleCard { get; }

            public SettingsCardViewModel CompatibleGameVersionsCard { get; }

            public SettingsCardViewModel GameCommandLinesCard { get; }

            public SettingsCardViewModel PreferredHostsCard { get; }

            public SettingsCardViewModel InstallationFiltersCard { get; }

            public SettingsCardViewModel PluginsCard { get; }
        }

        private sealed class SettingsCardViewModel : ReactiveObject
        {
            private Func<Task>? action;
            private bool isBusy;
            private string errorMessage = "";

            public Func<Task>? Action
            {
                get => action;
                set
                {
                    action = value;
                    this.RaisePropertyChanged(nameof(CanInvoke));
                }
            }

            public bool CanInvoke => Action != null && !IsBusy;

            public bool IsBusy
            {
                get => isBusy;
                private set
                {
                    this.RaiseAndSetIfChanged(ref isBusy, value);
                    this.RaisePropertyChanged(nameof(ButtonText));
                    this.RaisePropertyChanged(nameof(CanInvoke));
                }
            }

            public string ButtonText => IsBusy ? "Opening..." : "Open";

            public string ErrorMessage
            {
                get => errorMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref errorMessage, value);
                    this.RaisePropertyChanged(nameof(ShowError));
                }
            }

            public bool ShowError => !string.IsNullOrWhiteSpace(ErrorMessage);

            public async Task InvokeAsync()
            {
                if (Action == null || IsBusy)
                {
                    return;
                }

                try
                {
                    ErrorMessage = "";
                    IsBusy = true;
                    await Action();
                }
                catch (Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
