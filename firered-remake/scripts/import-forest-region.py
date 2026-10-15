#!/usr/bin/env python3
"""Authenticated, local Git-blob-only Route 2 and Viridian Forest source increment."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from overworld_sprites import graphics_records
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parents[1] / 'scripts')
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
from map_runtime import compact_world as _compact_world, encode as _encode_runtime
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse
import ast
import base64
import collections
import io
import json
import os
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile
from PIL import Image
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-forest-region-import-v1'
MAPS = [('Route2', 'map.route-2', 'route-2'),
        ('Route2_ViridianForest_SouthEntrance', 'map.route-2-forest-south-gate', 'forest-south-gate'),
        ('ViridianForest', 'map.viridian-forest', 'viridian-forest'),
        ('Route2_ViridianForest_NorthEntrance', 'map.route-2-forest-north-gate', 'forest-north-gate')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_ViridianCity': 'secondary/viridian_city',
            'gTileset_ViridianForest': 'secondary/viridian_forest', 'gTileset_Building': 'primary/building',
            'gTileset_GenericBuilding2': 'secondary/generic_building_2'}


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')) + '\n'

def object_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result

def json_bytes(data):
    return json.loads(data, object_pairs_hook=object_pairs)

def require_safe_target(root, target):
    root, target = pathlib.Path(root), pathlib.Path(target)
    if root.is_symlink() or target.is_symlink():
        raise ValueError('Symlink input/output is not allowed: ' + str(target))
    for ancestor in target.parents:
        if ancestor.is_symlink():
            raise ValueError('Symlink ancestor is not allowed: ' + str(ancestor))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Target escapes selected directory')

def read_lock(path=None):
    data = (path or ROOT / 'scripts/forest-region.lock.json').read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != PROFILE) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in MAPS]) or (not lock.get('files'))):
        raise ValueError('Invalid locked source identity or scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if entry['path'] in seen:
            raise ValueError('Duplicate source path')
        seen.add(entry['path'])
        if (not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    return lock


class Source:
    def __init__(self, raw, lock_path=None):
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.data = {}
        for path, entry in self.entries.items():
            require_safe_target(raw, raw / path)
            data = (raw / path).read_bytes()
            self.data[path] = data

    def text(self, path):
        return self.data[path].decode('utf-8')

    def json(self, path):
        return json_bytes(self.data[path])

    def units(self, path, code):
        data = self.data[path]
        if len(data) % struct.calcsize(code):
            raise ValueError('Misaligned source binary: ' + path)
        return list(struct.unpack('<' + str(len(data) // struct.calcsize(code)) + code, data))

    def ref(self, path, symbol, line=None):
        result = dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path,
                      symbol=symbol)
        if line is not None:
            result['line'] = line
        return result

def movement(behavior):
    if behavior == 0:
        return 'normal'
    if behavior == 2:
        return 'grassUnsupported'
    if 0x38 <= behavior <= 0x3b:
        return 'ledgeUnsupported'
    if behavior == 0x69:
        return 'doorUnsupported'
    if 0x62 <= behavior <= 0x65:
        return 'arrowWarpUnsupported'
    if 0x6c <= behavior <= 0x6f:
        return 'stairWarpUnsupported'
    if behavior in (0x60, 0x61, 0x66, 0x67, 0x68, 0x71):
        return 'warpUnsupported'
    if behavior in (0x10, 0x11, 0x12, 0x13, 0x15):
        return 'waterUnsupported'
    if behavior == 0x84:
        return 'signBlocked'
    return 'unsupported'

def import_map(src, name, stable_id, out):
    path = f'data/maps/{name}/map.json'
    events = src.json(path)
    layouts = src.json('data/layouts/layouts.json')['layouts']
    layout = next(x for x in layouts if x.get('id') == events['layout'])
    width, height = layout['width'], layout['height']
    bw, bh = layout['border_width'], layout['border_height']
    blocks = src.units(layout['blockdata_filepath'], 'H')
    border = src.units(layout['border_filepath'], 'H')
    if len(blocks) != width * height or len(border) != bw * bh:
        raise ValueError('Map binary dimensions differ')
    bases = ['data/tilesets/' + TILESETS[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
    headers = src.text('src/data/tilesets/headers.h')
    graphics_text = src.text('src/data/tilesets/graphics.h') + src.text('src/graphics.c')
    for kind, base in zip(('primary_tileset', 'secondary_tileset'), bases):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        symbol = re.search(r'\.tiles = (\w+)', body)[1]
        declared = re.search(r'\b' + symbol + r'\[\] = INCBIN_U32\("(.*?)"\)', graphics_text)[1]
        if declared != base + '/tiles.4bpp.lz':
            raise ValueError('Tileset graphics binding differs')
    attrs = [src.units(p + '/metatile_attributes.bin', 'I') for p in bases]
    metatiles = [src.units(p + '/metatiles.bin', 'H') for p in bases]
    if len(attrs[0]) != 640 or any(len(m) != len(a) * 8 for m, a in zip(metatiles, attrs)):
        raise ValueError('Unsupported tileset dimensions')
    behavior_names = {int(v, 16): n for n, v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)', src.text('include/constants/metatile_behaviors.h'), re.M)}

    def attributes(mid):
        index = 0 if mid < 640 else 1
        local = mid if index == 0 else mid - 640
        if local >= len(attrs[index]):
            raise ValueError('Metatile outside tileset')
        return attrs[index][local], metatiles[index][local * 8:local * 8 + 8], bases[index], local

    def cell(raw, x, y):
        mid = raw & 1023
        a, _, base, local = attributes(mid)
        behavior = a & 511
        return dict(x=x, y=y, rawBlock=raw, metatileId=mid, collision=(raw >> 10) & 3,
                    elevation=raw >> 12, rawAttributes=a, behaviorId=behavior,
                    behaviorName=behavior_names.get(behavior, f'UNKNOWN_0x{behavior:03X}'),
                    terrain=(a >> 9) & 31, attribute2=(a >> 14) & 15, attribute3=(a >> 18) & 63,
                    encounterType=(a >> 24) & 7, attribute5=(a >> 27) & 3, layerType=(a >> 29) & 3,
                    attribute7=a >> 31, movement=movement(behavior),
                    sourceRef=src.ref(base + '/metatile_attributes.bin', f'metatile_{local:03x}'))

    cells = [cell(b, i % width, i // width) for i, b in enumerate(blocks)]
    graphics = [Image.open(io.BytesIO(src.data[p + '/tiles.png'])) for p in bases]
    if any(i.mode != 'P' or i.width % 8 or i.height % 8 or i.getextrema()[1] > 15 for i in graphics):
        raise ValueError('Expected indexed four-bit graphics')
    emitted = []
    for base, img in zip(bases, graphics):
        rule = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', src.text('tileset_rules.mk'))
        emitted.append(int(rule[1]) if rule else img.width * img.height // 64)
        if emitted[-1] > img.width * img.height // 64:
            raise ValueError('Tileset emission exceeds source image')
    palettes = []
    for i in range(13):
        lines = src.text(bases[0 if i < 7 else 1] + f'/palettes/{i:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
            raise ValueError('Invalid JASC palette')
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in s.split()) for s in lines[3:]]
        if any(len(c) != 3 or any(v < 0 or v > 255 for v in c) for c in colors):
            raise ValueError('Invalid palette RGB')
        palettes.append(colors)
    palettes[0][0] = (0, 0, 0)
    tile_cache = {}

    def tile(entry):
        if entry not in tile_cache:
            tid, palette = entry & 1023, entry >> 12
            index, local = (0, tid) if tid < 640 else (1, tid - 640)
            if palette >= 13 or local >= emitted[index]:
                raise ValueError('Uninitialized tile/palette reference')
            img = graphics[index]
            pixels = []
            for y in range(8):
                for x in range(8):
                    ix, iy = (7 - x if entry & 0x400 else x), (7 - y if entry & 0x800 else y)
                    color = img.getpixel(((local % (img.width // 8)) * 8 + ix, (local // (img.width // 8)) * 8 + iy))
                    pixels.append((*palettes[palette][color], 0 if color == 0 else 255))
            result = Image.new('RGBA', (8, 8))
            result.putdata(pixels)
            tile_cache[entry] = result
        return tile_cache[entry]

    layers = [Image.new('RGBA', (width * 16, height * 16)) for _ in range(3)]
    for c in cells:
        _, tiles, _, _ = attributes(c['metatileId'])
        if c['layerType'] == 0:
            groups = [[0x3014] * 4, tiles[:4], tiles[4:]]
        elif c['layerType'] == 1:
            groups = [tiles[:4], tiles[4:], [0] * 4]
        elif c['layerType'] == 2:
            groups = [tiles[:4], [0] * 4, tiles[4:]]
        else:
            raise ValueError('Reserved metatile layer type')
        for layer, entries in zip(layers, groups):
            for k, entry in enumerate(entries):
                layer.paste(tile(entry), (c['x'] * 16 + (k % 2) * 8, c['y'] * 16 + (k // 2) * 8))
    composite = Image.new('RGBA', layers[0].size, (0, 0, 0, 255))
    for layer in layers:
        composite = Image.alpha_composite(composite, layer)
    out.mkdir(parents=True)
    artifacts = []
    for filename, img in zip(('bottom.png', 'middle.png', 'top.png', 'map.png'), layers + [composite]):
        img.save(out / filename, optimize=False, compress_level=9)
        artifacts.append(dict(path=filename,  width=img.width, height=img.height))
    # The forest has a 3x2 border. Render its exact periodic source block cells separately;
    # do not infer the common 2x2 pattern or stretch a map-edge screenshot.
    border_cells = [cell(b, i % bw, i // bw) for i, b in enumerate(border)]
    border_layers = [Image.new('RGBA', (bw * 16, bh * 16)) for _ in range(3)]
    for c in border_cells:
        _, tiles, _, _ = attributes(c['metatileId'])
        groups = {0: [[0x3014] * 4, tiles[:4], tiles[4:]],
                  1: [tiles[:4], tiles[4:], [0] * 4],
                  2: [tiles[:4], [0] * 4, tiles[4:]]}.get(c['layerType'])
        if groups is None:
            raise ValueError('Reserved border layer type')
        for layer, entries in zip(border_layers, groups):
            for k, entry in enumerate(entries):
                layer.paste(tile(entry), (c['x'] * 16 + k % 2 * 8, c['y'] * 16 + k // 2 * 8))
    border_composite = Image.new('RGBA', border_layers[0].size, (0, 0, 0, 255))
    for layer in border_layers:
        border_composite = Image.alpha_composite(border_composite, layer)
    for filename, img in zip(('border-bottom.png','border-middle.png','border-top.png','border.png'), border_layers + [border_composite]):
        img.save(out / filename, optimize=False, compress_level=9)
        artifacts.append(dict(path=filename,  width=img.width, height=img.height))
    embedded_paths = [path, f'data/maps/{name}/scripts.inc', f'data/maps/{name}/text.inc',
                      'data/layouts/layouts.json', 'include/constants/metatile_behaviors.h',
                      layout['blockdata_filepath'], layout['border_filepath']] + [p + '/metatile_attributes.bin' for p in bases]
    world = dict(schemaVersion=1, profile=PROFILE, stableId=stable_id, sourceRef=src.ref(path, events['id']),
                 isPlayable=False, runtimeIntegrated=False, width=width, height=height, tileSize=16,
                 borderWidth=bw, borderHeight=bh, texturePath='map.png', artifacts=artifacts,
                 layout=layout, cells=cells, borderCells=border_cells,
                 events=events, embeddedSource=[dict(path=p,
                                                  base64=base64.b64encode(src.data[p]).decode()) for p in embedded_paths])
    (out / 'runtime-map.json').write_bytes(_encode_runtime(_compact_world(world)))
    return world

def connections_and_warps(src, worlds):
    selected = {w['events']['id']: w for w in worlds}
    reference_maps = {src.json(p)['id']: (p, src.json(p)) for p in src.data if p.startswith('data/maps/') and p.endswith('/map.json')}
    layouts = {x['id']: x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id')}
    connections, warps = [], []
    opposite = {'up': 'down', 'down': 'up', 'left': 'right', 'right': 'left'}
    for world in worlds:
        events = world['events']
        for index, conn in enumerate(events['connections'] or []):
            _, target = reference_maps[conn['map']]
            reciprocal = [x for x in target['connections'] or [] if x['map'] == events['id'] and x['direction'] == opposite[conn['direction']]]
            if len(reciprocal) != 1 or reciprocal[0]['offset'] != -conn['offset']:
                raise ValueError('Nonreciprocal map connection')
            connections.append(dict(sourceMap=events['id'], sourceConnectionIndex=index, **conn,
                                    runtimeStatus='notIntegrated' if conn['map'] in selected else 'externalBoundary',
                                    coordinateRule='destination transverse coordinate = source transverse coordinate - offset',
                                    sourceRef=world['sourceRef']))
        for index, warp in enumerate(events['warp_events']):
            target_path, target = reference_maps[warp['dest_map']]
            destination_index = int(warp['dest_warp_id'])
            if not 0 <= destination_index < len(target['warp_events']):
                raise ValueError('Warp destination index outside source array')
            destination = target['warp_events'][destination_index]
            target_layout = layouts[target['layout']]
            if destination['dest_map'] != events['id']:
                raise ValueError('Warp destination points to different source map')
            if not (0 <= warp['x'] < world['width'] and 0 <= warp['y'] < world['height'] and
                    0 <= destination['x'] < target_layout['width'] and 0 <= destination['y'] < target_layout['height']):
                raise ValueError('Warp coordinates outside source layout')
            c = world['cells'][warp['y'] * world['width'] + warp['x']]
            warps.append(dict(sourceMap=events['id'], sourceWarpId=index, source=warp,
                              destinationMap=target['id'], destinationWarpId=destination_index,
                              destinationX=destination['x'], destinationY=destination['y'],
                              behaviorName=c['behaviorName'], behaviorId=c['behaviorId'],
                              cellCollision=c['collision'], cellElevation=c['elevation'],
                              runtimeStatus='notIntegrated' if target['id'] in selected else 'externalBoundary',
                              ordinaryWarpCandidate=c['behaviorId'] in (0x69, 0x62, 0x63, 0x64, 0x65),
                              destinationSourceRef=src.ref(target_path, target['id'])))
    return connections, warps

def script_inventory(src, worlds):
    blocks, aliases, preambles = {}, {}, {}
    script_paths = [p for p, e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc', '.s'))]
    for path in sorted(script_paths):
        current = None
        preambles[path] = []
        for number, line in enumerate(src.text(path).splitlines(), 1):
            label = re.fullmatch(r'(\w+)::?', line.split('@')[0].strip())
            if label:
                name = label[1]
                if name in blocks:
                    raise ValueError('Duplicate script label: ' + name)
                current = dict(label=name, sourceRef=src.ref(path, name, number), lines=[])
                blocks[name] = current
            elif current is not None:
                current['lines'].append(dict(line=number, text=line))
            else:
                preambles[path].append(dict(line=number, text=line))
            alias = re.fullmatch(r'\s*\.equ\s+(\w+),\s*(\w+)\s*', line)
            if alias:
                aliases[alias[1]] = alias[2]
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'Std_FindItem', 'EventScript_HiddenItemScript',
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach'])
    required = {'ViridianForest_OnTransition', 'ViridianForest_EventScript_Rick', 'EventScript_CutTree'}
    if not required <= roots:
        raise ValueError('Critical forest entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    selected, pending = set(), sorted(roots)
    references, unresolved = [], []
    commands, specials, movements = collections.defaultdict(list), collections.defaultdict(list), collections.defaultdict(list)
    while pending:
        label = pending.pop()
        if label in selected:
            continue
        selected.add(label)
        block = blocks[label]
        for line in block['lines']:
            text = line['text'].split('@')[0].strip()
            if not text or text.startswith(('.string', '.include', '#')):
                continue
            op = text.split()[0]
            location = dict(label=label, path=block['sourceRef']['path'], line=line['line'])
            commands[op].append(location)
            if op in ('special', 'specialvar'):
                specials[text.split(',')[-1].strip().split()[-1]].append(location)
            if label.endswith('Movement') or 'Movement_' in label:
                movements[op].append(location)
            # Target operands are validated independently of naming conventions. A renamed
            # unknown label must not disappear just because it lacks an EventScript prefix.
            operands = text.split(None, 1)[1] if ' ' in text else ''
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'):
                target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement') or op.startswith(('goto_if_', 'call_if_')):
                target = operands.split(',')[-1].strip()
            elif op == 'case':
                target = re.split(r'[,\s]+', operands.strip())[-1]
            elif op == 'msgbox':
                target = operands.split(',')[0].strip()
            if op == 'trainerbattle_single':
                for extra in operands.split(',')[1:]:
                    if aliases.get(extra.strip(), extra.strip()) not in blocks:
                        unresolved.append(dict(symbol=extra.strip(), **location))
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(symbol=target, **location))
            for token in re.findall(r'\b[A-Za-z_]\w*\b', text):
                token = aliases.get(token, token)
                if token in blocks:
                    references.append(dict(source=label, target=token, line=line['line']))
                    if token not in selected:
                        pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Movement_|Text_|.*_(?:EventScript|Movement|Text)_|CableClub_On).*', token):
                    unresolved.append(dict(symbol=token, **location))
    if unresolved:
        raise ValueError('Unresolved source label dependencies: ' + ', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1, profile=PROFILE, executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='unsupportedInForestRegion') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])

def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


def materialize(raw, reference_repo=None, lock_path=None):
    """Read only immutable Git blobs, or verify an already materialized local source set."""
    raw = pathlib.Path(raw)
    lock = read_lock(lock_path)
    if reference_repo is not None:
        reference = pathlib.Path(reference_repo).resolve()
        if raw.resolve() == reference or raw.resolve().is_relative_to(reference) or reference.is_relative_to(raw.resolve()):
            raise ValueError('Raw materialization and reference repository must be disjoint')
    tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        tree = {}
        for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
            if row:
                fields, name = row.split(b'\t')
                tree[name.decode()] = tuple(fields.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = raw / path
        require_safe_target(raw, target)
        if tree is not None:
            data = git('cat-file', 'blob', COMMIT + ':' + entry['path'])
        else:
            data = target.read_bytes()
        verified[path] = data
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=COMMIT,  inputCount=len(verified))
    return manifest


def constants_inventory(src, worlds, scripts):
    values = {}
    for path in sorted(src.data):
        if not path.startswith('include/constants/'):
            continue
        for match in re.finditer(r'^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$', src.text(path), re.M):
            name, token = match.groups()
            value = int(token, 16 if token.startswith('0x') else 10)
            if name in values and values[name]['value'] != value:
                raise ValueError('Conflicting source constant: ' + name)
            values[name] = dict(value=value, sourceRef=src.ref(path, name, src.text(path)[:match.start()].count('\n') + 1))
    # Resolve only the arithmetic needed by regional flags and trainer AI. No eval,
    # C preprocessing, functions, or upstream execution is permitted.
    expressions = {}
    for path in sorted(src.data):
        if path.startswith('include/constants/'):
            for match in re.finditer(r'^#define[ \t]+([A-Za-z_][A-Za-z0-9_]+)[ \t]+([^\n]+)', src.text(path), re.M):
                token = match[2].split('//')[0].strip()
                expressions[match[1]] = (token, path, src.text(path)[:match.start()].count('\n') + 1)
    wanted = {'AI_SCRIPT_CHECK_BAD_MOVE'}
    for world in worlds:
        for kind in ('object_events','coord_events','bg_events'):
            for event in world['events'][kind]:
                if event.get('flag','').startswith('FLAG_'):
                    wanted.add(event['flag'])
    for block in scripts['blocks']:
        for row in block['lines']:
            wanted.update(re.findall(r'\bFLAG_[A-Za-z0-9_]+\b', row['text']))
    pending = set()
    def resolve(name):
        if name in values:
            return values[name]['value']
        if name in pending or name not in expressions:
            raise ValueError('Missing/cyclic regional constant: ' + name)
        pending.add(name)
        expression, path, line = expressions[name]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return resolve(node.id)
            if isinstance(node, ast.BinOp):
                left, right = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
                if isinstance(node.op, ast.LShift) and 0 <= right < 32: return left << right
            raise ValueError('Unsupported regional constant expression: ' + expression)
        value = number(ast.parse(expression, mode='eval').body)
        if not 0 <= value <= 0x7fffffff:
            raise ValueError('Regional constant outside supported range')
        values[name] = dict(value=value, sourceRef=src.ref(path,name,line))
        pending.remove(name)
        return value
    for name in sorted(wanted):
        resolve(name)
    generator = src.text('tools/mapjson/mapjson.cpp')
    if 'json_to_string(obj_event, "local_id") << " " << i + 1' not in generator:
        raise ValueError('Unsupported local object ID generation')
    for path in sorted(src.data):
        if path.startswith('data/maps/') and path.endswith('/map.json'):
            for i, obj in enumerate(src.json(path)['object_events'], 1):
                if 'local_id' in obj:
                    values[obj['local_id']] = dict(value=i, sourceRef=src.ref(path, obj['local_id']))
    return dict(schemaVersion=1, values=values)


def dependency_inventory(src, worlds, scripts, constants):
    """Bounded declarative extraction. No species/move/special executor is implied."""
    def one(pattern, path):
        matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one source definition: ' + path + ':' + pattern)
        return matches[0]

    def reference(path, symbol, match):
        return src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1)

    def fields(body):
        values = {}
        for line in body.splitlines():
            if not line.strip():
                continue
            match = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if match is None or match[1] in values:
                raise ValueError('Unparsed/duplicate table field: ' + line)
            values[match[1]] = match[2]
        return values

    def constant(symbol):
        return constants['values'][symbol]['value']

    def table(path, symbol):
        m = one(r'^    \[' + re.escape(symbol) + r'\] =\s*\n?    ?\{\n(.*?)^    \},', path)
        return fields(m[1]), reference(path, symbol, m)

    encounter_path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(encounter_path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    template = src.text('src/data/wild_encounters.json.txt')
    if 'contains(encounter.base_label, "FireRed")' not in template or '#ifdef FIRERED' not in template:
        raise ValueError('Unknown wild encounter version-selection template')
    weights = next(f['encounter_rates'] for f in group['fields'] if f['type'] == 'land_mons')
    if weights != [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1]:
        raise ValueError('Unexpected source encounter slots')
    encounters = []
    species_symbols = set()
    for world in worlds:
        rows = [r for r in group['encounters'] if r['map'] == world['events']['id'] and 'FireRed' in r['base_label']]
        expected = 1 if world['events']['id'] in ('MAP_ROUTE2', 'MAP_VIRIDIAN_FOREST') else 0
        if len(rows) != expected:
            raise ValueError('Wild table scope differs')
        for row in rows:
            if set(row) != {'map', 'base_label', 'land_mons'} or len(row['land_mons']['mons']) != 12:
                raise ValueError('Unsupported encounter field/table shape')
            for mon in row['land_mons']['mons']:
                if set(mon) != {'min_level', 'max_level', 'species'} or not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                    raise ValueError('Invalid encounter slot')
                species_symbols.add(mon['species'])
            encounters.append(dict(table=row, slotWeights=weights, sourceRef=src.ref(encounter_path, row['base_label']),
                                   runtimeStatus='unsupportedInForestRegion'))

    trainers = []
    trainer_symbols = []
    for block in scripts['blocks']:
        for line in block['lines']:
            m = re.fullmatch(r'\s*trainerbattle_single (TRAINER_\w+), (\w+), (\w+)\s*', line['text'])
            if m:
                trainer_symbols.append((m[1], block['label'], m[2], m[3], block['sourceRef']))
    if {t[0] for t in trainer_symbols} != {'TRAINER_BUG_CATCHER_' + n for n in ('RICK','DOUG','SAMMY','ANTHONY','CHARLIE')} or len(trainer_symbols) != 5:
        raise ValueError('Forest trainer roster differs')
    charmap = {c: int(v, 16) for c, v in re.findall(r"^'([A-Z])'\s*=\s*([0-9A-F]{2})$", src.text('charmap.txt'), re.M)}
    for symbol, label, intro, defeat, script_ref in sorted(trainer_symbols):
        path = 'src/data/trainers.h'
        match = one(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', path)
        f = fields(match[1])
        if set(f) != {'trainerClass','encounterMusic_gender','trainerPic','trainerName','items','doubleBattle','aiFlags','party'}:
            raise ValueError('Trainer fields differ')
        if f['items'] != '{}' or f['doubleBattle'] != 'FALSE' or f['aiFlags'] != 'AI_SCRIPT_CHECK_BAD_MOVE':
            raise ValueError('Unsupported forest trainer layout')
        party_symbol = re.fullmatch(r'NO_ITEM_DEFAULT_MOVES\((\w+)\)', f['party'])[1]
        pp = 'src/data/trainer_parties.h'
        pm = one(r'^static const struct TrainerMonNoItemDefaultMoves ' + party_symbol + r'\[\] = \{\n(.*?)^\};', pp)
        entries = list(re.finditer(r'    \{\n(.*?)    \},\n', pm[1], re.S))
        if not entries or ''.join(e[0] for e in entries) != pm[1]:
            raise ValueError('Unparsed forest trainer party')
        party = []
        for entry in entries:
            pf = fields(entry[1])
            if set(pf) != {'iv','lvl','species'} or not pf['iv'].isdigit() or not pf['lvl'].isdigit():
                raise ValueError('Unsupported trainer member fields')
            species_symbols.add(pf['species'])
            party.append(dict(species=pf['species'], sourceSpeciesId=constant(pf['species']), level=int(pf['lvl']),
                              sourceIv=int(pf['iv']), heldItem='ITEM_NONE', customMoves=[],
                              speciesName=one(r'^    \[' + pf['species'] + r'\]\s*= _\("([A-Z]+)"\),$', 'src/data/text/species_names.h')[1]))
        name = re.fullmatch(r'_\("([A-Z]+)"\)', f['trainerName'])[1]
        money = one(r'^    \{' + f['trainerClass'] + r', (\d+)\},$', 'src/battle_main.c')
        trainers.append(dict(symbol=symbol, sourceTrainerId=constant(symbol), declaration=f,
                             name=name, encodedName=[charmap[c] for c in name], baseMoney=int(money[1]), party=party,
                             partySourceRef=reference(pp, party_symbol, pm), sourceRef=reference(path, symbol, match),
                             sourceTrainerClass=constant(f['trainerClass']), sourceTrainerPic=constant(f['trainerPic']),
                             encounterMusicGender=constant(f['encounterMusic_gender']), aiFlags=constant(f['aiFlags']), partyFlags=0,
                             scriptEntry=label, introText=intro, defeatText=defeat, scriptSourceRef=script_ref,
                             runtimeStatus='unsupportedInForestRegion'))
    expected_species = {'SPECIES_'+n for n in ('RATTATA','PIDGEY','CATERPIE','WEEDLE','METAPOD','KAKUNA','PIKACHU')}
    if species_symbols != expected_species:
        raise ValueError('Wild/trainer species scope differs')
    species = []
    move_symbols = {'MOVE_CUT'}
    for symbol in sorted(species_symbols):
        f, ref = table('src/data/pokemon/species_info.h', symbol)
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', 'src/data/pokemon/level_up_learnset_pointers.h')[1]
        path = 'src/data/pokemon/level_up_learnsets.h'
        m = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', path)
        lines = m[1].splitlines()
        if not lines or lines[-1].strip() != 'LEVEL_UP_END':
            raise ValueError('Learnset terminator absent')
        learns = []
        for line in lines[:-1]:
            row = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if row is None:
                raise ValueError('Unparsed level-up move')
            learns.append(dict(level=int(row[1]), move=row[2])); move_symbols.add(row[2])
        path = 'src/data/pokemon/evolution.h'
        evolution = one(r'^    \[' + symbol + r'\]\s*= ([^\n]*),$', path)
        species.append(dict(symbol=symbol, sourceSpeciesId=constant(symbol), declaration=f, sourceRef=ref,
                            levelUpMoves=learns, learnsetSourceRef=reference('src/data/pokemon/level_up_learnsets.h', pointer, m),
                            evolutionDeclaration=evolution[1], evolutionSourceRef=reference(path, symbol, evolution),
                            evolutionStatus='unsupported; targets outside direct regional species are not imported',
                            runtimeStatus='unsupportedInForestRegion'))
    moves = []
    for symbol in sorted(move_symbols):
        f, ref = table('src/data/battle_moves.h', symbol)
        if set(f) != {'effect','power','type','accuracy','pp','secondaryEffectChance','target','priority','flags'}:
            raise ValueError('Move fields differ')
        moves.append(dict(symbol=symbol, sourceMoveId=constant(symbol), declaration=f, sourceRef=ref,
                          runtimeStatus='unsupportedInForestRegion'))
    item_symbols = {e['item'] for w in worlds for e in w['events']['bg_events'] if e['type'] == 'hidden_item'}
    for b in scripts['blocks']:
        for row in b['lines']:
            m = re.fullmatch(r'\s*finditem (ITEM_\w+)\s*', row['text'])
            if m: item_symbols.add(m[1])
    item_table = src.json('src/data/items.json')['items']
    items = []
    for symbol in sorted(item_symbols):
        row = next(i for i in item_table if i.get('itemId') == symbol)
        items.append(dict(symbol=symbol, table=row, sourceRef=src.ref('src/data/items.json', symbol), runtimeStatus='unsupportedInForestRegion'))
    event_inventory = []
    references = {src.json(p)['id']: (p, src.json(p)) for p in src.data if p.startswith('data/maps/') and p.endswith('/map.json')}
    for world in worlds:
        events = world['events']
        for index, obj in enumerate(events['object_events']):
            record = dict(map=events['id'], sourceObjectIndex=index, localId=index + 1, object=obj,
                          runtimeStatus='unsupportedInForestRegion')
            if obj['type'] == 'clone':
                path, target = references[obj['target_map']]
                targets = [(i, o) for i, o in enumerate(target['object_events']) if o.get('local_id') == obj['target_local_id']]
                if len(targets) != 1:
                    raise ValueError('Unresolved clone object target')
                target_index, target_obj = targets[0]
                record.update(cloneTarget=dict(map=target['id'], sourceObjectIndex=target_index, localId=target_index+1,
                                              object=target_obj, sourceRef=src.ref(path,obj['target_local_id'])))
            elif not 0 <= obj['x'] < world['width'] or not 0 <= obj['y'] < world['height']:
                raise ValueError('Ordinary object outside map')
            event_inventory.append(record)
    unsupported = dict(opcodes=sorted(c['opcode'] for c in scripts['commands'] if not c['opcode'].startswith('.')),
                       movements=[m['opcode'] for m in scripts['movements']],
                       autonomousMovementTypes=sorted({o['movement_type'] for w in worlds for o in w['events']['object_events'] if 'movement_type' in o}),
                       nativeSpecials=[s['symbol'] for s in scripts['specials']],
                       encounters=[e['table']['base_label'] for e in encounters], trainers=[t['symbol'] for t in trainers],
                       fieldEffects=['FLDEFF_USE_CUT_ON_TREE'], eventUnions=['clone','hidden_item'],
                       other=['Evolution execution and evolution target species','Trainer sight/approach and persistent beaten flags',
                              'Item flags/bag-full branches','Original movement timing, audio and map animations',
                              'External boundaries: Pewter City, Digletts Cave, Route 2 House and East Building'])
    return dict(schemaVersion=1, profile=PROFILE, runtimeIntegrated=False, completeGame=False,
                encounters=encounters, noEncounterMaps=[w['events']['id'] for w in worlds if not any(e['table']['map']==w['events']['id'] for e in encounters)],
                trainers=trainers, species=species, moves=moves, items=items, objects=event_inventory,
                coordinateEvents=[dict(map=w['events']['id'], events=w['events']['coord_events']) for w in worlds],
                backgroundEvents=[dict(map=w['events']['id'], events=w['events']['bg_events']) for w in worlds],
                unsupported=unsupported,
                limitations=['All original input files are retained in full; extracted tables are a bounded declarative dependency inventory.',
                             'No opcodes, movement, encounters or trainer battles are certified by this content-only increment.',
                             'Native C files are audit evidence, not a complete C call graph. No upstream code is executed.'])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.forest-region-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        constants = constants_inventory(src, worlds, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        dependencies = dependency_inventory(src, worlds, scripts, constants)
        catalog = dict(schemaVersion=1, profile=PROFILE, commit=COMMIT,
                        runtimeIntegrated=False,
                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=world['events']['id'], directory=directory)
                             for (name, stable, directory), world in zip(MAPS, worlds)],
                       connections=connections, warps=warps)
        for filename, document in package_documents(out.parent, constants, {'forest-catalog.json': catalog, 'scripts.json': scripts,
                                    'npc-source.json': npcs, 'dependencies.json': dependencies}).items():
            (stage / filename).write_bytes(canonical(document).encode('utf-8'))
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,  inputCount=len(src.entries), mapCount=len(worlds),
                        cellCount=sum(len(w['cells']) for w in worlds), borderCellCount=sum(len(w['borderCells']) for w in worlds),
                        warpCount=len(warps), connectionCount=len(connections),
                        npcGraphicsCount=len(npcs['graphics']), scriptBlockCount=len(scripts['blocks']),
                        trainerCount=len(dependencies['trainers']), encounterTableCount=len(dependencies['encounters']),
                        unresolvedLabelCount=len(scripts['unresolvedLabelReferences']), outputs=outputs)
        manifest_bytes = canonical(manifest).encode('utf-8')
        (stage / 'manifest.json').write_bytes(manifest_bytes)
        for path in sorted(stage.rglob('*')):
            require_safe_target(out, out / path.relative_to(stage))
        for path in sorted(stage.rglob('*')):
            destination = out / path.relative_to(stage)
            if path.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, destination)
    print(json.dumps({k: v for k, v in manifest.items() if k != 'outputs'}, sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/forest-region')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'),
                        help='Local immutable Git repository; also FIRERED_ORIGINAL_REFERENCE. No network or upstream execution.')
    args = parser.parse_args()
    if args.reference_repo is not None:
        materialize(args.raw, args.reference_repo)
    import_catalog(args.raw, args.out)
