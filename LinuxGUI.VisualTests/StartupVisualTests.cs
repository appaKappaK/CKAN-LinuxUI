using System;
using System.Threading.Tasks;

using Avalonia.Headless.NUnit;

using NUnit.Framework;

namespace CKAN.LinuxGUI.VisualTests
{
    [TestFixture]
    public sealed class StartupVisualTests
    {
        [AvaloniaTest]
        public Task LoadingShell_Renders()
            => RenderScenarioAsync(VisualScenario.Loading, "startup-loading");

        [AvaloniaTest]
        public Task EmptyShell_Renders()
            => RenderScenarioAsync(VisualScenario.Empty, "startup-empty");

        [AvaloniaTest]
        public Task SelectionRequiredShell_Renders()
            => RenderScenarioAsync(VisualScenario.SelectionRequired, "startup-selection-required");

        [AvaloniaTest]
        public Task ReadyShell_Renders()
            => RenderScenarioAsync(VisualScenario.Ready, "startup-ready");

        [AvaloniaTest]
        public Task ErrorShell_Renders()
            => RenderScenarioAsync(VisualScenario.Error, "startup-error");

        private static async Task RenderScenarioAsync(VisualScenario scenario, string snapshotName)
        {
            using var service = new FakeGameInstanceService(scenario);
            var catalog = new FakeModCatalogService();
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(
                scenario == VisualScenario.Error
                    ? new ErrorGameInstanceServiceWrapper(service)
                    : service,
                catalog,
                user);
            var window = new MainWindow(viewModel);

            window.Width = 1200;
            window.Height = 760;

            await Task.Delay(scenario == VisualScenario.Loading ? 40 : 150);
            VisualTestSupport.CaptureAndAssert(window, snapshotName);
        }

        [AvaloniaTest]
        public async Task FilteredBrowser_Renders()
        {
            using var service = new FakeGameInstanceService(VisualScenario.Ready);
            var catalog = new FakeModCatalogService();
            var user = new AvaloniaUser();
            var viewModel = new MainWindowViewModel(service, catalog, user);
            var window = new MainWindow(viewModel)
            {
                Width = 1200,
                Height = 760,
            };

            await Task.Delay(150);
            viewModel.ModSearchText = "planet";
            viewModel.FilterCachedOnly = true;
            await Task.Delay(400);

            VisualTestSupport.CaptureAndAssert(window, "browser-filtered");
        }

        private sealed class ErrorGameInstanceServiceWrapper : CKAN.App.Services.IGameInstanceService
        {
            private readonly FakeGameInstanceService inner;

            public ErrorGameInstanceServiceWrapper(FakeGameInstanceService inner)
            {
                this.inner = inner;
            }

            public GameInstanceManager Manager => inner.Manager;
            public RepositoryDataManager RepositoryData => inner.RepositoryData;
            public CKAN.Configuration.IConfiguration Configuration => inner.Configuration;
            public GameInstance? CurrentInstance => inner.CurrentInstance;
            public RegistryManager? CurrentRegistryManager => inner.CurrentRegistryManager;
            public System.Collections.Generic.IReadOnlyList<CKAN.App.Models.InstanceSummary> Instances => inner.Instances;
            public event Action<GameInstance?>? CurrentInstanceChanged
            {
                add    => inner.CurrentInstanceChanged += value;
                remove => inner.CurrentInstanceChanged -= value;
            }
            public Task InitializeAsync(System.Threading.CancellationToken cancellationToken)
                => inner.InitializeErrorAsync(cancellationToken);
            public Task SetCurrentInstanceAsync(string name, System.Threading.CancellationToken cancellationToken)
                => inner.SetCurrentInstanceAsync(name, cancellationToken);
            public void Dispose() => inner.Dispose();
        }
    }
}
