#!/usr/bin/env python3
"""Verify/materialize the bounded Pallet whiteout source; never execute upstream code."""
import argparse
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def safe_path(root, name):
    relative = pathlib.PurePosixPath(name)
    if relative.is_absolute() or '..' in relative.parts or not relative.parts or '\\' in name:
        raise ValueError('Unsafe source path: ' + name)
    target = root / relative
    for path in [root, *root.parents, target, *target.parents]:
        if path.is_symlink():
            raise ValueError('Symlink source/output path: ' + str(path))
    return target


def fetch(output, reference_repo=None, lock_path=None):
    if reference_repo is not None:
        source_root, output_root = reference_repo.resolve(), output.resolve()
        if output_root == source_root or source_root in output_root.parents or output_root in source_root.parents:
            raise ValueError('Output must not overlap the reference repository')
    lock_bytes = (lock_path or ROOT / 'scripts/route1-whiteout-source.lock.json').read_bytes()
    lock = json.loads(lock_bytes)
    if lock['schemaVersion'] != 1 or lock['repository'] != 'pret/pokefirered' or lock['commit'] != COMMIT:
        raise ValueError('Unsupported whiteout source identity')
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        if path in verified:
            raise ValueError('Duplicate locked source path: ' + path)
        target = safe_path(output, path)
        if reference_repo is not None:
            data = subprocess.check_output(['git', '-C', str(reference_repo), 'show', COMMIT + ':' + path])
        else:
            data = target.read_bytes()
        verified[path] = data
    if not verified:
        raise ValueError('Empty whiteout source lock')
    manifest = dict(schemaVersion=1, commit=COMMIT, inputCount=len(verified),
                    upstreamCodeExecuted=False, romUsed=False)
    if reference_repo is not None:
        for path, data in verified.items():
            target = output / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Export fixed git-show blobs; never read the working tree.')
    parser.add_argument('--lock', type=pathlib.Path)
    args = parser.parse_args()
    print(json.dumps(fetch(args.output, args.reference_repo, args.lock), sort_keys=True))
