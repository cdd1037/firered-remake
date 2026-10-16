"""Source-map decoding/rendering and topology, shared by regional conversions.

Only map_runtime serializes runtime maps. Region importers select source maps and
validate their own gameplay contracts; this module contains no region branches.
"""
import base64
import io
import json
import pathlib
import re
import struct
from PIL import Image
from map_runtime import COMMIT, require, movement, compact_world as _compact_world, encode as _encode_runtime

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

class LockedSource:
    def __init__(self, raw, lock):
        raw = pathlib.Path(raw)
        self.lock = lock
        self.entries = {e['path']: e for e in self.lock['files']}
        self.data = {}
        for path, entry in self.entries.items():
            require_safe_target(raw, raw / path)
            data = (raw / path).read_bytes()
            self.data[path] = data
        require_safe_target(raw, raw)

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

def render_map(src, name, stable_id, out, *, tilesets, render_border=True, unloaded_tile=None):
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
    bases = ['data/tilesets/' + tilesets[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
    headers = src.text('src/data/tilesets/headers.h')
    graphics_text = src.text('src/data/tilesets/graphics.h') + src.text('src/graphics.c')
    graphics_bases = []
    for kind, base in zip(('primary_tileset', 'secondary_tileset'), bases):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        symbol = re.search(r'\.tiles = (\w+)', body)[1]
        declared = re.search(r'\b' + symbol + r'\[\] = INCBIN_U32\("(.*?)"\)', graphics_text)[1]
        if not declared.startswith('data/tilesets/') or not declared.endswith('/tiles.4bpp.lz') or '..' in declared.split('/'):
            raise ValueError('Invalid source tileset graphics binding')
        graphics_base = declared.removesuffix('/tiles.4bpp.lz')
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
        if emitted[-1] > min(img.width * img.height // 64, 640 if len(emitted) == 1 else 384):
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
            if palette >= 13:
                raise ValueError('Uninitialized palette reference')
            if local >= emitted[index] and unloaded_tile is not None:
                result = unloaded_tile(name, tid, palette, emitted[index])
                if result is not None:
                    tile_cache[entry] = result
                    return result
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
    # Render the exact periodic source border cells separately;
    # do not infer the common 2x2 pattern or stretch a map-edge screenshot.
    border_cells = [cell(b, i % bw, i // bw) for i, b in enumerate(border)]
    if render_border:
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
    world = dict(schemaVersion=1,  stableId=stable_id, sourceRef=src.ref(path, events['id']),
                 isPlayable=False,  width=width, height=height, tileSize=16,
                 borderWidth=bw, borderHeight=bh, texturePath='map.png', artifacts=artifacts,
                 layout=layout, cells=cells, borderCells=border_cells,
                 events=events, embeddedSource=[dict(path=p,
                                                  base64=base64.b64encode(src.data[p]).decode()) for p in embedded_paths if p in src.data])
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


def read_source_lock(path, *, maps, profile):
    target = pathlib.Path(path)
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != profile) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in maps]) or (not lock.get('files'))):
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


def source_topology(src,worlds):
    allmaps={}
    for p in src.tree:
        if p.startswith('data/maps/') and p.endswith('/map.json'):
            n=p.split('/')[2]; symbol='MAP_'+re.sub(r'([a-z])([A-Z])',r'\1_\2',n).upper(); allmaps[symbol]=p
    active={w['events']['id'] for w in worlds}; warps=[]; connections=[]
    for w in worlds:
        e=w['events'];symbol=e['id']
        for i,x in enumerate(e['warp_events']):
            d=src.json(allmaps[x['dest_map']]); j=int(x['dest_warp_id']); back=d['warp_events'][j]
            warps.append(dict(sourceMap=symbol,sourceIndex=i,source=x,destination=back,
              destinationInSlice=x['dest_map'] in active,reciprocalMap=back['dest_map']==symbol,
              reciprocalIndex=back['dest_map']==symbol and int(back['dest_warp_id'])==i))
        opposite={'up':'down','down':'up','left':'right','right':'left'}
        for x in e['connections'] or []:
            d=src.json(allmaps[x['map']]);back=[y for y in d['connections'] or [] if y['map']==symbol and y['direction']==opposite[x['direction']]]
            require(len(back)==1 and back[0]['offset']==-x['offset'],'connection mismatch')
            connections.append(dict(sourceMap=symbol,source=x,destinationInSlice=x['map'] in active,reciprocal=True))
    return dict(warps=warps,connections=connections)


def reciprocal_topology(src,worlds):
    by_symbol={w['events']['id']:w for w in worlds};warps=[];connections=[]
    def target(symbol):
        if symbol in by_symbol:return by_symbol[symbol]['events']
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                # Names are generated deterministically by mapjson; do not guess a URL.
                name=p.split('/')[2]
                if re.sub(r'([a-z])([A-Z])',r'\1_\2',name).upper()==symbol.removeprefix('MAP_'):
                    return src.json(p)
        raise ValueError('Unknown external map '+symbol)
    opposite={'up':'down','down':'up','left':'right','right':'left'}
    for w in worlds:
        m=w['events']
        for i,c in enumerate(m['connections'] or []):
            d=target(c['map']);back=[x for x in d['connections'] or [] if x['map']==m['id'] and x['direction']==opposite[c['direction']]]
            if len(back)!=1 or back[0]['offset']!=-c['offset']:raise ValueError('Nonreciprocal connection')
            connections.append(dict(sourceMap=m['id'],sourceIndex=i,**c,coordinateRule='destination transverse coordinate = source transverse coordinate - offset'))
        for i,e in enumerate(m['warp_events']):
            d=target(e['dest_map']);j=int(e['dest_warp_id']);end=d['warp_events'][j]
            if end['dest_map']!=m['id']:raise ValueError('Nonreciprocal warp')
            warps.append(dict(sourceMap=m['id'],sourceIndex=i,source=e,destinationMap=d['id'],destinationIndex=j,destination=end))
    return dict(connections=connections,warps=warps)


def source_directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())


