#!/usr/bin/env python3
"""Shared map importer regressions plus bounded regeneration from the local raw cache.

This is not a full cold rebuild: it verifies current map JSON, atlas pixels,
script closure, and explicit catalog selection without changing generated assets.
"""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as ROOT
from map_runtime import publish_map_package
from map_scripts import parse_scripts, script_closure
from map_source import read_source_lock, source_topology
from runtime_map_manifest import compose, region_paths, verify_package

RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'


def module(relative):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(path.stem.replace('-', '_'), path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_source(text):
    path = 'data/maps/Test/scripts.inc'
    return SimpleNamespace(entries={path: {'roles': ['script-label-closure']}},
        text=lambda _: text, ref=lambda path, symbol, line=None: dict(path=path, symbol=symbol, line=line))


class MapConsolidationTests(unittest.TestCase):
    def test_aliases_macros_and_transitive_closure(self):
        source = script_source('.macro local arg\n call Hidden\n.endm\n.equ Alias, Branch\n'
            'Root::\n call Alias\n goto End\nBranch::\n message Text\nEnd::\n end\n'
            'Text::\n .string "label-like text$"\nHidden::\n end\n')
        parsed = parse_scripts(source)
        result = script_closure(parsed, {'Root'})
        self.assertEqual(['Branch', 'End', 'Root', 'Text'], [b['label'] for b in result['blocks']])
        self.assertEqual('local', result['localMacros'][0]['name'])
        self.assertFalse(result['unresolvedLabelReferences'])
        set_source = script_source('.set Alias, Root\nRoot::\n end\n')
        self.assertEqual({}, parse_scripts(set_source)[1])
        self.assertEqual({'Alias': 'Root'}, parse_scripts(set_source, alias_directives=('equ', 'set'))[1])
        with self.assertRaises(ValueError):
            parse_scripts(source, alias_directives=('include',))

    def test_unknown_targets_and_duplicate_labels_fail(self):
        for text in ('Root::\n call RenamedUnknown\n', 'Root::\n trainerbattle_no_intro T, Missing\n'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                script_closure(parse_scripts(script_source(text)), {'Root'})
        with self.assertRaises(ValueError):
            parse_scripts(script_source('Root::\n end\nRoot::\n end\n'))

    def test_lock_rejects_duplicates_and_escaping_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'source.json'
            from map_runtime import COMMIT
            lock = dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT,
                profile='test', gameVersion='FIRERED', revision=0, completeGame=False,
                maps=['Test'], files=[dict(path='data/maps/Test/map.json', roles=['map-source'])])
            path.write_text(json.dumps(lock))
            self.assertEqual(lock, read_source_lock(path, maps=[('Test',)], profile='test'))
            for paths in (['x', 'x'], ['../escape']):
                lock['files'] = [dict(path=p, roles=['map-source']) for p in paths]
                path.write_text(json.dumps(lock))
                with self.assertRaises(ValueError):
                    read_source_lock(path, maps=[('Test',)], profile='test')

    def test_topology_preserves_reciprocal_connection_semantics(self):
        a = dict(id='MAP_A', warp_events=[], connections=[dict(map='MAP_B', direction='right', offset=3)])
        b = dict(id='MAP_B', warp_events=[], connections=[dict(map='MAP_A', direction='left', offset=-3)])
        documents = {'data/maps/A/map.json': a, 'data/maps/B/map.json': b}
        source = SimpleNamespace(tree=documents, json=documents.__getitem__)
        result = source_topology(source, [dict(events=a), dict(events=b)])
        self.assertEqual(2, len(result['connections']))
        self.assertTrue(all(row['reciprocal'] for row in result['connections']))
        b['connections'][0]['offset'] = 2
        with self.assertRaises(ValueError):
            source_topology(source, [dict(events=a), dict(events=b)])

    def assert_published_map(self, directory, region, name, atlas):
        current = GENERATED / 'maps/regions' / region / (name + '.json')
        converted = directory / (name + '.json')
        self.assertEqual(json.loads(current.read_bytes()), json.loads(converted.read_bytes()))
        images = 0
        for path in directory.glob(name + '.*.png'):
            relative = f'maps/regions/{region}/{path.name}'
            expected = atlas.read(relative)
            with Image.open(path) as actual:
                self.assertEqual(expected.size, actual.size)
                self.assertEqual(expected.tobytes(), actual.convert('RGBA').tobytes(), relative)
            images += 1
        self.assertGreater(images, 0)
        return images

    def test_early_region_json_and_atlas_pixels(self):
        atlas = module('scripts/imports/maps/import-map-atlases.py').AtlasReader(GENERATED / 'maps/atlases')
        images = maps = 0
        with tempfile.TemporaryDirectory() as temporary:
            for region, filename in [('route22', 'route22-region'), ('forest', 'forest-region'),
                    ('pewter', 'pewter-region'), ('viridian', 'viridian-region'), ('viridian-center', 'viridian-center')]:
                importer = module('scripts/imports/maps/import-' + filename + '.py')
                source = importer.Source(RAW)
                for name, stable_id, directory in importer.MAPS:
                    # The first two Viridian inputs are explicitly reference-only copies.
                    if region == 'viridian' and name.startswith('PalletTown'):
                        continue
                    with self.subTest(map=name):
                        stage = Path(temporary) / region / directory
                        importer.import_map(source, name, stable_id, stage / directory)
                        publish_map_package(stage)
                        images += self.assert_published_map(stage, region, directory, atlas)
                        maps += 1
            importer = module('scripts/imports/maps/import-pallet.py')
            for name, stable_id, directory in importer.MAPS:
                with self.subTest(map=name):
                    stage = Path(temporary) / 'opening' / directory
                    importer.import_map(name, stable_id, stage / directory)
                    publish_map_package(stage)
                    images += self.assert_published_map(stage, 'opening', directory, atlas)
                    maps += 1
        self.assertEqual(24, maps)
        self.assertEqual(156, images)

    def test_recipe_driven_gym_json_and_atlas_pixels(self):
        importer = module('scripts/imports/maps/import-gym-region.py')
        atlas = module('scripts/imports/maps/import-map-atlases.py').AtlasReader(GENERATED / 'maps/atlases')
        with tempfile.TemporaryDirectory() as temporary:
            for gym in ('celadon', 'fuchsia'):
                out = Path(temporary) / 'content/generated/maps/regions' / (gym + '-gym')
                result = importer.run(ROOT / f'scripts/recipes/maps/{gym}-gym-region.lock.json', RAW, out)
                self.assertEqual(1, result['maps'])
                self.assertEqual(8, self.assert_published_map(out, gym + '-gym', gym + '-city-gym', atlas))
                for filename in ('npc-source.json', gym + '-gym-catalog.json'):
                    self.assertEqual(json.loads((GENERATED / 'maps/regions' / (gym + '-gym') / filename).read_bytes()),
                                     json.loads((out / filename).read_bytes()))

    def test_cleared_vram_interiors_and_shared_graphics_alias(self):
        reader = module('scripts/imports/maps/import-fuchsia-route-region.py')
        codec = module('tools/import-regional-service-maps/_regional_service_shared.py')
        cleared = module('scripts/lib/school-house-map-renderer.py')
        interiors = module('scripts/imports/maps/import-school-house-interiors.py')
        atlas = module('scripts/imports/maps/import-map-atlases.py').AtlasReader(GENERATED / 'maps/atlases')
        source = reader.Source(raw_root=RAW)
        with tempfile.TemporaryDirectory() as temporary:
            codec.TILESETS = interiors.TILESETS
            catalog = json.loads((GENERATED / interiors.PACKAGE / 'catalog.json').read_bytes())
            self.assertTrue(catalog['maps'])
            for row in catalog['maps']:
                name = row['sourceName']
                directory = Path(row['mapPath']).stem
                for leaf in ('scripts.inc', 'text.inc'):
                    source.read(f'data/maps/{name}/{leaf}')
                stage = Path(temporary) / directory
                cleared.import_map(source, name, row['stableId'], stage / directory, codec)
                publish_map_package(stage)
                self.assertEqual(8, self.assert_published_map(stage, 'school-house-interiors', directory, atlas))
            for region, name, directory, tilesets in [
                ('regional-service-maps', 'CeladonCity_Condominiums_RoofRoom', 'celadon-city-condominiums-roof-room',
                 {'gTileset_Building': 'primary/building', 'gTileset_School': 'secondary/school'}),
                ('silph', 'SilphCo_1F', 'silph-co-1f',
                 {'gTileset_Building': 'primary/building', 'gTileset_SilphCo': 'secondary/silph_co'})]:
                codec.TILESETS = tilesets
                for leaf in ('scripts.inc', 'text.inc'):
                    path = f'data/maps/{name}/{leaf}'
                    if path in source.tree:
                        source.read(path)
                stage = Path(temporary) / directory
                codec.import_map(source, name, 'map.' + directory, stage / directory)
                publish_map_package(stage)
                self.assertEqual(8, self.assert_published_map(stage, region, directory, atlas))

    def test_actual_regional_script_closures_are_nonempty(self):
        files = ['viridian-region', 'viridian-center', 'vermilion-region', 'vermilion-gym-region',
            'ssanne-region', 'route3-region', 'route22-region', 'route11-region', 'rocktunnel-region',
            'pewter-region', 'mtmoon-region', 'forest-region', 'cerulean-region', 'celadon-approach-region', 'bill-region']
        for name in files:
            with self.subTest(region=name):
                importer = module('scripts/imports/maps/import-' + name + '.py')
                source = importer.Source(RAW)
                names = [row[0] for row in importer.MAPS] if hasattr(importer, 'MAPS') else importer.SERVICE_MAPS
                worlds = [dict(events=source.json(f'data/maps/{n}/map.json')) for n in names]
                result = importer.script_inventory(source, worlds)
                self.assertTrue(result['roots'] and result['blocks'])
                self.assertFalse(result['unresolvedLabelReferences'])
                self.assertTrue(set(result['roots']) <= {b['label'] for b in result['blocks']})

    def test_package_check_accepts_verified_pruning_but_rejects_data_drift(self):
        atlas = module('scripts/imports/maps/import-map-atlases.py')
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            generated = root / 'generated'
            output = generated / 'maps/regions/test'
            stage = root / 'stage'
            stage.mkdir()
            output.mkdir(parents=True)
            image = Image.new('RGBA', (16, 16), (11, 22, 33, 0))
            image.save(stage / 'map.png', compress_level=0)
            image.save(output / 'map.png', compress_level=9)
            document = dict(schemaVersion=1, meaningful=[1, 2], enabled=False)
            (stage / 'data.json').write_text(json.dumps(document, indent=2))
            (output / 'data.json').write_text(json.dumps(document, sort_keys=True))
            manifest = dict(schemaVersion=1, outputCount=2,
                outputs=[dict(path='data.json'), dict(path='map.png')])
            (stage / 'manifest.json').write_text(json.dumps(manifest))
            (output / 'manifest.json').write_text(json.dumps(manifest))
            verify_package(stage, output, generated)
            atlas.compile_atlases(generated, generated / 'maps/atlases', {'maps/regions/test/map.png': (16, 16)})
            (output / 'map.png').unlink()
            pruned = dict(manifest, outputCount=1, outputs=[dict(path='data.json')])
            (output / 'manifest.json').write_text(json.dumps(pruned))
            verify_package(stage, output, generated)
            (output / 'data.json').write_text(json.dumps(dict(document, enabled=0)))
            with self.assertRaises(ValueError):
                verify_package(stage, output, generated)
            (output / 'data.json').write_text(json.dumps(document))
            (output / 'manifest.json').write_text(json.dumps(dict(pruned, unexpected=True)))
            with self.assertRaises(ValueError):
                verify_package(stage, output, generated)
            (output / 'manifest.json').write_text(json.dumps(pruned))
            Image.new('RGBA', (16, 16), (12, 22, 33, 0)).save(stage / 'map.png')
            with self.assertRaises(ValueError):
                verify_package(stage, output, generated)

    def test_explicit_regions_reproduce_every_current_catalog(self):
        catalogs = sorted((GENERATED / 'maps/catalogs').glob('*.json'))
        self.assertTrue(catalogs)
        with tempfile.TemporaryDirectory() as temporary:
            for catalog in catalogs:
                with self.subTest(catalog=catalog.name):
                    expected = json.loads(catalog.read_bytes())
                    regions = sorted({row['path'].split('/')[2] for row in expected['maps']})
                    output = Path(temporary) / catalog.name
                    compose(GENERATED, output, region_paths(GENERATED, regions))
                    expected['maps'].sort(key=lambda row: row['path'])
                    self.assertEqual(expected, json.loads(output.read_bytes()))
                    compose(GENERATED, catalog, region_paths(GENERATED, regions), check=True)
            with self.assertRaises(ValueError):
                region_paths(GENERATED, ['../escape'])
            with self.assertRaises(ValueError):
                region_paths(GENERATED, ['opening', 'opening'])


if __name__ == '__main__':
    unittest.main()
