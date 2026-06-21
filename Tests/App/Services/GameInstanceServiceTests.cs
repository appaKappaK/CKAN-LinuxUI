using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

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
        private const string InstalledModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""InstalledMod"",
            ""name"": ""Installed Mod"",
            ""abstract"": ""Test installed mod."",
            ""author"": ""Test Author"",
            ""license"": ""MIT"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/installed.zip""
        }";

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

        [Test]
        public async Task ReloadCurrentRegistry_RereadsRegistryFromDisk()
        {
            var user = new NullUser();
            using (var settingsDir = new TemporaryDirectory())
            using (var inst = new DisposableKSP())
            using (var config = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repoData = new TemporaryRepositoryData(user))
            {
                var settings = new AppSettingsService(Path.Combine(settingsDir, "linuxgui.settings.json"));
                settings.SaveLastInstanceName(inst.KSP.Name);
                using (var service = new GameInstanceService(config, repoData.Manager, settings))
                {
                    await service.InitializeAsync(CancellationToken.None);

                    var registryManager = service.CurrentRegistryManager;
                    Assert.That(registryManager, Is.Not.Null);

                    registryManager!.registry.RegisterModule(CkanModule.FromJson(InstalledModMetadata),
                                                             Array.Empty<string>(),
                                                             inst.KSP,
                                                             false);
                    registryManager.Save();
                    service.RefreshCurrentRegistry();
                    Assert.That(service.CurrentRegistry!.InstalledModules, Has.Count.EqualTo(1));

                    string registryPath = Path.Combine(inst.KSP.CkanDir, "registry.json");
                    var registryJson = JObject.Parse(File.ReadAllText(registryPath));
                    registryJson["installed_modules"] = new JObject();
                    registryJson["installed_files"] = new JObject();
                    File.WriteAllText(registryPath, registryJson.ToString());

                    service.RefreshCurrentRegistry();
                    Assert.That(service.CurrentRegistry!.InstalledModules, Has.Count.EqualTo(1));

                    service.ReloadCurrentRegistry();
                    Assert.That(service.CurrentRegistry!.InstalledModules, Is.Empty);
                }
            }
        }

        [Test]
        public async Task ReloadCurrentRegistry_RescansUnmanagedDlls()
        {
            using (var settingsDir = new TemporaryDirectory())
            using (var inst = new DisposableKSP())
            using (var config = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repoData = new TemporaryRepositoryData(new NullUser()))
            {
                Directory.CreateDirectory(Path.Combine(inst.KSP.GameDir, "GameData", "scatterer"));
                File.WriteAllText(Path.Combine(inst.KSP.GameDir, "GameData", "scatterer", "scatterer.dll"), "");

                var settings = new AppSettingsService(Path.Combine(settingsDir, "linuxgui.settings.json"));
                settings.SaveLastInstanceName(inst.KSP.Name);
                using (var service = new GameInstanceService(config, repoData.Manager, settings))
                {
                    await service.InitializeAsync(CancellationToken.None);

                    service.ReloadCurrentRegistry();

                    Assert.That(service.CurrentRegistry?.IsAutodetected("scatterer"), Is.True);
                }
            }
        }
    }
}
