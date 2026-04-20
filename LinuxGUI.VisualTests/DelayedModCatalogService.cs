using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class DelayedModCatalogService : IModCatalogService
    {
        private readonly IModCatalogService inner;
        private readonly int listDelayMs;
        private readonly int detailsDelayMs;

        public DelayedModCatalogService(int listDelayMs = 0,
                                        int detailsDelayMs = 0,
                                        IModCatalogService? inner = null)
        {
            this.listDelayMs = listDelayMs;
            this.detailsDelayMs = detailsDelayMs;
            this.inner = inner ?? new FakeModCatalogService();
        }

        public async Task<IReadOnlyList<ModListItem>> GetModListAsync(FilterState filter,
                                                                      CancellationToken cancellationToken)
        {
            if (listDelayMs > 0)
            {
                await Task.Delay(listDelayMs, cancellationToken);
            }

            return await inner.GetModListAsync(filter, cancellationToken);
        }

        public async Task<ModDetailsModel?> GetModDetailsAsync(string identifier,
                                                               CancellationToken cancellationToken)
        {
            if (detailsDelayMs > 0)
            {
                await Task.Delay(detailsDelayMs, cancellationToken);
            }

            return await inner.GetModDetailsAsync(identifier, cancellationToken);
        }
    }
}
