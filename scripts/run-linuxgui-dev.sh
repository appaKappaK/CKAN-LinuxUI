#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
DEV_HOME=${CKAN_LINUX_DEV_HOME:-"$HOME/.ckan-linux-dev"}

PUBLISH_BIN="$REPO_ROOT/_build/publish/CKAN-LinuxGUI/linux-x64/CKAN-LinuxGUI"
PACKAGE_BIN="$REPO_ROOT/_build/package/ckan-linux/linux-x64/usr/lib/ckan-linux/CKAN-LinuxGUI"

if [[ -x "$PUBLISH_BIN" ]]; then
    APP_BIN="$PUBLISH_BIN"
elif [[ -x "$PACKAGE_BIN" ]]; then
    APP_BIN="$PACKAGE_BIN"
else
    cat >&2 <<'EOF'
CKAN Linux dev binary not found.

Build one of these first:
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
exec "$APP_BIN" "$@"
