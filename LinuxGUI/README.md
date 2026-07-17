# CKAN Linux GUI

This directory contains the Linux-first Avalonia shell for CKAN.

## Entry Points

The installed desktop command is `ckan-linux`. The package launcher lives at
`usr/bin/ckan-linux` and execs the self-contained Avalonia binary at
`usr/lib/ckan-linux/CKAN-LinuxGUI`.

The app binary starts in `LinuxGUI/Program.cs`, which initializes LinuxGUI
logging and starts Avalonia with the classic desktop lifetime. `App.axaml.cs`
then registers the app services and opens `Shell/MainWindow.axaml`.

When this repository's Debian package installs `/usr/bin/ckan-linux`, the
`/usr/bin/ckan` wrapper uses it as the replacement UI for graphical no-argument
launches. Command line arguments still run through the existing Mono `ckan.exe`
path, and no-display launches continue to use `ckan consoleui`.

## Source Layout

The Linux GUI app keeps startup files at the `LinuxGUI/` root and groups the
desktop surface by role:

- `Shell/` contains `MainWindow`, its view model partials, and startup shell
  state.
- `Windows/` contains secondary dialogs and utility windows.
- `Models/` contains Linux GUI view-model support items used by bindings.
- `Services/` contains Linux GUI service adapters and controller glue.

## Current Desktop Flow

The app starts on the mod browser for the current instance. When no saved
browser filter is active, the default mod list is `Installed`, so cleanup and
updates are the first view instead of the full catalog. If no installed mods are
detected, the browser falls back to `All`. Installed startup snapshots are
sorted by mod name from A-Z before the full catalog data is available.

If no active instance is selected, or if the saved/default instance cannot be
activated, LinuxGUI opens a first-run instance picker over the normal browser
shell. Pick a registered instance from the table, then use `Open Install` to
load the mod browser. Invalid or unavailable instance paths are reported inline
in the picker instead of taking over the app with a separate error page.

The first-run picker can be stress-tested in development with fake entries:

```bash
CKAN_LINUX_DEV_FAKE_INSTANCES=1 ./scripts/run-linuxgui-dev.sh
```

Fake entries are only for dev UI testing; they are not written as real CKAN
instances.

Refreshing the catalog temporarily replaces the browser rows with the matching
skeleton table until the reload finishes. During that work the header action
changes from `Reload` to `Reloading...`. On cold start, catalog loading uses
`Loading...` instead so the first load does not read like a user-triggered
reload. An explicit reload now reopens the current CKAN registry from disk
before rebuilding the browser and rescans unmanaged `GameData` DLLs, so changes
made outside the current session, including manually installed DLL mods such as
`scatterer`, are reflected without closing and reopening CKAN Linux.

Actions are queued before they are applied. Use the `Preview` surface to inspect
what CKAN Linux is about to do:

- direct installs, updates, removals, and downloads
- required dependency installs, which CKAN resolves automatically
- dependent removals when a queued removal would break another installed mod
- removable auto-installed dependencies that are no longer needed
- conflicts or provider choices that need user input
- optional recommendations, suggestions, and supported integration mods

The mod browser supports normal desktop multi-selection. Shift-click selects a
continuous range, while Ctrl-click adds or removes individual rows. When more
than one mod is selected, the details pane offers separate bulk install, update,
and remove buttons for whichever selected mods are eligible for each action.

After an uninstall, CKAN may preserve directories containing unregistered
configuration files. The completion dialog lists those directories and offers
`Remove Leftover` or `Remove Leftovers`. Cleanup rechecks that the directories
are inside a mod folder and contain no files owned by an installed mod, then
removes the leftovers and any empty parent mod folders.

Optional recommendations, suggestions, and supported mods are informational.
Use the `View` button on each optional section to open Browse filtered to those
mods, queue any extras you want, then use the `Close` button in the notice above
the mod list to return to Preview. The `Apply Changes` button does not require
reviewing optional extras first.

Required virtual dependencies are different. If several mods provide the same
required dependency, apply opens a provider-choice dialog. The dialog is capped
to a scrollable list and shows the provider identifier, display name, and any
available CKAN hints such as version, cache state, download count, and summary
so large provider sets do not take over the screen.

