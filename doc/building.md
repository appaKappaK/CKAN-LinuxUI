# Building CKAN Linux

CKAN Linux uses the .NET 8 SDK and a Cake Frosting build project. Mono,
WinForms, ILRepack, and the old combined `ckan.exe` are not part of this fork's
build.

## Requirements

- A Linux x86-64 environment
- .NET 8 SDK
- A POSIX-compatible shell
- Git

All generated files are written under `_build/`.

## Main desktop build

The default target publishes the self-contained Avalonia app and assembles its
install-shaped package layout:

```bash
./build.sh --configuration=Release
```

Outputs:

- `_build/publish/CKAN-LinuxGUI/linux-x64/`
- `_build/package/ckan-linux/linux-x64/`

Use `./build.sh LinuxGUI` to publish without staging the package, or
`./build.sh LinuxGUIPackage` to name the package target explicitly.

## Optional CLI

```bash
./build.sh CLI --configuration=Release
_build/publish/CKAN-CmdLine/linux-x64/CKAN-CmdLine version
```

The CLI is a separate self-contained Linux executable. It is retained for
scripting and recovery and does not launch a graphical or terminal UI.

## Tests

```bash
./build.sh Test --configuration=Release
```

This builds the net8 product/test dependency graph and runs the NUnit suite.
To rerun an already built test graph, use `./build.sh Test+Only`.

Visual regression tests are separate:

```bash
./build.sh LinuxGUIVisualTests --configuration=Release
```

## Optional maintainer tooling

NetKAN metadata tooling remains available but is not a default product output:

```bash
./build.sh Netkan --configuration=Release
```

Its self-contained output is written to
`_build/publish/CKAN-NetKAN/linux-x64/`.

## Distribution packages

After producing the LinuxGUI package layout:

```bash
./build.sh deb --configuration=Release
./build.sh rpm --configuration=Release
```

These packages contain `ckan-linux` only. They do not install a `ckan` command,
Mono runtime, WinForms client, or ConsoleUI.
