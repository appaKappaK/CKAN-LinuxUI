using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using CKAN;
using CKAN.Extensions;
using CKAN.Configuration;
using CKAN.Versioning;

using Tests.Data;
using Tests.Core.Relationships;

namespace Tests.Core.Registry
{
    [TestFixture]
    public class CompatibilitySorterTests
    {
        [Test,
            TestCase(new string[]
                     {
                         @"{
                             ""spec_version"":    ""v1.4"",
                             ""identifier"":      ""kOS-EVA"",
                             ""name"":            ""kOS-EVA"",
                             ""abstract"":        ""Addon for kOS that allows controlling a kerbal while on EVA"",
                             ""version"":         ""0.2.0.0"",
                             ""ksp_version_min"": ""1.8.0"",
                             ""ksp_version_max"": ""1.12.99"",
                             ""license"":         ""GPL-3.0"",
                             ""download"":        ""https://github.com/"",
                             ""depends"": [
                                 { ""name"": ""Harmony2"" }
                             ]
                         }",
                         @"{
                             ""spec_version"": ""v1.4"",
                             ""identifier"":   ""Harmony2"",
                             ""name"":         ""Harmony 2"",
                             ""abstract"":     ""A library for patching, replacing and decorating .NET and Mono methods during runtime"",
                             ""version"":      ""2.2.1.0"",
                             ""ksp_version_min"": ""1.8.0"",
                             ""ksp_version_max"": ""1.12.99"",
                             ""license"":         ""MIT"",
                             ""download"":        ""https://spacedockinfo/""
                         }",
                     },
                     new string[]
                     {
                         @"{
                             ""spec_version"":    ""v1.4"",
                             ""identifier"":      ""kOS-EVA"",
                             ""name"":            ""kOS-EVA"",
                             ""abstract"":        ""Addon for kOS that allows controlling a kerbal while on EVA"",
                             ""version"":         ""0.2.0.0"",
                             ""ksp_version_min"": ""1.8.0"",
                             ""ksp_version_max"": ""1.12.99"",
                             ""license"":         ""GPL-3.0"",
                             ""download"":        ""https://github.com/""
                         }",
                     },
                     "kOS-EVA"),
        ]
        public void Constructor_OverlappingModules_HigherPriorityOverrides(string[] modules1,
                                                                           string[] modules2,
                                                                           string   identifier)
        {
            // Arrange
            var user = new NullUser();
            using (var repo1 = new TemporaryRepository(0, modules1))
            using (var repo2 = new TemporaryRepository(1, modules2))
            using (var repoData = new TemporaryRepositoryData(user, repo1.repo,
                                                                    repo2.repo))
            {
                var versCrit  = new GameVersionCriteria(GameVersion.Parse("1.12.5"));
                var repos     = new Repository[] { repo1.repo, repo2.repo };
                var providers = repoData.Manager
                                        .GetAllAvailableModules(repos)
                                        .ToGroupedDictionary(am => am.AllAvailable()
                                                                     .First().identifier);
                var installed = new Dictionary<string, InstalledModule>();
                var dlls      = new Dictionary<string, string>().Keys;
                var dlcs      = new Dictionary<string, UnmanagedModuleVersion>();
                var highPrio  = repoData.Manager
                                        .GetAvailableModules(Enumerable.Repeat(repo1.repo, 1),
                                                             identifier)
                                        .First()
                                        .Latest(ReleaseStatus.stable);

                // Act
                var sorter = new CompatibilitySorter(
                    new StabilityToleranceConfig(""), versCrit,
                    repoData.Manager.GetAllAvailDicts(repos),
                    providers, installed, dlls, dlcs);

                // Assert
                Assert.AreEqual(0, sorter.LatestIncompatible.Count);
                Assert.AreEqual(2, sorter.LatestCompatible.Count);
                Assert.AreEqual(highPrio!.ToJson(),
                                sorter.LatestCompatible.First(m => m.identifier == identifier).ToJson());
            }
        }

        [Test]
        public void Constructor_OnlyCompatibleIsPrerelease_Incompatible()
        {
            var avail = new AvailableModule(
                            "BDArmoryContinued",
                            new string[]
                            {
                                @"{
                                    ""identifier"":     ""BDArmoryContinued"",
                                    ""version"":        ""2.0"",
                                    ""ksp_version"":    ""1.12"",
                                    ""release_status"": ""testing""
                                }",
                                @"{
                                    ""identifier"":  ""BDArmoryContinued"",
                                    ""version"":     ""1.1"",
                                    ""ksp_version"": ""1.9""
                                }",
                                @"{
                                    ""identifier"":  ""BDArmoryContinued"",
                                    ""version"":     ""1.0"",
                                    ""ksp_version"": ""1.9""
                                }",
                            }.Select(RelationshipResolverTests.MergeWithDefaults)
                             .Select(CkanModule.FromJson)
                             .ToArray());
            var ident     = avail.AllAvailable().First().identifier;
            var versCrit  = new GameVersionCriteria(GameVersion.Parse("1.12.5"));
            var providers = new Dictionary<string, AvailableModule[]>
            {
                { ident, new[] { avail } }
            };
            var allAvailable = new Dictionary<string, AvailableModule>[]
            {
                new Dictionary<string, AvailableModule>
                {
                    { ident, avail }
                }
            };
            var installed = new Dictionary<string, InstalledModule>();
            var dlls      = new Dictionary<string, string>().Keys;
            var dlcs      = new Dictionary<string, UnmanagedModuleVersion>();
            var stability = new StabilityToleranceConfig("");

            var sorter = new CompatibilitySorter(stability, versCrit,
                                                 allAvailable, providers,
                                                 installed, dlls, dlcs);

            CollectionAssert.IsEmpty(sorter.Compatible,
                                     "No compatible available modules");
            CollectionAssert.IsEmpty(sorter.LatestCompatible,
                                     "No latest compatible module");
            CollectionAssert.AreEquivalent(Enumerable.Repeat(ident, 1),
                                           sorter.Incompatible.Keys,
                                           "Incompatible available module");
            CollectionAssert.AreEquivalent(Enumerable.Repeat(ident, 1),
                                           sorter.LatestIncompatible.Select(m => m.identifier),
                                           "Incompatible latest module");
        }
    }
}
