using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public IReadOnlyList<CatalogSkeletonSnapshotRow> CatalogSkeletonRows
        {
            get
            {
                lock (sync)
                {
                    return (settings.CatalogSkeletonRows ?? new List<CatalogSkeletonSnapshotRow>())
                        .Select(CloneCatalogSkeletonRow)
                        .ToList();
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

        public void SaveCatalogSkeletonRows(IReadOnlyList<CatalogSkeletonSnapshotRow> rows)
        {
            lock (sync)
            {
                var normalized = (rows ?? Array.Empty<CatalogSkeletonSnapshotRow>())
                    .Select(CloneCatalogSkeletonRow)
                    .ToList();

                if (CatalogSkeletonRowsEqual(settings.CatalogSkeletonRows, normalized))
                {
                    return;
                }

                settings.CatalogSkeletonRows = normalized;
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
               && string.Equals(left?.NameText ?? "", right?.NameText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.IdentifierText ?? "", right?.IdentifierText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.AuthorText ?? "", right?.AuthorText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.SummaryText ?? "", right?.SummaryText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.DescriptionText ?? "", right?.DescriptionText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.LicenseText ?? "", right?.LicenseText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.LanguageText ?? "", right?.LanguageText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.DependsText ?? "", right?.DependsText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.RecommendsText ?? "", right?.RecommendsText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.SuggestsText ?? "", right?.SuggestsText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.ConflictsText ?? "", right?.ConflictsText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.SupportsText ?? "", right?.SupportsText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.TagText ?? "", right?.TagText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.LabelText ?? "", right?.LabelText ?? "", StringComparison.Ordinal)
               && string.Equals(left?.CompatibilityText ?? "", right?.CompatibilityText ?? "", StringComparison.Ordinal)
               && (left?.SortOption ?? ModSortOption.Name) == (right?.SortOption ?? ModSortOption.Name)
               && (left?.SortDescending ?? false) == (right?.SortDescending ?? false)
               && (left?.InstalledOnly ?? false) == (right?.InstalledOnly ?? false)
               && (left?.NotInstalledOnly ?? false) == (right?.NotInstalledOnly ?? false)
               && (left?.UpdatableOnly ?? false) == (right?.UpdatableOnly ?? false)
               && (left?.NotUpdatableOnly ?? false) == (right?.NotUpdatableOnly ?? false)
               && (left?.NewOnly ?? false) == (right?.NewOnly ?? false)
               && (left?.CompatibleOnly ?? false) == (right?.CompatibleOnly ?? false)
               && (left?.CachedOnly ?? false) == (right?.CachedOnly ?? false)
               && (left?.UncachedOnly ?? false) == (right?.UncachedOnly ?? false)
               && (left?.IncompatibleOnly ?? false) == (right?.IncompatibleOnly ?? false)
               && (left?.HasReplacementOnly ?? false) == (right?.HasReplacementOnly ?? false)
               && (left?.NoReplacementOnly ?? false) == (right?.NoReplacementOnly ?? false);

        private static bool WindowStatesEqual(AppWindowState? left,
                                              AppWindowState? right)
            => left?.Width == right?.Width
               && left?.Height == right?.Height
               && left?.PositionX == right?.PositionX
               && left?.PositionY == right?.PositionY
               && (left?.IsMaximized ?? false) == (right?.IsMaximized ?? false);

        private static bool CatalogSkeletonRowsEqual(IReadOnlyList<CatalogSkeletonSnapshotRow>? left,
                                                     IReadOnlyList<CatalogSkeletonSnapshotRow>? right)
        {
            var safeLeft = left ?? Array.Empty<CatalogSkeletonSnapshotRow>();
            var safeRight = right ?? Array.Empty<CatalogSkeletonSnapshotRow>();
            if (safeLeft.Count != safeRight.Count)
            {
                return false;
            }

            for (int index = 0; index < safeLeft.Count; index++)
            {
                var a = safeLeft[index];
                var b = safeRight[index];
                if (a.AccentBrush != b.AccentBrush
                    || a.TitleWidth != b.TitleWidth
                    || a.AuthorWidth != b.AuthorWidth
                    || a.SummaryWidth != b.SummaryWidth
                    || a.DownloadsWidth != b.DownloadsWidth
                    || a.CompatibilityWidth != b.CompatibilityWidth
                    || a.ReleaseWidth != b.ReleaseWidth
                    || a.VersionPrimaryWidth != b.VersionPrimaryWidth
                    || a.VersionSecondaryWidth != b.VersionSecondaryWidth
                    || a.Opacity != b.Opacity
                    || a.PrimaryBadgeWidth != b.PrimaryBadgeWidth
                    || a.PrimaryBadgeBackground != b.PrimaryBadgeBackground
                    || a.HasCachedBadge != b.HasCachedBadge
                    || a.SecondaryBadgeWidth != b.SecondaryBadgeWidth
                    || a.SecondaryBadgeBackground != b.SecondaryBadgeBackground
                    || a.SecondaryBadgeBorderBrush != b.SecondaryBadgeBorderBrush
                    || a.TertiaryBadgeWidth != b.TertiaryBadgeWidth
                    || a.TertiaryBadgeBackground != b.TertiaryBadgeBackground
                    || a.TertiaryBadgeBorderBrush != b.TertiaryBadgeBorderBrush
                    || a.QueueBadgeWidth != b.QueueBadgeWidth
                    || a.QueueBadgeBackground != b.QueueBadgeBackground
                    || a.QueueBadgeBorderBrush != b.QueueBadgeBorderBrush)
                {
                    return false;
                }
            }

            return true;
        }

        private static CatalogSkeletonSnapshotRow CloneCatalogSkeletonRow(CatalogSkeletonSnapshotRow row)
            => new CatalogSkeletonSnapshotRow
            {
                AccentBrush             = row.AccentBrush,
                TitleWidth              = row.TitleWidth,
                AuthorWidth             = row.AuthorWidth,
                SummaryWidth            = row.SummaryWidth,
                DownloadsWidth          = row.DownloadsWidth,
                CompatibilityWidth      = row.CompatibilityWidth,
                ReleaseWidth            = row.ReleaseWidth,
                VersionPrimaryWidth     = row.VersionPrimaryWidth,
                VersionSecondaryWidth   = row.VersionSecondaryWidth,
                Opacity                 = row.Opacity,
                PrimaryBadgeWidth       = row.PrimaryBadgeWidth,
                PrimaryBadgeBackground  = row.PrimaryBadgeBackground,
                HasCachedBadge          = row.HasCachedBadge,
                SecondaryBadgeWidth     = row.SecondaryBadgeWidth,
                SecondaryBadgeBackground = row.SecondaryBadgeBackground,
                SecondaryBadgeBorderBrush = row.SecondaryBadgeBorderBrush,
                TertiaryBadgeWidth      = row.TertiaryBadgeWidth,
                TertiaryBadgeBackground = row.TertiaryBadgeBackground,
                TertiaryBadgeBorderBrush = row.TertiaryBadgeBorderBrush,
                QueueBadgeWidth         = row.QueueBadgeWidth,
                QueueBadgeBackground    = row.QueueBadgeBackground,
                QueueBadgeBorderBrush   = row.QueueBadgeBorderBrush,
            };

        private sealed class StoredSettings
        {
            public string? LastInstanceName { get; set; }

            public FilterState FilterState { get; set; } = new FilterState();

            public bool ShowAdvancedFilters { get; set; }

            public AppWindowState WindowState { get; set; } = new AppWindowState();

            public int UiScalePercent { get; set; } = UiScaleSettings.DefaultPercent;

            public List<CatalogSkeletonSnapshotRow> CatalogSkeletonRows { get; set; } = new List<CatalogSkeletonSnapshotRow>();
        }
    }
}
