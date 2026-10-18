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
PNG = 'graphics/door_anims/pallet.png'
PALETTE = 'data/tilesets/secondary/pallet_town/palettes/08.pal'


def source():
    return WorldSource(RAW)


def unpack(outputs):
    return json.loads(outputs['maps/door-animations.json']), Image.open(io.BytesIO(outputs['maps/door-animations.png']))


class FieldDoorArtTests(unittest.TestCase):
    def test_source_table_supplies_identity_size_sound_pixels_and_palette_slots(self):
        row = source_doors(source())['METATILE_PalletTown_Door']
        self.assertEqual(dict(metatile=0x2a3, height=16, path=PNG,
                              palettes=[8] * 8, sound='SE_DOOR'), row)

    def test_every_rgba_matches_independent_original_pixels_with_current_map_palettes(self):
        src = source()
        document, actual = unpack(compile_outputs(src, {PALLET}))
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
        _, atlas = unpack(compile_outputs(source(), {PALLET}))
        for layer in range(3):
            old = Image.new('RGBA', (32, 32), (231, 12, 197, 255))
            replacement = atlas.crop((layer * 16, 32, layer * 16 + 16, 48))
            old.paste(replacement, (8, 8))  # No alpha mask: equivalent to Image.BlitRect.
            self.assertEqual(replacement.tobytes(), old.crop((8, 8, 24, 24)).tobytes())
            self.assertEqual((231, 12, 197, 255), old.getpixel((0, 0)))
            if layer > 0:
                self.assertEqual(0, old.getpixel((8, 8))[3])

    def test_palette_pixels_follow_supplied_current_map_and_not_embedded_png_palette(self):
        original = compile_outputs(source(), {PALLET})
        src = source()
        image = Image.open(io.BytesIO(src.data[PNG]))
        image.putpalette([value for _ in range(256) for value in (255, 0, 255)])
        data = io.BytesIO()
        image.save(data, format='PNG')
        src.data[PNG] = data.getvalue()
        self.assertEqual(original, compile_outputs(src, {PALLET}))
        lines = src.text(PALETTE).splitlines()
        for i in range(4, 19):
            lines[i] = '255 0 255'
        src.data[PALETTE] = ('\n'.join(lines) + '\n').encode()
        changed = compile_outputs(src, {PALLET})
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
        document, _ = unpack(compile_outputs(src, {PALLET}))
        self.assertEqual(0x2a4, document['entries'][0]['metatileId'])
        struct.pack_into('<I', attributes, (0x2a4 - 640) * 4, 0)
        src.data[path] = bytes(attributes)
        document, _ = unpack(compile_outputs(src, {PALLET}))
        self.assertEqual([], document['entries'])

    def test_missing_admitted_sheet_and_unsupported_source_contract_fail(self):
        src = source()
        expected = compile_outputs(src, {PALLET})
        with tempfile.TemporaryDirectory(prefix='door-source-') as directory:
            root = Path(directory)
            for name, data in src.data.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(data)
            self.assertEqual(expected, compile_outputs(WorldSource(root), {PALLET}))
            (root / PNG).unlink()
            with self.assertRaises(FileNotFoundError):
                compile_outputs(WorldSource(root), {PALLET})
        with self.assertRaisesRegex(ValueError, 'small-door'):
            compile_outputs(source(), {PALLET}, admitted=['METATILE_SeaCottage_Teleporter_Door'])
        with self.assertRaisesRegex(ValueError, 'no source table row'):
            compile_outputs(source(), {PALLET}, admitted=['METATILE_Missing'])
        with self.assertRaisesRegex(ValueError, 'Empty or duplicate'):
            compile_outputs(source(), {PALLET}, admitted=[])

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
