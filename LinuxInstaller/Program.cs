using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace CKAN.LinuxInstaller;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<InstallerApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}

public sealed class InstallerApp : Application
{
    public override void Initialize()
    {
        Resources["SystemAccentColor"] = Color.Parse("#3F8A63");
        Styles.Add(new FluentTheme());
        AddHoverStyle("primary", "#398C64", "#4BA777");
        AddHoverStyle("secondary", "#34495C", "#597C9C");
        AddHoverStyle("title-close", "#3A536A", "#3A536A");
    }

    private void AddHoverStyle(string buttonClass, string background, string border)
    {
        var style = new Style(x => x.OfType<Button>().Class(buttonClass).Class(":pointerover").Template().OfType<ContentPresenter>());
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, Brush.Parse(background)));
        style.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, Brush.Parse(border)));
        Styles.Add(style);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new InstallerWindow();
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class InstallerWindow : Window
{
    private readonly StackPanel content = new() { Spacing = 16 };
    private readonly string bundle = AppContext.BaseDirectory;
    private readonly string prefix = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local");

    public InstallerWindow()
    {
        Title = "CKAN Linux Installer";
        Width = 470;
        Height = 360;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#1B222A");
        var title = new Grid { Height = 38, Background = Brush.Parse("#202F40") };
        title.Children.Add(new TextBlock { Text = "CKAN Linux Installer", Foreground = Brush.Parse("#DCE9F5"), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0) });
        title.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(title).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        var close = new Button { Content = "×", Width = 38, Height = 38, Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Background = Brushes.Transparent, Foreground = Brush.Parse("#DCE9F5"), FontSize = 22 };
        close.Classes.Add("title-close");
        close.Click += (_, _) => Close();
        title.Children.Add(close);
        content.VerticalAlignment = VerticalAlignment.Center;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        layout.Children.Add(title);
        var body = new Border { Padding = new Thickness(24, 10, 24, 20), Child = content };
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        Content = new Border { BorderBrush = Brush.Parse("#598FC0"), BorderThickness = new Thickness(2), Child = layout };
        ShowInstall();
    }

    private void Header(string message)
    {
        content.Children.Clear();
        using var image = typeof(InstallerWindow).Assembly.GetManifestResourceStream("ckan-logo.png");
        if (image != null)
            content.Children.Add(new Image { Source = new Bitmap(image), Width = 245, Height = 132, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = message, Foreground = Brush.Parse("#F3F7FC"), TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
    }

    private Button Button(string label, bool primary = false)
    {
        var button = new Button { Content = label, MinWidth = 105, Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(8), Foreground = Brushes.White, Background = Brush.Parse(primary ? "#286B4C" : "#24303B"), BorderBrush = Brush.Parse(primary ? "#347F5D" : "#405061"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Classes.Add(primary ? "primary" : "secondary");
        return button;
    }

    private void ShowInstall()
    {
        Header("Install CKAN Linux on this PC.");
        var install = Button("Install", true);
        install.Click += async (_, _) => await InstallAsync();
        content.Children.Add(install);
    }

    private async Task InstallAsync()
    {
        Header("Installing CKAN Linux…");
        content.Children.Add(InstallProgress(indeterminate: true));
        var minimumDisplay = Task.Delay(TimeSpan.FromSeconds(1));
        try
        {
            var script = Path.Combine(bundle, "install-payload.sh");
            var payload = Path.Combine(bundle, "payload");
            var start = new ProcessStartInfo("/bin/bash") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(payload);
            start.ArgumentList.Add(prefix);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the installer.");
            var errorTask = process.StandardError.ReadToEndAsync();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            await Task.WhenAll(process.WaitForExitAsync(), minimumDisplay);
            var error = await errorTask;
            await outputTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Installation failed." : error.Trim());
            Header("CKAN Linux is installed. You can find it in your application menu.");
            content.Children.Add(InstallProgress(indeterminate: false));
            var ok = Button("OK", true);
            ok.Click += (_, _) => ShowCleanup();
            content.Children.Add(ok);
        }
        catch (Exception ex)
        {
            await minimumDisplay;
            Header($"Installation failed: {ex.Message}");
            var retry = Button("Try Again");
            retry.Click += (_, _) => ShowInstall();
            content.Children.Add(retry);
        }
    }

    private static ProgressBar InstallProgress(bool indeterminate)
        => new() { Width = 280, Height = 8, Minimum = 0, Maximum = 100,
                   Value = indeterminate ? 0 : 100, IsIndeterminate = indeterminate,
                   Background = Brush.Parse("#111820"), Foreground = Brush.Parse("#3F8A63"),
                   HorizontalAlignment = HorizontalAlignment.Center };

    private void ShowCleanup()
    {
        content.Children.Clear();
        content.Children.Add(new TextBlock { Text = "Remove the installer files?", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#F3F7FC"), HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = MatchingArchive() != null
            ? "The extracted installer and matching ZIP are no longer needed."
            : "The extracted installer is no longer needed. The ZIP could not be located and will remain.",
            Foreground = Brush.Parse("#AAB8C7"), TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        var open = new CheckBox { Content = "Open CKAN Linux when done", IsChecked = true, Foreground = Brush.Parse("#F3F7FC"), HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(open);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        var keep = Button("Keep files");
        var remove = Button("Remove", true);
        keep.Click += (_, _) => Finish(open.IsChecked == true);
        remove.Click += (_, _) => Cleanup(open.IsChecked == true);
        buttons.Children.Add(keep);
        buttons.Children.Add(remove);
        content.Children.Add(buttons);
    }

    private void Cleanup(bool open)
    {
        try
        {
            if (!File.Exists(Path.Combine(bundle, ".ckan-linux-installer-bundle"))) throw new InvalidOperationException("Installer bundle marker is missing.");
            var archive = MatchingArchive();
            if (archive != null) File.Delete(archive);
            Directory.Delete(Path.Combine(bundle, "payload"), true);
            File.Delete(Path.Combine(bundle, "install-payload.sh"));
            File.Delete(Path.Combine(bundle, ".ckan-linux-installer-bundle"));
            if (Environment.ProcessPath is string executable && Path.GetDirectoryName(executable) == Path.TrimEndingDirectorySeparator(bundle)) File.Delete(executable);
            Finish(open);
        }
        catch (Exception ex)
        {
            Header($"CKAN Linux was installed, but cleanup failed: {ex.Message}");
            var close = Button("Close");
            close.Click += (_, _) => Close();
            content.Children.Add(close);
        }
    }

    private void Finish(bool open)
    {
        try
        {
            if (open)
            {
                var launcher = Path.Combine(prefix, "bin", "ckan-linux");
                _ = Process.Start(new ProcessStartInfo(launcher) { UseShellExecute = false })
                    ?? throw new InvalidOperationException("The CKAN Linux launcher did not start.");
            }
            Close();
        }
        catch (Exception ex)
        {
            Header($"CKAN Linux was installed, but could not be opened: {ex.Message}");
            var close = Button("Close");
            close.Click += (_, _) => Close();
            content.Children.Add(close);
        }
    }

    private string? MatchingArchive()
    {
        try
        {
            var directory = Directory.GetParent(Path.TrimEndingDirectorySeparator(bundle));
            var executable = Environment.ProcessPath;
            if (directory == null || executable == null) return null;
            var candidate = Path.Combine(directory.FullName, "CKAN-Linux-Installer-linux-x64.zip");
            if (!File.Exists(candidate)) return null;
            using var zip = ZipFile.OpenRead(candidate);
            var entry = zip.GetEntry("CKAN-Linux-Installer/CKAN-Linux-Installer");
            var marker = zip.GetEntry("CKAN-Linux-Installer/.ckan-linux-installer-bundle");
            if (entry == null || marker == null || marker.Length > 128) return null;
            using var markerReader = new StreamReader(marker.Open());
            if (markerReader.ReadToEnd() != File.ReadAllText(Path.Combine(bundle, ".ckan-linux-installer-bundle"))) return null;
            using var archived = entry.Open();
            using var running = File.OpenRead(executable);
            return SHA256.HashData(archived).AsSpan().SequenceEqual(SHA256.HashData(running)) ? candidate : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }
}
