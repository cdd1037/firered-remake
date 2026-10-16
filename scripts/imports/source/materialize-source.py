#!/usr/bin/env python3
"""Materialize a source recipe from existing raw files or a pinned local Git tree.

No upstream programs are executed. Network downloads require --download explicitly.
The recipe is an input selection, not an authentication receipt.
"""
import argparse
import json
from pathlib import Path
import sys
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from pokemon_import_io import safe, preflight_directory, write_files


def materialize(recipe, raw, reference=None, cache=None, download=False):
    raw = safe(raw)
    preflight_directory(raw)
    for label, source in (('reference repository', reference), ('source cache', cache)):
        if source is not None:
            source = safe(source)
            if raw.resolve().is_relative_to(source.resolve()) or source.resolve().is_relative_to(raw.resolve()):
                raise ValueError('Output must not overlap the ' + label)
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
    pending = {}
    for relative in paths:
        target = safe(raw, relative)
        if reference:
            # An explicitly selected Git revision is authoritative, even when
            # output already exists. Never read the reference working tree.
            data = subprocess.check_output(['git', '-C', str(reference), 'show', commit + ':' + relative])
        elif target.is_file():
            data = target.read_bytes()
        elif cache and safe(cache, relative).is_file():
            data = safe(cache, relative).read_bytes()
        elif download:
            with urllib.request.urlopen(f'https://raw.githubusercontent.com/{repository}/{commit}/{relative}', timeout=60) as response:
                data = response.read()
        else:
            raise FileNotFoundError('Missing pinned source input: ' + str(target))
        pending[relative] = data
    write_files(raw, pending)
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
