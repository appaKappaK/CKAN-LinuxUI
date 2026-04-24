using System.Collections.Generic;

using CKAN.App.Models;

namespace CKAN.App.Services
{
    public interface IAppSettingsService
    {
        string SettingsPath { get; }

        string? LastInstanceName { get; }

        FilterState FilterState { get; }

        bool ShowAdvancedFilters { get; }

        AppWindowState WindowState { get; }

        int UiScalePercent { get; }

        bool PreselectRecommendedMods { get; }

        IReadOnlyList<CatalogSkeletonSnapshotRow> CatalogSkeletonRows { get; }

        void SaveLastInstanceName(string? instanceName);

        void SaveBrowserState(FilterState filterState,
                              bool        showAdvancedFilters);

        void SaveWindowState(AppWindowState windowState);

        void SaveUiScalePercent(int uiScalePercent);

        void SavePreselectRecommendedMods(bool preselectRecommendedMods);

        void SaveCatalogSkeletonRows(IReadOnlyList<CatalogSkeletonSnapshotRow> rows);
    }
}
