using System;
using System.IO;

using Newtonsoft.Json;

using CKAN.App.Models;
using CKAN.IO;

namespace CKAN.App.Services
{
    public sealed class AppSettingsService : IAppSettingsService
    {
        private readonly object sync = new object();
        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting        = Formatting.Indented,
        };

        private StoredSettings settings;

        public AppSettingsService(string? settingsPath = null)
        {
            SettingsPath = settingsPath ?? Path.Combine(CKANPathUtils.AppDataPath, "linuxgui.settings.json");
            settings = LoadSettings();
        }

        public string SettingsPath { get; }

        public string? LastInstanceName
        {
            get
            {
                lock (sync)
                {
                    return settings.LastInstanceName;
                }
            }
        }

        public FilterState FilterState
        {
            get
            {
                lock (sync)
                {
                    return settings.FilterState ?? new FilterState();
                }
            }
        }

        public bool ShowAdvancedFilters
        {
            get
            {
                lock (sync)
                {
                    return settings.ShowAdvancedFilters;
                }
            }
        }

        public AppWindowState WindowState
        {
            get
            {
                lock (sync)
                {
                    return (settings.WindowState ?? new AppWindowState()).Clone();
                }
            }
        }

        public int UiScalePercent
        {
            get
            {
                lock (sync)
                {
                    return UiScaleSettings.NormalizePercent(settings.UiScalePercent);
                }
            }
        }

        public void SaveLastInstanceName(string? instanceName)
        {
            lock (sync)
            {
                if (string.Equals(settings.LastInstanceName, instanceName, StringComparison.Ordinal))
                {
                    return;
                }

                settings.LastInstanceName = instanceName;
                SaveSettings();
            }
        }

        public void SaveBrowserState(FilterState filterState,
                                     bool        showAdvancedFilters)
        {
            lock (sync)
            {
                if (FilterStatesEqual(settings.FilterState, filterState)
                    && settings.ShowAdvancedFilters == showAdvancedFilters)
                {
                    return;
                }

                settings.FilterState = filterState ?? new FilterState();
                settings.ShowAdvancedFilters = showAdvancedFilters;
                SaveSettings();
            }
        }

        public void SaveWindowState(AppWindowState windowState)
        {
            lock (sync)
            {
                var state = windowState ?? new AppWindowState();
                if (WindowStatesEqual(settings.WindowState, state))
                {
                    return;
                }

                settings.WindowState = state.Clone();
                SaveSettings();
            }
        }

        public void SaveUiScalePercent(int uiScalePercent)
        {
            lock (sync)
            {
                var normalized = UiScaleSettings.NormalizePercent(uiScalePercent);
                if (UiScaleSettings.NormalizePercent(settings.UiScalePercent) == normalized)
                {
                    return;
                }

                settings.UiScalePercent = normalized;
                SaveSettings();
            }
        }

        private StoredSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new StoredSettings();
                }

                var raw = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<StoredSettings>(raw)
                       ?? new StoredSettings();
            }
            catch
            {
                return new StoredSettings();
            }
        }

        private void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)
                                    ?? CKANPathUtils.AppDataPath);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, serializerSettings));
        }

        private static bool FilterStatesEqual(FilterState? left,
                                              FilterState? right)
            => string.Equals(left?.SearchText ?? "", right?.SearchText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.AuthorText ?? "", right?.AuthorText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.CompatibilityText ?? "", right?.CompatibilityText ?? "", StringComparison.Ordinal)
               && (left?.SortOption ?? ModSortOption.Name) == (right?.SortOption ?? ModSortOption.Name)
               && (left?.InstalledOnly ?? false) == (right?.InstalledOnly ?? false)
               && (left?.NotInstalledOnly ?? false) == (right?.NotInstalledOnly ?? false)
               && (left?.UpdatableOnly ?? false) == (right?.UpdatableOnly ?? false)
               && (left?.NewOnly ?? false) == (right?.NewOnly ?? false)
               && (left?.CachedOnly ?? false) == (right?.CachedOnly ?? false)
               && (left?.IncompatibleOnly ?? false) == (right?.IncompatibleOnly ?? false)
               && (left?.HasReplacementOnly ?? false) == (right?.HasReplacementOnly ?? false);

        private static bool WindowStatesEqual(AppWindowState? left,
                                              AppWindowState? right)
            => left?.Width == right?.Width
               && left?.Height == right?.Height
               && left?.PositionX == right?.PositionX
               && left?.PositionY == right?.PositionY
               && (left?.IsMaximized ?? false) == (right?.IsMaximized ?? false);

        private sealed class StoredSettings
        {
            public string? LastInstanceName { get; set; }

            public FilterState FilterState { get; set; } = new FilterState();

            public bool ShowAdvancedFilters { get; set; }

            public AppWindowState WindowState { get; set; } = new AppWindowState();

            public int UiScalePercent { get; set; } = UiScaleSettings.DefaultPercent;
        }
    }
}
