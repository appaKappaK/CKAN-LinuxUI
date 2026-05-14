#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

DATA_HOME=${XDG_DATA_HOME:-"$HOME/.local/share"}
CONFIG_PATH=${CKAN_CONFIG_FILE:-"$DATA_HOME/CKAN/config.json"}
REPOS_DIR="$DATA_HOME/CKAN/repos"
CATALOG_INDEX_PATH=${CKAN_CATALOG_INDEX_PATH:-}
INSTANCE_NAME=""
ITERATIONS=3
KEEP_TEMP=0

usage() {
    cat <<EOF
Usage: $0 [options]

Benchmark LinuxGUI catalog list construction against the current CKAN cache.
This does not update repositories or write to the selected game instance.

Options:
  --config PATH          CKAN config.json path
  --repos PATH           CKAN repository cache directory
  --catalog-index PATH   Rust sidecar catalog-index JSON
  --instance NAME        Instance name to benchmark
  --iterations N         Iterations per path, default: 3
  --keep-temp            Keep generated benchmark project for debugging
  -h, --help             Show this help

Environment:
  CKAN_CONFIG_FILE
  CKAN_CATALOG_INDEX_PATH
  XDG_DATA_HOME
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --config)
            [[ $# -ge 2 ]] || { echo "Missing value for --config" >&2; exit 2; }
            CONFIG_PATH=$2
            shift 2
            ;;
        --repos)
            [[ $# -ge 2 ]] || { echo "Missing value for --repos" >&2; exit 2; }
            REPOS_DIR=$2
            shift 2
            ;;
        --catalog-index)
            [[ $# -ge 2 ]] || { echo "Missing value for --catalog-index" >&2; exit 2; }
            CATALOG_INDEX_PATH=$2
            shift 2
            ;;
        --instance)
            [[ $# -ge 2 ]] || { echo "Missing value for --instance" >&2; exit 2; }
            INSTANCE_NAME=$2
            shift 2
            ;;
        --iterations)
            [[ $# -ge 2 ]] || { echo "Missing value for --iterations" >&2; exit 2; }
            ITERATIONS=$2
            shift 2
            ;;
        --keep-temp)
            KEEP_TEMP=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

if ! [[ "$ITERATIONS" =~ ^[0-9]+$ ]] || [[ "$ITERATIONS" -lt 1 ]]; then
    echo "--iterations must be a positive integer" >&2
    exit 2
fi

find_catalog_index() {
    local candidate
    for candidate in \
        "$CATALOG_INDEX_PATH" \
        "$DATA_HOME/CKAN/catalog-index-latest.json" \
        "$DATA_HOME/CKAN/catalog-index.json" \
        "$HOME/GithubProjects/ckan-meta-rs-github/ckan-meta-rs/data/catalog-index-latest.json" \
        "$HOME/GithubProjects/ckan-meta-rs-github/ckan-meta-rs/data/catalog-index.json" \
        "$HOME/GithubProjects/ckan-meta-rs/data/catalog-index-latest.json" \
        "$HOME/GithubProjects/ckan-meta-rs/data/catalog-index.json" \
        "$REPO_ROOT/../ckan-meta-rs/data/catalog-index-latest.json" \
        "$REPO_ROOT/../ckan-meta-rs/data/catalog-index.json" \
        "$REPO_ROOT/../../ckan-meta-rs-github/ckan-meta-rs/data/catalog-index-latest.json" \
        "$REPO_ROOT/../../ckan-meta-rs-github/ckan-meta-rs/data/catalog-index.json"
    do
        if [[ -n "$candidate" && -f "$candidate" ]]; then
            printf '%s\n' "$candidate"
            return
        fi
    done
}

CATALOG_INDEX_PATH=$(find_catalog_index || true)

if [[ ! -f "$CONFIG_PATH" ]]; then
    echo "CKAN config not found: $CONFIG_PATH" >&2
    exit 1
fi

if [[ ! -d "$REPOS_DIR" ]]; then
    echo "CKAN repository cache directory not found: $REPOS_DIR" >&2
    exit 1
fi

TMP_DIR=$(mktemp -d "${TMPDIR:-/tmp}/ckan-linuxgui-catalog-bench.XXXXXX")
cleanup() {
    if [[ "$KEEP_TEMP" == "1" ]]; then
        echo "Kept temp benchmark project: $TMP_DIR" >&2
    else
        rm -rf "$TMP_DIR"
    fi
}
trap cleanup EXIT

cat > "$TMP_DIR/CatalogBench.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion>
    <Nullable>enable</Nullable>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$REPO_ROOT/App/CKAN-App.csproj">
      <SetTargetFramework>TargetFramework=net8.0</SetTargetFramework>
    </ProjectReference>
  </ItemGroup>
</Project>
EOF

cat > "$TMP_DIR/Program.cs" <<'EOF'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CKAN;
using CKAN.App.Models;
using CKAN.App.Services;
using CKAN.Configuration;

internal static class Program
{
    private sealed record Options(
        string ConfigPath,
        string ReposDir,
        string TempDataHome,
        string CatalogIndexPath,
        string InstanceName,
        int Iterations);

    private sealed record Result(
        string Name,
        string Source,
        int Items,
        long FirstMs,
        long BestMs,
        long AverageMs,
        long MaxMs,
        string Note);

    public static async Task<int> Main(string[] args)
    {
        var options = Parse(args);

        Environment.SetEnvironmentVariable("XDG_DATA_HOME", options.TempDataHome);
        Environment.SetEnvironmentVariable("CKAN_LINUX_DEV_NO_REGISTRY_LOCK", "1");
        Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", "");
        Directory.CreateDirectory(Path.Combine(options.TempDataHome, "CKAN"));

        var config = new JsonConfiguration(options.ConfigPath);
        var repoData = new RepositoryDataManager(options.ReposDir);
        var settings = new AppSettingsService(Path.Combine(options.TempDataHome, "linuxgui.settings.json"));
        using var game = new GameInstanceService(config, repoData, settings);

        await game.InitializeAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(options.InstanceName))
        {
            await game.SetCurrentInstanceAsync(options.InstanceName, CancellationToken.None);
        }

        game.RefreshCurrentRegistry();
        if (game.CurrentInstance == null)
        {
            Console.Error.WriteLine("No current CKAN instance was selected.");
            var names = game.Instances.Select(instance => instance.Name).ToList();
            if (names.Count > 0)
            {
                Console.Error.WriteLine("Available instances:");
                foreach (var name in names)
                {
                    Console.Error.WriteLine($"  {name}");
                }
            }
            return 1;
        }

        Console.WriteLine($"Instance: {game.CurrentInstance.Name}");
        Console.WriteLine($"Config:   {options.ConfigPath}");
        Console.WriteLine($"Repos:    {options.ReposDir}");
        Console.WriteLine($"Sidecar:  {(File.Exists(options.CatalogIndexPath) ? options.CatalogIndexPath : "(not found)")}");
        Console.WriteLine($"Runs:     {options.Iterations}");
        Console.WriteLine();

        var results = new List<Result>
        {
            await Measure("Installed snapshot",
                          "installed-registry",
                          options.Iterations,
                          () =>
                          {
                              Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", "");
                              return new ModCatalogService(game, new CatalogIndexService());
                          },
                          service => service.GetInstalledModListAsync(CancellationToken.None)),

            await Measure("CKAN registry cache",
                          "registry",
                          options.Iterations,
                          () =>
                          {
                              Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", "");
                              return new ModCatalogService(game, new CatalogIndexService());
                          },
                          service => service.GetAllModListAsync(CancellationToken.None)),
        };

        if (File.Exists(options.CatalogIndexPath))
        {
            results.Add(await Measure("Rust sidecar index",
                                      "catalog-index",
                                      options.Iterations,
                                      () =>
                                      {
                                          Environment.SetEnvironmentVariable("CKAN_CATALOG_INDEX_PATH", options.CatalogIndexPath);
                                          return new ModCatalogService(game, new CatalogIndexService());
                                      },
                                      service => service.GetAllModListAsync(CancellationToken.None)));
        }
        else
        {
            results.Add(new Result("Rust sidecar index", "skipped", 0, 0, 0, 0, 0, "no catalog-index file"));
        }

        PrintResults(results);
        return 0;
    }

    private static async Task<Result> Measure(
        string name,
        string expectedSource,
        int iterations,
        Func<ModCatalogService> serviceFactory,
        Func<ModCatalogService, Task<IReadOnlyList<ModListItem>>> action)
    {
        var times = new List<long>();
        int itemCount = 0;
        string source = expectedSource;

        for (var i = 0; i < iterations; ++i)
        {
            var service = serviceFactory();
            var watch = Stopwatch.StartNew();
            var items = await action(service);
            watch.Stop();

            itemCount = items.Count;
            source = service.LastSource;
            times.Add(watch.ElapsedMilliseconds);
        }

        return new Result(name,
                          source,
                          itemCount,
                          times[0],
                          times.Min(),
                          (long)Math.Round(times.Average()),
                          times.Max(),
                          "");
    }

    private static void PrintResults(IReadOnlyList<Result> results)
    {
        Console.WriteLine("| Path | Source | Items | First ms | Best ms | Avg ms | Max ms | Note |");
        Console.WriteLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var result in results)
        {
            Console.WriteLine($"| {result.Name} | {result.Source} | {result.Items} | {result.FirstMs} | {result.BestMs} | {result.AverageMs} | {result.MaxMs} | {result.Note} |");
        }
    }

    private static Options Parse(string[] args)
    {
        string ValueAfter(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }

        var iterationsText = ValueAfter("--iterations");
        return new Options(
            ConfigPath: ValueAfter("--config"),
            ReposDir: ValueAfter("--repos"),
            TempDataHome: ValueAfter("--temp-data-home"),
            CatalogIndexPath: ValueAfter("--catalog-index"),
            InstanceName: ValueAfter("--instance"),
            Iterations: int.TryParse(iterationsText, out var iterations) && iterations > 0 ? iterations : 3);
    }
}
EOF

echo "Benchmarking LinuxGUI catalog paths"
echo "  Config:   $CONFIG_PATH"
echo "  Repos:    $REPOS_DIR"
echo "  Sidecar:  ${CATALOG_INDEX_PATH:-"(not found)"}"
if [[ -n "$INSTANCE_NAME" ]]; then
    echo "  Instance: $INSTANCE_NAME"
else
    echo "  Instance: current/preferred from CKAN config"
fi
echo

dotnet run --project "$TMP_DIR/CatalogBench.csproj" --configuration Release -- \
    --config "$CONFIG_PATH" \
    --repos "$REPOS_DIR" \
    --temp-data-home "$TMP_DIR/data" \
    --catalog-index "${CATALOG_INDEX_PATH:-}" \
    --instance "$INSTANCE_NAME" \
    --iterations "$ITERATIONS"
