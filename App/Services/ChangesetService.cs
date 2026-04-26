using System;
using System.Collections.Generic;
using System.Linq;

using CKAN.App.Models;
using CKAN.Versioning;

namespace CKAN.App.Services
{
    public sealed class ChangesetService : IChangesetService
    {
        private readonly Dictionary<string, QueuedActionModel> queue
            = new Dictionary<string, QueuedActionModel>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<QueuedActionModel> CurrentQueue
            => SortedQueue();

        public IReadOnlyList<QueuedActionModel> CurrentApplyQueue
            => SortedQueue(item => item.ActionKind != QueuedActionKind.Download);

        public IReadOnlyList<QueuedActionModel> CurrentDownloadQueue
            => SortedQueue(item => item.ActionKind == QueuedActionKind.Download);

        public event Action? QueueChanged;

        public QueuedActionModel? FindQueuedAction(string identifier)
            => queue.TryGetValue(identifier, out var action)
                ? action
                : null;

        public QueuedActionModel? FindQueuedApplyAction(string identifier)
            => FindQueued(identifier, item => item.ActionKind != QueuedActionKind.Download);

        public QueuedActionModel? FindQueuedDownloadAction(string identifier)
            => FindQueued(identifier, item => item.ActionKind == QueuedActionKind.Download);

        public void QueueDownload(ModListItem mod)
        {
            var targetVersion = QueueTargetVersion(mod, null);
            Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = targetVersion,
                ActionKind = QueuedActionKind.Download,
                ActionText = "Add to Cache",
                DetailText = mod.IsInstalled
                    ? string.IsNullOrWhiteSpace(targetVersion)
                        ? "Cache the latest available version locally"
                        : $"Cache {targetVersion} locally"
                    : string.IsNullOrWhiteSpace(targetVersion)
                        ? "Cache latest available version for later install"
                        : $"Cache {targetVersion} for later install",
            });
        }

        public void QueueInstall(ModListItem mod, string? targetVersion = null, string? sourceText = null)
        {
            var resolvedTargetVersion = QueueTargetVersion(mod, targetVersion);
            Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = resolvedTargetVersion,
                ActionKind = QueuedActionKind.Install,
                ActionText = "Install",
                DetailText = string.IsNullOrWhiteSpace(resolvedTargetVersion)
                    ? "Install latest available version"
                    : $"Install {resolvedTargetVersion}",
                SourceText = sourceText?.Trim() ?? "",
            });
        }

        public void QueueUpdate(ModListItem mod, string? targetVersion = null)
        {
            var resolvedTargetVersion = QueueTargetVersion(mod, targetVersion);
            Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = resolvedTargetVersion,
                ActionKind = QueuedActionKind.Update,
                ActionText = UpdateActionText(mod.InstalledVersion, resolvedTargetVersion),
                DetailText = string.IsNullOrWhiteSpace(resolvedTargetVersion)
                    ? string.IsNullOrWhiteSpace(mod.InstalledVersion)
                        ? $"Update to {mod.LatestVersion}"
                        : $"{mod.InstalledVersion} -> {mod.LatestVersion}"
                    : string.IsNullOrWhiteSpace(mod.InstalledVersion)
                        ? $"Switch to {resolvedTargetVersion}"
                        : $"{mod.InstalledVersion} -> {resolvedTargetVersion}",
            });
        }

        public void QueueRemove(ModListItem mod)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = "",
                ActionKind = QueuedActionKind.Remove,
                ActionText = "Remove",
                DetailText = string.IsNullOrWhiteSpace(mod.InstalledVersion)
                    ? "Remove installed module"
                    : $"Remove {mod.InstalledVersion}",
            });

        public void Restore(IReadOnlyList<QueuedActionModel> actions)
        {
            queue.Clear();
            foreach (var action in actions ?? Array.Empty<QueuedActionModel>())
            {
                if (string.IsNullOrWhiteSpace(action.Identifier))
                {
                    continue;
                }

                queue[action.Identifier] = action;
            }

            QueueChanged?.Invoke();
        }

        public bool Remove(string identifier)
        {
            var removed = queue.Remove(identifier);
            if (removed)
            {
                QueueChanged?.Invoke();
            }
            return removed;
        }

        public void Clear()
        {
            if (queue.Count == 0)
            {
                return;
            }

            queue.Clear();
            QueueChanged?.Invoke();
        }

        public void ClearApplyQueue()
            => ClearWhere(item => item.ActionKind != QueuedActionKind.Download);

        public void ClearDownloadQueue()
            => ClearWhere(item => item.ActionKind == QueuedActionKind.Download);

        private static string UpdateActionText(string installedVersion,
                                               string? targetVersion)
        {
            if (string.IsNullOrWhiteSpace(targetVersion)
                || string.IsNullOrWhiteSpace(installedVersion))
            {
                return "Update";
            }

            try
            {
                var comparison = new ModuleVersion(targetVersion!).CompareTo(new ModuleVersion(installedVersion));
                return comparison switch
                {
                    < 0 => "Downgrade",
                    > 0 => "Update",
                    _ => "Change Version",
                };
            }
            catch
            {
                return "Change Version";
            }
        }

        private static string QueueTargetVersion(ModListItem mod,
                                                 string?     targetVersion)
            => !string.IsNullOrWhiteSpace(targetVersion)
                ? targetVersion.Trim()
                : mod.LatestVersion?.Trim() ?? "";

        private void Upsert(QueuedActionModel action)
        {
            queue[action.Identifier] = action;
            QueueChanged?.Invoke();
        }

        private IReadOnlyList<QueuedActionModel> SortedQueue(Func<QueuedActionModel, bool>? predicate = null)
            => queue.Values
                    .Where(item => predicate == null || predicate(item))
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                    .ToList();

        private QueuedActionModel? FindQueued(string identifier,
                                              Func<QueuedActionModel, bool> predicate)
            => queue.TryGetValue(identifier, out var action) && predicate(action)
                ? action
                : null;

        private void ClearWhere(Func<QueuedActionModel, bool> predicate)
        {
            var identifiers = queue.Values
                                   .Where(predicate)
                                   .Select(item => item.Identifier)
                                   .ToList();
            if (identifiers.Count == 0)
            {
                return;
            }

            foreach (var identifier in identifiers)
            {
                queue.Remove(identifier);
            }
            QueueChanged?.Invoke();
        }
    }
}
