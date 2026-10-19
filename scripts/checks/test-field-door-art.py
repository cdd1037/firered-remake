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
SS_ANNE_DOOR = 'METATILE_SSAnne_Door'
SS_ANNE = 'LAYOUT_SSANNE_1F_CORRIDOR'
SS_ANNE_PNG = 'graphics/door_anims/ss_anne.png'
ELEVATOR_DOORS = {
    'METATILE_DepartmentStore_ElevatorDoor',
    'METATILE_SilphCo_HideoutElevatorDoor',
    'METATILE_SilphCo_ElevatorDoor',
    'METATILE_TrainerTower_LobbyElevatorDoor',
    'METATILE_TrainerTower_RoofElevatorDoor',
}


def source():
    return WorldSource(RAW)


def unpack(outputs):
    return json.loads(outputs['maps/door-animations.json']), Image.open(io.BytesIO(outputs['maps/door-animations.png']))


def current_layouts():
    selected = load_selection(ROOT / 'data/world-maps.json')
    return {row.get('layout') or json.loads((RAW / 'data/maps' / row['sourceName'] / 'map.json').read_bytes())['layout']
            for row in selected}


def independent_doors():
    """Read original declarations without any importer parser or palette helper."""
    text = (RAW / 'src/field_door.c').read_text()
    labels = (RAW / 'include/constants/metatile_labels.h').read_text()
    result = {}
    for line in text.splitlines():
        match = re.fullmatch(r'\s*\{(METATILE_\w+),\s*(DOOR_SOUND_\w+),\s*DOOR_SIZE_(1x[12]),\s*(\w+),\s*(\w+)\},', line)
        if not match:
            continue
        name, sound, size, tiles, palette = match.groups()
        metatile = int(re.search(r'^#define\s+' + name + r'\s+(0x[\da-fA-F]+)\b', labels, re.M)[1], 16)
        path = re.search(r'\b' + tiles + r'\[\] = INCBIN_U8\("([^"]+)"\)', text)[1]
        numbers = re.search(r'\b' + palette + r'\[\] = \{([^}]+)\}', text)[1]
        result[name] = dict(metatile=metatile, sound={'DOOR_SOUND_NORMAL': 'SE_DOOR', 'DOOR_SOUND_SLIDING': 'SE_SLIDING_DOOR'}[sound],
                            height={'1x1': 16, '1x2': 32}[size], path=path.replace('.4bpp', '.png'),
                            palettes=[int(x) for x in numbers.split(',')])
    if not result:
        raise AssertionError('No original door rows found')
    return result


