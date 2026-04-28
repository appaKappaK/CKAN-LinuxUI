using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;

using CKAN.App.Services;
using CKAN.Configuration;

namespace CKAN.LinuxGUI
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            RequestedThemeVariant = ThemeVariant.Dark;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = Services.GetRequiredService<MainWindow>();
                desktop.Exit += Desktop_Exit;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(_ => new JsonConfiguration());
            services.AddSingleton<RepositoryDataManager>();
            services.AddSingleton<IAppSettingsService, AppSettingsService>();
            services.AddSingleton<AvaloniaUser>();
            services.AddSingleton<IUser>(provider => provider.GetRequiredService<AvaloniaUser>());
            services.AddSingleton<IGameInstanceService, GameInstanceService>();
            services.AddSingleton<CatalogIndexService>();
            services.AddSingleton<IModCatalogService, ModCatalogService>();
            services.AddSingleton<IModSearchService, ModSearchService>();
            services.AddSingleton<IChangesetService, ChangesetService>();
            services.AddSingleton<IModActionService, ModActionService>();
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
        }

        private static void Desktop_Exit(object? sender,
                                         ControlledApplicationLifetimeExitEventArgs e)
        {
            if (Services is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
