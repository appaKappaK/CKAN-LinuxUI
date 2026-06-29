using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using CKAN;
using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;

using Tests.Core.Configuration;
using Tests.Data;

namespace Tests.App.Services
{
    [TestFixture]
    public sealed class DisabledModServiceTests
    {
        [Test]
        public async Task DisableAndEnable_MovesManagedFiles_AndUpdatesManifest()
        {
            var user = new NullUser();
            using (var inst     = new DisposableKSP())
            using (var config   = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repo     = new TemporaryRepository(SingleModMetadata))
            using (var repoData = new TemporaryRepositoryData(user, repo.repo))
            using (var regMgr   = RegistryManager.Instance(inst.KSP, repoData.Manager,
                                                           new Repository[] { repo.repo }))
            using (var gameService = new TestGameInstanceService(inst.KSP,
                                                                 config,
                                                                 repoData.Manager,
                                                                 regMgr))
            {
                Directory.CreateDirectory(Path.Combine(inst.KSP.GameDir, "GameData", "A-DISABLED"));
                string sourceFile = Path.Combine(inst.KSP.GameDir, "GameData", "SingleMod", "single.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
                File.WriteAllText(sourceFile, "managed");

                var module = regMgr.registry.GetModuleByVersion("SingleMod", "1.0")!;
                regMgr.registry.RegisterModule(module,
                                              new[] { sourceFile },
                                              inst.KSP,
                                              false);

                var service = new DisabledModService(gameService);

                var disable = await service.DisableAsync("SingleMod", CancellationToken.None);
                string storedFile = Path.Combine(inst.KSP.GameDir,
                                                "GameData",
                                                "A-DISABLED",
                                                "SingleMod",
                                                "GameData",
                                                "SingleMod",
                                                "single.txt");
                string manifestPath = Path.Combine(inst.KSP.CkanDir, "disabled-mods.json");

                Assert.Multiple(() =>
                {
                    Assert.That(disable.Success, Is.True);
                    Assert.That(File.Exists(sourceFile), Is.False);
                    Assert.That(File.Exists(storedFile), Is.True);
                    Assert.That(File.Exists(manifestPath), Is.True);
                    Assert.That(service.GetCurrentSnapshot().IsDisabled("SingleMod"), Is.True);
                });

                var enable = await service.EnableAsync("SingleMod", CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(enable.Success, Is.True);
                    Assert.That(File.Exists(sourceFile), Is.True);
                    Assert.That(File.Exists(storedFile), Is.False);
                    Assert.That(File.Exists(manifestPath), Is.False);
                    Assert.That(service.GetCurrentSnapshot().IsDisabled("SingleMod"), Is.False);
                });
            }
        }

        [Test]
        public async Task PreviewDisable_IncludesDependentManagedMods()
        {
            var user = new NullUser();
            using (var inst     = new DisposableKSP())
            using (var config   = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repo     = new TemporaryRepository(BaseModMetadata,
                                                          DependentModMetadata))
            using (var repoData = new TemporaryRepositoryData(user, repo.repo))
            using (var regMgr   = RegistryManager.Instance(inst.KSP, repoData.Manager,
                                                           new Repository[] { repo.repo }))
            using (var gameService = new TestGameInstanceService(inst.KSP,
                                                                 config,
                                                                 repoData.Manager,
                                                                 regMgr))
            {
                Directory.CreateDirectory(Path.Combine(inst.KSP.GameDir, "GameData", "A-DISABLED"));
                regMgr.registry.RegisterModule(regMgr.registry.GetModuleByVersion("BaseMod", "1.0")!,
                                               Array.Empty<string>(),
                                               inst.KSP,
                                               false);
                regMgr.registry.RegisterModule(regMgr.registry.GetModuleByVersion("DependentMod", "1.0")!,
                                               Array.Empty<string>(),
                                               inst.KSP,
                                               false);

                var service = new DisabledModService(gameService);
                var preview = await service.PreviewDisableAsync("BaseMod", CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(preview.CanApply, Is.True);
                    Assert.That(preview.SummaryLines, Does.Contain("1 dependent disable"));
                    Assert.That(preview.FollowUpLines.Any(line => line.Contains("Dependent Mod")), Is.True);
                });
            }
        }