Long overwrite and conflict-style prompts also use a scrollable detail block so
the confirmation buttons remain reachable even when the prompt includes a large
file list.

The `Mods` menu includes maintenance actions for large cleanup passes:

- `Clean up missing installed mods` immediately removes stale CKAN registry
  entries for CKAN-managed mods whose registered files were manually deleted,
  prunes stale autodetected DLL records, then reloads the browser.
- `Queue remove all installed mods` replaces the current apply queue with
  removals for CKAN-managed installed mods.

Removal previews also include dependent removals for installed mods that would
break, plus auto-removable dependencies where the registry can prove they are
no longer required by anything that will remain installed.

`Mods > Installation History` opens as a non-modal utility window. Keep it open
while browsing the mod list to cross-reference saved snapshots against current
mods without repeatedly closing and reopening the history window.

Installation-history snapshots are kept unless you opt in to cleanup. In
Settings, enable `Prune installation history older than 30 days` if you want
CKAN Linux to remove old snapshot files when opening history or when the option
is enabled.

The active filter popout includes `External` for catalogued mods that are
present locally but were not installed through CKAN. `Replaceable` appears only
when replacements exist. Cache filters look at the download cache itself, so a
mod can remain marked cached after uninstalling if a matching archive is still
available in the cache.

## Build

Build and publish the self-contained desktop shell:

```bash
./build.sh LinuxGUI --configuration=Release
```

Output:

- `_build/publish/CKAN-LinuxGUI/linux-x64/`

## Package Layout

Assemble an install-shaped Linux desktop layout with launcher, desktop entry,
icons, and documentation:

```bash
./build.sh LinuxGUIPackage --configuration=Release
```

Output:

- `_build/package/ckan-linux/linux-x64/`

The staged layout includes:

- `usr/bin/ckan-linux`
- `usr/lib/ckan-linux/`
- `usr/share/applications/ckan-linux.desktop`
- `usr/share/icons/hicolor/*/apps/ckan-linux.png`
- `usr/share/doc/ckan-linux/README.md`

## Local Install

Install the Linux GUI into a local prefix so it can be launched as
`~/.local/bin/ckan-linux` without typing the staged package path:

```bash
./scripts/install-linuxgui.sh
~/.local/bin/ckan-linux
```

By default this installs under `~/.local`, keeping it separate from any existing
system `ckan` command. If `~/.local/bin` is already on your `PATH`, you can
launch it as `ckan-linux`. Use `--prefix /usr/local` to install somewhere else.

## Development

Launch the Linux shell with an isolated XDG home so it does not share config,
cache, or app-data state with your normal CKAN setup:

```bash
./scripts/run-linuxgui-dev.sh
```

If the checked-in `LinuxGUI`, `App`, `Core`, or `PluginCompat` sources are newer
than the local framework-dependent dev build, the launcher automatically refreshes
`_build/out/CKAN-LinuxGUI/Debug/bin/net8.0/` before starting the app. Override
that with `CKAN_LINUX_DEV_BUILD_CONFIGURATION=Release` if you intentionally want
the dev launcher to track the Release output path.

By default this uses:

- `~/.ckan-linux-dev/data`
- `~/.ckan-linux-dev/config`
- `~/.ckan-linux-dev/cache`
- `~/.ckan-linux-dev/run`

You can override the base directory with:

```bash
CKAN_LINUX_DEV_HOME=/path/to/dev-home ./scripts/run-linuxgui-dev.sh
```

### Rust Catalog Sidecar

The mod browser can optionally use a Rust-generated catalog sidecar for faster
catalog/search. Normal LinuxGUI builds and installs do not require Rust, the
Rust repository, or a sidecar index; if no valid index is configured, the
browser uses CKAN's normal registry/repository cache.

