using System;
using System.Collections.Generic;
using System.Linq;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public sealed class ChangesetService : IChangesetService
    {
        private readonly Dictionary<string, QueuedActionModel> queue
            = new Dictionary<string, QueuedActionModel>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<QueuedActionModel> CurrentQueue
            => queue.Values
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Identifier, StringComparer.OrdinalIgnoreCase)
                    .ToList();

        public event Action? QueueChanged;

        public QueuedActionModel? FindQueuedAction(string identifier)
            => queue.TryGetValue(identifier, out var action)
                ? action
                : null;

        public void QueueInstall(ModListItem mod)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                ActionKind = QueuedActionKind.Install,
                ActionText = "Install",
                DetailText = string.IsNullOrWhiteSpace(mod.LatestVersion)
                    ? "Install latest available version"
                    : $"Install {mod.LatestVersion}",
            });

        public void QueueUpdate(ModListItem mod)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
                ActionKind = QueuedActionKind.Update,
                ActionText = "Update",
                DetailText = string.IsNullOrWhiteSpace(mod.InstalledVersion)
                    ? $"Update to {mod.LatestVersion}"
                    : $"{mod.InstalledVersion} -> {mod.LatestVersion}",
            });

        public void QueueRemove(ModListItem mod)
            => Upsert(new QueuedActionModel
            {
                Identifier = mod.Identifier,
                Name       = mod.Name,
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

        private void Upsert(QueuedActionModel action)
        {
            queue[action.Identifier] = action;
            QueueChanged?.Invoke();
        }
    }
}
