using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using Avalonia.Controls;
using ReactiveUI;

namespace CKAN.LinuxGUI
{
    public partial class RecommendationAuditWindow : Window
    {
        private readonly WindowViewModel viewModel;

        public RecommendationAuditWindow()
        {
            InitializeComponent();
            viewModel = new WindowViewModel(Array.Empty<RecommendationAuditItem>(), "current instance");
            DataContext = viewModel;
        }

        public RecommendationAuditWindow(IReadOnlyList<RecommendationAuditItem> items,
                                         string                                 instanceName)
        {
            InitializeComponent();
            viewModel = new WindowViewModel(items, instanceName);
            DataContext = viewModel;
        }

        public IReadOnlyList<RecommendationAuditItem> SelectedItems
            => viewModel.Items.Where(item => item.IsSelected && item.CanQueue).ToList();

        private void CheckRecommendationsButton_OnClick(object? sender,
                                                        Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectRecommendations();

        private void CheckAllButton_OnClick(object? sender,
                                            Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectAll();

        private void UncheckAllButton_OnClick(object? sender,
                                              Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectNone();

        private void QueueSelectedButton_OnClick(object? sender,
                                                 Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private sealed class WindowViewModel : ReactiveObject
        {
            public WindowViewModel(IReadOnlyList<RecommendationAuditItem> items,
                                   string                                 instanceName)
            {
                Items = new ObservableCollection<RecommendationAuditItem>(items);
                SummaryText = $"Review recommended, suggested, and supporting mods for {instanceName}. Checked items will be added to the Preview queue.";

                foreach (var item in Items)
                {
                    item.WhenAnyValue(entry => entry.IsSelected)
                        .Subscribe(_ => this.RaisePropertyChanged(nameof(SelectionSummary)));
                }
            }

            public ObservableCollection<RecommendationAuditItem> Items { get; }

            public string SummaryText { get; }

            public string SelectionSummary
            {
                get
                {
                    var selected = Items.Count(item => item.IsSelected && item.CanQueue);
                    return selected == 1
                        ? "1 item selected"
                        : $"{selected} items selected";
                }
            }

            public void SelectRecommendations()
                => SetSelection(item => item.Kind == "Recommendation");

            public void SelectAll()
                => SetSelection(_ => true);

            public void SelectNone()
                => SetSelection(_ => false);

            private void SetSelection(Func<RecommendationAuditItem, bool> predicate)
            {
                foreach (var item in Items)
                {
                    item.IsSelected = item.CanQueue && predicate(item);
                }
                this.RaisePropertyChanged(nameof(SelectionSummary));
            }
        }
    }
}
