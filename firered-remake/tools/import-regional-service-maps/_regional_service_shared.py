#!/usr/bin/env python3
"""Authenticated Route22, League gate, and Route23 source increment; no gameplay or audio activation."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parents[2] / 'scripts')
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from overworld_sprites import graphics_records
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parents[2] / 'scripts')
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
from map_runtime import compact_world as _compact_world, encode as _encode_runtime
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
PROFILE = 'firered-route22-region-import-v1'
MAPS = [('Route22', 'map.route-22', 'route-22'), ('Route22_NorthEntrance', 'map.route-22-north-entrance', 'route-22-north-entrance'), ('Route23', 'map.route-23', 'route-23')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_ViridianCity': 'secondary/viridian_city', 'gTileset_IndigoPlateau': 'secondary/indigo_plateau', 'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}


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
    target = path or ROOT / 'scripts/route22-region.lock.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or
            lock.get('repository') != 'pret/pokefirered' or lock.get('commit') != COMMIT or
            lock.get('profile') != PROFILE or
            lock.get('gameVersion') != 'FIRERED' or type(lock.get('revision')) is not int or
            lock['revision'] != 0 or lock.get('completeGame') is not False or
            lock.get('maps') != [x[0] for x in MAPS] or not lock.get('files')):
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
    graphics_bases = []
    for kind, base in zip(('primary_tileset', 'secondary_tileset'), bases):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        symbol = re.search(r'\.tiles = (\w+)', body)[1]
        declared = re.search(r'\b' + symbol + r'\[\] = INCBIN_U32\("(.*?)"\)', graphics_text)[1]
        graphics_base = declared.removesuffix('/tiles.4bpp.lz')
        if graphics_base != base and (base, graphics_base) != ('data/tilesets/secondary/silph_co', 'data/tilesets/secondary/condominiums'):
            raise ValueError('Unreviewed tileset graphics alias')
        palette_symbol = re.search(r'\.palettes = (\w+)', body)[1]
        palette_body = re.search(r'\b' + palette_symbol + r'\[\]\[16\] =\s*\{(.*?)\n\};', graphics_text, re.S)[1]
        if re.findall(r'INCBIN_U16\("(.*?)"\)', palette_body) != [graphics_base + f'/palettes/{i:02d}.gbapal' for i in range(16)]:
            raise ValueError('Tileset palette binding differs')
        graphics_bases.append(graphics_base)
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
    graphics = [Image.open(io.BytesIO(src.data[p + '/tiles.png'])) for p in graphics_bases]
    if any(i.mode != 'P' or i.width % 8 or i.height % 8 or i.getextrema()[1] > 15 for i in graphics):
        raise ValueError('Expected indexed four-bit graphics')
    emitted = []
    for base, img in zip(graphics_bases, graphics):
        rule = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', src.text('tileset_rules.mk'))
        emitted.append(int(rule[1]) if rule else img.width * img.height // 64)
        if emitted[-1] > img.width * img.height // 64:
            raise ValueError('Tileset emission exceeds source image')
    palettes = []
    for i in range(13):
        lines = src.text(graphics_bases[0 if i < 7 else 1] + f'/palettes/{i:02d}.pal').splitlines()
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
            # ResetScreenForMapLoad clears VRAM; only the decompressed 50 School tiles are loaded.
            # The source RoofRoom uses tile 0x2C0 twice behind the bookshelf bottom.
            # Preserve its transparent zero VRAM result, never invent a source bitmap.
            if local >= emitted[index] and name == 'CeladonCity_Condominiums_RoofRoom' and tid == 0x2c0 and palette == 7 and emitted[index] == 50:
                result = Image.new('RGBA', (8, 8)); tile_cache[entry] = result; return result
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
        artifacts.append(dict(path=filename, width=img.width, height=img.height))
    # Render the exact periodic source border cells separately;
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
        artifacts.append(dict(path=filename, width=img.width, height=img.height))
    embedded_paths = [path, f'data/maps/{name}/scripts.inc', f'data/maps/{name}/text.inc',
                      'data/layouts/layouts.json', 'include/constants/metatile_behaviors.h',
                      layout['blockdata_filepath'], layout['border_filepath']] + [p + '/metatile_attributes.bin' for p in bases]
    world = dict(schemaVersion=1, profile=PROFILE, stableId=stable_id, sourceRef=src.ref(path, events['id']),
                 isPlayable=False, runtimeIntegrated=False, width=width, height=height, tileSize=16,
                 borderWidth=bw, borderHeight=bh, texturePath='map.png', artifacts=artifacts,
                 layout=layout, cells=cells, borderCells=border_cells,
                 events=events, embeddedSource=[dict(path=p, base64=base64.b64encode(src.data[p]).decode()) for p in embedded_paths if p in src.data])
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
    blocks, aliases, preambles, local_macros = {}, {}, {}, []
    script_paths = [p for p, e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc', '.s'))]
    for path in sorted(script_paths):
        current = None
        macro = None
        preambles[path] = []
        for number, line in enumerate(src.text(path).splitlines(), 1):
            definition = re.fullmatch(r'\s*\.macro\s+(\w+)(.*)', line)
            if definition:
                if macro is not None: raise ValueError('Nested regional macro')
                macro = dict(name=definition[1], arguments=definition[2].strip(), lines=[], sourceRef=src.ref(path, definition[1], number))
                local_macros.append(macro)
                current = None
                continue
            if macro is not None:
                if line.strip() == '.endm': macro = None
                else: macro['lines'].append(dict(line=number, text=line))
                continue
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
    required = {'Route22_EventScript_EarlyRival', 'Route23_OnTransition', 'Route22_NorthEntrance_EventScript_BoulderBadgeGuardTrigger'}
    if not required <= roots: raise ValueError('Critical Route22 script entries missing')
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
            elif op in ('msgbox', 'giveitem_msg', 'pokemart'):
                target = operands.split(',')[0].strip()
            if op == 'trainerbattle_single':
                for extra in operands.split(',')[1:4]:
                    if extra.strip() not in ('0', 'NULL') and aliases.get(extra.strip(), extra.strip()) not in blocks:
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
                filePreambles=preambles, localMacros=local_macros, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='unsupportedInRoute22Region') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])

def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


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
    wanted = {'AI_SCRIPT_CHECK_BAD_MOVE', 'AI_SCRIPT_TRY_TO_FAINT', 'AI_SCRIPT_CHECK_VIABILITY'}
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


def materialize(raw, reference_repo):
    """Copy the declared paths from the selected source commit."""
    raw, reference = pathlib.Path(raw), pathlib.Path(reference_repo)
    require_safe_target(raw, raw)
    if raw.resolve().is_relative_to(reference.resolve()) or reference.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Reference and destination must be disjoint')
    lock = read_lock()
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull,
               GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    def git(*args):
        return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks', '-C', str(reference), *args], env=env)
    verified = {}
    for entry in lock['files']:
        path = entry['path']; require_safe_target(raw, raw / path)
        data = git('show', COMMIT + ':' + path); verified[path] = data
    for path, data in verified.items():
        target = raw / path; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)


def first_badge_boundary(worlds):
    """Recompute the exact source collision/guard/trigger stop set, independently of the planning manifest."""
    w = next(w for w in worlds if w['stableId'] == 'map.route-23')
    cells = {(c['x'], c['y']): c for c in w['cells']}
    events = w['events']
    triggers = {(e['x'], e['y']) for e in events['coord_events'] if e['var_value'] == '1'}
    objects = {(e['x'], e['y']) for e in events['object_events']}
    entry = (events['warp_events'][2]['x'], events['warp_events'][2]['y'])
    visited, attempted, queue = {entry}, set(), collections.deque([entry])
    while queue:
        x, y = queue.popleft()
        for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            c = cells.get(p)
            if c is None or c['collision'] or p in objects: continue
            if c['elevation'] != 3 or c['behaviorName'] not in ('MB_NORMAL','MB_SOUTH_ARROW_WARP'):
                raise ValueError('South corridor needs another movement rule')
            if p in triggers: attempted.add(p)
            elif p not in visited: visited.add(p); queue.append(p)
    if len(visited) != 51 or attempted != {(13,149),(14,149)} or any(cells[p]['encounterType'] for p in visited | attempted):
        raise ValueError('First-badge source boundary changed')
    return dict(stableReachableCellCount=len(visited), entry=list(entry),
                stableRows=[dict(y=y, x=sorted(x for x,row in visited if row == y)) for y in sorted({y for x,y in visited})],
                attemptedCascadeTriggerCells=sorted(map(list,attempted)), allCascadeTriggerCells=sorted(map(list,triggers)),
                blockingNpc=[15,149], reachableEncounterCells=0, requiredScene=1,
                requiredMissingBadge='FLAG_BADGE02_GET', runtimeVerified=False)


def dependency_inventory(src, worlds, scripts):
    # The reviewed audit is a bounded declarative source extractor. Its immutable output is
    # authenticated here, and every reused field points back to retained pinned raw sources.
    data = (ROOT / 'tests/Game.Route22Planning.Tests/manifest.json').read_bytes()
    plan = json_bytes(data)
    for w,p in zip(worlds, plan['maps']):
        if w['events'] != p['events'] or w['layout'] != p['layout']:
            raise ValueError('Map differs from reviewed first-badge boundary')
    boundary = first_badge_boundary(worlds)
    for k in ('stableReachableCellCount','stableRows','entry','attemptedCascadeTriggerCells','allCascadeTriggerCells'):
        if boundary[k] != plan['route23FirstBadgeBoundary'][k]: raise ValueError('First-badge proof differs')
    route22, route23 = worlds[0], worlds[2]
    if any(c['collision'] == 0 for c in route22['cells'] if c['y'] == 0 and c['x'] < 24) or any(c['collision'] == 0 for c in route23['cells'] if c['y'] == 159):
        raise ValueError('Direct north connection bypass opened')
    trigger_status = []
    for w in worlds:
        for i,e in enumerate(w['events']['coord_events']):
            candidate = (w['stableId'] == 'map.route-22' and e['var_value'] == '1') or (w['stableId'] == 'map.route-22-north-entrance' and e['var_value'] == '0') or (w['stableId'] == 'map.route-23' and e['var_value'] == '1')
            trigger_status.append(dict(map=w['events']['id'], sourceCoordEventIndex=i, declaration=e,
                                       scope='firstBadgeCandidate' if candidate else 'deferredLaterStory', enabled=False))
    return dict(schemaVersion=1, profile=PROFILE, runtimeIntegrated=False, completeGame=False,
                verificationKind='authenticated-content-and-static-boundary',
                earlyRivalTrainers=plan['earlyRivalTrainers'], encounters=plan['encounters'],
                requiredBadgeStrings=plan['requiredBadgeStrings'], route22SceneWrites=plan['route22SceneWrites'],
                firstBadgeBoundary=boundary, northConnectionBoundary=plan['route22Route23ConnectionBoundary'],
                coordinateEvents=trigger_status,
                audio=[dict(symbol=symbol, sourceRef=src.ref(path,symbol), status='missingPlayableAudio', converted=False)
                       for symbol,path in [('MUS_ROUTE3','sound/songs/midi/mus_route3.mid'),('MUS_VICTORY_ROAD','sound/songs/midi/mus_victory_road.mid')]],
                deferred=['Route22 scene 3/4 and late rival parties','Route23 beyond the Cascade checkpoint',
                          'Route23 high-level land encounters and hidden items','Surf and all fishing encounters',
                          'Victory Road and Indigo Plateau access','Species/moves/AI and battle integration','Audio synthesis and real listening'],
                unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']], specials=[x['symbol'] for x in scripts['specials']],
                                 runtimeIntegration=False),
                limitations=['Full source declarations are preserved, including late-game scripts; none are registered as executable by this increment.',
                             'The 51-cell boundary is a static source proof, not application or GUI acceptance.',
                             'Raw trainer tables retain their complete original file; only the three early-rival parties are exposed as metadata.',
                             'NPC and map frame-zero renders do not certify original animation timing.'])


def import_catalog(raw, out):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw); out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.route22-region-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        constants = constants_inventory(src, worlds, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        dependencies = dependency_inventory(src, worlds, scripts)
        catalog = dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, runtimeIntegrated=False,
                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=w['events']['id'], directory=d)
                             for (name,stable,d),w in zip(MAPS,worlds)],
                       connections=connections, warps=warps)
        for filename, document in package_documents(out.parent, constants, {'route22-catalog.json':catalog,'scripts.json':scripts,
                                   'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage / filename).write_bytes(canonical(document).encode())
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputCount=len(src.entries), mapCount=len(worlds), cellCount=sum(len(w['cells']) for w in worlds),
                        unresolvedLabelCount=len(scripts['unresolvedLabelReferences']), outputs=outputs)
        data = canonical(manifest).encode()
        (stage / 'manifest.json').write_bytes(data)
        for path in sorted(stage.rglob('*')): require_safe_target(out, out / path.relative_to(stage))
        for path in sorted(stage.rglob('*')):
            dest = out / path.relative_to(stage)
            if path.is_dir(): dest.mkdir(parents=True, exist_ok=True)
            else: dest.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(path,dest)
    print(json.dumps(dict(profile=PROFILE, inputCount=len(src.entries), outputCount=len(outputs),
                         runtimeIntegrated=False),sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/route22-region')
    parser.add_argument('--reference-root',type=pathlib.Path,help='Pinned local reference; no network or upstream execution')
    parser.add_argument('--materialize',action='store_true',help='Copy locked Git blobs from --reference-root or .tools/reference-root')
    args=parser.parse_args()
    if args.materialize or args.reference_root:
        reference=args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip())
        materialize(args.raw,reference)
    import_catalog(args.raw,args.out)
