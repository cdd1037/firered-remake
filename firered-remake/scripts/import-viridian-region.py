#!/usr/bin/env python3
"""Locked, local-only Viridian increment: inspectable data and renderings, never a playable-world claim."""
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
import tempfile

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA256 = 'dd0045b11236a019539fdb57a80ff152fab493a87acec2c4a9eb53e2b2463fba'
PROFILE = 'firered-viridian-region-import-v1'
MAPS = [('PalletTown', 'map.pallet-town', 'pallet'),
        ('PalletTown_ProfessorOaksLab', 'map.professor-oaks-lab', 'professor-oaks-lab'),
        ('Route1', 'map.route-1', 'route-1'),
        ('ViridianCity', 'map.viridian-city', 'viridian-city'),
        ('ViridianCity_Mart', 'map.viridian-mart', 'viridian-mart')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_PalletTown': 'secondary/pallet_town', 'gTileset_ViridianCity': 'secondary/viridian_city',
            'gTileset_Lab': 'secondary/lab', 'gTileset_Mart': 'secondary/mart'}
NEW_MAPS = ['MAP_ROUTE1', 'MAP_VIRIDIAN_CITY', 'MAP_VIRIDIAN_CITY_MART']


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
    data = (path or ROOT / 'scripts/viridian-region.lock.json').read_bytes()
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
    roots.update(['Std_ObtainItem', 'Std_FindItem', 'Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo'])
    required = {'ViridianCity_Mart_EventScript_ParcelScene', 'ViridianCity_Mart_EventScript_Clerk',
                'ViridianCity_Mart_Items', 'PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene',
                'Route1_EventScript_MartClerk', 'ViridianCity_EventScript_SetOldManBlockingRoad'}
    if not required <= roots:
        raise ValueError('Critical parcel/encounter-route labels are missing')
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
            for token in re.findall(r'\b[A-Za-z_]\w*\b', text):
                token = aliases.get(token, token)
                if token in blocks:
                    references.append(dict(source=label, target=token, line=line['line']))
                    if token not in selected:
                        pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Text_|.*_(?:EventScript|Movement|Text)_).*', token):
                    unresolved.append(dict(symbol=token, **location))
    if unresolved:
        raise ValueError('Unresolved source label dependencies: ' + ', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1, profile=PROFILE, executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='notCertifiedForRegion') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])


def encounter_inventory(src, worlds):
    path = 'src/data/wild_encounters.json'
    groups = src.json(path)['wild_encounter_groups']
    group = next(x for x in groups if x['label'] == 'gWildMonHeaders' and x['for_maps'] is True)
    selected = {w['events']['id'] for w in worlds}
    records = [x for x in group['encounters'] if x['map'] in selected and x['base_label'].endswith('_FireRed')]
    if {x['map'] for x in records} != {'MAP_ROUTE1', 'MAP_PALLET_TOWN', 'MAP_VIRIDIAN_CITY'}:
        raise ValueError('FireRed region encounter selection differs')
    fields = {x['type']: x for x in group['fields']}
    for record in records:
        for kind, value in record.items():
            if kind in ('map', 'base_label'):
                continue
            if kind not in fields or len(value['mons']) != len(fields[kind]['encounter_rates']):
                raise ValueError('Encounter slot count differs from weighted source table')
            if type(value['encounter_rate']) is not int or not 0 < value['encounter_rate'] <= 255:
                raise ValueError('Invalid encounter rate')
            for mon in value['mons']:
                if any(type(mon[k]) is not int for k in ('min_level', 'max_level')) or not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                    raise ValueError('Invalid encounter level range')
    route = next(x for x in records if x['map'] == 'MAP_ROUTE1')['land_mons']
    if route['encounter_rate'] != 21 or fields['land_mons']['encounter_rates'] != [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1]:
        raise ValueError('Route 1 reviewed encounter table differs')
    return dict(schemaVersion=1, gameVersion='FIRERED', sourceRef=src.ref(path, 'gWildMonHeaders'),
                fields=group['fields'], records=records, isExecutable=False,
                semantics='encounter_rate is a raw engine rate, not a percentage per step',
                blockers=['Field encounter state and step rate/avoidance/repel/lead modifiers',
                          'Source RNG consumption and map-transition encounter continuity',
                          'Wild Pidgey/Rattata generation, moves, battle, escape, capture and field return',
                          'Surfing and all fishing profiles remain outside this playable increment'])


def quest_inventory(src, scripts):
    blocks = {x['label']: x for x in scripts['blocks']}
    parcel = blocks['ViridianCity_Mart_EventScript_ParcelScene']
    receipt = blocks['PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene']
    stock = blocks['ViridianCity_Mart_Items']
    stock_items = [re.fullmatch(r'\s*\.2byte (ITEM_\w+)\s*', x['text'])[1]
                   for x in stock['lines'] if re.fullmatch(r'\s*\.2byte (ITEM_\w+)\s*', x['text'])]
    if stock_items != ['ITEM_POKE_BALL', 'ITEM_POTION', 'ITEM_ANTIDOTE', 'ITEM_PARALYZE_HEAL', 'ITEM_NONE']:
        raise ValueError('Mart inventory differs')
    item_symbols = stock_items[:-1] + ['ITEM_OAKS_PARCEL']
    source_items = src.json('src/data/items.json')['items']
    items = []
    for symbol in item_symbols:
        matches = [x for x in source_items if x['itemId'] == symbol]
        if len(matches) != 1:
            raise ValueError('Expected unique source item: ' + symbol)
        numeric_id = re.search(r'^#define\s+' + symbol + r'\s+(\d+)\s*$', src.text('include/constants/items.h'), re.M)
        if numeric_id is None:
            raise ValueError('Item ID is not a reviewed numeric declaration')
        items.append(dict(sourceItemId=int(numeric_id[1]), source=matches[0],
                          sourceRef=src.ref('src/data/items.json', symbol)))
    required_lines = {
        parcel['label']: ['setvar VAR_MAP_SCENE_VIRIDIAN_CITY_MART, 1',
                          'giveitem_msg ViridianCity_Mart_Text_ReceivedOaksParcelFromClerk, ITEM_OAKS_PARCEL, 1, MUS_OBTAIN_KEY_ITEM',
                          'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 5'],
        receipt['label']: ['removeitem ITEM_OAKS_PARCEL', 'setflag FLAG_SYS_POKEDEX_GET',
                           'special SetUnlockedPokedexFlags',
                           'giveitem_msg PalletTown_ProfessorOaksLab_Text_ReceivedFivePokeBalls, ITEM_POKE_BALL, 5',
                           'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 6',
                           'setvar VAR_MAP_SCENE_VIRIDIAN_CITY_MART, 2',
                           'setvar VAR_MAP_SCENE_VIRIDIAN_CITY_OLD_MAN, 1',
                           'setvar VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE, 1',
                           'setvar VAR_MAP_SCENE_ROUTE22, 1']}
    transitions = []
    for label, required in required_lines.items():
        lines = [x['text'].strip() for x in blocks[label]['lines']]
        positions = [lines.index(x) for x in required]
        if positions != sorted(positions):
            raise ValueError('Parcel source effect ordering differs')
        transitions.append(dict(label=label, sourceRef=blocks[label]['sourceRef'],
                                orderedRequiredInstructions=required, originalBlock=blocks[label]['lines']))
    return dict(schemaVersion=1, executable=False, shopStock=stock_items, items=items,
                transitions=transitions,
                requiredEntryLabels=['ViridianCity_Mart_MapScripts', 'ViridianCity_Mart_OnLoad',
                                     'ViridianCity_Mart_OnFrame', parcel['label'],
                                     'ViridianCity_Mart_EventScript_Clerk',
                                     'PalletTown_ProfessorOaksLab_EventScript_ProfOak', receipt['label']],
                warnings=['Preserve original scene/giveitem ordering and source failure semantics; do not inject item/flags.',
                          'Importing complete original blocks is not script execution or whole-game reachability.'])


def npc_inventory(src, worlds, out):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    dynamic = 'OBJ_EVENT_GFX_VAR_0' in graphics
    graphics.discard('OBJ_EVENT_GFX_VAR_0')
    graphics.update(['OBJ_EVENT_GFX_RED_NORMAL', 'OBJ_EVENT_GFX_OLD_MAN_1', 'OBJ_EVENT_GFX_OLD_MAN_LYING_DOWN'])
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
    return dict(schemaVersion=1, graphics=records,
                dynamicGraphics=[dict(graphicsId='OBJ_EVENT_GFX_VAR_0', variable='VAR_OBJ_GFX_ID_0',
                                      candidates=['OBJ_EVENT_GFX_OLD_MAN_LYING_DOWN', 'OBJ_EVENT_GFX_OLD_MAN_1'],
                                      sourceScript='ViridianCity_OnTransition', runtimeStatus='requiresVariableBinding')] if dynamic else [])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    # Staging protects previous valid outputs from any source/parser/render failure.
    with tempfile.TemporaryDirectory(prefix='.viridian-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        encounters = encounter_inventory(src, worlds)
        quest = quest_inventory(src, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        rules = [
            ('outdoorConnections', 'Coordinate offsets and reciprocal references validated; field/camera streaming is not integrated.'),
            ('grassEncounter', 'Preserve grass and encounter type; stepping cannot bypass encounter evaluation.'),
            ('ledgeJump', 'Directional ledge behavior and forced jump landing require authoritative movement.'),
            ('martOnLoad', 'setmetatile hides questionnaire before Pokedex; source tile mutation is required.'),
            ('parcelGift', 'giveitem_msg and key-item pocket/space/failure/message semantics need runtime handlers.'),
            ('oakReceipt', 'Remove parcel, SetUnlockedPokedexFlags, gift five balls and all ordered scene changes.'),
            ('shop', 'pokemart menu, stock, funds, quantity/space validation and atomic purchase/sale required.'),
            ('dynamicNpcs', 'Bind VAR_OBJ_GFX_ID_0 and preserve old-man coordinates, flags and collision.'),
            ('oldManTutorial', 'StartOldManTutorialBattle, Teachy TV and capture presentation remain outside parcel milestone.'),
            ('externalBoundaries', 'Route2/22/21 and unimported interiors are explicit boundaries, not removed warps.'),
            ('sourcePresentation', 'No NPC animation, tileset animation, music/sound output or original visual parity is certified.')]
        matrix = dict(schemaVersion=1, runtimeIntegrated=False, commands=scripts['commands'],
                      specials=scripts['specials'], movements=scripts['movements'],
                      rules=[dict(id=k, status='unsupportedInThisIncrement', requirement=v) for k, v in rules],
                      unresolvedLabelReferences=scripts['unresolvedLabelReferences'])
        catalog = dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                       fullSourceHash=source_hash(src.lock), isPlayable=False, runtimeIntegrated=False,
                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=w['events']['id'], directory=directory,
                                  role='newMap' if w['events']['id'] in NEW_MAPS else 'existingPalletReference')
                             for (name, stable, directory), w in zip(MAPS, worlds)],
                       connections=connections, warps=warps, inputFiles=src.lock['files'],
                       scope='Independent source-data increment. The existing five-map application catalog is unchanged.')
        documents = {'viridian-catalog.json': catalog, 'scripts.json': scripts, 'encounters.json': encounters,
                     'parcel-quest.json': quest, 'npc-source.json': npcs, 'support-matrix.json': matrix}
        for filename, document in documents.items():
            (stage / filename).write_text(canonical(document), encoding='utf-8')
        outputs = [dict(path=p.relative_to(stage).as_posix(), sha256=digest(p.read_bytes()), byteLength=p.stat().st_size)
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, fullSourceHash=source_hash(src.lock),
                        lockSha256=LOCK_SHA256, inputCount=len(src.entries), mapCount=len(worlds),
                        newMapCount=3, cellCount=sum(len(w['cells']) for w in worlds),
                        connectionCount=len(connections), warpCount=len(warps),
                        scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),
                        isPlayable=False, outputs=outputs)
        (stage / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
        # Only these owned paths are replaced, after the whole import has succeeded.
        for path in sorted(stage.rglob('*')):
            destination = out / path.relative_to(stage)
            require_safe_target(out, destination)
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
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/viridian-region')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/viridian-region')
    args = parser.parse_args()
    import_catalog(args.raw, args.out)
