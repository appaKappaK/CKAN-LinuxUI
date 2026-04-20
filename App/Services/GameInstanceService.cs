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
        public GameInstanceService(IConfiguration       configuration,
                                   RepositoryDataManager repositoryData)
        {
            Configuration = configuration;
            RepositoryData = repositoryData;
            Manager = new GameInstanceManager(new NullUser(), configuration);
            Manager.InstanceChanged += OnInstanceChanged;
        }

        public GameInstanceManager Manager { get; }

        public RepositoryDataManager RepositoryData { get; }

        public IConfiguration Configuration { get; }

        public GameInstance? CurrentInstance => Manager.CurrentInstance;

        public RegistryManager? CurrentRegistryManager { get; private set; }

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
                if (Manager.GetPreferredInstance() is GameInstance inst)
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

        private void OnInstanceChanged(GameInstance? previous,
                                       GameInstance? current)
        {
            if (current == null)
            {
                CurrentRegistryManager = null;
            }
            else
            {
                CurrentRegistryManager = RegistryManager.Instance(current, RepositoryData);
            }
            CurrentInstanceChanged?.Invoke(current);
        }
    }
}
