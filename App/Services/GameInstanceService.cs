using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.Configuration;

namespace CKAN.App.Services
{
    public sealed class GameInstanceService : IGameInstanceService
    {
        private readonly bool preferReadOnlyRegistry = Environment.GetEnvironmentVariable("CKAN_LINUX_DEV_NO_REGISTRY_LOCK") == "1";
        private readonly bool showDevFakeInstances = Environment.GetEnvironmentVariable("CKAN_LINUX_DEV_FAKE_INSTANCES") == "1";

        public GameInstanceService(IConfiguration       configuration,
                                   RepositoryDataManager repositoryData,
                                   IAppSettingsService   appSettingsService)
        {
            Configuration = configuration;
            RepositoryData = repositoryData;
            AppSettings = appSettingsService;
            Manager = new GameInstanceManager(new NullUser(), configuration);
            Manager.InstanceChanged += OnInstanceChanged;
        }

        public GameInstanceManager Manager { get; }

        public RepositoryDataManager RepositoryData { get; }

        public IConfiguration Configuration { get; }

        public IAppSettingsService AppSettings { get; }

        public GameInstance? CurrentInstance => Manager.CurrentInstance;

        public RegistryManager? CurrentRegistryManager { get; private set; }

        public Registry? CurrentRegistry { get; private set; }

        public IReadOnlyList<InstanceSummary> Instances
        {
            get
            {
                var instances = Manager.Instances.Values
                                       .Select(inst => InstanceSummary.From(inst,
                                                                            CurrentInstance?.Name,
                                                                            Configuration.AutoStartInstance))
                                       .ToList();
                if (showDevFakeInstances)
                {
                    AddDevFakeInstances(instances);
                }
                return instances;
            }
        }

        public event Action<GameInstance?>? CurrentInstanceChanged;

        public Task InitializeAsync(CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AppSettings.LastInstanceName is string preferredName
                    && preferredName.Length > 0
                    && Manager.Instances.TryGetValue(preferredName, out GameInstance? preferredInst)
                    && preferredInst.Valid)
                {
                    Manager.SetCurrentInstance(preferredName);
                }
                else if (Manager.GetPreferredInstance() is GameInstance inst)
                {
                    Manager.SetCurrentInstance(inst);
                }
            }, cancellationToken);

        public Task SetCurrentInstanceAsync(string name, CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Manager.SetCurrentInstance(name);
            }, cancellationToken);

        private static void AddDevFakeInstances(ICollection<InstanceSummary> instances)
        {
            string[] names =
            {
                "Dev Test Install",
                "Dev RSS Sandbox",
                "Dev RP-1 Career",
                "Dev Modded Science",
                "Dev Stock Baseline",
                "Dev Steam Mirror",
                "Dev GOG Mirror",
                "Dev Portable Copy",
                "Dev Broken Path",
                "Dev Long Path Regression",
                "Dev Minimal Mods",
                "Dev Heavy Mods",
                "Dev KSP 1.8 Compatibility",
                "Dev KSP 1.10 Compatibility",
                "Dev KSP 1.11 Compatibility",
                "Dev KSP 1.12 Compatibility",
                "Dev Localization Test",
                "Dev Scroll Boundary",
            };

            foreach (string name in names)
            {
                AddDevFakeInstance(instances,
                                   name,
                                   $"/tmp/ckan-linux-dev/{SlugifyDevInstanceName(name)}");
            }
        }

        private static void AddDevFakeInstance(ICollection<InstanceSummary> instances,
                                               string                       name,
                                               string                       gameDir)
        {
            if (instances.Any(inst => string.Equals(inst.Name, name, StringComparison.Ordinal)))
            {
                return;
            }

            instances.Add(new InstanceSummary
            {
                Name = name,
                GameDir = gameDir,
                GameName = "KSP",
                VersionText = "1.12.5",
            });
        }

        private static string SlugifyDevInstanceName(string name)
            => name.ToLowerInvariant()
                   .Replace("dev ", "", StringComparison.Ordinal)
                   .Replace(" ", "-", StringComparison.Ordinal)
                   .Replace(".", "-", StringComparison.Ordinal);

        public void Dispose()
        {
            Manager.InstanceChanged -= OnInstanceChanged;
            RegistryManager.DisposeAll();
            Manager.Dispose();
        }

        public RegistryManager? AcquireWriteRegistryManager()
        {
            if (CurrentInstance?.Valid != true)
            {
                return null;
            }

            try
            {
                return CurrentRegistryManager ?? RegistryManager.Instance(CurrentInstance, RepositoryData);
            }
            catch (RegistryInUseKraken)
            {
                return null;
            }
        }

        public void RefreshCurrentRegistry()
        {
            if (CurrentInstance?.Valid != true)
            {
                CurrentRegistry = null;
                return;
            }

            CurrentRegistry = CurrentRegistryManager?.registry
                              ?? RegistryManager.ReadOnlyRegistry(CurrentInstance, RepositoryData);
        }

        public void ReloadCurrentRegistry()
        {
            if (CurrentInstance?.Valid != true)
            {
                CurrentRegistryManager = null;
                CurrentRegistry = null;
                return;
            }

            CurrentRegistryManager?.Dispose();
            CurrentRegistryManager = null;
            RegistryManager.DisposeInstance(CurrentInstance);

            if (!preferReadOnlyRegistry)
            {
                CurrentRegistryManager = RegistryManager.Instance(CurrentInstance, RepositoryData);
                if (CurrentRegistryManager.ScanUnmanagedFiles())
                {
                    CurrentRegistryManager.Save(false);
                }
            }

            RefreshCurrentRegistry();
        }

        private void OnInstanceChanged(GameInstance? previous,
                                       GameInstance? current)
        {
            AppSettings.SaveLastInstanceName(current?.Name);
            if (current == null)
            {
                CurrentRegistryManager = null;
                CurrentRegistry = null;
            }
            else if (current.Valid)
            {
                CurrentRegistryManager = preferReadOnlyRegistry
                    ? null
                    : RegistryManager.Instance(current, RepositoryData);
                RefreshCurrentRegistry();
            }
            else
            {
                CurrentRegistryManager = null;
                CurrentRegistry = null;
            }
            CurrentInstanceChanged?.Invoke(current);
        }
    }
}