        [Test]
        public async Task PreviewEnable_ReenablesDisabledDependencies_AndCatalogShowsDisabledFilter()
        {
            var user = new NullUser();
            using (var inst     = new DisposableKSP())
            using (var config   = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repo     = new TemporaryRepository(DependencyModMetadata,
                                                          ParentModMetadata))
            using (var repoData = new TemporaryRepositoryData(user, repo.repo))
            using (var regMgr   = RegistryManager.Instance(inst.KSP, repoData.Manager,
                                                           new Repository[] { repo.repo }))
            using (var gameService = new TestGameInstanceService(inst.KSP,
                                                                 config,
                                                                 repoData.Manager,
                                                                 regMgr))
            {
                Directory.CreateDirectory(Path.Combine(inst.KSP.GameDir, "GameData", "A-DISABLED"));

                string dependencyFile = CreateManagedFile(inst.KSP, "GameData/DependencyMod/dep.txt", "dep");
                string parentFile = CreateManagedFile(inst.KSP, "GameData/ParentMod/parent.txt", "parent");
                regMgr.registry.RegisterModule(regMgr.registry.GetModuleByVersion("DependencyMod", "1.0")!,
                                               new[] { dependencyFile },
                                               inst.KSP,
                                               false);
                regMgr.registry.RegisterModule(regMgr.registry.GetModuleByVersion("ParentMod", "1.0")!,
                                               new[] { parentFile },
                                               inst.KSP,
                                               false);

                var disabledService = new DisabledModService(gameService);
                var disableResult = await disabledService.DisableAsync("DependencyMod", CancellationToken.None);
                Assert.That(disableResult.Success, Is.True);

                var preview = await disabledService.PreviewEnableAsync("ParentMod", CancellationToken.None);
                var catalog = new ModCatalogService(gameService,
                                                    new CatalogIndexService(),
                                                    disabledService);
                var items = await catalog.GetAllModListAsync(CancellationToken.None);
                var counts = catalog.GetFilterOptionCounts(items, new FilterState());
                var filtered = catalog.ApplyFilter(items,
                                                  new FilterState
                                                  {
                                                      InstalledOnly = true,
                                                      DisabledOnly = true,
                                                  });

                Assert.Multiple(() =>
                {
                    Assert.That(preview.CanApply, Is.True);
                    Assert.That(preview.SummaryLines, Does.Contain("1 additional dependency enable"));
                    Assert.That(preview.FollowUpLines.Any(line => line.Contains("Dependency Mod")), Is.True);
                    Assert.That(items.Single(item => item.Identifier == "DependencyMod").IsDisabled, Is.True);
                    Assert.That(items.Single(item => item.Identifier == "ParentMod").IsDisabled, Is.True);
                    Assert.That(counts.Disabled, Is.EqualTo(2));
                    Assert.That(filtered.Select(item => item.Identifier),
                                Is.EquivalentTo(new[] { "DependencyMod", "ParentMod" }));
                });
            }
        }

        private static string CreateManagedFile(GameInstance instance,
                                                string       relativePath,
                                                string       contents)
        {
            string absolutePath = instance.ToAbsoluteGameDir(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, contents);
            return absolutePath;
        }

        private sealed class TestGameInstanceService : IGameInstanceService
        {
            public TestGameInstanceService(GameInstance          instance,
                                           IConfiguration        configuration,
                                           RepositoryDataManager repositoryData,
                                           RegistryManager       registryManager)
            {
                Configuration          = configuration;
                RepositoryData         = repositoryData;
                CurrentRegistryManager = registryManager;
                Manager                = new GameInstanceManager(new NullUser(), configuration);
                Manager.SetCurrentInstance(instance);
            }

            public Registry? CurrentRegistry => CurrentRegistryManager.registry;

            public GameInstanceManager Manager { get; }

            public RepositoryDataManager RepositoryData { get; }

            public IConfiguration Configuration { get; }

            public GameInstance? CurrentInstance => Manager.CurrentInstance;

            public RegistryManager CurrentRegistryManager { get; }

            public IReadOnlyList<InstanceSummary> Instances => Array.Empty<InstanceSummary>();

            event Action<GameInstance?>? IGameInstanceService.CurrentInstanceChanged
            {
                add { }
                remove { }
            }

            public Task InitializeAsync(CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task SetCurrentInstanceAsync(string name,
                                                CancellationToken cancellationToken)
                => Task.CompletedTask;

            public RegistryManager? AcquireWriteRegistryManager()
                => CurrentRegistryManager;

            public void RefreshCurrentRegistry()
            {
            }

            public void ReloadCurrentRegistry()
            {
            }

            public void Dispose()
                => Manager.Dispose();
        }

        private const string SingleModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""SingleMod"",
            ""name"": ""Single Mod"",
            ""abstract"": ""Standalone mod."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/single.zip""
        }";

        private const string BaseModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""BaseMod"",
            ""name"": ""Base Mod"",
            ""abstract"": ""Dependency root."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/base.zip""
        }";

        private const string DependentModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""DependentMod"",
            ""name"": ""Dependent Mod"",
            ""abstract"": ""Depends on BaseMod."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/dependent.zip"",
            ""depends"": [
                { ""name"": ""BaseMod"" }
            ]
        }";

        private const string DependencyModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""DependencyMod"",
            ""name"": ""Dependency Mod"",
            ""abstract"": ""Required by ParentMod."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/dependency.zip""
        }";

        private const string ParentModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""ParentMod"",
            ""name"": ""Parent Mod"",
            ""abstract"": ""Depends on DependencyMod."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/parent.zip"",
            ""depends"": [
                { ""name"": ""DependencyMod"" }
            ]
        }";
    }
}
