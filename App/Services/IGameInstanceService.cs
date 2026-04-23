using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CKAN.App.Models;
using CKAN.Configuration;

namespace CKAN.App.Services
{
    public interface IGameInstanceService : IDisposable
    {
        Registry? CurrentRegistry { get; }

        GameInstanceManager Manager { get; }

        RepositoryDataManager RepositoryData { get; }

        IConfiguration Configuration { get; }

        GameInstance? CurrentInstance { get; }

        RegistryManager? CurrentRegistryManager { get; }

        IReadOnlyList<InstanceSummary> Instances { get; }

        event Action<GameInstance?>? CurrentInstanceChanged;

        Task InitializeAsync(CancellationToken cancellationToken);

        Task SetCurrentInstanceAsync(string name, CancellationToken cancellationToken);

        RegistryManager? AcquireWriteRegistryManager();

        void RefreshCurrentRegistry();
    }
}
