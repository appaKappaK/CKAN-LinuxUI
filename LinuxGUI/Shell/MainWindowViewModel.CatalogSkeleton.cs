using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.Exporters;
using CKAN.IO;
using CKAN.Types;
using CKAN.Versioning;

namespace CKAN.LinuxGUI
{
    public sealed partial class MainWindowViewModel : ReactiveObject
    {
        private static IReadOnlyList<CatalogSkeletonRow> BuildCatalogSkeletonRows(IReadOnlyList<CatalogSkeletonSnapshotRow>? savedRows)
            => savedRows is { Count: > 0 }
                ? savedRows.Select(ToCatalogSkeletonRow).ToList()
                : BuildDefaultCatalogSkeletonRows();

        private static IReadOnlyList<CatalogSkeletonRow> BuildCatalogSkeletonRows(IEnumerable<ModListItem> items)
        {
            var opacityCycle = new[] { 1.00, 0.98, 0.96, 0.94, 0.97, 0.95, 0.93, 0.91 };
            return items.Take(26)
                        .Select((item, index) => new CatalogSkeletonRow
                        {
                            AccentBrush             = string.IsNullOrWhiteSpace(item.PrimaryStateColor) ? "#39424E" : item.PrimaryStateColor,
                            TitleWidth              = SkeletonTextWidth(item.Name, 108, 300, 6.0),
                            AuthorWidth             = SkeletonTextWidth(item.Author, 48, 168, 4.5),
                            SummaryWidth            = SkeletonTextWidth(item.Summary, 168, 396, 5.1),
                            DownloadsWidth          = SkeletonTextWidth(item.DownloadCountLabel, 40, 68, 5.4),
                            CompatibilityWidth      = SkeletonTextWidth(item.Compatibility, 22, 38, 4.0),
                            ReleaseWidth            = SkeletonTextWidth(item.ReleaseDate, 34, 64, 4.5),
                            VersionPrimaryWidth     = SkeletonTextWidth(item.LatestVersion, 30, 58, 3.8),
                            VersionSecondaryWidth   = item.IsInstalled
                                ? SkeletonTextWidth(item.InstalledVersion, 24, 48, 3.5)
                                : 0,
                            Opacity                 = opacityCycle[index % opacityCycle.Length],
                            PrimaryBadgeWidth       = PillWidth(item.PrimaryStateLabel),
                            PrimaryBadgeBackground  = string.IsNullOrWhiteSpace(item.PrimaryStateColor) ? "#3B4653" : item.PrimaryStateColor,
                            HasCachedBadge          = item.IsCached,
                            SecondaryBadgeWidth     = item.HasSecondaryState ? PillWidth(item.SecondaryStateLabel) : 0,
                            SecondaryBadgeBackground = item.SecondaryStateBackground,
                            SecondaryBadgeBorderBrush = item.SecondaryStateBorderBrush,
                            TertiaryBadgeWidth      = item.HasTertiaryState ? PillWidth(item.TertiaryStateLabel) : 0,
                            TertiaryBadgeBackground = item.TertiaryStateBackground,
                            TertiaryBadgeBorderBrush = item.TertiaryStateBorderBrush,
                            QueueBadgeWidth         = item.HasQueueState ? PillWidth(item.QueueStateLabel) : 0,
                            QueueBadgeBackground    = item.QueueStateBackground,
                            QueueBadgeBorderBrush   = item.QueueStateBorderBrush,
                        })
                        .ToList();
        }

        private static IReadOnlyList<CatalogSkeletonRow> BuildDefaultCatalogSkeletonRows()
        {
            string[] accents =
            {
                "#24588A",
                "#39424E",
                "#2E7C59",
                "#9B4559",
            };

            (double TitleWidth,
             double AuthorWidth,
             double SummaryWidth,
             double DownloadsWidth,
             double CompatibilityWidth,
             double ReleaseWidth,
             double VersionPrimaryWidth,
             double VersionSecondaryWidth,
             double PrimaryBadgeWidth,
             bool   HasCachedBadge,
             double SecondaryBadgeWidth,
             double TertiaryBadgeWidth,
             double QueueBadgeWidth,
             double Opacity)[] patterns =
            {
                (182, 92, 286, 66, 42, 76, 54, 42, 54, true,  0,  0,  0, 1.00),
                (208, 108, 254, 60, 36, 68, 48, 36, 48, false, 0,  0,  0, 0.98),
                (196, 96, 308, 64, 40, 72, 52, 40, 58, false, 0,  0,  0, 0.96),
                (176, 88, 244, 58, 34, 64, 44, 32, 74, false, 0,  0,  0, 0.94),
                (214, 112, 322, 68, 44, 80, 56, 44, 54, true,  0,  0, 66, 0.97),
                (188, 94, 266, 62, 38, 70, 50, 38, 58, false, 54, 0,  0, 0.95),
                (202, 104, 296, 66, 40, 74, 54, 42, 48, false, 0,  60, 0, 0.93),
                (172, 84, 236, 56, 32, 62, 42, 30, 74, false, 0,  0,  0, 0.91),
            };

            return Enumerable.Range(0, 26)
                             .Select(index =>
                             {
                                 var pattern = patterns[index % patterns.Length];
                                 var accent = accents[index % accents.Length];
                                 return new CatalogSkeletonRow
                                 {
                                     AccentBrush              = accent,
                                     TitleWidth               = pattern.TitleWidth,
                                     AuthorWidth              = pattern.AuthorWidth,
                                     SummaryWidth             = pattern.SummaryWidth,
                                     DownloadsWidth           = pattern.DownloadsWidth,
                                     CompatibilityWidth       = pattern.CompatibilityWidth,
                                     ReleaseWidth             = pattern.ReleaseWidth,
                                     VersionPrimaryWidth      = pattern.VersionPrimaryWidth,
                                     VersionSecondaryWidth    = pattern.VersionSecondaryWidth,
                                     PrimaryBadgeWidth        = pattern.PrimaryBadgeWidth,
                                     PrimaryBadgeBackground   = accent,
                                     HasCachedBadge           = pattern.HasCachedBadge,
                                     SecondaryBadgeWidth      = pattern.SecondaryBadgeWidth,
                                     SecondaryBadgeBackground = "#39424E",
                                     SecondaryBadgeBorderBrush = "#607286",
                                     TertiaryBadgeWidth       = pattern.TertiaryBadgeWidth,
                                     TertiaryBadgeBackground  = "#31424F",
                                     TertiaryBadgeBorderBrush = "#4C6A86",
                                     QueueBadgeWidth          = pattern.QueueBadgeWidth,
                                     QueueBadgeBackground     = "#4B5A69",
                                     QueueBadgeBorderBrush    = "#4B5A69",
                                     Opacity                  = pattern.Opacity,
                                 };
                             })
                             .ToList();
        }

