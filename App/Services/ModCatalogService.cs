using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.Versioning;

namespace CKAN.App.Services
{
    public sealed class ModCatalogService : IModCatalogService
    {
        private readonly IGameInstanceService gameInstanceService;

        public ModCatalogService(IGameInstanceService gameInstanceService)
        {
            this.gameInstanceService = gameInstanceService;
        }

        public Task<IReadOnlyList<ModListItem>> GetAllModListAsync(CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CurrentContext() is not CatalogContext context)
                {
                    return (IReadOnlyList<ModListItem>)Array.Empty<ModListItem>();
                }

                PrimeRepositoryCache(context);
                return (IReadOnlyList<ModListItem>)BuildItems(context).ToList();
            }, cancellationToken);

        public async Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                      CancellationToken cancellationToken)
            => ApplyFilter(await GetAllModListAsync(cancellationToken), filter);

        public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                   CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CurrentContext() is not CatalogContext context)
                {
                    return new FilterOptionCounts();
                }

                PrimeRepositoryCache(context);
                var items = BuildItems(context).ToList();

                return GetFilterOptionCounts(items, filter);
            }, cancellationToken);

        public IReadOnlyList<ModListItem> ApplyFilter(IReadOnlyList<ModListItem> items,
                                                      FilterState                 filter)
            => SortItems(items.Where(item => Matches(item, filter)),
                         filter.SortOption,
                         filter.SortDescending ?? DefaultSortDescending(filter.SortOption))
                .ToList();

        public FilterOptionCounts GetFilterOptionCounts(IReadOnlyCollection<ModListItem> items,
                                                        FilterState                       filter)
            => new FilterOptionCounts
            {
                Compatible   = CountForPreview(items, filter, WithCompatibleOnly),
                Installed    = CountForPreview(items, filter, WithInstalledOnly),
                Updatable    = CountForPreview(items, filter, WithUpdatableOnly),
                Replaceable  = CountForPreview(items, filter, WithReplacementOnly),
                Cached       = CountForPreview(items, filter, WithCachedOnly),
                Uncached     = CountForPreview(items, filter, WithUncachedOnly),
                NotInstalled = CountForPreview(items, filter, WithNotInstalledOnly),
                Incompatible = CountForPreview(items, filter, WithIncompatibleOnly),
            };

        public Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                         CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CurrentContext() is not CatalogContext context)
                {
                    return null;
                }

                PrimeRepositoryCache(context);
                var registry = context.Registry;
                var inst     = context.Instance;
                var installedModule = registry.InstalledModule(identifier);
                var installed       = installedModule?.Module;
                var latestCompatible = TryLatestAvailable(registry, identifier, inst, compatibleOnly: true);
                var latestAvailable  = TryLatestAvailable(registry, identifier, inst, compatibleOnly: false);
                var displayMod       = latestCompatible ?? latestAvailable ?? installed;
                var downloadCount    = gameInstanceService.RepositoryData.GetDownloadCount(
                    registry.Repositories.Values,
                    identifier);
                if (displayMod == null)
                {
                    return null;
                }

                bool isAutodetected = registry.IsAutodetected(identifier);
                bool hasUpdate = HasUpdate(registry, inst, identifier);
                bool hasVersionUpdate = hasUpdate
                                        && installed != null
                                        && (latestCompatible ?? latestAvailable) != null
                                        && (latestCompatible ?? latestAvailable)!.version.CompareTo(installed.version) > 0;

                return new ModDetailsModel
                {
                    Identifier       = identifier,
                    Title            = displayMod.name ?? identifier,
                    Summary          = displayMod.@abstract ?? "",
                    Description      = string.IsNullOrWhiteSpace(displayMod.description)
                        ? displayMod.@abstract ?? ""
                        : displayMod.description ?? "",
                    Authors          = string.Join(", ", displayMod.author ?? new List<string>()),
                    LatestVersion    = latestCompatible?.version.ToString()
                                       ?? latestAvailable?.version.ToString()
                                       ?? installed?.version.ToString()
                                       ?? "-",
                    InstalledVersion = installed?.version.ToString()
                                       ?? (isAutodetected ? "Autodetected" : "Not installed"),
                    Compatibility    = FormatCompatibility(displayMod, inst),
                    ModuleKind       = FormatModuleKind(displayMod.kind),
                    License          = FormatLicense(displayMod),
                    Resources        = displayMod.resources,
                    ReleaseDate      = displayMod.release_date?.ToString("yyyy-MM-dd") ?? "Unknown",
                    DownloadSize     = displayMod.download_size > 0
                        ? CkanModule.FmtSize(displayMod.download_size)
                        : "Unknown",
                    DownloadCount    = downloadCount,
                    DependencyCount     = displayMod.depends?.Count ?? 0,
                    RecommendationCount = displayMod.recommends?.Count ?? 0,
                    SuggestionCount     = displayMod.suggests?.Count ?? 0,
                    IsInstalled      = installed != null || isAutodetected,
                    IsAutodetected   = isAutodetected,
                    HasUpdate        = hasUpdate,
                    HasVersionUpdate = hasVersionUpdate,
                    IsCached         = IsCached(context, displayMod),
                    IsIncompatible   = !IdentifierCompatible(registry, identifier, inst)
                                       && (installed == null || !installed.IsCompatible(inst.VersionCriteria())),
                    HasReplacement   = registry.GetReplacement(identifier,
                                                               inst.StabilityToleranceConfig,
                                                               inst.VersionCriteria()) != null,
                };
            }, cancellationToken);

        private CatalogContext? CurrentContext()
        {
            var instance = gameInstanceService.CurrentInstance;
            var registry = gameInstanceService.CurrentRegistry;
            return instance != null && registry != null
                ? new CatalogContext(instance, registry)
                : null;
        }

        private void PrimeRepositoryCache(CatalogContext context)
            => gameInstanceService.RepositoryData.Prepopulate(context.Registry.Repositories.Values.ToList(), null);

        private IEnumerable<ModListItem> BuildItems(CatalogContext context)
        {
            var registry = context.Registry;
            var inst     = context.Instance;
            var installedIdents = registry.InstalledModules.Select(im => im.identifier)
                                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var instMod in registry.InstalledModules
                                            .Where(im => !im.Module.IsDLC)
                                            .OrderBy(im => im.identifier, StringComparer.OrdinalIgnoreCase))
            {
                var identifier = instMod.identifier;
                var latestCompatible = TryLatestAvailable(registry, identifier, inst, compatibleOnly: true);
                var latestAvailable  = TryLatestAvailable(registry, identifier, inst, compatibleOnly: false);
                var displayMod       = latestCompatible ?? latestAvailable ?? instMod.Module;

                yield return MakeListItem(context,
                                          displayMod,
                                          installedModule: instMod,
                                          hasUpdate: HasUpdate(registry, inst, identifier),
                                          incompatibleOverride: latestCompatible == null
                                                                && !instMod.Module.IsCompatible(inst.VersionCriteria()));
            }

            foreach (var mod in registry.CompatibleModules(inst.StabilityToleranceConfig, inst.VersionCriteria())
                                        .Where(m => !installedIdents.Contains(m.identifier))
                                        .Where(m => !m.IsDLC))
            {
                yield return MakeListItem(context,
                                          mod,
                                          installedModule: null,
                                          hasUpdate: false,
                                          incompatibleOverride: false);
            }

            foreach (var mod in registry.IncompatibleModules(inst.StabilityToleranceConfig, inst.VersionCriteria())
                                        .Where(m => !installedIdents.Contains(m.identifier))
                                        .Where(m => !m.IsDLC))
            {
                yield return MakeListItem(context,
                                          mod,
                                          installedModule: null,
                                          hasUpdate: false,
                                          incompatibleOverride: true);
            }
        }

        private ModListItem MakeListItem(CatalogContext context,
                                         CkanModule      displayMod,
                                         InstalledModule? installedModule,
                                         bool            hasUpdate,
                                         bool            incompatibleOverride)
        {
            var installedCkanModule = installedModule?.Module;
            int? downloadCount = gameInstanceService.RepositoryData.GetDownloadCount(
                context.Registry.Repositories.Values,
                displayMod.identifier);
            bool isAutodetected = context.Registry.IsAutodetected(displayMod.identifier);
            bool isInstalled = installedModule != null || isAutodetected;
            bool isCached = IsCached(context, displayMod);
            bool hasReplacement = context.Registry.GetReplacement(displayMod.identifier,
                                                                  context.Instance.StabilityToleranceConfig,
                                                                  context.Instance.VersionCriteria()) != null;
            bool hasVersionUpdate = hasUpdate
                                    && installedCkanModule != null
                                    && displayMod.version.CompareTo(installedCkanModule.version) > 0;
            string primaryStateLabel = FormatPrimaryStateLabel(isInstalled,
                                                               isAutodetected,
                                                               hasVersionUpdate,
                                                               incompatibleOverride,
                                                               isCached,
                                                               hasReplacement);
            string secondaryStateLabel = FormatSecondaryStateLabel(isAutodetected);
            string tertiaryStateLabel = FormatTertiaryStateLabel(isAutodetected,
                                                                 incompatibleOverride);
            string statusSummary = FormatStatusSummary(isInstalled,
                                                       hasVersionUpdate,
                                                       incompatibleOverride,
                                                       isCached,
                                                       hasReplacement);

            return new ModListItem
            {
                Identifier        = displayMod.identifier,
                Name              = displayMod.name?.Trim() ?? displayMod.identifier,
                Author            = string.Join(", ", displayMod.author ?? new List<string>()),
                Summary           = displayMod.@abstract?.Trim() ?? "",
                Description       = string.IsNullOrWhiteSpace(displayMod.description)
                    ? displayMod.@abstract?.Trim() ?? ""
                    : displayMod.description!.Trim(),
                License           = FormatLicense(displayMod),
                Languages         = string.Join(", ", displayMod.localizations ?? Array.Empty<string>()),
                Depends           = FormatRelationshipList(displayMod.depends),
                Recommends        = FormatRelationshipList(displayMod.recommends),
                Suggests          = FormatRelationshipList(displayMod.suggests),
                Conflicts         = FormatRelationshipList(displayMod.conflicts),
                Supports          = FormatRelationshipList(displayMod.supports),
                Tags              = string.Join(", ", displayMod.Tags?.OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
                                                               ?? Enumerable.Empty<string>()),
                Labels            = string.Join(", ", ModuleLabelList.ModuleLabels.LabelsFor(context.Instance.Name)
                                                                          .Where(label => label.ContainsModule(context.Instance.Game,
                                                                                                               displayMod.identifier))
                                                                          .Select(label => label.Name)
                                                                          .OrderBy(name => name,
                                                                                   StringComparer.CurrentCultureIgnoreCase)),
                LatestVersion     = displayMod.version.ToString(),
                InstalledVersion  = installedCkanModule?.version.ToString() ?? "",
                ReleaseDate       = displayMod.release_date?.ToString("yyyy-MM-dd") ?? "Unknown",
                ReleaseDateValue  = displayMod.release_date?.Date,
                InstallDate       = installedModule?.InstallTime.ToString("yyyy-MM-dd")
                                    ?? (isAutodetected ? "External" : "-"),
                InstallDateValue  = installedModule?.InstallTime.Date,
                DownloadCount     = downloadCount,
                DownloadCountLabel = downloadCount?.ToString("N0") ?? "-",
                IsInstalled       = isInstalled,
                IsAutodetected    = isAutodetected,
                HasUpdate         = hasUpdate,
                HasVersionUpdate  = hasVersionUpdate,
                IsIncompatible    = incompatibleOverride,
                IsCached          = isCached,
                HasReplacement    = hasReplacement,
                Compatibility     = FormatCompatibility(displayMod, context.Instance),
                PrimaryStateLabel = primaryStateLabel,
                PrimaryStateColor = FormatPrimaryStateColor(isInstalled,
                                                            isAutodetected,
                                                            hasVersionUpdate,
                                                            incompatibleOverride,
                                                            hasReplacement),
                SecondaryStateLabel = secondaryStateLabel,
                SecondaryStateBackground = FormatSecondaryStateBackground(isAutodetected),
                SecondaryStateBorderBrush = FormatSecondaryStateBorderBrush(isAutodetected),
                TertiaryStateLabel = tertiaryStateLabel,
                TertiaryStateBackground = FormatTertiaryStateBackground(isAutodetected,
                                                                        incompatibleOverride),
                TertiaryStateBorderBrush = FormatTertiaryStateBorderBrush(isAutodetected,
                                                                          incompatibleOverride),
                StatusSummary     = statusSummary,
                HasStatusSummary  = !string.IsNullOrWhiteSpace(statusSummary),
            };
        }

        private static bool Matches(ModListItem item, FilterState filter)
        {
            bool availabilityScopedToNotInstalled = !filter.InstalledOnly && !filter.UpdatableOnly;

            return MatchesSearchText(item, filter.SearchText)
                   && MatchesTextFilter(item.Name, filter.NameText)
                   && MatchesTextFilter(item.Identifier, filter.IdentifierText)
                   && MatchesTextFilter(item.Author, filter.AuthorText)
                   && MatchesTextFilter(item.Summary, filter.SummaryText)
                   && MatchesTextFilter(item.Description, filter.DescriptionText)
                   && MatchesTextFilter(item.License, filter.LicenseText)
                   && MatchesTextFilter(item.Languages, filter.LanguageText)
                   && MatchesTextFilter(item.Depends, filter.DependsText)
                   && MatchesTextFilter(item.Recommends, filter.RecommendsText)
                   && MatchesTextFilter(item.Suggests, filter.SuggestsText)
                   && MatchesTextFilter(item.Conflicts, filter.ConflictsText)
                   && MatchesTextFilter(item.Supports, filter.SupportsText)
                   && MatchesListFilter(item.Tags, filter.TagText)
                   && MatchesTextFilter(item.Labels, filter.LabelText)
                   && MatchesTextFilter(item.Compatibility, filter.CompatibilityText)
                   && MatchesBooleanFilter(item.IsInstalled,
                                           filter.InstalledOnly,
                                           filter.NotInstalledOnly)
                   && MatchesBooleanFilter(item.HasVersionUpdate,
                                           filter.UpdatableOnly,
                                           filter.NotUpdatableOnly)
                   && MatchesBooleanFilter(!item.IsIncompatible,
                                           filter.CompatibleOnly,
                                           filter.IncompatibleOnly)
                   && MatchesBooleanFilter(item.HasReplacement,
                                           filter.HasReplacementOnly,
                                           filter.NoReplacementOnly)
                   && MatchesCacheFilter(item,
                                         availabilityScopedToNotInstalled,
                                         filter.CachedOnly,
                                         filter.UncachedOnly)
                   && (!filter.NewOnly || !item.IsInstalled);
        }

        private static int CountForPreview(IReadOnlyCollection<ModListItem> items,
                                           FilterState                     filter,
                                           Func<FilterState, FilterState>   applyPreviewFilter)
            => items.Count(item => Matches(item, applyPreviewFilter(filter)));

        private static FilterState WithInstalledOnly(FilterState filter)
            => filter with
            {
                InstalledOnly    = true,
                NotInstalledOnly = false,
            };

        private static FilterState WithNotInstalledOnly(FilterState filter)
            => filter with
            {
                InstalledOnly    = false,
                NotInstalledOnly = true,
                UpdatableOnly    = false,
            };

        private static FilterState WithUpdatableOnly(FilterState filter)
            => filter with
            {
                NotInstalledOnly   = false,
                UpdatableOnly      = true,
                NotUpdatableOnly   = false,
            };

        private static FilterState WithCompatibleOnly(FilterState filter)
            => filter with
            {
                CompatibleOnly   = true,
                IncompatibleOnly = false,
            };

        private static FilterState WithCachedOnly(FilterState filter)
            => filter with
            {
                CachedOnly   = true,
                UncachedOnly = false,
            };

        private static FilterState WithUncachedOnly(FilterState filter)
            => filter with
            {
                CachedOnly   = false,
                UncachedOnly = true,
            };

        private static FilterState WithIncompatibleOnly(FilterState filter)
            => filter with
            {
                CompatibleOnly   = false,
                IncompatibleOnly = true,
            };

        private static FilterState WithReplacementOnly(FilterState filter)
            => filter with
            {
                HasReplacementOnly = true,
                NoReplacementOnly  = false,
            };

        private static bool Contains(string text, string search)
            => text?.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private static bool MatchesTextFilter(string text, string search)
            => string.IsNullOrWhiteSpace(search)
               || Contains(text, search.Trim());

        private static bool MatchesListFilter(string text, string search)
        {
            var requiredValues = SplitFilterValues(search).ToList();
            if (requiredValues.Count == 0)
            {
                return true;
            }

            var itemValues = SplitListValues(text).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            return requiredValues.All(itemValues.Contains);
        }

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

        private static bool MatchesBooleanFilter(bool value,
                                                 bool mustBeTrue,
                                                 bool mustBeFalse)
            => (!mustBeTrue || value)
               && (!mustBeFalse || !value);

        private static bool MatchesCacheFilter(ModListItem item,
                                               bool        availabilityScopedToNotInstalled,
                                               bool        mustBeCached,
                                               bool        mustBeUncached)
        {
            if (!MatchesBooleanFilter(item.IsCached, mustBeCached, mustBeUncached))
            {
                return false;
            }

            if ((mustBeCached || mustBeUncached)
                && availabilityScopedToNotInstalled
                && item.IsInstalled)
            {
                return false;
            }

            return true;
        }

        private static string FormatRelationshipList(IEnumerable<RelationshipDescriptor>? relationships)
            => string.Join(", ",
                           (relationships ?? Enumerable.Empty<RelationshipDescriptor>())
                               .Select(relationship => relationship.ToString())
                               .Where(text => !string.IsNullOrWhiteSpace(text)));

        private static bool MatchesSearchText(ModListItem item, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var query = ParseSearchText(searchText);
            return query.FreeTerms.All(term => MatchesPlainSearchField(item, term))
                   && query.FieldTerms.All(term => MatchesFieldSearch(item, term.Field, term.Value));
        }

        private static bool MatchesPlainSearchField(ModListItem item, string search)
            => Contains(item.Name, search)
               || Contains(item.Identifier, search)
               || Contains(item.Author, search)
               || Contains(item.Summary, search)
               || Contains(item.Description, search);

        private static bool MatchesAnySearchField(ModListItem item, string search)
            => Contains(item.Name, search)
               || Contains(item.Identifier, search)
               || Contains(item.Author, search)
               || Contains(item.Summary, search)
               || Contains(item.Description, search)
               || Contains(item.License, search)
               || Contains(item.Languages, search)
               || Contains(item.Depends, search)
               || Contains(item.Recommends, search)
               || Contains(item.Suggests, search)
               || Contains(item.Conflicts, search)
               || Contains(item.Supports, search)
               || Contains(item.Tags, search)
               || Contains(item.Labels, search)
               || Contains(item.Compatibility, search)
               || Contains(item.LatestVersion, search)
               || Contains(item.ReleaseDate, search);

        private static bool MatchesFieldSearch(ModListItem item, string field, string value)
            => field switch
            {
                "name" or "mod"                 => Contains(item.Name, value),
                "id" or "ident" or "identifier" => Contains(item.Identifier, value),
                "author" or "authors"           => Contains(item.Author, value),
                "summary" or "abstract"         => Contains(item.Summary, value),
                "description" or "desc"         => Contains(item.Description, value),
                "license"                       => Contains(item.License, value),
                "language" or "lang"           => Contains(item.Languages, value),
                "depends" or "dependency"      => Contains(item.Depends, value),
                "recommends" or "recommend"    => Contains(item.Recommends, value),
                "suggests" or "suggest"        => Contains(item.Suggests, value),
                "conflicts" or "conflict"      => Contains(item.Conflicts, value),
                "supports" or "support"        => Contains(item.Supports, value),
                "tag" or "tags"                => Contains(item.Tags, value),
                "label" or "labels"            => Contains(item.Labels, value),
                "compat" or "compatibility" or "ksp"
                                                => Contains(item.Compatibility, value),
                "version"                       => Contains(item.LatestVersion, value),
                "release" or "released" or "date"
                                                => Contains(item.ReleaseDate, value),
                "is"                            => MatchesIsSearch(item, value),
                _                               => MatchesAnySearchField(item, $"{field}:{value}"),
            };

        private static bool MatchesIsSearch(ModListItem item, string value)
            => value.Trim().ToLowerInvariant() switch
            {
                "installed"                  => item.IsInstalled,
                "not-installed" or "notinstalled" or "uninstalled"
                                              => !item.IsInstalled,
                "updatable" or "update" or "update-available"
                                              => item.HasVersionUpdate,
                "not-updatable" or "noupdate" or "notupdate"
                                              => !item.HasVersionUpdate,
                "compatible"                 => !item.IsIncompatible,
                "incompatible"               => item.IsIncompatible,
                "cached"                     => item.IsCached,
                "uncached" or "not-cached" or "notcached"
                                              => !item.IsCached,
                "replaceable" or "replacement" or "has-replacement"
                                              => item.HasReplacement,
                "not-replaceable" or "noreplacement" or "no-replacement"
                                              => !item.HasReplacement,
                "external" or "autodetected" => item.IsAutodetected,
                _                            => MatchesAnySearchField(item, value),
            };

        private static ParsedSearchQuery ParseSearchText(string searchText)
        {
            var freeTerms = new List<string>();
            var fieldTerms = new List<SearchFieldTerm>();

            foreach (var token in TokenizeSearchText(searchText))
            {
                var separatorIndex = token.IndexOf(':');
                if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
                {
                    freeTerms.Add(token);
                    continue;
                }

                var field = token[..separatorIndex].Trim().ToLowerInvariant();
                var value = token[(separatorIndex + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    freeTerms.Add(token);
                    continue;
                }

                if (IsRecognizedSearchField(field))
                {
                    fieldTerms.Add(new SearchFieldTerm(field, value));
                }
                else
                {
                    freeTerms.Add(token);
                }
            }

            return new ParsedSearchQuery(freeTerms, fieldTerms);
        }

        private static IEnumerable<string> TokenizeSearchText(string text)
        {
            var builder = new StringBuilder();
            bool inQuotes = false;

            foreach (char ch in text)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (builder.Length > 0)
                    {
                        yield return builder.ToString();
                        builder.Clear();
                    }
                    continue;
                }

                builder.Append(ch);
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static bool IsRecognizedSearchField(string field)
            => field is "name"
               or "mod"
               or "id"
               or "ident"
               or "identifier"
               or "author"
               or "authors"
               or "summary"
               or "abstract"
               or "description"
               or "desc"
               or "license"
               or "language"
               or "lang"
               or "depends"
               or "dependency"
               or "recommends"
               or "recommend"
               or "suggests"
               or "suggest"
               or "conflicts"
               or "conflict"
               or "supports"
               or "support"
               or "tag"
               or "tags"
               or "label"
               or "labels"
               or "compat"
               or "compatibility"
               or "ksp"
               or "version"
               or "release"
               or "released"
               or "date"
               or "is";

        private sealed record SearchFieldTerm(string Field,
                                              string Value);

        private sealed record ParsedSearchQuery(IReadOnlyList<string>        FreeTerms,
                                                IReadOnlyList<SearchFieldTerm> FieldTerms);

        private static string FormatPrimaryStateLabel(bool isInstalled,
                                                      bool isAutodetected,
                                                      bool hasUpdate,
                                                      bool isIncompatible,
                                                      bool isCached,
                                                      bool hasReplacement)
            => isIncompatible && !isAutodetected
                ? "Incompatible"
                : hasUpdate
                    ? "Update Available"
                    : isInstalled
                        ? "Installed"
                        : hasReplacement
                            ? "Has Replacement"
                            : "Available";

        private static string FormatPrimaryStateColor(bool isInstalled,
                                                      bool isAutodetected,
                                                      bool hasUpdate,
                                                      bool isIncompatible,
                                                      bool hasReplacement)
            => isIncompatible && !isAutodetected
                ? "#9A485C"
                : hasUpdate
                    ? "#6A952B"
                    : isInstalled
                        ? "#2B6A98"
                        : hasReplacement
                            ? "#734790"
                            : "#2F7C58";

        private static string FormatSecondaryStateLabel(bool isAutodetected)
            => isAutodetected ? "External" : "";

        private static string FormatSecondaryStateBackground(bool isAutodetected)
            => isAutodetected ? "#5A4322" : "#39424E";

        private static string FormatSecondaryStateBorderBrush(bool isAutodetected)
            => isAutodetected ? "#9F7A40" : "#607286";

        private static string FormatTertiaryStateLabel(bool isAutodetected,
                                                       bool isIncompatible)
            => isAutodetected && isIncompatible ? "Dependency" : "";

        private static string FormatTertiaryStateBackground(bool isAutodetected,
                                                            bool isIncompatible)
            => isAutodetected && isIncompatible ? "#31424F" : "#31424F";

        private static string FormatTertiaryStateBorderBrush(bool isAutodetected,
                                                             bool isIncompatible)
            => isAutodetected && isIncompatible ? "#4C6A86" : "#4C6A86";

        private static string FormatStatusSummary(bool isInstalled,
                                                  bool hasUpdate,
                                                  bool isIncompatible,
                                                  bool isCached,
                                                  bool hasReplacement)
        {
            var parts = new List<string>();

            if (hasUpdate)
            {
                parts.Add("Installed");
            }
            if (hasReplacement)
            {
                parts.Add("Has replacement");
            }

            return string.Join(" • ", parts);
        }

        private static IEnumerable<ModListItem> SortItems(IEnumerable<ModListItem> items,
                                                          ModSortOption         sortOption,
                                                          bool                  descending)
            => sortOption switch
            {
                ModSortOption.Author
                    => descending
                        ? items.OrderByDescending(item => item.Author, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Author, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Popularity
                    => descending
                        ? items.OrderByDescending(item => item.DownloadCount ?? 0)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.DownloadCount ?? 0)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Compatibility
                    => descending
                        ? items.OrderByDescending(item => item.Compatibility, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Compatibility, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.ReleaseDate
                    => descending
                        ? items.OrderByDescending(item => item.ReleaseDateValue.HasValue)
                               .ThenByDescending(item => item.ReleaseDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(item => item.ReleaseDateValue.HasValue)
                               .ThenBy(item => item.ReleaseDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.InstallDate
                    => descending
                        ? items.OrderByDescending(item => item.InstallDateValue.HasValue)
                               .ThenByDescending(item => item.InstallDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(item => item.InstallDateValue.HasValue)
                               .ThenBy(item => item.InstallDateValue)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.Version
                    => descending
                        ? items.OrderByDescending(item => item.LatestVersion, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.LatestVersion, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.InstalledFirst
                    => descending
                        ? items.OrderByDescending(item => item.IsInstalled)
                               .ThenByDescending(item => item.HasVersionUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.IsInstalled)
                               .ThenByDescending(item => item.HasVersionUpdate)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                ModSortOption.UpdatesFirst
                    => descending
                        ? items.OrderByDescending(item => item.HasVersionUpdate)
                               .ThenByDescending(item => item.IsInstalled)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.HasVersionUpdate)
                               .ThenByDescending(item => item.IsInstalled)
                               .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
                _
                    => descending
                        ? items.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                        : items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase),
            };

        private static bool DefaultSortDescending(ModSortOption sortOption)
            => sortOption == ModSortOption.Popularity
               || sortOption == ModSortOption.ReleaseDate
               || sortOption == ModSortOption.InstallDate
               || sortOption == ModSortOption.InstalledFirst
               || sortOption == ModSortOption.UpdatesFirst;

        private bool IsCached(CatalogContext context, CkanModule module)
            => gameInstanceService.Manager.Cache != null
               && module.download is { Count: > 0 }
               && gameInstanceService.Manager.Cache.IsMaybeCachedZip(module);

        private static string FormatCompatibility(CkanModule module, GameInstance instance)
        {
            var latest = module.LatestCompatibleGameVersion();
            if (latest.IsAny)
            {
                return "Any";
            }
            return FormatDisplayedCompatibilityVersion(latest);
        }

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
                ModuleKind.package     => "Package",
                ModuleKind.metapackage => "Meta package",
                ModuleKind.dlc         => "DLC",
                _                      => kind.ToString(),
            };

        private static string FormatLicense(CkanModule module)
            => module.license?.Count > 0
                ? string.Join(", ", module.license)
                : "Unspecified";

        private static CkanModule? TryLatestAvailable(IRegistryQuerier registry,
                                                      string          identifier,
                                                      GameInstance    instance,
                                                      bool            compatibleOnly)
        {
            try
            {
                return compatibleOnly
                    ? registry.LatestAvailable(identifier,
                                               instance.StabilityToleranceConfig,
                                               instance.VersionCriteria())
                    : registry.LatestAvailable(identifier,
                                               instance.StabilityToleranceConfig,
                                               null);
            }
            catch
            {
                return null;
            }
        }

        private static bool IdentifierCompatible(IRegistryQuerier registry,
                                                 string           identifier,
                                                 GameInstance     instance)
        {
            try
            {
                return registry.IdentifierCompatible(identifier,
                                                     instance.StabilityToleranceConfig,
                                                     instance.VersionCriteria());
            }
            catch
            {
                return false;
            }
        }

        private bool HasUpdate(IRegistryQuerier registry, GameInstance instance, string identifier)
        {
            var filters = gameInstanceService.Configuration.GetGlobalInstallFilters(instance.Game)
                                                           .Concat(instance.InstallFilters)
                                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return registry.HasUpdate(identifier,
                                      instance.StabilityToleranceConfig,
                                      instance,
                                      filters,
                                      true,
                                      out _);
        }

        private sealed record CatalogContext(GameInstance Instance,
                                             Registry     Registry);
    }
}