The LinuxGUI sidecar reader is included in this repository. The external
generator source is published at
[`appaKappaK/ckan-meta-rs`](https://github.com/appaKappaK/ckan-meta-rs).
Sidecar rows include browse-list metadata such as title, summary,
relationships, compatibility, release date, and download count. CKAN's registry
remains authoritative for details, installs, updates, and dependency
resolution.
Schema v2 sidecars also contain stable, testing, and development candidates;
LinuxGUI selects them using the current instance's overall and per-mod stability
tolerances. Schema v1 sidecars remain readable with their original latest-row
behavior.

To use the sidecar, generate the file with `ckan-meta-rs` and point the GUI at
it:

```bash
CKAN_CATALOG_INDEX_PATH=/path/to/catalog-index-latest.json ./scripts/run-linuxgui-dev.sh
```

In dev runs, `scripts/run-linuxgui-dev.sh` also checks the normal host app-data
location, `${XDG_DATA_HOME:-$HOME/.local/share}/CKAN/catalog-index-latest.json`,
and links it into the isolated dev data home when no explicit
`CKAN_CATALOG_INDEX_PATH` is set.

For repeated local use outside the dev launcher, either keep using
`CKAN_CATALOG_INDEX_PATH` or symlink the generated file into app data:

```bash
mkdir -p ~/.local/share/CKAN
ln -s /path/to/ckan-meta-rs/data/catalog-index-latest.json \
      ~/.local/share/CKAN/catalog-index-latest.json
```

If the sidecar is missing, invalid, or not configured, the browser uses the
normal CKAN metadata loader.

If a configured sidecar predates CKAN's last repository-content update, the
browser also falls back to the normal metadata loader until the sidecar is
regenerated. The `ckan-meta-rs` checkout includes
`scripts/refresh-ckan-linux-sidecar.sh` for a validated atomic refresh.

Catalog load timings are written to the dev session/debug logs. Look for these
prefixes when comparing normal mode to sidecar mode:

- `Mod catalog service list`
- `Mod catalog index direct build`
- `Mod catalog registry build`
- `LinuxGUI catalog load`
- `LinuxGUI catalog filter`

The browser diagnostics text also includes whether the last load used the Rust
catalog-index sidecar or the CKAN registry fallback.

For repeatable local measurements, run:

```bash
./scripts/benchmark-linuxgui-catalog.sh --iterations 3
```

On 2026-05-13 against the `KSP-Steam` instance, 98 installed mods, and the
cached CKAN repository data in `~/.local/share/CKAN/repos`, the benchmark
reported:

| Path | Items | First run | Best run | Average |
| --- | ---: | ---: | ---: | ---: |
| Installed snapshot | 98 | 26 ms | 2 ms | 10 ms |
| CKAN registry cache | 3,505 | 6,345 ms | 2,886 ms | 4,057 ms |
| Rust sidecar index | 3,495 | 3,000 ms | 2,956 ms | 2,971 ms |

These numbers are local cache timings, not network update timings. The script
does not update repositories and uses read-only registry access so it can be run
while comparing catalog load paths.

Skip the automatic rebuild check if you deliberately want to launch the current
artifacts as-is:

```bash
CKAN_LINUX_DEV_SKIP_BUILD=1 ./scripts/run-linuxgui-dev.sh
```

Run deterministic visual tests:

```bash
./build.sh LinuxGUIVisualTests
```

Output:

- `_build/visual-tests/actual/`

## Settings and Logs

The Linux shell keeps its own UI settings separate from the legacy GUI. By
default it writes:

- settings: `~/.local/share/CKAN/linuxgui.settings.json`
- shared CKAN data: `~/.local/share/CKAN/`

The Linux shell initializes logging from `log4net.linuxgui.xml`. When you use
`./scripts/run-linuxgui-dev.sh`, the launcher now writes a comprehensive dev
session log and installs a debug-level log4net config in the isolated `run/`
directory.

The main dev log files are:

- session log: `~/.ckan-linux-dev/run/ckan-linux-session.log`
- per-run session log: `~/.ckan-linux-dev/run/ckan-linux-session-YYYYMMDD-HHMMSS.log`
- debug app log: `~/.ckan-linux-dev/run/ckan-linux-debug.log`

The session log captures launcher decisions, selected binary, XDG paths, and
all stdout/stderr from the app process. The debug app log captures log4net
output at `DEBUG` level.

By default, `run-linuxgui-dev.sh` now keeps the app's stdout/stderr out of the
terminal and writes it only to the session log. If you want the old mirrored
behavior for a debugging session, launch with:

```bash
CKAN_LINUX_DEV_STREAM_STDIO=1 ./scripts/run-linuxgui-dev.sh
```