def decode_geometry(src, name, *, tilesets):
    events=src.json(f'data/maps/{name}/map.json');layout=next(x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id')==events['layout'])
    attrs=[src.units('data/tilesets/'+tilesets[layout[k]]+'/metatile_attributes.bin','I') for k in ('primary_tileset','secondary_tileset')]
    names={int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)',src.text('include/constants/metatile_behaviors.h'),re.M)}
    blocks=src.units(layout['blockdata_filepath'],'H');border=src.units(layout['border_filepath'],'H')
    if len(blocks)!=layout['width']*layout['height']:raise ValueError('Grid size differs')
    cells=[]
    for i,b in enumerate(blocks):
        mid=b&1023;a=attrs[mid>=640][mid if mid<640 else mid-640];bh=a&511
        cells.append(dict(x=i%layout['width'],y=i//layout['width'],rawBlock=b,metatileId=mid,rawAttributes=a,collision=(b>>10)&3,elevation=b>>12,behaviorId=bh,behaviorName=names.get(bh,'UNKNOWN'),encounterType=(a>>24)&7))
    return dict(sourceName=name,stableId='map.'+source_directory(name),sourceRef=src.ref(f'data/maps/{name}/map.json',events['id']),width=layout['width'],height=layout['height'],layout=layout,events=events,cells=cells,borderBlocks=border)


def tileset_bindings(src):
    """Resolve tileset geometry directories from the pinned C declarations."""
    headers = src.text('src/data/tilesets/headers.h')
    declarations = src.text('src/data/tilesets/metatiles.h')
    result = {}
    for match in re.finditer(r'const struct Tileset (\w+) =\s*\{(.*?)\n\};', headers, re.S):
        symbol = re.search(r'\.metatileAttributes = (\w+)', match[2])
        require(symbol, 'Missing tileset attribute binding: ' + match[1])
        declaration = re.search(r'\b' + symbol[1] + r'\[\] = INCBIN_U32\("(.*?)"\)', declarations)
        require(declaration, 'Missing tileset attribute declaration: ' + symbol[1])
        path = declaration[1]
        require(path.startswith('data/tilesets/') and path.endswith('/metatile_attributes.bin')
                and '..' not in path.split('/'), 'Unsafe tileset declaration')
        result[match[1]] = path.removeprefix('data/tilesets/').removesuffix('/metatile_attributes.bin')
    require(result, 'Missing source tileset declarations')
    return result
