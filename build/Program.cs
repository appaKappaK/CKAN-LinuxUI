using System;
using System.Collections.Generic;
using System.IO;

using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Solution.Project.Properties;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.DotNet.Restore;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core.IO;
using Cake.Frosting;

using IOPath = System.IO.Path;

namespace Build;

public static class Program
{
    public static int Main(string[] args)
        => new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
}

[TaskName("Default")]
[TaskDescription("Build and package the CKAN Linux desktop app")]
[IsDependentOn(typeof(LinuxGuiPackageTask))]
public sealed class DefaultTask : FrostingTask<BuildContext>;

[TaskName("Debug")]
[TaskDescription("Build and package the CKAN Linux desktop app in Debug configuration")]
[IsDependentOn(typeof(DefaultTask))]
public sealed class DebugTask : FrostingTask<BuildContext>;

[TaskName("Release")]
[TaskDescription("Build and package the CKAN Linux desktop app in Release configuration")]
[IsDependentOn(typeof(DefaultTask))]
public sealed class ReleaseTask : FrostingTask<BuildContext>;

[TaskName("CLI")]
[TaskDescription("Build and publish the optional CKAN Linux command-line client")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(GenerateGlobalAssemblyVersionInfoTask))]
public sealed class CliTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        const string runtime = "linux-x64";
        var publishDirectory = context.Paths.CmdlinePublishDirectory(runtime);

        context.CleanDirectory(context.Paths.OutDirectory.Combine("CKAN-CmdLine")
                                                         .Combine(context.BuildConfiguration));
        context.CleanDirectory(publishDirectory);

        context.DotNetPublish(context.Paths.CmdlineProject.FullPath, new DotNetPublishSettings
        {
            Configuration     = context.BuildConfiguration,
            Framework         = "net8.0",
            Runtime           = runtime,
            PublishSingleFile = true,
            SelfContained     = true,
            OutputDirectory   = publishDirectory,
        });

        var executable = publishDirectory.CombineWithFilePath("CKAN-CmdLine");
        var smokeDirectory = context.Paths.BuildDirectory.Combine("smoke").Combine("cli");
        context.CleanDirectory(smokeDirectory);
        var output = context.RunExecutable(executable,
                                           "version",
                                           new Dictionary<string, string?>
                                           {
                                               { "CKAN_CONFIG_FILE", smokeDirectory.CombineWithFilePath("config.json").FullPath },
                                           },
                                           smokeDirectory);
        if (output.Count == 0 || output[0] != $"v{context.GetVersion()}")
        {
            throw new Exception($"CLI smoke test failed: {string.Join(Environment.NewLine, output)}");
        }
    }
}

[TaskName("Netkan")]
[TaskDescription("Build and publish the optional NetKAN maintainer tool")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(GenerateGlobalAssemblyVersionInfoTask))]
public sealed class NetkanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        const string runtime = "linux-x64";
        var publishDirectory = context.Paths.NetkanPublishDirectory(runtime);

        context.CleanDirectory(context.Paths.OutDirectory.Combine("CKAN-NetKAN")
                                                         .Combine(context.BuildConfiguration));
        context.CleanDirectory(publishDirectory);

        context.DotNetPublish(context.Paths.NetkanProject.FullPath, new DotNetPublishSettings
        {
            Configuration     = context.BuildConfiguration,
            Framework         = "net8.0",
            Runtime           = runtime,
            PublishSingleFile = true,
            SelfContained     = true,
            OutputDirectory   = publishDirectory,
        });
    }
}

