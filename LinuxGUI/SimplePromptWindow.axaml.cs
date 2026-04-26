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

            public string OptionsSummary
                => Options.Count == 1
                    ? "1 option available"
                    : $"{Options.Count} options available";

            public string ConfirmLabel { get; }

            public string CancelLabel { get; }

            public int SelectedIndex { get; set; }
        }

        private sealed class PromptOption
        {
            private PromptOption(string primary,
                                 string secondary)
            {
                Primary = primary;
                Secondary = secondary;
            }

            public string Primary { get; }

            public string Secondary { get; }

            public bool HasSecondary => !string.IsNullOrWhiteSpace(Secondary);

            public static PromptOption FromText(string text)
            {
                var value = text?.Trim() ?? "";
                var nameStart = value.LastIndexOf(" (", StringComparison.Ordinal);
                if (nameStart > 0
                    && value.EndsWith(")", StringComparison.Ordinal)
                    && nameStart + 2 < value.Length - 1)
                {
                    return new PromptOption(value[..nameStart],
                                            value[(nameStart + 2)..^1]);
                }

                return new PromptOption(value, "");
            }
        }
    }
}
