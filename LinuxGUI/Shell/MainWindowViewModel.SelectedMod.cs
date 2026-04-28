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
        private static void ReplacePreviewCollection(ObservableCollection<string> target,
                                                     System.Collections.Generic.IEnumerable<string> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private static void ReplaceSelectedModCollection(ObservableCollection<ModRelationshipItem> target,
                                                         IEnumerable<ModRelationshipItem>         values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }
        }

        private void ReplaceSelectedModResourceLinks(IEnumerable<ModResourceLinkItem> values)
        {
            SelectedModResourceLinks.Clear();
            foreach (var value in values)
            {
                SelectedModResourceLinks.Add(value);
            }

            this.RaisePropertyChanged(nameof(ShowSelectedModResourceLinks));
        }

        private static IReadOnlyList<ModResourceLinkItem> BuildSelectedModResourceLinks(ResourcesDescriptor? resources)
        {
            if (resources == null)
            {
                return Array.Empty<ModResourceLinkItem>();
            }

            var links = new List<ModResourceLinkItem>();

            AddResourceLink(links, "Home page", resources.homepage);
            AddResourceLink(links, "Repository", resources.repository);
            AddResourceLink(links, "Bug tracker", resources.bugtracker);
            AddResourceLink(links, "SpaceDock", resources.spacedock);
            AddResourceLink(links, "Discussions", resources.discussions);
            AddResourceLink(links, "Manual", resources.manual);
            AddResourceLink(links, "License", resources.license);
            AddResourceLink(links, "Curse", resources.curse);
            AddResourceLink(links, "CI", resources.ci);
            AddResourceLink(links, "Metanetkan", resources.metanetkan);
            AddResourceLink(links, "Remote version info", resources.remoteSWInfo);
            AddResourceLink(links, "Remote version file", resources.remoteAvc);
            AddResourceLink(links, "Store", resources.store);
            AddResourceLink(links, "Steam", resources.steamstore);
            AddResourceLink(links, "GOG", resources.gogstore);
            AddResourceLink(links, "Epic", resources.epicstore);

            return links;
        }

        private static void AddResourceLink(ICollection<ModResourceLinkItem> target,
                                            string                           label,
                                            Uri?                             url)
        {
            if (url == null)
            {
                return;
            }

            target.Add(new ModResourceLinkItem
            {
                Label = label,
                Url   = url.ToString(),
            });
        }

        private void PopulateSelectedModVersionChoices(ModDetailsModel details)
        {
            SelectedModAvailableVersions.Clear();
            foreach (var choice in BuildSelectedModVersionChoices(details.Identifier))
            {
                SelectedModAvailableVersions.Add(choice);
            }

            var preferredChoice = SelectedModAvailableVersions.FirstOrDefault(choice =>
                                      choice.VersionText == details.LatestVersion)
                                  ?? SelectedModAvailableVersions.FirstOrDefault(choice =>
                                      choice.IsInstalledVersion && !details.HasVersionUpdate)
                                  ?? SelectedModAvailableVersions.FirstOrDefault();

            SelectedModVersionChoice = preferredChoice;
            this.RaisePropertyChanged(nameof(ShowSelectedModVersionPicker));
        }

        private IReadOnlyList<ModVersionChoiceItem> BuildSelectedModVersionChoices(string identifier)
        {
            if (CurrentRegistry == null || CurrentInstance == null)
            {
                return Array.Empty<ModVersionChoiceItem>();
            }

            var installedVersion = CurrentRegistry.InstalledModule(identifier)?.Module.version;
            var modules = Enumerable.Repeat(CurrentRegistry.InstalledModule(identifier)?.Module, 1)
                                    .Concat(Utilities.DefaultIfThrows(
                                                () => CurrentRegistry.AvailableByIdentifier(identifier))
                                            ?? Enumerable.Empty<CkanModule>())
                                    .OfType<CkanModule>()
                                    .Distinct()
                                    .OrderByDescending(module => module.version)
                                    .ToList();

            return modules.Select(module =>
                          {
                              var isCompatible = IsModuleInstallable(module,
                                                                     CurrentRegistry,
                                                                     CurrentInstance);
                              var comparison = installedVersion == null
                                  ? 0
                                  : module.version.CompareTo(installedVersion);
                              var badgeText = installedVersion?.Equals(module.version) == true
                                  ? "Installed"
                                  : "";
                              var badgeForeground = installedVersion?.Equals(module.version) == true
                                  ? "#8EC7F3"
                                  : "#AEB8C6";

                              return new ModVersionChoiceItem
                              {
                                  VersionText = module.version.ToString(),
                                  CompatibilityText = BuildVersionCompatibilityLabel(module, CurrentInstance),
                                  ReleaseDateText = module.release_date?.ToLocalTime().ToString("M/d/yyyy") ?? "Unknown",
                                  BadgeText = badgeText,
                                  BadgeForeground = badgeForeground,
                                  IsInstalledVersion = installedVersion?.Equals(module.version) == true,
                                  IsCompatible = isCompatible,
                                  VersionComparisonToInstalled = comparison,
                                  Module = module,
                              };
                          })
                          .ToList();
        }

        private void ApplySelectedVersionDetails()
        {
            if (selectedModDetails == null)
            {
                return;
            }

            if (SelectedModVersionChoice?.Module is not CkanModule module
                || CurrentInstance == null
                || CurrentRegistry == null)
            {
                SelectedModCompatibility = selectedModDetails.Compatibility;
                SelectedModCacheState = selectedModDetails.IsCached ? "Cached" : "Not cached";
                SelectedModModuleKind = selectedModDetails.ModuleKind;
                SelectedModLicense = selectedModDetails.License;
                SelectedModReleaseDate = selectedModDetails.ReleaseDate;
                SelectedModDownloadSize = selectedModDetails.DownloadSize;
                SelectedModRelationships = $"{selectedModDetails.DependencyCount} depends • {selectedModDetails.RecommendationCount} recommends • {selectedModDetails.SuggestionCount} suggests";
                SelectedModDependencyCountLabel = CountLabel(selectedModDetails.DependencyCount, "Dependency", "Dependencies");
                SelectedModRecommendationCountLabel = CountLabel(selectedModDetails.RecommendationCount, "Recommendation", "Recommendations");
                SelectedModSuggestionCountLabel = CountLabel(selectedModDetails.SuggestionCount, "Suggestion", "Suggestions");
                ReplaceSelectedModCollection(SelectedModDependencies, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModRecommendations, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModCollection(SelectedModSuggestions, Array.Empty<ModRelationshipItem>());
                ReplaceSelectedModResourceLinks(BuildSelectedModResourceLinks(selectedModDetails.Resources));
                SelectedModIsCached = selectedModDetails.IsCached;
                SelectedModIsIncompatible = selectedModDetails.IsIncompatible;
                UpdateSelectedModCachedArchivePath();
                PublishSelectedModRelationshipState();
                PublishSelectedModActionState();
                return;
            }

            SelectedModCompatibility = BuildVersionCompatibilityLabel(module, CurrentInstance);
            SelectedModCacheState = CurrentCache?.IsMaybeCachedZip(module) == true ? "Cached" : "Not cached";
            SelectedModModuleKind = FormatModuleKind(module.kind);
            SelectedModLicense = FormatLicense(module);
            SelectedModReleaseDate = module.release_date?.ToString("yyyy-MM-dd") ?? "Unknown";
            SelectedModDownloadSize = module.download_size > 0
                ? CkanModule.FmtSize(module.download_size)
                : "Unknown";

            var dependencies = BuildRelationshipEntries(module.depends);
            var recommendations = BuildRelationshipEntries(module.recommends);
            var suggestions = BuildRelationshipEntries(module.suggests);

            ReplaceSelectedModCollection(SelectedModDependencies, dependencies);
            ReplaceSelectedModCollection(SelectedModRecommendations, recommendations);
            ReplaceSelectedModCollection(SelectedModSuggestions, suggestions);
            ReplaceSelectedModResourceLinks(BuildSelectedModResourceLinks(module.resources ?? selectedModDetails.Resources));

            SelectedModRelationships = $"{dependencies.Count} depends • {recommendations.Count} recommends • {suggestions.Count} suggests";
            SelectedModDependencyCountLabel = CountLabel(dependencies.Count, "Dependency", "Dependencies");
            SelectedModRecommendationCountLabel = CountLabel(recommendations.Count, "Recommendation", "Recommendations");
            SelectedModSuggestionCountLabel = CountLabel(suggestions.Count, "Suggestion", "Suggestions");
            SelectedModIsCached = CurrentCache?.IsMaybeCachedZip(module) == true;
            SelectedModIsIncompatible = !IsModuleInstallable(module,
                                                             CurrentRegistry,
                                                             CurrentInstance);
            UpdateSelectedModCachedArchivePath();
            PublishSelectedModRelationshipState();
            PublishSelectedModActionState();
        }

        private void UpdateSelectedModCachedArchivePath()
        {
            selectedModCachedArchivePath = ResolveSelectedModCachedArchivePath();
            this.RaisePropertyChanged(nameof(ShowOpenSelectedModCacheLocationAction));
        }

        private string? ResolveSelectedModCachedArchivePath()
        {
            if (SelectedMod == null || CurrentCache == null)
            {
                return null;
            }

            if (SelectedModVersionChoice?.Module is CkanModule selectedModule
                && CurrentCache.GetCachedFilename(selectedModule) is string selectedPath)
            {
                return selectedPath;
            }

            if (CurrentRegistry == null)
            {
                return null;
            }

            return Enumerable.Repeat(CurrentRegistry.InstalledModule(SelectedMod.Identifier)?.Module, 1)
                             .Concat(Utilities.DefaultIfThrows(
                                         () => CurrentRegistry.AvailableByIdentifier(SelectedMod.Identifier))
                                     ?? Enumerable.Empty<CkanModule>())
                             .OfType<CkanModule>()
                             .Distinct()
                             .Select(CurrentCache.GetCachedFilename)
                             .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        }

        private List<ModRelationshipItem> BuildRelationshipEntries(IEnumerable<RelationshipDescriptor>? relationships)
            => (relationships ?? Enumerable.Empty<RelationshipDescriptor>())
                .Select(relationship => new ModRelationshipItem
                {
                    Text = CurrentRegistry != null && CurrentInstance != null
                        ? relationship.ToStringWithCompat(CurrentRegistry, CurrentInstance.Game)
                        : relationship.ToString() ?? "",
                    Identifiers = RelationshipIdentifiers(relationship).ToList(),
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .ToList();

        private static IEnumerable<string> RelationshipIdentifiers(RelationshipDescriptor relationship)
            => relationship switch
            {
                ModuleRelationshipDescriptor moduleRelationship
                    => Enumerable.Repeat(moduleRelationship.name, 1),
                AnyOfRelationshipDescriptor anyOfRelationship
                    => anyOfRelationship.any_of?.SelectMany(RelationshipIdentifiers)
                       ?? Enumerable.Empty<string>(),
                _ => Enumerable.Empty<string>(),
            };

        private void ShowRelationshipsInBrowser(string relationshipName,
                                                IEnumerable<ModRelationshipItem> relationships)
        {
            var identifiers = relationships.SelectMany(item => item.Identifiers)
                                           .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
                                           .Distinct(StringComparer.OrdinalIgnoreCase)
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (identifiers.Count == 0)
            {
                StatusMessage = $"No browser-visible {relationshipName} were found for {SelectedModTitle}.";
                return;
            }

            relationshipBrowserScopeIdentifiers = identifiers;
            relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeReturnsToPreview = false;
            RelationshipBrowserScopeText = $"{relationshipName} for {SelectedModTitle}";
            pendingModListScrollReset = true;
            ApplyCatalogFilterToLoadedItems(identifiers.FirstOrDefault());
            PublishRelationshipBrowserScopeState();
        }

        private void ShowPreviewEntriesInBrowser(string              relationshipName,
                                                 IEnumerable<string> entries)
        {
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddPreviewEntriesBrowserIdentifiers(identifiers, queueSources, relationshipName, entries);

            if (identifiers.Count == 0)
            {
                StatusMessage = $"No browser-visible {relationshipName} were found in the preview.";
                return;
            }

            relationshipBrowserScopeIdentifiers = identifiers;
            relationshipBrowserScopeQueueSources = queueSources;
            relationshipBrowserScopeReturnsToPreview = true;
            RelationshipBrowserScopeText = $"Preview {relationshipName}";
            pendingModListScrollReset = true;
            ShowBrowseSurfaceTab();
            ApplyCatalogFilterToLoadedItems(identifiers.FirstOrDefault());
            PublishRelationshipBrowserScopeState();
        }

        private void AddPreviewEntriesBrowserIdentifiers(HashSet<string>             identifiers,
                                                         Dictionary<string, string> queueSources,
                                                         string                     relationshipName,
                                                         IEnumerable<string>        entries)
        {
            foreach (var entry in entries)
            {
                AddPreviewEntryBrowserIdentifier(identifiers, queueSources, relationshipName, entry);
            }
        }

        private void AddPreviewEntryBrowserIdentifier(HashSet<string>             identifiers,
                                                      Dictionary<string, string> queueSources,
                                                      string                     relationshipName,
                                                      string                     entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            var mainText = PreviewEntryModText(entry);
            var matchingItem = FindPreviewEntryMod(mainText);
            if (matchingItem == null
                && string.Equals(mainText, entry.Trim(), StringComparison.Ordinal))
            {
                matchingItem = FindPreviewEntryMod(entry);
            }
            if (matchingItem != null)
            {
                identifiers.Add(matchingItem.Identifier);
                var source = PreviewEntryQueueSourceText(relationshipName, entry);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    queueSources[matchingItem.Identifier] = source;
                }
            }
        }

        private ModListItem? FindPreviewEntryMod(string entry)
            => allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => PreviewEntryMatchesMod(entry, item));

        private static string PreviewEntryQueueSourceText(string relationshipName,
                                                          string entry)
        {
            var sourceText = PreviewEntrySourceText(entry);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return relationshipName switch
                {
                    "recommendations" => "Recommended from preview",
                    "suggestions"     => "Suggested from preview",
                    "supporters"      => "Supported from preview",
                    "dependencies"    => "Required dependency from preview",
                    _                 => "",
                };
            }

            return relationshipName switch
            {
                "recommendations" => $"Recommended by {sourceText}",
                "suggestions"     => $"Suggested by {sourceText}",
                "supporters"      => $"Supported by {sourceText}",
                "dependencies"    => $"Required by {sourceText}",
                _                 => sourceText,
            };
        }

        private static string PreviewEntrySourceText(string entry)
        {
            var markers = new[]
            {
                " required by ",
                " recommended by ",
                " suggested by ",
                " supports ",
            };
            foreach (var marker in markers)
            {
                var index = entry.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && index + marker.Length < entry.Length)
                {
                    return entry[(index + marker.Length)..].Trim();
                }
            }

            return "";
        }

        private static string PreviewEntryModText(string entry)
        {
            var markers = new[]
            {
                " required by ",
                " recommended by ",
                " suggested by ",
                " supports ",
            };
            var markerIndex = markers
                .Select(marker => entry.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
                .Where(index => index >= 0)
                .DefaultIfEmpty(-1)
                .Min();
            return markerIndex >= 0
                ? entry[..markerIndex].Trim()
                : entry.Trim();
        }

        private IEnumerable<string> FilterPreviewOptionalEntries(IEnumerable<string> entries)
        {
            var queuedActions = changesetService.CurrentQueue.ToList();
            foreach (var entry in entries)
            {
                if (!PreviewEntryMatchesQueuedAction(entry, queuedActions))
                {
                    yield return entry;
                }
            }
        }

        private static bool PreviewEntryMatchesQueuedAction(string entry,
                                                            IReadOnlyList<QueuedActionModel> queuedActions)
        {
            var mainText = PreviewEntryModText(entry);
            return queuedActions.Any(action => PreviewEntryMatchesQueuedAction(mainText, action));
        }

        private static bool PreviewEntryMatchesQueuedAction(string            entry,
                                                            QueuedActionModel action)
            => ConflictSideStartsWith(entry, action.Identifier)
               || ConflictSideStartsWith(entry, action.Name)
               || ContainsText(entry, $"({action.Identifier} ")
               || ContainsText(entry, $"({action.Identifier})")
               || ContainsText(entry, action.Identifier)
               || ContainsText(entry, action.Name);

        private static bool PreviewEntryMatchesMod(string entry,
                                                   ModListItem item)
            => ConflictSideStartsWith(entry, item.Identifier)
               || ConflictSideStartsWith(entry, item.Name)
               || ContainsText(entry, $"({item.Identifier} ")
               || ContainsText(entry, $"({item.Identifier})")
               || ContainsText(entry, item.Identifier)
               || ContainsText(entry, item.Name);

        private IReadOnlySet<string> ConflictBrowserIdentifiers(string leftSide,
                                                                string rightSide)
        {
            var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddConflictBrowserIdentifier(identifiers, leftSide);
            AddConflictBrowserIdentifier(identifiers, rightSide);
            return identifiers;
        }

        private void AddConflictBrowserIdentifier(HashSet<string> identifiers,
                                                  string          side)
        {
            if (string.IsNullOrWhiteSpace(side))
            {
                return;
            }

            var matchingItem = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ConflictSideStartsWith(side, item.Identifier)
                                     || ConflictSideStartsWith(side, item.Name));
            if (matchingItem != null)
            {
                identifiers.Add(matchingItem.Identifier);
                return;
            }

            var displayName = StripConflictVersionSuffix(side);
            matchingItem = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ContainsText(item.Identifier, displayName)
                                     || ContainsText(item.Name, displayName)
                                     || ContainsText(displayName, item.Identifier)
                                     || ContainsText(displayName, item.Name));
            if (matchingItem != null)
            {
                identifiers.Add(matchingItem.Identifier);
            }
        }

        private void ClearRelationshipBrowserScope()
        {
            if (!ShowRelationshipBrowserScope)
            {
                return;
            }

            var returnToPreview = relationshipBrowserScopeReturnsToPreview;
            relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeReturnsToPreview = false;
            RelationshipBrowserScopeText = "";
            pendingModListScrollReset = true;
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems();
            }
            PublishRelationshipBrowserScopeState();
            if (returnToPreview)
            {
                ShowPreviewSurfaceTab();
            }
        }

        private async Task ReturnToPreviewAfterConflictQueueChangeAsync()
        {
            relationshipBrowserScopeIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeQueueSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            relationshipBrowserScopeReturnsToPreview = false;
            RelationshipBrowserScopeText = "";
            ShowPreviewSurface = true;
            IsPreviewLoading = true;
            PublishRelationshipBrowserScopeState();
            PublishFilterStateLabels();
            if (IsReady && allCatalogItems.Count > 0)
            {
                ApplyCatalogFilterToLoadedItems();
            }
            await Task.Delay(180);
            await LoadPreviewAsync();
        }

        private static bool IsModuleInstallable(CkanModule module,
                                                IRegistryQuerier registry,
                                                GameInstance instance)
        {
            var versionCriteria = instance.VersionCriteria();
            if (!module.IsCompatible(versionCriteria))
            {
                return false;
            }

            var stabilityTolerance = instance.StabilityToleranceConfig.ModStabilityTolerance(module.identifier)
                                     ?? instance.StabilityToleranceConfig.OverallStabilityTolerance;
            if ((module.release_status ?? ReleaseStatus.stable) > stabilityTolerance)
            {
                return false;
            }

            try
            {
                return registry.IdentifierCompatible(module.identifier,
                                                     instance.StabilityToleranceConfig,
                                                     versionCriteria);
            }
            catch
            {
                return false;
            }
        }

        private void PublishSelectedModRelationshipState()
        {
            this.RaisePropertyChanged(nameof(ShowSelectedModVersionPicker));
            this.RaisePropertyChanged(nameof(SelectedModVersionPickerLabel));
            this.RaisePropertyChanged(nameof(SelectedModSelectedVersionMatchesInstalled));
            this.RaisePropertyChanged(nameof(SelectedModSelectedVersionIsCompatible));
            this.RaisePropertyChanged(nameof(HasSelectedModDependencies));
            this.RaisePropertyChanged(nameof(HasSelectedModRecommendations));
            this.RaisePropertyChanged(nameof(HasSelectedModSuggestions));
            this.RaisePropertyChanged(nameof(ShowSelectedModResourceLinks));
            this.RaisePropertyChanged(nameof(ShowSelectedModDependenciesExpanded));
            this.RaisePropertyChanged(nameof(ShowSelectedModRecommendationsExpanded));
            this.RaisePropertyChanged(nameof(ShowSelectedModSuggestionsExpanded));
            this.RaisePropertyChanged(nameof(SelectedModDependencyChevron));
            this.RaisePropertyChanged(nameof(SelectedModRecommendationChevron));
            this.RaisePropertyChanged(nameof(SelectedModSuggestionChevron));
        }

        private void PublishRelationshipBrowserScopeState()
        {
            this.RaisePropertyChanged(nameof(ShowRelationshipBrowserScope));
            this.RaisePropertyChanged(nameof(RelationshipBrowserScopeBackground));
            this.RaisePropertyChanged(nameof(RelationshipBrowserScopeBorderBrush));
            this.RaisePropertyChanged(nameof(RelationshipBrowserScopeFrameBorderBrush));
            this.RaisePropertyChanged(nameof(RelationshipBrowserScopeText));
            this.RaisePropertyChanged(nameof(ModCountLabel));
        }

        private void ToggleSelectedModDependenciesExpanded()
        {
            if (HasSelectedModDependencies)
            {
                ShowSelectedModDependenciesExpanded = !ShowSelectedModDependenciesExpanded;
                this.RaisePropertyChanged(nameof(SelectedModDependencyChevron));
            }
        }

        private void ToggleSelectedModRecommendationsExpanded()
        {
            if (HasSelectedModRecommendations)
            {
                ShowSelectedModRecommendationsExpanded = !ShowSelectedModRecommendationsExpanded;
                this.RaisePropertyChanged(nameof(SelectedModRecommendationChevron));
            }
        }

        private void ToggleSelectedModSuggestionsExpanded()
        {
            if (HasSelectedModSuggestions)
            {
                ShowSelectedModSuggestionsExpanded = !ShowSelectedModSuggestionsExpanded;
                this.RaisePropertyChanged(nameof(SelectedModSuggestionChevron));
            }
        }

        private static string BuildVersionCompatibilityLabel(CkanModule module,
                                                             GameInstance instance)
        {
            var latest = module.LatestCompatibleGameVersion();
            if (latest.IsAny)
            {
                return "Any";
            }
            return FormatDisplayedCompatibilityVersion(latest);
        }

        private static string BuildSelectedModVersions(ModDetailsModel details)
            => details.IsAutodetected
                ? $"Latest {details.LatestVersion}\nInstalled version unknown"
                : details.IsInstalled
                    ? $"Latest {details.LatestVersion}\nInstalled {details.InstalledVersion}"
                    : $"Latest {details.LatestVersion}";

        private static string FormatDisplayedCompatibilityVersion(GameVersion? version)
        {
            if (version == null || version.IsAny)
            {
                return "Unknown";
            }

            var normalized = version.WithoutBuild;
            if (normalized.IsPatchDefined && normalized.Patch == 99)
            {
                normalized = new GameVersion(normalized.Major, normalized.Minor);
            }

            return normalized.ToString() ?? "Unknown";
        }

        private static string FormatModuleKind(ModuleKind kind)
            => kind switch
            {
                ModuleKind.metapackage => "Metapackage",
                ModuleKind.dlc => "DLC",
                _ => "Package",
            };

        private static string FormatLicense(CkanModule module)
            => module.license?.Count > 0 == true
                ? string.Join(", ", module.license)
                : "Unknown";

        private static string BuildInstallState(ModDetailsModel details)
        {
            var parts = new List<string>();

            if (details.IsAutodetected)
            {
                parts.Add("Managed outside CKAN");
            }
            else if (details.IsInstalled)
            {
                parts.Add($"Installed {details.InstalledVersion}");
            }
            else if (!details.IsIncompatible)
            {
                parts.Add("Not installed");
            }

            if (details.HasVersionUpdate)
            {
                parts.Add($"Update available to {details.LatestVersion}");
            }
            if (details.IsIncompatible && !details.IsAutodetected)
            {
                parts.Add("Currently incompatible");
            }
            if (details.HasReplacement)
            {
                parts.Add("Replacement available");
            }

            return string.Join(" • ", parts);
        }

    }
}
