#!/usr/bin/env python3
"""Import TM34 and two trainer portraits.
Only pinned Git blobs/local inputs are read; upstream code is never executed.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes

import argparse
import ui_tiles as codec
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-vermilion-gym-assets-v1'
PORTRAITS = [('engineer-front', 'Engineer', 'engineer', 93),
             ('lt-surge-front', 'LeaderLtSurge', 'leader_lt_surge', 118)]


def encoded(value): return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()
def pairs(rows):
    result = {}
    for key, value in rows:
        if key in result: raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result

def safe(root, relative=None):
    if '..' in pathlib.Path(root).parts: raise ValueError('Parent traversal root rejected')
    root = pathlib.Path(root).absolute()
    if relative is not None:
        if (not relative or pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative
                or any(p in ('', '.', '..') for p in relative.split('/'))):
            raise ValueError('Unsafe Gym asset path')
        root /= relative
    for part in [root, *root.parents]:
        if part.is_symlink(): raise ValueError('Gym asset symlink rejected')
    return root

def read(root, path):
    return safe(root, path).read_bytes()

def read_lock(path=None):
    path = safe(path or ROOT / 'scripts/recipes/story/vermilion-gym-assets.lock.json')
    data = read(path.parent, path.name)
    lock = json.loads(data, object_pairs_hook=pairs)
    if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], lock['gameVersion'], lock['revision']) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Gym asset lock identity differs')
    return lock


def read_sources(lock, raw, repository=None):
    sources = {}
    if repository:
        repository = safe(repository)
    for entry in lock['files']:
        path = entry['path']
        safe(raw, path)
        if path in sources: raise ValueError('Duplicate source file')
        if repository:
            data = subprocess.check_output(['git', '-C', str(repository), 'cat-file', 'blob', COMMIT + ':' + entry['path']])
        else: data = read(raw, path)
        sources[path] = data
    return sources

def build(sources):
    text = {p: b.decode() for p, b in sources.items() if not p.endswith(('.png', '.mid'))}
    def match(path, pattern):
        matches = list(re.finditer(pattern, text[path], re.M | re.S))
        if len(matches) != 1: raise ValueError('Nonunique source match: ' + path + ' ' + pattern)
        return matches[0]
    def require(path, fragment):
        if fragment not in text[path]: raise ValueError('Source contract missing: ' + fragment)
    def constant(path, symbol):
        m = match(path, r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$')
        return int(m[1])
    def ident(symbol): return dict(value='pret:' + symbol)

    outputs, portraits = {}, []
    require('tools/gbagfx/gfx.c', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
    def convert(name, image_path, palette_path, size):
        width, height, indices, _ = codec.indexed_png(sources[image_path])
        if (width, height) != size or max(indices) > 15: raise ValueError('Source indexed image geometry differs')
        rgba = codec.rgba(indices, codec.palette(sources[palette_path]))
        png = codec.png(width, height, rgba)
        outputs[name + '.png'] = png
        return rgba, png
    p = 'src/data/item_icon_table.h'
    m = match(p, r'^    \[ITEM_TM34\][ \t]*= \{(\w+), (\w+)\},')
    symbols, paths = m.groups(), []
    for symbol in symbols:
        p = 'src/data/graphics/items.h'
        m = match(p, r'^const u32 ' + symbol + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);')
        paths.append(m[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    rgba, png = convert('item_tm34', paths[0], paths[1], (24, 24))
    icon = dict(item=ident('ITEM_TM34'), width=24, height=24, pngPath='item_tm34.png')
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', text['src/data/trainer_graphics/front_pic_tables.h'].split('const struct CompressedSpriteSheet')[0])
    require('src/battle_controller_opponent.c', '(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
    require('src/battle_controller_opponent.c', 'CreateSprite(&gMultiuseSpriteTemplate,\n                                                     176,')
    for name, symbol, stem, picture in PORTRAITS:
        image_path = 'graphics/trainers/front_pics/' + stem + '_front_pic.png'
        palette_path = 'graphics/trainers/palettes/' + stem + '.pal'
        require('src/data/graphics/trainers.h', 'gTrainerFrontPic_' + symbol + '[] = INCBIN_U32("' + image_path.replace('.png', '.4bpp.lz') + '")')
        require('src/data/graphics/trainers.h', 'gTrainerPalette_' + symbol + '[] = INCBIN_U32("' + palette_path.replace('.pal', '.gbapal.lz') + '")')
        require('src/data/trainer_graphics/front_pic_tables.h', 'TRAINER_SPRITE(' + stem.upper() + ', gTrainerFrontPic_' + symbol + ', 0x800)')
        require('src/data/trainer_graphics/front_pic_tables.h', 'TRAINER_PAL(' + stem.upper() + ', gTrainerPalette_' + symbol + ')')
        if constant('include/constants/trainers.h', 'TRAINER_PIC_' + stem.upper()) != picture or coords[picture] != ('8', '1'):
            raise ValueError('Trainer picture identity/geometry differs')
        rgba, png = convert(name, image_path, palette_path, (64, 64))
        portraits.append(dict(id=name, width=64, height=64, x=144, y=8, sourceTrainerPicId=picture, sourceFrame=0,
                              sourceImage=image_path, sourcePalette=palette_path))

    document = dict(schemaVersion=1,
                    icon=icon, portraits=portraits)
    outputs['assets.json'] = runtime_json_bytes(document)
    outputs['manifest.json'] = encoded(dict(schemaVersion=1,
         outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs

def run(raw, out, repository=None, lock_path=None, check=False):
    raw, out = safe(raw), safe(out)
    if raw == out or raw in out.parents or out in raw.parents: raise ValueError('Raw/generated overlap')
    if repository:
        reference = safe(repository)
        if any(p == reference or p in reference.parents or reference in p.parents for p in (raw, out)):
            raise ValueError('Reference/output overlap')
    source_root = ROOT / 'content/raw'
    if out == source_root or source_root in out.parents or out in source_root.parents:
        raise ValueError('Generated output overlaps project raw sources')
    if repository:
        generated_root = ROOT / 'content/generated'
        if raw == generated_root or generated_root in raw.parents or raw in generated_root.parents:
            raise ValueError('Raw materialization overlaps generated resources')
    lock = read_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = build(sources)
    materials = {(out, p): b for p, b in outputs.items()}
    if repository:
        materials.update({(raw, p): b for p, b in sources.items()})
    # Preflight every target before any writes; an untrusted failed input cannot alter output.
    targets = []
    for (parent, path), data in materials.items():
        target, stage = safe(parent, path), safe(parent, path + '.tmp')
        for part in [target.parent, *target.parent.parents]:
            if part.exists() and not part.is_dir(): raise ValueError('Non-directory target parent')
        if any(p.exists() and not p.is_file() for p in (target, stage)): raise ValueError('Non-file target')
        targets.append((target, stage, data))
    for target, stage, data in targets:
        if check:
            if target.read_bytes() != data: raise ValueError('Generated output differs: ' + str(target))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            stage.write_bytes(data)
            stage.replace(target)
    return dict(passed=True, sourceFiles=len(sources),
                newPortraits=len(json.loads(outputs['assets.json'])['portraits']), outputs=len(outputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/ui/vermilion-gym-assets')
    parser.add_argument('--lock', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.raw, args.out, args.reference_repo, args.lock, check=args.check), sort_keys=True))
