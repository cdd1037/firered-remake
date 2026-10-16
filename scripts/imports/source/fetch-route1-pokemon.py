#!/usr/bin/env python3
"""Verify/materialize bounded Route 1 Pokemon source. Read fixed Git blobs, never upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import subprocess

ROOT = _PROJECT_ROOT


def safe_path(root, name):
    relative = pathlib.PurePosixPath(name)
    if relative.is_absolute() or '..' in relative.parts or not relative.parts:
        raise ValueError('Unsafe source path')
    target = root / relative
    for path in [root, *root.parents, target, *target.parents]:
        if path.is_symlink():
            raise ValueError('Symlink source/output path: ' + str(path))
    return target


def verify_sources(output, reference_repo=None, lock_path=None):
    if reference_repo is not None:
        source_root, output_root = reference_repo.resolve(), output.resolve()
        if output_root == source_root or source_root in output_root.parents or output_root in source_root.parents:
            raise ValueError('Output must not overlap the reference repository')
    lock_bytes = (lock_path or ROOT / 'scripts/recipes/pokemon/route1-pokemon.lock.json').read_bytes()
    lock = json.loads(lock_bytes)
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = safe_path(output, path)
        if reference_repo is not None:
            data = subprocess.check_output(['git', '-C', str(reference_repo), 'show', lock['commit'] + ':' + path])
        else:
            data = target.read_bytes()
        verified[path] = data
    return lock, verified


def fetch(output, reference_repo=None, lock_path=None):
    lock, verified = verify_sources(output, reference_repo, lock_path)
    for path, data in verified.items():
        target = output / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=lock['commit'], inputCount=len(verified),
                    upstreamCodeExecuted=False, romUsed=False)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Read fixed Git blobs, never working-tree files.')
    parser.add_argument('--lock', type=pathlib.Path)
    args = parser.parse_args()
    print(json.dumps(fetch(args.output, args.reference_repo, args.lock), sort_keys=True))
