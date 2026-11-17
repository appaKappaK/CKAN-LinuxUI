#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
out="$root/_build/release-installer"
bundle="$out/CKAN-Linux-Installer"
archive="$out/CKAN-Linux-Installer-linux-x64.zip"
cd "$root"
./build.sh LinuxGUIPackage --configuration=Release
rm -rf "$bundle"
mkdir -p "$bundle/install-files"
dotnet publish LinuxInstaller/CKAN-LinuxInstaller.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$bundle"
find "$bundle" -maxdepth 1 -name '*.pdb' -delete
cp -a _build/package/ckan-linux/linux-x64/usr/. "$bundle/install-files/"
cp scripts/install-release-files.sh "$bundle/install-files.sh"
cat /proc/sys/kernel/random/uuid > "$bundle/.ckan-linux-installer-bundle"
chmod +x "$bundle/CKAN-Linux-Installer"
rm -f "$archive"
(cd "$out" && zip -qr "$archive" CKAN-Linux-Installer)
printf 'Installer ZIP: %s\n' "$archive"
