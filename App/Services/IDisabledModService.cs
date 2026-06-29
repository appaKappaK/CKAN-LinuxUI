using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IDisabledModService
    {
        DisabledModsSnapshot GetCurrentSnapshot();

        Task<DisabledModOperationPreview> PreviewDisableAsync(string identifier,
                                                              CancellationToken cancellationToken);

        Task<DisabledModOperationPreview> PreviewEnableAsync(string identifier,
                                                             CancellationToken cancellationToken);

        Task<ApplyChangesResult> DisableAsync(string identifier,
                                              CancellationToken cancellationToken);

        Task<ApplyChangesResult> EnableAsync(string identifier,
                                             CancellationToken cancellationToken);
    }
}
