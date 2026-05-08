using System;
using System.Linq;
using System.Collections.Generic;

using Avalonia.Controls;
using ReactiveUI;

using CKAN.IO;

namespace CKAN.LinuxGUI
{
    public partial class GameCommandLinesWindow : Window
    {
        private readonly GameInstance? instance;
        private readonly EditorViewModel viewModel;

        public GameCommandLinesWindow()
        {
            InitializeComponent();
            viewModel = new EditorViewModel(Array.Empty<string>(),
                                            Array.Empty<string>());
            DataContext = viewModel;
        }

        public GameCommandLinesWindow(GameInstance instance,
                                      SteamLibrary steamLibrary)
        {
            InitializeComponent();
            this.instance = instance;
            var defaults = instance.Game.DefaultCommandLines(steamLibrary,
                                                             new System.IO.DirectoryInfo(instance.GameDir));
            var current = GameCommandLineConfigStore.Load(instance,
                                                          steamLibrary,
                                                          out var loadWarning);
            viewModel = new EditorViewModel(current, defaults, loadWarning);
            DataContext = viewModel;
        }

        private void ResetToDefaultsButton_OnClick(object? sender,
                                                   Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.ResetToDefaults();

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private void SaveButton_OnClick(object? sender,
                                        Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (instance == null || !viewModel.TryValidate())
            {
                return;
            }

            GameCommandLineConfigStore.Save(instance, viewModel.CommandLines);
            Close(true);
        }

        private sealed class EditorViewModel : ReactiveObject
        {
            private readonly string[] defaults;
            private string commandLinesText;
            private string validationMessage = "";

            public EditorViewModel(IReadOnlyCollection<string> currentCommandLines,
                                   IEnumerable<string>         defaults,
                                   string                      initialValidationMessage = "")
            {
                this.defaults = defaults.Where(line => !string.IsNullOrWhiteSpace(line))
                                        .Distinct()
                                        .ToArray();
                commandLinesText = string.Join(Environment.NewLine,
                                               currentCommandLines
                                                   .Where(line => !string.IsNullOrWhiteSpace(line))
                                                   .Distinct());
                validationMessage = initialValidationMessage;
            }

            public string CommandLinesText
            {
                get => commandLinesText;
                set
                {
                    this.RaiseAndSetIfChanged(ref commandLinesText, value);
                    this.RaisePropertyChanged(nameof(PreviewText));
                }
            }

            public string PreviewText
                => CommandLines.Length == 0
                    ? "No launch commands are configured."
                    : $"Launch commands that will be saved:{Environment.NewLine}{string.Join(Environment.NewLine, CommandLines)}";

            public string ValidationMessage
            {
                get => validationMessage;
                private set
                {
                    this.RaiseAndSetIfChanged(ref validationMessage, value);
                    this.RaisePropertyChanged(nameof(ShowValidationMessage));
                }
            }

            public bool ShowValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

            public string[] CommandLines
                => CommandLinesText.Split(new[] { "\r\n", "\n", "\r" },
                                          StringSplitOptions.RemoveEmptyEntries)
                                   .Select(line => line.Trim())
                                   .Where(line => !string.IsNullOrWhiteSpace(line))
                                   .Distinct()
                                   .ToArray();

            public void ResetToDefaults()
            {
                CommandLinesText = string.Join(Environment.NewLine, defaults);
                ValidationMessage = "";
            }

            public bool TryValidate()
            {
                var lines = ParsedCommandLines();
                var duplicate = lines.GroupBy(line => line, StringComparer.Ordinal)
                                     .FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null)
                {
                    ValidationMessage = $"Duplicate launch command: {duplicate.Key}";
                    return false;
                }

                if (lines.Length > 0)
                {
                    ValidationMessage = "";
                    return true;
                }

                ValidationMessage = "At least one launch command is required.";
                return false;
            }

            private string[] ParsedCommandLines()
                => CommandLinesText.Split(new[] { "\r\n", "\n", "\r" },
                                          StringSplitOptions.RemoveEmptyEntries)
                                   .Select(line => line.Trim())
                                   .Where(line => !string.IsNullOrWhiteSpace(line))
                                   .ToArray();
        }
    }
}
