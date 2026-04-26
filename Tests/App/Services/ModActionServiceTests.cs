using System;
using System.Collections.Generic;
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
    public sealed class ModActionServiceTests
    {
        [Test]
        public async Task PreviewChanges_RemoveOnly_ShowsUnusedAutoInstalledDependencies()
        {
            var user = new NullUser();
            using (var inst     = new DisposableKSP())
            using (var config   = new FakeConfiguration(inst.KSP, inst.KSP.Name))
            using (var repo     = new TemporaryRepository(RemovingModMetadata,
                                                          OtherModMetadata,
                                                          AutoDependencyMetadata,
                                                          SharedDependencyMetadata))
            using (var repoData = new TemporaryRepositoryData(user, repo.repo))
            using (var regMgr   = RegistryManager.Instance(inst.KSP, repoData.Manager,
                                                           new Repository[] { repo.repo }))
            using (var gameService = new TestGameInstanceService(inst.KSP,
                                                                 config,
                                                                 repoData.Manager,
                                                                 regMgr))
            {
                var registry = regMgr.registry;
                registry.RegisterModule(registry.GetModuleByVersion("RemovingMod", "1.0")!,
                                        Array.Empty<string>(),
                                        inst.KSP,
                                        false);
                registry.RegisterModule(registry.GetModuleByVersion("OtherMod", "1.0")!,
                                        Array.Empty<string>(),
                                        inst.KSP,
                                        false);
                registry.RegisterModule(registry.GetModuleByVersion("AutoDep", "1.0")!,
                                        Array.Empty<string>(),
                                        inst.KSP,
                                        true);
                registry.RegisterModule(registry.GetModuleByVersion("SharedDep", "1.0")!,
                                        Array.Empty<string>(),
                                        inst.KSP,
                                        true);

                var changes = new ChangesetService();
                changes.QueueRemove(new ModListItem
                {
                    Identifier       = "RemovingMod",
                    Name             = "Removing Mod",
                    IsInstalled      = true,
                    InstalledVersion = "1.0",
                });
                var actions = new ModActionService(gameService, changes, user);

                var preview = await actions.PreviewChangesAsync(CancellationToken.None);

                Assert.Multiple(() =>
                {
                    Assert.That(preview.CanApply, Is.True);
                    Assert.That(preview.SummaryText, Does.Contain("1 auto-removal"));
                    Assert.That(preview.AutoRemovals, Has.Count.EqualTo(1));
                    Assert.That(preview.AutoRemovals[0], Does.Contain("Auto Dependency"));
                    Assert.That(preview.AutoRemovals[0], Does.Contain("AutoDep"));
                    Assert.That(preview.AutoRemovals[0], Does.Contain("Removing Mod"));
                    Assert.That(preview.AutoRemovals[0], Does.Not.Contain("SharedDep"));
                });
            }
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

            public void Dispose()
                => Manager.Dispose();
        }

        private const string RemovingModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""RemovingMod"",
            ""name"": ""Removing Mod"",
            ""abstract"": ""Root mod being removed."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/removing.zip"",
            ""depends"": [
                { ""name"": ""AutoDep"" },
                { ""name"": ""SharedDep"" }
            ]
        }";

        private const string OtherModMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""OtherMod"",
            ""name"": ""Other Mod"",
            ""abstract"": ""Another installed mod still using the shared dependency."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/other.zip"",
            ""depends"": [
                { ""name"": ""SharedDep"" }
            ]
        }";

        private const string AutoDependencyMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""AutoDep"",
            ""name"": ""Auto Dependency"",
            ""abstract"": ""Dependency only used by the removed root."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/auto-dep.zip""
        }";

        private const string SharedDependencyMetadata = @"{
            ""spec_version"": 1,
            ""identifier"": ""SharedDep"",
            ""name"": ""Shared Dependency"",
            ""abstract"": ""Dependency still used by another installed root."",
            ""author"": ""Test Author"",
            ""version"": ""1.0"",
            ""download"": ""https://example.com/shared-dep.zip""
        }";
    }
}
