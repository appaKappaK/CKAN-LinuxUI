using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

using CKAN.Configuration;

namespace CKAN.LinuxGUI
{
    public partial class PreferredHostsWindow : Window
    {
        private readonly IConfiguration? configuration;
        private readonly EditorViewModel viewModel;

        public PreferredHostsWindow()
        {
            InitializeComponent();
            viewModel = new EditorViewModel(Array.Empty<string>(),
                                            Array.Empty<string?>());
            DataContext = viewModel;
        }

        public PreferredHostsWindow(IConfiguration configuration,
                                    Registry        registry)
        {
            InitializeComponent();
            this.configuration = configuration;
            viewModel = new EditorViewModel(registry.GetAllHosts(),
                                            configuration.PreferredHosts);
            DataContext = viewModel;
        }

        public bool Changed { get; private set; }

        private void MoveRightButton_OnClick(object? sender,
                                             Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.MoveRight();

        private void MoveLeftButton_OnClick(object? sender,
                                            Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.MoveLeft();

        private void MoveUpButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.MoveUp();

        private void MoveDownButton_OnClick(object? sender,
                                            Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.MoveDown();

        private void HelpButton_OnClick(object? sender,
                                        Avalonia.Interactivity.RoutedEventArgs e)
            => Utilities.ProcessStartURL(HelpURLs.PreferredHosts);

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private void AcceptButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (configuration == null)
            {
                Close(false);
                return;
            }

            var updated = viewModel.ToPreferredHostsArray();
            Changed = !configuration.PreferredHosts.SequenceEqual(updated);
            configuration.PreferredHosts = updated;
            Close(true);
        }

        private sealed class EditorViewModel : ReactiveObject
        {
            private const string Placeholder = "<ALL OTHER HOSTS>";

            private readonly IReadOnlyList<string> allHosts;
            private string? selectedAvailableHost;
            private string? selectedPreferredHost;

            public EditorViewModel(IEnumerable<string>  allHosts,
                                   IEnumerable<string?> preferredHosts)
            {
                this.allHosts = allHosts.Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();
                AvailableHosts = new ObservableCollection<string>();
                PreferredHosts = new ObservableCollection<string>();
                Load(preferredHosts);
            }

            public ObservableCollection<string> AvailableHosts { get; }

            public ObservableCollection<string> PreferredHosts { get; }

            public string? SelectedAvailableHost
            {
                get => selectedAvailableHost;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedAvailableHost, value);
                    this.RaisePropertyChanged(nameof(CanMoveRight));
                }
            }

            public string? SelectedPreferredHost
            {
                get => selectedPreferredHost;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedPreferredHost, value);
                    this.RaisePropertyChanged(nameof(CanMoveLeft));
                    this.RaisePropertyChanged(nameof(CanMoveUp));
                    this.RaisePropertyChanged(nameof(CanMoveDown));
                }
            }

            public bool CanMoveRight => !string.IsNullOrWhiteSpace(SelectedAvailableHost);

            public bool CanMoveLeft
                => !string.IsNullOrWhiteSpace(SelectedPreferredHost)
                   && !string.Equals(SelectedPreferredHost, Placeholder, StringComparison.Ordinal);

            public bool CanMoveUp
                => SelectedPreferredHost != null
                   && PreferredHosts.IndexOf(SelectedPreferredHost) > 0;

            public bool CanMoveDown
                => SelectedPreferredHost != null
                   && PreferredHosts.IndexOf(SelectedPreferredHost) > -1
                   && PreferredHosts.IndexOf(SelectedPreferredHost) < PreferredHosts.Count - 1;

            public void MoveRight()
            {
                if (SelectedAvailableHost is not string host)
                {
                    return;
                }

                if (PreferredHosts.Count == 0)
                {
                    PreferredHosts.Add(Placeholder);
                }

                int insertIndex = PreferredHosts.IndexOf(Placeholder);
                if (insertIndex < 0)
                {
                    PreferredHosts.Add(Placeholder);
                    insertIndex = PreferredHosts.Count - 1;
                }

                AvailableHosts.Remove(host);
                PreferredHosts.Insert(insertIndex, host);
                SelectedPreferredHost = host;
                SelectedAvailableHost = AvailableHosts.FirstOrDefault();
                this.RaisePropertyChanged(nameof(CanMoveRight));
            }

