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
    tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        if git('rev-parse', lock['commit'] + '^{tree}').decode().strip() != lock['tree']:
            raise ValueError('Reference commit tree differs')
        tree = {}
        for row in git('ls-tree', '-rz', lock['commit']).split(b'\0'):
            if row:
                fields, name = row.split(b'\t')
                tree[name.decode()] = tuple(fields.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = raw / path
        importer.require_safe_target(raw, target)
        if tree is not None:
            if tree.get(path) != ('100644', 'blob', entry['gitBlobSha1']):
                raise ValueError('Path is not the locked regular Git blob: ' + path)
            data = git('cat-file', 'blob', entry['gitBlobSha1'])
        elif target.is_file():
            data = target.read_bytes()
        elif download:
            url = f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}'
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
        else:
            raise FileNotFoundError('Missing locked source; pass --reference-repo or --download: ' + path)
        importer.verify_bytes(entry, data)
        verified[path] = data
    importer.require_safe_target(raw, raw / 'manifest.json')
    # No writes until the entire set, including the Git tree bindings, has passed.
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=lock['commit'], tree=lock['tree'],
                    inputCount=len(verified), fullSourceHash=importer.source_hash(lock),
                    lockSha256=importer.LOCK_SHA256)
    importer.require_safe_target(raw, raw / 'manifest.json')
    (raw / 'manifest.json').write_text(importer.canonical(manifest), encoding='utf-8')
    print(json.dumps(manifest, sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/viridian-region')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        help='Local pret repository; read pinned tree/blob objects, never working files.')
    parser.add_argument('--download', action='store_true', help='Fetch missing inputs from the pinned public source URL.')
    args = parser.parse_args()
    fetch(args.raw, args.reference_repo, args.download)
