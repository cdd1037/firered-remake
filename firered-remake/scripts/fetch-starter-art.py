#!/usr/bin/env python3
"""Fetch only the pinned, local-only starter showmonpic inputs; never run upstream code."""
import argparse
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-starter-showmonpic-static-v1'
TEXT_PATHS = (
    'Makefile', 'include/constants/species.h', 'include/constants/pokemon.h',
    'include/pokemon.h', 'src/script_menu.c', 'src/field_effect.c',
    'src/trainer_pokemon_sprites.c', 'src/pokemon.c', 'src/decompress.c', 'src/data.c',
    'src/data/graphics/pokemon.h', 'src/data/pokemon_graphics/front_pic_table.h',
    'src/data/pokemon_graphics/palette_table.h',
    'src/data/pokemon_graphics/front_pic_coordinates.h',
    'data/maps/PalletTown_ProfessorOaksLab/scripts.inc',
    'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
)
INPUT_PATHS = frozenset(TEXT_PATHS) | {
    f'graphics/pokemon/{name}/{filename}'
    for name in ('bulbasaur', 'charmander', 'squirtle')
    for filename in ('front.png', 'normal.pal')
}


def canonical(document):
    return (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def source_url(path):
    return f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}'


def load_lock(lock_path, project_lock_path):
    lock = json.loads(pathlib.Path(lock_path).read_text(encoding='utf-8'))
    project = json.loads(pathlib.Path(project_lock_path).read_text(encoding='utf-8'))
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit'),
            lock.get('gameEdition'), lock.get('romRevision')) != (1, PROFILE, REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Unsupported starter-art lock identity/profile')
    if project['source']['sourceCommit'] != COMMIT or project['source']['repository'] != REPOSITORY:
        raise ValueError('Project source lock differs from starter-art lock')
    if project['referenceGame']['gameEdition'] != 'FIRERED' or project['referenceGame']['romRevision'] != 0:
        raise ValueError('Project game edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or set(paths) != INPUT_PATHS:
        raise ValueError('Starter-art dependency set differs; empty, duplicate, or extra inputs rejected')
    for entry in entries:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe input path')
        if entry['sourceUrl'] != source_url(entry['path']):
            raise ValueError('Unexpected source URL')
    return lock


def safe_path(root, relative):
    root = pathlib.Path(root).resolve()
    path = root / relative
    if not path.resolve().is_relative_to(root):
        raise ValueError('Input/output symlink escapes root: ' + relative)
    return path


def read_verified(lock, raw):
    sources = {}
    for entry in lock['files']:
        data = safe_path(raw, entry['path']).read_bytes()
        sources[entry['path']] = data
    return sources


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/starter-art.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    parser.add_argument('--cache', type=pathlib.Path, help='Optional previously downloaded source tree')
    parser.add_argument('--offline', action='store_true', help='Reject missing files without networking')
    args = parser.parse_args()
    lock = load_lock(args.lock, args.project_lock)
    pending = []
    for entry in sorted(lock['files'], key=lambda row: row['path']):
        target = safe_path(args.raw, entry['path'])
        cached = safe_path(args.cache, entry['path']) if args.cache else None
        if target.exists():
            data = target.read_bytes()
        elif cached is not None and cached.is_file():
            data = cached.read_bytes()
        elif args.offline:
            raise FileNotFoundError('Missing pinned starter-art input: ' + entry['path'])
        else:
            url = f'https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{entry["path"]}'
            with urllib.request.urlopen(url, timeout=60) as response:
                data = response.read()
        pending.append((target, data))
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    args.raw.mkdir(parents=True, exist_ok=True)
    print(json.dumps({'status': 'materialized', 'files': len(pending)}))


if __name__ == '__main__':
    main()
