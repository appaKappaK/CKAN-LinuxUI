using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class PlayTimeWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public PlayTimeWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel(Array.Empty<GameInstance>());
            DataContext = viewModel;
        }

        public PlayTimeWindow(IReadOnlyCollection<GameInstance> instances)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(instances);
            DataContext = viewModel;
        }

        private void CloseButton_OnClick(object? sender,
                                         Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private void SaveButton_OnClick(object? sender,
                                        Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (viewModel.TrySave())
            {
                Close();
            }
        }

        private sealed class WindowViewModel : ReactiveObject
        {
            private string validationMessage = "";

            public WindowViewModel(IReadOnlyCollection<GameInstance> instances)
            {
                Entries = new ObservableCollection<PlayTimeEntry>(
                    instances.OrderBy(inst => inst.Name, StringComparer.OrdinalIgnoreCase)
                             .Select(inst => new PlayTimeEntry(inst)));
                foreach (var entry in Entries)
                {
                    entry.WhenAnyValue(model => model.HoursText)
                         .Subscribe(_ => this.RaisePropertyChanged(nameof(TotalHoursLabel)));
                }
            }

            public ObservableCollection<PlayTimeEntry> Entries { get; }

            public string ValidationMessage
            {
                get => validationMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref validationMessage, value);
                    this.RaisePropertyChanged(nameof(HasValidationMessage));
                }
            }

            public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

            public string TotalHoursLabel
            {
                get
                {
                    double total = Entries.Select(entry => entry.TryGetHours(out var hours) ? hours : 0d)
                                          .Sum();
                    return $"Total recorded play time: {total:N1} hours";
                }
            }

            public bool TrySave()
            {
                foreach (var entry in Entries)
                {
                    if (!entry.TryGetHours(out var hours))
                    {
                        ValidationMessage = $"Invalid hours value for {entry.Name}. Use a non-negative number.";
                        return false;
                    }

                    var timeLog = entry.Instance.playTime ?? new TimeLog();
                    timeLog.Time = TimeSpan.FromHours(hours);
                    timeLog.Save(TimeLog.GetPath(entry.Instance.CkanDir));
                    entry.Instance.playTime = timeLog;
                }

                ValidationMessage = "";
                return true;
            }
        }

        private sealed class PlayTimeEntry : ReactiveObject
        {
            private string hoursText;

            public PlayTimeEntry(GameInstance instance)
            {
                Instance = instance;
                Name = instance.Name;
                GameName = instance.Game.ShortName;
                GameDir = Platform.FormatPath(instance.GameDir);
                hoursText = (instance.playTime?.Time.TotalHours ?? 0d).ToString("N1", CultureInfo.InvariantCulture);
            }

            public GameInstance Instance { get; }

            public string Name { get; }

            public string GameName { get; }

            public string GameDir { get; }

            public string HoursText
            {
                get => hoursText;
                set => this.RaiseAndSetIfChanged(ref hoursText, value);
            }

            public bool TryGetHours(out double hours)
                => double.TryParse(HoursText,
                                   NumberStyles.Float | NumberStyles.AllowThousands,
                                   CultureInfo.InvariantCulture,
                                   out hours)
                   && hours >= 0;
        }
    }
}
