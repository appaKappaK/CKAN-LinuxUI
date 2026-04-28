using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;

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
            Close((DataContext as PromptViewModel)?.SelectedIndex ?? 0);
        }

        private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(-1);
        }

        private sealed class PromptViewModel
        {
            public PromptViewModel(string                prompt,
                                   IReadOnlyList<string> options,
                                   string                confirmLabel,
                                   string                cancelLabel)
            {
                Prompt = prompt;
                Options = options.Select(PromptOption.FromText).ToList();
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "OK" : confirmLabel;
                CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
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

            public int SelectedIndex { get; set; }
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
