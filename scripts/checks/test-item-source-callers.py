#!/usr/bin/env python3
"""Item producers resolve definitions only from their selected source closure."""
import contextlib
import importlib.util
import hashlib
import io
import json
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
import item_catalog
from pokemon_import_io import SourceReader
from generated_output_compare import check_item_outputs, generated_output_matches
from project_paths import PROJECT_ROOT as ROOT

RAW = ROOT / 'content/raw/pokefirered/037335f4'
RECIPES = {
    'early-shop': ('items/import-early-shop.py', 'items/early-shop.lock.json'),
    'quest': ('items/import-quest-items.py', 'items/quest-items.lock.json'),
    'rocket': ('items/import-rocket-hideout-items.py', 'items/rocket-hideout-items.lock.json'),
    'celadon': ('items/import-celadon-gym-catalog.py', 'items/celadon-gym-items.lock.json'),
    'town-map': ('presentation/import-town-map.py', 'config/town-map.json'),
    'safari': ('items/import-safari-items.py', 'items/safari-items.lock.json'),
}


def load(relative):
    path = ROOT / 'scripts/imports' / relative
    spec = importlib.util.spec_from_file_location('source_caller_' + path.stem.replace('-', '_'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def convert(name, module, lock, sources):
    if name in ('early-shop', 'quest'):
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()):
            root = Path(directory)
            raw, output = root / 'raw', root / 'generated' / name
            for relative, data in sources.items():
                target = raw / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
            if name == 'early-shop':
                module.convert(raw, output)
            else:
                module.import_catalog(raw, output)
            return {path.relative_to(output.parent).as_posix(): path.read_bytes()
                    for path in output.parent.rglob('*') if path.is_file()}
    if name == 'rocket':
        return module.outputs(lock, sources, ROOT)
    if name == 'celadon':
        return module.item_outputs(lock, sources, ROOT)
    if name == 'town-map':
        return module.convert(sources)
    if name == 'safari':
        with patch.object(module, 'authenticated', return_value=(lock, sources)):
            return module.generate(ROOT)
    raise AssertionError('Unknown source producer: ' + name)


class ItemSourceCallerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixtures = {}
        for name, (script, recipe) in RECIPES.items():
            lock = json.loads((ROOT / 'scripts/recipes' / recipe).read_bytes())
            sources = {row['path']: (RAW / row['path']).read_bytes() for row in lock['files']}
            cls.fixtures[name] = load(script), lock, sources

    def test_recipe_conversions_work_with_ambient_catalog_disabled(self):
        with patch.object(item_catalog, 'definitions', side_effect=AssertionError('Ambient item catalog read')):
            for name, (module, lock, sources) in self.fixtures.items():
                with self.subTest(importer=name):
                    outputs = convert(name, module, lock, sources)
                    documents = [json.loads(data) for path, data in outputs.items()
                                 if path.endswith('.json') and not path.endswith('manifest.json')]
                    self.assertTrue(documents)
                    self.assertIn('pret:ITEM_', json.dumps(documents))
                    if name != 'quest':
                        self.assertTrue(any(path.endswith('.png') for path in outputs))

    def test_selected_catalog_mutation_is_not_hidden_by_warm_project_cache(self):
        item_catalog.definitions(ROOT)
        for name, (module, lock, sources) in self.fixtures.items():
            with self.subTest(importer=name):
                changed = dict(sources)
                original = changed['src/data/items.json']
                changed['src/data/items.json'] = original.replace(
                    b'"itemId": "ITEM_MASTER_BALL"', b'"itemId": "ITEM_UNKNOWN_SOURCE"', 1)
                self.assertNotEqual(original, changed['src/data/items.json'])
                with self.assertRaisesRegex(ValueError, 'Invalid item identity: ITEM_UNKNOWN_SOURCE'):
                    convert(name, module, lock, changed)

    def test_description_only_callers_need_no_unselected_item_catalog(self):
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / 'raw'
            selected = ('src/data/items.json', 'src/move_descriptions.c',
                        'data/maps/CinnabarIsland_Mart/scripts.inc')
            for relative in selected:
                target = raw / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes((RAW / relative).read_bytes())
            source = SourceReader(raw, commit=item_catalog.COMMIT)
            dependency = Path(directory) / 'empty-project'
            with patch.object(item_catalog, 'definitions', side_effect=AssertionError('Ambient item catalog read')):
                for name in ('silph-catalogs', 'saffron-gym-catalogs', 'cinnabar-items'):
                    with self.subTest(importer=name):
                        module = load('items/import-' + name + '.py')
                        document = module.convert(source, dependency) if name == 'cinnabar-items' else module.metadata(source, dependency)
                        self.assertEqual(['pret:ITEM_' + name for name in module.ITEMS],
                                         [row['metadata']['item'] for row in document['items']])
                        self.assertTrue(all(row['metadata']['description'] for row in document['items']))
            self.assertFalse((raw / 'include/constants/items.h').exists())
            self.assertFalse(dependency.exists())

    def test_icon_only_callers_use_selected_pixels_without_business_or_predecessor_catalogs(self):
        from PIL import Image
        from item_icon_exports import decode_icon_page
        from item_source import ItemSource
        from pokemon_import_io import write_files
        cases = (
            ('seafoam-items', 'WATER_STONE', 'water_stone'),
            ('poke-doll-items', 'POKE_DOLL', 'poke_doll'),
            ('ordinary-item-supplement', 'PP_MAX', 'pp_max'),
            ('department-fourth-floor', 'RETRO_MAIL', 'retro_mail'),
        )
        for name, item, image in cases:
            with self.subTest(importer=name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                raw, output = root / 'raw', root / 'output'
                selected = ('src/data/item_icon_table.h', 'src/data/graphics/items.h',
                            'graphics/items/icons/' + image + '.png',
                            'graphics/items/icon_palettes/' + image + '.pal')
                for relative in selected:
                    destination = raw / relative
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    destination.write_bytes((RAW / relative).read_bytes())
                module = load('items/import-' + name + '.py')
                with patch.object(ItemSource, 'definition', side_effect=AssertionError('Business catalog read')):
                    files, document = module.generate(raw)
                self.assertEqual({'schemaVersion', 'icons'}, set(document))
                self.assertEqual(1, document['schemaVersion'])
                self.assertEqual(1, len(document['icons']))
                icon = document['icons'][0]
                self.assertEqual({'item', 'width', 'height', 'pngPath'}, set(icon))
                self.assertEqual('pret:ITEM_' + item, icon['item']['value'])
                self.assertEqual((24, 24), (icon['width'], icon['height']))
                self.assertEqual({module.DOCUMENT, icon['pngPath']}, set(files))
                retained = json.loads((ROOT / module.DOCUMENT).read_bytes())['icons'][0]
                field = 'pngPath' if 'pngPath' in retained else 'texturePath'
                actual = decode_icon_page((ROOT / retained[field]).read_bytes(), retained)
                expected = Image.open(io.BytesIO(files[icon['pngPath']])).convert('RGBA')
                self.assertEqual(actual.size, expected.size)
                self.assertEqual(actual.tobytes(), expected.tobytes(), 'All RGBA channels, including transparent colors')
                write_files(output, files)
                check_item_outputs(output / 'content/generated', {
                    path.removeprefix('content/generated/'): data for path, data in files.items()})
                self.assertFalse((raw / 'src/data/items.json').exists())
                self.assertFalse((output / 'content/generated/data/items.json').exists())
                self.assertFalse(list(output.rglob('source-contract.json')))
                with patch.object(sys, 'argv', [str(module.__file__), str(raw), str(output), '--check']), \
                        contextlib.redirect_stdout(io.StringIO()):
                    module.main()

    def test_vs_seeker_cli_preserves_runtime_output_and_success_report(self):
        script = ROOT / 'scripts/imports/trainers/import-vs-seeker-presentation.py'
        blobs = {}
        def source_blob(command, **kwargs):
            self.assertEqual('git', command[0])
            if 'ls-tree' in command:
                index = command.index('ls-tree')
                self.assertEqual(['-z', item_catalog.COMMIT, '--'], command[index + 1:index + 4])
                relative = command[-1]
                data = (RAW / relative).read_bytes()
                identity = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
                blobs[identity] = data
                return ('100644 blob ' + identity + '\t' + relative + '\0').encode()
            self.assertEqual(['cat-file', 'blob'], command[-3:-1])
            return blobs[command[-1]]
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            stream = io.StringIO()
            with patch('subprocess.check_output', side_effect=source_blob), patch.object(
                    sys, 'argv', [str(script), str(RAW), str(ROOT), str(output)]), patch.object(
                    item_catalog, 'definitions', side_effect=AssertionError('Ambient item catalog read')):
                with contextlib.redirect_stdout(stream):
                    runpy.run_path(str(script), run_name='__main__')
            report = json.loads(stream.getvalue())
            self.assertTrue(report['passed'])
            self.assertFalse(report['checked'])
            files = [path for path in output.rglob('*') if path.is_file()]
            self.assertEqual(report['files'], len(files))
            self.assertTrue(any(path.suffix == '.png' for path in files))
            self.assertTrue(any(path.suffix == '.json' for path in files))
            for path in files:
                relative = path.relative_to(output)
                self.assertTrue(generated_output_matches(ROOT / relative, path.read_bytes()), str(relative))

    def test_fuchsia_recipe_is_closed_without_item_constants(self):
        module = load('items/import-fuchsia-gym-items.py')
        lock = json.loads((ROOT / 'scripts/recipes/items/fuchsia-gym-items.lock.json').read_bytes())
        sources = {row['path']: (RAW / row['path']).read_bytes() for row in lock['files']}
        sources.pop('include/constants/items.h', None)
        with tempfile.TemporaryDirectory() as directory, patch.object(
                item_catalog, 'definitions', side_effect=AssertionError('Ambient item catalog read')):
            outputs = module.item_outputs(lock, sources, Path(directory))
        document = json.loads(outputs['fuchsia-gym-items.json'])
        self.assertEqual('pret:ITEM_TM06', document['items'][0]['metadata']['item'])
        self.assertTrue(outputs['item_tm06.png'].startswith(b'\x89PNG\r\n\x1a\n'))


if __name__ == '__main__':
    unittest.main()
