using System.IO;
using System.Linq;

using NUnit.Framework;

using CKAN.App.Services;
using Tests.Data;

namespace Tests.App.Services
{
    [TestFixture]
    public sealed class CatalogIndexServiceTests
    {
        [Test]
        public void TryLoad_WithValidIndex_LoadsModules()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(path, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        {
                            ""identifier"": ""ModuleManager"",
                            ""name"": ""Module Manager"",
                            ""kind"": ""package"",
                            ""is_latest"": true
                        }
                    ]
                }");

                var index = new CatalogIndexService().TryLoad(path);

                Assert.That(index, Is.Not.Null);
                Assert.That(index!.Modules.Single().Identifier, Is.EqualTo("ModuleManager"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LatestIdentifiers_FiltersHistoricalDlcAndDuplicates()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(path, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""Old"", ""name"": ""Old"", ""kind"": ""package"", ""is_latest"": false },
                        { ""identifier"": ""DLC"", ""name"": ""DLC"", ""kind"": ""dlc"", ""is_latest"": true },
                        { ""identifier"": ""RealMod"", ""name"": ""Real Mod"", ""kind"": ""package"", ""is_latest"": true },
                        { ""identifier"": ""RealMod"", ""name"": ""Real Mod"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");

                var index = new CatalogIndexService().TryLoad(path);
                var identifiers = CatalogIndexService.LatestIdentifiers(index!).ToList();

                Assert.That(identifiers, Is.EqualTo(new[] { "RealMod" }));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TryLoad_WithChangedFile_ReloadsIndex()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "catalog-index-latest.json");
                var service = new CatalogIndexService();

                File.WriteAllText(path, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""FirstModule"", ""name"": ""First Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(path, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));

                var first = service.TryLoad(path);

                File.WriteAllText(path, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""SecondModule"", ""name"": ""Second Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(path, new System.DateTime(2026, 1, 1, 0, 0, 1, System.DateTimeKind.Utc));

                var second = service.TryLoad(path);

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Not.Null);
                Assert.That(first!.Modules.Single().Identifier, Is.EqualTo("FirstModule"));
                Assert.That(second!.Modules.Single().Identifier, Is.EqualTo("SecondModule"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
