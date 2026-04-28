using System;

using Avalonia.Controls;

namespace CKAN.LinuxGUI
{
    public partial class TextInputWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public TextInputWindow()
            : this("Input", "Value", "", "OK")
        {
        }

        public TextInputWindow(string title,
                               string prompt,
                               string initialValue,
                               string confirmLabel)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(title, prompt, initialValue, confirmLabel);
            DataContext = viewModel;
        }

        public string ResultText => viewModel.Value?.Trim() ?? "";

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void ConfirmButton_OnClick(object? sender,
                                           Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private sealed class WindowViewModel
        {
            public WindowViewModel(string title,
                                   string prompt,
                                   string initialValue,
                                   string confirmLabel)
            {
                Title = title;
                Prompt = prompt;
                Value = initialValue;
                ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "OK" : confirmLabel;
            }

            public string Title { get; }

            public string Prompt { get; }

            public string? Value { get; set; }

            public string ConfirmLabel { get; }
        }
    }
}
