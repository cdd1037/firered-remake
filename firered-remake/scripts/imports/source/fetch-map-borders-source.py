#!/usr/bin/env python3
"""Read four pinned field rendering files as data. Never execute upstream programs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import urllib.request

ROOT = _PROJECT_ROOT


def fetch(lock_path, destination, cache=None):
    data = lock_path.read_bytes()
    lock = json.loads(data)
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts:
            raise ValueError('Unsafe source path')
        target = destination / path
        if cache is not None:
            data = (cache / path).read_bytes()
        elif target.is_file():
            data = target.read_bytes()
        else:
            url = f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}'
            data = urllib.request.urlopen(url, timeout=60).read()
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    print(f'Materialized {len(lock["files"])} pinned map-border source files; no upstream code executed.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/maps/map-borders-source.lock.json')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--cache', type=pathlib.Path)
    args = parser.parse_args()
    fetch(args.lock, args.output, args.cache)


if __name__ == '__main__':
    main()
