#!/usr/bin/env python3
"""Independent cave source-cell/palette checks and importer rejection tests."""
import argparse
import base64
import io
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
from PIL import Image

SCRIPT_ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project-root', type=Path, default=SCRIPT_ROOT)
parser.add_argument('--raw', type=Path)
parser.add_argument('--generated', type=Path)
args, rest = parser.parse_known_args()
ROOT = args.project_root
RAW = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = args.generated or ROOT / 'content/generated'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
sys.path.insert(0, str(SCRIPT_ROOT / 'scripts/lib'))
from field_cave_art import compile_outputs, preview_types, cave_preview_flags
from world_source import WorldSource

PNG = 'graphics/cave_transition/tiles.png'
TILEMAP = 'graphics/cave_transition/tilemap.bin'
PALETTE = 'graphics/cave_transition/white.pal'
PREVIEWS = 'src/map_preview_screen.c'


def source():
    return WorldSource(RAW)


def document(src=None):
    return json.loads(compile_outputs(src or source())['maps/cave-transition.json'])


def unpack(doc):
    packed = base64.b64decode(doc['indices4Bpp'], validate=True)
    return bytes(value for pair in packed for value in (pair & 15, pair >> 4))


def independent_previews():
    text = (RAW / PREVIEWS).read_text()
    table = text.split('sMapPreviewScreenData[MPS_COUNT] = {', 1)[1].split('\n};', 1)[0]
    result = {}
    for row in table.split('    [')[1:]:
        fields = {}
        for line in row.splitlines():
            match = re.fullmatch(r'\s*\.(mapsec|type) = ([A-Z0-9_]+),', line)
            if match:
                fields[match[1]] = match[2]
        if len(fields) != 2 or fields['mapsec'] in result:
            raise AssertionError('Invalid source preview table')
        result[fields['mapsec']] = fields['type']
    if not result:
        raise AssertionError('Empty source preview table')
    return result


