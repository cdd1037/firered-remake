#!/usr/bin/env python3
"""Offline Route15 upstairs geometry, package ownership and reproduction checks."""
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from PIL import Image, PngImagePlugin

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from map_runtime import runtime_cells

spec = importlib.util.spec_from_file_location('fuchsia_route_import',
    ROOT / 'scripts/imports/maps/import-fuchsia-route-region.py')
IMPORTER = importlib.util.module_from_spec(spec)
spec.loader.exec_module(IMPORTER)
RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
PACKAGE = GENERATED / IMPORTER.PACKAGE
UPSTAIRS = 'Route15_WestEntrance_2F'
UPSTAIRS_ID = 'map.route-15-west-entrance-2f'


def document(path):
    return json.loads(path.read_bytes())


class Route15GateImportTests(unittest.TestCase):
    def test_all_owned_maps_preserve_original_events_and_every_source_block(self):
        layouts = document(RAW / 'data/layouts/layouts.json')['layouts']
        catalog = document(PACKAGE / 'fuchsia-route-region-catalog.json')
        self.assertEqual(IMPORTER.CORE[1:], [row['sourceName'] for row in catalog['maps']])
        for row in catalog['maps']:
            with self.subTest(map=row['sourceName']):
                source = document(RAW / f"data/maps/{row['sourceName']}/map.json")
                layout = next(row for row in layouts if row.get('id') == source['layout'])
                actual = document(PACKAGE / row['mapPath'])
                self.assertEqual(source, actual['events'])
                self.assertEqual(row['stableId'], actual['stableId'])
                self.assertEqual(source['id'], actual['sourceMapSymbol'])
                self.assertEqual((layout['width'], layout['height']), (actual['width'], actual['height']))
                self.assertEqual([f"data/maps/{row['sourceName']}/{name}.inc" for name in ('scripts', 'text')],
                                 actual['scriptPaths'])
                for border in (False, True):
                    path = layout['border_filepath' if border else 'blockdata_filepath']
                    expected = [block for block, in struct.iter_unpack('<H', (RAW / path).read_bytes())]
                    cells = list(runtime_cells(actual, border=border))
                    self.assertTrue(cells)
                    self.assertEqual(expected, [cell['rawBlock'] for cell in cells])
                    for cell in cells:
                        primary = cell['metatileId'] < 640
                        symbol = layout['primary_tileset' if primary else 'secondary_tileset']
                        attributes = RAW / f'data/tilesets/{IMPORTER.TILESETS[symbol]}/metatile_attributes.bin'
                        index = cell['metatileId'] if primary else cell['metatileId'] - 640
                        expected_attribute, = struct.unpack_from('<I', attributes.read_bytes(), index * 4)
                        self.assertEqual(expected_attribute, cell['rawAttributes'])

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

    def test_upstairs_admission_keeps_route12_reused_and_package_schemas(self):
        catalog = document(PACKAGE / 'fuchsia-route-region-catalog.json')
        runtime = document(PACKAGE / 'runtime-catalog.json')
        self.assertEqual(catalog['maps'], runtime['maps'])
        self.assertEqual(1, len(catalog['reusedMaps']))
        reused, = catalog['reusedMaps']
        self.assertEqual('map.route-12', reused['stableId'])
        self.assertEqual('data/maps/Route12/map.json', reused['sourceRef']['path'])
        self.assertFalse(reused['regenerate'])
        self.assertFalse((PACKAGE / 'route-12.json').exists())
        self.assertFalse(any(row['stableId'] == 'map.route-12' for row in catalog['maps']))
        self.assertEqual(1, sum(row['stableId'] == UPSTAIRS_ID for row in catalog['maps']))
        availability = document(PACKAGE / 'availability.json')
        self.assertIn('MAP_ROUTE15_WEST_ENTRANCE_2F', availability['loadedMapSymbols'])
        self.assertNotIn('MAP_ROUTE15_WEST_ENTRANCE_2F', availability['deferredDestinations'])
        self.assertEqual([], availability['enabledServices'])
        self.assertFalse(availability['completeGame'])
        self.assertEqual(['Route12_FishingHouse'], IMPORTER.OPTIONAL)
        manifest = document(PACKAGE / 'manifest.json')
        paths = [row['path'] for row in manifest['outputs']]
        self.assertEqual(len(paths), len(set(paths)))
        self.assertIn('route-15-west-entrance-2f.json', paths)
        self.assertTrue(all('/' not in path and (PACKAGE / path).is_file() for path in paths))

    def test_source_actor_and_binocular_bindings_are_not_replaced(self):
        events = document(RAW / f'data/maps/{UPSTAIRS}/map.json')
        objects = [row for row in document(PACKAGE / 'objects.json')['objects'] if row['map'] == UPSTAIRS_ID]
        self.assertEqual(1, len(objects))
        actor, = objects
        self.assertEqual(1, actor['localId'])
        self.assertEqual(0, actor['sourceObjectIndex'])
        self.assertEqual(events['object_events'][0], actor['declaration'])
        self.assertEqual('Route15_WestEntrance_2F_EventScript_Aide', actor['declaration']['script'])
        runtime = document(PACKAGE / 'route-15-west-entrance-2f.json')
        self.assertEqual(events['bg_events'], runtime['events']['bg_events'])
        self.assertEqual([(4, 1), (8, 1)], [(row['x'], row['y']) for row in events['bg_events']])
        npcs = document(PACKAGE / 'npc-source.json')
        self.assertIn(dict(graphicsId='OBJ_EVENT_GFX_SCIENTIST'), npcs['graphics'])
        self.assertEqual(1, len(npcs['dynamicGraphics']))
        self.assertEqual('OBJ_EVENT_GFX_OMANYTE', npcs['dynamicGraphics'][0]['whenSet'])
        self.assertEqual('OBJ_EVENT_GFX_KABUTO', npcs['dynamicGraphics'][0]['whenUnset'])

    def test_raw_source_is_offline_and_rejects_missing_or_escaping_paths(self):
        with patch.object(IMPORTER, 'git', side_effect=AssertionError('Raw cache must not invoke Git')):
            source = IMPORTER.Source(raw_root=RAW)
            self.assertEqual('MAP_ROUTE15_WEST_ENTRANCE_2F', source.json(f'data/maps/{UPSTAIRS}/map.json')['id'])
            for path in ('../outside', '/outside', 'data\\maps\\Route15', 'data/maps/Missing/map.json'):
                with self.subTest(path=path), self.assertRaises(ValueError):
                    source.read(path)
        with tempfile.TemporaryDirectory(prefix='route15-raw-') as temporary:
            raw = Path(temporary)
            (raw / 'input').symlink_to(RAW / f'data/maps/{UPSTAIRS}/map.json')
            with self.assertRaisesRegex(ValueError, 'Symlink'):
                IMPORTER.Source(raw_root=raw)

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

    def test_reproduction_checks_pixels_for_standalone_and_packed_images(self):
        with tempfile.TemporaryDirectory(prefix='route15-image-check-') as temporary:
            root = Path(temporary)
            stage, output = root / 'stage', root / 'output'
            stage.mkdir(); output.mkdir()
            expected = Image.new('RGBA', (2, 2), (27, 58, 93, 127))
            expected.save(stage / 'room.png', compress_level=0)
            metadata = PngImagePlugin.PngInfo(); metadata.add_text('Description', 'Different PNG encoding')
            expected.save(output / 'room.png', compress_level=9, pnginfo=metadata)
            manifest = dict(schemaVersion=1, outputs=[dict(path='room.png')])
            (stage / 'manifest.json').write_bytes(IMPORTER.canonical(manifest))
            (output / 'manifest.json').write_bytes(IMPORTER.canonical(manifest))
            self.assertNotEqual((stage / 'room.png').read_bytes(), (output / 'room.png').read_bytes())
            IMPORTER.check_package(stage, output, root, manifest)
            Image.new('RGBA', (2, 2), (27, 58, 93, 128)).save(output / 'room.png')
            with self.assertRaisesRegex(ValueError, 'RGBA differs'):
                IMPORTER.check_package(stage, output, root, manifest)
            (output / 'room.png').unlink()
            (output / 'manifest.json').write_bytes(IMPORTER.canonical(dict(schemaVersion=1, outputs=[])))
            paths = []
            def read(path):
                paths.append(path)
                return expected
            with patch.object(IMPORTER, 'load_helper', return_value=SimpleNamespace(
                    AtlasReader=lambda _: SimpleNamespace(read=read))):
                IMPORTER.check_package(stage, output, root, manifest)
            self.assertEqual([IMPORTER.PACKAGE + '/room.png'], paths)
            (output / 'manifest.json').write_bytes(IMPORTER.canonical(manifest))
            with self.assertRaisesRegex(ValueError, 'Regeneration differs'):
                IMPORTER.check_package(stage, output, root, manifest)


if __name__ == '__main__':
    unittest.main()
