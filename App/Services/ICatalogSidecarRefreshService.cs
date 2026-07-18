using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CKAN.App.Services
{
    public interface ICatalogSidecarRefreshService
    {
        Task<CatalogSidecarRefreshResult> RefreshIfNeededAsync(
            IReadOnlyList<string> repositoryCachePaths,
            CancellationToken     cancellationToken);
    }

    public enum CatalogSidecarRefreshStatus
    {
        Current,
        Updated,
        Unavailable,
        Failed,
        Skipped,
    }

    public sealed class CatalogSidecarRefreshResult
    {
        public CatalogSidecarRefreshResult(CatalogSidecarRefreshStatus status,
                                           string?                     message = null)
        {
            Status  = status;
            Message = message;
        }

        public CatalogSidecarRefreshStatus Status { get; }
        public string? Message { get; }
    }
}
