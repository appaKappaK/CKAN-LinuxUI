using CKAN.App.Models;

namespace CKAN.App.Services
{
    public sealed class ModSearchService : IModSearchService
    {
        public ModSearchService(IAppSettingsService appSettingsService)
        {
            Current = DefaultStartupFilter(appSettingsService.FilterState);
            ShowAdvancedFilters = false;
        }

        public FilterState Current { get; private set; }

        public bool ShowAdvancedFilters { get; private set; }

        public void SetCurrent(FilterState current)
            => Current = current;

        public void SetShowAdvancedFilters(bool showAdvancedFilters)
            => ShowAdvancedFilters = showAdvancedFilters;

        private static FilterState DefaultStartupFilter(FilterState filter)
            => IsClearAllFilter(filter)
                ? filter with
                {
                    InstalledOnly    = true,
                    NotInstalledOnly = false,
                }
                : filter;

        private static bool IsClearAllFilter(FilterState filter)
            => string.IsNullOrWhiteSpace(filter.SearchText)
               && string.IsNullOrWhiteSpace(filter.NameText)
               && string.IsNullOrWhiteSpace(filter.IdentifierText)
               && string.IsNullOrWhiteSpace(filter.AuthorText)
               && string.IsNullOrWhiteSpace(filter.SummaryText)
               && string.IsNullOrWhiteSpace(filter.DescriptionText)
               && string.IsNullOrWhiteSpace(filter.LicenseText)
               && string.IsNullOrWhiteSpace(filter.LanguageText)
               && string.IsNullOrWhiteSpace(filter.DependsText)
               && string.IsNullOrWhiteSpace(filter.RecommendsText)
               && string.IsNullOrWhiteSpace(filter.SuggestsText)
               && string.IsNullOrWhiteSpace(filter.ConflictsText)
               && string.IsNullOrWhiteSpace(filter.SupportsText)
               && string.IsNullOrWhiteSpace(filter.TagText)
               && string.IsNullOrWhiteSpace(filter.LabelText)
               && string.IsNullOrWhiteSpace(filter.CompatibilityText)
               && !filter.InstalledOnly
               && !filter.NotInstalledOnly
               && !filter.UpdatableOnly
               && !filter.NotUpdatableOnly
               && !filter.NewOnly
               && !filter.CompatibleOnly
               && !filter.CachedOnly
               && !filter.UncachedOnly
               && !filter.IncompatibleOnly
               && !filter.HasReplacementOnly
               && !filter.NoReplacementOnly;
    }
}
