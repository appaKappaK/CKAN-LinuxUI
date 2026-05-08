using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class SimplePromptWindow : Window
    {
        public SimplePromptWindow() : this("", Array.Empty<string>())
        {
        }

        public SimplePromptWindow(string                prompt,
                                  IReadOnlyList<string> options,
                                  string                confirmLabel = "OK",
                                  string                cancelLabel  = "Cancel")
        {
            InitializeComponent();
            DataContext = new PromptViewModel(prompt, options, confirmLabel, cancelLabel);
        }

        private void ConfirmButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is PromptViewModel { CanConfirm: true } viewModel)
            {
                Close(viewModel.SelectedIndex);
            }
        }

        private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(-1);
        }

        private sealed class PromptViewModel : ReactiveObject
        {
            private int selectedIndex;

            public PromptViewModel(string                prompt,
                                   IReadOnlyList<string> options,
                                   string                confirmLabel,
                                   string                cancelLabel)
            {
                Prompt = prompt;
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "OK" : confirmLabel;
                CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
                Options = ShouldRenderOptions(options, ConfirmLabel, CancelLabel)
                    ? options.Select(PromptOption.FromText).ToList()
                    : Array.Empty<PromptOption>();
                selectedIndex = Options.Count > 0 ? -1 : 0;
            }

            public string Prompt { get; }

            public IReadOnlyList<PromptOption> Options { get; }

            public bool HasOptions => Options.Count > 0;

            public string SelectionHint
                => Prompt.Contains("provider", StringComparison.OrdinalIgnoreCase)
                   || Prompt.Contains("dependency", StringComparison.OrdinalIgnoreCase)
                    ? "This dependency can be satisfied by more than one mod. Select the provider you want CKAN to install."
                    : "Select one option, then confirm.";

            public string ConfirmLabel { get; }

            public string CancelLabel { get; }

            public int SelectedIndex
            {
                get => selectedIndex;
                set
                {
                    this.RaiseAndSetIfChanged(ref selectedIndex, value);
                    this.RaisePropertyChanged(nameof(CanConfirm));
                }
            }

            public bool CanConfirm => !HasOptions || SelectedIndex >= 0;

            private static bool ShouldRenderOptions(IReadOnlyList<string> options,
                                                    string                confirmLabel,
                                                    string                cancelLabel)
                => options.Count > 0
                   && !(options.Count == 2
                        && string.Equals(options[0], confirmLabel, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(options[1], cancelLabel, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class PromptOption
        {
            private PromptOption(string primary,
                                 string secondary,
                                 string detail)
            {
                Primary = primary;
                Secondary = secondary;
                Detail = detail;
            }

            public string Primary { get; }

            public string Secondary { get; }

            public string Detail { get; }

            public bool HasSecondary => !string.IsNullOrWhiteSpace(Secondary);

            public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

            public static PromptOption FromText(string text)
            {
                var value = text?.Trim() ?? "";
                var lines = value.Split(new[] { '\r', '\n' },
                                        StringSplitOptions.RemoveEmptyEntries
                                        | StringSplitOptions.TrimEntries);
                var firstLine = lines.FirstOrDefault() ?? "";
                var detail = lines.Length > 1
                    ? string.Join(Environment.NewLine, lines.Skip(1))
                    : "";
                var nameStart = firstLine.LastIndexOf(" (", StringComparison.Ordinal);
                if (nameStart > 0
                    && firstLine.EndsWith(")", StringComparison.Ordinal)
                    && nameStart + 2 < firstLine.Length - 1)
                {
                    return new PromptOption(firstLine[..nameStart],
                                            firstLine[(nameStart + 2)..^1],
                                            detail);
                }

                return new PromptOption(firstLine, "", detail);
            }
        }
    }
}
