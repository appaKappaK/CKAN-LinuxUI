using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class FakeModCatalogService : IModCatalogService
    {
        private static readonly IReadOnlyList<ModListItem> allMods = new[]
        {
            Decorate(new ModListItem
            {
                Identifier       = "restock",
                Name             = "Restock",
                Author           = "Nertea",
                Summary          = "Refreshes stock parts with a consistent art pass.",
                Description      = "Restock replaces stock part art with a modernized, cohesive visual pass while keeping gameplay behavior familiar.",
                LatestVersion    = "1.5.2",
                InstalledVersion = "1.5.1",
                DownloadCount    = 452318,
                DownloadCountLabel = "452,318",
                IsInstalled      = true,
                HasUpdate        = true,
                HasVersionUpdate = true,
                IsCached         = true,
                IsIncompatible   = false,
                HasReplacement   = false,
                Compatibility    = "KSP 1.12.5",
            }),
            Decorate(new ModListItem
            {
                Identifier       = "parallax",
                Name             = "Parallax",
                Author           = "Gameslinx",
                Summary          = "Procedural surface scattering and planet terrain shading.",
                Description      = "Parallax adds procedural terrain detail, tessellation, and dense planetary scatter systems for surface exploration.",
                LatestVersion    = "2.1.0",
                InstalledVersion = "",
                DownloadCount    = 1863579,
                DownloadCountLabel = "1,863,579",
                IsInstalled      = false,
                HasUpdate        = false,
                IsCached         = true,
                IsIncompatible   = false,
                HasReplacement   = false,
                Compatibility    = "KSP 1.12.5",
            }),
            Decorate(new ModListItem
            {
                Identifier       = "mechjeb2",
                Name             = "MechJeb 2",
                Author           = "Sarbian",
                Summary          = "Flight automation, maneuver planning, and vessel information.",
                Description      = "MechJeb adds ascent guidance, rendezvous tools, information readouts, and maneuver planning for complex missions.",
                LatestVersion    = "2.14.3",
                InstalledVersion = "2.14.3",
                DownloadCount    = 893441,
                DownloadCountLabel = "893,441",
                IsInstalled      = true,
                HasUpdate        = false,
                IsCached         = false,
                IsIncompatible   = false,
                HasReplacement   = false,
                Compatibility    = "KSP 1.12.5",
            }),
            Decorate(new ModListItem
            {
                Identifier       = "kerbalism",
                Name             = "Kerbalism",
                Author           = "ShotgunNinja",
                Summary          = "Life support, reliability, science, and long-duration mission systems.",
                Description      = "Kerbalism reshapes campaign progression around life support, radiation, reliability, and expanded science mechanics.",
                LatestVersion    = "3.19.1",
                InstalledVersion = "",
                DownloadCount    = 217604,
                DownloadCountLabel = "217,604",
                IsInstalled      = false,
                HasUpdate        = false,
                IsCached         = false,
                IsIncompatible   = true,
                HasReplacement   = false,
                Compatibility    = "Older KSP release",
            }),
            Decorate(new ModListItem
            {
                Identifier       = "planetshine",
                Name             = "PlanetShine",
                Author           = "Valerian",
                Summary          = "Adds bounced planetary light to vessels and IVA scenes.",
                Description      = "PlanetShine adds subtle reflected light from planets and moons, improving atmosphere without changing gameplay.",
                LatestVersion    = "0.2.7.5",
                InstalledVersion = "",
                DownloadCount    = 625183,
                DownloadCountLabel = "625,183",
                IsInstalled      = false,
                HasUpdate        = false,
                IsCached         = true,
                IsIncompatible   = false,
                HasReplacement   = false,
                Compatibility    = "KSP 1.12.5",
            }),
        };

        private static readonly IReadOnlyDictionary<string, ModDetailsModel> details
            = new Dictionary<string, ModDetailsModel>(StringComparer.OrdinalIgnoreCase)
            {
                ["restock"] = new ModDetailsModel
                {
                    Identifier       = "restock",
                    Title            = "Restock",
                    Summary          = "Refreshes stock parts with a consistent art pass.",
                    Description      = "Restock replaces stock part art with a modernized, cohesive visual pass while keeping gameplay behavior familiar.",
                    Authors          = "Nertea",
                    LatestVersion    = "1.5.2",
                    InstalledVersion = "1.5.1",
                    Compatibility    = "KSP 1.12.5",
                    ModuleKind       = "Package",
                    License          = "CC-BY-NC-SA-4.0",
                    ReleaseDate      = "2025-01-14",
                    DownloadSize     = "128 MiB",
                    DownloadCount    = 452318,
                    DependencyCount     = 2,
                    RecommendationCount = 1,
                    SuggestionCount     = 0,
                    IsInstalled      = true,
                    HasUpdate        = true,
                    HasVersionUpdate = true,
                    IsCached         = true,
                    IsIncompatible   = false,
                    HasReplacement   = false,
                },
                ["parallax"] = new ModDetailsModel
                {
                    Identifier       = "parallax",
                    Title            = "Parallax",
                    Summary          = "Procedural surface scattering and planet terrain shading.",
                    Description      = "Parallax adds procedural terrain detail, tessellation, and dense planetary scatter systems for surface exploration.",
                    Authors          = "Gameslinx",
                    LatestVersion    = "2.1.0",
                    InstalledVersion = "Not installed",
                    Compatibility    = "KSP 1.12.5",
                    ModuleKind       = "Package",
                    License          = "All rights reserved",
                    ReleaseDate      = "2024-11-08",
                    DownloadSize     = "412 MiB",
                    DownloadCount    = 1863579,
                    DependencyCount     = 3,
                    RecommendationCount = 0,
                    SuggestionCount     = 1,
                    IsInstalled      = false,
                    HasUpdate        = false,
                    IsCached         = true,
                    IsIncompatible   = false,
                    HasReplacement   = false,
                },
                ["mechjeb2"] = new ModDetailsModel
                {
                    Identifier       = "mechjeb2",
                    Title            = "MechJeb 2",
                    Summary          = "Flight automation, maneuver planning, and vessel information.",
                    Description      = "MechJeb adds ascent guidance, rendezvous tools, information readouts, and maneuver planning for complex missions.",
                    Authors          = "Sarbian",
                    LatestVersion    = "2.14.3",
                    InstalledVersion = "2.14.3",
                    Compatibility    = "KSP 1.12.5",
                    ModuleKind       = "Package",
                    License          = "GPL-3.0",
                    ReleaseDate      = "2024-06-22",
                    DownloadSize     = "19 MiB",
                    DownloadCount    = 893441,
                    DependencyCount     = 1,
                    RecommendationCount = 0,
                    SuggestionCount     = 2,
                    IsInstalled      = true,
                    HasUpdate        = false,
                    IsCached         = false,
                    IsIncompatible   = false,
                    HasReplacement   = false,
                },
                ["kerbalism"] = new ModDetailsModel
                {
                    Identifier       = "kerbalism",
                    Title            = "Kerbalism",
                    Summary          = "Life support, reliability, science, and long-duration mission systems.",
                    Description      = "Kerbalism reshapes campaign progression around life support, radiation, reliability, and expanded science mechanics.",
                    Authors          = "ShotgunNinja",
                    LatestVersion    = "3.19.1",
                    InstalledVersion = "Not installed",
                    Compatibility    = "Older KSP release",
                    ModuleKind       = "Package",
                    License          = "CC-BY-NC-SA-4.0",
                    ReleaseDate      = "2023-09-19",
                    DownloadSize     = "74 MiB",
                    DownloadCount    = 217604,
                    DependencyCount     = 4,
                    RecommendationCount = 1,
                    SuggestionCount     = 1,
                    IsInstalled      = false,
                    HasUpdate        = false,
                    IsCached         = false,
                    IsIncompatible   = true,
                    HasReplacement   = false,
                },
                ["planetshine"] = new ModDetailsModel
                {
                    Identifier       = "planetshine",
                    Title            = "PlanetShine",
                    Summary          = "Adds bounced planetary light to vessels and IVA scenes.",
                    Description      = "PlanetShine adds subtle reflected light from planets and moons, improving atmosphere without changing gameplay.",
                    Authors          = "Valerian",
                    LatestVersion    = "0.2.7.5",
                    InstalledVersion = "Not installed",
                    Compatibility    = "KSP 1.12.5",
                    ModuleKind       = "Package",
                    License          = "MIT",
                    ReleaseDate      = "2022-03-12",
                    DownloadSize     = "6 MiB",
                    DownloadCount    = 625183,
                    DependencyCount     = 1,
                    RecommendationCount = 0,
                    SuggestionCount     = 0,
                    IsInstalled      = false,
                    HasUpdate        = false,
                    IsCached         = true,
                    IsIncompatible   = false,
                    HasReplacement   = false,
                },
            };

        public Task<IReadOnlyList<ModListItem>> GetAllModListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(allMods);
        }

        public async Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                      CancellationToken cancellationToken)
            => ApplyFilter(await GetAllModListAsync(cancellationToken), filter);

        public Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                                   CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(GetFilterOptionCounts(allMods, filter));
        }

        public IReadOnlyList<ModListItem> ApplyFilter(IReadOnlyList<ModListItem> items,
                                                      FilterState                 filter)
        {
            var filtered = items.Where(item => Matches(item, filter));
            return SortItems(filtered,
                             filter.SortOption,
                             filter.SortDescending ?? DefaultSortDescending(filter.SortOption)).ToList();
        }

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
        {
            cancellationToken.ThrowIfCancellationRequested();
            details.TryGetValue(identifier, out var model);
            return Task.FromResult(model);
        }

        private static bool Matches(ModListItem item, FilterState filter)
        {
            bool availabilityScopedToNotInstalled = !filter.InstalledOnly && !filter.UpdatableOnly;

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim();
                if (!Contains(item.Name, search)
                    && !Contains(item.Identifier, search)
                    && !Contains(item.Author, search)
                    && !Contains(item.Summary, search)
                    && !Contains(item.Description, search))
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
            if (filter.NewOnly && item.IsInstalled)
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
            if (filter.CompatibleOnly && item.IsIncompatible)
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

            return true;
        }

        private static int CountForPreview(IReadOnlyCollection<ModListItem> items,
                                           FilterState                       filter,
                                           Func<FilterState, FilterState>    applyPreviewFilter)
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
                NotInstalledOnly   = true,
                InstalledOnly      = false,
                UpdatableOnly      = false,
            };

        private static FilterState WithUpdatableOnly(FilterState filter)
            => filter with
            {
                UpdatableOnly      = true,
                NotInstalledOnly   = false,
                NotUpdatableOnly   = false,
            };

        private static FilterState WithCompatibleOnly(FilterState filter)
            => filter with
            {
                CompatibleOnly     = true,
                IncompatibleOnly   = false,
            };

        private static FilterState WithCachedOnly(FilterState filter)
            => filter with
            {
                CachedOnly         = true,
                UncachedOnly       = false,
            };

        private static FilterState WithUncachedOnly(FilterState filter)
            => filter with
            {
                CachedOnly         = false,
                UncachedOnly       = true,
            };

        private static FilterState WithIncompatibleOnly(FilterState filter)
            => filter with
            {
                CompatibleOnly     = false,
                IncompatibleOnly   = true,
            };

        private static FilterState WithReplacementOnly(FilterState filter)
            => filter with
            {
                HasReplacementOnly = true,
                NoReplacementOnly  = false,
            };

        private static bool Contains(string text, string search)
            => text.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private static ModListItem Decorate(ModListItem item)
        {
            string primaryStateLabel = item.IsIncompatible && !item.IsAutodetected
                ? "Incompatible"
                : item.HasVersionUpdate
                    ? "Update Available"
                    : item.IsInstalled
                        ? "Installed"
                        : item.HasReplacement
                            ? "Has Replacement"
                            : "Available";
            string primaryStateColor = item.IsIncompatible && !item.IsAutodetected
                ? "#9A485C"
                : item.HasVersionUpdate
                    ? "#6A952B"
                    : item.IsInstalled
                        ? "#2B6A98"
                        : item.HasReplacement
                            ? "#734790"
                            : "#2F7C58";
            string secondaryStateLabel = item.IsAutodetected ? "External" : "";
            string secondaryStateBackground = item.IsAutodetected ? "#5A4322" : "#39424E";
            string secondaryStateBorderBrush = item.IsAutodetected ? "#9F7A40" : "#607286";
            string tertiaryStateLabel = item.IsAutodetected && item.IsIncompatible ? "Dependency" : "";
            string tertiaryStateBackground = "#31424F";
            string tertiaryStateBorderBrush = "#4C6A86";

            var parts = new List<string>();
            if (item.HasVersionUpdate)
            {
                parts.Add("Installed");
            }
            if (item.HasReplacement)
            {
                parts.Add("Has replacement");
            }

            string statusSummary = string.Join(" • ", parts);

            return new ModListItem
            {
                Identifier = item.Identifier,
                Name = item.Name,
                Author = item.Author,
                Summary = item.Summary,
                Description = item.Description,
                LatestVersion = item.LatestVersion,
                InstalledVersion = item.InstalledVersion,
                ReleaseDate = item.ReleaseDate,
                ReleaseDateValue = item.ReleaseDateValue,
                InstallDate = item.InstallDate,
                InstallDateValue = item.InstallDateValue,
                DownloadCount = item.DownloadCount,
                DownloadCountLabel = item.DownloadCountLabel,
                IsInstalled = item.IsInstalled,
                IsAutodetected = item.IsAutodetected,
                HasUpdate = item.HasUpdate,
                HasVersionUpdate = item.HasVersionUpdate,
                IsIncompatible = item.IsIncompatible,
                IsCached = item.IsCached,
                HasReplacement = item.HasReplacement,
                Compatibility = item.Compatibility,
                PrimaryStateLabel = primaryStateLabel,
                PrimaryStateColor = primaryStateColor,
                SecondaryStateLabel = secondaryStateLabel,
                SecondaryStateBackground = secondaryStateBackground,
                SecondaryStateBorderBrush = secondaryStateBorderBrush,
                TertiaryStateLabel = tertiaryStateLabel,
                TertiaryStateBackground = tertiaryStateBackground,
                TertiaryStateBorderBrush = tertiaryStateBorderBrush,
                StatusSummary = statusSummary,
                HasStatusSummary = !string.IsNullOrWhiteSpace(statusSummary),
            };
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
    }
}
