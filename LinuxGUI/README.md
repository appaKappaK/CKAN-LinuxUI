# CKAN Linux GUI

This directory contains the Fedora/Linux-first Avalonia shell for CKAN.

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

## Visual Review

Run deterministic visual tests and refresh the optional Gemini review bundle:

```bash
./build.sh LinuxGUIVisualTests
```

Outputs:

- `.gemini-review/`
- `_build/visual-tests/actual/`

Generate implementation-oriented Gemini output from the curated screenshots and
focused code bundle:

```bash
python3 scripts/gemini_ui_work.py --force
```

Output:

- `.gemini-review/work/latest.md`
- `.gemini-review/work/request.md`

## Isolated Dev Run

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

Skip the automatic rebuild check if you deliberately want to launch the current
artifacts as-is:

```bash
CKAN_LINUX_DEV_SKIP_BUILD=1 ./scripts/run-linuxgui-dev.sh
```

## Settings and Logs

The Linux shell keeps its own UI settings separate from the legacy GUI. By
default it writes:

- settings: `~/.local/share/CKAN/linuxgui.settings.json`
- shared CKAN data: `~/.local/share/CKAN/`

The shell now also stores a persisted display-scale setting in that
`linuxgui.settings.json` file. Use the `Display Scale` section in the left rail,
pick a smaller or larger scale, then use `Restart to Apply` to relaunch the app
with the new scale.

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
