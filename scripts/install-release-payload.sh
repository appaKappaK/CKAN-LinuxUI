#!/usr/bin/env bash
set -euo pipefail
stage=${1:?Missing package directory}
prefix=${2:?Missing installation prefix}
if [[ ! -x "$stage/bin/ckan-linux" || ! -x "$stage/lib/ckan-linux/CKAN-LinuxGUI" || ! -f "$stage/share/applications/ckan-linux.desktop" ]]; then
    echo 'The installer package is incomplete. Extract the complete ZIP and try again.' >&2
    exit 1
fi
mkdir -p "$prefix/bin" "$prefix/lib" "$prefix/share/applications" "$prefix/share/icons"
temp=$(mktemp -d "$prefix/lib/.ckan-linux-install.XXXXXXXX")
trap 'rm -rf "$temp"' EXIT
cp -a "$stage/lib/ckan-linux/." "$temp/"
rm -rf "$prefix/lib/ckan-linux"
mv "$temp" "$prefix/lib/ckan-linux"
install -m 755 "$stage/bin/ckan-linux" "$prefix/bin/ckan-linux"
cp -a "$stage/share/icons/." "$prefix/share/icons/"
# Desktop entries require escaping spaces and backslashes in executable paths.
launcher="$prefix/bin/ckan-linux"
launcher=${launcher//\\/\\\\}
launcher=${launcher//\"/\\\"}
sed -e "s|^Exec=.*|Exec=\"$launcher\"|" -e "s|^TryExec=.*|TryExec=\"$launcher\"|" "$stage/share/applications/ckan-linux.desktop" > "$prefix/share/applications/ckan-linux.desktop"
