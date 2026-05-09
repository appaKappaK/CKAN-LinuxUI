using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using CKAN;
using CKAN.App.Services;

using Tests.Core.Configuration;
using Tests.Data;

namespace Tests.App.Services
{
    [TestFixture]
    public sealed class GameInstanceServiceTests
    {
        [Test]
        public async Task InitializeAsync_LastInstanceFolderDeleted_DoesNotRecreateFolder()
        {
            using (var tempdir = new TemporaryDirectory())
            using (var config = new FakeConfiguration(
                       new System.Collections.Generic.List<Tuple<string, string, string>>
                       {
                           new Tuple<string, string, string>("deleted",
                                                             Path.Combine(tempdir, "DeletedKSP"),
                                                             "KSP")
                       },
                       null,
                       null))
            {
                string missingGameDir = config.GetInstance(0).Item2;
                string missingCkanDir = Path.Combine(missingGameDir, "CKAN");
                var settingsPath = Path.Combine(tempdir, "settings", "linuxgui.settings.json");
                var settings = new AppSettingsService(settingsPath);
                settings.SaveLastInstanceName("deleted");
                var repoData = new RepositoryDataManager(Path.Combine(tempdir, "repos"));
                using (var service = new GameInstanceService(config, repoData, settings))
                {
                    await service.InitializeAsync(CancellationToken.None);

                    Assert.Multiple(() =>
                    {
                        Assert.That(service.CurrentInstance, Is.Null);
                        DirectoryAssert.DoesNotExist(missingGameDir);
                        DirectoryAssert.DoesNotExist(missingCkanDir);
                    });
                }
            }
        }
    }
}
