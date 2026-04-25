using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.IO;

namespace CKAN.App.Services
{
    public sealed class ModActionService : IModActionService
    {
        private readonly IGameInstanceService gameInstanceService;
        private readonly IChangesetService    changesetService;
        private readonly IUser                user;

        public ModActionService(IGameInstanceService gameInstanceService,
                                IChangesetService    changesetService,
                                IUser                user)
        {
            this.gameInstanceService = gameInstanceService;
            this.changesetService    = changesetService;
            this.user                = user;
        }

        public Task<ChangesetPreviewModel> PreviewChangesAsync(CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var plan = BuildExecutionPlan(changesetService.CurrentApplyQueue, cancellationToken);
                return new ChangesetPreviewModel
                {
                    SummaryText        = plan.SummaryText,
                    CanApply           = plan.CanApply,
                    DownloadsRequired  = plan.DownloadsRequired,
                    DependencyInstalls = plan.DependencyInstalls,
                    AutoRemovals       = plan.AutoRemovals,
                    AttentionNotes     = plan.AttentionNotes,
                    Recommendations    = plan.Recommendations,
                    Suggestions        = plan.Suggestions,
                    Conflicts          = plan.Conflicts,
                };
            }, cancellationToken);

        public Task<ApplyChangesResult> ApplyChangesAsync(CancellationToken cancellationToken)
            => ExecuteQueuedAsync(changesetService.CurrentApplyQueue,
                                  changesetService.ClearApplyQueue,
                                  downloadOnlyRun: false,
                                  cancellationToken);

