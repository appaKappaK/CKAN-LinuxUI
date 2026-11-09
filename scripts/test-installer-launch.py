#!/usr/bin/env python3
"""Check that opting in to launch runs the installed launcher after cleanup."""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parent.parent
archive = root / '_build/release-installer/CKAN-Linux-Installer-linux-x64.zip'
with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as temp:
    temp = Path(temp)
    subprocess.run(['unzip', '-q', str(archive), '-d', str(temp)], check=True)
    home = temp / 'home'
    home.mkdir()
    script = r'''
"$1" > "$HOME/installer.log" 2>&1 & pid=$!
trap 'kill "$pid" 2>/dev/null || true' EXIT
sleep 3
id=$(xdotool search --name 'CKAN Linux Installer' | head -1)
xdotool mousemove --window "$id" 235 275 click 1
for i in {1..30}; do [ -x "$HOME/.local/bin/ckan-linux" ] && break; sleep 1; done
[ -x "$HOME/.local/bin/ckan-linux" ] || exit 1
printf '#!/bin/sh\nprintf launched > "$HOME/launched"\n' > "$HOME/.local/bin/ckan-linux"
chmod +x "$HOME/.local/bin/ckan-linux"
sleep 2
xdotool mousemove --window "$id" 235 307 click 1
sleep 2
if [ "$CKAN_TEST_ACTION" = remove ]; then
    xdotool mousemove --window "$id" 290 266 click 1
else
    xdotool mousemove --window "$id" 180 266 click 1
fi
for i in {1..20}; do [ -f "$HOME/launched" ] && exit 0; sleep .2; done
cat "$HOME/installer.log"
exit 1
'''
    env = os.environ.copy()
    env['HOME'] = str(home)
    env['CKAN_TEST_ACTION'] = sys.argv[1] if len(sys.argv) > 1 else 'keep'
    subprocess.run(['xvfb-run', '-a', 'bash', '-c', script, '_', str(temp / 'CKAN-Linux-Installer/CKAN-Linux-Installer')], env=env, check=True)
    assert (home / 'launched').read_text() == 'launched'
    payload = temp / 'CKAN-Linux-Installer/payload'
    assert payload.exists() == (env['CKAN_TEST_ACTION'] == 'keep')
    print(f"Installer launched CKAN Linux after {env['CKAN_TEST_ACTION']}.")
