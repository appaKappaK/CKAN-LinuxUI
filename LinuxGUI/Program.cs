using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CKAN.LinuxGUI
{
    internal static class Program
    {
        [System.STAThread]
        public static int Main(string[] args)
        {
            Trace.AutoFlush = true;
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
            AppDomain.CurrentDomain.UnhandledException += (_, evt) =>
                Console.Error.WriteLine($"FATAL AppDomain unhandled exception: {evt.ExceptionObject}");
            TaskScheduler.UnobservedTaskException += (_, evt) =>
                Console.Error.WriteLine($"ERROR Unobserved task exception: {evt.Exception}");
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
