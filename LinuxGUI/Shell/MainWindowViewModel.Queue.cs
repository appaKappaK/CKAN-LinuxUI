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
        private void QueueInstallSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueInstall(SelectedMod,
                                          SelectedModVersionChoice?.VersionKey,
                                          QueueSourceForScopedBrowserMod(SelectedMod));
            StatusMessage = SelectedModVersionChoice == null
                ? $"Queued install for {SelectedMod.Name}."
                : $"Queued install of {SelectedMod.Name} {SelectedModVersionChoice.VersionText}.";
        }

        private bool QueueInstallCandidate(CkanModule module)
        {
            if (module.IsDLC)
            {
                return false;
            }

            var catalogItem = allCatalogItems.FirstOrDefault(mod =>
                string.Equals(mod.Identifier, module.identifier, StringComparison.OrdinalIgnoreCase));
            if (catalogItem == null || catalogItem.IsInstalled || catalogItem.IsIncompatible)
            {
                return false;
            }

            changesetService.QueueInstall(catalogItem, module.version?.ToString());
            return true;
        }

        private void QueueUpdateSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueUpdate(SelectedMod, SelectedModVersionChoice?.VersionKey);
            StatusMessage = SelectedModVersionChoice == null
                ? $"Queued update for {SelectedMod.Name}."
                : $"Queued version change for {SelectedMod.Name} to {SelectedModVersionChoice.VersionText}.";
        }

        private void QueueRemoveSelected()
        {
            if (SelectedMod == null)
            {
                return;
            }

            ClearApplyResult();
            changesetService.QueueRemove(SelectedMod);
            StatusMessage = $"Queued removal for {SelectedMod.Name}.";
        }

        private async Task QueueRemoveAllInstalledModsAsync()
        {
            var targets = BuildRemoveAllInstalledTargets();
            if (targets.Count == 0)
            {
                StatusMessage = "No CKAN-managed installed mods are available to queue for removal.";
                return;
            }

            var existingCount = changesetService.CurrentQueue.Count;
            var prompt = $"Queue removal for all {targets.Count} CKAN-managed installed mod{Pluralize(targets.Count)} in {CurrentInstanceName}? This does not apply immediately; it replaces the current queue and opens the preview so you can review everything before applying.";
            if (existingCount > 0)
            {
                prompt += $" The current {existingCount} queued item{Pluralize(existingCount)} will be replaced.";
            }

            if (ConfirmQueueRemoveAllInstalledModsAsync != null
                && !await ConfirmQueueRemoveAllInstalledModsAsync(prompt))
            {
                StatusMessage = "Canceled queue removal for all installed mods.";
                return;
            }

            ClearApplyResult();
            lastRemovedQueuedActions = Array.Empty<QueuedActionModel>();
            changesetService.Restore(targets.Select(CreateRemoveQueuedAction).ToList());
            queueDrawerStickyCollapsed = false;
            IsQueueDrawerExpanded = true;
            ShowPreviewSurfaceTab();
            StatusMessage = $"Queued removal for {targets.Count} CKAN-managed installed mod{Pluralize(targets.Count)}.";
            PublishQueueStateLabels();
        }

        private async Task CleanupMissingInstalledModsAsync()
        {
            var managedTargets = BuildRemoveMissingInstalledTargets();
            var autodetectedTargets = BuildMissingAutodetectedDllTargets();
            if (managedTargets.Count == 0
                && autodetectedTargets.Count == 0)
            {
                StatusMessage = "No CKAN-managed installed mods or autodetected DLL records with missing files were detected.";
                return;
            }

            var targetSummary = FormatMissingCleanupTargetSummary(managedTargets.Count,
                                                                  autodetectedTargets.Count);
            var prompt = $"Clean up {targetSummary} from {CurrentInstanceName}? This updates CKAN's registry immediately and does not delete files.";

            if (ConfirmCleanupMissingInstalledModsAsync != null
                && !await ConfirmCleanupMissingInstalledModsAsync(prompt))
            {
                StatusMessage = "Canceled cleanup for missing installed mods.";
                return;
            }

            ClearApplyResult();
            SetExecutionState("Cleaning Missing Mods",
                              $"Cleaning {targetSummary}...");
            IsApplyingChanges = true;

            ApplyChangesResult result;
            try
            {
                var cleanupResult = await Task.Run(() => CleanupMissingInstalledRegistryEntries(
                    managedTargets.Select(target => target.Identifier).ToList(),
                    cleanupAutodetectedDlls: autodetectedTargets.Count > 0));

                foreach (var target in managedTargets)
                {
                    var queued = changesetService.FindQueuedApplyAction(target.Identifier);
                    if (queued?.ActionKind == QueuedActionKind.Remove)
                    {
                        changesetService.Remove(target.Identifier);
                    }
                }

                gameInstanceService.RefreshCurrentRegistry();

                var removedCount = cleanupResult.RemovedManagedModules.Count
                                   + cleanupResult.RemovedAutodetectedModules.Count;
                var summaryLines = cleanupResult.RemovedManagedModules
                    .Select(name => $"Removed CKAN registry entry: {name}")
                    .Concat(cleanupResult.RemovedAutodetectedModules
                        .Select(name => $"Removed stale autodetected DLL record: {name}"))
                    .ToList();
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Success,
                    Success = true,
                    Title = "Missing Mods Cleaned Up",
                    Message = removedCount == 0
                        ? "No missing installed mod records needed cleanup."
                        : $"Cleaned up {removedCount} missing installed mod record{Pluralize(removedCount)}.",
                    SummaryLines = summaryLines,
                };
                SetApplyResult(result);
                StatusMessage = result.Message;
            }
            catch (Exception ex)
            {
                result = new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Error,
                    Success = false,
                    Title = "Cleanup Failed",
                    Message = ex.Message,
                };
                SetApplyResult(result);
                Diagnostics = ex.Message;
                StatusMessage = "Missing installed mod cleanup failed.";
            }
            finally
            {
                IsApplyingChanges = false;
            }

            if (result.Success)
            {
                await LoadCatalogAfterAppliedChangesAsync();
            }

            ShowExecutionResultDialog(result.Success);
        }

        private MissingInstalledCleanupResult CleanupMissingInstalledRegistryEntries(
            IReadOnlyCollection<string> identifiers,
            bool                        cleanupAutodetectedDlls)
        {
            if (gameInstanceService.CurrentInstance is not GameInstance instance)
            {
                throw new InvalidOperationException("No current instance is available.");
            }

            using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                ? gameInstanceService.AcquireWriteRegistryManager()
                : null;
            var manager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
            if (manager == null)
            {
                throw new InvalidOperationException("Could not open the current registry for cleanup.");
            }

            var requested = identifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedManaged = new List<string>();
            if (requested.Count > 0)
            {
                using var transaction = CkanTransaction.CreateTransactionScope();
                foreach (var installed in manager.registry.InstalledModules.ToList())
                {
                    if (!requested.Contains(installed.identifier)
                        || installed.Module.IsDLC
                        || manager.registry.IsAutodetected(installed.identifier)
                        || !RegisteredFilesMissingFromDisk(instance, installed))
                    {
                        continue;
                    }

                    manager.registry.DeregisterModule(instance, installed.identifier);
                    removedManaged.Add(installed.Module.name ?? installed.identifier);
                }

                if (removedManaged.Count > 0)
                {
                    manager.Save(false);
                }
                transaction.Complete();
            }

            var removedAutodetected = new List<string>();
            if (cleanupAutodetectedDlls)
            {
                var staleAutodetectedBefore = BuildMissingAutodetectedDllTargets(instance, manager.registry)
                    .ToList();
                if (staleAutodetectedBefore.Count > 0)
                {
                    var scanChanged = manager.ScanUnmanagedFiles();
                    removedAutodetected.AddRange(staleAutodetectedBefore
                        .Where(target => !manager.registry.IsAutodetected(target.Identifier))
                        .Select(target => target.Identifier)
                        .OrderBy(identifier => identifier, StringComparer.CurrentCultureIgnoreCase));
                    if (scanChanged)
                    {
                        manager.Save(false);
                    }
                }
            }

            return new MissingInstalledCleanupResult
            {
                RemovedManagedModules     = removedManaged,
                RemovedAutodetectedModules = removedAutodetected,
            };
        }

        private IReadOnlyList<ModListItem> BuildRemoveAllInstalledTargets()
        {
            if (gameInstanceService.CurrentRegistry is Registry registry)
            {
                var catalogByIdentifier = allCatalogItems
                    .ToDictionary(item => item.Identifier,
                                  item => item,
                                  StringComparer.OrdinalIgnoreCase);

                return registry.InstalledModules
                               .Where(im => !im.Module.IsDLC
                                            && !registry.IsAutodetected(im.identifier))
                               .Select(im => catalogByIdentifier.TryGetValue(im.identifier, out var item)
                                   ? item
                                   : new ModListItem
                                   {
                                       Identifier       = im.identifier,
                                       Name             = im.Module.name ?? im.identifier,
                                       InstalledVersion = im.Module.version.ToString(),
                                       IsInstalled      = true,
                                   })
                               .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                               .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                               .ToList();
            }

            return allCatalogItems
                   .Where(item => item.IsInstalled && !item.IsAutodetected)
                   .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                   .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                   .ToList();
        }

        private IReadOnlyList<ModListItem> BuildRemoveMissingInstalledTargets()
        {
            if (gameInstanceService.CurrentInstance is not GameInstance instance
                || gameInstanceService.CurrentRegistry is not Registry registry)
            {
                return Array.Empty<ModListItem>();
            }

            var catalogByIdentifier = allCatalogItems
                .ToDictionary(item => item.Identifier,
                              item => item,
                              StringComparer.OrdinalIgnoreCase);

            return registry.InstalledModules
                           .Where(im => !im.Module.IsDLC
                                        && !registry.IsAutodetected(im.identifier)
                                        && RegisteredFilesMissingFromDisk(instance, im))
                           .Select(im => catalogByIdentifier.TryGetValue(im.identifier, out var item)
                               ? item
                               : new ModListItem
                               {
                                   Identifier       = im.identifier,
                                   Name             = im.Module.name ?? im.identifier,
                                   InstalledVersion = im.Module.version.ToString(),
                                   IsInstalled      = true,
                               })
                           .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                           .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                           .ToList();
        }

        private IReadOnlyList<AutodetectedDllCleanupTarget> BuildMissingAutodetectedDllTargets()
        {
            if (gameInstanceService.CurrentInstance is not GameInstance instance
                || gameInstanceService.CurrentRegistry is not Registry registry)
            {
                return Array.Empty<AutodetectedDllCleanupTarget>();
            }

            return BuildMissingAutodetectedDllTargets(instance, registry);
        }

        private static IReadOnlyList<AutodetectedDllCleanupTarget> BuildMissingAutodetectedDllTargets(
            GameInstance instance,
            Registry     registry)
            => registry.InstalledDlls
                       .Select(identifier => new AutodetectedDllCleanupTarget
                       {
                           Identifier  = identifier,
                           RelativePath = registry.DllPath(identifier) ?? "",
                       })
                       .Where(target => target.RelativePath.Length > 0
                                        && !File.Exists(instance.ToAbsoluteGameDir(target.RelativePath)))
                       .OrderBy(target => target.Identifier, StringComparer.CurrentCultureIgnoreCase)
                       .ToList();

        private static string FormatMissingCleanupTargetSummary(int managedCount,
                                                                int autodetectedCount)
        {
            var parts = new List<string>();
            if (managedCount > 0)
            {
                parts.Add($"{managedCount} CKAN-managed installed mod record{Pluralize(managedCount)}");
            }
            if (autodetectedCount > 0)
            {
                parts.Add($"{autodetectedCount} stale autodetected DLL record{Pluralize(autodetectedCount)}");
            }

            return string.Join(" and ", parts);
        }

        private static bool RegisteredFilesMissingFromDisk(GameInstance instance, InstalledModule module)
        {
            var registeredFiles = module.Files.ToList();
            return registeredFiles.Count > 0
                   && registeredFiles.All(relPath => !File.Exists(instance.ToAbsoluteGameDir(relPath)));
        }

        private sealed class MissingInstalledCleanupResult
        {
            public IReadOnlyList<string> RemovedManagedModules { get; init; }
                = Array.Empty<string>();

            public IReadOnlyList<string> RemovedAutodetectedModules { get; init; }
                = Array.Empty<string>();
        }

        private sealed class AutodetectedDllCleanupTarget
        {
            public string Identifier { get; init; } = "";

            public string RelativePath { get; init; } = "";
        }

        private static QueuedActionModel CreateRemoveQueuedAction(ModListItem mod)
            => new QueuedActionModel
            {
                Identifier    = mod.Identifier,
                Name          = mod.Name,
                TargetVersion = "",
                ActionKind    = QueuedActionKind.Remove,
                ActionText    = "Remove",
                DetailText    = string.IsNullOrWhiteSpace(mod.InstalledVersion)
                    ? "Remove installed module"
                    : $"Remove {mod.InstalledVersion}",
            };

        private void SeedDevQueueSmoke(IReadOnlyList<ModListItem> catalogItems)
        {
            if (hasSeededDevQueueSmoke
                || !DevQueueSmokeEnabled()
                || catalogItems.Count == 0)
            {
                return;
            }

            hasSeededDevQueueSmoke = true;
            suppressQueueSnapshotPersistence = true;
            suppressQueueChangedRefresh = true;
            pendingQueueChangedRefresh = false;
            SaveEmptyQueuedActionSnapshotForCurrentInstance();
            ClearApplyResult();
            int seeded = 0;
            try
            {
                changesetService.Clear();

                var usedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                seeded += QueueDevSmokeActions(catalogItems,
                                               usedIdentifiers,
                                               CanQueueInstall,
                                               mod => changesetService.QueueInstall(mod),
                                               InstallSmokeScore,
                                               10);
                seeded += QueueDevSmokeActions(catalogItems,
                                               usedIdentifiers,
                                               CanQueueUpdate,
                                               mod => changesetService.QueueUpdate(mod),
                                               _ => 0,
                                               3);
                seeded += QueueDevSmokeActions(catalogItems,
                                               usedIdentifiers,
                                               CanQueueRemove,
                                               mod => changesetService.QueueRemove(mod),
                                               _ => 0,
                                               3);
                seeded += QueueDevSmokeActions(catalogItems,
                                               usedIdentifiers,
                                               CanQueueDownloadForDevSmoke,
                                               mod => changesetService.QueueDownload(mod),
                                               DownloadSmokeScore,
                                               DevQueueSmokeTargetActionCount - seeded);

                while (seeded < DevQueueSmokeTargetActionCount)
                {
                    var added = 0;
                    added += QueueDevSmokeActions(catalogItems,
                                                  usedIdentifiers,
                                                  CanQueueInstall,
                                                  mod => changesetService.QueueInstall(mod),
                                                  InstallSmokeScore,
                                                  1);
                    added += QueueDevSmokeActions(catalogItems,
                                                  usedIdentifiers,
                                                  CanQueueUpdate,
                                                  mod => changesetService.QueueUpdate(mod),
                                                  _ => 0,
                                                  1);
                    added += QueueDevSmokeActions(catalogItems,
                                                  usedIdentifiers,
                                                  CanQueueRemove,
                                                  mod => changesetService.QueueRemove(mod),
                                                  _ => 0,
                                                  1);
                    added += QueueDevSmokeActions(catalogItems,
                                                  usedIdentifiers,
                                                  CanQueueDownloadForDevSmoke,
                                                  mod => changesetService.QueueDownload(mod),
                                                  DownloadSmokeScore,
                                                  1);
                    if (added == 0)
                    {
                        break;
                    }

                    seeded += added;
                }
            }
            finally
            {
                suppressQueueChangedRefresh = false;
            }

            if (seeded > 0)
            {
                ShowPreviewSurface = true;
                IsQueueDrawerExpanded = true;
                PublishQueueStateLabels();
            }
            if (pendingQueueChangedRefresh)
            {
                pendingQueueChangedRefresh = false;
                Dispatcher.UIThread.Post(() =>
                {
                    RefreshQueuedActions();
                    _ = LoadPreviewAsync();
                });
            }
        }

        private static int QueueDevSmokeActions(IReadOnlyList<ModListItem> catalogItems,
                                                HashSet<string>            usedIdentifiers,
                                                Func<ModListItem, bool>    canQueue,
                                                Action<ModListItem>        queue,
                                                Func<ModListItem, int>     score,
                                                int                        count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var mods = catalogItems.Where(item => !usedIdentifiers.Contains(item.Identifier)
                                                  && canQueue(item))
                                   .OrderByDescending(score)
                                   .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                                   .Take(count)
                                   .ToList();
            foreach (var mod in mods)
            {
                usedIdentifiers.Add(mod.Identifier);
                queue(mod);
            }

            return mods.Count;
        }

        private static bool CanQueueDownloadForDevSmoke(ModListItem mod)
            => !mod.IsAutodetected
               && !mod.IsCached
               && !mod.IsIncompatible;

        private static int InstallSmokeScore(ModListItem mod)
            => (!string.IsNullOrWhiteSpace(mod.Depends) ? 4 : 0)
               + (!string.IsNullOrWhiteSpace(mod.Recommends) ? 3 : 0)
               + (!string.IsNullOrWhiteSpace(mod.Suggests) ? 2 : 0)
               + (!mod.IsCached ? 1 : 0);

        private static int DownloadSmokeScore(ModListItem mod)
            => !mod.IsInstalled ? 1 : 0;

        private static bool DevQueueSmokeEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("CKAN_LINUX_DEV_QUEUE_SMOKE"),
                             "1",
                             StringComparison.Ordinal);

        private IReadOnlyList<string> BuildDevSmokePreviewAutoRemovals(IReadOnlyList<string> current)
        {
            var candidates = new[]
            {
                "[Dev Smoke] Auto-removal fixture 1",
                "[Dev Smoke] Auto-removal fixture 2",
                "[Dev Smoke] Auto-removal fixture 3",
                "[Dev Smoke] Auto-removal fixture 4",
                "[Dev Smoke] Auto-removal fixture 5",
                "[Dev Smoke] Auto-removal fixture 6",
            };

            return BuildDevSmokePreviewList(current,
                                            candidates,
                                            DevQueueSmokePreviewAutoRemovalCount);
        }

        private IReadOnlyList<string> BuildDevSmokePreviewConflicts(IReadOnlyList<string> current)
        {
            var result = current.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            var usedPairs = result.Select(ConflictPairKey)
                                  .Where(key => !string.IsNullOrWhiteSpace(key))
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fixtureConflicts = new[]
            {
                "[Dev Smoke] queued install fixture 1 conflicts with [Dev Smoke] installed fixture 1",
                "[Dev Smoke] queued install fixture 2 conflicts with [Dev Smoke] installed fixture 2",
                "[Dev Smoke] queued update fixture 1 conflicts with [Dev Smoke] available fixture 1",
                "[Dev Smoke] queued removal fixture 1 conflicts with [Dev Smoke] installed fixture 3",
                "[Dev Smoke] queued install fixture with a long display name conflicts with [Dev Smoke] installed fixture with a long display name",
                "[Dev Smoke] available fixture 2 conflicts with [Dev Smoke] installed fixture 4",
                "[Dev Smoke] queued install fixture 3 conflicts with [Dev Smoke] queued install fixture 4",
                "[Dev Smoke] installed fixture 5 conflicts with [Dev Smoke] installed fixture 6",
            };

            foreach (var conflict in fixtureConflicts)
            {
                var pairKey = ConflictPairKey(conflict);
                if (string.IsNullOrWhiteSpace(pairKey)
                    || !usedPairs.Add(pairKey))
                {
                    continue;
                }

                result.Add(conflict);
                if (result.Count >= DevQueueSmokePreviewConflictCount)
                {
                    break;
                }
            }

            return result;
        }

        private static IReadOnlyList<string> BuildDevSmokePreviewList(IReadOnlyList<string> current,
                                                                      IEnumerable<string>   candidates,
                                                                      int                   targetCount)
        {
            var result = current.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
            var used = result.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (result.Count >= targetCount)
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(candidate)
                    && used.Add(candidate))
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private static IReadOnlyList<RecommendationAuditItem> BuildRecommendationAuditItems(
            Dictionary<CkanModule, Tuple<bool, List<string>>> recommendations,
            Dictionary<CkanModule, List<string>>              suggestions,
            Dictionary<CkanModule, HashSet<string>>           supporters,
            bool                                              preselectRecommendations,
            Func<CkanModule, int?>                            downloadCountForModule)
            => recommendations
                   .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                              "Recommendation",
                                                              FormatRecommendationSource("Recommended by", kvp.Value.Item2),
                                                              preselectRecommendations && kvp.Value.Item1,
                                                              downloadCountForModule(kvp.Key)))
                   .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                   .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                   .Concat(suggestions
                       .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                                  "Suggestion",
                                                                  FormatRecommendationSource("Suggested by", kvp.Value),
                                                                  false,
                                                                  downloadCountForModule(kvp.Key)))
                       .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase))
                   .Concat(supporters
                       .Select(kvp => new RecommendationAuditItem(kvp.Key,
                                                                  "Supporter",
                                                                  FormatRecommendationSource("Supports", kvp.Value),
                                                                  false,
                                                                  downloadCountForModule(kvp.Key)))
                       .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                       .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase))
                   .ToList();

        private static string FormatRecommendationSource(string              label,
                                                         IEnumerable<string> sources)
        {
            var sourceText = string.Join(", ", sources.Where(source => !string.IsNullOrWhiteSpace(source))
                                                      .OrderBy(source => source, StringComparer.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(sourceText)
                ? label
                : $"{label}: {sourceText}";
        }

        private static string RecommendationQueueSourceText(RecommendationAuditItem item)
        {
            var related = item.RelatedMods;
            if (string.IsNullOrWhiteSpace(related))
            {
                return item.Kind;
            }

            return item.Kind switch
            {
                "Recommendation" => $"Recommended by {related}",
                "Suggestion"     => $"Suggested by {related}",
                "Supporter"      => $"Supported by {related}",
                _                => $"{item.Kind} from {related}",
            };
        }

        private static CkanModule? ResolveQueuedModule(IRegistryQuerier  registry,
                                                       GameInstance      instance,
                                                       QueuedActionModel action)
        {
            if (!string.IsNullOrWhiteSpace(action.TargetVersion))
            {
                var exact = TryQueuedVersion(registry, action);
                if (exact != null)
                {
                    return exact;
                }

                return null;
            }

            return TryLatestCompatible(registry, instance, action.Identifier);
        }

        private static CkanModule? TryQueuedVersion(IRegistryQuerier  registry,
                                                    QueuedActionModel action)
        {
            var targetVersion = action.TargetVersion.Trim();
            var exact = Utilities.DefaultIfThrows(() => registry.GetModuleByVersion(action.Identifier,
                                                                                    targetVersion));
            if (exact != null)
            {
                return exact;
            }

            if (registry.InstalledModule(action.Identifier)?.Module is CkanModule installed
                && VersionTextMatches(installed.version.ToString(), targetVersion))
            {
                return installed;
            }

            return Utilities.DefaultIfThrows(() => registry.AvailableByIdentifier(action.Identifier)
                .FirstOrDefault(module => VersionTextMatches(module.version.ToString(), targetVersion)));
        }

        private static CkanModule? TryLatestCompatible(IRegistryQuerier registry,
                                                       GameInstance    instance,
                                                       string          identifier)
        {
            try
            {
                var latest = registry.LatestAvailable(identifier,
                                                       instance.StabilityToleranceConfig,
                                                       instance.VersionCriteria());
                if (latest != null)
                {
                    return latest;
                }
            }
            catch
            {
            }

            var versionCriteria = instance.VersionCriteria();
            return Utilities.DefaultIfThrows(() => registry.AvailableByIdentifier(identifier)
                .Where(module => module.IsCompatible(versionCriteria))
                .OrderByDescending(module => module.version)
                .FirstOrDefault());
        }

        private static bool VersionTextMatches(string left, string right)
            => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase)
               || string.Equals(NormalizeVersionText(left),
                                NormalizeVersionText(right),
                                StringComparison.OrdinalIgnoreCase);

        private static string NormalizeVersionText(string version)
            => version.Trim().TrimStart('v', 'V');

        private static List<CkanModule> DistinctRecommendationModules(IEnumerable<CkanModule> modules)
            => modules.GroupBy(module => module.identifier, StringComparer.OrdinalIgnoreCase)
                      .Select(group => group.First())
                      .ToList();

        private void RemoveSelectedQueuedAction()
        {
            if (SelectedQueuedAction == null)
            {
                return;
            }

            RemoveQueuedAction(SelectedQueuedAction);
        }

        public bool ShowRemoveQueuedActionContextAction(QueuedActionModel action)
            => !IsApplyingChanges
               && !IsRequiredDependencyQueuedAction(action);

        public void RemoveQueuedActionFromPreview(QueuedActionModel action)
        {
            if (!ShowRemoveQueuedActionContextAction(action))
            {
                return;
            }

            SelectedQueuedAction = action;
            RemoveQueuedAction(action);
        }

        private void RemoveQueuedAction(QueuedActionModel action)
        {
            ClearApplyResult();
            if (changesetService.Remove(action.Identifier))
            {
                RememberRemovedQueuedActions(new[] { action });
                StatusMessage = $"Removed queued action for {action.Name}.";
            }
        }

        private static bool IsRequiredDependencyQueuedAction(QueuedActionModel action)
        {
            var sourceText = action.SourceText.Trim();
            return sourceText.StartsWith("Required by", StringComparison.OrdinalIgnoreCase)
                   || sourceText.StartsWith("Required dependency", StringComparison.OrdinalIgnoreCase);
        }

        private void RemoveSelectedPreviewConflict()
        {
            if (selectedPreviewConflicts.Count == 0)
            {
                return;
            }

            var targets = selectedPreviewConflicts
                .Select(QueuedActionFromConflict)
                .Where(target => target != null)
                .Select(target => target!)
                .GroupBy(target => target.Identifier, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (targets.Count == 0)
            {
                StatusMessage = "Select conflicts for queued mods before removing them.";
                return;
            }

            ClearApplyResult();
            var removedNames = new List<string>();
            foreach (var target in targets)
            {
                if (changesetService.Remove(target.Identifier))
                {
                    removedNames.Add(target.Name);
                }
            }

            RememberRemovedQueuedActions(targets);
            ClearPreviewConflictSelection();
            if (removedNames.Count > 0)
            {
                StatusMessage = removedNames.Count == 1
                    ? $"Removed queued action for {removedNames[0]}."
                    : $"Removed {removedNames.Count} queued actions.";
            }
        }

        private QueuedActionModel? QueuedActionFromConflict(string conflict)
            => QueuedActionFromConflictSide(ConflictLeftSide(conflict))
               ?? QueuedActions.OrderByDescending(action => action.Identifier.Length)
                               .FirstOrDefault(action => ConflictMentionsQueuedAction(conflict, action));

        private QueuedActionModel? QueuedActionFromConflictSide(string side)
            => QueuedActions.OrderByDescending(action => action.Identifier.Length)
                            .FirstOrDefault(action => ConflictSideStartsWith(side, action.Identifier)
                                                   || ConflictSideStartsWith(side, action.Name));

        private ModListItem? ModFromConflictSide(string side)
        {
            var directMatch = allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ConflictSideStartsWith(side, item.Identifier)
                                     || ConflictSideStartsWith(side, item.Name));
            if (directMatch != null)
            {
                return directMatch;
            }

            var displayName = StripConflictVersionSuffix(side);
            return allCatalogItems
                .OrderByDescending(item => item.Identifier.Length)
                .FirstOrDefault(item => ContainsText(item.Identifier, displayName)
                                     || ContainsText(item.Name, displayName)
                                     || ContainsText(displayName, item.Identifier)
                                     || ContainsText(displayName, item.Name));
        }

        private static string ConflictLeftSide(string conflict)
        {
            var parts = conflict.Split(new[] { " conflicts with " },
                                       StringSplitOptions.None);
            return parts.Length > 0
                ? parts[0].Trim()
                : conflict.Trim();
        }

        private static string ConflictRightSide(string conflict)
        {
            var parts = conflict.Split(new[] { " conflicts with " },
                                       StringSplitOptions.None);
            return parts.Length > 1
                ? parts[1].Trim()
                : "";
        }

        private static string DisplayConflictTarget(string side,
                                                    QueuedActionModel? queuedTarget = null)
        {
            if (queuedTarget != null
                && (ConflictSideStartsWith(side, queuedTarget.Identifier)
                    || ConflictSideStartsWith(side, queuedTarget.Name)))
            {
                return queuedTarget.Name;
            }

            return StripConflictVersionSuffix(side);
        }

        private static string StripConflictVersionSuffix(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return "";
            }

            var withoutVersion = Regex.Replace(trimmed,
                                               @"\s+(?:\d+:)?v?\d+(?:[.\-_]\w+)+(?:\+\S*)?$",
                                               "",
                                               RegexOptions.IgnoreCase);
            return withoutVersion.Length > 0
                ? withoutVersion
                : trimmed;
        }

        private static bool ConflictMentionsQueuedAction(string conflict,
                                                        QueuedActionModel action)
            => ContainsText(conflict, action.Identifier)
               || ContainsText(conflict, action.Name);

        private static bool ConflictSideStartsWith(string side,
                                                   string candidate)
        {
            if (string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            var trimmedSide = side.Trim();
            var trimmedCandidate = candidate.Trim();
            if (!trimmedSide.StartsWith(trimmedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmedSide.Length == trimmedCandidate.Length)
            {
                return true;
            }

            var next = trimmedSide[trimmedCandidate.Length];
            return char.IsWhiteSpace(next)
                   || next == ':'
                   || next == '('
                   || next == '[';
        }

        private static bool ContainsText(string text,
                                         string value)
            => !string.IsNullOrWhiteSpace(value)
               && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        private async Task ClearQueuedActionsAsync()
        {
            var queuedActions = changesetService.CurrentQueue.ToList();
            if (queuedActions.Count == 0)
            {
                return;
            }

            if (ConfirmClearQueueAsync != null
                && !await ConfirmClearQueueAsync($"Clear all {queuedActions.Count} queued action{(queuedActions.Count == 1 ? "" : "s")}?"))
            {
                return;
            }

            ClearApplyResult();
            changesetService.Clear();
            StatusMessage = "Cleared all pending items.";
        }

        private void UndoQueuedActionRemoval()
        {
            if (lastRemovedQueuedActions.Count == 0)
            {
                return;
            }

            var restoredCount = lastRemovedQueuedActions.Count;
            var restoredActions = changesetService.CurrentQueue
                                                  .Concat(lastRemovedQueuedActions)
                                                  .GroupBy(action => action.Identifier, StringComparer.OrdinalIgnoreCase)
                                                  .Select(group => group.Last())
                                                  .ToList();
            lastRemovedQueuedActions = Array.Empty<QueuedActionModel>();
            ClearApplyResult();
            changesetService.Restore(restoredActions);
            ShowPreviewSurface = true;
            StatusMessage = restoredCount == 1
                ? "Restored queued action."
                : $"Restored {restoredCount} queued actions.";
            PublishQueueStateLabels();
        }

        private void RememberRemovedQueuedActions(IEnumerable<QueuedActionModel> actions)
        {
            lastRemovedQueuedActions = actions.ToList();
            PublishQueueStateLabels();
        }

        private void DiscardQueuedActionsForInstanceSwitch()
        {
            ClearApplyResult();
            changesetService.Clear();
            RefreshQueuedActions();
            ResetPreviewState();
            ShowPreviewSurface = false;
        }

        private void RestorePersistedQueuedActions()
        {
            if (hasRestoredQueuedActionSnapshot)
            {
                return;
            }

            var instanceName = gameInstanceService.CurrentInstance?.Name;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                return;
            }

            hasRestoredQueuedActionSnapshot = true;
            var snapshot = appSettingsService.QueuedActionSnapshot;
            if (snapshot.Actions.Count == 0)
            {
                return;
            }

            if (!string.Equals(snapshot.InstanceName, instanceName, StringComparison.Ordinal))
            {
                SaveQueuedActionsForCurrentInstance();
                return;
            }

            var actions = snapshot.Actions
                                  .Select(ToQueuedActionModel)
                                  .Where(action => !string.IsNullOrWhiteSpace(action.Identifier))
                                  .ToList();
            if (actions.Count == 0)
            {
                SaveQueuedActionsForCurrentInstance();
                return;
            }

            changesetService.Restore(actions);
        }

        private void SaveQueuedActionsForCurrentInstance()
        {
            appSettingsService.SaveQueuedActionSnapshot(new QueuedActionSnapshot
            {
                InstanceName = gameInstanceService.CurrentInstance?.Name ?? "",
                Actions = changesetService.CurrentQueue
                    .Select(ToQueuedActionSnapshotItem)
                    .ToList(),
            });
        }

        private void SaveEmptyQueuedActionSnapshotForCurrentInstance()
            => appSettingsService.SaveQueuedActionSnapshot(new QueuedActionSnapshot
            {
                InstanceName = gameInstanceService.CurrentInstance?.Name ?? "",
                Actions = new List<QueuedActionSnapshotItem>(),
            });

        private static QueuedActionSnapshotItem ToQueuedActionSnapshotItem(QueuedActionModel action)
            => new QueuedActionSnapshotItem
            {
                Identifier    = action.Identifier,
                Name          = action.Name,
                TargetVersion = action.TargetVersion,
                ActionKind    = action.ActionKind,
                ActionText    = action.ActionText,
                DetailText    = action.DetailText,
                SourceText    = action.SourceText,
            };

        private static QueuedActionModel ToQueuedActionModel(QueuedActionSnapshotItem item)
            => new QueuedActionModel
            {
                Identifier    = item.Identifier ?? "",
                Name          = item.Name ?? item.Identifier ?? "",
                TargetVersion = item.TargetVersion ?? "",
                ActionKind    = item.ActionKind,
                ActionText    = string.IsNullOrWhiteSpace(item.ActionText)
                    ? DefaultQueuedActionText(item.ActionKind)
                    : item.ActionText,
                DetailText    = string.IsNullOrWhiteSpace(item.DetailText)
                    ? DefaultQueuedDetailText(item)
                    : item.DetailText,
                SourceText    = item.SourceText ?? "",
            };

        private static string DefaultQueuedActionText(QueuedActionKind kind)
            => kind switch
            {
                QueuedActionKind.Download => "Add to Cache",
                QueuedActionKind.Install  => "Install",
                QueuedActionKind.Update   => "Update",
                QueuedActionKind.Remove   => "Remove",
                _                         => "Queue",
            };

        private static string DefaultQueuedDetailText(QueuedActionSnapshotItem item)
            => item.ActionKind switch
            {
                QueuedActionKind.Download => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Cache latest available version for later install"
                    : $"Cache {item.TargetVersion} for later install",
                QueuedActionKind.Install => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Install latest available version"
                    : $"Install {item.TargetVersion}",
                QueuedActionKind.Update => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Update to latest available version"
                    : $"Update to {item.TargetVersion}",
                QueuedActionKind.Remove => string.IsNullOrWhiteSpace(item.TargetVersion)
                    ? "Remove installed module"
                    : $"Remove {item.TargetVersion}",
                _ => "Queued action",
            };

        private void RefreshQueuedActions()
        {
            var previousSelection = SelectedQueuedAction?.Identifier;
            var previousCount = QueuedActions.Count;

            QueuedActions.Clear();
            foreach (var item in changesetService.CurrentQueue)
            {
                QueuedActions.Add(item);
            }

            SelectedQueuedAction = previousSelection != null
                ? QueuedActions.FirstOrDefault(item => item.Identifier == previousSelection) ?? QueuedActions.FirstOrDefault()
                : QueuedActions.FirstOrDefault();

            if (previousCount == 0 && QueuedActions.Count > 0)
            {
                queueDrawerStickyCollapsed = false;
                IsQueueDrawerExpanded = true;
            }
            else if (QueuedActions.Count == 0)
            {
                queueDrawerStickyCollapsed = false;
                IsQueueDrawerExpanded = false;
            }
            else if (queueDrawerStickyCollapsed)
            {
                IsQueueDrawerExpanded = false;
            }

            PublishVisibleModQueueState();
            PublishQueueStateLabels();
            PublishSelectedModActionState();
        }

        private void PublishVisibleModQueueState()
        {
            foreach (var mod in Mods)
            {
                PublishModQueueState(mod);
            }
        }

        private void PublishPreviewActionStateLabels()
        {
            this.RaisePropertyChanged(nameof(PreviewExtrasSelectionAvailable));
            this.RaisePropertyChanged(nameof(ShowPreviewExtrasActionNotice));
            this.RaisePropertyChanged(nameof(PreviewExtrasActionNotice));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBackground));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonBorderBrush));
            this.RaisePropertyChanged(nameof(ApplyChangesButtonLabel));
        }

        private void PublishModQueueState(ModListItem mod)
        {
            var queued = changesetService.FindQueuedApplyAction(mod.Identifier)
                        ?? changesetService.FindQueuedDownloadAction(mod.Identifier);

            if (queued == null)
            {
                mod.QueueStateLabel = "";
                mod.QueueStateBackground = "#00000000";
                mod.QueueStateBorderBrush = "#00000000";
                mod.QueueRowAccentBrush = "#00000000";
                return;
            }

            mod.QueueStateLabel = queued.ActionKind switch
            {
                QueuedActionKind.Install => "Queued Install",
                QueuedActionKind.Update => "Queued Update",
                QueuedActionKind.Remove => "Queued Remove",
                QueuedActionKind.Download when string.Equals(queued.ActionText, "Add to Cache", StringComparison.Ordinal)
                    => "Queued Cache",
                QueuedActionKind.Download => "Queued Download",
                _ => "Queued",
            };

            (mod.QueueStateBackground, mod.QueueStateBorderBrush, mod.QueueRowAccentBrush) = queued.ActionKind switch
            {
                QueuedActionKind.Install => ("#1A2027", "#4B7B5E", "#664B7B5E"),
                QueuedActionKind.Update => ("#1A2027", "#7C8F55", "#667C8F55"),
                QueuedActionKind.Remove => ("#1A2027", "#8A5665", "#668A5665"),
                QueuedActionKind.Download => ("#1A2027", "#5F7DA0", "#665F7DA0"),
                _ => ("#1A2027", "#667487", "#66667487"),
            };
        }
    }
}
