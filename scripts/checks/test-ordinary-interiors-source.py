#!/usr/bin/env python3
"""Ordinary interiors: independent raw geometry checks and fresh final-pixel comparison."""
import base64
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from world_selection import load_selection
from world_source import WorldSource
from map_source import tileset_bindings
from world_map_renderer import render_map

# This reader independently reconstructs packed images from the final atlas.
spec = importlib.util.spec_from_file_location('world_pixel_oracle', ROOT / 'scripts/checks/test-world-pipeline-oracle.py')
oracle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(oracle)
LogicalImages = oracle.LogicalImages


def document(path):
    return json.loads(path.read_bytes())


def assert_rgba_equal(case, expected, actual, message=''):
    case.assertEqual(expected.size, actual.size, message)
    case.assertEqual(expected.convert('RGBA').tobytes(), actual.convert('RGBA').tobytes(), message)


def assert_raw_geometry(case, row):
    """Decode source blocks/attributes directly, independently of the production decoder."""
    actual = document(GENERATED / row['path'])
    events = document(RAW / 'data/maps' / row['sourceName'] / 'map.json')
    layouts = document(RAW / 'data/layouts/layouts.json')['layouts']
    layout = next(item for item in layouts if item.get('id') == row.get('layout', events['layout']))
    case.assertEqual(events, actual['events'])
    case.assertEqual((row['stableId'], events['id'], row['sourceName']),
                     (actual['stableId'], actual['sourceMapSymbol'], actual['sourceMapName']))
    case.assertEqual((layout['width'], layout['height'], layout['border_width'], layout['border_height']),
                     (actual['width'], actual['height'], actual['borderWidth'], actual['borderHeight']))
    headers = (RAW / 'src/data/tilesets/headers.h').read_text()
    declarations = (RAW / 'src/data/tilesets/metatiles.h').read_text()
    attributes = []
    for key in ('primary_tileset', 'secondary_tileset'):
        body = re.search(r'const struct Tileset ' + layout[key] + r'\s*=\s*\{(.*?)\};', headers, re.S)[1]
        symbol = re.search(r'\.metatileAttributes\s*=\s*(\w+)', body)[1]
        path = re.search(r'\b' + symbol + r'\[\]\s*=\s*INCBIN_U32\("([^"]+)"\)', declarations)[1]
        attributes.append([value for value, in struct.iter_unpack('<I', (RAW / path).read_bytes())])
    metatiles = dict(actual['metatiles'])
    for source_key, final_key in (('blockdata_filepath', 'blocks'), ('border_filepath', 'borderBlocks')):
        raw = (RAW / layout[source_key]).read_bytes()
        case.assertEqual(raw, base64.b64decode(actual[final_key], validate=True))
        for block, in struct.iter_unpack('<H', raw):
            mid = block & 1023
            case.assertEqual(attributes[0][mid] if mid < 640 else attributes[1][mid - 640], metatiles[mid])
    case.assertEqual([f'data/maps/{row["sourceName"]}/{leaf}' for leaf in ('scripts.inc', 'text.inc')
                      if (RAW / 'data/maps' / row['sourceName'] / leaf).is_file()], actual['scriptPaths'])
    return actual


def assert_raw_pixels(case, row, source, images, temporary):
    output = Path(temporary) / row['path']
    fresh = render_map(source, row['sourceName'], row['stableId'], output,
                       tilesets=tileset_bindings(source), render_border=row.get('renderBorder', True),
                       layout_symbol=row.get('layout'), technical_spawn=row.get('technicalSpawn'))
    actual = document(GENERATED / row['path'])
    case.assertEqual(fresh['artifacts'], actual['artifacts'])
    for artifact in fresh['artifacts']:
        relative = (Path(row['path']).parent / artifact['path']).as_posix()
        with Image.open(output.parent / artifact['path']) as expected:
            assert_rgba_equal(case, expected, images.read(relative), relative)
    return len(fresh['artifacts'])


class OrdinaryInteriorsSourceTests(unittest.TestCase):
    def test_all_selected_interiors_match_raw_blocks_events_and_fresh_pixels(self):
        selection = [row for row in load_selection(ROOT / 'data/world-maps.json')
                     if row['path'].startswith('maps/regions/school-house-interiors/')]
        self.assertTrue(selection)
        source, images = WorldSource(RAW), LogicalImages(GENERATED)
        count = 0
        with tempfile.TemporaryDirectory(prefix='ordinary-interiors-oracle-') as temporary:
            for row in selection:
                with self.subTest(map=row['sourceName']):
                    assert_raw_geometry(self, row)
                    count += assert_raw_pixels(self, row, source, images, temporary)
        print(f'Checked {len(selection)} ordinary interiors and {count} fresh RGBA layers against final world assets')


if __name__ == '__main__':
    unittest.main(verbosity=2)
