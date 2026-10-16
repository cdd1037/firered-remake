#!/usr/bin/env python3
"""Compile complete field metatile tables for the current schema-3 regional maps.

Reads pinned raw layout and tileset declarations, including tiles absent from a
map's initial grid. Runtime uses only content/generated/maps/tilesets.json.
"""
from __future__ import annotations

import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT

import argparse
import json
from pathlib import Path, PurePosixPath
import re
import struct


def map_layouts(generated: Path) -> set[str]:
    index = json.loads((generated / 'maps/world.json').read_bytes())
    used = set()
    for entry in index['maps']:
        document = json.loads((generated / entry['path']).read_bytes())
        used.add(entry.get('layout', document['events']['layout']))
    if not used:
        raise ValueError('No schema-3 field maps found in content/generated/maps/regions')
    return used


def tileset_declarations(raw: Path) -> dict[str, tuple[bool, str]]:
    """Read source references, not inferred filenames or partial regional catalogs."""
    metatiles = (raw / 'src/data/tilesets/metatiles.h').read_text(encoding='utf-8')
    attributes = {}
    for symbol, relative in re.findall(
            r'const\s+u32\s+(\w+)\[\]\s*=\s*INCBIN_U32\("([^"]+)"\)\s*;', metatiles):
        path = PurePosixPath(relative)
        if (path.parts[:2] != ('data', 'tilesets') or len(path.parts) != 5
                or path.parts[2] not in ('primary', 'secondary')
                or path.name != 'metatile_attributes.bin' or '..' in path.parts or '\\' in relative):
            raise ValueError(f'Unsupported metatile attribute path: {relative}')
        if symbol in attributes:
            raise ValueError(f'Duplicate metatile attribute declaration: {symbol}')
        attributes[symbol] = path.parent.relative_to('data/tilesets').as_posix()

    headers = (raw / 'src/data/tilesets/headers.h').read_text(encoding='utf-8')
    symbols = {}
    for symbol, body in re.findall(r'const\s+struct\s+Tileset\s+(\w+)\s*=\s*\{(.*?)\};', headers, re.S):
        reference = re.search(r'\.metatileAttributes\s*=\s*(\w+)\s*,', body)
        secondary = re.search(r'\.isSecondary\s*=\s*(TRUE|FALSE)\s*,', body)
        if reference is None or secondary is None or reference[1] not in attributes:
            raise ValueError(f'Incomplete tileset declaration: {symbol}')
        if symbol in symbols:
            raise ValueError(f'Duplicate tileset declaration: {symbol}')
        is_secondary = secondary[1] == 'TRUE'
        name = attributes[reference[1]]
        if name.startswith('secondary/') != is_secondary:
            raise ValueError(f'Tileset bank differs from attribute path: {symbol}')
        symbols[symbol] = (is_secondary, name)
    if not symbols:
        raise ValueError('No source tileset declarations found')
    return symbols


def compile_tables(root: Path, *, raw=None, used=None) -> dict:
    raw = raw or root / 'content/raw/pokefirered/037335f4'
    used = map_layouts(root / 'content/generated') if used is None else used
    symbols = tileset_declarations(raw)
    layouts, tables = {}, {}
    for layout in json.loads((raw / 'data/layouts/layouts.json').read_bytes())['layouts']:
        identity = layout.get('id')
        if identity not in used:
            continue
        if identity in layouts:
            raise ValueError(f'Duplicate source layout: {identity}')
        pair = []
        for bank, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            if symbol not in symbols:
                raise ValueError(f'Missing tileset declaration for {identity}: {symbol}')
            secondary, name = symbols[symbol]
            if secondary != bool(bank):
                raise ValueError(f'Incorrect {key} bank for {identity}: {symbol}')
            pair.append(name)
            if name not in tables:
                path = raw / 'data/tilesets' / name / 'metatile_attributes.bin'
                data = path.read_bytes()
                capacity = 384 if secondary else 640
                if not data or len(data) % 4 or len(data) // 4 > capacity:
                    raise ValueError(f'Invalid metatile attribute length: {path}')
                tables[name] = [[value & 511, (value >> 9) & 31,
                                 (value >> 24) & 7, (value >> 29) & 3]
                                for value, in struct.iter_unpack('<I', data)]
        layouts[identity] = pair
    missing = used - layouts.keys()
    if missing:
        raise ValueError('Missing source layouts: ' + ', '.join(sorted(missing)))
    return {'layouts': layouts, 'tilesets': tables}


def run(root: Path, check: bool = False) -> None:
    document = compile_tables(root)
    data = (json.dumps(document, sort_keys=True, separators=(',', ':')) + '\n').encode()
    output = root / 'content/generated/maps/tilesets.json'
    if check:
        if output.read_bytes() != data:
            raise ValueError('Regenerate content/generated/maps/tilesets.json')
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(data)
    print(json.dumps({'layouts': len(document['layouts']), 'tilesets': len(document['tilesets']),
                      'bytes': len(data)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--check', action='store_true')
    arguments = parser.parse_args()
    run(arguments.root, arguments.check)
