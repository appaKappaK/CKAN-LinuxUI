using System.IO;
using System.Linq;

using NUnit.Framework;
using Moq;

using CKAN;
using CKAN.Games;
using CKAN.Versioning;
using Tests.Data;

namespace Tests.Core.Repositories
{
    [TestFixture]
    public class RepositoryDataManagerTests
    {
        [Test]
        public void UpdateRegistryTarGz()
        {
            // Arrange
            var user     = new NullUser();
            var testRepo = new Repository("testRepo", TestData.TestKANTarGz());
            using (var repoData = new TemporaryRepositoryData(user, testRepo))
            {
                var crit = new GameVersionCriteria(GameVersion.Parse("0.25.0"));

                // Act
                var versions = repoData.Manager.GetAvailableModules(Enumerable.Repeat(testRepo, 1),
                                                                    "FerramAerospaceResearch")
                                               .Select(am => am.Latest(ReleaseStatus.stable, crit)?.version.ToString())
                                               .ToArray();

                // Assert
                CollectionAssert.AreEquivalent(new string[] { "v0.14.3.2" },
                                               versions);
            }
        }

        [Test]
        public void UpdateRegistryZip()
        {
            // Arrange
            var user     = new NullUser();
            var testRepo = new Repository("testRepo", TestData.TestKANZip());
            using (var repoData = new TemporaryRepositoryData(user, testRepo))
            {
                var crit = new GameVersionCriteria(GameVersion.Parse("0.25.0"));

                // Act
                var versions = repoData.Manager.GetAvailableModules(Enumerable.Repeat(testRepo, 1),
                                                                    "FerramAerospaceResearch")
                                               .Select(am => am.Latest(ReleaseStatus.stable, crit)?.version.ToString())
                                               .ToArray();

                // Assert
                CollectionAssert.AreEquivalent(new string[] { "v0.14.3.2" },
                                               versions);
            }
        }

        [Test]
        public void BadKanTarGz()
        {
            Assert.DoesNotThrow(delegate
            {
                var user = new NullUser();
                var badRepo = new Repository("badRepo", TestData.BadKANTarGz());
                using (var repoData = new TemporaryRepositoryData(user, badRepo))
                {
                }
            });
        }

        [Test]
        public void BadKanZip()
        {
            Assert.DoesNotThrow(delegate
            {
                var user = new NullUser();
                var badRepo = new Repository("badRepo", TestData.BadKANZip());
                using (var repoData = new TemporaryRepositoryData(user, badRepo))
                {
                }
            });
        }

        [Test]
        public void Prepopulate_PreviouslyLoadedDir_LoadsMetadata()
        {
            // Arrange
            var game  = new Mock<IGame>();
            var user  = new NullUser();
            var repos = new Repository[] { new Repository("TestRepo", TestData.TestKANTarGz()) };
            using (var reposDir = new TemporaryDirectory())
            {
                var prev  = new RepositoryDataManager(reposDir);
                prev.Update(repos, game.Object, true,
                            new NetAsyncDownloader(user, () => null ), user);
                var sut = new RepositoryDataManager(reposDir);

                // Act
                sut.Prepopulate(repos, null);

                // Assert
                CollectionAssert.IsNotEmpty(sut.GetAllAvailableModules(repos));
            }
        }

        [Test]
        public void GetRepositoryCachePaths_SortsDeduplicatesAndSkipsMissingFiles()
        {
            using (var reposDir = new TemporaryDirectory())
            {
                var sut         = new RepositoryDataManager(reposDir);
                var firstByName = new Repository("Alpha", "https://example.test/alpha", 10);
                var nextByName  = new Repository("Zulu",  "https://example.test/zulu",  10);
                var last        = new Repository("Later", "https://example.test/later", 20);
                var missing     = new Repository("Missing", "https://example.test/missing", 0);
                var firstPath   = CachePath(reposDir, firstByName);
                var nextPath    = CachePath(reposDir, nextByName);
                var lastPath    = CachePath(reposDir, last);
                File.WriteAllText(firstPath, "{}");
                File.WriteAllText(nextPath,  "{}");
                File.WriteAllText(lastPath,  "{}");

                var paths = sut.GetRepositoryCachePaths(new[]
                {
                    last, nextByName, missing, firstByName, nextByName,
                });

                CollectionAssert.AreEqual(new[] { firstPath, nextPath, lastPath }, paths);
                CollectionAssert.IsEmpty(sut.GetRepositoryCachePaths(null));
            }
        }

        private static string CachePath(string reposDir, Repository repo)
            => Path.Combine(reposDir,
                            $"{NetFileCache.CreateURLHash(repo.uri)}-{repo.name}.json");
    }
}
