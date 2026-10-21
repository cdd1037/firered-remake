#!/usr/bin/env python3
"""Real item/UI conversions use pure codecs and preserve source pixel semantics."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from import_check_fixtures import snapshot_tree, write_files
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts/lib'))
from PIL import Image
import item_catalog
from item_catalog import item_json
from item_icon_exports import decode_icon_page, pack_project
from item_source import ItemSource
from project_paths import PROJECT_ROOT
from ui_tiles import palette

RAW = PROJECT_ROOT / 'content/raw/pokefirered/037335f4'
PACKAGES = {
    'early-shop': ('data/services/early-shop', 'early-shop.json', 'ITEM_POKE_BALL'),
    'cerulean-items': ('data/items/cerulean', 'cerulean-items.json', 'ITEM_SUPER_POTION'),
    'pewter-machines': ('data/machine-learning', 'pewter.json', 'ITEM_TM39'),
    'pokemon-tower-items': ('data/items/pokemon-tower', 'pokemon-tower-items.json', 'ITEM_ELIXIR'),
    'item-ui': ('ui/item-ui', 'item-ui.json', None),
}


def load(name):
    path = PROJECT_ROOT / 'scripts/imports/items' / ('import-' + name + '.py')
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sources(name):
    lock = json.loads((PROJECT_ROOT / 'scripts/recipes/items' / (name + '.lock.json')).read_bytes())
    return lock, {entry['path']: (RAW / entry['path']).read_bytes() for entry in lock['files']}


def convert(name, lock, data):
    module = load(name)
    if name == 'early-shop':
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            raw, output = root / 'raw', root / 'generated' / name
            write_files(raw, data)
            module.convert(raw, output)
            return {path.relative_to(output).as_posix() if path.is_relative_to(output)
                    else 'content/generated/' + path.relative_to(output.parent).as_posix(): path.read_bytes()
                    for path in output.parent.rglob('*') if path.is_file()}
    if name == 'pewter-machines':
        document, outputs = module.build(lock, data)
        return {**outputs, 'pewter.json': json.dumps(document, default=item_json).encode()}
    if name == 'pokemon-tower-items':
        return module.outputs(data)
    return module.convert(lock, data)


def sheet(size, value):
    image = Image.new('P', size, value)
    image.putpalette([channel for i in range(256) for channel in (i, i, i)])
    result = io.BytesIO()
    image.save(result, format='PNG')
    return result.getvalue()


def pixels(encoded):
    with Image.open(io.BytesIO(encoded)) as image:
        return image.size, image.convert('RGBA').tobytes()


class ItemUiLibraryImportTests(unittest.TestCase):
    def assert_retained_document(self, relative, document, outputs):
        path = PROJECT_ROOT / 'content/generated' / relative
        retained = json.loads(path.read_bytes())

        def normalize(expected, actual):
            if isinstance(expected, list) and isinstance(actual, list):
                self.assertEqual(len(expected), len(actual), relative)
                return [normalize(a, b) for a, b in zip(expected, actual)]
            if not isinstance(expected, dict) or not isinstance(actual, dict):
                return expected
            expected = copy.deepcopy(expected)
            if 'pngPath' in expected and 'pngPath' in actual:
                source = expected['pngPath']
                image_path = PROJECT_ROOT / source if source.startswith('content/generated/') else path.parent / source
                image = decode_icon_page(image_path.read_bytes(), expected)
                self.assertEqual((image.size, image.tobytes()), pixels(outputs[actual['pngPath']]), relative)
                expected.pop('region', None)
                expected['pngPath'] = actual['pngPath']
            return {key: normalize(value, actual.get(key)) for key, value in expected.items()}

        self.assertEqual(normalize(retained, document), document, relative)

    def test_real_outputs_match_retained_without_loading_lab_cli(self):
        original = importlib.util.spec_from_file_location

        def no_lab_cli(name, location, *args, **kwargs):
            if Path(location).name == 'import-lab-battle-ui.py':
                raise AssertionError('Pure item conversion loaded the lab UI command')
            return original(name, location, *args, **kwargs)

        with patch('importlib.util.spec_from_file_location', side_effect=no_lab_cli):
            for name, (folder, _, _) in PACKAGES.items():
                with self.subTest(package=name):
                    lock, data = sources(name)
                    outputs = convert(name, lock, data)
                    documents = {path: json.loads(content) for path, content in outputs.items()
                                 if path.endswith('.json') and path != 'manifest.json'}
                    self.assertTrue(documents)
                    self.assertTrue(any(path.endswith('.png') for path in outputs))
                    for path, document in documents.items():
                        self.assert_retained_document(folder + '/' + path, document, outputs)
                    if name == 'item-ui':
                        for path, content in outputs.items():
                            if path.endswith('.png'):
                                self.assertEqual(pixels((PROJECT_ROOT / 'content/generated' / folder / path).read_bytes()),
                                                 pixels(content), path)

    def test_icons_reject_duplicate_binding(self):
        for name, (_, _, symbol) in PACKAGES.items():
            if symbol is None:
                continue
            with self.subTest(package=name):
                lock, data = sources(name)
                path = 'src/data/item_icon_table.h'
                line = next(line for line in data[path].splitlines() if ('[' + symbol + ']').encode() in line)
                data[path] += b'\n' + line + b'\n'
                with self.assertRaisesRegex(ValueError, 'source match'):
                    convert(name, lock, data)

    def test_icons_reject_missing_graphics_binding(self):
        for name, (_, _, symbol) in PACKAGES.items():
            if symbol is None:
                continue
            with self.subTest(package=name):
                lock, data = sources(name)
                image, _ = ItemSource(data).icon_sources(symbol)
                path = 'src/data/graphics/items.h'
                declaration = image.replace('.png', '.4bpp.lz').encode()
                data[path] = b'\n'.join(line for line in data[path].splitlines() if declaration not in line) + b'\n'
                with self.assertRaisesRegex(ValueError, 'source match'):
                    convert(name, lock, data)

    def test_icons_reject_wrong_dimensions(self):
        for name, (_, _, symbol) in PACKAGES.items():
            if symbol is None:
                continue
            with self.subTest(package=name):
                lock, data = sources(name)
                image, _ = ItemSource(data).icon_sources(symbol)
                data[image] = sheet((16, 24), 1)
                with self.assertRaises((ValueError, AssertionError)):
                    convert(name, lock, data)

    def test_icons_keep_strict_ui_palette_shape(self):
        for name, (_, _, symbol) in PACKAGES.items():
            if symbol is None:
                continue
            with self.subTest(package=name):
                lock, data = sources(name)
                _, colors = ItemSource(data).icon_sources(symbol)
                data[colors] = b'JASC-PAL\n0100\n17\n' + b'32 64 96\n' * 17
                with self.assertRaisesRegex(ValueError, 'Unsupported palette'):
                    convert(name, lock, data)

    def test_icons_keep_four_bit_reduction_and_transparent_rgb(self):
        for name, (_, document_name, symbol) in PACKAGES.items():
            if symbol is None:
                continue
            with self.subTest(package=name):
                lock, data = sources(name)
                image, colors = ItemSource(data).icon_sources(symbol)
                data[colors] = b'JASC-PAL\n0100\n16\n' + b'32 64 96\n' * 16
                for index in (0, 16, 17):
                    data[image] = sheet((24, 24), index)
                    output = convert(name, lock, data)
                    document = json.loads(output[document_name])
                    rows = document.get('icons', [document.get('icon')])
                    row = next(row for row in rows if row['item']['value'] == 'pret:' + symbol)
                    expected = bytes((*palette(data[colors])[index & 15], 0 if not index & 15 else 255)) * (24 * 24)
                    self.assertEqual(((24, 24), expected), pixels(output[row['pngPath']]))

    def test_tower_cli_checks_standalone_and_packed_outputs_without_writes(self):
        script = PROJECT_ROOT / 'scripts/imports/items/import-pokemon-tower-items.py'
        with tempfile.TemporaryDirectory() as directory:
            generated = Path(directory) / 'generated'
            output = generated / 'data/items/pokemon-tower'
            command = [sys.executable, str(script), '--raw', str(RAW), '--out', str(output)]
            built = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(0, built.returncode, built.stderr)
            self.assertFalse((output / 'pokemon-tower-item-contracts.json').exists())

            for packed in (False, True):
                with self.subTest(packed=packed):
                    if packed:
                        pack_project(generated=generated)
                    before = snapshot_tree(generated)
                    checked = subprocess.run(command + ['--check'], capture_output=True, text=True)
                    self.assertEqual(0, checked.returncode, checked.stderr)
                    self.assertTrue(json.loads(checked.stdout)['checked'])
                    self.assertEqual(before, snapshot_tree(generated))

            original_bytes = {path: state[0] for path, state in snapshot_tree(generated).items()}
            for mutation in ('pickup', 'pixels', 'manifest'):
                with self.subTest(mutation=mutation):
                    for path, content in original_bytes.items():
                        (generated / path).write_bytes(content)
                    if mutation == 'pixels':
                        path = generated / 'sprites/items.png'
                        with Image.open(path) as source:
                            image = source.convert('RGBA')
                        icon = json.loads((output / 'pokemon-tower-items.json').read_bytes())['icons'][0]
                        x, y = icon['region']['x'], icon['region']['y']
                        red, green, blue, alpha = image.getpixel((x, y))
                        image.putpixel((x, y), (red ^ 1, green, blue, alpha))
                        image.save(path)
                    else:
                        path = output / ('manifest.json' if mutation == 'manifest' else 'pokemon-tower-pickups.json')
                        document = json.loads(path.read_bytes())
                        if mutation == 'manifest':
                            document['inputCount'] += 1
                        else:
                            document['pickups'][0]['quantity'] += 1
                        path.write_text(json.dumps(document))
                    before = snapshot_tree(generated)
                    checked = subprocess.run(command + ['--check'], capture_output=True, text=True)
                    self.assertNotEqual(0, checked.returncode)
                    self.assertIn('differ', checked.stderr)
                    self.assertEqual(before, snapshot_tree(generated))

    def test_tower_cli_rejects_source_output_overlap_before_writes(self):
        script = PROJECT_ROOT / 'scripts/imports/items/import-pokemon-tower-items.py'
        with tempfile.TemporaryDirectory() as directory:
            raw = Path(directory) / 'raw'
            raw.mkdir()
            result = subprocess.run([sys.executable, str(script), '--raw', str(raw), '--out', str(raw / 'generated')],
                                    capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Raw/output overlap', result.stderr)
            self.assertEqual([], list(raw.iterdir()))

    def test_tower_cold_inputs_and_pinned_git_need_no_ambient_item_catalog(self):
        lock, data = sources('pokemon-tower-items')
        original_data = data.copy()
        original_open = Path.open

        def isolated_open(path, *args, **kwargs):
            if path.absolute().is_relative_to(PROJECT_ROOT / 'content/raw'):
                raise AssertionError('Tower conversion read ambient project raw: ' + str(path))
            return original_open(path, *args, **kwargs)

        item_catalog.definitions.cache_clear()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            raw, output = root / 'raw', root / 'generated/data/items/pokemon-tower'
            recipe = root / 'tower.json'
            recipe.write_text(json.dumps(lock))
            write_files(raw, data)
            self.assertFalse((raw / 'include/constants/items.h').exists())

            with patch.object(Path, 'open', isolated_open), \
                 patch.object(item_catalog, 'definitions', side_effect=AssertionError('Ambient item definitions requested')), \
                 patch.object(item_catalog, 'item_definition', side_effect=AssertionError('Ambient item definition requested')):
                module = load('pokemon-tower-items')
                with patch.object(module, 'ROOT', root / 'unavailable-project'):
                    before = snapshot_tree(root)
                    expected = module.outputs(data)
                    self.assertEqual(original_data, data)
                    self.assertEqual(before, snapshot_tree(root))

                    report = module.run(raw, output, lock_path=recipe)
                    self.assertFalse(report['checked'])
                    for path, content in expected.items():
                        self.assertEqual(content, (output / path).read_bytes(), path)
                    expected_before = expected.copy()
                    before = snapshot_tree(root)
                    self.assertTrue(module.run(raw, output, lock_path=recipe, check=True)['checked'])
                    self.assertEqual(before, snapshot_tree(root))

                    reference, empty_raw = root / 'reference', root / 'unmaterialized-raw'
                    reference.mkdir()
                    calls = []

                    def pinned_git(command):
                        self.assertEqual(['git', '-C', str(reference), 'show'], command[:-1])
                        revision, path = command[-1].split(':', 1)
                        self.assertEqual(module.COMMIT, revision)
                        calls.append(path)
                        return data[path]

                    before = snapshot_tree(root)
                    with patch.object(module.subprocess, 'check_output', side_effect=pinned_git):
                        self.assertTrue(module.run(empty_raw, output, reference, recipe, check=True)['checked'])
                    self.assertEqual([entry['path'] for entry in lock['files']], calls)
                    self.assertFalse(empty_raw.exists())
                    self.assertEqual(before, snapshot_tree(root))
                    self.assertEqual(original_data, data)
                    self.assertEqual(expected_before, expected)

            for path, content in expected.items():
                if path.endswith('.json'):
                    self.assert_retained_document('data/items/pokemon-tower/' + path, json.loads(content), expected)

    def test_tower_rejects_missing_and_duplicate_selected_item_declarations(self):
        _, data = sources('pokemon-tower-items')
        source = json.loads(data['src/data/items.json'])
        selected = next(row for row in source['items'] if row['itemId'] == 'ITEM_ELIXIR')
        for rows in ([row for row in source['items'] if row['itemId'] != 'ITEM_ELIXIR'],
                     source['items'] + [selected]):
            with self.subTest(selected_count=sum(row['itemId'] == 'ITEM_ELIXIR' for row in rows)), \
                 patch.object(item_catalog, 'definitions', side_effect=AssertionError('Ambient item definitions requested')), \
                 patch.object(item_catalog, 'item_definition', side_effect=AssertionError('Ambient item definition requested')):
                module = load('pokemon-tower-items')
                changed = dict(data, **{'src/data/items.json': json.dumps(dict(source, items=rows), indent=2).encode()})
                before = changed.copy()
                with patch.object(Path, 'open', side_effect=AssertionError('Pure conversion opened a file')):
                    with self.assertRaises(ValueError):
                        module.outputs(changed)
                self.assertEqual(before, changed)


if __name__ == '__main__':
    unittest.main()
