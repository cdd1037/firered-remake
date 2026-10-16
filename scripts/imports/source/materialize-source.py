#!/usr/bin/env python3
"""Materialize a source recipe from existing raw files or a pinned local Git tree.

No upstream programs are executed. Network downloads require --download explicitly.
The recipe is an input selection, not an authentication receipt.
"""
import argparse
import json
from pathlib import Path, PurePosixPath
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[3]


def materialize(recipe, raw, reference=None, cache=None, download=False):
    config = json.loads((ROOT / 'scripts/recipes/config/source.json').read_text(encoding='utf-8'))
    document = json.loads(recipe.read_text(encoding='utf-8'))
    commit = document.get('commit', document.get('sourceCommit'))
    repository = document.get('repository', 'pret/pokefirered')
    if commit != config['source']['sourceCommit'] or repository != config['source']['repository']:
        raise ValueError('Recipe must select the project source revision and repository')
    rows = document.get('files', [])
    paths = [row['path'] for row in rows]
    if not paths or len(paths) != len(set(paths)):
        raise ValueError('Recipe input selection is empty or duplicated')
    pending = []
    for relative in paths:
        path = PurePosixPath(relative)
        target = raw / path
        if path.is_absolute() or '..' in path.parts or '\\' in relative or not target.resolve().is_relative_to(raw.resolve()):
            raise ValueError('Unsafe source path: ' + relative)
        if target.is_file():
            data = target.read_bytes()
        elif reference:
            data = subprocess.check_output(['git', '-C', str(reference), 'show', commit + ':' + relative])
        elif cache and (cache / path).is_file():
            if not (cache / path).resolve().is_relative_to(cache.resolve()):
                raise ValueError('Cache path escapes input directory: ' + relative)
            data = (cache / path).read_bytes()
        elif download:
            with urllib.request.urlopen(f'https://raw.githubusercontent.com/{repository}/{commit}/{relative}', timeout=60) as response:
                data = response.read()
        else:
            raise FileNotFoundError('Missing pinned source input: ' + str(target))
        pending.append((target, data))
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return len(pending)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True, help='Source-selection JSON; relative to the working directory')
    parser.add_argument('--raw', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-root', type=Path, help='Read pinned Git blobs, never working-tree files')
    parser.add_argument('--cache', type=Path, help='Read-only cache tree using original relative paths')
    network = parser.add_mutually_exclusive_group()
    network.add_argument('--download', action='store_true', help='Explicitly allow missing files to be downloaded')
    network.add_argument('--offline', action='store_true', help='Explicit default: no network')
    args = parser.parse_args()
    count = materialize(args.recipe, args.raw, args.reference_root, args.cache, args.download)
    print(json.dumps({'status': 'materialized', 'files': count}))


if __name__ == '__main__':
    main()
