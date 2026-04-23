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
            => Manager.Instances.Values
                      .Select(inst => new InstanceSummary
                      {
                          Name      = inst.Name,
                          GameDir   = inst.GameDir,
                          GameName  = inst.Game.ShortName,
                          IsCurrent = CurrentInstance?.Name == inst.Name,
                      })
                      .ToList();

        public event Action<GameInstance?>? CurrentInstanceChanged;

        public Task InitializeAsync(CancellationToken cancellationToken)
            => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (AppSettings.LastInstanceName is string preferredName
                    && preferredName.Length > 0
                    && Manager.HasInstance(preferredName))
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

        public void Dispose()
        {
            Manager.InstanceChanged -= OnInstanceChanged;
            RegistryManager.DisposeAll();
            Manager.Dispose();
        }

        public RegistryManager? AcquireWriteRegistryManager()
        {
            if (CurrentInstance == null)
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
            if (CurrentInstance == null)
            {
                CurrentRegistry = null;
                return;
            }

            CurrentRegistry = CurrentRegistryManager?.registry
                              ?? RegistryManager.ReadOnlyRegistry(CurrentInstance, RepositoryData);
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
            else
            {
                CurrentRegistryManager = preferReadOnlyRegistry
                    ? null
                    : RegistryManager.Instance(current, RepositoryData);
                RefreshCurrentRegistry();
            }
            CurrentInstanceChanged?.Invoke(current);
        }
    }
}
