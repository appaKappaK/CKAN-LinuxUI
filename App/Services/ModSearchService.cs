using CKAN.App.Models;

namespace CKAN.App.Services
{
    public sealed class ModSearchService : IModSearchService
    {
        public ModSearchService(IAppSettingsService appSettingsService)
        {
            Current = appSettingsService.FilterState;
            ShowAdvancedFilters = false;
        }

        public FilterState Current { get; private set; }

        public bool ShowAdvancedFilters { get; private set; }

        public void SetCurrent(FilterState current)
            => Current = current;

        public void SetShowAdvancedFilters(bool showAdvancedFilters)
            => ShowAdvancedFilters = showAdvancedFilters;
    }
}