        public async Task<ApplyChangesResult> InstallNowAsync(ModListItem mod,
                                                              CancellationToken cancellationToken,
                                                              string? targetVersion = null)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }

            var result = await ExecuteQueuedAsync(new[]
            {
                CreateInstallAction(mod, targetVersion),
            },
            () => { },
            downloadOnlyRun: false,
            cancellationToken);

            return RewordInstallNowResult(mod, result);
        }

        public async Task<ApplyChangesResult> RemoveNowAsync(ModListItem mod,
                                                             CancellationToken cancellationToken)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }

            var result = await ExecuteQueuedAsync(new[]
            {
                CreateRemoveAction(mod),
            },
            () => { },
            downloadOnlyRun: false,
            cancellationToken);

            return RewordRemoveNowResult(mod, result);
        }

        public Task<ApplyChangesResult> DownloadQueuedAsync(CancellationToken cancellationToken)
            => ExecuteQueuedAsync(changesetService.CurrentDownloadQueue,
                                  changesetService.ClearDownloadQueue,
                                  downloadOnlyRun: true,
                                  cancellationToken);

        private Task<ApplyChangesResult> ExecuteQueuedAsync(IReadOnlyList<QueuedActionModel> requestedActions,
                                                            Action                       clearQueue,
                                                            bool                         downloadOnlyRun,
                                                            CancellationToken            cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var plan = BuildExecutionPlan(requestedActions, cancellationToken);
                if (!plan.CanApply)
                {
                    var followUps = plan.AttentionNotes.Concat(plan.Conflicts)
                                                       .Distinct()
                                                       .ToList();
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Blocked,
                        Success = false,
                        Title = plan.Conflicts.Count > 0
                            ? downloadOnlyRun ? "Downloads Blocked" : "Apply Blocked"
                            : downloadOnlyRun ? "Downloads Need Attention" : "Apply Needs Attention",
                        Message = plan.Conflicts.Count > 0
                            ? downloadOnlyRun
                                ? $"Cannot download queued mods: {plan.Conflicts[0]}"
                                : $"Cannot apply queued changes: {plan.Conflicts[0]}"
                            : followUps.FirstOrDefault() ?? plan.SummaryText,
                        SummaryLines = BuildSummaryLines(plan),
                        FollowUpLines = followUps,
                    };
                }

                if (gameInstanceService.Manager.Cache is not NetModuleCache cache)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Blocked,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Unavailable" : "Apply Unavailable",
                        Message = "Cannot apply changes because downloaded files are unavailable right now.",
                        FollowUpLines = new[]
                        {
                            "Reload instances or restart CKAN Linux to restore access to downloaded files.",
                        },
                    };
                }

                using var transientRegistryManager = gameInstanceService.CurrentRegistryManager == null
                    ? gameInstanceService.AcquireWriteRegistryManager()
                    : null;
                var registryManager = transientRegistryManager ?? gameInstanceService.CurrentRegistryManager;
                if (registryManager == null)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Blocked,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Unavailable" : "Apply Unavailable",
                        Message = "Cannot access the writable registry for the current install.",
                        FollowUpLines = new[]
                        {
                            "Reload the current install and try again.",
                        },
                    };
                }

                plan = BuildExecutionPlan(requestedActions, cancellationToken, registryManager);
                if (!plan.CanApply)
                {
                    var followUps = plan.AttentionNotes.Concat(plan.Conflicts)
                                                       .Distinct()
                                                       .ToList();
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Blocked,
                        Success = false,
                        Title = plan.Conflicts.Count > 0
                            ? downloadOnlyRun ? "Downloads Blocked" : "Apply Blocked"
                            : downloadOnlyRun ? "Downloads Need Attention" : "Apply Needs Attention",
                        Message = plan.Conflicts.Count > 0
                            ? downloadOnlyRun
                                ? $"Cannot download queued mods: {plan.Conflicts[0]}"
                                : $"Cannot apply queued changes: {plan.Conflicts[0]}"
                            : followUps.FirstOrDefault() ?? plan.SummaryText,
                        SummaryLines = BuildSummaryLines(plan),
                        FollowUpLines = followUps,
                    };
                }

                try
                {
                    registryManager.ScanUnmanagedFiles();

                    var queuedDownloads = plan.RequestedDownloads
                                              .Where(mod => !ContainsIdentifier(plan.RequestedInstalls, mod.identifier)
                                                            && !ContainsIdentifier(plan.RequestedUpdates, mod.identifier)
                                                            && !mod.IsMetapackage
                                                            && !cache.IsMaybeCachedZip(mod))
                                              .ToList();
                    var toInstall   = plan.RequestedInstalls.ToList();
                    var toUpgrade   = plan.RequestedUpdates.ToList();
                    var toUninstall = plan.RequestedRemovals.Select(im => im.identifier)
                                                            .ToList();
                    var hasTransactionalActions = toInstall.Count > 0
                                                  || toUpgrade.Count > 0
                                                  || toUninstall.Count > 0;
                    var autoInstalled = new HashSet<CkanModule>();
                    var downloader = new NetAsyncModulesDownloader(user, cache, null, cancellationToken);
                    var deduper = new InstalledFilesDeduplicator(plan.Instance,
                                                                 gameInstanceService.Manager.Instances.Values,
                                                                 gameInstanceService.RepositoryData);
                    var installer = new ModuleInstaller(plan.Instance,
                                                        cache,
                                                        gameInstanceService.Configuration,
                                                        user,
                                                        cancellationToken);
                    HashSet<string>? possibleConfigOnlyDirs = null;

                    if (queuedDownloads.Count > 0)
                    {
                        downloader.DownloadModules(queuedDownloads);
                    }

                    if (!hasTransactionalActions)
                    {
                        clearQueue();

                        return new ApplyChangesResult
                        {
                            Kind = ApplyResultKind.Success,
                            Success = true,
                            Title = "Downloads Completed",
                            Message = queuedDownloads.Count > 0
                                ? $"Downloaded {queuedDownloads.Count} queued item{Pluralize(queuedDownloads.Count)} for later install."
                                : "All queued downloads were already available locally.",
                            SummaryLines = BuildSummaryLines(plan),
                            FollowUpLines = Array.Empty<string>(),
                        };
                    }

                    for (var resolvedAllProvidedMods = false; !resolvedAllProvidedMods;)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (var transaction = CkanTransaction.CreateTransactionScope())
                        {
                            try
                            {
                                if (toUninstall.Count > 0)
                                {
                                    installer.UninstallList(toUninstall,
                                                            ref possibleConfigOnlyDirs,
                                                            registryManager,
                                                            false,
                                                            toInstall.Concat(toUpgrade).ToList());
                                    toUninstall.Clear();
                                }

                                if (toInstall.Count > 0)
                                {
                                    installer.InstallList(toInstall,
                                                          plan.InstallOptions!,
                                                          registryManager,
                                                          ref possibleConfigOnlyDirs,
                                                          deduper,
                                                          null,
                                                          downloader,
                                                          autoInstalled,
                                                          false);
                                    toInstall.Clear();
                                }

                                if (toUpgrade.Count > 0)
                                {
                                    installer.Upgrade(toUpgrade,
                                                      downloader,
                                                      ref possibleConfigOnlyDirs,
                                                      registryManager,
                                                      deduper,
                                                      autoInstalled,
                                                      true,
                                                      false);
                                    toUpgrade.Clear();
                                }

                                transaction.Complete();
                                resolvedAllProvidedMods = true;
                            }
                            catch (TooManyModsProvideKraken ex)
                            {
                                if (PromptForProvidedModule(registryManager.registry, cache, ex)
                                    is not CkanModule chosen)
                                {
                                    return new ApplyChangesResult
                                    {
                                        Kind = ApplyResultKind.Canceled,
                                        Success = false,
                                        Title = "Apply Canceled",
                                        Message = "Apply canceled while resolving a virtual dependency choice.",
                                        FollowUpLines = new[]
                                        {
                                            $"Choose a provider for {ex.requested} and apply again.",
                                        },
                                    };
                                }

                                if (!ContainsIdentifier(toInstall, chosen.identifier)
                                    && !ContainsIdentifier(toUpgrade, chosen.identifier))
                                {
                                    toInstall.Add(chosen);
                                }
                                autoInstalled.Add(chosen);
                            }
                        }
                    }

                    var leftoverConfigDirs = FilterConfigOnlyDirs(possibleConfigOnlyDirs,
                                                                  registryManager.registry,
                                                                  plan.Instance);

                    clearQueue();

                    return new ApplyChangesResult
                    {
                        Kind = leftoverConfigDirs.Count > 0
                            ? ApplyResultKind.Warning
                            : ApplyResultKind.Success,
                        Success = true,
                        Title = leftoverConfigDirs.Count > 0
                            ? "Changes Applied - Review Needed"
                            : "Changes Applied",
                        Message = leftoverConfigDirs.Count > 0
                            ? $"Applied {plan.RequestedActions.Count} queued action{Pluralize(plan.RequestedActions.Count)}. Kept {leftoverConfigDirs.Count} config-only director{(leftoverConfigDirs.Count == 1 ? "y" : "ies")} for manual review."
                            : $"Applied {plan.RequestedActions.Count} queued action{Pluralize(plan.RequestedActions.Count)} successfully.",
                        SummaryLines = BuildSummaryLines(plan),
                        FollowUpLines = leftoverConfigDirs.Count > 0
                            ? leftoverConfigDirs.Select(dir => $"Review leftover config-only directory: {dir}")
                                               .ToList()
                            : Array.Empty<string>(),
                    };
                }
                catch (CancelledActionKraken ex)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Canceled,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Canceled" : "Apply Canceled",
                        Message = string.IsNullOrWhiteSpace(ex.Message)
                            ? downloadOnlyRun ? "Downloads canceled." : "Apply canceled."
                            : ex.Message,
                    };
                }
                catch (RequestThrottledKraken ex)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Blocked,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Delayed by Rate Limit" : "Apply Delayed by Rate Limit",
                        Message = ex.Message,
                        FollowUpLines = new[]
                        {
                            $"Try again after {ex.retryTime.ToLocalTime():yyyy-MM-dd HH:mm}.",
                            $"More details: {ex.infoUrl}",
                        },
                    };
                }
                catch (Kraken ex)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Error,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Failed" : "Apply Failed",
                        Message = ex.Message,
                    };
                }
                catch (Exception ex)
                {
                    return new ApplyChangesResult
                    {
                        Kind = ApplyResultKind.Error,
                        Success = false,
                        Title = downloadOnlyRun ? "Downloads Failed" : "Apply Failed",
                        Message = $"{(downloadOnlyRun ? "Downloads" : "Apply")} failed: {ex.Message}",
                    };
                }
                finally
                {
                    if (transientRegistryManager != null)
                    {
                        gameInstanceService.RefreshCurrentRegistry();
                    }
                }
            }, cancellationToken);

        private ExecutionPlan BuildExecutionPlan(IReadOnlyList<QueuedActionModel> requestedActions,
                                                 CancellationToken            cancellationToken,
                                                 RegistryManager?             registryManager = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (gameInstanceService.CurrentInstance is not GameInstance instance
                || (registryManager?.registry ?? gameInstanceService.CurrentRegistry) is not Registry registry)
            {
                return new ExecutionPlan
                {
                    SummaryText = "Preview is unavailable until a game instance and registry are loaded.",
                    AttentionNotes = new[]
                    {
                        "Select or reload an install before building an apply preview.",
                    },
                };
            }

            if (requestedActions.Count == 0)
            {
                return new ExecutionPlan
                {
                    Instance        = instance,
                    RegistryManager = registryManager,
                    SummaryText     = "Queue install, update, or remove actions to build an apply preview.",
                };
            }

            if (gameInstanceService.Manager.Cache == null)
            {
                return new ExecutionPlan
                {
                    Instance        = instance,
                    RegistryManager = registryManager,
                    RequestedActions = requestedActions,
                    SummaryText     = "Preview is unavailable because downloaded files are unavailable right now.",
                    AttentionNotes  = new[]
                    {
                        "Reload instances or restart CKAN Linux to restore access to downloaded files.",
                    },
                };
            }
            var requestedDownloads = new List<CkanModule>();
            var requestedInstalls = new List<CkanModule>();
            var requestedUpdates  = new List<CkanModule>();
            var requestedRemovals = new List<InstalledModule>();
            var resolutionErrors  = new List<string>();

            foreach (var action in requestedActions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (action.ActionKind)
                {
                    case QueuedActionKind.Download:
                        if (ResolveRequestedModule(registry, instance, action) is CkanModule downloadMod)
                        {
                            requestedDownloads.Add(downloadMod);
                        }
                        else
                        {
                            resolutionErrors.Add($"Could not resolve a download candidate for {action.Name}.");
                        }
                        break;

                    case QueuedActionKind.Install:
                        if (ResolveRequestedModule(registry, instance, action) is CkanModule installMod)
                        {
                            requestedInstalls.Add(installMod);
                        }
                        else
                        {
                            resolutionErrors.Add($"Could not resolve an install candidate for {action.Name}.");
                        }
                        break;

                    case QueuedActionKind.Update:
                        if (registry.InstalledModule(action.Identifier) is InstalledModule
                            && ResolveRequestedModule(registry, instance, action) is CkanModule updateMod)
                        {
                            requestedUpdates.Add(updateMod);
                        }
                        else
                        {
                            resolutionErrors.Add($"Could not resolve an update candidate for {action.Name}.");
                        }
                        break;

                    case QueuedActionKind.Remove:
                        if (registry.InstalledModule(action.Identifier) is InstalledModule removeMod)
                        {
                            requestedRemovals.Add(removeMod);
                        }
                        else if (registry.IsAutodetected(action.Identifier))
                        {
                            resolutionErrors.Add($"{action.Name} was detected outside CKAN and cannot be removed automatically. Remove its files manually from GameData if you want it gone.");
                        }
                        else
                        {
                            resolutionErrors.Add($"Could not resolve an installed module to remove for {action.Name}.");
                        }
                        break;
                }
            }

            requestedDownloads = DistinctModules(requestedDownloads);
            requestedInstalls = DistinctModules(requestedInstalls);
            requestedUpdates  = DistinctModules(requestedUpdates);
            requestedRemovals = DistinctInstalledModules(requestedRemovals);

            var dependencyInstalls = new List<string>();
            var downloadsRequired  = new List<string>();
            var autoRemovals       = new List<string>();
            var recommendations    = new List<string>();
            var suggestions        = new List<string>();
            var notices            = new List<string>();
            var conflicts          = new List<string>();
            var cache = gameInstanceService.Manager.Cache!;

            conflicts.AddRange(resolutionErrors);

            var combinedInstalls = DistinctModules(requestedInstalls.Concat(requestedUpdates));
            var directDownloads = DistinctModules(
                requestedDownloads.Where(mod => !ContainsIdentifier(combinedInstalls, mod.identifier)));

            downloadsRequired.AddRange(
                directDownloads
                    .Where(mod => !mod.IsMetapackage && !cache.IsMaybeCachedZip(mod))
                    .Select(mod => cache.DescribeAvailability(gameInstanceService.Configuration, mod))
                    .Distinct()
                    .ToList());

            if (conflicts.Count == 0 && combinedInstalls.Count > 0)
            {
                try
                {
                    var autoRemovalModules = registry.FindRemovableAutoInstalled(
                                                     combinedInstalls,
                                                     requestedRemovals.Select(im => im.identifier)
                                                                     .ToHashSet(StringComparer.OrdinalIgnoreCase),
                                                     instance)
                                                 .ToList();
                    var allRemoving = DistinctModules(requestedRemovals.Select(im => im.Module)
                                                                      .Concat(autoRemovalModules.Select(im => im.Module)));

                    var resolver = new RelationshipResolver(combinedInstalls,
                                                            allRemoving,
                                                            RelationshipResolverOptions.ConflictsOpts(instance.StabilityToleranceConfig),
                                                            registry,
                                                            instance.Game,
                                                            instance.VersionCriteria());
                    var resolvedInstalls = DistinctModules(resolver.ModList(false));

                    downloadsRequired.AddRange(
                        resolvedInstalls
                            .Where(mod => !mod.IsMetapackage && !cache.IsMaybeCachedZip(mod))
                            .Select(mod => cache.DescribeAvailability(gameInstanceService.Configuration, mod))
                            .Distinct()
                            .ToList());

                    dependencyInstalls.AddRange(
                        BuildDependencyInstalls(resolvedInstalls,
                                                combinedInstalls,
                                                registry));

                    autoRemovals.AddRange(
                        autoRemovalModules
                            .Select(im => FormatModule(im.Module))
                            .Distinct()
                            .ToList());

                    conflicts.AddRange(resolver.ConflictDescriptions);

                    try
                    {
                        if (combinedInstalls.Count > 0
                            && ModuleInstaller.FindRecommendations(instance,
                                                                   resolvedInstalls,
                                                                   resolvedInstalls,
                                                                   allRemoving,
                                                                   Array.Empty<CkanModule>(),
                                                                   registry,
                                                                   out var recs,
                                                                   out var suggs,
                                                                   out _))
                        {
                            recommendations.AddRange(recs.Select(kvp
                                => $"{FormatModule(kvp.Key)} recommended by {string.Join(", ", kvp.Value.Item2.OrderBy(v => v))}"));
                            suggestions.AddRange(suggs.Select(kvp
                                => $"{FormatModule(kvp.Key)} suggested by {string.Join(", ", kvp.Value.OrderBy(v => v))}"));
                        }
                    }
                    catch (Exception ex)
                    {
                        conflicts.Add($"Recommendation analysis failed: {ex.Message}");
                    }
                }
                catch (TooManyModsProvideKraken ex)
                {
                    notices.Add(
                        $"Provider choice required for {ex.requested}. Apply will prompt you to choose one during resolution.");
                }
                catch (Kraken ex)
                {
                    conflicts.Add(ex.Message);
                }
                catch (Exception ex)
                {
                    conflicts.Add($"Preview generation failed: {ex.Message}");
                }
            }

            conflicts = conflicts.Distinct().ToList();

            return new ExecutionPlan
            {
                Instance         = instance,
                RegistryManager  = registryManager,
                RequestedActions = requestedActions,
                RequestedDownloads = requestedDownloads,
                RequestedInstalls = requestedInstalls,
                RequestedUpdates = requestedUpdates,
                RequestedRemovals = requestedRemovals,
                InstallOptions   = RelationshipResolverOptions.DependsOnlyOpts(instance.StabilityToleranceConfig),
                SummaryText      = BuildSummary(requestedActions.Count,
                                                requestedDownloads.Count,
                                                combinedInstalls.Count,
                                                downloadsRequired.Count,
                                                dependencyInstalls.Count,
                                                autoRemovals.Count,
                                                conflicts.Count),
                CanApply           = conflicts.Count == 0,
                DownloadsRequired  = downloadsRequired,
                DependencyInstalls = dependencyInstalls,
                AutoRemovals       = autoRemovals,
                AttentionNotes     = notices,
                Recommendations    = recommendations.Distinct().ToList(),
                Suggestions        = suggestions.Distinct().ToList(),
                Conflicts          = conflicts,
            };
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

        private static string BuildSummary(int requestedCount,
                                           int requestedDownloadCount,
                                           int resolvedInstallCount,
                                           int downloadCount,
                                           int dependencyCount,
                                           int autoRemovalCount,
                                           int conflictCount)
        {
            var parts = new List<string>
            {
                $"{requestedCount} requested action{(requestedCount == 1 ? "" : "s")}",
            };

            if (requestedDownloadCount > 0)
            {
                parts.Add($"{requestedDownloadCount} direct download{(requestedDownloadCount == 1 ? "" : "s")}");
            }
            if (resolvedInstallCount > 0)
            {
                parts.Add($"{resolvedInstallCount} install/update target{(resolvedInstallCount == 1 ? "" : "s")}");
            }

            if (downloadCount > 0)
            {
                parts.Add($"{downloadCount} download{(downloadCount == 1 ? "" : "s")} required");
            }
            if (dependencyCount > 0)
            {
                parts.Add($"{dependencyCount} dependency install{(dependencyCount == 1 ? "" : "s")}");
            }
            if (autoRemovalCount > 0)
            {
                parts.Add($"{autoRemovalCount} auto-removal{(autoRemovalCount == 1 ? "" : "s")}");
            }
            if (conflictCount > 0)
            {
                parts.Add($"{conflictCount} conflict{(conflictCount == 1 ? "" : "s")}");
            }

            return string.Join(" • ", parts);
        }

        private static string FormatModule(CkanModule module)
            => $"{module.name} ({module.identifier} {module.version})";

        private static IReadOnlyList<string> BuildDependencyInstalls(IReadOnlyCollection<CkanModule> resolvedInstalls,
                                                                     IReadOnlyCollection<CkanModule> requestedInstalls,
                                                                     Registry                        registry)
        {
            var reverseDependencies = BuildReverseDependencyMap(resolvedInstalls);
            var requestedRoots = requestedInstalls.ToDictionary(mod => mod.identifier,
                                                                mod => mod,
                                                                StringComparer.OrdinalIgnoreCase);

            return resolvedInstalls
                   .Where(mod => !requestedRoots.ContainsKey(mod.identifier)
                                 && !registry.IsInstalled(mod.identifier))
                   .Select(mod =>
                   {
                       var requiredBy = FindRequestedDependencyRoots(mod.identifier,
                                                                     reverseDependencies,
                                                                     requestedRoots);
                       return requiredBy.Count > 0
                           ? $"{FormatModule(mod)} required by {string.Join(", ", requiredBy)}"
                           : FormatModule(mod);
                   })
                   .Distinct()
                   .ToList();
        }

        private static Dictionary<string, HashSet<string>> BuildReverseDependencyMap(IReadOnlyCollection<CkanModule> modules)
        {
            var reverseDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var module in modules)
            {
                if (module.depends == null)
                {
                    continue;
                }

                foreach (var dependency in module.depends)
                {
                    if (!dependency.MatchesAny(modules, null, null, out var matched)
                        || matched == null)
                    {
                        continue;
                    }

                    if (!reverseDependencies.TryGetValue(matched.identifier, out var dependers))
                    {
                        dependers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        reverseDependencies[matched.identifier] = dependers;
                    }

                    dependers.Add(module.identifier);
                }
            }

            return reverseDependencies;
        }

        private static IReadOnlyList<string> FindRequestedDependencyRoots(
            string                                  dependencyIdentifier,
            IReadOnlyDictionary<string, HashSet<string>> reverseDependencies,
            IReadOnlyDictionary<string, CkanModule> requestedRoots)
        {
            var pending = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requiredBy = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

            pending.Enqueue(dependencyIdentifier);
            visited.Add(dependencyIdentifier);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!reverseDependencies.TryGetValue(current, out var dependers))
                {
                    continue;
                }

                foreach (var dependerIdentifier in dependers)
                {
                    if (requestedRoots.TryGetValue(dependerIdentifier, out var requestedRoot))
                    {
                        requiredBy.Add(requestedRoot.name);
                    }

                    if (visited.Add(dependerIdentifier))
                    {
                        pending.Enqueue(dependerIdentifier);
                    }
                }
            }

            return requiredBy.ToList();
        }

        private CkanModule? PromptForProvidedModule(Registry registry,
                                                    NetModuleCache cache,
                                                    TooManyModsProvideKraken ex)
        {
            var choices = ex.modules.OrderByDescending(cache.IsCached)
                                    .ThenByDescending(m => gameInstanceService.RepositoryData.GetDownloadCount(
                                        registry.Repositories.Values,
                                        m.identifier) ?? 0)
                                    .ThenByDescending(m => string.Equals(m.identifier,
                                                                         ex.requested,
                                                                         StringComparison.OrdinalIgnoreCase))
                                    .ThenBy(m => m.name, StringComparer.CurrentCultureIgnoreCase)
                                    .ToArray();

            var selection = user.RaiseSelectionDialog(ex.Message,
                                                      choices.Select(m => $"{m.identifier} ({m.name})")
                                                             .ToArray());

            return selection >= 0 && selection < choices.Length
                ? choices[selection]
                : null;
        }

        private static List<CkanModule> DistinctModules(IEnumerable<CkanModule> modules)
            => modules.GroupBy(mod => mod.identifier, StringComparer.OrdinalIgnoreCase)
                      .Select(grp => grp.First())
                      .ToList();

        private static List<InstalledModule> DistinctInstalledModules(IEnumerable<InstalledModule> modules)
            => modules.GroupBy(mod => mod.identifier, StringComparer.OrdinalIgnoreCase)
                      .Select(grp => grp.First())
                      .ToList();

        private static bool ContainsIdentifier(IEnumerable<CkanModule> modules,
                                               string                  identifier)
            => modules.Any(mod => string.Equals(mod.identifier, identifier, StringComparison.OrdinalIgnoreCase));

        private static HashSet<string> FilterConfigOnlyDirs(HashSet<string>? possibleConfigOnlyDirs,
                                                            Registry         registry,
                                                            GameInstance     instance)
        {
            possibleConfigOnlyDirs ??= new HashSet<string>(Platform.PathComparer);
            possibleConfigOnlyDirs.RemoveWhere(directory =>
            {
                if (!Directory.Exists(directory))
                {
                    return true;
                }

                try
                {
                    return Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                                    .Select(instance.ToRelativeGameDir)
                                    .Any(relPath => registry.FileOwner(relPath) != null);
                }
                catch
                {
                    return false;
                }
            });
            return possibleConfigOnlyDirs;
        }

        private static string Pluralize(int count)
            => count == 1 ? "" : "s";

        private static QueuedActionModel CreateInstallAction(ModListItem mod,
                                                             string?     targetVersion = null)
        {
            var resolvedTargetVersion = QueueTargetVersion(mod, targetVersion);
            return new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = resolvedTargetVersion,
                ActionKind = QueuedActionKind.Install,
                ActionText = "Install",
                DetailText = string.IsNullOrWhiteSpace(resolvedTargetVersion)
                    ? "Install latest available version"
                    : $"Install {resolvedTargetVersion}",
            };
        }

        private static QueuedActionModel CreateRemoveAction(ModListItem mod)
            => new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = "",
                ActionKind = QueuedActionKind.Remove,
                ActionText = "Remove",
                DetailText = string.IsNullOrWhiteSpace(mod.InstalledVersion)
                    ? "Remove installed module"
                    : $"Remove {mod.InstalledVersion}",
            };

        private static CkanModule? ResolveRequestedModule(IRegistryQuerier  registry,
                                                          GameInstance      instance,
                                                          QueuedActionModel action)
        {
            if (!string.IsNullOrWhiteSpace(action.TargetVersion))
            {
                var exact = TryRequestedVersion(registry, action);
                if (exact != null)
                {
                    return exact;
                }

                return null;
            }

            return TryLatestCompatible(registry, instance, action.Identifier);
        }

        private static string QueueTargetVersion(ModListItem mod,
                                                 string?     targetVersion)
            => !string.IsNullOrWhiteSpace(targetVersion)
                ? targetVersion.Trim()
                : mod.LatestVersion?.Trim() ?? "";

        private static CkanModule? TryRequestedVersion(IRegistryQuerier  registry,
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

        private static bool VersionTextMatches(string left, string right)
            => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase)
               || string.Equals(NormalizeVersionText(left),
                                NormalizeVersionText(right),
                                StringComparison.OrdinalIgnoreCase);

        private static string NormalizeVersionText(string version)
            => version.Trim().TrimStart('v', 'V');

        private static ApplyChangesResult RewordInstallNowResult(ModListItem mod,
                                                                 ApplyChangesResult result)
        {
            var summaryLines = result.SummaryLines
                                     .Where(line => line.IndexOf("queued action",
                                                                 StringComparison.OrdinalIgnoreCase) < 0)
                                     .ToList();
            if (summaryLines.Count == 0)
            {
                summaryLines.Add("1 direct install");
            }

            return new ApplyChangesResult
            {
                Kind          = result.Kind,
                Success       = result.Success,
                Title         = GetInstallNowTitle(result.Kind),
                Message       = GetInstallNowMessage(mod.Name, result),
                SummaryLines  = summaryLines,
                FollowUpLines = result.FollowUpLines,
            };
        }

        private static ApplyChangesResult RewordRemoveNowResult(ModListItem mod,
                                                                ApplyChangesResult result)
        {
            var summaryLines = result.SummaryLines
                                     .Where(line => line.IndexOf("queued action",
                                                                 StringComparison.OrdinalIgnoreCase) < 0)
                                     .ToList();
            if (summaryLines.Count == 0)
            {
                summaryLines.Add("1 direct removal");
            }

            return new ApplyChangesResult
            {
                Kind          = result.Kind,
                Success       = result.Success,
                Title         = GetRemoveNowTitle(result.Kind),
                Message       = GetRemoveNowMessage(mod.Name, result),
                SummaryLines  = summaryLines,
                FollowUpLines = result.FollowUpLines,
            };
        }

        private static string GetInstallNowTitle(ApplyResultKind kind)
            => kind switch
            {
                ApplyResultKind.Success  => "Installed",
                ApplyResultKind.Warning  => "Installed with Follow-Up",
                ApplyResultKind.Blocked  => "Install Blocked",
                ApplyResultKind.Canceled => "Install Canceled",
                _                        => "Install Failed",
            };

        private static string GetInstallNowMessage(string moduleName,
                                                   ApplyChangesResult result)
        {
            if (result.Kind == ApplyResultKind.Success)
            {
                return $"Installed {moduleName}.";
            }

            if (result.Kind == ApplyResultKind.Warning)
            {
                return $"Installed {moduleName}. Review the follow-up items below.";
            }

            if (result.Kind == ApplyResultKind.Blocked)
            {
                const string prefix = "Cannot apply queued changes: ";
                return result.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? $"Could not install {moduleName}: {result.Message[prefix.Length..]}"
                    : $"Could not install {moduleName}: {result.Message}";
            }

            if (result.Kind == ApplyResultKind.Canceled)
            {
                return RewritePrefix(result.Message, "Apply canceled", "Install canceled");
            }

            return RewritePrefix(result.Message, "Apply failed", "Install failed");
        }

        private static string GetRemoveNowTitle(ApplyResultKind kind)
            => kind switch
            {
                ApplyResultKind.Success  => "Removed",
                ApplyResultKind.Warning  => "Removed with Follow-Up",
                ApplyResultKind.Blocked  => "Removal Blocked",
                ApplyResultKind.Canceled => "Removal Canceled",
                _                        => "Removal Failed",
            };

        private static string GetRemoveNowMessage(string moduleName,
                                                  ApplyChangesResult result)
        {
            if (result.Kind == ApplyResultKind.Success)
            {
                return $"Removed {moduleName}.";
            }

            if (result.Kind == ApplyResultKind.Warning)
            {
                return $"Removed {moduleName}. Review the follow-up items below.";
            }

            if (result.Kind == ApplyResultKind.Blocked)
            {
                const string prefix = "Cannot apply queued changes: ";
                return result.Message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? $"Could not remove {moduleName}: {result.Message[prefix.Length..]}"
                    : $"Could not remove {moduleName}: {result.Message}";
            }

            if (result.Kind == ApplyResultKind.Canceled)
            {
                return RewritePrefix(result.Message, "Apply canceled", "Removal canceled");
            }

            return RewritePrefix(result.Message, "Apply failed", "Removal failed");
        }

        private static string RewritePrefix(string message,
                                            string sourcePrefix,
                                            string targetPrefix)
            => message.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
                ? $"{targetPrefix}{message[sourcePrefix.Length..]}"
                : message;

        private static IReadOnlyList<string> BuildSummaryLines(ExecutionPlan plan)
        {
            var lines = new List<string>
            {
                $"{plan.RequestedActions.Count} queued action{Pluralize(plan.RequestedActions.Count)}",
            };

            if (plan.RequestedDownloads.Count > 0)
            {
                lines.Add($"{plan.RequestedDownloads.Count} direct download{Pluralize(plan.RequestedDownloads.Count)}");
            }
            if (plan.RequestedInstalls.Count > 0)
            {
                lines.Add($"{plan.RequestedInstalls.Count} direct install{Pluralize(plan.RequestedInstalls.Count)}");
            }
            if (plan.RequestedUpdates.Count > 0)
            {
                lines.Add($"{plan.RequestedUpdates.Count} direct update{Pluralize(plan.RequestedUpdates.Count)}");
            }
            if (plan.RequestedRemovals.Count > 0)
            {
                lines.Add($"{plan.RequestedRemovals.Count} removal{Pluralize(plan.RequestedRemovals.Count)}");
            }
            if (plan.DependencyInstalls.Count > 0)
            {
                lines.Add($"{plan.DependencyInstalls.Count} dependency install{Pluralize(plan.DependencyInstalls.Count)}");
            }
            if (plan.DownloadsRequired.Count > 0)
            {
                lines.Add($"{plan.DownloadsRequired.Count} download{Pluralize(plan.DownloadsRequired.Count)} required");
            }
            if (plan.AutoRemovals.Count > 0)
            {
                lines.Add($"{plan.AutoRemovals.Count} auto-removal{Pluralize(plan.AutoRemovals.Count)}");
            }

            return lines;
        }

        private sealed class ExecutionPlan
        {
            public GameInstance Instance { get; init; } = null!;

            public RegistryManager? RegistryManager { get; init; }

            public IReadOnlyList<QueuedActionModel> RequestedActions { get; init; }
                = Array.Empty<QueuedActionModel>();

            public IReadOnlyList<CkanModule> RequestedInstalls { get; init; }
                = Array.Empty<CkanModule>();

            public IReadOnlyList<CkanModule> RequestedDownloads { get; init; }
                = Array.Empty<CkanModule>();

            public IReadOnlyList<CkanModule> RequestedUpdates { get; init; }
                = Array.Empty<CkanModule>();

            public IReadOnlyList<InstalledModule> RequestedRemovals { get; init; }
                = Array.Empty<InstalledModule>();

            public RelationshipResolverOptions? InstallOptions { get; init; }

            public string SummaryText { get; init; } = "";

            public bool CanApply { get; init; }

            public IReadOnlyList<string> DownloadsRequired { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> DependencyInstalls { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> AutoRemovals { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> AttentionNotes { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();
        }
    }
}
