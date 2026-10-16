#!/usr/bin/env python3
"""Fetch the bounded, pinned trainer research texts. No ROM or upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import hashlib
import json
import pathlib
import urllib.request

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
lock = json.loads((ROOT / 'scripts/recipes/trainers/lab-trainers.lock.json').read_text(encoding='utf-8'))
if (lock['schemaVersion'] != 1 or lock['commit'] != COMMIT or
        lock['repository'] != 'pret/pokefirered' or lock['completeSourceRoster'] is not False or
        lock['commit'] != json.loads((ROOT / 'scripts/recipes/config/source.json').read_text(encoding='utf-8'))['source']['sourceCommit']):
    raise ValueError('Unexpected trainer source identity or scope')
seen = set()
for entry in lock['files']:
    path = pathlib.PurePosixPath(entry['path'])
    if path.is_absolute() or '..' in path.parts or str(path) in seen:
        raise ValueError('Unsafe or duplicate source path')
    seen.add(str(path))
    dest = ROOT / 'content/raw/pokefirered/037335f4' / path
    data = dest.read_bytes() if dest.exists() else urllib.request.urlopen(
        f'https://raw.githubusercontent.com/{lock["repository"]}/{COMMIT}/{path}', timeout=60).read()
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
if not seen:
    raise ValueError('Empty source manifest')
print(f'Verified {len(seen)} pinned source texts for three lab trainer parties; no upstream execution.')
