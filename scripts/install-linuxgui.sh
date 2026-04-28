#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

PREFIX=${CKAN_LINUX_INSTALL_PREFIX:-"$HOME/.local"}
SKIP_BUILD=0
START_TIME=$SECONDS

if [[ -t 1 ]]; then
    BOLD=$(tput bold 2>/dev/null || true)
    DIM=$(tput dim 2>/dev/null || true)
    GREEN=$(tput setaf 2 2>/dev/null || true)
    BLUE=$(tput setaf 4 2>/dev/null || true)
    YELLOW=$(tput setaf 3 2>/dev/null || true)
    RED=$(tput setaf 1 2>/dev/null || true)
    RESET=$(tput sgr0 2>/dev/null || true)
else
    BOLD=""
    DIM=""
    GREEN=""
    BLUE=""
    YELLOW=""
    RED=""
    RESET=""
fi

print_header() {
    printf '%s\n' "${BOLD}CKAN Linux GUI local installer${RESET}"
    printf '%s\n\n' "${DIM}Builds the release package layout and installs ckan-linux into your local prefix.${RESET}"
}

step() {
    printf '\n%s==>%s %s\n' "$BLUE" "$RESET" "$1"
}

detail() {
    printf '    %s\n' "$1"
}

ok() {
    printf '%s[ok]%s %s\n' "$GREEN" "$RESET" "$1"
}

warn() {
    printf '%s[warn]%s %s\n' "$YELLOW" "$RESET" "$1"
}

fail() {
    printf '%s[error]%s %s\n' "$RED" "$RESET" "$1" >&2
}

finish() {
    local status=$?
    if [[ $status -ne 0 ]]; then
        fail "Install failed after ${SECONDS}s."
    fi
    exit $status
}

trap finish EXIT

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

print_header
detail "Repository: $REPO_ROOT"
detail "Install prefix: $PREFIX"
if [[ "$SKIP_BUILD" == "1" ]]; then
    detail "Build: skipped by request"
else
    detail "Build: Release LinuxGUIPackage"
fi

if [[ "$SKIP_BUILD" != "1" ]]; then
    step "Building release package layout"
    detail "./build.sh LinuxGUIPackage --configuration=Release"
    "$REPO_ROOT/build.sh" LinuxGUIPackage --configuration=Release
    ok "Package layout built."
else
    step "Skipping build"
    detail "Using existing package layout under $STAGE"
fi

step "Checking package layout"
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
ok "Found launcher and app binary."

step "Preparing install directories"
install -d "$PREFIX/bin" \
           "$PREFIX/lib" \
           "$PREFIX/share/applications"
ok "Install directories are ready."

step "Installing launcher"
install -m 0755 "$STAGE/bin/ckan-linux" "$PREFIX/bin/ckan-linux"
ok "Installed $PREFIX/bin/ckan-linux"

step "Installing app files"
rm -rf "$PREFIX/lib/ckan-linux"
cp -a "$STAGE/lib/ckan-linux" "$PREFIX/lib/ckan-linux"
ok "Installed app runtime under $PREFIX/lib/ckan-linux"

if [[ -d "$STAGE/share/icons" ]]; then
    step "Installing desktop icons"
    install -d "$PREFIX/share/icons"
    cp -a "$STAGE/share/icons/." "$PREFIX/share/icons/"
    ok "Installed desktop icons."
else
    warn "No desktop icons found in package layout."
fi

if [[ -f "$STAGE/share/applications/ckan-linux.desktop" ]]; then
    step "Installing desktop entry"
    sed \
        -e "s#^Exec=.*#Exec=$PREFIX/bin/ckan-linux#" \
        -e "s#^TryExec=.*#TryExec=$PREFIX/bin/ckan-linux#" \
        "$STAGE/share/applications/ckan-linux.desktop" \
        > "$PREFIX/share/applications/ckan-linux.desktop"
    ok "Installed desktop entry."
else
    warn "No desktop entry found in package layout."
fi

elapsed=$((SECONDS - START_TIME))
cat <<EOF

${GREEN}Install complete in ${elapsed}s.${RESET}

Launcher:
  ${BOLD}$PREFIX/bin/ckan-linux${RESET}

Run it with:
  ckan-linux

If that command is not found, add this to your shell profile:
  export PATH="$PREFIX/bin:\$PATH"
EOF
