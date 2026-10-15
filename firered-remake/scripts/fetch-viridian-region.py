#!/usr/bin/env python3
"""Materialize the locked Viridian region data. Never execute reference code or read its worktree."""
import argparse
import importlib.util
import json
import pathlib
import subprocess
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('viridian_import', ROOT / 'scripts/import-viridian-region.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


def fetch(raw, reference_repo=None, download=False):
    lock = importer.read_lock()
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = raw / path
        importer.require_safe_target(raw, target)
        if reference_repo is not None:
            data = subprocess.check_output(['git', '-C', str(reference_repo), 'show', lock['commit'] + ':' + path])
        elif target.is_file():
            data = target.read_bytes()
        elif download:
            url = f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}'
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
        else:
            raise FileNotFoundError('Missing locked source; pass --reference-repo or --download: ' + path)
        verified[path] = data
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=lock['commit'], inputCount=len(verified))
    print(json.dumps(manifest, sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        help='Local pret repository; read pinned tree/blob objects, never working files.')
    parser.add_argument('--download', action='store_true', help='Fetch missing inputs from the pinned public source URL.')
    args = parser.parse_args()
    fetch(args.raw, args.reference_repo, args.download)
