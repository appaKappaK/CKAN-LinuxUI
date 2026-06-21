using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
                Prompt = prompt?.Trim() ?? "";
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "OK" : confirmLabel;
                CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
                Options = ShouldRenderOptions(options, ConfirmLabel, CancelLabel)
                    ? options.Select(PromptOption.FromText).ToList()
                    : Array.Empty<PromptOption>();
                (PromptIntro, PromptDetail, PromptOutro) = ParsePromptSections(Prompt, Options.Count > 0);
                selectedIndex = Options.Count > 0 ? -1 : 0;
            }

            public string Prompt { get; }

            public string PromptIntro { get; }

            public string PromptDetail { get; }

            public string PromptOutro { get; }

            public IReadOnlyList<PromptOption> Options { get; }

            public bool HasOptions => Options.Count > 0;

            public bool HasPromptIntro => !string.IsNullOrWhiteSpace(PromptIntro);

            public bool HasPromptDetail => !string.IsNullOrWhiteSpace(PromptDetail);

            public bool HasPromptOutro => !string.IsNullOrWhiteSpace(PromptOutro);

            public bool ShowSinglePrompt => !HasPromptDetail;

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

            private static (string Intro, string Detail, string Outro) ParsePromptSections(string prompt,
                                                                                           bool   hasOptions)
            {
                if (hasOptions || string.IsNullOrWhiteSpace(prompt))
                {
                    return ("", "", "");
                }

                var sections = Regex.Split(prompt, @"(?:\r?\n){2,}")
                                    .Select(section => section.Trim())
                                    .Where(section => !string.IsNullOrWhiteSpace(section))
                                    .ToArray();

                if (sections.Length < 3)
                {
                    return ("", "", "");
                }

                var detail = string.Join(Environment.NewLine + Environment.NewLine,
                                         sections.Skip(1).Take(sections.Length - 2));
                if (!LooksLikeDetailBlock(detail))
                {
                    return ("", "", "");
                }

                return (sections[0], detail, sections[^1]);
            }

            private static bool LooksLikeDetailBlock(string detail)
            {
                var lines = detail.Split(new[] { '\r', '\n' },
                                         StringSplitOptions.RemoveEmptyEntries
                                         | StringSplitOptions.TrimEntries);
                return lines.Length > 1
                       || lines.Any(line => line.StartsWith("- ", StringComparison.Ordinal));
            }
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