            public void MoveLeft()
            {
                if (SelectedPreferredHost is not string host
                    || string.Equals(host, Placeholder, StringComparison.Ordinal))
                {
                    return;
                }

                int fromIndex = PreferredHosts.IndexOf(host);
                PreferredHosts.Remove(host);
                RebuildAvailableHosts();

                if (PreferredHosts.Count == 1
                    && string.Equals(PreferredHosts[0], Placeholder, StringComparison.Ordinal))
                {
                    PreferredHosts.Clear();
                }

                SelectedAvailableHost = host;
                SelectedPreferredHost = PreferredHosts.ElementAtOrDefault(Math.Min(fromIndex,
                                                                                    PreferredHosts.Count - 1));
                RaisePreferredState();
            }

            public void MoveUp()
            {
                if (SelectedPreferredHost is not string host)
                {
                    return;
                }

                int index = PreferredHosts.IndexOf(host);
                if (index <= 0)
                {
                    return;
                }

                PreferredHosts.RemoveAt(index);
                PreferredHosts.Insert(index - 1, host);
                SelectedPreferredHost = host;
                RaisePreferredState();
            }

            public void MoveDown()
            {
                if (SelectedPreferredHost is not string host)
                {
                    return;
                }

                int index = PreferredHosts.IndexOf(host);
                if (index < 0 || index >= PreferredHosts.Count - 1)
                {
                    return;
                }

                PreferredHosts.RemoveAt(index);
                PreferredHosts.Insert(index + 1, host);
                SelectedPreferredHost = host;
                RaisePreferredState();
            }

            public string?[] ToPreferredHostsArray()
            {
                var items = PreferredHosts.Where(host => !string.Equals(host, Placeholder, StringComparison.Ordinal))
                                          .ToList();
                if (items.Count == 0)
                {
                    return Array.Empty<string?>();
                }

                var result = PreferredHosts.Select(host => string.Equals(host, Placeholder, StringComparison.Ordinal)
                                                               ? null
                                                               : host)
                                           .ToList();
                if (!result.Contains(null))
                {
                    result.Add(null);
                }
                return result.ToArray();
            }

            private void Load(IEnumerable<string?> preferredHosts)
            {
                var ordered = preferredHosts.Select(host => host ?? Placeholder)
                                            .ToList();
                if (ordered.Count > 0 && !ordered.Contains(Placeholder))
                {
                    ordered.Add(Placeholder);
                }

                PreferredHosts.Clear();
                foreach (var host in ordered)
                {
                    PreferredHosts.Add(host);
                }

                RebuildAvailableHosts();
                SelectedAvailableHost = AvailableHosts.FirstOrDefault();
                SelectedPreferredHost = PreferredHosts.FirstOrDefault();
                RaisePreferredState();
            }

            private void RebuildAvailableHosts()
            {
                var preferredRealHosts = PreferredHosts.Where(host => !string.Equals(host, Placeholder, StringComparison.Ordinal))
                                                       .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var firstAvailable = SelectedAvailableHost;
                AvailableHosts.Clear();
                foreach (var host in allHosts.Where(host => !preferredRealHosts.Contains(host)))
                {
                    AvailableHosts.Add(host);
                }
                SelectedAvailableHost = firstAvailable != null && AvailableHosts.Contains(firstAvailable)
                    ? firstAvailable
                    : AvailableHosts.FirstOrDefault();
            }

            private void RaisePreferredState()
            {
                this.RaisePropertyChanged(nameof(CanMoveLeft));
                this.RaisePropertyChanged(nameof(CanMoveUp));
                this.RaisePropertyChanged(nameof(CanMoveDown));
            }
        }
    }
}