def independent_small_doors():
    return {name: row for name, row in independent_doors().items() if row['height'] == 16}


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
        height = door['height']
        if original.mode != 'P' or original.size != (16, height * 3):
            raise AssertionError('Invalid original door sheet')
        if height == 32 and self.primary.crop((0, 0, 8, 8)).getextrema() != (0, 0):
            raise AssertionError('Large original door requires transparent blank tile')
        pixels = bytearray()
        for y in range(height * 3):
            for x in range(48):
                layer, local_x = divmod(x, 16)
                tile = (y % height // 8) * 2 + local_x // 8
                if layer == 1 and tile >= 4:
                    # The lower metatile's unused palette read is out of the
                    # source array. The verified all-zero tile stays invisible.
                    pixels.extend((0, 0, 0, 0))
                    continue
                index = (original.getpixel((local_x, y)) if layer == 0 else self.primary.getpixel((local_x % 8, y % 8))) & 15
                slot = 0 if layer == 2 else door['palettes'][tile + layer * 4]
                pixels.extend((*self.palettes[slot][index], 255 if index else 0))
        return bytes(pixels)


class FieldDoorArtTests(unittest.TestCase):
    def test_recipe_admits_real_small_normal_large_and_elevator_doors_only(self):
        rows = independent_small_doors()
        admitted = json.loads(RECIPE.read_bytes())['metatiles']
        self.assertEqual(len(admitted), len(set(admitted)))
        normal_large = {name for name, row in independent_doors().items()
                        if row['height'] == 32 and row['sound'] == 'SE_DOOR'}
        self.assertEqual({SS_ANNE_DOOR}, normal_large)
        self.assertEqual((set(rows) - {UNUSED_WARP}) | normal_large | ELEVATOR_DOORS, set(admitted))
        for name in ELEVATOR_DOORS:
            self.assertEqual((32, 'SE_SLIDING_DOOR'),
                             (independent_doors()[name]['height'], independent_doors()[name]['sound']))
        # The original table documents this reused harbor warp as having no door.
        self.assertIn('warp to enter the SS Anne from the harbor, which has no door.', (RAW / 'src/field_door.c').read_text())

    def test_all_styles_and_current_layouts_match_independent_source_rgba_and_sounds(self):
        rows = independent_small_doors()
        del rows[UNUSED_WARP]
        rows[SS_ANNE_DOOR] = independent_doors()[SS_ANNE_DOOR]
        rows.update({name: independent_doors()[name] for name in ELEVATOR_DOORS})
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
                    self.assertEqual((16, door['height']), (entry['width'], entry['height']))
                    self.assertEqual(0, entry['atlasY'] % 48)
                    self.assertGreaterEqual(entry['atlasY'], 0)
                    end = entry['atlasY'] + door['height'] * 3
                    self.assertLessEqual(end, atlas.height)
                    self.assertEqual(original.rgba(door), atlas.crop((0, entry['atlasY'], 48, end)).tobytes())
        self.assertEqual(expected, set(actual))
        self.assertEqual(set(rows), represented)
        self.assertEqual({'SE_DOOR', 'SE_SLIDING_DOOR'}, sounds)
        self.assertGreater(len(expected), len(rows))

    def test_elevator_door_bindings_use_actual_floor_warp_cells_and_source_palettes(self):
        examples = (
            ('CeladonCity_DepartmentStore_1F', 'METATILE_DepartmentStore_ElevatorDoor'),
            ('CeladonCity_DepartmentStore_5F', 'METATILE_DepartmentStore_ElevatorDoor'),
            ('RocketHideout_B1F', 'METATILE_SilphCo_HideoutElevatorDoor'),
            ('RocketHideout_B4F', 'METATILE_SilphCo_HideoutElevatorDoor'),
            ('SilphCo_1F', 'METATILE_SilphCo_ElevatorDoor'),
            ('SilphCo_11F', 'METATILE_SilphCo_ElevatorDoor'),
            ('TrainerTower_Lobby', 'METATILE_TrainerTower_LobbyElevatorDoor'),
            ('TrainerTower_Roof', 'METATILE_TrainerTower_RoofElevatorDoor'),
        )
        layouts = {row['id']: row for row in json.loads((RAW / 'data/layouts/layouts.json').read_bytes())['layouts'] if 'id' in row}
        doors = independent_doors()
        selected = current_layouts()
        document, atlas = unpack(compile_outputs(source(), selected))
        actual = {(entry['layout'], entry['metatileId']): entry for entry in document['entries']}
        for name, label in examples:
            with self.subTest(map=name, door=label):
                raw_map = json.loads((RAW / 'data/maps' / name / 'map.json').read_bytes())
                layout, door = layouts[raw_map['layout']], doors[label]
                self.assertIn(raw_map['layout'], selected)
                blocks = [x[0] & 1023 for x in struct.iter_unpack('<H', (RAW / layout['blockdata_filepath']).read_bytes())]
                anchors = [warp for warp in raw_map['warp_events'] if warp['dest_map'].endswith('_ELEVATOR')
                           and blocks[warp['y'] * layout['width'] + warp['x']] == door['metatile']]
                self.assertTrue(anchors, 'No actual elevator warp anchors the admitted door')
                self.assertTrue(all(warp['y'] > 0 for warp in anchors), 'A 32px door needs its upper metatile')
                entry = actual[raw_map['layout'], door['metatile']]
                self.assertEqual((16, 32, 'SE_SLIDING_DOOR'),
                                 (entry['width'], entry['height'], entry['soundEffect']))
                self.assertEqual(OriginalLayout(layout).rgba(door),
                                 atlas.crop((0, entry['atlasY'], 48, entry['atlasY'] + 96)).tobytes())

    def test_elevator_extension_preserves_existing_bindings_and_all_replacement_pixels(self):
        admitted = json.loads(RECIPE.read_bytes())['metatiles']
        previous = [label for label in admitted if label not in ELEVATOR_DOORS]
        selected = current_layouts()
        before, before_atlas = unpack(compile_outputs(source(), selected, admitted=previous))
        after, after_atlas = unpack(compile_outputs(source(), selected))
        actual = {(entry['layout'], entry['metatileId']): entry for entry in after['entries']}
        self.assertTrue(before['entries'])
        self.assertGreater(len(after['entries']), len(before['entries']))
        for old in before['entries']:
            with self.subTest(layout=old['layout'], metatile=old['metatileId']):
                current = actual[old['layout'], old['metatileId']]
                self.assertEqual({key: value for key, value in old.items() if key != 'atlasY'},
                                 {key: value for key, value in current.items() if key != 'atlasY'})
                self.assertEqual(before_atlas.crop((0, old['atlasY'], 48, old['atlasY'] + old['height'] * 3)).tobytes(),
                                 after_atlas.crop((0, current['atlasY'], 48, current['atlasY'] + current['height'] * 3)).tobytes())

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
        self.assertEqual(dict(metatile=0x281, height=32, path=SS_ANNE_PNG,
                              palettes=[7] * 8, sound='SE_DOOR'), source_doors(source())[SS_ANNE_DOOR])

    def test_large_and_small_source_callbacks_share_durations_and_use_correct_tile_offsets(self):
        text = (RAW / 'src/field_door.c').read_text()
        for direction in ('Open', 'Close'):
            tables = {}
            for size, stride in (('Small', 4), ('Large', 8)):
                name = 'sDoorAnimFrames_' + direction + size
                body = re.search(r'\b' + name + r'\[\] = \{(.*?)\n\};', text, re.S)[1]
                rows = re.findall(r'\{(\d+),\s*([^}]+)\}', body)
                offsets = ['CLOSED_DOOR_TILES_OFFSET', '0 * TILE_SIZE_4BPP',
                           str(stride) + ' * TILE_SIZE_4BPP', str(stride * 2) + ' * TILE_SIZE_4BPP']
                if direction == 'Close':
                    offsets.reverse()
                self.assertEqual(offsets, [offset.strip() for _, offset in rows])
                self.assertEqual([4] * 4, [int(duration) for duration, _ in rows])
                self.assertRegex(body, r'\{\}\s*$')
                tables[size] = [duration for duration, _ in rows]
            self.assertEqual(tables['Small'], tables['Large'])
        drawing = re.search(r'static void DrawCurrentDoorAnimFrame\([^;]*?\)\n\{.*?\n\}', text, re.S)[0]
        self.assertIn('DrawDoorMetatileAt(x, y - 1, tiles);', drawing)
        self.assertIn('BuildDoorTiles(tiles, DOOR_TILE_START + 4, &paletteNums[4]);', drawing)
        self.assertTrue(drawing.rstrip().endswith('DrawDoorMetatileAt(x, y, tiles);\n}'))

    def test_large_frames_cover_both_metatiles_and_replace_all_layers(self):
        door = independent_doors()[SS_ANNE_DOOR]
        layout = next(row for row in json.loads((RAW / 'data/layouts/layouts.json').read_bytes())['layouts']
                      if row.get('id') == SS_ANNE)
        original = OriginalLayout(layout)
        document, atlas = unpack(compile_outputs(source(), {SS_ANNE}, admitted=[SS_ANNE_DOOR]))
        self.assertEqual([dict(layout=SS_ANNE, metatileId=0x281, soundEffect='SE_DOOR',
                               width=16, height=32, atlasY=0)], document['entries'])
        self.assertEqual((48, 96), atlas.size)
        self.assertEqual(original.rgba(door), atlas.tobytes())
        for frame in range(3):
            for layer in range(3):
                replacement = atlas.crop((layer * 16, frame * 32, (layer + 1) * 16, (frame + 1) * 32))
                old = Image.new('RGBA', (48, 64), (231, 12, 197, 255))
                # The anchor is the bottom metatile, so a 32px frame begins one
                # 16px metatile above it, exactly as DrawCurrentDoorAnimFrame.
                old.paste(replacement, (16, 32 - 32 + 16))
                self.assertEqual(replacement.tobytes(), old.crop((16, 16, 32, 48)).tobytes())
                self.assertEqual((231, 12, 197, 255), old.getpixel((16, 48)))
                if layer > 0:
                    self.assertEqual({0}, set(replacement.getchannel('A').tobytes()))
        self.assertEqual(bytes(16 * 16 * 4), atlas.crop((16, 16, 32, 32)).tobytes())

    def test_mixed_heights_pack_without_overlap_and_keep_small_pixels(self):
        document, atlas = unpack(compile_outputs(source(), {PALLET, SS_ANNE}, admitted=[PALLET_DOOR, SS_ANNE_DOOR]))
        self.assertEqual(2, len(document['entries']))
        ordered = sorted(document['entries'], key=lambda row: row['atlasY'])
        self.assertEqual((48, 144), atlas.size)
        self.assertEqual(0, ordered[0]['atlasY'])
        self.assertEqual(ordered[0]['height'] * 3, ordered[1]['atlasY'])
        for entry in ordered:
            label = PALLET_DOOR if entry['height'] == 16 else SS_ANNE_DOOR
            _, isolated = unpack(compile_outputs(source(), {entry['layout']}, admitted=[label]))
            self.assertEqual(isolated.tobytes(), atlas.crop((0, entry['atlasY'], 48,
                                                             entry['atlasY'] + entry['height'] * 3)).tobytes())

    def test_large_door_rejects_nontransparent_primary_tile_and_changed_source_placement(self):
        src = source()
        path = 'data/tilesets/primary/general/tiles.png'
        image = Image.open(io.BytesIO(src.data[path]))
        image.putpixel((0, 0), 1)
        data = io.BytesIO()
        image.save(data, format='PNG')
        src.data[path] = data.getvalue()
        with self.assertRaisesRegex(ValueError, 'transparent primary tile 0'):
            compile_outputs(src, {SS_ANNE}, admitted=[SS_ANNE_DOOR])
        src = source()
        src.data['src/field_door.c'] = src.data['src/field_door.c'].replace(
            b'DrawDoorMetatileAt(x, y - 1, tiles);', b'DrawDoorMetatileAt(x, y - 2, tiles);')
        with self.assertRaisesRegex(ValueError, 'source door tile placement'):
            compile_outputs(src, {SS_ANNE}, admitted=[SS_ANNE_DOOR])

    def test_large_admitted_sheet_requires_three_full_indexed_frames_even_outside_its_layout(self):
        doors = independent_doors()
        for label in sorted(ELEVATOR_DOORS | {SS_ANNE_DOOR}):
            for mutation in ('rgb', 'size'):
                with self.subTest(door=label, mutation=mutation):
                    image = Image.open(RAW / doors[label]['path'])
                    if mutation == 'rgb':
                        image = image.convert('RGB')
                    elif mutation == 'size':
                        image = image.crop((0, 0, 16, 48))
                    data = io.BytesIO()
                    image.save(data, format='PNG')
                    src = source()
                    src.data[doors[label]['path']] = data.getvalue()
                    with self.assertRaisesRegex(ValueError, 'indexed 4bpp|three original 16x32 frames'):
                        compile_outputs(src, {PALLET}, admitted=[label])

    def test_indexed_png_editing_palette_banks_follow_original_4bpp_conversion(self):
        conversion = (RAW / 'tools/gbagfx/convert_png.c').read_text()
        self.assertIn('unsigned char pixel = ((srcByte >> j) % (1 << srcBitDepth)) % (1 << destBitDepth);', conversion)
        self.assertIn('image->pixels = ConvertBitDepth(image->pixels, bit_depth, image->bitDepth, image->width * image->height);', conversion)
        command = (RAW / 'tools/gbagfx/main.c').read_text()
        self.assertIn("options.bitDepth = outputFileExtension[0] - '0';", command)
        # These source sheets actually use several editing-palette banks. Their
        # high bits never select the runtime palette; field_door.c does that.
        doors = independent_doors()
        banked = [label for label in ELEVATOR_DOORS
                  if Image.open(RAW / doors[label]['path']).getextrema()[1] > 15]
        self.assertTrue(banked)
        admitted = json.loads(RECIPE.read_bytes())['metatiles']
        src = source()
        selected = current_layouts()
        expected = compile_outputs(src, selected)
        for label in admitted:
            path = doors[label]['path']
            image = Image.open(RAW / path)
            image.putpalette([0, 0, 0] * 256)
            image = image.point(lambda index: (index & 15) | 0xf0)
            data = io.BytesIO()
            image.save(data, format='PNG')
            self.assertGreater(Image.open(io.BytesIO(data.getvalue())).getextrema()[0], 15)
            src.data[path] = data.getvalue()
        self.assertEqual(expected, compile_outputs(src, selected))

    def test_every_admitted_elevator_sheet_is_required_even_outside_its_layout(self):
        src = source()
        compile_outputs(src, {PALLET})
        doors = independent_doors()
        with tempfile.TemporaryDirectory(prefix='elevator-door-required-') as directory:
            root = Path(directory)
            for name, data in src.data.items():
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            for label in sorted(ELEVATOR_DOORS):
                with self.subTest(door=label):
                    path = doors[label]['path']
                    (root / path).unlink()
                    with self.assertRaises(FileNotFoundError):
                        compile_outputs(WorldSource(root), {PALLET})
                    (root / path).write_bytes(src.data[path])

    def test_incomplete_or_unloaded_source_palette_slots_fail(self):
        for numbers in ('12, 12, 2, 2, 2, 2, 2', '13, 12, 2, 2, 2, 2, 2, 2'):
            with self.subTest(palettes=numbers):
                src = source()
                path = 'src/field_door.c'
                changed, replacements = re.subn(
                    rb'(sDoorAnimPalettes_HideoutElevator\[\] = \{)[^}]+',
                    lambda match: match[1] + numbers.encode(), src.data[path])
                self.assertEqual(1, replacements)
                src.data[path] = changed
                with self.assertRaisesRegex(ValueError, 'unloaded map palette'):
                    compile_outputs(src, {PALLET})

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
        for mutation in ('rgb', 'size'):
            with self.subTest(mutation=mutation):
                image = Image.open(RAW / path)
                if mutation == 'rgb':
                    image = image.convert('RGB')
                elif mutation == 'size':
                    image = image.crop((0, 0, 16, 32))
                data = io.BytesIO()
                image.save(data, format='PNG')
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
