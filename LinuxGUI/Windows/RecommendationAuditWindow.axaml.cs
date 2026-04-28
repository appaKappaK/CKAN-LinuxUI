using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Input;
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

        private void CheckSuggestionsButton_OnClick(object? sender,
                                                    Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectSuggestions();

        private void CheckAllButton_OnClick(object? sender,
                                            Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectAll();

        private void UncheckAllButton_OnClick(object? sender,
                                              Avalonia.Interactivity.RoutedEventArgs e)
            => viewModel.SelectNone();

        private void RecommendationRow_OnPointerPressed(object? sender,
                                                        PointerPressedEventArgs e)
        {
            if (sender is Control { DataContext: RecommendationAuditItem item })
            {
                viewModel.SelectedDetailItem = item;
            }
        }

        private void QueueSelectedButton_OnClick(object? sender,
                                                 Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CancelButton_OnClick(object? sender,
                                          Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);

        private sealed class WindowViewModel : ReactiveObject
        {
            private RecommendationAuditItem? selectedDetailItem;

            public WindowViewModel(IReadOnlyList<RecommendationAuditItem> items,
                                   string                                 instanceName)
            {
                Items = new ObservableCollection<RecommendationAuditItem>(items);
                Rows = BuildRows(items);
                SummaryText = $"Review recommended, suggested, and supporting mods for {instanceName}. Checked items will be added to the Preview queue.";

                foreach (var item in Items)
                {
                    item.WhenAnyValue(entry => entry.IsSelected)
                        .Subscribe(_ =>
                        {
                            this.RaisePropertyChanged(nameof(SelectionSummary));
                            this.RaisePropertyChanged(nameof(PrimaryActionLabel));
                        });
                }
            }

            public ObservableCollection<RecommendationAuditItem> Items { get; }

            public IReadOnlyList<object> Rows { get; }

            public string SummaryText { get; }

            public RecommendationAuditItem? SelectedDetailItem
            {
                get => selectedDetailItem;
                set
                {
                    if (selectedDetailItem == value)
                    {
                        return;
                    }

                    if (selectedDetailItem != null)
                    {
                        selectedDetailItem.IsDetailSelected = false;
                    }

                    this.RaiseAndSetIfChanged(ref selectedDetailItem, value);

                    if (selectedDetailItem != null)
                    {
                        selectedDetailItem.IsDetailSelected = true;
                    }

                    this.RaisePropertyChanged(nameof(ShowDetailPane));
                }
            }

            public bool ShowDetailPane => SelectedDetailItem != null;

            public int SelectedCount
                => Items.Count(item => item.IsSelected && item.CanQueue);

            public string SelectionSummary
            {
                get
                {
                    var selected = SelectedCount;
                    return selected == 1
                        ? "1 item selected"
                        : $"{selected} items selected";
                }
            }

            public string PrimaryActionLabel
                => SelectedCount == 0 ? "Continue" : "Queue Selected";

            public void SelectRecommendations()
                => SelectKind("Recommendation");

            public void SelectSuggestions()
                => SelectKind("Suggestion");

            public void SelectAll()
                => SetSelection(_ => true);

            public void SelectNone()
                => SetSelection(_ => false);

            private void SelectKind(string kind)
            {
                foreach (var item in Items.Where(item => item.CanQueue && item.Kind == kind))
                {
                    item.IsSelected = true;
                }
                this.RaisePropertyChanged(nameof(SelectionSummary));
            }

            private void SetSelection(Func<RecommendationAuditItem, bool> predicate)
            {
                foreach (var item in Items)
                {
                    item.IsSelected = item.CanQueue && predicate(item);
                }
                this.RaisePropertyChanged(nameof(SelectionSummary));
            }

            private static IReadOnlyList<object> BuildRows(IReadOnlyList<RecommendationAuditItem> items)
            {
                var rows = new List<object>();
                AddGroup(rows, "Recommendations", items.Where(item => item.Kind == "Recommendation"));
                AddGroup(rows, "Suggestions", items.Where(item => item.Kind == "Suggestion"));
                AddGroup(rows, "Supported By (not endorsed by chosen mods)", items.Where(item => item.Kind == "Supporter"));
                return rows;
            }

            private static void AddGroup(List<object> rows,
                                         string       title,
                                         IEnumerable<RecommendationAuditItem> items)
            {
                var groupItems = items.ToList();
                if (groupItems.Count == 0)
                {
                    return;
                }

                rows.Add(new RecommendationAuditGroupHeader(title));
                rows.AddRange(groupItems);
            }
        }
    }
}
