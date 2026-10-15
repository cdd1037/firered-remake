#!/usr/bin/env python3
"""Regenerate nine compact machine emitters from retained local source bytes.

This is an offline source-adapter test, not upstream Git authentication. Machine
references, ordering and surrounding machine-document facts match the existing
resources. Source inventories may change when predecessor dependencies retire;
those inventories are validated separately rather than treated as game data.
The standalone prize icon is compared pixel-for-pixel with its packed atlas view.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'scripts'))
from machine_catalog import load_catalog

RAW = ROOT / 'content/raw/pokefirered/037335f4'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PACKAGES = (
    'breeding-species', 'gift-egg-species', 'ordinary-evolution-species',
    'original-machine-catalog', 'department-machine-stock',
    'department-rooftop-items', 'game-corner-prizes',
    'stone-evolution-species', 'field-fly',
)
RECIPE_INPUTS = {
    'scripts/import-config/machine-admissions.json',
    'scripts/import-config/machine-catalog.json',
}
READ_BYTES = Path.read_bytes
READ_TEXT = Path.read_text


def machine_path(path):
    return ('content/generated/' in path and
            (path.endswith('/machines.json') or path.endswith('/hm02.json')))


def guarded_bytes(path, *args, **kwargs):
    if machine_path(path.as_posix()):
        raise AssertionError('Importer read a predecessor machine payload: ' + str(path))
    return READ_BYTES(path, *args, **kwargs)


def guarded_text(path, *args, **kwargs):
    if machine_path(path.as_posix()):
        raise AssertionError('Importer read a predecessor machine payload: ' + str(path))
    return READ_TEXT(path, *args, **kwargs)


def raw_source(argv, **kwargs):
    """Supply only an explicitly pinned source file, never execute a process."""
    if argv[0] != 'git':
        raise AssertionError('Unexpected subprocess: ' + repr(argv))
    command = next((name for name in ('show', 'rev-parse') if name in argv), None)
    if command is None or ':' not in argv[-1]:
        raise AssertionError('Unexpected source request: ' + repr(argv))
    commit, relative = argv[-1].split(':', 1)
    if commit != COMMIT or Path(relative).is_absolute() or '..' in Path(relative).parts:
        raise AssertionError('Unpinned or unsafe source request: ' + repr(argv))
    data = READ_BYTES(RAW / relative)
    if command == 'show':
        return data
    # The legacy acquisition reader requests a blob identifier but does not use
    # it as runtime data. This derives the actual Git blob ID from retained bytes.
    return (hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest() + '\n').encode()


def load(package):
    spec = importlib.util.spec_from_file_location(
        'shop_machine_' + package.replace('-', '_'), ROOT / f'scripts/import-{package}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def generate(package):
    module = load(package)
    with patch('subprocess.check_output', raw_source), \
            patch.object(Path, 'read_bytes', guarded_bytes), \
            patch.object(Path, 'read_text', guarded_text):
        if package == 'department-machine-stock':
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                # Copy only the bounded compiler inputs, avoiding symlink
                # privileges on Windows and leaving project outputs untouched.
                raw_prefix = 'content/raw/pokefirered/037335f4/'
                inputs = ['scripts/department-machine-stock.contract.json', *sorted(RECIPE_INPUTS),
                          *(raw_prefix + path for path in load('machine-catalog').SOURCE_PATHS)]
                for relative in inputs:
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(READ_BYTES(ROOT / relative))
                with patch.object(sys, 'argv', [package, '--source', '/retained-source-adapter',
                                               '--root', str(root)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    module.main()
                relative = 'content/generated/department-machine-stock/machines.json'
                return {relative: READ_BYTES(root / relative)}
        result = module.generate(Path('/retained-source-adapter'), ROOT)
        if package in ('department-rooftop-items', 'game-corner-prizes', 'field-fly'):
            return result[0]
        raw = result.pop('_raw', {})
        return {**raw, **{module.BASE + path: json.dumps(document).encode()
                         for path, document in result.items()}}


def refs(value, pointer=''):
    """Preserve every array position while ignoring irrelevant object key order."""
    if isinstance(value, dict):
        if {'machine', 'move', 'sourceMoveId', 'movePp', 'compatibility'} <= value.keys():
            raise AssertionError('Emitter produced a full machine definition: ' + pointer)
        if {'species', 'sourceSpeciesId', 'canLearn', 'source'} <= value.keys():
            raise AssertionError('Emitter produced a full compatibility cell: ' + pointer)
        if set(value) == {'item', 'species'} and isinstance(value['item'], str):
            return {pointer: value}
        return {key: row for name, child in value.items()
                for key, row in refs(child, pointer + '/' + name).items()}
    if isinstance(value, list):
        return {key: row for index, child in enumerate(value)
                for key, row in refs(child, pointer + '/' + str(index)).items()}
    return {}


def pixels(data, region=None):
    with Image.open(io.BytesIO(data)) as image:
        image = image.convert('RGBA')
        if region:
            image = image.crop((region['x'], region['y'],
                                region['x'] + region['width'], region['y'] + region['height']))
        return image.size, image.tobytes()


class ShopMachineImportersTests(unittest.TestCase):
    def assert_inventory(self, package, files):
        if package == 'department-machine-stock':
            document = json.loads(files['content/generated/' + package + '/machines.json'])
            self.assertTrue(document['inputs'])
            for row in document['inputs']:
                self.assertEqual({'path', 'localPath'}, set(row))
                self.assertTrue((ROOT / row['localPath']).is_file(), row['localPath'])
            return
        relative = ('scripts/field-fly.contract.json' if package == 'field-fly' else
                    'content/generated/' + package + '/source-input-hashes.json')
        proof = json.loads(files[relative])
        self.assertEqual(COMMIT, proof['sourceCommit'])
        field = 'dependencies' if package == 'field-fly' else 'dependencyInputs'
        dependencies = proof[field]
        paths = [row['path'] for row in dependencies]
        self.assertEqual(len(paths), len(set(paths)))
        self.assertTrue(RECIPE_INPUTS <= set(paths))
        for row in dependencies:
            self.assertEqual({'path'}, set(row))
            self.assertFalse(machine_path(row['path']), row['path'])
            self.assertTrue((ROOT / row['path']).is_file(), row['path'])
        self.assertTrue(proof['inputs'])
        paths = [row['path'] for row in proof['inputs']]
        self.assertEqual(sorted(set(paths)), paths)
        for row in proof['inputs']:
            self.assertTrue(set(row) <= {'path', 'localPath', 'shared'}, row)
            self.assertTrue((ROOT / row['localPath'] if 'localPath' in row else RAW / row['path']).is_file(), row)
        if package == 'field-fly':
            expected = json.loads(READ_BYTES(ROOT / relative))
            # Source-tree/hash/inventory history is deliberately outside this
            # equality. These are the unchanged, meaningful field-service facts.
            for key in ('schemaVersion', 'sourceCommit', 'upstreamScriptsExecuted', 'romDownloaded',
                        'speciesCount', 'compatibleSpeciesCount', 'iconAlias', 'giftFlagId'):
                self.assertEqual(expected[key], proof[key], key)
            self.assertEqual([row['path'] for row in expected['outputs']],
                             [row['path'] for row in proof['outputs']])
            self.assertTrue(all(set(row) == {'path'} for row in proof['outputs']))

    def test_all_nine_emit_initially_compact_views_without_predecessor_reads(self):
        catalog = load_catalog(ROOT)
        definitions = compatibility_cells = checked = 0
        for package in PACKAGES:
            with self.subTest(package=package):
                filename = 'hm02.json' if package == 'field-fly' else 'machines.json'
                relative = 'content/generated/' + package + '/' + filename
                expected = json.loads(READ_BYTES(ROOT / relative))
                files = generate(package)
                actual = json.loads(files[relative])
                selected = refs(actual)
                self.assertTrue(selected, relative)
                self.assertEqual(refs(expected), selected, relative)
                for pointer, row in selected.items():
                    self.assertRegex(row['item'], r'^pret:ITEM_[TH]M\d\d$')
                    if isinstance(row['species'], list):
                        self.assertTrue(row['species'], pointer)
                        self.assertEqual(len(row['species']), len(set(row['species'])), pointer)
                        catalog.machine(row['item'], row['species'])
                        definitions += 1
                        compatibility_cells += len(row['species'])
                    else:
                        self.assertIsInstance(row['species'], str)
                        catalog.compatibility(row['item'], row['species'])
                        compatibility_cells += 1
                # Machine-document counts, aliases and all other ordinary fields
                # remain exact. Only the existing icon atlas post-pass changes
                # icon storage; validate its pixels and logical metadata below.
                self.assertEqual({k: v for k, v in expected.items() if k != 'icons'},
                                 {k: v for k, v in actual.items() if k != 'icons'})
                expected_icons, actual_icons = expected.get('icons', []), actual.get('icons', [])
                self.assertEqual(len(expected_icons), len(actual_icons))
                for previous, emitted in zip(expected_icons, actual_icons):
                    storage = {'texturePath', 'region'}
                    self.assertEqual({k: v for k, v in previous.items() if k not in storage},
                                     {k: v for k, v in emitted.items() if k not in storage})
                    self.assertEqual(pixels(READ_BYTES(ROOT / previous['texturePath']), previous.get('region')),
                                     pixels(files[emitted['texturePath']]))
                self.assert_inventory(package, files)
                checked += 1
        self.assertEqual(9, checked)
        self.assertEqual(16, definitions)
        self.assertEqual(4033, compatibility_cells)


if __name__ == '__main__':
    unittest.main()
