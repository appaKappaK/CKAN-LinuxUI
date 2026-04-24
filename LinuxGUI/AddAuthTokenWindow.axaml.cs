using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CKAN.LinuxGUI
{
    public partial class AddAuthTokenWindow : Window
    {
        private readonly HashSet<string> existingHosts;

        public AddAuthTokenWindow()
            : this(Array.Empty<string>())
        {
        }

        public AddAuthTokenWindow(IReadOnlyCollection<string> existingHosts)
        {
            InitializeComponent();
            this.existingHosts = existingHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
            AcceptButton.IsEnabled = false;
        }

        public string ResultHost => HostTextBox.Text?.Trim() ?? "";

        public string ResultToken => TokenTextBox.Text?.Trim() ?? "";

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            HostTextBox.Focus();
        }

        private void InputTextBox_OnTextChanged(object? sender,
                                                TextChangedEventArgs e)
        {
            ErrorTextBlock.Text = "";
            AcceptButton.IsEnabled = !string.IsNullOrWhiteSpace(HostTextBox.Text)
                                  && !string.IsNullOrWhiteSpace(TokenTextBox.Text);
        }

        private void AcceptButton_OnClick(object? sender,
                                          RoutedEventArgs e)
        {
            var host = ResultHost;
            var token = ResultToken;
            if (host.Length == 0)
            {
                ErrorTextBlock.Text = "Host is required.";
                return;
            }
            if (token.Length == 0)
            {
                ErrorTextBlock.Text = "Token is required.";
                return;
            }
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
            {
                ErrorTextBlock.Text = $"{host} is not a valid host name.";
                return;
            }
            if (existingHosts.Contains(host))
            {
                ErrorTextBlock.Text = $"A token already exists for {host}.";
                return;
            }

            Close(true);
        }

        private void CancelButton_OnClick(object? sender,
                                          RoutedEventArgs e)
            => Close(false);
    }
}
