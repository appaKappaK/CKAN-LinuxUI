# CKAN Linux GUI

This directory contains the Fedora/Linux-first Avalonia shell for CKAN.

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
detected, the browser falls back to `All`.

Actions are queued before they are applied. Use the `Preview` surface to inspect
what CKAN Linux is about to do:

- direct installs, updates, removals, and downloads
- required dependency installs, which CKAN resolves automatically
- removable auto-installed dependencies that are no longer needed
- conflicts or provider choices that need user input
- optional recommendations, suggestions, and supported integration mods

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

The `Mods` menu includes maintenance actions for large cleanup passes:

- `Clean up missing installed mods` immediately removes stale CKAN registry
  entries for CKAN-managed mods whose registered files were manually deleted,
  prunes stale autodetected DLL records, then reloads the browser.
- `Queue remove all installed mods` replaces the current apply queue with
  removals for CKAN-managed installed mods.

Removal previews also include auto-removable dependencies where the registry can
prove they are no longer required by anything that will remain installed.

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

By default this installs under `~/.local`, keeping it separate from the upstream
`ckan` command. If `~/.local/bin` is already on your `PATH`, you can launch it
as `ckan-linux`. Use `--prefix /usr/local` to install somewhere else.

## Development

Launch the Linux shell with an isolated XDG home so it does not share config,
cache, or app-data state with your normal CKAN setup:

```bash
./scripts/run-linuxgui-dev.sh
```

If the checked-in `LinuxGUI`, `App`, `Core`, or `PluginCompat` sources are newer
than the local framework-dependent dev build, the launcher automatically refreshes
`_build/out/CKAN-LinuxGUI/VSCodeIDE/bin/net8.0/` before starting the app.

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

The mod browser includes a Rust-generated catalog sidecar path for faster
catalog/search while CKAN's registry remains authoritative for details, installs,
and dependency resolution. The LinuxGUI sidecar reader is included in this
repository; the generator source is published at
[`appaKappaK/ckan-meta-rs`](https://github.com/appaKappaK/ckan-meta-rs).
Sidecar rows include browse-list metadata such as title, summary,
relationships, compatibility, release date, and download count. Generate the
file with `ckan-meta-rs` and point the GUI at it:

```bash
CKAN_CATALOG_INDEX_PATH=/path/to/catalog-index-latest.json ./scripts/run-linuxgui-dev.sh
```

If the sidecar is missing, invalid, or not configured, the browser uses the
normal CKAN metadata loader.

Catalog load timings are written to the dev session/debug logs. Look for these
prefixes when comparing normal mode to sidecar mode:

- `Mod catalog service list`
- `Mod catalog index direct build`
- `Mod catalog registry build`
- `LinuxGUI catalog load`
- `LinuxGUI catalog filter`

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
