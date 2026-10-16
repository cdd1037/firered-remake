#!/usr/bin/env python3
"""Exercise supplied-input conversion and real shared-raw CLI publication."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from generated_output_compare import generated_output_matches
from item_catalog import definitions, definitions_from_source, item_json
from item_icon_exports import decode_icon_page

RAW = ROOT / 'content/raw/pokefirered/037335f4'
ITEMS = ('bill', 'cerulean', 'mtmoon', 'rocktunnel')


def load(relative):
    spec = importlib.util.spec_from_file_location('closure_' + Path(relative).stem.replace('-', '_'),
                                                  ROOT / 'scripts/imports' / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def source_inputs(recipe):
    return {entry['path']: (RAW / entry['path']).read_bytes() for entry in recipe['files']}


def no_ambient_content():
    """The supplied dictionary is the source, even when the code checkout is warm."""
    original = Path.open
    def guarded(path, *args, **kwargs):
        if path.absolute().is_relative_to(ROOT / 'content'):
            raise AssertionError('Conversion read ambient content: ' + str(path))
        return original(path, *args, **kwargs)
    return patch.object(Path, 'open', guarded)


class ImportInputClosureTests(unittest.TestCase):
    def assert_document_pixels(self, expected, actual, expected_directory, actual_directory):
        def normalize(left, right):
            if isinstance(left, list) and isinstance(right, list):
                self.assertEqual(len(left), len(right))
                return [normalize(a, b) for a, b in zip(left, right)]
            if not isinstance(left, dict) or not isinstance(right, dict):
                return left
            left = copy.deepcopy(left)
            for field in ('pngPath', 'texturePath'):
                if field not in left or field not in right:
                    continue
                def image_path(directory, value):
                    if not value.startswith('content/generated/'):
                        return directory / value
                    generated = next(parent for parent in directory.parents if parent.name == 'generated')
                    return generated / value.removeprefix('content/generated/')
                # Package folders are CONTENT/generated/data/items/NAME.
                a = decode_icon_page(image_path(expected_directory, left[field]).read_bytes(), left)
                b = decode_icon_page(image_path(actual_directory, right[field]).read_bytes(), right)
                self.assertEqual((a.size, a.tobytes()), (b.size, b.tobytes()))
                left[field] = right[field]
                left.pop('region', None)
            return {key: normalize(value, right.get(key)) for key, value in left.items()}
        self.assertEqual(normalize(expected, actual), actual)

    def test_item_dict_converters_are_closed_and_preserve_values(self):
        for name in ITEMS:
            with self.subTest(name=name):
                module = load('items/import-' + name + '-items.py')
                recipe = module.lock_read()
                data = source_inputs(recipe)
                definitions.cache_clear()
                with no_ambient_content():
                    outputs = module.convert(recipe, data)
                # Compare publication via the actual CLI below, including atlas pixels.
                self.assertIn(name + '-items.json', outputs)
                self.assertTrue(any(path.endswith('.png') for path in outputs))

    def test_item_validation_uses_supplied_declaration(self):
        module = load('items/import-cerulean-items.py')
        recipe = module.lock_read()
        data = source_inputs(recipe)
        source = json.loads(data['src/data/items.json'])
        next(row for row in source['items'] if row['itemId'] == 'ITEM_SUPER_POTION')['holdEffectParam'] = 49
        data['src/data/items.json'] = (json.dumps(source, indent=2) + '\n').encode()
        with no_ambient_content(), self.assertRaisesRegex(ValueError, 'Super Potion values differ'):
            module.convert(recipe, data)

    def test_machine_catalog_uses_only_explicit_source_and_items(self):
        module = load('items/import-machine-catalog.py')
        recipe = module.read_json(ROOT / module.RECIPE_PATH)
        data = {path: (RAW / path).read_bytes() for path in module.SOURCE_PATHS}
        retained = json.loads((ROOT / module.CATALOG_PATH).read_bytes())
        definitions.cache_clear()
        with no_ambient_content():
            rebuilt = module.compile_catalog(data, recipe)
            self.assertEqual(retained, json.loads(module.encode(rebuilt)))
            direct = module.Catalog(rebuilt)
            explicit = module.Catalog(retained, definitions_from_source(data))
            self.assertEqual(direct.machine('pret:ITEM_TM39', ['pret:SPECIES_BULBASAUR']),
                             explicit.machine('pret:ITEM_TM39', ['pret:SPECIES_BULBASAUR']))
            with self.assertRaisesRegex(ValueError, 'explicit item definitions'):
                module.Catalog(retained)
        with tempfile.TemporaryDirectory() as temporary:
            raw = Path(temporary)
            for path, content in data.items():
                target = raw / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(content)
            with no_ambient_content():
                self.assertEqual(retained, json.loads(module.encode(module.compile_catalog(raw, recipe))))

    def test_old_man_art_uses_supplied_items_without_generated_tutorial(self):
        module = load('presentation/import-old-man-art.py')
        recipe = module.load_lock()
        data = source_inputs(recipe)
        definitions.cache_clear()
        with no_ambient_content():
            outputs = module.convert(recipe, data)
        for path, content in outputs.items():
            self.assertTrue(generated_output_matches(ROOT / 'content/generated/sprites/pokemon/old-man' / path,
                                                     content), path)
        source = json.loads(data['src/data/items.json'])
        source['items'] += [row for row in source['items'] if row['itemId'] == 'ITEM_TEACHY_TV']
        data['src/data/items.json'] = json.dumps(source).encode()
        with no_ambient_content(), self.assertRaisesRegex(ValueError, 'one Teachy TV'):
            module.convert(recipe, data)

    def test_real_default_raw_item_clis_publish_isolated_outputs(self):
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            for name in ITEMS:
                with self.subTest(name=name):
                    relative = Path('content/generated/data/items') / name
                    output = project / relative
                    result = subprocess.run([sys.executable, str(ROOT / 'scripts/imports/items' /
                        ('import-' + name + '-items.py')), '--out', str(output)],
                        cwd=temporary, capture_output=True, text=True, timeout=60)
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    documents = list(output.glob('*.json'))
                    self.assertTrue(documents)
                    for document in documents:
                        if document.name == 'manifest.json':
                            continue
                        expected = json.loads((ROOT / relative / document.name).read_bytes())
                        actual = json.loads(document.read_bytes())
                        self.assert_document_pixels(expected, actual, ROOT / relative, output)
                    # The explicitly selected package and shared transient icon directory
                    # are the only destinations inside this isolated generated tree.
                    self.assertFalse((project / 'content/raw').exists())

    def test_default_shared_raw_cycle_cli_and_real_overlap_rejection(self):
        module = load('presentation/import-city-cycle-art.py')
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'cycle'
            result = subprocess.run([sys.executable, module.__file__, '--offline', '--output', str(output)],
                cwd=temporary, capture_output=True, text=True, timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            files = list(output.iterdir())
            self.assertTrue(files)
            for path in files:
                self.assertTrue(generated_output_matches(ROOT / 'content/generated/ui/city-cycle-art' / path.name,
                                                         path.read_bytes()), path.name)
            for name in ITEMS:
                importer = load('items/import-' + name + '-items.py')
                with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'Raw/output overlap'):
                    importer.run(output, output / 'nested')
            with self.assertRaisesRegex(ValueError, 'raw/output paths overlap'):
                module.import_content(output, output / 'nested')

    def test_real_cold_item_clis_publish_from_only_selected_raw(self):
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            raw = project / 'selected-raw'
            # Execute each real __main__ with the explicit raw tree. Any fallback
            # to this checkout's warm raw/generated content is a test failure.
            bootstrap = '''
import runpy, sys
from pathlib import Path
from unittest.mock import patch
script, ambient, raw, output = sys.argv[1:]
original = Path.open
def guarded(path, *args, **kwargs):
    if path.absolute().is_relative_to(Path(ambient)):
        raise AssertionError('CLI read ambient content: ' + str(path))
    return original(path, *args, **kwargs)
sys.argv = [script, '--raw', raw, '--out', output]
with patch.object(Path, 'open', guarded):
    runpy.run_path(script, run_name='__main__')
'''
            for name in ITEMS:
                module = load('items/import-' + name + '-items.py')
                for path, content in source_inputs(module.lock_read()).items():
                    target = raw / path
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(content)
                relative = Path('content/generated/data/items') / name
                output = project / relative
                with self.subTest(name=name):
                    result = subprocess.run([sys.executable, '-c', bootstrap, module.__file__,
                        str(ROOT / 'content'), str(raw), str(output)], cwd=temporary,
                        capture_output=True, text=True, timeout=60)
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    documents = [path for path in output.glob('*.json') if path.name != 'manifest.json']
                    self.assertTrue(documents)
                    for document in documents:
                        self.assert_document_pixels(json.loads((ROOT / relative / document.name).read_bytes()),
                            json.loads(document.read_bytes()), ROOT / relative, output)


if __name__ == '__main__':
    unittest.main()
