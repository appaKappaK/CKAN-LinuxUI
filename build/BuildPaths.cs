using Cake.Common;
using Cake.Core.IO;

namespace Build;

public class BuildPaths
{
    public DirectoryPath RootDirectory { get; init; }
    public FilePath CoreProject { get; }
    public FilePath NetkanProject { get; }
    public FilePath CmdlineProject { get; }
    public FilePath LinuxGuiProject { get; }
    public FilePath LinuxGuiVisualTestsProject { get; }
    public FilePath TestsProject { get; }
    public DirectoryPath BuildDirectory { get; }
    public DirectoryPath NugetDirectory { get; }
    public DirectoryPath OutDirectory { get; }
    public DirectoryPath TestResultsDirectory => BuildDirectory.Combine("test")
                                                               .Combine("results");

    public DirectoryPath LinuxGuiPublishDirectory(string runtime)
        => BuildDirectory.Combine("publish")
                         .Combine("CKAN-LinuxGUI")
                         .Combine(runtime);

    public DirectoryPath LinuxGuiPackageDirectory(string runtime)
        => BuildDirectory.Combine("package")
                         .Combine("ckan-linux")
                         .Combine(runtime);

    public DirectoryPath CmdlinePublishDirectory(string runtime)
        => BuildDirectory.Combine("publish")
                         .Combine("CKAN-CmdLine")
                         .Combine(runtime);

    public DirectoryPath NetkanPublishDirectory(string runtime)
        => BuildDirectory.Combine("publish")
                         .Combine("CKAN-NetKAN")
                         .Combine(runtime);

    public BuildPaths(DirectoryPath rootDirectory)
    {
        RootDirectory = rootDirectory;
        CoreProject = rootDirectory.Combine("Core")
                                   .CombineWithFilePath("CKAN-core.csproj");
        NetkanProject = rootDirectory.Combine("Netkan")
                                     .CombineWithFilePath("CKAN-netkan.csproj");
        CmdlineProject = rootDirectory.Combine("Cmdline")
                                      .CombineWithFilePath("CKAN-cmdline.csproj");
        LinuxGuiProject = rootDirectory.Combine("LinuxGUI")
                                      .CombineWithFilePath("CKAN-LinuxGUI.csproj");
        LinuxGuiVisualTestsProject = rootDirectory.Combine("LinuxGUI.VisualTests")
                                                  .CombineWithFilePath("CKAN-LinuxGUI.VisualTests.csproj");
        TestsProject = rootDirectory.Combine("Tests")
                                    .CombineWithFilePath("Tests.csproj");
        BuildDirectory = rootDirectory.Combine("_build");
        NugetDirectory = BuildDirectory.Combine("lib").Combine("nuget");
        OutDirectory = BuildDirectory.Combine("out");
    }
}
