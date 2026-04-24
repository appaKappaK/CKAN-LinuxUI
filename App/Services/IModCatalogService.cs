using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModCatalogService
    {
        Task<IReadOnlyList<ModListItem>> GetAllModListAsync(CancellationToken cancellationToken)
            => GetModListAsync(new FilterState { InstalledOnly = false }, cancellationToken);

        Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                         CancellationToken cancellationToken);

        Task<FilterOptionCounts> GetFilterOptionCountsAsync(FilterState filter,
                                                            CancellationToken cancellationToken);

        IReadOnlyList<ModListItem> ApplyFilter(IReadOnlyList<ModListItem> items,
                                               FilterState                 filter)
            => items;

        FilterOptionCounts GetFilterOptionCounts(IReadOnlyCollection<ModListItem> items,
                                                 FilterState                       filter)
            => new FilterOptionCounts();

        Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                  CancellationToken cancellationToken);
    }
}
