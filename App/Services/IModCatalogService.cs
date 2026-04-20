using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModCatalogService
    {
        Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                         CancellationToken cancellationToken);

        Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                  CancellationToken cancellationToken);
    }
}
