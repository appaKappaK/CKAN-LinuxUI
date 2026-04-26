using System;
using System.IO;

using NUnit.Framework;

using CKAN.App.Models;
using CKAN.App.Services;

namespace Tests.App.Services
{
    [TestFixture]
    public sealed class ModSearchServiceTests
    {
        [Test]
        public void Constructor_DefaultsSavedClearAllFilterToInstalled()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"ckan-linux-search-tests-{Guid.NewGuid():N}");
            string settingsPath = Path.Combine(tempDir, "linuxgui.settings.json");

            try
            {
                var settings = new AppSettingsService(settingsPath);
                settings.SaveBrowserState(new FilterState
                {
                    InstalledOnly  = false,
                    SortOption     = ModSortOption.Popularity,
                    SortDescending = true,
                }, false);

                var search = new ModSearchService(settings);

                Assert.Multiple(() =>
                {
                    Assert.That(search.Current.InstalledOnly, Is.True);
                    Assert.That(search.Current.NotInstalledOnly, Is.False);
                    Assert.That(search.Current.SortOption, Is.EqualTo(ModSortOption.Popularity));
                    Assert.That(search.Current.SortDescending, Is.True);
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

        [Test]
        public void Constructor_PreservesExplicitSavedFilter()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"ckan-linux-search-tests-{Guid.NewGuid():N}");
            string settingsPath = Path.Combine(tempDir, "linuxgui.settings.json");

            try
            {
                var settings = new AppSettingsService(settingsPath);
                settings.SaveBrowserState(new FilterState
                {
                    InstalledOnly    = false,
                    NotInstalledOnly = true,
                }, false);

                var search = new ModSearchService(settings);

                Assert.Multiple(() =>
                {
                    Assert.That(search.Current.InstalledOnly, Is.False);
                    Assert.That(search.Current.NotInstalledOnly, Is.True);
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

        [Test]
        public void Constructor_PreservesSavedSearch()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"ckan-linux-search-tests-{Guid.NewGuid():N}");
            string settingsPath = Path.Combine(tempDir, "linuxgui.settings.json");

            try
            {
                var settings = new AppSettingsService(settingsPath);
                settings.SaveBrowserState(new FilterState
                {
                    SearchText    = "parallax",
                    InstalledOnly = false,
                }, false);

                var search = new ModSearchService(settings);

                Assert.Multiple(() =>
                {
                    Assert.That(search.Current.SearchText, Is.EqualTo("parallax"));
                    Assert.That(search.Current.InstalledOnly, Is.False);
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
