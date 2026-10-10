#!/usr/bin/env python3
"""Authenticated, local Git-blob-only Viridian Pokemon Center source increment."""
import argparse
import base64
import collections
import hashlib
import io
import json
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
LOCK_SHA256 = '3a933ce9e3b542e328887410dfd9d6a8091fd243987f0d408fee9745240a7abb'
PROFILE = 'firered-viridian-center-import-v1'
MAPS = [('ViridianCity_PokemonCenter_1F', 'map.viridian-pokemon-center-1f', 'viridian-pokemon-center-1f')]
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_PokemonCenter': 'secondary/pokemon_center'}


def digest(data):
    return hashlib.sha256(data).hexdigest()

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
    data = (path or ROOT / 'scripts/viridian-center.lock.json').read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or
            lock.get('repository') != 'pret/pokefirered' or lock.get('commit') != COMMIT or
            lock.get('tree') != TREE or lock.get('profile') != PROFILE or
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
        if (set(entry) != {'path', 'byteLength', 'gitBlobSha1', 'sha256', 'roles'} or
                type(entry['byteLength']) is not int or entry['byteLength'] <= 0 or
                not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1']) or
                not re.fullmatch('[0-9a-f]{64}', entry['sha256']) or not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    if digest(data) != LOCK_SHA256:
        raise ValueError('Reviewed source lock hash differs')
    return lock

def verify_bytes(entry, data):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if len(data) != entry['byteLength'] or blob != entry['gitBlobSha1'] or digest(data) != entry['sha256']:
        raise ValueError('Source hash mismatch: ' + entry['path'])

def source_hash(lock):
    return digest(''.join(e['path'] + '\0' + e['sha256'] + '\n'
                          for e in sorted(lock['files'], key=lambda x: x['path'])).encode())

class Source:
    def __init__(self, raw, lock_path=None):
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.data = {}
        for path, entry in self.entries.items():
            require_safe_target(raw, raw / path)
            data = (raw / path).read_bytes()
            verify_bytes(entry, data)
            self.data[path] = data
        require_safe_target(raw, raw / 'manifest.json')
        manifest = json_bytes((raw / 'manifest.json').read_bytes())
        expected = dict(schemaVersion=1, commit=COMMIT, tree=TREE, inputCount=len(self.entries),
                        fullSourceHash=source_hash(self.lock), lockSha256=LOCK_SHA256)
        if canonical(manifest) != canonical(expected):
            raise ValueError('Raw source manifest differs')

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
                      symbol=symbol, sha256=self.entries[path]['sha256'])
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
        artifacts.append(dict(path=filename, sha256=digest((out / filename).read_bytes()), width=img.width, height=img.height))
    embedded_paths = [path, f'data/maps/{name}/scripts.inc', f'data/maps/{name}/text.inc',
                      'data/layouts/layouts.json', 'include/constants/metatile_behaviors.h',
                      layout['blockdata_filepath'], layout['border_filepath']] + [p + '/metatile_attributes.bin' for p in bases]
    world = dict(schemaVersion=1, profile=PROFILE, stableId=stable_id, sourceRef=src.ref(path, events['id']),
                 isPlayable=False, runtimeIntegrated=False, width=width, height=height, tileSize=16,
                 borderWidth=bw, borderHeight=bh, texturePath='map.png', artifacts=artifacts,
                 layout=layout, cells=cells, borderCells=[cell(b, i % bw, i // bw) for i, b in enumerate(border)],
                 events=events, embeddedSource=[dict(path=p, sha256=src.entries[p]['sha256'],
                                                  base64=base64.b64encode(src.data[p]).decode()) for p in embedded_paths])
    (out / 'world.json').write_text(canonical(world), encoding='utf-8')
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
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'EventScript_AfterWhiteOutHeal'])
    required = {'ViridianCity_PokemonCenter_1F_OnTransition',
                'ViridianCity_PokemonCenter_1F_EventScript_Nurse'}
    if not required <= roots:
        raise ValueError('Critical center entry labels are missing')
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
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='notCertifiedForCenter') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])

