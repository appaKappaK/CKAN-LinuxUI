using System;
using System.IO;

using NUnit.Framework;

using CKAN.App.Models;
using CKAN.App.Services;

namespace Tests.Core.Configuration
{
    [TestFixture]
    public sealed class AppSettingsServiceTests
    {
        [Test]
        public void SettingsRoundTripPersistsSessionState()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"ckan-linux-settings-tests-{Guid.NewGuid():N}");
            string settingsPath = Path.Combine(tempDir, "linuxgui.settings.json");

            try
            {
                var first = new AppSettingsService(settingsPath);
                first.SaveLastInstanceName("Career Save");
                first.SaveBrowserState(new FilterState
                {
                    SearchText         = "restock",
                    AuthorText         = "Nertea",
                    CompatibilityText  = "1.12",
                    SortOption         = ModSortOption.UpdatesFirst,
                    SortDescending     = false,
                    InstalledOnly      = true,
                    UpdatableOnly      = true,
                    CompatibleOnly     = true,
                    CachedOnly         = true,
                    UncachedOnly       = false,
                    IncompatibleOnly   = false,
                    HasReplacementOnly = true,
                }, true);
                first.SaveWindowState(new AppWindowState
                {
                    Width       = 1480,
                    Height      = 900,
                    PositionX   = 120,
                    PositionY   = 80,
                    IsMaximized = true,
                    ShowDetailsPane = false,
                });
                first.SaveUiScalePercent(90);

                var second = new AppSettingsService(settingsPath);

                Assert.Multiple(() =>
                {
                    Assert.That(second.LastInstanceName, Is.EqualTo("Career Save"));
                    Assert.That(second.ShowAdvancedFilters, Is.True);
                    Assert.That(second.FilterState.SearchText, Is.EqualTo("restock"));
                    Assert.That(second.FilterState.AuthorText, Is.EqualTo("Nertea"));
                    Assert.That(second.FilterState.CompatibilityText, Is.EqualTo("1.12"));
                    Assert.That(second.FilterState.SortOption, Is.EqualTo(ModSortOption.UpdatesFirst));
                    Assert.That(second.FilterState.SortDescending, Is.False);
                    Assert.That(second.FilterState.InstalledOnly, Is.True);
                    Assert.That(second.FilterState.UpdatableOnly, Is.True);
                    Assert.That(second.FilterState.CompatibleOnly, Is.True);
                    Assert.That(second.FilterState.CachedOnly, Is.True);
                    Assert.That(second.FilterState.UncachedOnly, Is.False);
                    Assert.That(second.FilterState.HasReplacementOnly, Is.True);
                    Assert.That(second.WindowState.Width, Is.EqualTo(1480));
                    Assert.That(second.WindowState.Height, Is.EqualTo(900));
                    Assert.That(second.WindowState.PositionX, Is.EqualTo(120));
                    Assert.That(second.WindowState.PositionY, Is.EqualTo(80));
                    Assert.That(second.WindowState.IsMaximized, Is.True);
                    Assert.That(second.WindowState.ShowDetailsPane, Is.False);
                    Assert.That(second.UiScalePercent, Is.EqualTo(90));
                });
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
