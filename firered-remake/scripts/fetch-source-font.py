#!/usr/bin/env python3
"""Fetch/verify the bounded English font source set. Never execute upstream code."""
import argparse
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-english-source-font-v1'
REPOSITORY = 'pret/pokefirered'
INPUT_PATHS = frozenset(('charmap.txt', 'include/text.h', 'include/characters.h',
    'src/text.c', 'src/text_printer.c', 'src/new_menu_helpers.c', 'src/menu.c',
    'src/start_menu.c', 'src/player_pc.c', 'src/battle_message.c', 'src/script_menu.c',
    'graphics_file_rules.mk', 'tools/gbagfx/font.c', 'tools/gbagfx/gfx.c',
    'src/text_window.c', 'src/text_window_graphics.c', 'graphics/text_window/stdpal_0.pal',
    'graphics/fonts/latin_normal.png', 'graphics/fonts/latin_small.png',
    'graphics/fonts/latin_male.png', 'graphics/fonts/latin_female.png'))


def canonical(data):
    return (json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()


def load_lock(path, project_path):
    lock = json.loads(pathlib.Path(path).read_bytes())
    project = json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit'),
        lock.get('gameEdition'), lock.get('romRevision')) != (1, PROFILE, REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Unsupported source-font lock identity')
    if project['source']['sourceCommit'] != COMMIT or project['source']['repository'] != REPOSITORY:
        raise ValueError('Project source lock differs')
    if project['referenceGame']['gameEdition'] != 'FIRERED' or project['referenceGame']['romRevision'] != 0:
        raise ValueError('Project edition/revision differs')
    paths = [entry['path'] for entry in lock['files']]
    if len(paths) != len(set(paths)) or set(paths) != INPUT_PATHS:
        raise ValueError('Source-font dependency set differs')
    for entry in lock['files']:
        if entry['sourceUrl'] != f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{entry["path"]}':
            raise ValueError('Invalid source URL')
    return lock


def safe_path(root, relative):
    root = pathlib.Path(root).resolve()
    result = root / relative
    if not result.resolve().is_relative_to(root):
        raise ValueError('Source-font path escapes root')
    return result


def read_verified(lock, raw):
    result = {}
    for entry in lock['files']:
        data = safe_path(raw, entry['path']).read_bytes()
        result[entry['path']] = data
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/source-font.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'scripts/import-config/source.json')
    parser.add_argument('--cache', type=pathlib.Path)
    parser.add_argument('--offline', action='store_true')
    args = parser.parse_args()
    lock = load_lock(args.lock, args.project_lock)
    pending = []
    for entry in lock['files']:
        target = safe_path(args.raw, entry['path'])
        cache = safe_path(args.cache, entry['path']) if args.cache else None
        if target.exists():
            data = target.read_bytes()
        elif cache and cache.is_file():
            data = cache.read_bytes()
        elif args.offline:
            raise FileNotFoundError('Missing pinned source-font input: ' + entry['path'])
        else:
            with urllib.request.urlopen(f'https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{entry["path"]}', timeout=60) as response:
                data = response.read()
        pending.append((target, data))
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    print(json.dumps(dict(status='materialized', files=len(pending))))


if __name__ == '__main__':
    main()
