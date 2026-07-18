using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;

namespace Build;

public partial class BuildContext : FrostingContext
{
    public string Target { get; }

    // Named to avoid conflict with ICakeContext.Configuration.
    public string BuildConfiguration { get; set; }
    public string Solution { get; }
    public BuildPaths Paths { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        var rootDir = context.Environment.WorkingDirectory.GetParent();

        Target = context.Argument("target", "Default");
        var requestedConfiguration = context.Argument<string?>("configuration", null);
        Solution = context.Argument("solution", rootDir.CombineWithFilePath("CKAN.sln").FullPath);

        if (string.Equals(Target, "Release", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedConfiguration != null)
            {
                context.Warning($"Ignoring configuration argument: '{requestedConfiguration}'");
            }
            BuildConfiguration = "Release";
        }
        else if (string.Equals(Target, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedConfiguration != null)
            {
                context.Warning($"Ignoring configuration argument: '{requestedConfiguration}'");
            }
            BuildConfiguration = "Debug";
        }
        else
        {
            BuildConfiguration = requestedConfiguration ?? "Debug";
        }

        Paths = new BuildPaths(rootDir);
    }

    public SemVersion GetVersion(bool withBuild = true)
    {
        var rootDirectory = Environment.WorkingDirectory.GetParent();
        var versionMatch = File.ReadAllLines(rootDirectory.CombineWithFilePath("CHANGELOG.md").FullPath)
                               .Select(line => VersionRegex().Match(line))
                               .First(match => match.Success);

        if (!SemVersion.TryParse(versionMatch.Groups["version"].Value, out var version))
        {
            throw new Exception("Could not parse version from CHANGELOG.md");
        }

        if (withBuild && this.DirectoryExists(rootDirectory.Combine(".git")))
        {
            var commitDate = this.GitLogTip(rootDirectory).Committer.When;
            version = new SemVersion(version.Major,
                                     version.Minor,
                                     version.Patch,
                                     version.PreRelease,
                                     "." + commitDate.ToString("yy")
                                         + commitDate.DayOfYear.ToString("000"));
        }

        return version;
    }

    public static void ChmodExecutable(FilePath path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var process = Process.Start(new ProcessStartInfo("chmod", $"+x \"{path}\"")
            {
                UseShellExecute = false,
            });
            process?.WaitForExit();
        }
    }

    public IReadOnlyList<string> RunExecutable(FilePath                     executable,
                                               string                       arguments,
                                               Dictionary<string, string?>? environmentVariables = null,
                                               DirectoryPath?               workingDirectory = null)
    {
        var exitCode = this.StartProcess(
            executable,
            new ProcessSettings
            {
                Arguments = arguments,
                RedirectStandardOutput = true,
                EnvironmentVariables = environmentVariables ?? new Dictionary<string, string?>(),
                WorkingDirectory = workingDirectory,
            },
            out IEnumerable<string> output);

        var lines = output.ToArray();
        if (exitCode != 0)
        {
            throw new Exception($"Process failed with exit code {exitCode}: {string.Join(System.Environment.NewLine, lines)}");
        }

        return lines;
    }

    [GeneratedRegex(@"^\s*##\s+v(?<version>\S+)\s?.*$")]
    private static partial Regex VersionRegex();
}