def npc_inventory(src, worlds, out):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = []
    out.mkdir()
    for graphics_id in sorted(graphics):
        symbol = re.search(r'\[' + graphics_id + r'\]\s*=\s*&(\w+)', src.text(root + 'object_event_graphics_info_pointers.h'))[1]
        body = re.search(r'\b' + symbol + r' = \{(.*?)\n\};', src.text(root + 'object_event_graphics_info.h'), re.S)[1]
        fields = dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)', body))
        rows = re.search(r'\b' + fields['images'] + r'\[\] = \{(.*?)\n\};', src.text(root + 'object_event_pic_tables.h'), re.S)[1]
        frames = []
        for pic, width, height, index in re.findall(r'overworld_frame\((\w+), (\d+), (\d+), (\d+)\)', rows):
            path = re.search(r'\b' + pic + r'\[\] = INCBIN_U16\("(.*?)"\)', src.text(root + 'object_event_graphics.h'))[1].replace('.4bpp', '.png')
            frames.append(dict(pic=pic, widthTiles=int(width), heightTiles=int(height), index=int(index), sourceRef=src.ref(path, pic)))
        palette = re.search(r'\{\s*(\w+)\s*,\s*' + fields['paletteTag'] + r'\s*\}', src.text('src/event_object_movement.c'))[1]
        palette_path = re.search(r'\b' + palette + r'\[\] = INCBIN_U16\("(.*?)"\)', src.text(root + 'object_event_graphics.h'))[1].replace('.gbapal', '.pal')
        width, height = int(fields['width']), int(fields['height'])
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in line.split())
                  for line in src.text(palette_path).splitlines()[3:]]
        if len(colors) != 16 or any(len(c) != 3 for c in colors) or not frames:
            raise ValueError('Invalid NPC palette/frame table')
        sheet = Image.new('RGBA', (width * len(frames), height))
        for i, frame in enumerate(frames):
            if frame['widthTiles'] * frame['heightTiles'] * 64 != width * height:
                raise ValueError('NPC frame byte span differs')
            image = Image.open(io.BytesIO(src.data[frame['sourceRef']['path']]))
            if image.mode != 'P' or image.height != height or (frame['index'] + 1) * width > image.width:
                raise ValueError('Unsupported NPC source sheet')
            crop = image.crop((frame['index'] * width, 0, (frame['index'] + 1) * width, height))
            rgba = Image.new('RGBA', crop.size)
            rgba.putdata([(*colors[c], 0 if c == 0 else 255) for c in crop.get_flattened_data()])
            sheet.paste(rgba, (i * width, 0))
        filename = graphics_id.removeprefix('OBJ_EVENT_GFX_').lower().replace('_', '-') + '.png'
        sheet.save(out / filename, optimize=False, compress_level=9)
        records.append(dict(graphicsId=graphics_id, declaration=fields, frames=frames,
                            texturePath='npc/' + filename, sha256=digest((out / filename).read_bytes()),
                            frameWidth=width, frameHeight=height, frameCount=len(frames),
                            tileCenterAnchor=dict(x=width // 2, y=height - 8),
                            sourceRef=src.ref(root + 'object_event_graphics_info.h', symbol),
                            paletteSourceRef=src.ref(palette_path, palette), runtimeStatus='sourceOnly'))
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


def materialize(raw, reference_repo=None, lock_path=None):
    """Read only immutable Git blobs, or verify an already materialized local source set."""
    raw = pathlib.Path(raw)
    lock = read_lock(lock_path)
    tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        if git('rev-parse', COMMIT + '^{tree}').decode().strip() != TREE:
            raise ValueError('Reference commit tree differs')
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
            if tree.get(path) != ('100644', 'blob', entry['gitBlobSha1']):
                raise ValueError('Path is not the locked regular Git blob: ' + path)
            data = git('cat-file', 'blob', entry['gitBlobSha1'])
        else:
            data = target.read_bytes()
        verify_bytes(entry, data)
        verified[path] = data
    require_safe_target(raw, raw / 'manifest.json')
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=COMMIT, tree=TREE, inputCount=len(verified),
                    fullSourceHash=source_hash(lock), lockSha256=LOCK_SHA256)
    (raw / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
    return manifest


def constants_inventory(src, world):
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
    path = 'src/data/heal_locations.json'
    table = src.json(path)['heal_locations']
    template = src.text('src/data/heal_locations.constants.json.txt')
    if 'HEAL_LOCATION_NONE,' not in template or '{{ heal_location.id }},' not in template:
        raise ValueError('Unsupported heal-location enumeration template')
    for i, row in enumerate(table, 1):
        values[row['id']] = dict(value=i, sourceRef=src.ref(path, row['id']))
    generator = src.text('tools/mapjson/mapjson.cpp')
    if 'json_to_string(obj_event, "local_id") << " " << i + 1' not in generator:
        raise ValueError('Unsupported local object ID generation')
    for i, obj in enumerate(world['events']['object_events'], 1):
        if 'local_id' in obj:
            values[obj['local_id']] = dict(value=i, sourceRef=src.ref(world['sourceRef']['path'], obj['local_id']))
    return dict(schemaVersion=1, values=values)


def service_inventory(src, scripts, world, constants):
    blocks = {b['label']: b for b in scripts['blocks']}
    def instructions(label):
        return [x['text'].strip() for x in blocks[label]['lines'] if x['text'].strip() and not x['text'].strip().startswith('@')]
    expected = ['applymovement VAR_LAST_TALKED, Common_Movement_WalkInPlaceFasterLeft', 'waitmovement 0',
                'dofieldeffect FLDEFF_POKECENTER_HEAL', 'waitfieldeffect FLDEFF_POKECENTER_HEAL',
                'applymovement VAR_LAST_TALKED, Common_Movement_WalkInPlaceFasterDown', 'waitmovement 0',
                'special HealPlayerParty', 'return']
    if instructions('EventScript_PkmnCenterNurse_TakeAndHealPkmn') != expected:
        raise ValueError('Original nurse healing order differs')
    if instructions('ViridianCity_PokemonCenter_1F_OnTransition') != ['setrespawn HEAL_LOCATION_VIRIDIAN_CITY', 'end']:
        raise ValueError('Original entry respawn script differs')
    if instructions('CableClub_OnResume') != ['special InitUnionRoom', 'end']:
        raise ValueError('Original resume script differs')
    if 'case 1 EventScript_PkmnCenterNurse_Goodbye' not in instructions('EventScript_PkmnCenterNurse'):
        raise ValueError('Original optional-comma case syntax differs')
    heal = next(x for x in src.json('src/data/heal_locations.json')['heal_locations'] if x['id'] == 'HEAL_LOCATION_VIRIDIAN_CITY')
    if (constants['values'][heal['id']]['value'], heal['x'], heal['y'], heal['respawn_npc']) != (2, 26, 27, 'LOCALID_VIRIDIAN_NURSE'):
        raise ValueError('Viridian heal destination differs')
    return dict(schemaVersion=1, runtimeIntegrated=False,
                mapScripts='ViridianCity_PokemonCenter_1F_MapScripts',
                onTransition='ViridianCity_PokemonCenter_1F_OnTransition', onResume='CableClub_OnResume',
                nurseEntry='ViridianCity_PokemonCenter_1F_EventScript_Nurse',
                whiteoutEntry='EventScript_AfterWhiteOutHeal', healLocation=heal,
                healLocationSourceRef=src.ref('src/data/heal_locations.json', heal['id']),
                orderedHealingInstructions=expected,
                specials=scripts['specials'],
                fieldEffect=dict(symbol='FLDEFF_POKECENTER_HEAL', value=25, native='FldEff_PokecenterHeal',
                                 sourceRef=src.ref('src/field_effect.c', 'FldEff_PokecenterHeal')),
                boundaries=[dict(id='secondFloor', status='unsupported', sourceWarpId=3,
                                 destinationMap='MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F', destinationWarpId=0),
                            dict(id='linkPlay', status='unsupported', sourceEntry='CableClub_OnResume'),
                            dict(id='pokemonStorage', status='unsupported', behavior='MB_PC')])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.viridian-center-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        name, stable, directory = MAPS[0]
        world = import_map(src, name, stable, stage / directory)
        connections, warps = connections_and_warps(src, [world])
        for warp in warps:
            warp['runtimeStatus'] = 'existingCityDestination' if warp['destinationMap'] == 'MAP_VIRIDIAN_CITY' else 'unsupportedSecondFloor'
        scripts = script_inventory(src, [world])
        constants = constants_inventory(src, world)
        npcs = npc_inventory(src, [world], stage / 'npc')
        service = service_inventory(src, scripts, world, constants)
        catalog = dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                       fullSourceHash=source_hash(src.lock), runtimeIntegrated=False,
                       map=dict(sourceName=name, stableId=stable, sourceMapSymbol=world['events']['id'], directory=directory),
                       connections=connections, warps=warps, inputFiles=src.lock['files'])
        for filename, document in {'center-catalog.json': catalog, 'scripts.json': scripts,
                                   'constants.json': constants, 'npc-source.json': npcs, 'service.json': service}.items():
            (stage / filename).write_text(canonical(document), encoding='utf-8')
        outputs = [dict(path=p.relative_to(stage).as_posix(), sha256=digest(p.read_bytes()), byteLength=p.stat().st_size)
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, fullSourceHash=source_hash(src.lock),
                        lockSha256=LOCK_SHA256, inputCount=len(src.entries), mapCount=1, cellCount=len(world['cells']),
                        borderCellCount=len(world['borderCells']), warpCount=len(warps),
                        npcGraphicsCount=len(npcs['graphics']), scriptBlockCount=len(scripts['blocks']),
                        unresolvedLabelCount=len(scripts['unresolvedLabelReferences']), outputs=outputs)
        (stage / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
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
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/viridian-center')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/viridian-center')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        help='Materialize locked Git blobs from this local repository; never read its worktree or run its scripts.')
    args = parser.parse_args()
    if args.reference_repo is not None:
        materialize(args.raw, args.reference_repo)
    import_catalog(args.raw, args.out)
