using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;
using CKAN.Games.KerbalSpaceProgram;

namespace CKAN.LinuxGUI.VisualTests
{
    internal sealed class FakeGameInstanceService : IGameInstanceService
    {
        private readonly List<string> tempDirs = new List<string>();
        private readonly TaskCompletionSource<bool>? loadingGate;

        public FakeGameInstanceService(VisualScenario scenario)
        {
            Configuration = new JsonConfiguration();
            RepositoryData = new RepositoryDataManager(Path.Combine(Path.GetTempPath(),
                                                                   $"ckan-linux-visual-repos-{Guid.NewGuid():N}"));
            Manager = new GameInstanceManager(new NullUser(), Configuration);

            switch (scenario)
            {
                case VisualScenario.Loading:
                    loadingGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    break;

                case VisualScenario.Empty:
                    break;

                case VisualScenario.SelectionRequired:
                    Instances = new[]
                    {
                        new InstanceSummary
                        {
                            Name = "Career Save",
                            GameDir = "/games/ksp-career",
                            GameName = "KSP",
                            IsCurrent = false,
                        },
                        new InstanceSummary
                        {
                            Name = "RSS Sandbox",
                            GameDir = "/games/ksp-rss",
                            GameName = "KSP",
                            IsCurrent = false,
                        },
                    };
                    break;

                case VisualScenario.Ready:
                    CurrentInstance = CreateGameInstance("Career Save");
                    Instances = new[]
                    {
                        new InstanceSummary
                        {
                            Name = "Career Save",
                            GameDir = CurrentInstance.GameDir,
                            GameName = "KSP",
                            IsCurrent = true,
                        },
                        new InstanceSummary
                        {
                            Name = "RSS Sandbox",
                            GameDir = "/games/ksp-rss",
                            GameName = "KSP",
                            IsCurrent = false,
                        },
                    };
                    break;

                case VisualScenario.Error:
                    break;
            }
        }

        public GameInstanceManager Manager { get; }

        public RepositoryDataManager RepositoryData { get; }

        public IConfiguration Configuration { get; }

        public GameInstance? CurrentInstance { get; private set; }

        public RegistryManager? CurrentRegistryManager => null;

        public IReadOnlyList<InstanceSummary> Instances { get; private set; } = Array.Empty<InstanceSummary>();

        public event Action<GameInstance?>? CurrentInstanceChanged;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (loadingGate != null)
            {
                return loadingGate.Task;
            }

            if (Instances.Count == 0 && CurrentInstance == null)
            {
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        public Task SetCurrentInstanceAsync(string name, CancellationToken cancellationToken)
        {
            foreach (var inst in Instances)
            {
                if (inst.Name == name)
                {
                    CurrentInstance = CreateGameInstance(inst.Name);
                    RebuildInstances(inst.Name);
                    CurrentInstanceChanged?.Invoke(CurrentInstance);
                    break;
                }
            }
            return Task.CompletedTask;
        }

        public void ThrowOnInitialize()
        {
            throw new InvalidOperationException("Injected visual test failure.");
        }

        public Task InitializeErrorAsync(CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("Injected visual test failure."));

        public void Dispose()
        {
            RegistryManager.DisposeAll();
            Manager.Dispose();
            foreach (var dir in tempDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch
                {
                }
            }
        }

        private GameInstance CreateGameInstance(string name)
        {
            var safeName = name.Replace(" ", "-").ToLowerInvariant();
            var dir = Path.Combine(Path.GetTempPath(), "ckan-linux-visual", safeName);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
            tempDirs.Add(dir);
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "GameData"));
            File.WriteAllText(Path.Combine(dir, "KSP.x86_64"), string.Empty);
            File.WriteAllText(Path.Combine(dir, "buildID64.txt"), "3190");
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "Kerbal Space Program");
            return new GameInstance(new KerbalSpaceProgram(), dir, name, new NullUser());
        }

        private void RebuildInstances(string currentName)
        {
            var updated = new InstanceSummary[Instances.Count];
            for (int i = 0; i < Instances.Count; i++)
            {
                var inst = Instances[i];
                updated[i] = new InstanceSummary
                {
                    Name = inst.Name,
                    GameDir = inst.Name == currentName && CurrentInstance != null
                        ? CurrentInstance.GameDir
                        : inst.GameDir,
                    GameName = inst.GameName,
                    IsCurrent = string.Equals(inst.Name, currentName, StringComparison.Ordinal),
                };
            }

            Instances = updated;
        }
    }
}
