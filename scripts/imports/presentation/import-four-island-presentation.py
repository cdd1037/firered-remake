#!/usr/bin/env python3
"""Rebuild the bounded Four Island pixels from pinned Git blobs, without source execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from runtime_json import runtime_json_bytes
import argparse
import io
import json
import pathlib
import re
import struct
import subprocess
from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-four-island-presentation-v1'
BASE = 'content/generated/sprites/pokemon/four-island/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Seel', 86), ('Dewgong', 87), ('Wooper', 194), ('Quagsire', 195), ('Swinub', 220), ('Piloswine', 221), ('Delibird', 225)]
TRAINERS = ('TEAM_ROCKET_GRUNT_45',)
INHERITED = {'ROCKET_GRUNT_M': 'content/generated/sprites/trainers/silph/rocket-grunt-m-front.png'}
ITEMS = ('HM07', 'NEVER_MELT_ICE')


from image_io import png as encode_rgba_png

def need(ok, message):
    if not ok:
        raise ValueError(message)


def encode(value):
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + '\n').encode()


def safe(root, path):
    need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and
         ':' not in path and all(p not in ('', '.', '..') for p in path.split('/')), 'Unsafe presentation path')
    result = pathlib.Path(root).absolute() / path
    need(not any(p.is_symlink() for p in (result, *result.parents)), 'Presentation symlink rejected')
    return result


png = encode_rgba_png


class Source:
    def __init__(self, root, reference):
        self.root, self.reference = root, reference
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        data = safe(root, 'scripts/recipes/presentation/four-island-presentation.lock.json').read_bytes()
        self.lock = json.loads(data)
        need((self.lock['schemaVersion'], self.lock['profile'], self.lock['repository'], self.lock['commit']) == (1, PROFILE, 'pret/pokefirered', COMMIT), 'Source lock identity differs')

    def read(self, path):
        if path not in self.raw:
            data = subprocess.check_output(['git', '-C', str(self.reference), 'show', COMMIT + ':' + path]) if self.reference else safe(self.root, RAW + path).read_bytes()
            row = dict(path=path)
            if self.lock:
                need(row in self.lock['files'], 'Source input differs: ' + path)
            self.raw[path], self.files[path] = data, row
        return self.raw[path]

    def text(self, path):
        return self.read(path).decode()

    def reuse(self, path):
        data = safe(self.root, path).read_bytes()
        row = dict(path=path)
        if self.lock:
            need(row in self.lock['inheritedInputs'], 'Inherited pixels differ: ' + path)
        self.inherited[path] = row
        return data


def build(root, reference):
    source = Source(root, reference)
    out, assets = {}, []

    def emit(identity, width, height, rgba, inputs):
        need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
        out[identity + '.png'] = png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png')
        assets.append(row)
        return row

    def palette(path, floor=False):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette framing differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor=False, first_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    sprites, icons, footprints = [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    for name, species in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
        elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
        elevation = int(elevation_match[1]) if elevation_match else 0
        for side in ('front', 'back'):
            coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinates))
            need(match, 'Species coordinates missing')
            width, height, y_offset = map(int, match.groups())
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
            for variant in ('normal', 'shiny'):
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                indices, rgba = indexed(image, pal, (64, 64), floor=True)
                x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, side=side, paletteVariant=variant,
                    x=x, y=y, sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                     sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)',
                    transparentIndex=0, **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
        icon_table = source.text('src/pokemon_icon.c')
        match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
        need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
        pal_index = int(match[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
        indices, rgba = indexed(image, pal, (32, 64), floor=True, first_frame=True)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                          sourceFrameCount=2,  **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source pixels differ')
        indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

    # Dewgong already appears as a Victory Road opponent. Keep its inherited
    # pixels bit-identical while adding its owned dex footprint in this profile.
    for side in ('front', 'back'):
        for variant in ('normal', 'shiny'):
            name = f'dewgong-{side}-{variant}.png'
            need(Image.open(io.BytesIO(source.reuse('content/generated/sprites/pokemon/victory-road/' + name))).convert('RGBA').tobytes() == Image.open(io.BytesIO(out[name])).convert('RGBA').tobytes(), 'Inherited Dewgong battle pixels differ')
    need(Image.open(io.BytesIO(source.reuse('content/generated/sprites/pokemon/victory-road/icons/dewgong.png'))).convert('RGBA').tobytes() == Image.open(io.BytesIO(out['icons/dewgong.png'])).convert('RGBA').tobytes(), 'Inherited Dewgong icon pixels differ')

    trainers, portraits = [], []
    trainer_source, trainer_constants = source.text('src/data/trainers.h'), source.text('include/constants/trainers.h')
    opponents, classes = source.text('include/constants/opponents.h'), source.text('src/data/text/trainer_class_names.h')
    trainer_graphics = source.text('src/data/graphics/trainers.h')
    picture_table = source.text('src/data/trainer_graphics/front_pic_tables.h')
    coords = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}', picture_table.split('const struct CompressedSpriteSheet')[0])
    trainer_pics = {}
    for name in TRAINERS:
        symbol = 'TRAINER_' + name
        match = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\n    \}', trainer_source, re.S)
        need(match, 'Trainer source record missing: ' + symbol)
        body = match[1]
        cls, pic = re.search(r'\.trainerClass = (\w+)', body)[1], re.search(r'\.trainerPic = (\w+)', body)[1]
        display_name = re.search(r'\.trainerName = _\("([^\"]+)"\)', body)[1]
        cls_name = re.search(r'\[' + cls + r'\]\s*= _\("([^\"]+)"\)', classes)[1]
        source_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)\s*$', opponents, re.M)[1])
        pic_id = int(re.search(r'^#define ' + pic + r'\s+(\d+)\s*$', trainer_constants, re.M)[1])
        music = re.search(r'\.encounterMusic_gender = ([^,]+),', body)[1]
        music_value = 0
        for part in music.split('|'):
            part = part.strip()
            music_value |= 128 if part == 'F_TRAINER_FEMALE' else int(re.search(r'^#define ' + part + r'\s+(\d+)', trainer_constants, re.M)[1])
        portrait_id = pic.removeprefix('TRAINER_PIC_').lower().replace('_', '-') + '-front'
        trainers.append(dict(trainer='pret:' + symbol, sourceTrainerId=source_id, name=display_name,
            trainerClass=cls, className=cls_name, trainerPic=pic, portrait=portrait_id,
            encounterMusicGender=music_value, doubleBattle='.doubleBattle = TRUE,' in body))
        trainer_pics[pic] = pic_id
    need(len(trainers) == 1 and not trainers[0]['doubleBattle'], 'Trainer cohort differs')
    for pic, number in sorted(trainer_pics.items()):
        key = pic.removeprefix('TRAINER_PIC_')
        pic_symbol = re.search(r'TRAINER_SPRITE\(' + key + r', (\w+), 0x800\)', picture_table)[1]
        pal_symbol = re.search(r'TRAINER_PAL\(' + key + r', (\w+)\)', picture_table)[1]
        paths = []
        for symbol in (pic_symbol, pal_symbol):
            match = re.search(r'const u32 ' + symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', trainer_graphics)
            need(match, 'Trainer graphic path missing')
            paths.append(match[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        _, rgba = indexed(*paths, (64, 64))
        identity = key.lower().replace('_', '-') + '-front'
        if key in INHERITED:
            path = INHERITED[key]
            need(Image.open(io.BytesIO(source.reuse(path))).convert('RGBA').tobytes() == rgba, 'Inherited trainer source pixels differ: ' + key)
            asset = dict(id=identity, width=64, height=64, texturePath=path)
            assets.append(asset)
        else:
            asset = emit(identity, 64, 64, rgba, paths)
        need(coords[number][0] == '8', 'Trainer portrait source size differs')
        portraits.append(dict(trainerPic=pic, sourceTrainerPicId=number, inherited=key in INHERITED,
                              x=144, y=8, sourceYOffset=int(coords[number][1]), **asset))

    items = []
    item_table, item_graphics = source.text('src/data/item_icon_table.h'), source.text('src/data/graphics/items.h')
    item_data = json.loads(source.read('src/data/items.json'))['items']
    for name in ITEMS:
        symbol = 'ITEM_' + name
        binding = re.search(r'\[' + symbol + r'\]\s*=\s*\{(\w+),\s*(\w+)\}', item_table)
        need(binding, 'Item source binding missing')
        paths = []
        for graphic in binding.groups():
            match = re.search(r'const u32 ' + graphic + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', item_graphics)
            need(match, 'Item graphic path missing')
            paths.append(match[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        _, rgba = indexed(*paths, (24, 24))
        row = next(d for d in item_data if d['itemId'] == symbol)
        items.append(dict(item='pret:' + symbol, name=row['english'], description=row['description_english'].replace('\\n', '\n'),
            pocket=row['pocket'], fieldUseFunction=row['fieldUseFunc'], **emit('item-' + name.lower().replace('_', '-'), 24, 24, rgba, paths)))

    tiles, layers = [], []
    labels = {k: int(v, 16) for k, v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)', source.text('include/constants/metatile_labels.h'), re.M)}
    layouts = json.loads(source.read('data/layouts/layouts.json'))['layouts']
    tile_graphics = source.text('src/data/tilesets/graphics.h') + '\n' + source.text('src/graphics.c')
    tile_headers = source.text('src/data/tilesets/headers.h')
    tile_bindings = source.text('src/data/tilesets/metatiles.h')
    tile_rules = source.text('tileset_rules.mk')
    for constant in ('METATILE_SeafoamIslands_CrackedIce', 'METATILE_SeafoamIslands_IceHole'):
        map_name, map_id = 'FourIsland_IcefallCave_1F', 'map.four-island-icefall-cave-1f'
        source_map = json.loads(source.read('data/maps/' + map_name + '/map.json'))
        layout = next(row for row in layouts if row.get('id') == source_map['layout'])
        field_tasks = source.text('src/field_tasks.c')
        need(constant in field_tasks, 'Dynamic ice metatile callback binding differs')
        bases = []
        for secondary, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            header = re.search(r'const struct Tileset ' + symbol + r'\s*=\s*\{(.*?)\};', tile_headers, re.S)[1]
            need('.isSecondary = ' + ('TRUE' if secondary else 'FALSE') in header, 'Map tileset side differs')
            image_symbol = re.search(r'\.tiles = (\w+)', header)[1]
            image_match = re.search(image_symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', tile_graphics)
            need(image_match, 'Map tileset graphics binding missing')
            base = image_match[1].rsplit('/', 1)[0]
            for field, suffix in [('metatiles', 'metatiles.bin'), ('metatileAttributes', 'metatile_attributes.bin')]:
                binding = re.search(r'\.' + field + r' = (\w+)', header)[1]
                need(re.search(binding + r'\[\] = INCBIN_U(?:16|32)\("' + re.escape(base + '/' + suffix) + r'"\)', tile_bindings), 'Map metatile binding differs')
            bases.append(base)
        images = [Image.open(io.BytesIO(source.read(base + '/tiles.png'))) for base in bases]
        palettes = [palette(bases[0 if n < 7 else 1] + f'/palettes/{n:02d}.pal') for n in range(13)]
        palettes[0][0] = (0, 0, 0)
        emitted = []
        for base, im in zip(bases, images):
            need(im.mode == 'P' and im.width % 8 == 0 and im.height % 8 == 0 and max(im.tobytes()) < 16, 'Map tile pixels differ')
            match = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', tile_rules)
            emitted.append(int(match[1]) if match else im.width * im.height // 64)
        mid = labels[constant]
        bi, local = (0, mid) if mid < 640 else (1, mid - 640)
        attribute = struct.unpack_from('<I', source.read(bases[bi] + '/metatile_attributes.bin'), local * 4)[0]
        values = struct.unpack_from('<8H', source.read(bases[bi] + '/metatiles.bin'), local * 16)
        layer_type = (attribute >> 29) & 3
        groups = {0: [[0x3014] * 4, values[:4], values[4:]], 1: [values[:4], values[4:], [0] * 4], 2: [values[:4], [0] * 4, values[4:]]}.get(layer_type)
        need(groups is not None, 'Reserved map layer type')
        tiles.append(dict(map=map_id, sourceMap=map_name, constant=constant, metatileId=mid,
                          layerType=layer_type, rawAttributes=attribute, tileEntries=values))
        for layer, group in enumerate(groups):
            rgba = bytearray(16 * 16 * 4)
            for n, value in enumerate(group):
                tid, pal = value & 1023, value >> 12
                tile_bi, tile_local = (0, tid) if tid < 640 else (1, tid - 640)
                im = images[tile_bi]
                need(pal < 13 and tile_local < emitted[tile_bi], 'Uninitialized map tile/palette')
                for y in range(8):
                    for x in range(8):
                        ix, iy = (7 - x if value & 0x400 else x), (7 - y if value & 0x800 else y)
                        color = im.getpixel((tile_local % (im.width // 8) * 8 + ix, tile_local // (im.width // 8) * 8 + iy))
                        pos = ((n // 2 * 8 + y) * 16 + n % 2 * 8 + x) * 4
                        rgba[pos:pos + 4] = bytes((*palettes[pal][color], 255 if color else 0))
            layers.append(dict(map=map_id, metatileId=mid, layer=layer, **emit('tiles/' + str(mid) + '-' + str(layer), 16, 16, bytes(rgba), bases)))

    terrains = []
    for terrain, terrain_id in [('cave', 7), ('pond', 5)]:
        terrain_source = source.text('src/battle_bg.c')
        terrain_block = re.search(r'\[BATTLE_TERRAIN_' + terrain.upper() + r'\]\s*=\s*\{(.*?)\}', terrain_source, re.S)[1]
        for field, symbol in [('tileset', 'sBattleTerrainTiles_' + terrain.title()), ('tilemap', 'sBattleTerrainTilemap_' + terrain.title()), ('palette', 'sBattleTerrainPalette_' + terrain.title())]:
            need('.' + field + ' = ' + symbol in terrain_block, f'{terrain.title()} terrain source selection differs')
        terrain_prefix = f'graphics/battle_terrain/{terrain}/terrain.'
        for symbol, suffix in [('sBattleTerrainTiles_' + terrain.title(), '4bpp.lz'), ('sBattleTerrainTilemap_' + terrain.title(), 'bin.lz'), ('sBattleTerrainPalette_' + terrain.title(), 'gbapal.lz')]:
            need(symbol + '[] = INCBIN_U32("' + terrain_prefix + suffix + '")' in terrain_source, f'{terrain.title()} terrain resource binding differs')
        terrain_image = Image.open(io.BytesIO(source.read(terrain_prefix + 'png')))
        need(terrain_image.mode == 'P' and terrain_image.width % 8 == 0 and terrain_image.height % 8 == 0, f'{terrain.title()} terrain sheet differs')
        terrain_tiles = [bytes(terrain_image.getpixel((tx * 8 + x, ty * 8 + y)) for y in range(8) for x in range(8)) for ty in range(terrain_image.height // 8) for tx in range(terrain_image.width // 8)]
        terrain_palette = palette(terrain_prefix + 'pal')
        terrain_map = source.read(terrain_prefix + 'bin')
        need(len(terrain_palette) == 48 and len(terrain_map) == 4096, f'{terrain.title()} terrain palette/map framing differs')
        entries = struct.unpack('<2048H', terrain_map)
        for value in entries:
            tile, bank = value & 1023, value >> 12
            need(tile < len(terrain_tiles) and bank in (0, 2, 3, 4) and (bank or set(terrain_tiles[tile]) == {0}), f'{terrain.title()} terrain tile reference differs')
        terrain_rgba = bytearray(240 * 160 * 4)
        for y in range(160):
            for x in range(240):
                value = entries[(y // 8) * 32 + x // 8]
                bank = value >> 12
                if bank == 0:
                    continue
                ix, iy = (7 - x % 8 if value & 1024 else x % 8), (7 - y % 8 if value & 2048 else y % 8)
                color = terrain_tiles[value & 1023][iy * 8 + ix]
                terrain_rgba[(y * 240 + x) * 4:(y * 240 + x + 1) * 4] = bytes((*terrain_palette[(bank - 2) * 16 + color], 255 if color else 0))
        need(set(terrain_rgba[3:240 * 112 * 4:4]) == {255} and set(terrain_rgba[240 * 112 * 4 + 3::4]) == {0}, f'{terrain.title()} terrain scene coverage differs')
        terrains.append(dict(terrain='BATTLE_TERRAIN_' + terrain.upper(), terrainId=terrain_id, **emit(terrain, 240, 112, bytes(terrain_rgba[:240 * 112 * 4]), [terrain_prefix + suffix for suffix in ('png', 'pal', 'bin')])))


    # Declare original sequences and cry samples already present in the source pack.
    pack = source.reuse('content/generated/audio/synth/firered.pack')
    cries = source.reuse('content/generated/audio/synth/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    songs = source.text('include/constants/songs.h')
    song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source.text('sound/song_table.inc'), re.M)
    symbol = 'MUS_SEVII_45'
    song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
    address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
    need(song_table[song_id] == ('mus_sevii_45', str(player), str(second)) and player == 0, 'Cave music table binding differs')
    midi_path = 'sound/songs/midi/mus_sevii_45.mid'
    midi = source.read(midi_path)
    need(midi[:4] == b'MThd' and b'\xff\x06\x01[' in midi and b'\xff\x06\x01]' in midi, 'Cave music loop markers differ')
    cues = [dict(id=symbol, sourceSymbol=symbol, sourcePlayer=player, priority=pack[address - 138975592 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0)]
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    for name, species in [(name.upper(), number) for name, number in SPECIES if number != 87]:
        row = cry_rows[species]
        need(cry_table[species - 1].upper() == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        wave_source = source.read(path)
        need(wave_source[:4] == b'RIFF' and wave_source[8:12] == b'WAVE', 'Species source cry framing differs')
        cues.append(dict(id='CRY_' + name + '_NORMAL', sourceSymbol='SPECIES_' + name, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))
    # Lorelei's original script uses Lapras's aggressive encounter cry, mode 2.
    # Reuse the already authenticated sample pack with the exact sound.c controls.
    sound = source.text('src/sound.c')
    encounter = re.search(r'case CRY_MODE_ENCOUNTER:(.*?)break;', sound, re.S)[1]
    need(all(re.search(r'\b' + name + r'\s*=\s*' + str(value) + r';', encounter) for name, value in
             [('release', 225), ('pitch', 15600), ('chorus', 20), ('volume', 90)]), 'Encounter cry source parameters differ')
    need(re.search(r'#define CRY_MODE_ENCOUNTER\s+2\b', source.text('include/constants/sound.h')), 'Encounter cry source mode differs')
    script = source.text('data/maps/FourIsland_IcefallCave_Back/scripts.inc')
    need('playmoncry SPECIES_LAPRAS, CRY_MODE_ENCOUNTER' in script and re.search(r'#define SPECIES_LAPRAS\s+131\b', constants), 'Lorelei Lapras cry binding differs')
    row = cry_rows[131]
    need(cry_table[130] == 'Lapras' and row[1] == 130, 'Lapras cry table differs')
    tone = 0x0848c914 + row[1] * 12 - 0x08489968
    wave = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
    need(struct.unpack_from('<HHIII', pack, wave) == (1, 0, 10764288, 0, row[2]), 'Lapras packed cry header differs')
    path = 'sound/direct_sound_samples/cries/lapras.wav'
    wave_source = source.read(path)
    need(wave_source[:4] == b'RIFF' and wave_source[8:12] == b'WAVE', 'Lapras source cry framing differs')
    cues.append(dict(id='CRY_LAPRAS_ENCOUNTER', sourceSymbol='SPECIES_LAPRAS', sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=131, looping=False, cryMode=2))
    inherited_seel = next(c for c in cues if c['id'] == 'CRY_SEEL_NORMAL')
    original_manifest = json.loads(source.reuse('content/generated/audio/synth/manifest.json'))
    original_seel = next(c for c in original_manifest['cues'] if c['id'] == 'CRY_SEEL_NORMAL')
    need(dict(original_seel, cryMode=original_seel.get('cryMode', 0)) == inherited_seel, 'Inherited Seel cue differs')
    cues = [c for c in cues if c['id'] != 'CRY_SEEL_NORMAL']
    out['synth-manifest.json'] = runtime_manifest_bytes(dict(schemaVersion=1, cues=cues, inheritedCues=[inherited_seel]))

    lock = encode(dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT,
                       files=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, items=items, trainers=trainers,
        portraits=portraits, tiles=tiles, layers=layers, terrains=terrains, newOwnedSpecies=[number for _, number in SPECIES], inheritedOpponentSprites=[87],
        audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=['CRY_SEEL_NORMAL'],  ),
        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})
    result['scripts/recipes/presentation/four-island-presentation.lock.json'] = lock
    report = dict(passed=True, sourceFiles=len(source.raw), sprites=len(sprites), pokemonIcons=len(icons), footprints=len(footprints),
        trainers=len(trainers), portraits=len(portraits), newPortraits=sum(not p['inherited'] for p in portraits),
        itemIcons=len(items), dynamicMetatiles=len(tiles), dynamicLayers=len(layers), outputs=len(out) - 1,
         lockBytes=len(lock),  manifestBytes=len(manifest),
         synthBytes=len(out['synth-manifest.json']), sourceAndStaticChecksOnly=True)
    return result, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root)
    for relative, data in output.items():
        target = safe(args.project_root, relative)
        if args.check:
            need(target.read_bytes() == data, 'Generated bytes differ: ' + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(report, sort_keys=True))
