#!/usr/bin/env python3
"""Rebuild the bounded Sevii Ruby pixels from pinned Git blobs, without source execution."""
import argparse
import io
import json
import pathlib
import re
import struct
import subprocess
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-sevii-ruby-presentation-v1'
BASE = 'content/generated/sevii-ruby-presentation/'
RAW = 'content/raw/pokefirered/037335f4/'
TRAINERS = (
    'BLACK_BELT_HUGH', 'BLACK_BELT_SHEA', 'CAMPER_BRYCE', 'CRUSH_GIRL_JOCELYN',
    'CRUSH_GIRL_SHARON', 'CRUSH_GIRL_TANYA', 'CRUSH_KIN_MIK_KIA', 'FISHERMAN_TOMMY',
    'PICNICKER_CLAIRE', 'PKMN_RANGER_BETH', 'PKMN_RANGER_LOGAN', 'SWIMMER_FEMALE_ABIGAIL',
    'SWIMMER_FEMALE_MARIA', 'SWIMMER_MALE_FINN', 'SWIMMER_MALE_GARRETT',
    'TEAM_ROCKET_GRUNT_43', 'TEAM_ROCKET_GRUNT_44')
INHERITED = {'BLACK_BELT': 'content/generated/viridian-gym-presentation/black-belt-front.png', 'CAMPER': 'content/generated/pewter-trainer-art/camper-front.png', 'FISHERMAN': 'content/generated/cinnabar-trainer-art/fisherman-front.png', 'PICNICKER': 'content/generated/cerulean-trainers/picnicker-front.png', 'SWIMMER_F': 'content/generated/first-sevii-presentation/first-sevii-swimmer-f-front.png', 'SWIMMER_M': 'content/generated/cinnabar-trainer-art/swimmer-m-front.png', 'ROCKET_GRUNT_M': 'content/generated/silph-trainer-art/rocket-grunt-m-front.png'}
ITEMS = ('RUBY', 'RAINBOW_PASS', 'HM06', 'BLACK_BELT', 'DIRE_HIT', 'FIRE_STONE')


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
        data = safe(root, 'scripts/sevii-ruby-presentation.lock.json').read_bytes()
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
        # Braille uses the ordinary dialogue window, already exported by battle UI.
        texture_path = 'content/generated/lab-battle-ui/dialogue.png' if identity == 'braille-dialogue-frame' else BASE + identity + '.png'
        if identity != 'braille-dialogue-frame':
            out[identity + '.png'] = png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, texturePath=texture_path, sourcePaths=inputs)
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
    for name, species in [('Slugma', 218), ('Magcargo', 219)]:
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
    need(len(trainers) == 17 and sum(t['doubleBattle'] for t in trainers) == 1, 'Trainer cohort differs')
    for pic, number in sorted(trainer_pics.items()):
        key = pic.removeprefix('TRAINER_PIC_')
        pic_symbol = re.search(r'TRAINER_SPRITE\(' + key + r', (\w+), 0x800\)', picture_table)[1]
        pal_symbol = re.search(r'TRAINER_PAL\(' + key + r', (\w+)\)', picture_table)[1]
        paths = []
        for symbol in (pic_symbol, pal_symbol):
            match = re.search(r'const u32 ' + symbol + r'\[\] = INCBIN_U32\("([^\"]+)"\)', trainer_graphics)
            need(match, 'Trainer graphic path missing')
            paths.append(match[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        _, rgba = indexed(*paths, (64, 64))
        identity = key.lower().replace('_', '-') + '-front'
        if key in INHERITED:
            path = INHERITED[key]
            need(Image.open(io.BytesIO(source.reuse(path))).convert('RGBA').tobytes() == rgba, 'Inherited trainer source pixels differ: ' + key)
            asset = dict(id=identity, width=64, height=64, texturePath=path, sourcePaths=paths)
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
            match = re.search(r'const u32 ' + graphic + r'\[\] = INCBIN_U32\("([^\"]+)"\)', item_graphics)
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
    for map_name, map_id, constant, map_x, map_y in [
        ('MtEmber_Exterior', 'map.mt-ember-exterior', 'METATILE_MtEmber_CaveEntrance', 42, 39),
        ('OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', 'METATILE_PokemonCenter_NetworkMachine_Ruby', 13, 4)]:
        source_map = json.loads(source.read('data/maps/' + map_name + '/map.json'))
        layout = next(row for row in layouts if row.get('id') == source_map['layout'])
        script = source.text('data/maps/' + map_name + '/scripts.inc')
        need(re.search(r'setmetatile\s+' + str(map_x) + r',\s*' + str(map_y) + r',\s*' + constant + r',', script), 'Dynamic metatile script binding differs')
        bases = []
        for secondary, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            header = re.search(r'const struct Tileset ' + symbol + r'\s*=\s*\{(.*?)\};', tile_headers, re.S)[1]
            need('.isSecondary = ' + ('TRUE' if secondary else 'FALSE') in header, 'Map tileset side differs')
            image_symbol = re.search(r'\.tiles = (\w+)', header)[1]
            image_match = re.search(image_symbol + r'\[\] = INCBIN_U32\("([^\"]+)"\)', tile_graphics)
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
        tiles.append(dict(map=map_id, sourceMap=map_name, constant=constant, metatileId=mid, x=map_x, y=map_y,
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
            layers.append(dict(map=map_id, layer=layer, **emit('tiles/' + map_name.lower() + '-' + str(layer), 16, 16, bytes(rgba), bases)))

    # The outdoor mountain behavior has an independent source battle background.
    terrain_source = source.text('src/battle_bg.c')
    terrain_block = re.search(r'\[BATTLE_TERRAIN_MOUNTAIN\]\s*=\s*\{(.*?)\}', terrain_source, re.S)[1]
    for field, symbol in [('tileset', 'sBattleTerrainTiles_Mountain'), ('tilemap', 'sBattleTerrainTilemap_Mountain'), ('palette', 'sBattleTerrainPalette_Mountain')]:
        need('.' + field + ' = ' + symbol in terrain_block, 'Mountain terrain source selection differs')
    terrain_prefix = 'graphics/battle_terrain/mountain/terrain.'
    for symbol, suffix in [('sBattleTerrainTiles_Mountain', '4bpp.lz'), ('sBattleTerrainTilemap_Mountain', 'bin.lz'), ('sBattleTerrainPalette_Mountain', 'gbapal.lz')]:
        need(symbol + '[] = INCBIN_U32("' + terrain_prefix + suffix + '")' in terrain_source, 'Mountain terrain resource binding differs')
    terrain_image = Image.open(io.BytesIO(source.read(terrain_prefix + 'png')))
    need(terrain_image.mode == 'P' and terrain_image.size == (64, 80), 'Mountain terrain sheet differs')
    terrain_tiles = [bytes(terrain_image.getpixel((tx * 8 + x, ty * 8 + y)) for y in range(8) for x in range(8)) for ty in range(10) for tx in range(8)]
    terrain_palette = palette(terrain_prefix + 'pal')
    terrain_map = source.read(terrain_prefix + 'bin')
    need(len(terrain_palette) == 48 and len(terrain_map) == 4096, 'Mountain terrain palette/map framing differs')
    entries = struct.unpack('<2048H', terrain_map)
    for value in entries:
        tile, bank = value & 1023, value >> 12
        need(tile < len(terrain_tiles) and bank in (0, 2, 3, 4) and (bank or set(terrain_tiles[tile]) == {0}), 'Mountain terrain tile reference differs')
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
    need(set(terrain_rgba[3:240 * 112 * 4:4]) == {255} and set(terrain_rgba[240 * 112 * 4 + 3::4]) == {0}, 'Mountain terrain scene coverage differs')
    terrains = [dict(terrain='BATTLE_TERRAIN_MOUNTAIN', terrainId=6, **emit('mountain', 240, 112, bytes(terrain_rgba[:240 * 112 * 4]), [terrain_prefix + suffix for suffix in ('png', 'pal', 'bin')]))]

    # Fullwidth source font, with original Braille encodings and dialogue colors.
    font_source = source.text('src/braille_text.c')
    need('sBrailleGlyphs + 0x100 * (glyph / 8) + 0x10 * (glyph % 8)' in font_source and 'gGlyphInfo.width = 16;' in font_source and 'gGlyphInfo.height = 16;' in font_source, 'Braille font addressing differs')
    source.text('tools/gbagfx/font.c')
    font_rules = source.text('graphics_file_rules.mk')
    need('$(FONTGFXDIR)/braille.fwjpnfont: $(FONTGFXDIR)/braille.png' in font_rules, 'Braille conversion binding differs')
    printer_source = source.text('src/text_printer.c')
    offsets_body = re.search(r'sFontHalfRowOffsets\[\] =\s*\{(.*?)\};', printer_source, re.S)[1]
    offsets = [int(v, 16) for v in re.findall(r'0x[0-9A-Fa-f]+', offsets_body)]
    need(offsets == [sum(((value >> (i * 2)) & 3) % 3 * (3 ** i) for i in range(4)) for value in range(256)], 'Font transparent box index mapping differs')
    menu_source = source.text('src/new_menu_helpers.c')
    font_block = re.search(r'\[FONT_BRAILLE\] = \{(.*?)\}', menu_source, re.S)[1]
    for field, value in [('letterSpacing', 0), ('lineSpacing', 2), ('fgColor', 2), ('bgColor', 1), ('shadowColor', 3)]:
        need('.' + field + ' = ' + str(value) + ',' in font_block, 'Braille font metrics/colors differ')
    need('AddTextPrinterParameterized(0, FONT_BRAILLE, msg, 0, 1, 0, NULL);' in source.text('src/scrcmd.c'), 'Braille source dialogue origin differs')
    font_image_path, font_palette_path = 'graphics/fonts/braille.png', 'graphics/text_window/stdpal_0.pal'
    font_image = Image.open(io.BytesIO(source.read(font_image_path)))
    need(font_image.mode == 'P' and font_image.size == (256, 64) and max(font_image.tobytes()) == 3, 'Braille font sheet differs')
    colors = palette(font_palette_path)
    font_rgba = bytes((v for index in font_image.tobytes() for v in (*colors[[1, 2, 3, 1][index]], 255)))
    font_asset = emit('braille-font', 256, 64, font_rgba, [font_image_path, font_palette_path])
    braille_constants = {name: int(value, 16) for name, value in re.findall(r'^#define (BRAILLE_CHAR_\w+)\s+(0x[0-9A-Fa-f]+)', source.text('include/characters.h'), re.M)}
    preproc = source.text('tools/preproc/asm_file.cpp')
    glyphs = []
    for character, symbol in [(chr(c), 'BRAILLE_CHAR_' + chr(c)) for c in range(ord('A'), ord('Z') + 1)] + [(' ', 'BRAILLE_CHAR_SPACE'), ('.', 'BRAILLE_CHAR_PERIOD'), (',', 'BRAILLE_CHAR_COMMA')]:
        need("{ '" + character + "', " + symbol + ' }' in preproc, 'Braille source character mapping differs')
        glyph = braille_constants[symbol]
        glyphs.append(dict(character=character, glyph=glyph, x=glyph % 16 * 16, y=glyph // 16 * 16, width=16, height=16))
    braille_text = source.text('data/text/braille.inc')
    braille_scripts = '\n'.join(source.text('data/maps/MtEmber_RubyPath_' + floor + '/scripts.inc') for floor in ('B4F', 'B5F'))
    labels = sorted(set(re.findall(r'braillemessage(?:_wait)? (Braille_Text_\w+)', braille_scripts)))
    need(len(labels) == 18, 'Braille label scope differs')
    messages = []
    for label in labels:
        text = re.search(r'^' + label + r'::\s*\.braille "([^\"]+)"', braille_text, re.M)[1]
        need(text.endswith('$') and set(text[:-1]) <= {g['character'] for g in glyphs}, 'Braille message character scope differs')
        messages.append(dict(label=label, text=text[:-1]))
    window_block = re.search(r'sStandardTextBox_WindowTemplates\[\]\s*=\s*\{\s*\{(.*?)\}', menu_source, re.S)[1]
    for field, value in [('tilemapLeft', 2), ('tilemapTop', 15), ('width', 26), ('height', 4)]:
        need('.' + field + ' = ' + str(value) + ',' in window_block, 'Braille standard window dimensions differ')
    frame_path = 'graphics/text_window/menu_message.png'
    frame_image = Image.open(io.BytesIO(source.read(frame_path)))
    need(frame_image.mode == 'P' and frame_image.width % 8 == 0 and frame_image.height % 8 == 0, 'Dialogue frame source differs')
    frame_tiles = [bytes(frame_image.getpixel((tx * 8 + x, ty * 8 + y)) for y in range(8) for x in range(8)) for ty in range(frame_image.height // 8) for tx in range(frame_image.width // 8)]
    frame_rgba = bytearray(bytes((*colors[1], 255)) * (240 * 48))
    frame_rows = [[0, 1] + [2] * 26 + [3, 4], [5, 6] + [None] * 26 + [8, 9], [10, 11] + [None] * 26 + [12, 13],
                  [10, 11] + [None] * 26 + [12, 13], [5, 6] + [None] * 26 + [8, 9], [0, 1] + [2] * 26 + [3, 4]]
    for row, columns in enumerate(frame_rows):
        for column, tile in enumerate(columns):
            if tile is None:
                continue
            for y in range(8):
                for x in range(8):
                    index = frame_tiles[tile][(7 - y if row >= 3 else y) * 8 + x]
                    pos = ((row * 8 + y) * 240 + column * 8 + x) * 4
                    frame_rgba[pos:pos + 4] = bytes((*colors[index], 255 if index else 0))
    frame_asset = emit('braille-dialogue-frame', 240, 48, bytes(frame_rgba), [frame_path, font_palette_path])
    text_source = source.text('src/text.c')
    field_source = source.text('src/field_specials.c')
    need('x = gSpecialVar_0x8004 + 27;' in field_source and 'CreateTextCursorSprite(0, x, gSpecialVar_0x8005, 0, 0)' in field_source and
         'CreateSprite(&sSpriteTemplate_TextCursor, x + 3, y + 4, subpriority)' in text_source and '#define CURSOR_DELAY 8' in text_source,
         'Braille cursor placement/timing differs')
    need('setvar VAR_0x8005, 130' in braille_scripts, 'Braille cursor source Y differs')
    source.text('asm/macros/event.inc')
    source.text('data/event_scripts.s')
    cursor_path, cursor_palette = 'graphics/fonts/down_arrow_3.png', 'graphics/interface/std_menu.pal'
    _, cursor_rgba = indexed(cursor_path, cursor_palette, (16, 16))
    cursor_asset = emit('braille-cursor', 16, 16, cursor_rgba, [cursor_path, cursor_palette])
    braille = dict(asset=font_asset, glyphs=glyphs, messages=messages, glyphWidth=16, glyphHeight=16, letterSpacing=0, lineSpacing=2, textX=16, textY=121, window=dict(x=16, y=120, width=208, height=32), frame=dict(x=0, y=112, **frame_asset), cursor=dict(xOffset=22, y=126, centerXOffsetFromStringWidth=30, centerY=134, delayFrames=8, ticksPerOffset=9, bobOffsets=[0, 1, 2, 1], **cursor_asset))

    # Declare original sequences and cry samples already present in the source pack.
    pack = source.reuse('content/generated/synth-audio/firered.pack')
    cries = source.reuse('content/generated/synth-audio/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    songs = source.text('include/constants/songs.h')
    song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source.text('sound/song_table.inc'), re.M)
    symbol = 'MUS_SEVII_CAVE'
    song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
    address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
    need(song_table[song_id] == ('mus_sevii_cave', str(player), str(second)) and player == 0, 'Cave music table binding differs')
    midi_path = 'sound/songs/midi/mus_sevii_cave.mid'
    midi = source.read(midi_path)
    need(midi[:4] == b'MThd' and b'\xff\x06\x01[' in midi and b'\xff\x06\x01]' in midi, 'Cave music loop markers differ')
    cues = [dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=midi_path, sourcePlayer=player,
                 priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0)]
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    for name, species in [('SLUGMA', 218), ('MAGCARGO', 219)]:
        row = cry_rows[species]
        need(cry_table[species - 1].upper() == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        wave_source = source.read(path)
        need(wave_source[:4] == b'RIFF' and wave_source[8:12] == b'WAVE', 'Species source cry framing differs')
        cues.append(dict(id='CRY_' + name + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name,
                         sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))
    out['synth-manifest.json'] = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues))

    lock = encode(dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT,
                       files=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    manifest = encode(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        inputs=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)],
        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, items=items, trainers=trainers,
        portraits=portraits, tiles=tiles, layers=layers, terrains=terrains, braille=braille, newOwnedSpecies=[218, 219], inheritedOwnedSpecies=[78],
        audio=dict(additionalCueIds=[c['id'] for c in cues], originalPackUnchanged=True, newAudioAssets=0, physicalAudioVerified=False),
        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]))
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})
    result['scripts/sevii-ruby-presentation.lock.json'] = lock
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
