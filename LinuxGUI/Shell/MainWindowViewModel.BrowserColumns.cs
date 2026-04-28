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
        public double BrowserColumnResizeMaxMetadataWidth(double tableWidth)
            => MaxBrowserMetadataColumnWidthForTable(tableWidth);

        public void ResizeBrowserNameDownloadsDivider(double startMetadataWidth,
                                                       double startDownloadsWidth,
                                                       double startReleasedWidth,
                                                       double startInstalledWidth,
                                                       double maxMetadataWidth,
                                                       double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var minimumMetadataWidth = MinimumBrowserMetadataColumnWidth();
            var actualDelta = ClampDimension(startMetadataWidth - delta,
                                             minimumMetadataWidth,
                                             maxMetadataWidth);
            actualDelta = startMetadataWidth - actualDelta;

            var metadataWidth = startMetadataWidth - actualDelta;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (actualDelta > 0)
            {
                var remainingShrink = actualDelta;
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
            }
            else if (actualDelta < 0)
            {
                var remainingGrowth = -actualDelta;
                GrowColumn(ref downloadsWidth, MaxBrowserDownloadsColumnWidth, ref remainingGrowth);
                GrowColumn(ref releasedWidth, MaxBrowserReleasedColumnWidth, ref remainingGrowth);
                GrowColumn(ref installedWidth, MaxBrowserInstalledColumnWidth, ref remainingGrowth);
            }

            SetBrowserColumnLayout(metadataWidth, downloadsWidth, releasedWidth, installedWidth, maxMetadataWidth);
        }

        public void ResizeBrowserDownloadsReleasedDivider(double startMetadataWidth,
                                                          double startDownloadsWidth,
                                                          double startReleasedWidth,
                                                          double startInstalledWidth,
                                                          double maxMetadataWidth,
                                                          double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserDownloadsColumnWidth - startDownloadsWidth,
                                                     availableGrowth));
                downloadsWidth = startDownloadsWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startDownloadsWidth - MinBrowserDownloadsColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth,
                                            0,
                                            Math.Min(MaxBrowserReleasedColumnWidth - startReleasedWidth,
                                                     availableGrowth));
                releasedWidth = startReleasedWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth, downloadsWidth, releasedWidth, installedWidth, maxMetadataWidth);
        }

        public void ResizeBrowserReleasedInstalledDivider(double startMetadataWidth,
                                                          double startDownloadsWidth,
                                                          double startReleasedWidth,
                                                          double startInstalledWidth,
                                                          double maxMetadataWidth,
                                                          double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserReleasedColumnWidth - startReleasedWidth,
                                                     availableGrowth));
                releasedWidth = startReleasedWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth,
                                            0,
                                            Math.Min(MaxBrowserInstalledColumnWidth - startInstalledWidth,
                                                     availableGrowth));
                installedWidth = startInstalledWidth + growth;
                var remainingShrink = growth;
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth,
                                   startDownloadsWidth,
                                   releasedWidth,
                                   installedWidth,
                                   maxMetadataWidth);
        }

        public void ResizeBrowserInstalledVersionDivider(double startMetadataWidth,
                                                         double startDownloadsWidth,
                                                         double startReleasedWidth,
                                                         double startInstalledWidth,
                                                         double maxMetadataWidth,
                                                         double delta)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            var startVersionWidth = BrowserVersionColumnWidth(startMetadataWidth,
                                                              startDownloadsWidth,
                                                              startReleasedWidth,
                                                              startInstalledWidth);
            var metadataWidth = startMetadataWidth;
            var downloadsWidth = startDownloadsWidth;
            var releasedWidth = startReleasedWidth;
            var installedWidth = startInstalledWidth;

            if (delta > 0)
            {
                var availableGrowth = (startVersionWidth - MinBrowserVersionColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(delta,
                                            0,
                                            Math.Min(MaxBrowserInstalledColumnWidth - startInstalledWidth,
                                                     availableGrowth));
                installedWidth = startInstalledWidth + growth;
                var remainingShrink = growth;
                var versionShrink = Math.Min(remainingShrink,
                                             Math.Max(0, startVersionWidth - MinBrowserVersionColumnWidth));
                remainingShrink -= versionShrink;
                metadataWidth += remainingShrink;
            }
            else if (delta < 0)
            {
                var requestedGrowth = -delta;
                var availableGrowth = (startInstalledWidth - MinBrowserInstalledColumnWidth)
                                      + (startReleasedWidth - MinBrowserReleasedColumnWidth)
                                      + (startDownloadsWidth - MinBrowserDownloadsColumnWidth)
                                      + (maxMetadataWidth - startMetadataWidth);
                var growth = ClampDimension(requestedGrowth, 0, availableGrowth);
                var remainingShrink = growth;
                ShrinkColumn(ref installedWidth, MinBrowserInstalledColumnWidth, ref remainingShrink);
                ShrinkColumn(ref releasedWidth, MinBrowserReleasedColumnWidth, ref remainingShrink);
                ShrinkColumn(ref downloadsWidth, MinBrowserDownloadsColumnWidth, ref remainingShrink);
                metadataWidth += remainingShrink;
            }

            SetBrowserColumnLayout(metadataWidth,
                                   downloadsWidth,
                                   releasedWidth,
                                   installedWidth,
                                   maxMetadataWidth);
        }

        public void ResizeBrowserMetadataColumn(double requestedWidth)
        {
            var width = ClampBrowserMetadataColumnWidth(requestedWidth);
            var downloadsWidth = browserDownloadsColumnWidth;
            var releasedWidth = browserReleasedColumnWidth;
            var installedWidth = browserInstalledColumnWidth;
            FitBrowserFixedColumns(width, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            if (Math.Abs(browserMetadataColumnWidth - width) < 0.1
                && Math.Abs(browserDownloadsColumnWidth - downloadsWidth) < 0.1
                && Math.Abs(browserReleasedColumnWidth - releasedWidth) < 0.1
                && Math.Abs(browserInstalledColumnWidth - installedWidth) < 0.1)
            {
                return;
            }

            browserMetadataColumnWidth = width;
            browserDownloadsColumnWidth = downloadsWidth;
            browserReleasedColumnWidth = releasedWidth;
            browserInstalledColumnWidth = installedWidth;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserDownloadsColumn(double requestedWidth)
        {
            var width = ClampBrowserDownloadsColumnWidth(requestedWidth);
            if (Math.Abs(browserDownloadsColumnWidth - width) < 0.1)
            {
                return;
            }

            browserDownloadsColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserReleasedColumn(double requestedWidth)
        {
            var width = ClampBrowserReleasedColumnWidth(requestedWidth);
            if (Math.Abs(browserReleasedColumnWidth - width) < 0.1)
            {
                return;
            }

            browserReleasedColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void ResizeBrowserInstalledColumn(double requestedWidth)
        {
            var width = ClampBrowserInstalledColumnWidth(requestedWidth);
            if (Math.Abs(browserInstalledColumnWidth - width) < 0.1)
            {
                return;
            }

            browserInstalledColumnWidth = width;
            RaiseBrowserColumnLayoutChanged();
        }

        public void CommitBrowserColumnLayout()
            => appSettingsService.SaveModBrowserColumnLayout(CurrentBrowserColumnLayout());

        private void ApplyBrowserColumnLayout(ModBrowserColumnLayout? layout)
        {
            var normalized = NormalizeBrowserColumnLayout(layout);
            browserMetadataColumnWidth  = normalized.MetadataColumnWidth;
            browserDownloadsColumnWidth = normalized.DownloadsColumnWidth;
            browserReleasedColumnWidth  = normalized.ReleasedColumnWidth;
            browserInstalledColumnWidth = normalized.InstalledColumnWidth;
        }

        private ModBrowserColumnLayout CurrentBrowserColumnLayout()
            => new ModBrowserColumnLayout
            {
                MetadataColumnWidth  = Math.Round(browserMetadataColumnWidth),
                DownloadsColumnWidth = Math.Round(browserDownloadsColumnWidth),
                ReleasedColumnWidth  = Math.Round(browserReleasedColumnWidth),
                InstalledColumnWidth = Math.Round(browserInstalledColumnWidth),
            };

        private static ModBrowserColumnLayout NormalizeBrowserColumnLayout(ModBrowserColumnLayout? layout)
        {
            var safeLayout = layout ?? new ModBrowserColumnLayout();
            var metadataWidth = ClampDimension(safeLayout.MetadataColumnWidth,
                                               MinBrowserMetadataColumnWidth,
                                               MaxBrowserMetadataColumnWidth);
            var downloadsWidth = ClampDimension(safeLayout.DownloadsColumnWidth,
                                                MinBrowserDownloadsColumnWidth,
                                                MaxBrowserDownloadsColumnWidth);
            var releasedWidth = ClampDimension(safeLayout.ReleasedColumnWidth,
                                               MinBrowserReleasedColumnWidth,
                                               MaxBrowserReleasedColumnWidth);
            var installedWidth = ClampDimension(safeLayout.InstalledColumnWidth,
                                                MinBrowserInstalledColumnWidth,
                                                MaxBrowserInstalledColumnWidth);
            FitBrowserFixedColumns(metadataWidth, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            return new ModBrowserColumnLayout
            {
                MetadataColumnWidth  = metadataWidth,
                DownloadsColumnWidth = downloadsWidth,
                ReleasedColumnWidth  = releasedWidth,
                InstalledColumnWidth = installedWidth,
            };
        }

        private double ClampBrowserMetadataColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserMetadataColumnWidth,
                              MaxBrowserMetadataColumnWidth);

        private double ClampBrowserDownloadsColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserDownloadsColumnWidth,
                              Math.Min(MaxBrowserDownloadsColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserReleasedColumnWidth
                                       - browserInstalledColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private double ClampBrowserReleasedColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserReleasedColumnWidth,
                              Math.Min(MaxBrowserReleasedColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserDownloadsColumnWidth
                                       - browserInstalledColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private double ClampBrowserInstalledColumnWidth(double requestedWidth)
            => ClampDimension(requestedWidth,
                              MinBrowserInstalledColumnWidth,
                              Math.Min(MaxBrowserInstalledColumnWidth,
                                       browserMetadataColumnWidth
                                       - browserDownloadsColumnWidth
                                       - browserReleasedColumnWidth
                                       - MinBrowserVersionColumnWidth));

        private static double ClampDimension(double value,
                                             double minimum,
                                             double maximum)
        {
            var safeValue = double.IsFinite(value) ? value : minimum;
            var safeMaximum = Math.Max(minimum, maximum);
            return Math.Clamp(safeValue, minimum, safeMaximum);
        }

        private static void FitBrowserFixedColumns(double metadataWidth,
                                                   ref double downloadsWidth,
                                                   ref double releasedWidth,
                                                   ref double installedWidth)
        {
            var availableWidth = Math.Max(MinBrowserDownloadsColumnWidth
                                          + MinBrowserReleasedColumnWidth
                                          + MinBrowserInstalledColumnWidth,
                                          metadataWidth - MinBrowserVersionColumnWidth);
            var combinedWidth = downloadsWidth + releasedWidth + installedWidth;
            if (combinedWidth <= availableWidth)
            {
                return;
            }

            var downloadsFlex = Math.Max(0, downloadsWidth - MinBrowserDownloadsColumnWidth);
            var releasedFlex = Math.Max(0, releasedWidth - MinBrowserReleasedColumnWidth);
            var installedFlex = Math.Max(0, installedWidth - MinBrowserInstalledColumnWidth);
            var totalFlex = downloadsFlex + releasedFlex + installedFlex;
            var excessWidth = combinedWidth - availableWidth;

            if (totalFlex <= 0)
            {
                downloadsWidth = MinBrowserDownloadsColumnWidth;
                releasedWidth = MinBrowserReleasedColumnWidth;
                installedWidth = MinBrowserInstalledColumnWidth;
                return;
            }

            downloadsWidth = Math.Max(MinBrowserDownloadsColumnWidth,
                                      downloadsWidth - (excessWidth * downloadsFlex / totalFlex));
            releasedWidth = Math.Max(MinBrowserReleasedColumnWidth,
                                     releasedWidth - (excessWidth * releasedFlex / totalFlex));
            installedWidth = Math.Max(MinBrowserInstalledColumnWidth,
                                      installedWidth - (excessWidth * installedFlex / totalFlex));
        }

        private static double MinimumBrowserMetadataColumnWidth()
            => Math.Max(MinBrowserMetadataColumnWidth,
                        MinBrowserDownloadsColumnWidth
                        + MinBrowserReleasedColumnWidth
                        + MinBrowserInstalledColumnWidth
                        + MinBrowserVersionColumnWidth);

        private static double BrowserVersionColumnWidth(double metadataWidth,
                                                        double downloadsWidth,
                                                        double releasedWidth,
                                                        double installedWidth)
            => Math.Max(MinBrowserVersionColumnWidth,
                        metadataWidth - downloadsWidth - releasedWidth - installedWidth);

        private static double MaxBrowserMetadataColumnWidthForTable(double tableWidth)
        {
            var minimum = MinimumBrowserMetadataColumnWidth();
            if (!double.IsFinite(tableWidth) || tableWidth <= 0)
            {
                return MaxBrowserMetadataColumnWidth;
            }

            return ClampDimension(tableWidth - MinBrowserNameColumnWidth,
                                  minimum,
                                  MaxBrowserMetadataColumnWidth);
        }

        private static double NormalizeBrowserMaxMetadataWidth(double maxMetadataWidth)
            => ClampDimension(maxMetadataWidth,
                              MinimumBrowserMetadataColumnWidth(),
                              MaxBrowserMetadataColumnWidth);

        private static void ShrinkColumn(ref double width,
                                         double     minimum,
                                         ref double remainingShrink)
        {
            if (remainingShrink <= 0)
            {
                return;
            }

            var shrink = Math.Min(remainingShrink, Math.Max(0, width - minimum));
            width -= shrink;
            remainingShrink -= shrink;
        }

        private static void GrowColumn(ref double width,
                                       double     maximum,
                                       ref double remainingGrowth)
        {
            if (remainingGrowth <= 0)
            {
                return;
            }

            var growth = Math.Min(remainingGrowth, Math.Max(0, maximum - width));
            width += growth;
            remainingGrowth -= growth;
        }

        private void SetBrowserColumnLayout(double metadataWidth,
                                            double downloadsWidth,
                                            double releasedWidth,
                                            double installedWidth,
                                            double maxMetadataWidth = MaxBrowserMetadataColumnWidth)
        {
            maxMetadataWidth = NormalizeBrowserMaxMetadataWidth(maxMetadataWidth);
            metadataWidth = ClampDimension(metadataWidth,
                                           MinimumBrowserMetadataColumnWidth(),
                                           maxMetadataWidth);
            downloadsWidth = ClampDimension(downloadsWidth,
                                            MinBrowserDownloadsColumnWidth,
                                            MaxBrowserDownloadsColumnWidth);
            releasedWidth = ClampDimension(releasedWidth,
                                           MinBrowserReleasedColumnWidth,
                                           MaxBrowserReleasedColumnWidth);
            installedWidth = ClampDimension(installedWidth,
                                            MinBrowserInstalledColumnWidth,
                                            MaxBrowserInstalledColumnWidth);
            FitBrowserFixedColumns(metadataWidth, ref downloadsWidth, ref releasedWidth, ref installedWidth);

            if (Math.Abs(browserMetadataColumnWidth - metadataWidth) < 0.1
                && Math.Abs(browserDownloadsColumnWidth - downloadsWidth) < 0.1
                && Math.Abs(browserReleasedColumnWidth - releasedWidth) < 0.1
                && Math.Abs(browserInstalledColumnWidth - installedWidth) < 0.1)
            {
                return;
            }

            browserMetadataColumnWidth = metadataWidth;
            browserDownloadsColumnWidth = downloadsWidth;
            browserReleasedColumnWidth = releasedWidth;
            browserInstalledColumnWidth = installedWidth;
            RaiseBrowserColumnLayoutChanged();
        }

        private void RaiseBrowserColumnLayoutChanged()
        {
            this.RaisePropertyChanged(nameof(BrowserMetadataColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserDownloadsColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserReleasedColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserInstalledColumnWidth));
            this.RaisePropertyChanged(nameof(BrowserMetadataColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserDownloadsColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserReleasedColumnGridLength));
            this.RaisePropertyChanged(nameof(BrowserInstalledColumnGridLength));
        }
    }
}
