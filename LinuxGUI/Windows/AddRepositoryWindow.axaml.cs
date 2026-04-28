using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CKAN.LinuxGUI
{
    public partial class AddRepositoryWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public AddRepositoryWindow()
            : this(Array.Empty<Repository>())
        {
        }

        public AddRepositoryWindow(IReadOnlyCollection<Repository> officialRepositories)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(officialRepositories);
            DataContext = viewModel;
            AddButton.IsEnabled = false;
        }

        public Repository? Selection { get; private set; }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (viewModel.OfficialRepositories.Count > 0)
            {
                OfficialReposListBox.SelectedIndex = 0;
            }
            RepoNameTextBox.Focus();
        }

        private void OfficialReposListBox_OnSelectionChanged(object? sender,
                                                             SelectionChangedEventArgs e)
        {
            if (OfficialReposListBox.SelectedItem is RepositoryOption option)
            {
                RepoNameTextBox.Text = option.Name;
                RepoUrlTextBox.Text = option.Url;
                ErrorTextBlock.Text = "";
                UpdateAddButton();
            }
        }

        private void OfficialReposListBox_OnDoubleTapped(object? sender,
                                                         TappedEventArgs e)
            => TryAccept();

        private void RepoTextBox_OnTextChanged(object? sender,
                                               TextChangedEventArgs e)
        {
            ErrorTextBlock.Text = "";
            UpdateAddButton();
        }

        private void AddButton_OnClick(object? sender,
                                       RoutedEventArgs e)
            => TryAccept();

        private void CancelButton_OnClick(object? sender,
                                          RoutedEventArgs e)
            => Close(false);

        private void TryAccept()
        {
            var name = RepoNameTextBox.Text?.Trim() ?? "";
            var url = RepoUrlTextBox.Text?.Trim() ?? "";
            if (name.Length == 0 || url.Length == 0)
            {
                ErrorTextBlock.Text = "Repository name and URL are required.";
                return;
            }

            try
            {
                Selection = new Repository(name, url);
                Close(true);
            }
            catch (Exception ex)
            {
                ErrorTextBlock.Text = ex.Message;
            }
        }

        private void UpdateAddButton()
            => AddButton.IsEnabled = !string.IsNullOrWhiteSpace(RepoNameTextBox.Text)
                                  && !string.IsNullOrWhiteSpace(RepoUrlTextBox.Text);

        private sealed class WindowViewModel
        {
            public WindowViewModel(IReadOnlyCollection<Repository> officialRepositories)
            {
                OfficialRepositories = officialRepositories
                    .Select(repo => new RepositoryOption(repo.name, repo.uri?.ToString() ?? ""))
                    .ToList();
            }

            public IReadOnlyList<RepositoryOption> OfficialRepositories { get; }
        }

        private sealed record RepositoryOption(string Name, string Url);
    }
}
