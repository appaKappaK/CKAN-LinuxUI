using System.Collections.Generic;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IChangesetService
    {
        IReadOnlyList<QueuedActionModel> CurrentQueue { get; }

        IReadOnlyList<QueuedActionModel> CurrentApplyQueue { get; }

        IReadOnlyList<QueuedActionModel> CurrentDownloadQueue { get; }

        event System.Action? QueueChanged;

        QueuedActionModel? FindQueuedAction(string identifier);

        QueuedActionModel? FindQueuedApplyAction(string identifier);

        QueuedActionModel? FindQueuedDownloadAction(string identifier);

        void QueueDownload(ModListItem mod);

        void QueueInstall(ModListItem mod);

        void QueueUpdate(ModListItem mod);

        void QueueRemove(ModListItem mod);

        bool Remove(string identifier);

        void Clear();

        void ClearApplyQueue();

        void ClearDownloadQueue();
    }
}
