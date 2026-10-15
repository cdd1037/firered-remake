#!/usr/bin/env python3
"""Fetch only pinned local opening-item research texts; never execute upstream code."""
import importlib.util
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('opening_items_import', ROOT / 'scripts/import-opening-items.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def fetch():
    lock = module.read_lock()
    for entry in lock['files']:
        path = entry['path']
        target = ROOT / 'content/raw/pokefirered/037335f4' / path
        data = target.read_bytes() if target.exists() else urllib.request.urlopen(
            f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}', timeout=60).read()
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    print(f'Verified {len(lock["files"])} fixed opening-item texts; no upstream execution or ROM.')


if __name__ == '__main__':
    fetch()
