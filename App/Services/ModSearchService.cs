using CKAN.App.Models;

namespace CKAN.App.Services
{
    public sealed class ModSearchService : IModSearchService
    {
        public FilterState Current { get; private set; } = new FilterState();

        public void SetCurrent(FilterState current)
            => Current = current;
    }
}