class FieldCaveArtTests(unittest.TestCase):
    def test_every_source_cell_flip_palette_and_pixel_matches(self):
        doc = document()
        self.assertEqual((256, 256), (doc['width'], doc['height']))
        actual = unpack(doc)
        self.assertEqual(65536, len(actual))
        image = Image.open(RAW / PNG)
        image.load()
        cells = struct.unpack('<1024H', (RAW / TILEMAP).read_bytes())
        pixels = bytearray()
        visible = backdrop = 0
        for y in range(256):
            for x in range(256):
                cell = cells[(y // 8) * 32 + x // 8]
                tile = cell % 1024
                local_x = x % 8 if not cell & 1024 else 7 - x % 8
                local_y = y % 8 if not cell & 2048 else 7 - y % 8
                index = image.getpixel((tile % (image.width // 8) * 8 + local_x,
                                        tile // (image.width // 8) * 8 + local_y))
                if index:
                    self.assertEqual(14, cell // 4096)
                    visible += 1
                else:
                    backdrop += 1
                pixels.append(index)
        self.assertGreater(visible, 0)
        self.assertGreater(backdrop, 0)
        self.assertEqual(bytes(pixels), actual)
        original = image.getpalette()
        expected = [((original[n] // 8) + (original[n + 1] // 8) * 32 + (original[n + 2] // 8) * 1024)
                    for n in range(0, 48, 3)]
        self.assertEqual(expected, doc['transitionPalette'])
        for field, file in [('whitePalette', 'white.pal'), ('blackPalette', 'black.pal')]:
            lines = (RAW / 'graphics/cave_transition' / file).read_text().splitlines()
            colors = [tuple(map(int, line.split())) for line in lines[3:]]
            self.assertEqual([r // 8 + g // 8 * 32 + b // 8 * 1024 for r, g, b in colors], doc[field])
        self.assertEqual(set(range(16)), set(actual))

    def test_installed_art_exactly_matches_conversion(self):
        self.assertEqual(compile_outputs(source())['maps/cave-transition.json'],
                         (GENERATED / 'maps/cave-transition.json').read_bytes())

    def test_preview_registry_is_complete_and_source_driven(self):
        expected = independent_previews()
        self.assertEqual(expected, preview_types(source()))
        self.assertEqual('MPS_TYPE_CAVE', expected['MAPSEC_MT_MOON'])
        self.assertEqual('MPS_TYPE_FOREST', expected['MAPSEC_VIRIDIAN_FOREST'])
        src = source()
        src.data[PREVIEWS] = src.data[PREVIEWS].replace(b'.mapsec = MAPSEC_MT_MOON,', b'.mapsec = MAPSEC_NEW_CAVE,')
        modified = preview_types(src)
        self.assertNotIn('MAPSEC_MT_MOON', modified)
        self.assertEqual('MPS_TYPE_CAVE', modified['MAPSEC_NEW_CAVE'])

    def test_every_installed_map_has_source_preview_type_without_changed_events(self):
        flags = cave_preview_flags(source())
        index = json.loads((GENERATED / 'maps/world.json').read_bytes())
        self.assertTrue(index['maps'])
        for row in index['maps']:
            current = json.loads((GENERATED / row['path']).read_bytes())
            raw = json.loads((RAW / 'data/maps' / current['sourceMapName'] / 'map.json').read_bytes())
            with self.subTest(map=current['sourceMapName'], variant=row.get('variant')):
                self.assertEqual(raw, current['events'])
                self.assertEqual(flags.get(raw['region_map_section']), current['cavePreviewVisitFlag'])

    def test_missing_required_art_input_fails(self):
        with tempfile.TemporaryDirectory(prefix='cave-art-missing-') as temporary:
            root = Path(temporary)
            (root / 'src').mkdir()
            (root / 'src/fldeff_flash.c').write_bytes((RAW / 'src/fldeff_flash.c').read_bytes())
            with self.assertRaises(FileNotFoundError):
                compile_outputs(WorldSource(root))

    def test_short_tilemap_fails(self):
        src = source()
        src.data[TILEMAP] = src.data[TILEMAP][:-2]
        with self.assertRaisesRegex(ValueError, '256x256'):
            compile_outputs(src)

    def test_out_of_range_tile_fails(self):
        src = source()
        src.data[TILEMAP] = struct.pack('<H', 0xE3FF) + src.data[TILEMAP][2:]
        with self.assertRaisesRegex(ValueError, 'exceeds source sheet'):
            compile_outputs(src)

    def test_visible_pixel_in_unloaded_palette_fails(self):
        src = source()
        src.data[TILEMAP] = struct.pack('<H', 0xD001) + src.data[TILEMAP][2:]
        with self.assertRaisesRegex(ValueError, 'unloaded palette'):
            compile_outputs(src)

    def test_index_above_4bpp_fails(self):
        src = source()
        image = Image.open(io.BytesIO(src.data[PNG]))
        image.load()
        image.putpixel((8, 0), 16)
        encoded = io.BytesIO()
        image.save(encoded, format='PNG', bits=8)
        src.data[PNG] = encoded.getvalue()
        self.assertEqual(16, Image.open(io.BytesIO(src.data[PNG])).getpixel((8, 0)))
        with self.assertRaisesRegex(ValueError, 'exceeds 4bpp'):
            compile_outputs(src)

    def test_invalid_palette_fails(self):
        for original, replacement in [(b'\n16\n', b'\n15\n'), (b'255 255 255', b'256 255 255')]:
            src = source()
            src.data[PALETTE] = src.data[PALETTE].replace(original, replacement, 1)
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                compile_outputs(src)

    def test_duplicate_unknown_or_missing_preview_rows_fail(self):
        for original, replacement in [(b'.mapsec = MAPSEC_MT_MOON,', b'.mapsec = MAPSEC_VIRIDIAN_FOREST,'),
                                      (b'.type = MPS_TYPE_CAVE,', b'.type = MPS_TYPE_ANY,'),
                                      (b'[MPS_MT_MOON] = {', b'[MPS_UNKNOWN] = {')]:
            src = source()
            src.data[PREVIEWS] = src.data[PREVIEWS].replace(original, replacement, 1)
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                preview_types(src)


if __name__ == '__main__':
    unittest.main(argv=[sys.argv[0], *rest])