[TaskName("LinuxGUI")]
[TaskDescription("Build and publish the Linux Avalonia shell")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(GenerateGlobalAssemblyVersionInfoTask))]
public sealed class LinuxGuiTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        const string runtime = "linux-x64";
        var publishDirectory = context.Paths.LinuxGuiPublishDirectory(runtime);

        context.CleanDirectory(context.Paths.OutDirectory.Combine("CKAN-LinuxGUI")
                                                         .Combine(context.BuildConfiguration)
                                                         .Combine("bin"));
        context.CleanDirectory(publishDirectory);

        context.DotNetPublish(context.Paths.LinuxGuiProject.FullPath, new DotNetPublishSettings
        {
            Configuration   = context.BuildConfiguration,
            Framework       = "net8.0",
            Runtime         = runtime,
            SelfContained   = true,
            OutputDirectory = publishDirectory,
            MSBuildSettings = new DotNetMSBuildSettings
            {
                Properties = { { "PublishTrimmed", ["false"] } },
            },
        });
    }
}

[TaskName("LinuxGUIPackage")]
[TaskDescription("Assemble a Linux desktop package layout for the Avalonia shell")]
[IsDependentOn(typeof(LinuxGuiTask))]
public sealed class LinuxGuiPackageTask : FrostingTask<BuildContext>
{
    private static readonly int[] IconSizes = [16, 32, 48, 64, 96, 128, 256];
    private static readonly string[] IconNames = ["ckan-linux", "CKAN-LinuxGUI", "ckan-linuxgui"];

    public override void Run(BuildContext context)
    {
        const string runtime = "linux-x64";

        var publishDirectory = context.Paths.LinuxGuiPublishDirectory(runtime);
        var packageDirectory = context.Paths.LinuxGuiPackageDirectory(runtime);
        var rootDirectory    = new DirectoryInfo(packageDirectory.FullPath);
        var binDirectory     = new DirectoryInfo(IOPath.Combine(rootDirectory.FullName, "usr", "bin"));
        var libDirectory     = new DirectoryInfo(IOPath.Combine(rootDirectory.FullName, "usr", "lib", "ckan-linux"));
        var appsDirectory    = new DirectoryInfo(IOPath.Combine(rootDirectory.FullName, "usr", "share", "applications"));
        var docsDirectory    = new DirectoryInfo(IOPath.Combine(rootDirectory.FullName, "usr", "share", "doc", "ckan-linux"));
        var packagingDir     = context.Paths.RootDirectory.Combine("LinuxGUI").Combine("packaging");

        if (rootDirectory.Exists)
        {
            rootDirectory.Delete(true);
        }

        rootDirectory.Create();
        binDirectory.Create();
        libDirectory.Create();
        appsDirectory.Create();
        docsDirectory.Create();

        CopyDirectoryContents(new DirectoryInfo(publishDirectory.FullPath), libDirectory);

        var launcherSource      = new FileInfo(packagingDir.CombineWithFilePath("ckan-linux").FullPath);
        var launcherDestination = new FileInfo(IOPath.Combine(binDirectory.FullName, "ckan-linux"));
        launcherSource.CopyTo(launcherDestination.FullName, true);
        BuildContext.ChmodExecutable(new FilePath(launcherDestination.FullName));

        var desktopSource      = new FileInfo(packagingDir.CombineWithFilePath("ckan-linux.desktop").FullPath);
        var desktopDestination = new FileInfo(IOPath.Combine(appsDirectory.FullName, "ckan-linux.desktop"));
        desktopSource.CopyTo(desktopDestination.FullName, true);

        var readmeSource = new FileInfo(context.Paths.RootDirectory.Combine("LinuxGUI")
                                                     .CombineWithFilePath("README.md")
                                                     .FullPath);
        readmeSource.CopyTo(IOPath.Combine(docsDirectory.FullName, "README.md"), true);

        foreach (var size in IconSizes)
        {
            var iconDirectory = new DirectoryInfo(IOPath.Combine(rootDirectory.FullName,
                                                                "usr", "share", "icons", "hicolor",
                                                                $"{size}x{size}", "apps"));
            iconDirectory.Create();

            var iconSource = new FileInfo(context.Paths.RootDirectory.Combine("assets")
                                                       .CombineWithFilePath($"ckan-{size}.png")
                                                       .FullPath);
            foreach (var iconName in IconNames)
            {
                iconSource.CopyTo(IOPath.Combine(iconDirectory.FullName, $"{iconName}.png"), true);
            }
        }
    }

