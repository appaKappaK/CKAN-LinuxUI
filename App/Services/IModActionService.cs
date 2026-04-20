using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModActionService
    {
        Task<ApplyChangesResult> ApplyChangesAsync(CancellationToken cancellationToken);
    }
}
