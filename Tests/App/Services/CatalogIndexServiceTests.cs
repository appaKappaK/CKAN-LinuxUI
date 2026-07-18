using System;
using System.IO;
using System.Linq;

using NUnit.Framework;

using CKAN;
using CKAN.App.Services;
using CKAN.Configuration;
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
        public void LatestModules_WithDuplicateLatestRows_PicksHighestVersion()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(path, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""SystemHeat"", ""name"": ""System Heat"", ""version"": ""0.8.2"", ""kind"": ""package"", ""release_date"": ""2025-07-21"", ""is_latest"": true },
                        { ""identifier"": ""SystemHeat"", ""name"": ""System Heat"", ""version"": ""0.9.1"", ""kind"": ""package"", ""release_date"": ""2026-05-12"", ""is_latest"": true }
                    ]
                }");

                var index = new CatalogIndexService().TryLoad(path);
                var modules = CatalogIndexService.LatestModules(index!).ToList();

                Assert.That(modules, Has.Count.EqualTo(1));
                Assert.That(modules[0].Identifier, Is.EqualTo("SystemHeat"));
                Assert.That(modules[0].Version, Is.EqualTo("0.9.1"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LatestModules_WithSchemaV2_HonorsOverallAndPerModStability()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var path = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(path, @"{
                    ""schema_version"": 2,
                    ""source"": ""fixture"",
                    ""modules"": [
                        {
                            ""identifier"": ""Example"", ""name"": ""Example Stable"", ""version"": ""1.0"",
                            ""release_status"": ""stable"", ""is_latest_stable"": true
                        },
                        {
                            ""identifier"": ""Example"", ""name"": ""Example Testing"", ""version"": ""2.0-beta"",
                            ""release_status"": ""testing"", ""is_latest_testing"": true
                        },
                        {
                            ""identifier"": ""Example"", ""name"": ""Example Development"", ""version"": ""3.0-alpha"",
                            ""release_status"": ""development"", ""is_latest"": true, ""is_latest_development"": true
                        }
                    ]
                }");
                var index = new CatalogIndexService().TryLoad(path);
                var stability = new StabilityToleranceConfig(Path.Combine(dir, "stability.json"));

                var stableModules = CatalogIndexService.LatestModules(index!, stability);
                stability.OverallStabilityTolerance = ReleaseStatus.testing;
                var testingModules = CatalogIndexService.LatestModules(index!, stability);
                stability.SetModStabilityTolerance("Example", ReleaseStatus.development);
                var overriddenModules = CatalogIndexService.LatestModules(index!, stability);

                Assert.Multiple(() =>
                {
                    Assert.That(stableModules.Single().Version, Is.EqualTo("1.0"));
                    Assert.That(stableModules.Single().ReleaseStatus, Is.EqualTo(ReleaseStatus.stable));
                    Assert.That(testingModules.Single().Version, Is.EqualTo("2.0-beta"));
                    Assert.That(overriddenModules.Single().Version, Is.EqualTo("3.0-alpha"));
                });
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

        [Test]
        public void TryLoad_WithOlderIndexThanRepositoryCache_RejectsStaleIndex()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryDir = Path.Combine(dir, "repos");
                Directory.CreateDirectory(repositoryDir);
                var repositoryPath = Path.Combine(repositoryDir, "default.json");
                var indexPath = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(repositoryPath, "{}");
                File.WriteAllText(indexPath, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""OldModule"", ""name"": ""Old Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(indexPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(repositoryPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

                var index = new CatalogIndexService().TryLoad(indexPath, repositoryDir);

                Assert.That(index, Is.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TryLoad_WithNewerIndexThanRepositoryCache_LoadsIndex()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryDir = Path.Combine(dir, "repos");
                Directory.CreateDirectory(repositoryDir);
                var repositoryPath = Path.Combine(repositoryDir, "default.json");
                var indexPath = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(repositoryPath, "{}");
                File.WriteAllText(indexPath, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""CurrentModule"", ""name"": ""Current Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(repositoryPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(indexPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

                var index = new CatalogIndexService().TryLoad(indexPath, repositoryDir);

                Assert.That(index, Is.Not.Null);
                Assert.That(index!.Modules.Single().Identifier, Is.EqualTo("CurrentModule"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TryLoad_WithRepositoryTouchedAfterUnchangedRefresh_UsesContentMarker()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryDir = Path.Combine(dir, "repos");
                Directory.CreateDirectory(repositoryDir);
                var repositoryPath = Path.Combine(repositoryDir, "default.json");
                var etagsPath = Path.Combine(repositoryDir, "etags.json");
                var indexPath = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(repositoryPath, "{}");
                File.WriteAllText(etagsPath, "{}");
                File.WriteAllText(indexPath, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""CurrentModule"", ""name"": ""Current Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(etagsPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(indexPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(repositoryPath,
                                         new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

                var index = new CatalogIndexService().TryLoad(indexPath, repositoryDir);

                Assert.That(index, Is.Not.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ComputeSourceFingerprint_TracksOrderedCacheContents()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var firstPath  = Path.Combine(dir, "first.json");
                var secondPath = Path.Combine(dir, "second.json");
                File.WriteAllText(firstPath, "first");
                File.WriteAllText(secondPath, "second");

                var fingerprint = CatalogIndexService.ComputeSourceFingerprint(
                    new[] { firstPath, secondPath });
                var repeatedFingerprint = CatalogIndexService.ComputeSourceFingerprint(
                    new[] { firstPath, secondPath });
                var reversedFingerprint = CatalogIndexService.ComputeSourceFingerprint(
                    new[] { secondPath, firstPath });
                File.WriteAllText(secondPath, "changed");
                var changedFingerprint = CatalogIndexService.ComputeSourceFingerprint(
                    new[] { firstPath, secondPath });

                Assert.Multiple(() =>
                {
                    Assert.That(fingerprint, Has.Length.EqualTo(64));
                    Assert.That(repeatedFingerprint, Is.EqualTo(fingerprint));
                    Assert.That(reversedFingerprint, Is.Not.EqualTo(fingerprint));
                    Assert.That(changedFingerprint, Is.Not.EqualTo(fingerprint));
                });
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TryLoad_WithSourceFingerprint_RequiresExactCacheFingerprint()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryPath = Path.Combine(dir, "repository.json");
                var indexPath      = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(repositoryPath, "repository data");
                var fingerprint = CatalogIndexService.ComputeSourceFingerprint(
                    new[] { repositoryPath });
                File.WriteAllText(indexPath, $@"{{
                    ""schema_version"": 2,
                    ""source"": ""fixture"",
                    ""source_fingerprint"": ""{fingerprint}"",
                    ""modules"": [
                        {{ ""identifier"": ""CurrentModule"", ""name"": ""Current Module"", ""is_latest"": true }}
                    ]
                }}");
                File.SetLastWriteTimeUtc(indexPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(repositoryPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

                var matching = new CatalogIndexService().TryLoad(
                    indexPath,
                    new[] { repositoryPath },
                    fingerprint);
                var mismatching = new CatalogIndexService().TryLoad(
                    indexPath,
                    new[] { repositoryPath },
                    new string('0', 64));

                Assert.Multiple(() =>
                {
                    Assert.That(matching, Is.Not.Null,
                                "a matching content fingerprint should supersede timestamps");
                    Assert.That(mismatching, Is.Null);
                });
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void TryLoad_LegacyIndexWithoutFingerprint_StillUsesFreshness()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryPath = Path.Combine(dir, "repository.json");
                var indexPath      = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(repositoryPath, "repository data");
                File.WriteAllText(indexPath, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""LegacyModule"", ""name"": ""Legacy Module"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(repositoryPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(indexPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

                var index = new CatalogIndexService().TryLoad(
                    indexPath,
                    new[] { repositoryPath },
                    new string('0', 64));

                Assert.That(index, Is.Not.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        #if NET8_0_OR_GREATER
        [Test]
        public void TryLoad_WithSymlinkedIndex_UsesTargetTimestamp()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var repositoryDir = Path.Combine(dir, "repos");
                Directory.CreateDirectory(repositoryDir);
                var etagsPath = Path.Combine(repositoryDir, "etags.json");
                var targetPath = Path.Combine(dir, "generated-index.json");
                var linkPath = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(etagsPath, "{}");
                File.WriteAllText(targetPath, @"{
                    ""schema_version"": 1,
                    ""source"": ""fixture"",
                    ""modules"": [
                        { ""identifier"": ""OldModule"", ""name"": ""Old Module"", ""kind"": ""package"", ""is_latest"": true }
                    ]
                }");
                File.SetLastWriteTimeUtc(targetPath,
                                         new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(etagsPath,
                                         new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
                File.CreateSymbolicLink(linkPath, targetPath);

                var index = new CatalogIndexService().TryLoad(linkPath, repositoryDir);

                Assert.That(index, Is.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ResolveRefreshOutputPath_WithSymlink_ReturnsFinalTarget()
        {
            var dir = TestData.NewTempDir();
            try
            {
                var targetPath = Path.Combine(dir, "generated-index.json");
                var linkPath   = Path.Combine(dir, "catalog-index-latest.json");
                File.WriteAllText(targetPath, "{}");
                File.CreateSymbolicLink(linkPath, targetPath);

                var outputPath = CatalogIndexService.ResolveRefreshOutputPath(linkPath);

                Assert.That(outputPath, Is.EqualTo(targetPath));
                Assert.That(new FileInfo(linkPath).LinkTarget, Is.Not.Null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
        #endif
    }
}
