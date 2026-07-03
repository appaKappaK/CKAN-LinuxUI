using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using CKAN.App.Models;
using CKAN.IO;

namespace CKAN.App.Services
{
    public sealed class DisabledModService : IDisabledModService
    {
        private const string ManifestFileName = "disabled-mods.json";
        private const string DefaultDisabledDirectoryName = "DISABLED";

        private readonly IGameInstanceService gameInstanceService;

        public DisabledModService(IGameInstanceService gameInstanceService)
            => this.gameInstanceService = gameInstanceService;

        public DisabledModsSnapshot GetCurrentSnapshot()
        {
            if (gameInstanceService.CurrentInstance is not GameInstance instance)
            {
                return new DisabledModsSnapshot();
            }

            var manifest = LoadManifest(instance);
            return SnapshotFromManifest(instance, manifest);
        }

        public Task<DisabledModOperationPreview> PreviewDisableAsync(string              identifier,
                                                                     CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                var plan = BuildDisablePlan(identifier, cancellationToken);
                return new DisabledModOperationPreview
                {
                    CanApply = plan.CanApply,
                    Title = plan.Title,
                    Message = plan.Message,
                    SummaryLines = plan.SummaryLines,
                    FollowUpLines = plan.FollowUpLines,
                };
            }, cancellationToken);

