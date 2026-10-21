#!/usr/bin/env python3
"""Rebuild the bounded Sevii Ruby pixels from pinned Git blobs, without source execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
from item_source import selected_item_art
from trainer_portraits import selected_trainer_art
from species_art import selected_species_art
from presentation_source import Source, need
from presentation_output import publish
import argparse
import io
import json
import pathlib
import re
import struct
from PIL import Image

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/sevii-ruby/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Slugma', 218), ('Magcargo', 219)]
TRAINERS = (
    'BLACK_BELT_HUGH', 'BLACK_BELT_SHEA', 'CAMPER_BRYCE', 'CRUSH_GIRL_JOCELYN',
    'CRUSH_GIRL_SHARON', 'CRUSH_GIRL_TANYA', 'CRUSH_KIN_MIK_KIA', 'FISHERMAN_TOMMY',
    'PICNICKER_CLAIRE', 'PKMN_RANGER_BETH', 'PKMN_RANGER_LOGAN', 'SWIMMER_FEMALE_ABIGAIL',
    'SWIMMER_FEMALE_MARIA', 'SWIMMER_MALE_FINN', 'SWIMMER_MALE_GARRETT',
    'TEAM_ROCKET_GRUNT_43', 'TEAM_ROCKET_GRUNT_44')
INHERITED = {'BLACK_BELT': 'content/generated/ui/viridian-gym-presentation/black-belt-front.png', 'CAMPER': 'content/generated/sprites/trainers/pewter/camper-front.png', 'FISHERMAN': 'content/generated/sprites/trainers/cinnabar/fisherman-front.png', 'PICNICKER': 'content/generated/data/trainer-battles/cerulean/picnicker-front.png', 'SWIMMER_F': 'content/generated/ui/first-sevii-presentation/first-sevii-swimmer-f-front.png', 'SWIMMER_M': 'content/generated/sprites/trainers/cinnabar/swimmer-m-front.png', 'ROCKET_GRUNT_M': 'content/generated/sprites/trainers/silph/rocket-grunt-m-front.png'}
ITEMS = ('RUBY', 'RAINBOW_PASS', 'HM06', 'BLACK_BELT', 'DIRE_HIT', 'FIRE_STONE')


from image_io import png as encode_rgba_png


png = encode_rgba_png


def build(root, reference):
    source = Source(root, reference)
    out, assets = {}, []

    def emit(identity, width, height, rgba, inputs):
        need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
        # Braille uses the ordinary dialogue window, already exported by battle UI.
        texture_path = 'content/generated/ui/lab-battle-ui/dialogue.png' if identity == 'braille-dialogue-frame' else BASE + identity + '.png'
        if identity != 'braille-dialogue-frame':
            out[identity + '.png'] = png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, texturePath=texture_path)
        assets.append(row)
        return row

    pixels = PixelReader(source)
    palette, indexed = pixels.palette, pixels.indexed

    sprites, icons, footprints = selected_species_art(source, SPECIES, SPECIES, emit)

    trainers, portraits = selected_trainer_art(source, TRAINERS, INHERITED, emit, assets, inherited_ids=False)
    need(len(trainers) == 17 and sum(t['doubleBattle'] for t in trainers) == 1, 'Trainer cohort differs')

    items = selected_item_art(source, ITEMS, emit)

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


    manifest = runtime_json_bytes(dict(schemaVersion=1,

        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, items=items, trainers=trainers,
        portraits=portraits, tiles=tiles, layers=layers, terrains=terrains, braille=braille, newOwnedSpecies=[218, 219], inheritedOwnedSpecies=[78],

        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.'],
        outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})

    report = dict(passed=True, sourceFiles=len(source.raw), sprites=len(sprites), pokemonIcons=len(icons), footprints=len(footprints),
        trainers=len(trainers), portraits=len(portraits), newPortraits=sum(not p['inherited'] for p in portraits),
        itemIcons=len(items), dynamicMetatiles=len(tiles), dynamicLayers=len(layers), outputs=len(out) - 1,
         manifestBytes=len(manifest),
          sourceAndStaticChecksOnly=True)
    return result, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root)
    publish(args.project_root, output, check=args.check)
    print(json.dumps(report, sort_keys=True))
