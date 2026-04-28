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
        public void RefreshCurrentRegistryReference()
        {
            gameInstanceService.RefreshCurrentRegistry();
            this.RaisePropertyChanged(nameof(CurrentRegistry));
            this.RaisePropertyChanged(nameof(CurrentRegistryManager));
        }

        public void RefreshLaunchCommandState()
            => PublishLaunchCommandState();

        public IReadOnlyList<GameInstance> KnownGameInstances
            => gameInstanceService.Manager.Instances.Values.ToList();

        public void RefreshInstanceSummaries()
            => ReloadInstances(loadCatalog: false);

        public Task RefreshCurrentStateAsync()
            => RefreshAsync();

        public async Task RefreshRepositoriesAndCatalogAsync(bool forceFullRefresh = false)
        {
            if (!IsReady || IsRefreshing || IsApplyingChanges || IsCatalogLoading)
            {
                return;
            }

            IsRefreshing = true;
            ClearApplyResult();
            try
            {
                await UpdateRepositoriesForCurrentInstanceAsync(forceFullRefresh);
                RefreshCurrentRegistryReference();
                await LoadModCatalogAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public void RefreshVisibleVersionDisplaySettings()
            => ApplyVersionDisplaySettings(allCatalogItems);

        public async Task InstallFromCkanFilesAsync(IEnumerable<string> paths)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Select an instance before installing from .ckan files.";
                return;
            }

            var queued = 0;
            var skipped = 0;
            ClearApplyResult();

            foreach (var path in paths)
            {
                try
                {
                    var module = CkanModule.FromFile(path);
                    if (module.IsDLC)
                    {
                        skipped++;
                        continue;
                    }

                    queued += QueueInstallCandidate(module) ? 1 : 0;
                    skipped += changesetService.FindQueuedApplyAction(module.identifier) == null ? 1 : 0;

                    if (module.IsMetapackage && module.depends != null)
                    {
                        foreach (var dependency in module.depends)
                        {
                            var match = dependency.LatestAvailableWithProvides(CurrentRegistry,
                                                                               CurrentInstance.StabilityToleranceConfig,
                                                                               CurrentInstance.VersionCriteria())
                                                  .FirstOrDefault();
                            if (match == null)
                            {
                                skipped++;
                                continue;
                            }

                            queued += QueueInstallCandidate(match) ? 1 : 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    Diagnostics = ex.Message;
                }
            }

            if (queued > 0)
            {
                ShowPreviewSurface = true;
                StatusMessage = skipped == 0
                    ? $"Queued {queued} item{(queued == 1 ? "" : "s")} from .ckan file{(queued == 1 ? "" : "s")}."
                    : $"Queued {queued} item{(queued == 1 ? "" : "s")} from .ckan files; skipped {skipped}.";
            }
            else
            {
                StatusMessage = "No installable modules from the selected .ckan files could be queued.";
            }

            await Task.CompletedTask;
        }

        public async Task ImportDownloadedModsAsync(IEnumerable<string> paths)
        {
            if (CurrentInstance == null || CurrentRegistry == null || CurrentCache == null)
            {
                StatusMessage = "Select an instance before importing downloaded mods.";
                return;
            }

            var files = paths.Select(path => new FileInfo(path))
                             .Where(file => file.Exists)
                             .ToHashSet();
            if (files.Count == 0)
            {
                StatusMessage = "No downloaded mod archives were selected.";
                return;
            }

            IsUserBusy = true;
            StatusMessage = "Importing downloaded mods into the cache...";
            var installable = new List<CkanModule>();
            try
            {
                var imported = await Task.Run(() =>
                    ModuleImporter.ImportFiles(files,
                                               user,
                                               mod => installable.Add(mod),
                                               CurrentRegistry,
                                               CurrentInstance,
                                               CurrentCache));

                if (imported)
                {
                    var queued = 0;
                    foreach (var module in installable)
                    {
                        queued += QueueInstallCandidate(module) ? 1 : 0;
                    }

                    if (queued > 0)
                    {
                        ShowPreviewSurface = true;
                        StatusMessage = $"Imported files and queued {queued} install{(queued == 1 ? "" : "s")}.";
                    }
                    else
                    {
                        StatusMessage = "Imported files into the cache.";
                    }

                    await RefreshCurrentStateAsync();
                }
                else
                {
                    StatusMessage = "No matching CKAN mods were found in the selected files.";
                }
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Import downloaded mods failed.";
            }
            finally
            {
                IsUserBusy = false;
            }
        }

        public async Task<string> DeduplicateInstalledFilesAsync()
        {
            IsUserBusy = true;
            StatusMessage = "Scanning for duplicate installed files...";
            var previousMessage = user.LastMessage;
            try
            {
                await Task.Run(() =>
                {
                    var deduper = new InstalledFilesDeduplicator(CurrentManager.Instances.Values,
                                                                 gameInstanceService.RepositoryData);
                    deduper.DeduplicateAll(user);
                });
                var result = string.IsNullOrWhiteSpace(user.LastMessage) || user.LastMessage == previousMessage
                    ? "Deduplicate installed files finished."
                    : user.LastMessage;
                StatusMessage = result;
                return result;
            }
            catch (CancelledActionKraken)
            {
                StatusMessage = "Deduplicate installed files canceled.";
                return StatusMessage;
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Deduplicate installed files failed.";
                return $"{StatusMessage}\n\n{ex.Message}";
            }
            finally
            {
                IsUserBusy = false;
            }
        }

        public async Task ExportInstalledModListAsync(string         path,
                                                      ExportFileType exportFileType)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Select an instance before exporting the installed mod list.";
                return;
            }

            try
            {
                using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                    ? gameInstanceService.AcquireWriteRegistryManager()
                    : null;
                var manager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
                if (manager == null)
                {
                    StatusMessage = "Could not open the current registry for export.";
                    return;
                }

                await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                new Exporter(exportFileType).Export(manager, manager.registry, stream);
                StatusMessage = $"Saved installed mod list to {path}.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Save installed mod list failed.";
            }
        }

        public async Task ExportModpackAsync(string path)
        {
            if (CurrentInstance == null)
            {
                StatusMessage = "Select an instance before exporting a modpack.";
                return;
            }

            try
            {
                using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                    ? gameInstanceService.AcquireWriteRegistryManager()
                    : null;
                var manager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
                if (manager == null)
                {
                    StatusMessage = "Could not open the current registry for modpack export.";
                    return;
                }

                var json = manager.GenerateModpack(false, true).ToJson();
                await File.WriteAllTextAsync(path, json);
                StatusMessage = $"Exported re-importable modpack to {path}.";
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Export modpack failed.";
            }
        }

        public async Task<IReadOnlyList<RecommendationAuditItem>> AuditRecommendationsAsync()
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                StatusMessage = "Recommendation audit is unavailable until an instance and registry are loaded.";
                return Array.Empty<RecommendationAuditItem>();
            }

            var instance = CurrentInstance;
            var registry = CurrentRegistry;
            var preselectRecommendations = PreselectRecommendedMods;
            StatusMessage = "Auditing recommendations for installed mods...";

            try
            {
                var items = await Task.Run(() =>
                {
                    var installedModules = registry.InstalledModules
                                                   .Select(installed => installed.Module)
                                                   .ToHashSet();

                    if (!ModuleInstaller.FindRecommendations(instance,
                                                             installedModules,
                                                             Array.Empty<CkanModule>(),
                                                             Array.Empty<CkanModule>(),
                                                             Array.Empty<CkanModule>(),
                                                             registry,
                                                             out var recommendations,
                                                             out var suggestions,
                                                             out var supporters))
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    return BuildRecommendationAuditItems(recommendations,
                                                         suggestions,
                                                         supporters,
                                                         preselectRecommendations,
                                                         module => gameInstanceService.RepositoryData.GetDownloadCount(
                                                             registry.Repositories.Values,
                                                             module.identifier));
                });

                StatusMessage = items.Count == 0
                    ? "No recommended, suggested, or supporting mods found."
                    : $"Found {items.Count} recommendation audit item{(items.Count == 1 ? "" : "s")}.";
                return items;
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Recommendation audit failed.";
                return Array.Empty<RecommendationAuditItem>();
            }
        }

        public void QueueRecommendationAuditSelections(IEnumerable<RecommendationAuditItem> selections)
        {
            var selected = selections.Where(item => item.CanQueue)
                                     .ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "No recommendation audit items were selected.";
                return;
            }

            ClearApplyResult();

            var queued = 0;
            var skipped = 0;
            foreach (var item in selected)
            {
                var catalogItem = allCatalogItems.FirstOrDefault(mod =>
                    string.Equals(mod.Identifier, item.Identifier, StringComparison.OrdinalIgnoreCase));
                if (catalogItem == null
                    || catalogItem.IsInstalled
                    || catalogItem.IsIncompatible)
                {
                    skipped++;
                    continue;
                }

                changesetService.QueueInstall(catalogItem,
                                              item.Version,
                                              RecommendationQueueSourceText(item));
                queued++;
            }

            if (queued > 0)
            {
                ShowPreviewSurface = true;
                StatusMessage = skipped == 0
                    ? $"Queued {queued} recommendation audit item{(queued == 1 ? "" : "s")}."
                    : $"Queued {queued} recommendation audit item{(queued == 1 ? "" : "s")}; skipped {skipped} unavailable item{(skipped == 1 ? "" : "s")}.";
            }
            else
            {
                StatusMessage = "No selected recommendation audit items could be queued.";
            }
        }

        private async Task<bool> PromptForQueuedRecommendationsAsync()
        {
            if (RecommendationSelectionPromptAsync == null
                || CurrentInstance == null
                || CurrentRegistry == null
                || !HasQueuedChangeActions)
            {
                return true;
            }

            var shown = new List<CkanModule>();
            while (true)
            {
                var items = await BuildQueuedRecommendationAuditItemsAsync(shown);
                if (items.Count == 0)
                {
                    return true;
                }

                shown.AddRange(items.Select(item => item.Module));

                var selections = await RecommendationSelectionPromptAsync(items);
                if (selections == null)
                {
                    StatusMessage = "Apply canceled before optional recommendations were installed.";
                    return false;
                }

                var selected = selections.Where(item => item.CanQueue)
                                         .ToList();
                if (selected.Count == 0)
                {
                    StatusMessage = "Continuing without optional recommendations.";
                    return true;
                }

                QueueRecommendationAuditSelections(selected);
            }
        }

        private async Task<IReadOnlyList<RecommendationAuditItem>> BuildQueuedRecommendationAuditItemsAsync(
            IReadOnlyList<CkanModule> shown)
        {
            if (CurrentInstance == null || CurrentRegistry == null)
            {
                return Array.Empty<RecommendationAuditItem>();
            }

            var instance = CurrentInstance;
            var registry = CurrentRegistry;
            var actions = changesetService.CurrentApplyQueue.ToList();
            var preselectRecommendations = PreselectRecommendedMods;
            if (actions.Count == 0)
            {
                return Array.Empty<RecommendationAuditItem>();
            }

            var queuedIdentifiers = actions.Select(action => action.Identifier)
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

            try
            {
                return await Task.Run<IReadOnlyList<RecommendationAuditItem>>(() =>
                {
                    var requestedInstalls = new List<CkanModule>();
                    var requestedUpdates = new List<CkanModule>();
                    var requestedRemovals = new List<InstalledModule>();

                    foreach (var action in actions)
                    {
                        switch (action.ActionKind)
                        {
                            case QueuedActionKind.Install:
                                if (ResolveQueuedModule(registry, instance, action) is CkanModule installMod)
                                {
                                    requestedInstalls.Add(installMod);
                                }
                                break;
                            case QueuedActionKind.Update:
                                if (ResolveQueuedModule(registry, instance, action) is CkanModule updateMod)
                                {
                                    requestedUpdates.Add(updateMod);
                                }
                                break;
                            case QueuedActionKind.Remove:
                                if (registry.InstalledModule(action.Identifier) is InstalledModule removeMod)
                                {
                                    requestedRemovals.Add(removeMod);
                                }
                                break;
                        }
                    }

                    var combinedInstalls = DistinctRecommendationModules(requestedInstalls.Concat(requestedUpdates));
                    if (combinedInstalls.Count == 0)
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    var allRemoving = DistinctRecommendationModules(requestedRemovals.Select(module => module.Module));
                    var resolver = new RelationshipResolver(combinedInstalls,
                                                            allRemoving,
                                                            RelationshipResolverOptions.ConflictsOpts(instance.StabilityToleranceConfig),
                                                            registry,
                                                            instance.Game,
                                                            instance.VersionCriteria());
                    var resolvedInstalls = DistinctRecommendationModules(resolver.ModList(false));

                    if (!ModuleInstaller.FindRecommendations(instance,
                                                             resolvedInstalls,
                                                             resolvedInstalls,
                                                             allRemoving,
                                                             shown,
                                                             registry,
                                                             out var recommendations,
                                                             out var suggestions,
                                                             out var supporters))
                    {
                        return Array.Empty<RecommendationAuditItem>();
                    }

                    return BuildRecommendationAuditItems(recommendations,
                                                         suggestions,
                                                         supporters,
                                                         preselectRecommendations,
                                                         module => gameInstanceService.RepositoryData.GetDownloadCount(
                                                             registry.Repositories.Values,
                                                             module.identifier))
                           .Where(item => !queuedIdentifiers.Contains(item.Identifier))
                           .ToList();
                });
            }
            catch (Exception ex)
            {
                Diagnostics = ex.Message;
                StatusMessage = "Recommendation check failed; continuing with queued changes only.";
                return Array.Empty<RecommendationAuditItem>();
            }
        }
    }
}
