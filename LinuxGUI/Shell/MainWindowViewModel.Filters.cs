using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.Exporters;
using CKAN.IO;
using CKAN.Types;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public sealed partial class MainWindowViewModel : ReactiveObject
    {
        private void ClearFilter(ref bool field, string propertyName)
        {
            if (field)
            {
                field = false;
                this.RaisePropertyChanged(propertyName);
            }
        }

        private static int CountTriStateFilter(bool? value)
            => value.HasValue ? 1 : 0;

        private static int TriStateFilterToIndex(bool? value)
            => value switch
            {
                true  => 1,
                false => 2,
                _     => 0,
            };

        private static bool? TriStateIndexToFilter(int value)
            => value switch
            {
                1 => true,
                2 => false,
                _ => null,
            };

        private static bool? GetTriStateFilterValue(bool includeOnly, bool excludeOnly)
            => includeOnly == excludeOnly ? (bool?)null : includeOnly;

        private static void AddTriStateSummary(ICollection<string> parts, string label, bool? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            parts.Add($"{label}: {(value.Value ? "Yes" : "No")}");
        }

        private IReadOnlyList<FilterTagOptionItem> BuildAvailableTagOptions(IEnumerable<ModListItem> items,
                                                                            FilterState              currentFilter)
        {
            var sourceItems = items.ToList();
            var selectedValues = SelectedFilterValues(currentFilter.TagText);
            var allTags = sourceItems.SelectMany(item => SplitListValues(item.Tags))
                                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                                     .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
                                     .ToList();

            return allTags.Select(tag =>
                          {
                              var previewValues = selectedValues.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
                              previewValues.Add(tag);
                              int count = modCatalogService.ApplyFilter(sourceItems,
                                                                        currentFilter with
                                                                        {
                                                                            TagText = SerializeFilterValues(previewValues),
                                                                        })
                                                           .Count;
                              return new FilterTagOptionItem(tag,
                                                             count,
                                                             selectedValues.Contains(tag));
                          })
                          .ToList();
        }

        private static IReadOnlyList<FilterTagOptionItem> BuildAvailableLabelOptions(IEnumerable<ModListItem> items,
                                                                                     string                  selectedLabel)
            => BuildAvailableFilterOptions(items, selectedLabel, item => item.Labels);

        private static IReadOnlyList<FilterTagOptionItem> BuildAvailableFilterOptions(IEnumerable<ModListItem> items,
                                                                                      string                  selectedValue,
                                                                                      Func<ModListItem, string> selector)
        {
            var counts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var item in items)
            {
                var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (string value in selector(item).Split(',',
                                                             StringSplitOptions.RemoveEmptyEntries
                                                             | StringSplitOptions.TrimEntries))
                {
                    if (!seen.Add(value))
                    {
                        continue;
                    }

                    counts[value] = counts.TryGetValue(value, out int current)
                        ? current + 1
                        : 1;
                }
            }

            var selectedValues = SelectedFilterValues(selectedValue);
            return counts.OrderBy(kvp => kvp.Key, StringComparer.CurrentCultureIgnoreCase)
                         .Select(kvp => new FilterTagOptionItem(kvp.Key,
                                                                kvp.Value,
                                                                selectedValues.Contains(kvp.Key)))
                         .ToList();
        }

        private static HashSet<string> SelectedFilterValues(string? text)
            => SplitFilterValues(text).ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        private static IEnumerable<string> SplitFilterValues(string? text)
            => (text ?? "").Split(new[] { ',', ';', '\n', '\r' },
                                  StringSplitOptions.RemoveEmptyEntries
                                  | StringSplitOptions.TrimEntries)
                           .Where(value => !string.IsNullOrWhiteSpace(value));

        private static IEnumerable<string> SplitListValues(string? text)
            => (text ?? "").Split(',',
                                  StringSplitOptions.RemoveEmptyEntries
                                  | StringSplitOptions.TrimEntries)
                           .Where(value => !string.IsNullOrWhiteSpace(value));

        private static string SerializeFilterValues(IEnumerable<string> values)
            => string.Join("; ",
                           values.Where(value => !string.IsNullOrWhiteSpace(value))
                                 .Distinct(StringComparer.CurrentCultureIgnoreCase)
                                 .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase));

        private IEnumerable<(string Label, string Value)> EnumerateAdvancedTextFilters()
        {
            yield return ("Name", AdvancedNameFilter);
            yield return ("Identifier", AdvancedIdentifierFilter);
            yield return ("Author", AdvancedAuthorFilter);
            yield return ("Summary", AdvancedSummaryFilter);
            yield return ("Description", AdvancedDescriptionFilter);
            yield return ("License", AdvancedLicenseFilter);
            yield return ("Language", AdvancedLanguageFilter);
            yield return ("Depends", AdvancedDependsFilter);
            yield return ("Recommends", AdvancedRecommendsFilter);
            yield return ("Suggests", AdvancedSuggestsFilter);
            yield return ("Conflicts", AdvancedConflictsFilter);
            yield return ("Supports", AdvancedSupportsFilter);
            yield return ("Category", AdvancedTagsFilter);
            yield return ("Labels", AdvancedLabelsFilter);
            yield return ("Compatibility", AdvancedCompatibilityFilter);
        }

        private bool SetFilterBackingField(ref bool field, bool value, string propertyName)
        {
            if (field == value)
            {
                return false;
            }

            field = value;
            this.RaisePropertyChanged(propertyName);
            return true;
        }

        private void SetExclusiveTriStateFilter(bool?  value,
                                                ref bool includeOnlyField,
                                                string includeOnlyPropertyName,
                                                ref bool excludeOnlyField,
                                                string excludeOnlyPropertyName,
                                                string triStatePropertyName)
        {
            bool includeOnly = value == true;
            bool excludeOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref includeOnlyField, includeOnly, includeOnlyPropertyName);
                changed |= SetFilterBackingField(ref excludeOnlyField, excludeOnly, excludeOnlyPropertyName);
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(triStatePropertyName);
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void SetInstalledFilterState(bool? value)
        {
            bool installedOnly = value == true;
            bool notInstalledOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref filterInstalledOnly, installedOnly, nameof(FilterInstalledOnly));
                changed |= SetFilterBackingField(ref filterNotInstalledOnly, notInstalledOnly, nameof(FilterNotInstalledOnly));
                if (notInstalledOnly)
                {
                    changed |= SetFilterBackingField(ref filterUpdatableOnly, false, nameof(FilterUpdatableOnly));
                }
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(nameof(FilterInstalledState));
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void SetUpdatableFilterState(bool? value)
        {
            bool updatableOnly = value == true;
            bool notUpdatableOnly = value == false;
            bool changed = false;

            suppressFilterAutoRefresh = true;
            try
            {
                changed |= SetFilterBackingField(ref filterUpdatableOnly, updatableOnly, nameof(FilterUpdatableOnly));
                changed |= SetFilterBackingField(ref filterNotUpdatableOnly, notUpdatableOnly, nameof(FilterNotUpdatableOnly));
                if (updatableOnly)
                {
                    changed |= SetFilterBackingField(ref filterNotInstalledOnly, false, nameof(FilterNotInstalledOnly));
                }
            }
            finally
            {
                suppressFilterAutoRefresh = false;
            }

            if (!changed)
            {
                return;
            }

            this.RaisePropertyChanged(nameof(FilterUpdatableState));
            PublishFilterStateLabels();
            RefreshCatalogForFilterChange();
        }

        private void NormalizeStoredFilterFlags()
        {
            if (filterInstalledOnly && filterNotInstalledOnly)
            {
                filterNotInstalledOnly = false;
            }
            if (filterNotInstalledOnly && filterUpdatableOnly)
            {
                filterUpdatableOnly = false;
            }
            if (filterUpdatableOnly && filterNotUpdatableOnly)
            {
                filterNotUpdatableOnly = false;
            }
            if (filterCompatibleOnly && filterIncompatibleOnly)
            {
                filterIncompatibleOnly = false;
            }
            if (filterCachedOnly && filterUncachedOnly)
            {
                filterUncachedOnly = false;
            }
            if (filterHasReplacementOnly && filterNoReplacementOnly)
            {
                filterNoReplacementOnly = false;
            }
        }
    }
}
