using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;

[assembly: AvaloniaTestApplication(typeof(CKAN.LinuxGUI.VisualTests.VisualTestAppBuilder))]

namespace CKAN.LinuxGUI.VisualTests
{
    public static class VisualTestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<CKAN.LinuxGUI.App>()
                         .UseSkia()
                         .UseHeadless(new AvaloniaHeadlessPlatformOptions
                         {
                             UseHeadlessDrawing = false,
                         });
    }
}
