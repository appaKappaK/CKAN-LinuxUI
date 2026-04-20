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
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return (IReadOnlyList<ModListItem>)items;
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
                if (displayMod == null)
                {
                    return null;
                }

                return new ModDetailsModel
                {
                    Identifier       = identifier,
                    Title            = displayMod.name ?? identifier,
                    Summary          = displayMod.@abstract ?? "",
                    Description      = string.IsNullOrWhiteSpace(displayMod.description)
                        ? displayMod.@abstract ?? ""
                        : displayMod.description,
                    Authors          = string.Join(", ", displayMod.author ?? new List<string>()),
                    LatestVersion    = latestCompatible?.version.ToString()
                                       ?? latestAvailable?.version.ToString()
                                       ?? installed?.version.ToString()
                                       ?? "-",
                    InstalledVersion = installed?.version.ToString() ?? "Not installed",
                    Compatibility    = FormatCompatibility(displayMod, inst),
                    ModuleKind       = FormatModuleKind(displayMod.kind),
                    License          = FormatLicense(displayMod),
                    ReleaseDate      = displayMod.release_date?.ToString("yyyy-MM-dd") ?? "Unknown",
                    DownloadSize     = displayMod.download_size > 0
                        ? CkanModule.FmtSize(displayMod.download_size)
                        : "Unknown",
                    DependencyCount     = displayMod.depends?.Count ?? 0,
                    RecommendationCount = displayMod.recommends?.Count ?? 0,
                    SuggestionCount     = displayMod.suggests?.Count ?? 0,
                    IsInstalled      = installed != null || registry.IsAutodetected(identifier),
                    HasUpdate        = HasUpdate(registry, inst, identifier),
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
            => new ModListItem
            {
                Identifier       = displayMod.identifier,
                Name             = displayMod.name?.Trim() ?? displayMod.identifier,
                Author           = string.Join(", ", displayMod.author ?? new List<string>()),
                Summary          = displayMod.@abstract?.Trim() ?? "",
                LatestVersion    = displayMod.version.ToString(),
                InstalledVersion = installedModule?.version.ToString() ?? "",
                IsInstalled      = installedModule != null || context.Registry.IsAutodetected(displayMod.identifier),
                HasUpdate        = hasUpdate,
                IsIncompatible   = incompatibleOverride,
                IsCached         = IsCached(context, displayMod),
                HasReplacement   = context.Registry.GetReplacement(displayMod.identifier,
                                                                   context.Instance.StabilityToleranceConfig,
                                                                   context.Instance.VersionCriteria()) != null,
                Compatibility    = FormatCompatibility(displayMod, context.Instance),
            };

        private static bool Matches(ModListItem item, FilterState filter)
        {
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

            if (filter.InstalledOnly && !item.IsInstalled)
            {
                return false;
            }
            if (filter.NotInstalledOnly && item.IsInstalled)
            {
                return false;
            }
            if (filter.UpdatableOnly && !item.HasUpdate)
            {
                return false;
            }
            if (filter.CachedOnly && !item.IsCached)
            {
                return false;
            }
            if (filter.IncompatibleOnly && !item.IsIncompatible)
            {
                return false;
            }
            if (filter.NewOnly && item.IsInstalled)
            {
                return false;
            }

            return true;
        }

        private static bool Contains(string text, string search)
            => text?.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;

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
