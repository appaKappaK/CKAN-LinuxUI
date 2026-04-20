using System;
using System.Collections.Generic;

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
                Options = options.Count > 0 ? options : new List<string> { "Yes", "No" };
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "OK" : confirmLabel;
                CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
            }

            public string Prompt { get; }

            public IReadOnlyList<string> Options { get; }

            public string ConfirmLabel { get; }

            public string CancelLabel { get; }

            public int SelectedIndex { get; set; }
        }
    }
}
