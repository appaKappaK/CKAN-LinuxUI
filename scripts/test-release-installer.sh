#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
stage="$tmp/stage"
prefix="$tmp/local"
mkdir -p "$stage/bin" "$stage/lib/ckan-linux" "$stage/share/applications" "$stage/share/icons/hicolor/16x16/apps"
printf '#!/bin/sh\nexit 0\n' > "$stage/bin/ckan-linux"
printf '#!/bin/sh\nexit 0\n' > "$stage/lib/ckan-linux/CKAN-LinuxGUI"
chmod +x "$stage/bin/ckan-linux" "$stage/lib/ckan-linux/CKAN-LinuxGUI"
printf '[Desktop Entry]\nExec=/usr/bin/ckan-linux\nTryExec=/usr/bin/ckan-linux\n' > "$stage/share/applications/ckan-linux.desktop"
printf icon > "$stage/share/icons/hicolor/16x16/apps/ckan-linux.png"
mkdir -p "$prefix/lib/ckan-linux" "$prefix/share/ckan"
printf old > "$prefix/lib/ckan-linux/old"
printf userdata > "$prefix/share/ckan/userdata"
bash "$root/scripts/install-release-payload.sh" "$stage" "$prefix"
test -x "$prefix/bin/ckan-linux"
test -x "$prefix/lib/ckan-linux/CKAN-LinuxGUI"
test ! -e "$prefix/lib/ckan-linux/old"
test "$(cat "$prefix/share/ckan/userdata")" = userdata
grep -Fx "Exec=\"$prefix/bin/ckan-linux\"" "$prefix/share/applications/ckan-linux.desktop"
test -f "$prefix/share/icons/hicolor/16x16/apps/ckan-linux.png"
rm "$stage/lib/ckan-linux/CKAN-LinuxGUI"
if bash "$root/scripts/install-release-payload.sh" "$stage" "$prefix"; then
    echo 'Missing executable was accepted' >&2
    exit 1
fi
test -x "$prefix/lib/ckan-linux/CKAN-LinuxGUI"
echo 'Release installer checks passed.'
