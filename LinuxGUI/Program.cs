using Avalonia;
using Avalonia.ReactiveUI;

namespace CKAN.LinuxGUI
{
    internal static class Program
    {
        [System.STAThread]
        public static int Main(string[] args)
        {
            Logging.Initialize("log4net.linuxgui.xml");
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .LogToTrace()
                         .UseReactiveUI();
    }
}
