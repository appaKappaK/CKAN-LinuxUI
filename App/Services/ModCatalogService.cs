using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public sealed class ModCatalogService : IModCatalogService
    {
        private readonly IGameInstanceService gameInstanceService;

        public ModCatalogService(IGameInstanceService gameInstanceService)
        {
            this.gameInstanceService = gameInstanceService;
        }

        public Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CurrentContext() is not CatalogContext context)
                {
                    return (IReadOnlyList<ModListItem>)Array.Empty<ModListItem>();
                }

                PrimeRepositoryCache(context);
                var items = BuildItems(context)
                    .Where(item => Matches(item, filter))
                    .ToList();
                items = SortItems(items,
                                  filter.SortOption,
                                  filter.SortDescending ?? DefaultSortDescending(filter.SortOption))
                    .ToList();
                return (IReadOnlyList<ModListItem>)items;
            }, cancellationToken);

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

                return new FilterOptionCounts
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
            }, cancellationToken);

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
                    IsIncompatible   = latestCompatible == null
                                       && (installed == null || !installed.IsCompatible(inst.VersionCriteria())),
                    HasReplacement   = registry.GetReplacement(identifier,
                                                               inst.StabilityToleranceConfig,
                                                               inst.VersionCriteria()) != null,
                };
            }, cancellationToken);

        private CatalogContext? CurrentContext()
        {
            var instance = gameInstanceService.CurrentInstance;
            var regMgr = gameInstanceService.CurrentRegistryManager;
            return instance != null && regMgr != null
                ? new CatalogContext(instance, regMgr.registry)
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
                                          installedModule: instMod.Module,
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
                                         CkanModule?     installedModule,
                                         bool            hasUpdate,
                                         bool            incompatibleOverride)
        {
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
                                    && installedModule != null
                                    && displayMod.version.CompareTo(installedModule.version) > 0;
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
                LatestVersion     = displayMod.version.ToString(),
                InstalledVersion  = installedModule?.version.ToString() ?? "",
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
                                                            isCached,
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

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var text = filter.SearchText.Trim();
                if (!Contains(item.Name, text)
                    && !Contains(item.Identifier, text)
                    && !Contains(item.Author, text)
                    && !Contains(item.Summary, text))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.AuthorText)
                && !Contains(item.Author, filter.AuthorText.Trim()))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(filter.CompatibilityText)
                && !Contains(item.Compatibility, filter.CompatibilityText.Trim()))
            {
                return false;
            }

            if (filter.InstalledOnly && !item.IsInstalled)
            {
                return false;
            }
            if (filter.NotInstalledOnly && item.IsInstalled)
            {
                return false;
            }
            if (filter.UpdatableOnly && !item.HasVersionUpdate)
            {
                return false;
            }
            if (filter.CompatibleOnly && item.IsIncompatible)
            {
                return false;
            }
            if (filter.CachedOnly && !item.IsCached)
            {
                return false;
            }
            if (filter.CachedOnly && availabilityScopedToNotInstalled && item.IsInstalled)
            {
                return false;
            }
            if (filter.UncachedOnly && item.IsCached)
            {
                return false;
            }
            if (filter.UncachedOnly && availabilityScopedToNotInstalled && item.IsInstalled)
            {
                return false;
            }
            if (filter.IncompatibleOnly && !item.IsIncompatible)
            {
                return false;
            }
            if (filter.HasReplacementOnly && !item.HasReplacement)
            {
                return false;
            }
            if (filter.NewOnly && item.IsInstalled)
            {
                return false;
            }

            return true;
        }

        private static int CountForPreview(IReadOnlyCollection<ModListItem> items,
                                           FilterState                     filter,
                                           Func<FilterState, FilterState>   applyPreviewFilter)
            => items.Count(item => Matches(item, applyPreviewFilter(filter)));

        private static FilterState WithInstalledOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = true,
                NotInstalledOnly    = false,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithNotInstalledOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = false,
                NotInstalledOnly    = true,
                UpdatableOnly       = false,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithUpdatableOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = false,
                UpdatableOnly       = true,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithCompatibleOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = filter.NotInstalledOnly,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = true,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = false,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithCachedOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = filter.NotInstalledOnly,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = true,
                UncachedOnly        = false,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithUncachedOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = filter.NotInstalledOnly,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = false,
                UncachedOnly        = true,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithIncompatibleOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = filter.NotInstalledOnly,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = false,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = true,
                HasReplacementOnly  = filter.HasReplacementOnly,
            };

        private static FilterState WithReplacementOnly(FilterState filter)
            => new FilterState
            {
                SearchText          = filter.SearchText,
                AuthorText          = filter.AuthorText,
                CompatibilityText   = filter.CompatibilityText,
                SortOption          = filter.SortOption,
                SortDescending      = filter.SortDescending,
                InstalledOnly       = filter.InstalledOnly,
                NotInstalledOnly    = filter.NotInstalledOnly,
                UpdatableOnly       = filter.UpdatableOnly,
                NewOnly             = filter.NewOnly,
                CompatibleOnly      = filter.CompatibleOnly,
                CachedOnly          = filter.CachedOnly,
                UncachedOnly        = filter.UncachedOnly,
                IncompatibleOnly    = filter.IncompatibleOnly,
                HasReplacementOnly  = true,
            };

        private static bool Contains(string text, string search)
            => text?.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;

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
                                                      bool isCached,
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
                latest = module.LatestCompatibleRealGameVersion(instance.Game.KnownVersions);
            }
            return latest?.ToString() ?? "Unknown";
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
