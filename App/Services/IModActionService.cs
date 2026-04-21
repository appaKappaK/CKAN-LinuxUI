using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModActionService
    {
        Task<ChangesetPreviewModel> PreviewChangesAsync(CancellationToken cancellationToken);

        Task<ApplyChangesResult> ApplyChangesAsync(CancellationToken cancellationToken);

        Task<ApplyChangesResult> InstallNowAsync(ModListItem mod, CancellationToken cancellationToken);

        Task<ApplyChangesResult> RemoveNowAsync(ModListItem mod, CancellationToken cancellationToken);

        Task<ApplyChangesResult> DownloadQueuedAsync(CancellationToken cancellationToken);
    }
}
