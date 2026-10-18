#!/usr/bin/env python3
"""Independent source pixels and bounded shared-door importer regressions."""
import argparse
import io
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from field_door_art import compile_outputs, source_doors
from world_selection import load_selection
from world_source import WorldSource

RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
PALLET = 'LAYOUT_PALLET_TOWN'
PALLET_DOOR = 'METATILE_PalletTown_Door'
PNG = 'graphics/door_anims/pallet.png'
PALETTE = 'data/tilesets/secondary/pallet_town/palettes/08.pal'
RECIPE = ROOT / 'scripts/recipes/presentation/field-door-art.json'
UNUSED_WARP = 'METATILE_VermilionCity_SSAnneWarp'


def source():
    return WorldSource(RAW)


def unpack(outputs):
    return json.loads(outputs['maps/door-animations.json']), Image.open(io.BytesIO(outputs['maps/door-animations.png']))


def current_layouts():
    selected = load_selection(ROOT / 'data/world-maps.json')
    return {row.get('layout') or json.loads((RAW / 'data/maps' / row['sourceName'] / 'map.json').read_bytes())['layout']
            for row in selected}


def independent_small_doors():
    """Read original declarations without any importer parser or palette helper."""
    text = (RAW / 'src/field_door.c').read_text()
    labels = (RAW / 'include/constants/metatile_labels.h').read_text()
    result = {}
    for line in text.splitlines():
        match = re.fullmatch(r'\s*\{(METATILE_\w+),\s*(DOOR_SOUND_\w+),\s*DOOR_SIZE_1x1,\s*(\w+),\s*(\w+)\},', line)
        if not match:
            continue
        name, sound, tiles, palette = match.groups()
        metatile = int(re.search(r'^#define\s+' + name + r'\s+(0x[\da-fA-F]+)\b', labels, re.M)[1], 16)
        path = re.search(r'\b' + tiles + r'\[\] = INCBIN_U8\("([^"]+)"\)', text)[1]
        numbers = re.search(r'\b' + palette + r'\[\] = \{([^}]+)\}', text)[1]
        result[name] = dict(metatile=metatile, sound={'DOOR_SOUND_NORMAL': 'SE_DOOR', 'DOOR_SOUND_SLIDING': 'SE_SLIDING_DOOR'}[sound],
                            path=path.replace('.4bpp', '.png'), palettes=[int(x) for x in numbers.split(',')])
    if not result:
        raise AssertionError('No original small-door rows found')
    return result


