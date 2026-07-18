using System;
using System.Collections.Generic;

using Cake.Common;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build;

[TaskName("deb")]
[TaskDescription("Build the CKAN Linux deb package for Debian-based distros.")]
[IsDependentOn(typeof(LinuxGuiPackageTask))]
public sealed class DebTask() : MakeTask("debian", "package");

[TaskName("deb-test")]
[TaskDescription("Test the deb packaging.")]
[IsDependentOn(typeof(DebTask))]
public sealed class DebTestTask() : MakeTask("debian", "test");

[TaskName("deb-clean")]
[TaskDescription("Clean the deb output directory.")]
public sealed class DebCleanTask() : MakeTask("debian", "clean");

[TaskName("rpm")]
[TaskDescription("Build the CKAN Linux rpm package for RPM-based distros.")]
[IsDependentOn(typeof(LinuxGuiPackageTask))]
public sealed class RpmTask() : MakeTask("rpm", "package");

[TaskName("rpm-test")]
[TaskDescription("Test the rpm packaging.")]
public sealed class RpmTestTask() : MakeTask("rpm", "test");

[TaskName("rpm-clean")]
[TaskDescription("Clean the rpm package output directory.")]
public sealed class RpmCleanTask() : MakeTask("rpm", "clean");

public abstract class MakeTask(string location, ProcessArgumentBuilder? args = null) : FrostingTask<BuildContext>
{
    private string Location { get; } = location;
    private ProcessArgumentBuilder Args { get; } = args ?? "";

    public override void Run(BuildContext context)
    {
        var exitCode = context.StartProcess("make", new ProcessSettings() {
            WorkingDirectory = context.Paths.RootDirectory.Combine(Location),
            Arguments = Args,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                { "CONFIGURATION", context.BuildConfiguration },
            }
        });
        if (exitCode != 0)
        {
            throw new Exception("Make failed with exit code: " + exitCode);
        }
    }
}