        private static CatalogSkeletonRow ToCatalogSkeletonRow(CatalogSkeletonSnapshotRow row)
            => new CatalogSkeletonRow
            {
                AccentBrush              = row.AccentBrush,
                TitleWidth               = row.TitleWidth,
                AuthorWidth              = row.AuthorWidth,
                SummaryWidth             = row.SummaryWidth,
                DownloadsWidth           = row.DownloadsWidth,
                CompatibilityWidth       = row.CompatibilityWidth,
                ReleaseWidth             = row.ReleaseWidth,
                VersionPrimaryWidth      = Math.Min(row.VersionPrimaryWidth, 58),
                VersionSecondaryWidth    = Math.Min(row.VersionSecondaryWidth, 48),
                Opacity                  = row.Opacity,
                PrimaryBadgeWidth        = row.PrimaryBadgeWidth,
                PrimaryBadgeBackground   = row.PrimaryBadgeBackground,
                HasCachedBadge           = row.HasCachedBadge,
                SecondaryBadgeWidth      = row.SecondaryBadgeWidth,
                SecondaryBadgeBackground = row.SecondaryBadgeBackground,
                SecondaryBadgeBorderBrush = row.SecondaryBadgeBorderBrush,
                TertiaryBadgeWidth       = row.TertiaryBadgeWidth,
                TertiaryBadgeBackground  = row.TertiaryBadgeBackground,
                TertiaryBadgeBorderBrush = row.TertiaryBadgeBorderBrush,
                QueueBadgeWidth          = row.QueueBadgeWidth,
                QueueBadgeBackground     = row.QueueBadgeBackground,
                QueueBadgeBorderBrush    = row.QueueBadgeBorderBrush,
            };

        private static CatalogSkeletonSnapshotRow ToCatalogSkeletonSnapshotRow(CatalogSkeletonRow row)
            => new CatalogSkeletonSnapshotRow
            {
                AccentBrush              = row.AccentBrush,
                TitleWidth               = row.TitleWidth,
                AuthorWidth              = row.AuthorWidth,
                SummaryWidth             = row.SummaryWidth,
                DownloadsWidth           = row.DownloadsWidth,
                CompatibilityWidth       = row.CompatibilityWidth,
                ReleaseWidth             = row.ReleaseWidth,
                VersionPrimaryWidth      = row.VersionPrimaryWidth,
                VersionSecondaryWidth    = row.VersionSecondaryWidth,
                Opacity                  = row.Opacity,
                PrimaryBadgeWidth        = row.PrimaryBadgeWidth,
                PrimaryBadgeBackground   = row.PrimaryBadgeBackground,
                HasCachedBadge           = row.HasCachedBadge,
                SecondaryBadgeWidth      = row.SecondaryBadgeWidth,
                SecondaryBadgeBackground = row.SecondaryBadgeBackground,
                SecondaryBadgeBorderBrush = row.SecondaryBadgeBorderBrush,
                TertiaryBadgeWidth       = row.TertiaryBadgeWidth,
                TertiaryBadgeBackground  = row.TertiaryBadgeBackground,
                TertiaryBadgeBorderBrush = row.TertiaryBadgeBorderBrush,
                QueueBadgeWidth          = row.QueueBadgeWidth,
                QueueBadgeBackground     = row.QueueBadgeBackground,
                QueueBadgeBorderBrush    = row.QueueBadgeBorderBrush,
            };

        private static double SkeletonTextWidth(string? text,
                                                double  min,
                                                double  max,
                                                double  perCharacter)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            var width = min + (text.Trim().Length * perCharacter);
            return Math.Max(min, Math.Min(max, width));
        }

        private static double PillWidth(string? text)
            => string.IsNullOrWhiteSpace(text)
                ? 0
                : Math.Max(40, Math.Min(112, 18 + (text.Trim().Length * 5.4)));
    }
}
