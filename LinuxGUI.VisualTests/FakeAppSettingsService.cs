using CKAN.App.Models;
using CKAN.App.Services;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class FakeAppSettingsService : IAppSettingsService
    {
        public string SettingsPath => "/tmp/ckan-linux-visual-settings.json";

        public string? LastInstanceName { get; private set; }

        public FilterState FilterState { get; private set; } = new FilterState();

        public bool ShowAdvancedFilters { get; private set; }

        public AppWindowState WindowState { get; private set; } = new AppWindowState();

        public int UiScalePercent { get; private set; } = UiScaleSettings.DefaultPercent;

        public void SaveLastInstanceName(string? instanceName)
            => LastInstanceName = instanceName;

        public void SaveBrowserState(FilterState filterState,
                                     bool        showAdvancedFilters)
        {
            FilterState = filterState ?? new FilterState();
            ShowAdvancedFilters = showAdvancedFilters;
        }

        public void SaveWindowState(AppWindowState windowState)
            => WindowState = windowState?.Clone() ?? new AppWindowState();

        public void SaveUiScalePercent(int uiScalePercent)
            => UiScalePercent = UiScaleSettings.NormalizePercent(uiScalePercent);
    }
}
