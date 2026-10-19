#!/usr/bin/env python3
"""Regenerate current service machine emitters from retained local source bytes.

This is an offline source-adapter test, not upstream Git authentication. Machine
references, ordering and surrounding machine-document facts match the existing
resources. Source inventories may change when predecessor dependencies retire;
those inventories are validated separately rather than treated as game data.
The standalone prize icon is compared pixel-for-pixel with its packed atlas view.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

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
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from machine_catalog import load_catalog

RAW = ROOT / 'content/raw/pokefirered/037335f4'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PACKAGES = [('original-machine-catalog', 'items', 'data/machine-learning/acquisition-machines.json'), ('department-machine-stock', 'items', 'data/machine-learning/department-machine-stock.json'), ('department-rooftop-items', 'items', 'data/items/department-rooftop/machines.json'), ('game-corner-prizes', 'story', 'data/services/game-corner-prizes/machines.json'), ('field-fly', 'story', 'data/services/field-fly/hm02.json')]

RECIPE_INPUTS = {
    'scripts/recipes/config/machine-admissions.json',
    'scripts/recipes/config/machine-catalog.json',
}
READ_BYTES = Path.read_bytes
READ_TEXT = Path.read_text


def machine_path(path):
    return ('content/generated/' in path and
            (path.endswith('/machines.json') or path.endswith('/acquisition-machines.json') or path.endswith('/hm02.json')))


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


def load(package, family="items"):
    spec = importlib.util.spec_from_file_location(
        'shop_machine_' + package.replace('-', '_'), ROOT / f'scripts/imports/{family}/import-{package}.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def generate(package, family):
    module = load(package, family)
    with patch('subprocess.check_output', raw_source), \
            patch.object(Path, 'read_bytes', guarded_bytes), \
            patch.object(Path, 'read_text', guarded_text):
        if package == 'department-machine-stock':
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                # Copy only the bounded compiler inputs, avoiding symlink
                # privileges on Windows and leaving project outputs untouched.
                raw_prefix = 'content/raw/pokefirered/037335f4/'
                inputs = ['scripts/recipes/items/department-machine-stock.contract.json', *sorted(RECIPE_INPUTS),
                          *(raw_prefix + path for path in load('machine-catalog').SOURCE_PATHS)]
                for relative in inputs:
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(READ_BYTES(ROOT / relative))
                with patch.object(sys, 'argv', [package, '--source', '/retained-source-adapter',
                                               '--root', str(root)]), \
                        contextlib.redirect_stdout(io.StringIO()):
                    module.main()
                relative = 'content/generated/data/machine-learning/department-machine-stock.json'
                return {relative: READ_BYTES(root / relative)}
        reference = None if package == 'game-corner-prizes' else Path('/retained-source-adapter')
        result = module.generate(reference, ROOT)
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
    def assert_import_boundary(self, package, files):
        # Original-file inventories stay in tooling, never in runtime resources.
        self.assertFalse(any(path.endswith(('source-input-hashes.json', 'source-proof.json'))
                             for path in files))
        if package != 'field-fly':
            return
        relative = 'scripts/recipes/story/field-fly.contract.json'
        recipe = json.loads(files[relative])
        expected = json.loads(READ_BYTES(ROOT / relative))
        for key in ('schemaVersion', 'sourceCommit', 'speciesCount',
                    'compatibleSpeciesCount', 'iconAlias', 'giftFlagId'):
            self.assertEqual(expected[key], recipe[key], key)
        self.assertEqual([row['path'] for row in expected['outputs']],
                         [row['path'] for row in recipe['outputs']])
        dependencies = recipe['dependencies']
        paths = [row['path'] for row in dependencies]
        self.assertEqual(len(paths), len(set(paths)))
        self.assertTrue(RECIPE_INPUTS <= set(paths))
        for row in dependencies:
            self.assertEqual({'path'}, set(row))
            self.assertFalse(machine_path(row['path']), row['path'])
            self.assertTrue((ROOT / row['path']).is_file(), row['path'])

    def test_current_services_emit_compact_views_without_predecessor_reads(self):
        catalog = load_catalog(ROOT)
        definitions = compatibility_cells = checked = 0
        for package, family, resource in PACKAGES:
            with self.subTest(package=package):
                filename = 'hm02.json' if package == 'field-fly' else 'machines.json'
                relative = 'content/generated/' + resource
                expected = json.loads(READ_BYTES(ROOT / relative))
                files = generate(package, family)
                actual = json.loads(files[relative])
                if package == 'original-machine-catalog':
                    acquisitions = 'content/generated/data/machine-learning/acquisitions.json'
                    self.assertEqual(json.loads(READ_BYTES(ROOT / acquisitions)),
                                     json.loads(files[acquisitions]))
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
                self.assert_import_boundary(package, files)
                checked += 1
        self.assertEqual(len(PACKAGES), checked)
        self.assertEqual(16, definitions)
        self.assertEqual(3013, compatibility_cells)


if __name__ == '__main__':
    unittest.main()
