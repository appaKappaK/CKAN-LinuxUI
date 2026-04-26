# CKAN LinuxGUI

CKAN LinuxGUI is the Linux desktop app for the Comprehensive Kerbal Archive
Network (CKAN). Its desktop entry point is `ckan-linux`, which launches the
self-contained `CKAN-LinuxGUI` Avalonia app and uses the existing CKAN core,
metadata, and install logic.

This fork uses the Avalonia LinuxGUI as the default desktop experience for CKAN
on Linux. In Debian packages built from this fork, launching `/usr/bin/ckan`
with no arguments from a graphical session opens `/usr/bin/ckan-linux`.
Command-style usage, such as `ckan install`, `ckan remove`, or any other
invocation with arguments, still runs through the existing Mono `ckan.exe`
command-line path. If no graphical display is available, a no-argument launch
opens the console UI instead. If `ckan-linux` is unavailable, the wrapper falls
back to the legacy `ckan.exe gui` path.

## Desktop App Quick Start

```bash
git clone https://github.com/appaKappaK/CKAN-LinuxUI.git
cd CKAN-LinuxUI
./scripts/install-linuxgui-local.sh
~/.local/bin/ckan-linux
```

You do not need a separate upstream CKAN checkout. This fork already contains
the CKAN core alongside the LinuxGUI shell. If `~/.local/bin` is already on your
`PATH`, you can also launch the app as `ckan-linux`.

## Entry Point

The explicit desktop command is:

```bash
ckan-linux
```

That command is installed from `LinuxGUI/packaging/ckan-linux`. It resolves the
installed app directory and execs:

```text
usr/lib/ckan-linux/CKAN-LinuxGUI
```

The .NET entry point for that binary is `LinuxGUI/Program.cs`; it initializes
logging and starts the Avalonia desktop lifetime. `LinuxGUI/App.axaml.cs` then
builds the app services and opens `MainWindow`.

For Debian packages produced by this fork, `/usr/bin/ckan` is also wired to use
this replacement desktop UI when no command line arguments are supplied and a
Wayland or X11 display is available:

- `ckan` with no args and a display: opens `/usr/bin/ckan-linux`.
- `ckan` with no args and a display but no `ckan-linux`: falls back to
  `ckan.exe gui`.
- `ckan` with args: runs the existing Mono `ckan.exe` command path.
- `ckan` with no args and no display: runs `ckan consoleui`.

## Desktop App Paths

For normal local use, install the desktop app into `~/.local` and launch it as
`~/.local/bin/ckan-linux`:

```bash
./scripts/install-linuxgui-local.sh
~/.local/bin/ckan-linux
```

For package-oriented builds, generate the staged Linux desktop layout:

```bash
./build.sh LinuxGUIPackage --configuration=Release
```

That produces `_build/package/ckan-linux/linux-x64/`, including:

- `usr/bin/ckan-linux`
- `usr/lib/ckan-linux/`
- `usr/share/applications/ckan-linux.desktop`
- `usr/share/icons/hicolor/*/apps/ckan-linux.png`

## What This Fork Provides

- A native Linux desktop app built with Avalonia in `LinuxGUI/`.
- A local installer that builds LinuxGUI and installs `ckan-linux` under
  `~/.local` by default.
- A package layout under `_build/package/ckan-linux/linux-x64/` with the
  launcher, runtime files, icons, and desktop entry.
- Debian packaging that routes graphical no-argument `ckan` launches to
  `ckan-linux` while keeping argument-driven command and console behavior
  intact.
- Visual coverage for the LinuxGUI in `LinuxGUI.VisualTests/`.

## Desktop App Development

Build the self-contained desktop app package layout:

```bash
./build.sh LinuxGUIPackage --configuration=Release
```

Run the LinuxGUI visual tests:

```bash
./build.sh LinuxGUIVisualTests
```

See [`LinuxGUI/README.md`](LinuxGUI/README.md) for the full LinuxGUI build,
packaging, development, logging, and visual-test workflow.

## Upstream CKAN Context

[<img src="https://img.shields.io/github/downloads/KSP-CKAN/CKAN/total.svg?label=%E2%A4%93Download&style=plastic" height="48px" style="height:48px;" />](https://github.com/KSP-CKAN/CKAN/releases/latest)

[![Coverage Status](https://coveralls.io/repos/github/KSP-CKAN/CKAN/badge.svg?branch=master)](https://coveralls.io/github/KSP-CKAN/CKAN?branch=master)
[![NuGet Version](https://img.shields.io/nuget/v/CKAN?label=NuGet&style=plastic&logo=nuget)](https://www.nuget.org/packages/CKAN)
[![Crowdin](https://img.shields.io/badge/Crowdin-2E3340.svg?plastic&logo=Crowdin&logoColor=white)](https://crowdin.com/project/ckan)


[Click here to open a new CKAN issue][6]

[Click here to go to the CKAN wiki][5]

[Click here to view the CKAN metadata specification](Spec.md)

## What's the CKAN?

The CKAN is a metadata repository and associated tools to allow you to find, install, and manage mods for Kerbal Space Program.
It provides strong assurances that mods are installed in the way prescribed by their metadata files,
for the correct version of Kerbal Space Program, alongside their dependencies, and without any conflicting mods.

CKAN is great for players _and_ for authors:

- players can find new content and install it with just a few clicks;
- modders don't have to worry about misinstall problems or outdated versions;

The CKAN has been inspired by the solid and proven metadata formats from both the Debian project and the CPAN, each of which manages tens of thousands of packages.

## What's the status of the CKAN?

The CKAN is currently under [active development][1].
We very much welcome contributions, discussions, and especially pull-requests.

## The CKAN spec

At the core of the CKAN is the **[metadata specification](Spec.md)**,
which comes with a corresponding [JSON Schema](CKAN.schema) that you can also find in the [Schema Store][8]

This repository includes a validator that you can use to [validate your files][3].

## CKAN for players

CKAN can download, install and update mods in just a few clicks. See the [User guide][2] to get started with CKAN.

## CKAN for modders

While anyone can contribute metadata for your mod, we believe that you know your mod best.
So while contributors will endeavor to be as accurate as possible, we would appreciate any efforts made by mod authors to ensure our metadata's accuracy.
If the metadata we have is incorrect please [open an issue][7] and let us know.

## Contributing to CKAN

**No technical expertise is required to contribute to CKAN**

If you want to contribute, please read our [CONTRIBUTING][4] file.

## Thanks

Our sincere thanks to [SignPath.io][10] for allowing us to use their free code signing service, and to [the SignPath Foundation][11] for giving us a free code signing certificate!

---

Note: Are you looking for the Open Data portal software called CKAN? If so, their GitHub repository is found [here][9].

 [1]: https://github.com/KSP-CKAN/CKAN/commits/master
 [2]: https://github.com/KSP-CKAN/CKAN/wiki/User-guide
 [3]: https://github.com/KSP-CKAN/CKAN/wiki/Adding-a-mod-to-the-CKAN#verifying-metadata-files
 [4]: https://github.com/KSP-CKAN/.github/blob/master/CONTRIBUTING.md
 [5]: https://github.com/KSP-CKAN/CKAN/wiki
 [6]: https://github.com/KSP-CKAN/CKAN/issues/new
 [7]: https://github.com/KSP-CKAN/NetKAN/issues/new
 [8]: https://schemastore.org/
 [9]: https://github.com/ckan/ckan
 [10]: https://signpath.io/
 [11]: https://signpath.org/
