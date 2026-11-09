#!/usr/bin/env python3
"""Exercise installer cleanup on a copied release ZIP inside an isolated home."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parent.parent
source = root / '_build/release-installer/CKAN-Linux-Installer-linux-x64.zip'
for matches in (True, False):
 with tempfile.TemporaryDirectory(ignore_cleanup_errors=True) as temp:
    temp = Path(temp)
    archive = temp / source.name
    shutil.copyfile(source, archive)
    subprocess.run(['unzip', '-q', str(archive), '-d', str(temp)], check=True)
    if not matches:
        archive.write_bytes(b'Unrelated file with the same name')
    bundle = temp / 'CKAN-Linux-Installer'
    home = temp / 'home'
    home.mkdir()
    script = r'''
"$1" > "$HOME/installer.log" 2>&1 & pid=$!
trap 'kill "$pid" 2>/dev/null || true' EXIT
sleep 3
id=$(xdotool search --name 'CKAN Linux Installer' | head -1)
xdotool mousemove --window "$id" 235 275 click 1
for i in {1..30}; do [ -x "$HOME/.local/lib/ckan-linux/CKAN-LinuxGUI" ] && break; sleep 1; done
[ -x "$HOME/.local/lib/ckan-linux/CKAN-LinuxGUI" ] || { cat "$HOME/installer.log"; exit 1; }
printf '#!/bin/sh\nprintf launched > "$HOME/launched"\n' > "$HOME/.local/bin/ckan-linux"
chmod +x "$HOME/.local/bin/ckan-linux"
sleep 2
xdotool mousemove --window "$id" 235 307 click 1
sleep 1
xdotool mousemove --window "$id" 134 215 click 1
xdotool mousemove --window "$id" 290 266 click 1
for i in {1..30}; do kill -0 "$pid" 2>/dev/null || exit 0; sleep .2; done
cat "$HOME/installer.log"
exit 1
'''
    env = os.environ.copy()
    env['HOME'] = str(home)
    subprocess.run(['xvfb-run', '-a', 'bash', '-c', script, '_', str(bundle / 'CKAN-Linux-Installer')], env=env, check=True)
    assert archive.exists() != matches, 'Only the matching downloaded ZIP should be removed'
    assert not (bundle / 'payload').exists(), 'Bundled payload was not removed'
    assert (home / '.local/lib/ckan-linux/CKAN-LinuxGUI').exists(), 'Installed app was removed'
    assert not (home / 'launched').exists(), 'CKAN Linux launched after the box was cleared'
    print('Matching ZIP removed.' if matches else 'Unrelated ZIP preserved.')
    print('Extracted files removed; installed app remains.')
