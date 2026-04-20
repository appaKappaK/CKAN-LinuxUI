using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IModSearchService
    {
        FilterState Current { get; }

        bool ShowAdvancedFilters { get; }

        void SetCurrent(FilterState current);

        void SetShowAdvancedFilters(bool showAdvancedFilters);
    }
}
