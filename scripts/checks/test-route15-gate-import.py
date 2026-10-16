#!/usr/bin/env python3
"""Route15 gate source semantics and final-world pixels without stage-package inventories."""
import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from PIL import Image, PngImagePlugin

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('ordinary_map_oracle', ROOT / 'scripts/checks/test-ordinary-interiors-source.py')
oracle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(oracle)
RAW, GENERATED = oracle.RAW, oracle.GENERATED
document = oracle.document
WorldSource, load_selection = oracle.WorldSource, oracle.load_selection
UPSTAIRS = 'Route15_WestEntrance_2F'
UPSTAIRS_ID = 'map.route-15-west-entrance-2f'
PACKAGE = GENERATED / 'maps/regions/fuchsia-route'


class Route15GateImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.selection = [row for row in load_selection(ROOT / 'data/world-maps.json')
                         if row['path'].startswith('maps/regions/fuchsia-route/')]

    def test_all_owned_maps_preserve_original_events_and_every_source_block(self):
        self.assertTrue(self.selection)
        for row in self.selection:
            with self.subTest(map=row['sourceName']):
                oracle.assert_raw_geometry(self, row)

    def test_every_source_rendered_layer_matches_final_packed_rgba(self):
        source, images = WorldSource(RAW), oracle.LogicalImages(GENERATED)
        count = 0
        with tempfile.TemporaryDirectory(prefix='route15-pixels-') as temporary:
            for row in self.selection:
                with self.subTest(map=row['sourceName']):
                    count += oracle.assert_raw_pixels(self, row, source, images, temporary)
        self.assertGreater(count, 0)
        print(f'Checked {len(self.selection)} Fuchsia-route maps and {count} final RGBA layers')

    def test_upstairs_uses_shared_layout_and_original_two_way_stair_endpoints(self):
        upstairs = document(PACKAGE / 'route-15-west-entrance-2f.json')
        downstairs = document(PACKAGE / 'route-15-west-entrance-1f.json')
        self.assertEqual('LAYOUT_ENTRANCE_2F', upstairs['events']['layout'])
        self.assertEqual((13, 11), (upstairs['width'], upstairs['height']))
        self.assertFalse(upstairs['events']['allow_cycling'])
        self.assertFalse(upstairs['events']['allow_running'])
        self.assertEqual([dict(x=10, y=9, elevation=3, dest_map='MAP_ROUTE15_WEST_ENTRANCE_1F',
                               dest_warp_id='4')], upstairs['events']['warp_events'])
        self.assertEqual(dict(x=9, y=10, elevation=3, dest_map='MAP_ROUTE15_WEST_ENTRANCE_2F',
                              dest_warp_id='0'), downstairs['events']['warp_events'][4])
        gate16 = document(RAW / 'data/maps/Route16_NorthEntrance_2F/map.json')
        self.assertEqual(gate16['layout'], upstairs['events']['layout'])

    def test_final_world_has_one_owner_for_route12_and_upstairs(self):
        index = document(GENERATED / 'maps/world.json')
        for identity in ('map.route-12', UPSTAIRS_ID):
            rows = [row for row in index['maps'] if row['stableId'] == identity and not row.get('variant')]
            self.assertEqual(1, len(rows))
            self.assertTrue((GENERATED / rows[0]['path']).is_file())
            if identity == 'map.route-12':
                self.assertFalse(rows[0]['path'].startswith('maps/regions/fuchsia-route/'))
            else:
                self.assertEqual('maps/regions/fuchsia-route/route-15-west-entrance-2f.json', rows[0]['path'])
        self.assertFalse((PACKAGE / 'route-12.json').exists())
        self.assertEqual(UPSTAIRS_ID, index['mapSymbols']['MAP_ROUTE15_WEST_ENTRANCE_2F'])

    def test_source_actor_and_binocular_bindings_are_not_replaced(self):
        source = document(RAW / f'data/maps/{UPSTAIRS}/map.json')
        actual = document(PACKAGE / 'route-15-west-entrance-2f.json')['events']
        self.assertEqual(source['object_events'], actual['object_events'])
        actor, = actual['object_events']
        self.assertEqual('Route15_WestEntrance_2F_EventScript_Aide', actor['script'])
        self.assertEqual('OBJ_EVENT_GFX_SCIENTIST', actor['graphics_id'])
        self.assertEqual(source['bg_events'], actual['bg_events'])
        self.assertEqual([(4, 1), (8, 1)], [(row['x'], row['y']) for row in actual['bg_events']])
        sprites = document(GENERATED / 'sprites/overworld/catalog.json')
        self.assertIn(actor['graphics_id'], sprites)
        sprite = sprites[actor['graphics_id']]
        with Image.open(GENERATED / 'sprites/overworld' / sprite['texturePath']) as image:
            region = sprite['region']
            self.assertGreater(sprite['frameCount'], 0)
            self.assertEqual(sprite['frameCount'] * sprite['frameWidth'], region['width'])
            self.assertLessEqual(region['x'] + region['width'], image.width)
            self.assertLessEqual(region['y'] + region['height'], image.height)
        programs = document(GENERATED / 'data/story.json')['Compilation']['Programs']
        for event in [actor, *actual['bg_events']]:
            self.assertEqual(f'data/maps/{UPSTAIRS}/scripts.inc', programs[event['script']]['SourcePath'])

    def test_raw_source_is_offline_and_rejects_missing_or_escaping_paths(self):
        with patch.object(subprocess, 'check_output', side_effect=AssertionError('Raw reads must not invoke Git')):
            source = WorldSource(RAW)
            self.assertEqual('MAP_ROUTE15_WEST_ENTRANCE_2F', source.json(f'data/maps/{UPSTAIRS}/map.json')['id'])
            for path in ('../outside', '/outside', 'data/maps/Missing/map.json'):
                with self.subTest(path=path), self.assertRaises((ValueError, OSError)):
                    source.read(path)
        with tempfile.TemporaryDirectory(prefix='route15-raw-') as temporary:
            raw = Path(temporary)
            (raw / 'input').symlink_to(RAW / f'data/maps/{UPSTAIRS}/map.json')
            with self.assertRaisesRegex(ValueError, 'Symlink'):
                WorldSource(raw).read('input')

    def test_compiled_aide_thresholds_preserve_each_source_file_scope(self):
        compilation = document(GENERATED / 'data/story.json')['Compilation']
        for name, threshold in ((UPSTAIRS, 50), ('Route16_NorthEntrance_2F', 40),
                                ('Route11_EastEntrance_2F', 30)):
            with self.subTest(map=name):
                path = f'data/maps/{name}/scripts.inc'
                source = (RAW / path).read_text()
                declaration = re.search(r'^\.equ REQUIRED_CAUGHT_MONS, (\d+)$', source, re.M)
                self.assertIsNotNone(declaration)
                self.assertEqual(threshold, int(declaration[1]))
                aide = compilation['Programs'][name + '_EventScript_Aide']
                self.assertEqual(path, aide['SourcePath'])
                comparisons = [row['Args'] for row in aide['Instructions']
                    if row['Opcode'] == 'goto_if_lt' and row['Args'][0] == 'VAR_0x8006']
                self.assertEqual([['VAR_0x8006', str(threshold), 'Aide_EventScript_HaventCaughtEnough']], comparisons)
                info = compilation['Programs'][name + '_EventScript_GetAideRequestInfo']
                buffers = [row['Args'] for row in info['Instructions']
                    if row['Opcode'] == 'buffernumberstring' and row['Args'][0] == 'STR_VAR_1']
                self.assertEqual([['STR_VAR_1', str(threshold)]], buffers)
        # The legacy shared Route11 definition stays intact. Map-local `.equ`
        # bindings are substituted by the compiler, never published globally.
        self.assertEqual(30, document(GENERATED / 'data/constants.json')['REQUIRED_CAUGHT_MONS'])

    def test_pixel_comparison_uses_rgba_not_png_encoding(self):
        with tempfile.TemporaryDirectory(prefix='route15-image-check-') as temporary:
            root = Path(temporary)
            expected = Image.new('RGBA', (2, 2), (27, 58, 93, 127))
            expected.save(root / 'first.png', compress_level=0)
            metadata = PngImagePlugin.PngInfo()
            metadata.add_text('Description', 'Different PNG encoding')
            expected.save(root / 'second.png', compress_level=9, pnginfo=metadata)
            self.assertNotEqual((root / 'first.png').read_bytes(), (root / 'second.png').read_bytes())
            images = oracle.LogicalImages(root)
            oracle.assert_rgba_equal(self, images.read('first.png'), images.read('second.png'))
            Image.new('RGBA', (2, 2), (27, 58, 93, 128)).save(root / 'second.png')
            with self.assertRaises(AssertionError):
                oracle.assert_rgba_equal(self, images.read('first.png'), images.read('second.png'))


if __name__ == '__main__':
    unittest.main(verbosity=2)