    private static void CopyDirectoryContents(DirectoryInfo source, DirectoryInfo destination)
    {
        foreach (var directory in source.GetDirectories("*", SearchOption.AllDirectories))
        {
            var relativePath = IOPath.GetRelativePath(source.FullName, directory.FullName);
            new DirectoryInfo(IOPath.Combine(destination.FullName, relativePath)).Create();
        }

        foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = IOPath.GetRelativePath(source.FullName, file.FullName);
            var targetPath   = IOPath.Combine(destination.FullName, relativePath);
            Directory.CreateDirectory(IOPath.GetDirectoryName(targetPath) ?? destination.FullName);
            file.CopyTo(targetPath, true);
        }
    }
}

[TaskName("LinuxGUIVisualTests")]
[TaskDescription("Run the Linux Avalonia visual regression tests")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(GenerateGlobalAssemblyVersionInfoTask))]
public sealed class LinuxGuiVisualTestsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetTest(context.Paths.LinuxGuiVisualTestsProject.FullPath, new DotNetTestSettings
        {
            Configuration = context.BuildConfiguration,
            Framework     = "net8.0",
            NoRestore     = true,
            NoLogo        = true,
            Verbosity     = DotNetVerbosity.Minimal,
        });
    }
}

[TaskName("Restore")]
[TaskDescription("Intermediate - Download dependencies")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetRestore(context.Solution, new DotNetRestoreSettings
        {
            PackagesDirectory = context.Paths.NugetDirectory,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                { "Configuration", context.BuildConfiguration },
            },
        });
    }
}

[TaskName("Generate-GlobalAssemblyVersionInfo")]
[TaskDescription("Intermediate - Calculate the version strings for the assembly")]
public sealed class GenerateGlobalAssemblyVersionInfoTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var metaDirectory = context.Paths.BuildDirectory.Combine("meta");
        context.CreateDirectory(metaDirectory);

        var version = context.GetVersion();
        context.CreateAssemblyInfo(
            metaDirectory.CombineWithFilePath("GlobalAssemblyVersionInfo.cs"),
            new AssemblyInfoSettings
            {
                Version = $"{version.Major}.{version.Minor}",
                FileVersion = version.HasMeta
                    ? $"{version.Major}.{version.Minor}.{version.Patch}{version.Meta}"
                    : $"{version.Major}.{version.Minor}.{version.Patch}",
                InformationalVersion = version.ToString(),
            });
    }
}

[TaskName("Build")]
[TaskDescription("Build the Linux product and test graph")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(GenerateGlobalAssemblyVersionInfoTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetBuild(context.Paths.TestsProject.FullPath, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            Framework     = "net8.0",
            NoRestore     = true,
        });
    }
}

[TaskName("Test")]
[TaskDescription("Build and run the Linux product tests")]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(TestUnitTestsOnlyTask))]
public sealed class TestTask : FrostingTask<BuildContext>;

[TaskName("Test-UnitTests")]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(TestUnitTestsOnlyTask))]
public sealed class TestUnitTestsTask : FrostingTask<BuildContext>;

[TaskName("Test+Only")]
[TaskDescription("Run tests without compiling")]
[IsDependentOn(typeof(TestUnitTestsOnlyTask))]
public sealed class TestOnlyTask : FrostingTask<BuildContext>;

[TaskName("Test-UnitTests+Only")]
[TaskDescription("Intermediate - Run tests without compiling")]
public sealed class TestUnitTestsOnlyTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var filter = context.Argument<string?>("filter", null);
        context.DotNetTest(context.Paths.TestsProject.FullPath, new DotNetTestSettings
        {
            Configuration    = context.BuildConfiguration,
            Framework        = "net8.0",
            NoRestore        = true,
            NoBuild          = true,
            NoLogo           = true,
            Filter           = filter,
            ResultsDirectory = context.Paths.TestResultsDirectory,
            Verbosity        = DotNetVerbosity.Minimal,
        });
    }
}
