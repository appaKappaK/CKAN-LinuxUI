#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

PREFIX=${CKAN_LINUX_INSTALL_PREFIX:-"$HOME/.local"}
SKIP_BUILD=0

usage() {
    cat <<EOF
Usage: $0 [--prefix PATH] [--skip-build]

Build and install the CKAN Linux GUI launcher into a local prefix.

Defaults:
  prefix: $HOME/.local

Examples:
  $0
  $0 --prefix /usr/local
  $0 --skip-build
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --prefix)
            if [[ $# -lt 2 ]]; then
                echo "Missing value for --prefix" >&2
                exit 2
            fi
            PREFIX=$2
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=1
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            echo "Unknown argument: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
done

case "$PREFIX" in
    /*) ;;
    *) PREFIX="$(pwd)/$PREFIX" ;;
esac

STAGE="$REPO_ROOT/_build/package/ckan-linux/linux-x64/usr"

if [[ "$SKIP_BUILD" != "1" ]]; then
    "$REPO_ROOT/build.sh" LinuxGUIPackage --configuration=Release
fi

if [[ ! -x "$STAGE/bin/ckan-linux" || ! -x "$STAGE/lib/ckan-linux/CKAN-LinuxGUI" ]]; then
    cat >&2 <<EOF
Linux GUI package layout was not found.

Expected:
  $STAGE/bin/ckan-linux
  $STAGE/lib/ckan-linux/CKAN-LinuxGUI

Build it first with:
  ./build.sh LinuxGUIPackage --configuration=Release
EOF
    exit 1
fi

install -d "$PREFIX/bin" \
           "$PREFIX/lib" \
           "$PREFIX/share/applications"

install -m 0755 "$STAGE/bin/ckan-linux" "$PREFIX/bin/ckan-linux"

rm -rf "$PREFIX/lib/ckan-linux"
cp -a "$STAGE/lib/ckan-linux" "$PREFIX/lib/ckan-linux"

if [[ -d "$STAGE/share/icons" ]]; then
    install -d "$PREFIX/share/icons"
    cp -a "$STAGE/share/icons/." "$PREFIX/share/icons/"
fi

if [[ -f "$STAGE/share/applications/ckan-linux.desktop" ]]; then
    sed \
        -e "s#^Exec=.*#Exec=$PREFIX/bin/ckan-linux#" \
        -e "s#^TryExec=.*#TryExec=$PREFIX/bin/ckan-linux#" \
        "$STAGE/share/applications/ckan-linux.desktop" \
        > "$PREFIX/share/applications/ckan-linux.desktop"
fi

cat <<EOF
Installed CKAN Linux GUI to:
  $PREFIX/bin/ckan-linux

Run it with:
  ckan-linux

If that command is not found, add this to your shell profile:
  export PATH="$PREFIX/bin:\$PATH"
EOF