class OriginalLayout:
    """Independent source declarations and pixels for one current layout."""
    def __init__(self, layout):
        headers = (RAW / 'src/data/tilesets/headers.h').read_text()
        declarations = '\n'.join((RAW / name).read_text() for name in
                                 ('src/data/tilesets/metatiles.h', 'src/data/tilesets/graphics.h', 'src/graphics.c'))
        self.attributes, self.palettes = [], []
        for bank, key in enumerate(('primary_tileset', 'secondary_tileset')):
            body = re.search(r'\b' + layout[key] + r'\s*=\s*\{([^}]+)\}', headers)[1]
            fields = dict(re.findall(r'\.(\w+)\s*=\s*(\w+)', body))
            path = re.search(r'\b' + fields['metatileAttributes'] + r'\[\] = INCBIN_U32\("([^"]+)"\)', declarations)[1]
            self.attributes.append([x[0] for x in struct.iter_unpack('<I', (RAW / path).read_bytes())])
            if bank == 0:
                path = re.search(r'\b' + fields['tiles'] + r'\[\] = INCBIN_U32\("([^"]+)"\)', declarations)[1]
                self.primary = Image.open(RAW / path.replace('.4bpp.lz', '.png'))
                self.primary.load()
            palette_body = re.search(r'\b' + fields['palettes'] + r'\[\]\[16\]\s*=\s*\{([^}]+)\}', declarations)[1]
            palette_paths = re.findall(r'INCBIN_U16\("([^"]+)"\)', palette_body)
            for slot in (range(7) if bank == 0 else range(7, 13)):
                lines = (RAW / palette_paths[slot].replace('.gbapal', '.pal')).read_text().splitlines()
                if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19:
                    raise AssertionError('Invalid original field palette')
                self.palettes.append([tuple((int(v) // 8) * 8 + (int(v) // 32) for v in line.split()) for line in lines[3:]])
        self.palettes[0][0] = (0, 0, 0)

    def is_door(self, metatile, behavior):
        bank, index = (0, metatile) if metatile < 640 else (1, metatile - 640)
        return index < len(self.attributes[bank]) and self.attributes[bank][index] & 0x1ff == behavior

    def rgba(self, door):
        original = Image.open(RAW / door['path'])
        if original.mode != 'P' or original.size != (16, 48) or original.getextrema()[1] > 15:
            raise AssertionError('Invalid original small-door sheet')
        pixels = bytearray()
        for y in range(48):
            for x in range(48):
                layer, local_x = divmod(x, 16)
                quadrant = (y % 16 // 8) * 2 + local_x // 8
                index = original.getpixel((local_x, y)) if layer == 0 else self.primary.getpixel((local_x % 8, y % 8))
                slot = 0 if layer == 2 else door['palettes'][quadrant + layer * 4]
                pixels.extend((*self.palettes[slot][index], 255 if index else 0))
        return bytes(pixels)


class FieldDoorArtTests(unittest.TestCase):
    def test_recipe_admits_all_real_small_doors_but_not_unused_warp_or_large_doors(self):
        rows = independent_small_doors()
        admitted = json.loads(RECIPE.read_bytes())['metatiles']
        self.assertEqual(len(admitted), len(set(admitted)))
        self.assertEqual(set(rows) - {UNUSED_WARP}, set(admitted))
        # The original table documents this reused harbor warp as having no door.
        self.assertIn('warp to enter the SS Anne from the harbor, which has no door.', (RAW / 'src/field_door.c').read_text())

    def test_all_styles_and_current_layouts_match_independent_source_rgba_and_sounds(self):
        rows = independent_small_doors()
        del rows[UNUSED_WARP]
        layouts = {row['id']: row for row in json.loads((RAW / 'data/layouts/layouts.json').read_bytes())['layouts'] if 'id' in row}
        selected = current_layouts()
        document, atlas = unpack(compile_outputs(source(), selected))
        actual = {(entry['layout'], entry['metatileId']): entry for entry in document['entries']}
        self.assertEqual(len(document['entries']), len(actual))
        behavior = int(re.search(r'^#define\s+MB_WARP_DOOR\s+(0x[\da-fA-F]+)\b',
                                 (RAW / 'include/constants/metatile_behaviors.h').read_text(), re.M)[1], 16)
        expected, represented, sounds = set(), set(), set()
        for name in sorted(selected):
            original = OriginalLayout(layouts[name])
            for label, door in rows.items():
                if not original.is_door(door['metatile'], behavior):
                    continue
                key = name, door['metatile']
                expected.add(key)
                represented.add(label)
                sounds.add(door['sound'])
                with self.subTest(layout=name, door=label):
                    self.assertIn(key, actual)
                    entry = actual[key]
                    self.assertEqual({'layout', 'metatileId', 'soundEffect', 'width', 'height', 'atlasY'}, set(entry))
                    self.assertEqual(door['sound'], entry['soundEffect'])
                    self.assertEqual((16, 16), (entry['width'], entry['height']))
                    self.assertEqual(0, entry['atlasY'] % 48)
                    self.assertGreaterEqual(entry['atlasY'], 0)
                    self.assertLessEqual(entry['atlasY'] + 48, atlas.height)
                    self.assertEqual(original.rgba(door), atlas.crop((0, entry['atlasY'], 48, entry['atlasY'] + 48)).tobytes())
        self.assertEqual(expected, set(actual))
        self.assertEqual(set(rows), represented)
        self.assertEqual({'SE_DOOR', 'SE_SLIDING_DOOR'}, sounds)
        self.assertGreater(len(expected), len(rows))

    def test_dynamic_warehouse_door_is_bound_even_before_source_script_replaces_locked_tile(self):
        name = 'METATILE_SeviiIslands45_RocketWarehouseDoor_Unlocked'
        metatile = independent_small_doors()[name]['metatile']
        raw_map = json.loads((RAW / 'data/maps/FiveIsland_Meadow/map.json').read_bytes())
        layout = next(row for row in json.loads((RAW / 'data/layouts/layouts.json').read_bytes())['layouts'] if row.get('id') == raw_map['layout'])
        blocks = [x[0] & 1023 for x in struct.iter_unpack('<H', (RAW / layout['blockdata_filepath']).read_bytes())]
        self.assertNotIn(metatile, blocks)
        self.assertIn('setmetatile 12, 21, ' + name + ', 1', (RAW / 'data/maps/FiveIsland_Meadow/scripts.inc').read_text())
        document, _ = unpack(compile_outputs(source(), {raw_map['layout']}))
        self.assertEqual(1, sum(row['metatileId'] == metatile for row in document['entries']))

    def test_source_table_supplies_identity_size_sound_pixels_and_palette_slots(self):
        row = source_doors(source())['METATILE_PalletTown_Door']
        self.assertEqual(dict(metatile=0x2a3, height=16, path=PNG,
                              palettes=[8] * 8, sound='SE_DOOR'), row)

    def test_every_rgba_matches_independent_original_pixels_with_current_map_palettes(self):
        src = source()
        document, actual = unpack(compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR]))
        self.assertEqual([dict(layout=PALLET, metatileId=0x2a3, soundEffect='SE_DOOR',
                               width=16, height=16, atlasY=0)], document['entries'])
        self.assertEqual((48, 48), actual.size)
        original = Image.open(RAW / PNG)
        primary = Image.open(RAW / 'data/tilesets/primary/general/tiles.png')
        # GBA 5-bit expansion is bit replication, not a floating-point recolor.
        colors = [tuple((int(v) >> 3) * 8 + (int(v) >> 5) for v in line.split())
                  for line in (RAW / PALETTE).read_text().splitlines()[3:]]
        zeros = [tuple((int(v) >> 3) * 8 + (int(v) >> 5) for v in line.split())
                 for line in (RAW / 'data/tilesets/primary/general/palettes/00.pal').read_text().splitlines()[3:]]
        zeros[0] = (0, 0, 0)
        transparent = 0
        for y in range(48):
            for x in range(48):
                layer, local_x = divmod(x, 16)
                index = original.getpixel((local_x, y)) if layer == 0 else primary.getpixel((local_x % 8, y % 8))
                color = (zeros if layer == 2 else colors)[index]
                expected = (*color, 255 if index else 0)
                self.assertEqual(expected, actual.getpixel((x, y)), (x, y))
                transparent += index == 0
        self.assertEqual(48 * 32, transparent)
        # The PNG's editing palette is deliberately not the current map palette.
        self.assertNotEqual(original.convert('RGBA').tobytes(), actual.crop((0, 0, 16, 48)).tobytes())

    def test_three_layers_replace_closed_art_including_alpha(self):
        _, atlas = unpack(compile_outputs(source(), {PALLET}, admitted=[PALLET_DOOR]))
        for layer in range(3):
            old = Image.new('RGBA', (32, 32), (231, 12, 197, 255))
            replacement = atlas.crop((layer * 16, 32, layer * 16 + 16, 48))
            old.paste(replacement, (8, 8))  # No alpha mask: equivalent to Image.BlitRect.
            self.assertEqual(replacement.tobytes(), old.crop((8, 8, 24, 24)).tobytes())
            self.assertEqual((231, 12, 197, 255), old.getpixel((0, 0)))
            if layer > 0:
                self.assertEqual(0, old.getpixel((8, 8))[3])

    def test_palette_pixels_follow_supplied_current_map_and_not_embedded_png_palette(self):
        original = compile_outputs(source(), {PALLET}, admitted=[PALLET_DOOR])
        src = source()
        image = Image.open(io.BytesIO(src.data[PNG]))
        image.putpalette([value for _ in range(256) for value in (255, 0, 255)])
        data = io.BytesIO()
        image.save(data, format='PNG')
        src.data[PNG] = data.getvalue()
        self.assertEqual(original, compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR]))
        lines = src.text(PALETTE).splitlines()
        for i in range(4, 19):
            lines[i] = '255 0 255'
        src.data[PALETTE] = ('\n'.join(lines) + '\n').encode()
        changed = compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR])
        self.assertEqual(original['maps/door-animations.json'], changed['maps/door-animations.json'])
        self.assertNotEqual(original['maps/door-animations.png'], changed['maps/door-animations.png'])

    def test_binding_comes_from_label_and_current_layout_behavior_not_map_names(self):
        src = source()
        label = 'include/constants/metatile_labels.h'
        src.data[label] = re.sub(rb'(#define\s+METATILE_PalletTown_Door\s+)0x2A3', rb'\g<1>0x2A4', src.data[label])
        path = 'data/tilesets/secondary/pallet_town/metatile_attributes.bin'
        attributes = bytearray(src.data[path])
        struct.pack_into('<I', attributes, (0x2a4 - 640) * 4, 0x69)
        src.data[path] = bytes(attributes)
        document, _ = unpack(compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR]))
        self.assertEqual(0x2a4, document['entries'][0]['metatileId'])
        struct.pack_into('<I', attributes, (0x2a4 - 640) * 4, 0)
        src.data[path] = bytes(attributes)
        document, _ = unpack(compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR]))
        self.assertEqual([], document['entries'])

    def test_missing_admitted_sheet_and_unsupported_source_contract_fail(self):
        src = source()
        expected = compile_outputs(src, {PALLET}, admitted=[PALLET_DOOR])
        with tempfile.TemporaryDirectory(prefix='door-source-') as directory:
            root = Path(directory)
            for name, data in src.data.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            self.assertEqual(expected, compile_outputs(WorldSource(root), {PALLET}, admitted=[PALLET_DOOR]))
            (root / PNG).unlink()
            with self.assertRaises(FileNotFoundError):
                compile_outputs(WorldSource(root), {PALLET}, admitted=[PALLET_DOOR])
        with self.assertRaisesRegex(ValueError, 'small-door'):
            compile_outputs(source(), {PALLET}, admitted=['METATILE_SeaCottage_Teleporter_Door'])
        with self.assertRaisesRegex(ValueError, 'no source table row'):
            compile_outputs(source(), {PALLET}, admitted=['METATILE_Missing'])
        with self.assertRaisesRegex(ValueError, 'Empty or duplicate'):
            compile_outputs(source(), {PALLET}, admitted=[])
        with self.assertRaisesRegex(ValueError, 'Empty or duplicate'):
            compile_outputs(source(), {PALLET}, admitted=[PALLET_DOOR, PALLET_DOOR])

    def test_nonlocal_admitted_sheet_is_required_and_malformed_inputs_fail(self):
        # An admitted sheet is required even when this layout never uses it.
        path = independent_small_doors()['METATILE_SeviiIslands67_Door']['path']
        src = source()
        compile_outputs(src, {PALLET})
        with tempfile.TemporaryDirectory(prefix='door-required-') as directory:
            root = Path(directory)
            for name, data in src.data.items():
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            (root / path).unlink()
            with self.assertRaises(FileNotFoundError):
                compile_outputs(WorldSource(root), {PALLET})
        for mutation in ('rgb', 'size', 'index'):
            with self.subTest(mutation=mutation):
                image = Image.open(RAW / path)
                if mutation == 'rgb':
                    image = image.convert('RGB')
                elif mutation == 'size':
                    image = image.crop((0, 0, 16, 32))
                else:
                    # Retain index 16 in the saved PNG rather than letting its
                    # original 16-color editing palette truncate it to 4 bits.
                    image.putpalette([0, 0, 0] * 256)
                    image.putpixel((0, 0), 16)
                data = io.BytesIO()
                image.save(data, format='PNG')
                if mutation == 'index':
                    self.assertEqual(16, Image.open(io.BytesIO(data.getvalue())).getpixel((0, 0)))
                malformed = source()
                malformed.data[path] = data.getvalue()
                with self.assertRaisesRegex(ValueError, 'indexed 4bpp|three original 16x16 frames'):
                    compile_outputs(malformed, {PALLET})

    def test_source_primary_backdrop_is_black_even_before_palette_fade(self):
        text = (RAW / 'src/fieldmap.c').read_text()
        loader = re.search(r'static void LoadTilesetPalette\(struct Tileset const \*tileset,.*?\n\}', text, re.S)[0]
        self.assertIn('u16 black = RGB_BLACK;', loader)
        self.assertIn('LoadPalette(&black, destOffset, PLTT_SIZEOF(1));', loader)
        self.assertIn('LoadPalette(tileset->palettes[0] + 1, destOffset + 1, size - PLTT_SIZEOF(1));', loader)

    def test_current_world_outputs_are_deterministic_and_installed(self):
        src = source()
        selected = load_selection(ROOT / 'data/world-maps.json')
        layouts = {row.get('layout') or src.json('data/maps/' + row['sourceName'] + '/map.json')['layout'] for row in selected}
        outputs = compile_outputs(src, layouts)
        self.assertEqual(outputs, compile_outputs(src, layouts))
        document, _ = unpack(outputs)
        self.assertGreater(len(document['entries']), 0)
        self.assertIn(PALLET, {row['layout'] for row in document['entries']})
        self.assertEqual(set(document), {'schemaVersion', 'texturePath', 'width', 'height', 'entries'})
        for name, data in outputs.items():
            self.assertEqual(data, (GENERATED / name).read_bytes(), name)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument('--generated', type=Path, default=GENERATED)
    arguments, remaining = parser.parse_known_args()
    GENERATED = arguments.generated
    unittest.main(argv=[sys.argv[0], *remaining])
