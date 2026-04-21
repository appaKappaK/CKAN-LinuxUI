#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
DEV_HOME=${CKAN_LINUX_DEV_HOME:-"$HOME/.ckan-linux-dev"}
HOST_DATA_HOME=${XDG_DATA_HOME:-"$HOME/.local/share"}
DEV_DATA_HOME="$DEV_HOME/data"
DEV_REPOS_DIR="$DEV_DATA_HOME/CKAN/repos"
DEV_DOWNLOADS_DIR="$DEV_DATA_HOME/CKAN/downloads"
HOST_REPOS_DIR="$HOST_DATA_HOME/CKAN/repos"
HOST_DOWNLOADS_DIR="$HOST_DATA_HOME/CKAN/downloads"
RUN_DIR="$DEV_HOME/run"
DEV_LOG_CONFIG_SRC="$REPO_ROOT/LinuxGUI/log4net.linuxgui.dev.xml"
DEV_LOG_CONFIG_DEST="$RUN_DIR/log4net.linuxgui.xml"

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

mkdir -p "$DEV_DATA_HOME" "$DEV_HOME/config" "$DEV_HOME/cache" "$RUN_DIR" "$DEV_DATA_HOME/CKAN"

if [[ ! -L "$DEV_REPOS_DIR" && -d "$HOST_REPOS_DIR" ]]; then
    if [[ ! -e "$DEV_REPOS_DIR" ]]; then
        ln -s "$HOST_REPOS_DIR" "$DEV_REPOS_DIR"
    elif [[ -d "$DEV_REPOS_DIR" && -z "$(find "$DEV_REPOS_DIR" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
        rmdir "$DEV_REPOS_DIR"
        ln -s "$HOST_REPOS_DIR" "$DEV_REPOS_DIR"
    fi
fi

if [[ ! -L "$DEV_DOWNLOADS_DIR" && -d "$HOST_DOWNLOADS_DIR" ]]; then
    if [[ ! -e "$DEV_DOWNLOADS_DIR" ]]; then
        ln -s "$HOST_DOWNLOADS_DIR" "$DEV_DOWNLOADS_DIR"
    elif [[ -d "$DEV_DOWNLOADS_DIR" && -z "$(find "$DEV_DOWNLOADS_DIR" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
        rmdir "$DEV_DOWNLOADS_DIR"
        ln -s "$HOST_DOWNLOADS_DIR" "$DEV_DOWNLOADS_DIR"
    fi
fi

export XDG_DATA_HOME="$DEV_DATA_HOME"
export XDG_CONFIG_HOME="$DEV_HOME/config"
export XDG_CACHE_HOME="$DEV_HOME/cache"

if [[ -f "$DEV_LOG_CONFIG_SRC" ]]; then
    cp "$DEV_LOG_CONFIG_SRC" "$DEV_LOG_CONFIG_DEST"
fi

SESSION_STAMP=$(date +%Y%m%d-%H%M%S)
SESSION_LOG="$RUN_DIR/ckan-linux-session-$SESSION_STAMP.log"
LATEST_SESSION_LOG="$RUN_DIR/ckan-linux-session.log"
LATEST_DEBUG_LOG="$RUN_DIR/ckan-linux-debug-latest.log"
ln -sfn "$(basename "$SESSION_LOG")" "$LATEST_SESSION_LOG"
ln -sfn "ckan-linux-debug.log" "$LATEST_DEBUG_LOG"

cd "$RUN_DIR"
exec > >(tee -a "$SESSION_LOG") 2>&1

echo "==== CKAN Linux Dev Session ===="
echo "timestamp: $(date --iso-8601=seconds)"
echo "repo_root: $REPO_ROOT"
echo "cwd: $RUN_DIR"
echo "app_cmd: ${APP_CMD[*]} $*"
echo "build_bin: $BUILD_BIN"
echo "publish_bin: $PUBLISH_BIN"
echo "package_bin: $PACKAGE_BIN"
echo "xdg_data_home: $XDG_DATA_HOME"
echo "xdg_config_home: $XDG_CONFIG_HOME"
echo "xdg_cache_home: $XDG_CACHE_HOME"
echo "dev_repos_dir: $DEV_REPOS_DIR"
echo "host_repos_dir: $HOST_REPOS_DIR"
echo "dev_downloads_dir: $DEV_DOWNLOADS_DIR"
echo "host_downloads_dir: $HOST_DOWNLOADS_DIR"
if [[ -L "$DEV_REPOS_DIR" ]]; then
    echo "dev_repos_link_target: $(readlink -f "$DEV_REPOS_DIR")"
fi
if [[ -L "$DEV_DOWNLOADS_DIR" ]]; then
    echo "dev_downloads_link_target: $(readlink -f "$DEV_DOWNLOADS_DIR")"
fi
echo "session_log: $SESSION_LOG"
echo "latest_session_log: $LATEST_SESSION_LOG"
echo "debug_log: $RUN_DIR/ckan-linux-debug.log"
echo "latest_debug_log: $LATEST_DEBUG_LOG"
echo "==============================="

exec "${APP_CMD[@]}" "$@"
