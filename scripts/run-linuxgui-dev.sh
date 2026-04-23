#!/usr/bin/env bash

set -euo pipefail

# ============================================================================
# CKAN Linux GUI Development Launcher Script
# 
# This script sets up a development environment for the CKAN Linux GUI
# application, providing:
#   - Intelligent build caching to avoid unnecessary rebuilds
#   - Flexible binary selection (dev vs self-contained)
#   - Comprehensive logging with rotation
#   - Environment setup for CKAN repository/downloads access
# ============================================================================

# shellcheck disable=SC1007
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
# shellcheck disable=SC1007
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

# Default environment paths
DEV_HOME=${CKAN_LINUX_DEV_HOME:-"$HOME/.ckan-linux-dev"}
HOST_DATA_HOME=${XDG_DATA_HOME:-"$HOME/.local/share"}
DEV_DATA_HOME="$DEV_HOME/data"
DEV_REPOS_DIR="$DEV_DATA_HOME/CKAN/repos"
DEV_DOWNLOADS_DIR="$DEV_DATA_HOME/CKAN/downloads"
HOST_REPOS_DIR="$HOST_DATA_HOME/CKAN/repos"
HOST_DOWNLOADS_DIR="$HOST_DATA_HOME/CKAN/downloads"
RUN_DIR="$DEV_HOME/run"

# Log configuration
DEV_LOG_CONFIG_SRC="$REPO_ROOT/LinuxGUI/log4net.linuxgui.dev.xml"
DEV_LOG_CONFIG_DEST="$RUN_DIR/log4net.linuxgui.xml"

# Build output paths (MSBuild conventions)
BUILD_BIN="$REPO_ROOT/_build/out/CKAN-LinuxGUI/VSCodeIDE/bin/net8.0/CKAN-LinuxGUI"
BUILD_DLL="$REPO_ROOT/_build/out/CKAN-LinuxGUI/VSCodeIDE/bin/net8.0/CKAN-LinuxGUI.dll"
PUBLISH_BIN="$REPO_ROOT/_build/publish/CKAN-LinuxGUI/linux-x64/CKAN-LinuxGUI"
PACKAGE_BIN="$REPO_ROOT/_build/package/ckan-linux/linux-x64/usr/lib/ckan-linux/CKAN-LinuxGUI"

# Files to monitor for build staleness (source files that trigger rebuilds)
BUILD_STAMP_TARGETS=(
    "$REPO_ROOT/LinuxGUI"
    "$REPO_ROOT/App"
    "$REPO_ROOT/Core"
    "$REPO_ROOT/PluginCompat"
)
BUILD_STAMP_PATTERNS=(
    '*.axaml'
    '*.cs'
    '*.csproj'
    '*.json'
    '*.props'
    '*.targets'
    '*.xaml'
)

# ============================================================================
# Configuration Options (set environment variables before sourcing this script)
# ============================================================================
: "${CKAN_LINUX_DEV_SKIP_BUILD:-0}"        # Skip build cache check if 1
: "${CKAN_LINUX_DEV_STREAM_STDIO:-0}"      # Stream output to terminal if 1
: "${CKAN_LINUX_DEV_LOG_ROTATE_SESSIONS:-5}"  # Keep last N session logs (default: 5)
: "${CKAN_LINUX_DEV_DEBUG_MODE:-0}"        # Enable verbose debug logging if 1

# ============================================================================
# Utility Functions
# ============================================================================

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

# Check if build is stale (sources modified since last compilation)
dev_build_is_stale() {
    [[ ! -f "$BUILD_DLL" ]] && return 0

    local target pattern
    for target in "${BUILD_STAMP_TARGETS[@]}"; do
        [[ -d "$target" ]] || continue
        for pattern in "${BUILD_STAMP_PATTERNS[@]}"; do
            if find "$target" -type f -name "$pattern" -newer "$BUILD_DLL" -print -quit | grep -q .; then
                return 0
            fi
        done
    done

    return 1
}

# ============================================================================
# Main Script Logic
# ============================================================================

maybe_refresh_dev_build() {
    # Skip build check if explicitly requested
    [[ "${CKAN_LINUX_DEV_SKIP_BUILD:-0}" == "1" ]] && return
    
    # Only check if dotnet runtime is available
    runtime_available || return

    if dev_build_is_stale; then
        echo "Refreshing LinuxGUI dev build..."
        dotnet build "$REPO_ROOT/LinuxGUI/CKAN-LinuxGUI.csproj" --no-restore
    fi
}

maybe_refresh_dev_build

# Determine which binary to use (prefer self-contained over framework-dependent)
latest_self_contained_bin=""
if self_contained_available "$PUBLISH_BIN"; then
    latest_self_contained_bin="$PUBLISH_BIN"
fi
if self_contained_available "$PACKAGE_BIN" && { [[ -z "$latest_self_contained_bin" ]] || [[ $(bin_mtime "$PACKAGE_BIN") -gt $(bin_mtime "$latest_self_contained_bin") ]]; }; then
    latest_self_contained_bin="$PACKAGE_BIN"
fi

# Select application command based on available binaries
if [[ -f "$BUILD_DLL" ]] && runtime_available; then
    APP_CMD=(dotnet "$BUILD_DLL")
elif [[ -n "$latest_self_contained_bin" ]]; then
    # Self-contained binary found - verify it's newer than dev build if both exist
    if [[ -x "$BUILD_BIN" && $(bin_mtime "$BUILD_BIN") -gt $(bin_mtime "$latest_self_contained_bin") ]]; then
        cat >&2 <<EOF
Latest LinuxGUI dev build is newer than the runnable self-contained binary.

