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

        public SimplePromptWindow(string prompt, IReadOnlyList<string> options)
        {
            InitializeComponent();
            DataContext = new PromptViewModel(prompt, options);
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
            public PromptViewModel(string prompt, IReadOnlyList<string> options)
            {
                Prompt = prompt;
                Options = options.Count > 0 ? options : new List<string> { "Yes", "No" };
            }

            public string Prompt { get; }

            public IReadOnlyList<string> Options { get; }

            public int SelectedIndex { get; set; }
        }
    }
}
