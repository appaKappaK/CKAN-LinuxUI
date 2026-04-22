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
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = "",
                ActionKind = QueuedActionKind.Download,
                ActionText = mod.IsInstalled
                    ? "Add to Cache"
                    : "Download Only",
                DetailText = mod.IsInstalled
                    ? string.IsNullOrWhiteSpace(mod.LatestVersion)
                        ? "Cache the latest available version locally"
                        : $"Cache {mod.LatestVersion} locally"
                    : string.IsNullOrWhiteSpace(mod.LatestVersion)
                        ? "Cache latest available version for later install"
                        : $"Cache {mod.LatestVersion} for later install",
            });

        public void QueueInstall(ModListItem mod, string? targetVersion = null)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = targetVersion ?? "",
                ActionKind = QueuedActionKind.Install,
                ActionText = "Install",
                DetailText = string.IsNullOrWhiteSpace(targetVersion)
                    ? string.IsNullOrWhiteSpace(mod.LatestVersion)
                        ? "Install latest available version"
                        : $"Install {mod.LatestVersion}"
                    : $"Install {targetVersion}",
            });

        public void QueueUpdate(ModListItem mod, string? targetVersion = null)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                TargetVersion = targetVersion ?? "",
                ActionKind = QueuedActionKind.Update,
                ActionText = UpdateActionText(mod.InstalledVersion, targetVersion),
                DetailText = string.IsNullOrWhiteSpace(targetVersion)
                    ? string.IsNullOrWhiteSpace(mod.InstalledVersion)
                        ? $"Update to {mod.LatestVersion}"
                        : $"{mod.InstalledVersion} -> {mod.LatestVersion}"
                    : string.IsNullOrWhiteSpace(mod.InstalledVersion)
                        ? $"Switch to {targetVersion}"
                        : $"{mod.InstalledVersion} -> {targetVersion}",
            });

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
                var comparison = new ModuleVersion(targetVersion).CompareTo(new ModuleVersion(installedVersion));
                return comparison switch
                {
                    < 0 => "Downgrade",
                    > 0 => "Update",
                    _ => "Switch Version",
                };
            }
            catch
            {
                return "Switch Version";
            }
        }

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
