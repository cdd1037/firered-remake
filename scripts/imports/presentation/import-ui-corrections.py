#!/usr/bin/env python3
"""Local companion assets from a bounded pinned git tree. Never reads ROMs/screenshots or runs upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  importlib.util, json, pathlib, struct, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PATHS = frozenset(('graphics/battle_interface/textbox.png', 'graphics/battle_interface/textbox1.pal',
    'graphics/interface/text_pp.pal', 'graphics/item_pc/bg.png', 'graphics/item_pc/bg.bin',
    'src/battle_controller_player.c', 'src/battle_message.c', 'src/item_pc.c'))

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result

codec = module('correction_codec', ROOT / 'scripts/imports/presentation/import-lab-battle-ui.py')
items = module('correction_items', ROOT / 'scripts/imports/items/import-item-ui.py')
canonical = codec.inputs.canonical

def inputs(lock_path, raw, cache=None):
    lock = json.loads(lock_path.read_bytes())
    project = json.loads((ROOT / 'scripts/recipes/config/source.json').read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('commit'), lock.get('repository')) != (1, 'firered-ui-corrections-v1', COMMIT, 'pret/pokefirered'):
        raise ValueError('Correction source identity differs')
    if project['source']['sourceCommit'] != COMMIT:
        raise ValueError('Pinned project identity differs')
    entries = lock['files']
    if len(entries) != len(PATHS) or {e['path'] for e in entries} != PATHS:
        raise ValueError('Correction dependency set differs')
    result = {}
    for e in entries:
        target = codec.inputs.safe_path(raw, e['path'])
        if target.exists(): data = target.read_bytes()
        elif cache is not None: data = subprocess.check_output(['git', '-C', str(cache), 'show', COMMIT + ':' + e['path']])
        else: raise FileNotFoundError('Missing correction input: ' + e['path'])
        result[e['path']] = data
    for path, data in result.items():
        target = codec.inputs.safe_path(raw, path); target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    return lock, result

def convert(lock, sources):
    controller = sources['src/battle_controller_player.c'].decode()
    for needle in ('src[0] = 1;', 'src[1] = 2;', '7 * (cursorPosition & 1) + 16, 35 + (cursorPosition & 2)', '9 * (cursorPosition & 1) + 1, 55 + (cursorPosition & 2)'):
        codec.require(controller, needle)
    codec.require(sources['src/battle_message.c'].decode(), 'gPlttBufferUnfaded[BG_PLTT_ID(5) + 12] = palPtr[(var * 2) + 0];')
    codec.require(sources['src/item_pc.c'].decode(), 'SetBgTilemapPalette(1, 0, 14, 30, 6, palIdx + 1);')
    pal = codec.palette(sources['graphics/battle_interface/textbox1.pal'])
    tiles = codec.tile_list(sources['graphics/battle_interface/textbox.png'])
    arrow = codec.rgba(tiles[1] + tiles[2], pal)
    tilemap = list(struct.unpack('<1024H', sources['graphics/item_pc/bg.bin']))
    for y in range(14, 20):
        for x in range(30): tilemap[y * 32 + x] = (tilemap[y * 32 + x] & 0xFFF) | 0x2000
    pc = items.render_map(sources['graphics/item_pc/bg.png'], struct.pack('<1024H', *tilemap))
    assets = []; outputs = {}
    for name, width, height, pixels in [('battle-selection-cursor', 8, 16, arrow), ('pc-submenu-background', 240, 160, pc)]:
        data = codec.png(width, height, pixels); outputs[name + '.png'] = data
        assets.append(dict(id=name, width=width, height=height))
    pp = codec.palette(sources['graphics/interface/text_pp.pal'])
    catalog = dict(schemaVersion=1,
          assets=assets,
        ppColors=[dict(foreground=pp[i * 2], shadow=pp[i * 2 + 1]) for i in range(4)],
        menuSeparatorColor=pal[9], sourceCursorTiles=[1, 2],
        scope='Settled selection cursor, PP palette and PC submenu background only. No animation, timing, audio or game-state changes.')
    outputs['ui-corrections.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict( outputs=[dict(path=p) for p, b in sorted(outputs.items())],
         romRead=False, screenshotsUsedAsAssets=False))
    return outputs

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/ui/ui-corrections')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/presentation/ui-corrections.lock.json')
    parser.add_argument('--cache', type=pathlib.Path)
    args = parser.parse_args()
    if args.output.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.output.resolve()):
        raise ValueError('Raw/output roots overlap')
    lock, sources = inputs(args.lock, args.raw, args.cache); outputs = convert(lock, sources)
    args.output.mkdir(parents=True, exist_ok=True)
    for path, data in outputs.items(): codec.inputs.safe_path(args.output, path).write_bytes(data)
    print(json.dumps(dict(status='imported', assets=2)))

if __name__ == '__main__': main()
