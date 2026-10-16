#!/usr/bin/env python3
"""Offline Water Path house source geometry, event and regeneration checks."""
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
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from map_runtime import normalize_runtime, runtime_cells

RAW = ROOT / 'content/raw/pokefirered/037335f4'
REGION = ROOT / 'content/generated/maps/regions/six-island'
MAPS = ('SixIsland_WaterPath_House1', 'SixIsland_WaterPath_House2')


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def slug(name):
    return re.sub(r'([a-z])([A-Z])', r'\1-\2', name).replace('_', '-').lower()


class WaterPathHouseImportTests(unittest.TestCase):
    def test_events_and_every_inside_border_block_match_source(self):
        layouts = json.loads((RAW / 'data/layouts/layouts.json').read_text())['layouts']
        for name in MAPS:
            with self.subTest(map=name):
                source = json.loads((RAW / f'data/maps/{name}/map.json').read_text())
                layout = next(row for row in layouts if row.get('id') == source['layout'])
                actual = json.loads((REGION / (slug(name) + '.json')).read_text())
                self.assertEqual(source, actual['events'])
                self.assertEqual((layout['width'], layout['height']), (actual['width'], actual['height']))
                for border in (False, True):
                    path = layout['border_filepath' if border else 'blockdata_filepath']
                    expected = [block for (block,) in struct.iter_unpack('<H', (RAW / path).read_bytes())]
                    self.assertEqual(expected, [cell['rawBlock'] for cell in runtime_cells(actual, border=border)])
                    self.assertTrue(expected)

    def test_both_doors_are_source_reciprocal_and_walkable(self):
        outside = json.loads((REGION / 'six-island-water-path.json').read_text())
        for index, name in enumerate(MAPS):
            with self.subTest(map=name):
                room = json.loads((REGION / (slug(name) + '.json')).read_text())
                entrance = outside['events']['warp_events'][index]
                exit = room['events']['warp_events'][int(entrance['dest_warp_id'])]
                self.assertEqual(room['events']['id'], entrance['dest_map'])
                self.assertEqual((outside['events']['id'], str(index)), (exit['dest_map'], exit['dest_warp_id']))
                for world, warp, expected in ((outside, entrance, (1, 0x69)), (room, exit, (0, 0x65))):
                    cell = list(runtime_cells(world))[warp['y'] * world['width'] + warp['x']]
                    self.assertEqual(expected, (cell['collision'], cell['behaviorId']))

    def test_fresh_source_render_matches_every_published_layer_pixel(self):
        from world_source import WorldSource
        from world_map_renderer import render_map
        from map_source import tileset_bindings
        source = WorldSource(RAW)
        atlas = module('scripts/imports/maps/import-map-atlases.py', 'water_path_atlas')
        packed = atlas.AtlasReader(ROOT / 'content/generated/maps/atlases')
        with tempfile.TemporaryDirectory(prefix='water-path-house-test-') as temp:
            for name in MAPS:
                with self.subTest(map=name):
                    folder = Path(temp)
                    fresh = render_map(source, name, 'map.' + slug(name), folder / (slug(name) + '.json'), tilesets=tileset_bindings(source))
                    current = json.loads((REGION / (slug(name) + '.json')).read_bytes())
                    for key in ('width', 'height', 'blocks', 'borderBlocks', 'events', 'metatiles', 'behaviors', 'scriptPaths'):
                        self.assertEqual(current[key], fresh[key], key)
                    self.assertEqual(8, len(fresh['artifacts']))
                    for artifact in fresh['artifacts']:
                        with Image.open(folder / artifact['path']) as image:
                            expected = image.convert('RGBA')
                            actual = packed.read('maps/regions/six-island/' + artifact['path'])
                            self.assertEqual(expected.size, actual.size)
                            self.assertEqual(expected.tobytes(), actual.tobytes(), artifact['path'])


if __name__ == '__main__':
    result = unittest.main(exit=False).result
    if result.testsRun == 0 or not result.wasSuccessful():
        raise SystemExit(1)