Newest local build:
  $BUILD_DLL

Newest self-contained binary:
  $latest_self_contained_bin

Your shell can build the project, but the newer dev build is framework-dependent 
and is not being launched directly. Use the installed dotnet host, or rebuild 
the self-contained LinuxGUI binary:

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

# ============================================================================
# Environment Setup
# ============================================================================

mkdir -p "$DEV_DATA_HOME" "$DEV_HOME/config" "$DEV_HOME/cache" "$RUN_DIR" "$DEV_DATA_HOME/CKAN"

# Set up symlinks to host repository/downloads directories if needed
setup_symlink() {
    local dev_dir="$1"
    local host_dir="$2"
    
    [[ -L "$dev_dir" ]] && return  # Already a symlink
    
    # Only create link if host directory exists and target isn't already populated
    if [[ -d "$host_dir" ]]; then
        if [[ ! -e "$dev_dir" ]]; then
            ln -s "$host_dir" "$dev_dir"
        elif [[ -d "$dev_dir" && -z "$(find "$dev_dir" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
            rmdir "$dev_dir"
            ln -s "$host_dir" "$dev_dir"
        fi
    fi
}

setup_symlink "$DEV_REPOS_DIR" "$HOST_REPOS_DIR"
setup_symlink "$DEV_DOWNLOADS_DIR" "$HOST_DOWNLOADS_DIR"

# Export environment variables for CKAN to use our dev directories
export XDG_DATA_HOME="$DEV_DATA_HOME"
export XDG_CONFIG_HOME="$DEV_HOME/config"
export XDG_CACHE_HOME="$DEV_HOME/cache"

# ============================================================================
# Logging Setup with Rotation
# ============================================================================

if [[ -f "$DEV_LOG_CONFIG_SRC" ]]; then
    cp "$DEV_LOG_CONFIG_SRC" "$DEV_LOG_CONFIG_DEST"
fi

SESSION_STAMP=$(date +%Y%m%d-%H%M%S)
SESSION_LOG="$RUN_DIR/ckan-linux-session-$SESSION_STAMP.log"
LATEST_SESSION_LOG="$RUN_DIR/ckan-linux-session.log"
LATEST_DEBUG_LOG="$RUN_DIR/ckan-linux-debug-latest.log"

# Rotate old session logs (keep last N sessions)
if [[ "$CKAN_LINUX_DEV_LOG_ROTATE_SESSIONS" -gt 0 ]]; then
    log_count=0
    for old_log in "$RUN_DIR"/ckan-linux-session-*.log; do
        if [[ -f "$old_log" ]]; then
            ((log_count++)) || true
            if [[ $log_count -gt "$CKAN_LINUX_DEV_LOG_ROTATE_SESSIONS" ]]; then
                rm -f "$old_log"
            fi
        fi
    done
fi

# Create symlinks for latest logs
ln -sfn "$(basename "$SESSION_LOG")" "$LATEST_SESSION_LOG"
if [[ ! -L "$RUN_DIR/ckan-linux-debug.log" ]]; then
    ln -s "ckan-linux-debug.log" "$RUN_DIR/ckan-linux-debug-latest.log"
fi

# ============================================================================
# Debug Mode (optional verbose output)
# ============================================================================

log_session_line() {
    local line="$1"
    if [[ "$CKAN_LINUX_DEV_DEBUG_MODE" == "1" ]]; then
        printf '%s\n' "$line" >&2
    fi
    printf '%s\n' "$line" | tee -a "$SESSION_LOG"
}

log_session_line "==== CKAN Linux Dev Session ===="
log_session_line "timestamp: $(date --iso-8601=seconds)"
log_session_line "repo_root: $REPO_ROOT"
log_session_line "cwd: $RUN_DIR"
log_session_line "app_cmd: ${APP_CMD[*]} $*"
log_session_line "build_bin: $BUILD_BIN"
log_session_line "publish_bin: $PUBLISH_BIN"
log_session_line "package_bin: $PACKAGE_BIN"
log_session_line "xdg_data_home: $XDG_DATA_HOME"
log_session_line "xdg_config_home: $XDG_CONFIG_HOME"
log_session_line "xdg_cache_home: $XDG_CACHE_HOME"
log_session_line "dev_repos_dir: $DEV_REPOS_DIR"
log_session_line "host_repos_dir: $HOST_REPOS_DIR"
log_session_line "dev_downloads_dir: $DEV_DOWNLOADS_DIR"
log_session_line "host_downloads_dir: $HOST_DOWNLOADS_DIR"

# Show symlink targets if applicable
if [[ -L "$DEV_REPOS_DIR" ]]; then
    log_session_line "dev_repos_link_target: $(readlink -f "$DEV_REPOS_DIR")"
fi
if [[ -L "$DEV_DOWNLOADS_DIR" ]]; then
    log_session_line "dev_downloads_link_target: $(readlink -f "$DEV_DOWNLOADS_DIR")"
fi

log_session_line "session_log: $SESSION_LOG"
log_session_line "latest_session_log: $LATEST_SESSION_LOG"
log_session_line "debug_log: $RUN_DIR/ckan-linux-debug.log"
log_session_line "latest_debug_log: $LATEST_DEBUG_LOG"
log_session_line "stream_stdio_to_terminal: $STREAM_STDIO_TO_TERMINAL"
log_session_line "==============================="

# Redirect output based on configuration
if [[ "$STREAM_STDIO_TO_TERMINAL" == "1" ]]; then
    exec > >(tee -a "$SESSION_LOG") 2>&1
else
    exec >>"$SESSION_LOG" 2>&1
fi

exec "${APP_CMD[@]}" "$@"