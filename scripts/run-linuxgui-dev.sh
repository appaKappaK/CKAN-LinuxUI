#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
DEV_HOME=${CKAN_LINUX_DEV_HOME:-"$HOME/.ckan-linux-dev"}

BUILD_BIN="$REPO_ROOT/_build/out/CKAN-LinuxGUI/VSCodeIDE/bin/net8.0/CKAN-LinuxGUI"
BUILD_DLL="$REPO_ROOT/_build/out/CKAN-LinuxGUI/VSCodeIDE/bin/net8.0/CKAN-LinuxGUI.dll"
PUBLISH_BIN="$REPO_ROOT/_build/publish/CKAN-LinuxGUI/linux-x64/CKAN-LinuxGUI"
PACKAGE_BIN="$REPO_ROOT/_build/package/ckan-linux/linux-x64/usr/lib/ckan-linux/CKAN-LinuxGUI"

bin_mtime() {
    stat -c '%Y' "$1"
}

runtime_available() {
    command -v dotnet >/dev/null 2>&1
}

self_contained_available() {
    local bin="$1"
    [[ -x "$bin" && -f "$(dirname "$bin")/libhostfxr.so" ]]
}

latest_self_contained_bin=""
if self_contained_available "$PUBLISH_BIN"; then
    latest_self_contained_bin="$PUBLISH_BIN"
fi
if self_contained_available "$PACKAGE_BIN" && { [[ -z "$latest_self_contained_bin" ]] || [[ $(bin_mtime "$PACKAGE_BIN") -gt $(bin_mtime "$latest_self_contained_bin") ]]; }; then
    latest_self_contained_bin="$PACKAGE_BIN"
fi

if [[ -f "$BUILD_DLL" ]] && runtime_available; then
    APP_CMD=(dotnet "$BUILD_DLL")
elif [[ -n "$latest_self_contained_bin" ]]; then
    if [[ -x "$BUILD_BIN" && $(bin_mtime "$BUILD_BIN") -gt $(bin_mtime "$latest_self_contained_bin") ]]; then
        cat >&2 <<EOF
Latest LinuxGUI dev build is newer than the runnable self-contained binary.

Newest local build:
  $BUILD_DLL

Newest self-contained binary:
  $latest_self_contained_bin

Your shell can build the project, but the newer dev build is framework-dependent and is not being launched directly.
Use the installed dotnet host, or rebuild the self-contained LinuxGUI binary:
  dotnet build LinuxGUI/CKAN-LinuxGUI.csproj --no-restore
  ./build.sh LinuxGUI --configuration=Release
  ./build.sh LinuxGUIPackage --configuration=Release
EOF
        exit 1
    fi

    APP_CMD=("$latest_self_contained_bin")
else
    cat >&2 <<'EOF'
CKAN Linux dev binary not found.

Build one of these first:
  dotnet build LinuxGUI/CKAN-LinuxGUI.csproj --no-restore
  ./build.sh LinuxGUI --configuration=Release
  ./build.sh LinuxGUIPackage --configuration=Release
EOF
    exit 1
fi

mkdir -p "$DEV_HOME/data" "$DEV_HOME/config" "$DEV_HOME/cache" "$DEV_HOME/run"

export XDG_DATA_HOME="$DEV_HOME/data"
export XDG_CONFIG_HOME="$DEV_HOME/config"
export XDG_CACHE_HOME="$DEV_HOME/cache"

cd "$DEV_HOME/run"
exec "${APP_CMD[@]}" "$@"