        public Task<DisabledModOperationPreview> PreviewEnableAsync(string              identifier,
                                                                    CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                var plan = BuildEnablePlan(identifier, cancellationToken);
                return new DisabledModOperationPreview
                {
                    CanApply = plan.CanApply,
                    Title = plan.Title,
                    Message = plan.Message,
                    SummaryLines = plan.SummaryLines,
                    FollowUpLines = plan.FollowUpLines,
                };
            }, cancellationToken);

        public Task<ApplyChangesResult> DisableAsync(string              identifier,
                                                     CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                var plan = BuildDisablePlan(identifier, cancellationToken);
                if (!plan.CanApply)
                {
                    return ToBlockedResult(plan);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(plan.DisabledDirectoryPath!);
                foreach (var module in plan.Modules)
                {
                    MoveModuleToDisabledStorage(plan.Instance!,
                                                module,
                                                plan.DisabledDirectoryPath!,
                                                plan.ManifestEntries[module.identifier].StorageDirectory,
                                                cancellationToken);
                }

                var manifest = LoadManifest(plan.Instance!);
                manifest.DisabledDirectoryName = Path.GetFileName(plan.DisabledDirectoryPath!);
                foreach (var entry in plan.ManifestEntries.Values)
                {
                    UpsertManifestEntry(manifest, entry);
                }
                SaveManifest(plan.Instance!, manifest);

                return new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Success,
                    Success = true,
                    Title = "Disabled",
                    Message = $"Disabled {plan.RootName}.",
                    SummaryLines = plan.SummaryLines,
                    FollowUpLines = plan.FollowUpLines,
                };
            }, cancellationToken);

        public Task<ApplyChangesResult> EnableAsync(string              identifier,
                                                    CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                var plan = BuildEnablePlan(identifier, cancellationToken);
                if (!plan.CanApply)
                {
                    return ToBlockedResult(plan);
                }

                cancellationToken.ThrowIfCancellationRequested();
                foreach (var module in plan.Modules)
                {
                    var entry = plan.DisabledEntries[module.identifier];
                    RestoreModuleFromDisabledStorage(plan.Instance!,
                                                     plan.DisabledDirectoryPath!,
                                                     entry.StorageDirectory,
                                                     entry.RelativeFiles,
                                                     cancellationToken);
                }

                var manifest = LoadManifest(plan.Instance!);
                manifest.Modules.RemoveAll(module => plan.DisabledEntries.ContainsKey(module.Identifier));
                SaveManifest(plan.Instance!, manifest);

                return new ApplyChangesResult
                {
                    Kind = ApplyResultKind.Success,
                    Success = true,
                    Title = "Enabled",
                    Message = $"Enabled {plan.RootName}.",
                    SummaryLines = plan.SummaryLines,
                    FollowUpLines = plan.FollowUpLines,
                };
            }, cancellationToken);

        private DisablePlan BuildDisablePlan(string              identifier,
                                             CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetCurrentContext(out GameInstance? instance, out Registry? registry))
            {
                return DisablePlan.Blocked("Disable Unavailable",
                                           "Select or reload a game instance before disabling mods.");
            }

            var snapshot = GetCurrentSnapshot();
            if (!TryResolveManagedInstalledModule(registry!, identifier, out InstalledModule? root, out string? error))
            {
                return DisablePlan.Blocked("Disable Unavailable", error ?? "That mod cannot be disabled.");
            }

            if (snapshot.IsDisabled(identifier))
            {
                return DisablePlan.Blocked("Already Disabled",
                                           $"{root!.Module.name ?? identifier} is already disabled.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.DisabledDirectoryPath))
            {
                return DisablePlan.Blocked("Disable Unavailable",
                                           "No uppercase DISABLED folder was found in the current game directory.");
            }

            var activeRegistry = CreateActiveRegistry(registry!, snapshot.Entries.Keys);
            var removalIds = activeRegistry.FindReverseDependencies(new[] { identifier })
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!removalIds.Contains(identifier))
            {
                removalIds.Add(identifier);
            }

            var modules = removalIds.Select(registry!.InstalledModule)
                                    .OfType<InstalledModule>()
                                    .OrderBy(module => string.Equals(module.identifier,
                                                                     identifier,
                                                                     StringComparison.OrdinalIgnoreCase)
                                                       ? 0
                                                       : 1)
                                    .ThenBy(module => module.Module.name ?? module.identifier,
                                            StringComparer.CurrentCultureIgnoreCase)
                                    .ToList();
            var manifestEntries = modules.ToDictionary(module => module.identifier,
                                                       CreateManifestEntry,
                                                       StringComparer.OrdinalIgnoreCase);

            var collisions = FindStorageCollisions(snapshot.DisabledDirectoryPath!,
                                                  manifestEntries.Values);
            if (collisions.Count > 0)
            {
                return DisablePlan.Blocked("Disable Blocked",
                                           $"Cannot disable {root!.Module.name ?? identifier} because the disabled storage already contains files for one of the selected mods.",
                                           BuildDisableSummaryLines(modules.Count),
                                           collisions);
            }

            return new DisablePlan
            {
                CanApply = true,
                Instance = instance,
                Registry = registry,
                RootName = root!.Module.name ?? identifier,
                DisabledDirectoryPath = snapshot.DisabledDirectoryPath,
                Modules = modules,
                ManifestEntries = manifestEntries,
                Title = "Disable Mod",
                Message = $"Disable {root.Module.name ?? identifier} and move its managed files into the disabled mods folder?",
                SummaryLines = BuildDisableSummaryLines(modules.Count),
                FollowUpLines = BuildDisableFollowUpLines(root.identifier, modules),
            };
        }

        private EnablePlan BuildEnablePlan(string              identifier,
                                           CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetCurrentContext(out GameInstance? instance, out Registry? registry))
            {
                return EnablePlan.Blocked("Enable Unavailable",
                                          "Select or reload a game instance before enabling mods.");
            }

            var snapshot = GetCurrentSnapshot();
            if (!snapshot.IsDisabled(identifier))
            {
                return EnablePlan.Blocked("Enable Unavailable",
                                          "That mod is not currently disabled.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.DisabledDirectoryPath))
            {
                return EnablePlan.Blocked("Enable Unavailable",
                                          "The disabled mods folder could not be found for this instance.");
            }

            var currentInstance = instance!;
            var currentRegistry = registry!;
            if (!TryResolveDisabledModule(currentRegistry,
                                          currentInstance,
                                          snapshot,
                                          identifier,
                                          out InstalledModule? rootModule))
            {
                return EnablePlan.Blocked("Enable Unavailable",
                                          "CKAN no longer has an installed record for that disabled mod.");
            }

            var activeRegistry = CreateActiveRegistry(currentRegistry, snapshot.Entries.Keys);
            var activeModules = activeRegistry.InstalledModules.Select(module => module.Module)
                                               .ToList();
            var disabledModules = snapshot.Entries.Keys
                                         .Select(key => TryResolveDisabledModule(currentRegistry,
                                                                                 currentInstance,
                                                                                 snapshot,
                                                                                 key,
                                                                                 out InstalledModule? module)
                                             ? module
                                             : null)
                                         .OfType<InstalledModule>()
                                         .ToDictionary(module => module.identifier,
                                                       module => module,
                                                       StringComparer.OrdinalIgnoreCase);

            var planned = new Dictionary<string, InstalledModule>(StringComparer.OrdinalIgnoreCase)
            {
                [rootModule!.identifier] = rootModule,
            };
            var missing = new List<string>();

            bool added;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                added = false;
                var combined = activeModules.Concat(planned.Values.Select(module => module.Module))
                                            .ToList();

                foreach (var unmet in SanityChecker.FindUnsatisfiedDepends(combined,
                                                                          activeRegistry.InstalledDlls,
                                                                          activeRegistry.InstalledDlc))
                {
                    if (TryResolveDisabledDependency(unmet.Item2,
                                                     disabledModules.Values,
                                                     planned,
                                                     out InstalledModule? dependency))
                    {
                        planned[dependency!.identifier] = dependency;
                        added = true;
                    }
                    else
                    {
                        missing.Add($"{unmet.Item1.name ?? unmet.Item1.identifier} requires {unmet.Item2}.");
                    }
                }
            }
            while (added);

            var followUpLines = BuildEnableFollowUpLines(rootModule.identifier, planned.Values).ToList();
            if (missing.Count > 0)
            {
                followUpLines.AddRange(missing.Distinct().OrderBy(line => line, StringComparer.CurrentCultureIgnoreCase));
                return EnablePlan.Blocked("Enable Blocked",
                                          $"Cannot enable {rootModule.Module.name ?? identifier} until its required dependencies are active again.",
                                          BuildEnableSummaryLines(planned.Count),
                                          followUpLines);
            }

            var resolver = new RelationshipResolver(planned.Values.Select(module => module.Module).ToList(),
                                                    Array.Empty<CkanModule>(),
                                                    RelationshipResolverOptions.ConflictsOpts(currentInstance.StabilityToleranceConfig),
                                                    activeRegistry,
                                                    currentInstance.Game,
                                                    currentInstance.VersionCriteria());
            var conflicts = resolver.ConflictDescriptions.Distinct()
                                  .OrderBy(line => line, StringComparer.CurrentCultureIgnoreCase)
                                  .ToList();
            if (conflicts.Count > 0)
            {
                followUpLines.AddRange(conflicts);
                return EnablePlan.Blocked("Enable Blocked",
                                          $"Cannot enable {rootModule.Module.name ?? identifier} without creating conflicts.",
                                          BuildEnableSummaryLines(planned.Count),
                                          followUpLines);
            }

            var disabledEntries = planned.Keys.ToDictionary(key => key,
                                                            key => snapshot.Entries[key],
                                                            StringComparer.OrdinalIgnoreCase);
            var restoreCollisions = FindRestoreCollisions(currentInstance,
                                                          snapshot.DisabledDirectoryPath!,
                                                          disabledEntries.Values);
            if (restoreCollisions.Count > 0)
            {
                followUpLines.AddRange(restoreCollisions);
                return EnablePlan.Blocked("Enable Blocked",
                                          $"Cannot enable {rootModule.Module.name ?? identifier} because some target files already exist in the game directory.",
                                          BuildEnableSummaryLines(planned.Count),
                                          followUpLines);
            }

            return new EnablePlan
            {
                CanApply = true,
                Instance = currentInstance,
                Registry = currentRegistry,
                RootName = rootModule.Module.name ?? identifier,
                DisabledDirectoryPath = snapshot.DisabledDirectoryPath,
                Modules = planned.Values.OrderBy(module => string.Equals(module.identifier,
                                                                         identifier,
                                                                         StringComparison.OrdinalIgnoreCase)
                                                           ? 0
                                                           : 1)
                                        .ThenBy(module => module.Module.name ?? module.identifier,
                                                StringComparer.CurrentCultureIgnoreCase)
                                        .ToList(),
                DisabledEntries = disabledEntries,
                Title = "Enable Mod",
                Message = $"Enable {rootModule.Module.name ?? identifier} and restore its managed files to the game directory?",
                SummaryLines = BuildEnableSummaryLines(planned.Count),
                FollowUpLines = followUpLines,
            };
        }

        private ApplyChangesResult ToBlockedResult(PlanBase plan)
            => new ApplyChangesResult
            {
                Kind = ApplyResultKind.Blocked,
                Success = false,
                Title = plan.Title,
                Message = plan.Message,
                SummaryLines = plan.SummaryLines,
                FollowUpLines = plan.FollowUpLines,
            };

        private bool TryGetCurrentContext(out GameInstance? instance,
                                          out Registry?     registry)
        {
            instance = gameInstanceService.CurrentInstance;
            registry = gameInstanceService.CurrentRegistry;
            return instance != null && registry != null;
        }

        private Registry CreateActiveRegistry(Registry registry,
                                              IEnumerable<string> disabledIdentifiers)
        {
            var disabledSet = disabledIdentifiers.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var installedModules = registry.InstalledModules
                                           .Where(module => !disabledSet.Contains(module.identifier))
                                           .ToDictionary(module => module.identifier,
                                                         module => module,
                                                         StringComparer.OrdinalIgnoreCase);
            var installedFiles = installedModules.Values
                                                 .SelectMany(module => module.Files.Select(file => new KeyValuePair<string, string>(file,
                                                                                                                                       module.identifier)))
                                                 .ToDictionary(Platform.PathComparer);
            var installedDlls = registry.InstalledDlls
                                        .Where(identifier => registry.DllPath(identifier) is string)
                                        .ToDictionary(identifier => identifier,
                                                      identifier => registry.DllPath(identifier)!,
                                                      StringComparer.OrdinalIgnoreCase);
            var repositories = new SortedDictionary<string, Repository>(registry.Repositories,
                                                                        StringComparer.OrdinalIgnoreCase);

            return new Registry(gameInstanceService.RepositoryData,
                                installedModules,
                                installedDlls,
                                installedFiles,
                                repositories);
        }

        private static bool TryResolveManagedInstalledModule(Registry registry,
                                                             string   identifier,
                                                             out InstalledModule? module,
                                                             out string?          error)
        {
            if (registry.InstalledModule(identifier) is not InstalledModule installed)
            {
                module = null;
                error = "Only CKAN-managed installed mods can be disabled.";
                return false;
            }

            if (registry.IsAutodetected(identifier))
            {
                module = null;
                error = $"{installed.Module.name ?? identifier} is managed outside CKAN and cannot be disabled automatically.";
                return false;
            }

            module = installed;
            error = null;
            return true;
        }

        private static IReadOnlyList<string> BuildDisableSummaryLines(int totalCount)
            => new[]
            {
                "1 requested disable",
                totalCount > 1
                    ? $"{totalCount - 1} dependent disable{(totalCount == 2 ? "" : "s")}"
                    : "0 dependent disables",
            };

        private static IReadOnlyList<string> BuildEnableSummaryLines(int totalCount)
            => new[]
            {
                "1 requested enable",
                totalCount > 1
                    ? $"{totalCount - 1} additional dependency enable{(totalCount == 2 ? "" : "s")}"
                    : "0 additional dependency enables",
            };

        private static IReadOnlyList<string> BuildDisableFollowUpLines(string                         rootIdentifier,
                                                                       IEnumerable<InstalledModule> modules)
            => modules.Where(module => !string.Equals(module.identifier,
                                                      rootIdentifier,
                                                      StringComparison.OrdinalIgnoreCase))
                      .Select(module => $"Also disables {FormatModule(module.Module)}.")
                      .ToList();

        private static IReadOnlyList<string> BuildEnableFollowUpLines(string                         rootIdentifier,
                                                                      IEnumerable<InstalledModule> modules)
            => modules.Where(module => !string.Equals(module.identifier,
                                                      rootIdentifier,
                                                      StringComparison.OrdinalIgnoreCase))
                      .Select(module => $"Also enables dependency {FormatModule(module.Module)}.")
                      .ToList();

        private static string FormatModule(CkanModule module)
            => $"{module.name ?? module.identifier} ({module.identifier} {module.version})";

        private static bool TryResolveDisabledDependency(RelationshipDescriptor                         relationship,
                                                         IEnumerable<InstalledModule>                  disabledModules,
                                                         IReadOnlyDictionary<string, InstalledModule>  planned,
                                                         out InstalledModule?                          dependency)
        {
            var remaining = disabledModules.Where(module => !planned.ContainsKey(module.identifier))
                                           .ToList();
            var candidates = remaining.Select(module => module.Module)
                                      .OrderByDescending(module => string.Equals(module.identifier,
                                                                                DirectDependencyIdentifier(relationship),
                                                                                StringComparison.OrdinalIgnoreCase))
                                      .ThenByDescending(module => module.version)
                                      .ToList();

            if (relationship.MatchesAny(candidates, null, null, out CkanModule? matched)
                && matched != null)
            {
                dependency = remaining.First(module => string.Equals(module.identifier,
                                                                     matched.identifier,
                                                                     StringComparison.OrdinalIgnoreCase));
                return true;
            }

            dependency = null;
            return false;
        }

        private static bool TryResolveDisabledModule(Registry             registry,
                                                     GameInstance         instance,
                                                     DisabledModsSnapshot snapshot,
                                                     string               identifier,
                                                     out InstalledModule? module)
        {
            if (registry.InstalledModule(identifier) is InstalledModule installed)
            {
                module = installed;
                return true;
            }

            if (!snapshot.Entries.TryGetValue(identifier, out DisabledModEntry? entry))
            {
                module = null;
                return false;
            }

            CkanModule? sourceModule = null;
            if (!string.IsNullOrWhiteSpace(entry.Version))
            {
                sourceModule = Utilities.DefaultIfThrows(() => registry.GetModuleByVersion(identifier, entry.Version));
            }

            sourceModule ??= Utilities.DefaultIfThrows(() => registry.LatestAvailable(identifier,
                                                                                      instance.StabilityToleranceConfig,
                                                                                      instance.VersionCriteria()));
            sourceModule ??= Utilities.DefaultIfThrows(() => registry.LatestAvailable(identifier,
                                                                                      instance.StabilityToleranceConfig,
                                                                                      null));

            if (sourceModule == null)
            {
                module = null;
                return false;
            }

            module = new InstalledModule(null, sourceModule, entry.RelativeFiles, false);
            return true;
        }

        private static string? DirectDependencyIdentifier(RelationshipDescriptor relationship)
            => relationship is ModuleRelationshipDescriptor moduleRelationship
                ? moduleRelationship.name
                : null;

        private static StoredDisabledModule CreateManifestEntry(InstalledModule module)
            => new StoredDisabledModule
            {
                Identifier = module.identifier,
                Name = module.Module.name ?? module.identifier,
                Version = module.Module.version.ToString(),
                StorageDirectory = Identifier.Sanitize(module.identifier),
                DisabledAtUtc = DateTime.UtcNow,
                RelativeFiles = NormalizeRelativeFiles(module.Files),
            };

        private static IReadOnlyList<string> FindStorageCollisions(string                            disabledDirectoryPath,
                                                                   IEnumerable<StoredDisabledModule> entries)
        {
            var collisions = new List<string>();
            foreach (var entry in entries)
            {
                string storageRoot = StorageRoot(disabledDirectoryPath, NormalizeStorageDirectory(entry));
                if (Directory.Exists(storageRoot)
                    && Directory.EnumerateFileSystemEntries(storageRoot).Any())
                {
                    collisions.Add($"Disabled storage already contains files for {entry.Identifier}: {storageRoot}");
                }
            }
            return collisions;
        }

        private static IReadOnlyList<string> FindRestoreCollisions(GameInstance                  instance,
                                                                   string                        disabledDirectoryPath,
                                                                   IEnumerable<DisabledModEntry> entries)
        {
            var collisions = new List<string>();
            foreach (var entry in entries)
            {
                string storageRoot = StorageRoot(disabledDirectoryPath, entry.StorageDirectory);
                foreach (var relPath in entry.RelativeFiles)
                {
                    string sourcePath = RelativePathToStorage(storageRoot, relPath);
                    if (!File.Exists(sourcePath))
                    {
                        continue;
                    }

                    string targetPath = instance.ToAbsoluteGameDir(relPath);
                    if (File.Exists(targetPath))
                    {
                        collisions.Add($"Game directory already contains {relPath}.");
                    }
                }
            }
            return collisions.Distinct().ToList();
        }

        private static void MoveModuleToDisabledStorage(GameInstance      instance,
                                                        InstalledModule   module,
                                                        string            disabledDirectoryPath,
                                                        string            storageDirectory,
                                                        CancellationToken cancellationToken)
        {
            string storageRoot = StorageRoot(disabledDirectoryPath, storageDirectory);
            Directory.CreateDirectory(storageRoot);

            var paths = module.Files.Select(relPath => new FileTarget(relPath,
                                                                      instance.ToAbsoluteGameDir(relPath),
                                                                      RelativePathToStorage(storageRoot, relPath)))
                                    .ToList();

            foreach (var file in paths.Where(path => File.Exists(path.SourcePath)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath) ?? storageRoot);
                File.Move(file.SourcePath, file.TargetPath, overwrite: false);
                RemoveEmptyParentDirectories(Path.GetDirectoryName(file.SourcePath),
                                             instance.GameDir);
            }

            foreach (var dir in paths.Where(path => Directory.Exists(path.SourcePath))
                                     .OrderByDescending(path => path.RelativePath.Count(ch => ch == '/'))
                                     .ThenByDescending(path => path.RelativePath.Length))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(dir.SourcePath))
                {
                    continue;
                }

                if (Directory.EnumerateFileSystemEntries(dir.SourcePath).Any())
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(dir.TargetPath) ?? storageRoot);
                Directory.Move(dir.SourcePath, dir.TargetPath);
                RemoveEmptyParentDirectories(Path.GetDirectoryName(dir.SourcePath),
                                             instance.GameDir);
            }
        }

        private static void RestoreModuleFromDisabledStorage(GameInstance          instance,
                                                             string                disabledDirectoryPath,
                                                             string                storageDirectory,
                                                             IReadOnlyList<string> relativeFiles,
                                                             CancellationToken     cancellationToken)
        {
            string storageRoot = StorageRoot(disabledDirectoryPath, storageDirectory);
            foreach (var relPath in relativeFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string sourcePath = RelativePathToStorage(storageRoot, relPath);
                string targetPath = instance.ToAbsoluteGameDir(relPath);

                if (File.Exists(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? instance.GameDir);
                    File.Move(sourcePath, targetPath, overwrite: false);
                }
                else if (Directory.Exists(sourcePath))
                {
                    Directory.CreateDirectory(targetPath);
                }
            }

            if (Directory.Exists(storageRoot))
            {
                DeleteEmptyDirectories(storageRoot);
                if (!Directory.EnumerateFileSystemEntries(storageRoot).Any())
                {
                    Directory.Delete(storageRoot);
                }
            }
        }

        private static void DeleteEmptyDirectories(string root)
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                               .OrderByDescending(path => path.Count(ch => ch == Path.DirectorySeparatorChar)))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
        }

        private static void RemoveEmptyParentDirectories(string? startDirectory,
                                                         string  stopAt)
        {
            string normalizedStopAt = CKANPathUtils.NormalizePath(stopAt);
            string? current = string.IsNullOrWhiteSpace(startDirectory)
                ? null
                : CKANPathUtils.NormalizePath(startDirectory);

            while (!string.IsNullOrWhiteSpace(current)
                   && current.StartsWith(normalizedStopAt, Platform.PathComparison)
                   && !string.Equals(current, normalizedStopAt, Platform.PathComparison))
            {
                if (!Directory.Exists(current)
                    || Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }

                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
                if (!string.IsNullOrWhiteSpace(current))
                {
                    current = CKANPathUtils.NormalizePath(current);
                }
            }
        }

        private static DisabledModsSnapshot SnapshotFromManifest(GameInstance            instance,
                                                                 StoredDisabledManifest manifest)
        {
            string? disabledDirectoryPath = ResolveDisabledDirectory(instance, manifest);
            return new DisabledModsSnapshot
            {
                DisabledDirectoryPath = disabledDirectoryPath,
                Entries = manifest.Modules
                                  .Where(module => !string.IsNullOrWhiteSpace(module.Identifier))
                                  .GroupBy(module => module.Identifier, StringComparer.OrdinalIgnoreCase)
                                  .Select(group => group.First())
                                  .ToDictionary(module => module.Identifier,
                                                module => new DisabledModEntry
                                                {
                                                    Identifier = module.Identifier,
                                                    Name = string.IsNullOrWhiteSpace(module.Name)
                                                        ? module.Identifier
                                                        : module.Name,
                                                    Version = module.Version ?? "",
                                                    StorageDirectory = NormalizeStorageDirectory(module),
                                                    DisabledAtUtc = module.DisabledAtUtc,
                                                    RelativeFiles = NormalizeRelativeFiles(module.RelativeFiles),
                                                },
                                                StringComparer.OrdinalIgnoreCase),
            };
        }

        private static StoredDisabledManifest LoadManifest(GameInstance instance)
        {
            try
            {
                string path = ManifestPath(instance);
                return File.Exists(path)
                    ? JsonConvert.DeserializeObject<StoredDisabledManifest>(File.ReadAllText(path))
                      ?? new StoredDisabledManifest()
                    : new StoredDisabledManifest();
            }
            catch
            {
                return new StoredDisabledManifest();
            }
        }

        private static void SaveManifest(GameInstance            instance,
                                         StoredDisabledManifest manifest)
        {
            string path = ManifestPath(instance);
            manifest.Modules = manifest.Modules
                                     .Where(module => !string.IsNullOrWhiteSpace(module.Identifier))
                                     .GroupBy(module => module.Identifier, StringComparer.OrdinalIgnoreCase)
                                     .Select(group => group.First())
                                     .OrderBy(module => module.Identifier, StringComparer.OrdinalIgnoreCase)
                                     .ToList();

            if (manifest.Modules.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? instance.CkanDir);
            File.WriteAllText(path,
                              JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }

        private static void UpsertManifestEntry(StoredDisabledManifest manifest,
                                                StoredDisabledModule   entry)
        {
            manifest.Modules.RemoveAll(module => string.Equals(module.Identifier,
                                                               entry.Identifier,
                                                               StringComparison.OrdinalIgnoreCase));
            manifest.Modules.Add(entry);
        }

        private static string ManifestPath(GameInstance instance)
            => Path.Combine(instance.CkanDir, ManifestFileName);

        private static string? ResolveDisabledDirectory(GameInstance            instance,
                                                        StoredDisabledManifest manifest)
        {
            string gameRoot = instance.GameDir;
            if (!Directory.Exists(gameRoot))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(manifest.DisabledDirectoryName))
            {
                string configured = Path.Combine(gameRoot, manifest.DisabledDirectoryName);
                if (Directory.Exists(configured))
                {
                    return configured;
                }
            }

            return Directory.EnumerateDirectories(gameRoot)
                            .FirstOrDefault(path => IsDisabledDirectoryName(Path.GetFileName(path)));
        }

        private static bool IsDisabledDirectoryName(string? name)
            => !string.IsNullOrWhiteSpace(name)
               && string.Equals(name, name.ToUpperInvariant(), StringComparison.Ordinal)
               && name.EndsWith("DISABLED", StringComparison.Ordinal);

        private static IReadOnlyList<string> NormalizeRelativeFiles(IEnumerable<string>? relativeFiles)
            => (relativeFiles ?? Array.Empty<string>())
               .Where(path => !string.IsNullOrWhiteSpace(path))
               .Select(CKANPathUtils.NormalizePath)
               .Distinct(Platform.PathComparer)
               .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
               .ToList();

        private static string NormalizeStorageDirectory(StoredDisabledModule module)
            => !string.IsNullOrWhiteSpace(module.StorageDirectory)
                ? module.StorageDirectory
                : Identifier.Sanitize(module.Identifier);

        private static string StorageRoot(string disabledDirectoryPath,
                                          string storageDirectory)
            => Path.Combine(disabledDirectoryPath, storageDirectory);

        private static string RelativePathToStorage(string storageRoot,
                                                    string relativePath)
            => Path.Combine(storageRoot,
                            relativePath.Replace('/', Path.DirectorySeparatorChar));

        private sealed class FileTarget
        {
            public FileTarget(string relativePath,
                              string sourcePath,
                              string targetPath)
            {
                RelativePath = relativePath;
                SourcePath = sourcePath;
                TargetPath = targetPath;
            }

            public string RelativePath { get; }

            public string SourcePath { get; }

            public string TargetPath { get; }
        }

        private sealed class StoredDisabledManifest
        {
            public int Version { get; set; } = 1;

            public string DisabledDirectoryName { get; set; } = DefaultDisabledDirectoryName;

            public List<StoredDisabledModule> Modules { get; set; } = new List<StoredDisabledModule>();
        }

        private sealed class StoredDisabledModule
        {
            public string Identifier { get; set; } = "";

            public string Name { get; set; } = "";

            public string Version { get; set; } = "";

            public string StorageDirectory { get; set; } = "";

            public DateTime? DisabledAtUtc { get; set; }

            public IReadOnlyList<string> RelativeFiles { get; set; } = Array.Empty<string>();
        }

        private abstract class PlanBase
        {
            public bool CanApply { get; init; }

            public GameInstance? Instance { get; init; }

            public Registry? Registry { get; init; }

            public string RootName { get; init; } = "";

            public string? DisabledDirectoryPath { get; init; }

            public IReadOnlyList<InstalledModule> Modules { get; init; } = Array.Empty<InstalledModule>();

            public string Title { get; init; } = "";

            public string Message { get; init; } = "";

            public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();

            public IReadOnlyList<string> FollowUpLines { get; init; } = Array.Empty<string>();
        }

        private sealed class DisablePlan : PlanBase
        {
            public IReadOnlyDictionary<string, StoredDisabledModule> ManifestEntries { get; init; }
                = new Dictionary<string, StoredDisabledModule>(StringComparer.OrdinalIgnoreCase);

            public static DisablePlan Blocked(string              title,
                                              string              message,
                                              IReadOnlyList<string>? summaryLines = null,
                                              IReadOnlyList<string>? followUpLines = null)
                => new DisablePlan
                {
                    CanApply = false,
                    Title = title,
                    Message = message,
                    SummaryLines = summaryLines ?? Array.Empty<string>(),
                    FollowUpLines = followUpLines ?? Array.Empty<string>(),
                };
        }

        private sealed class EnablePlan : PlanBase
        {
            public IReadOnlyDictionary<string, DisabledModEntry> DisabledEntries { get; init; }
                = new Dictionary<string, DisabledModEntry>(StringComparer.OrdinalIgnoreCase);

            public static EnablePlan Blocked(string              title,
                                             string              message,
                                             IReadOnlyList<string>? summaryLines = null,
                                             IReadOnlyList<string>? followUpLines = null)
                => new EnablePlan
                {
                    CanApply = false,
                    Title = title,
                    Message = message,
                    SummaryLines = summaryLines ?? Array.Empty<string>(),
                    FollowUpLines = followUpLines ?? Array.Empty<string>(),
                };
        }
    }
}
