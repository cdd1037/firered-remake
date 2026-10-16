#!/usr/bin/env python3
"""Read-only regeneration of direct TM/HM projections using retained raw inputs.

The fixture adapter supplies pinned local bytes to existing Git-reading source
parsers. It does not claim Git authentication, execute upstream programs, or
write generated resources. Machine membership is compared exactly with runtime.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import ast
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = _PROJECT_ROOT
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def load(script):
    spec = importlib.util.spec_from_file_location(script.replace('-', '_'), ROOT / 'scripts' / script)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def read_source(command, **kwargs):
    if command[:2] == ['git', '-C']:
        if len(command) != 5 or command[:4] != ['git', '-C', str(ROOT), 'show']:
            raise AssertionError('Unexpected source command: ' + repr(command))
    elif command[:2] != ['git', 'show'] or len(command) != 3 or kwargs.get('cwd') != ROOT:
        raise AssertionError('Unexpected source command: ' + repr(command))
    commit, relative = command[-1].split(':', 1)
    if commit != COMMIT or Path(relative).is_absolute() or '..' in Path(relative).parts:
        raise AssertionError('Unexpected source identity/path')
    return (RAW / relative).read_bytes()


class RawSource:
    def __init__(self):
        self.data = {}
    def read(self, path, *args):
        self.data[path] = (RAW / path).read_bytes()
        return self.data[path]
    def text(self, path):
        return self.read(path).decode()
    def json(self, path):
        return json.loads(self.read(path))


def references(value):
    if isinstance(value, list):
        return [ref for child in value for ref in references(child)]
    if not isinstance(value, dict):
        return []
    if set(value) == {'item', 'species'} and isinstance(value['item'], str):
        return [value]
    if 'canLearn' in value and 'species' in value:
        raise AssertionError('Importer emitted a full compatibility cell')
    if {'machine', 'move', 'sourceMoveId', 'movePp', 'compatibility'} <= value.keys():
        raise AssertionError('Importer emitted a full machine definition')
    return [ref for child in value.values() for ref in references(child)]


def item_wrappers(value):
    if isinstance(value, list):
        return [row for child in value for row in item_wrappers(child)]
    if not isinstance(value, dict):
        return []
    if 'description' in value and isinstance(value.get('item'), dict) and 'sourceItemSymbol' in value['item']:
        if set(value) != {'item', 'description'}:
            raise AssertionError('Retired item proof fields emitted')
        return [value]
    return [row for child in value.values() for row in item_wrappers(child)]


class LateMachineImporterTests(unittest.TestCase):
    def compare(self, relative, value):
        expected = references(json.loads((ROOT / relative).read_bytes()))
        actual = references(value)
        self.assertTrue(expected, 'Empty machine-view assertion: ' + relative)
        self.assertEqual(expected, actual, relative)
        self.assertEqual(item_wrappers(json.loads((ROOT / relative).read_bytes())), item_wrappers(value), relative)

    def test_midgame_machine_outputs_use_direct_shared_views(self):
        module = load('imports/items/import-fuchsia-gym-items.py')
        lock = json.loads((ROOT / 'scripts/recipes/items/fuchsia-gym-items.lock.json').read_bytes())
        files = {row['path']: (RAW / row['path']).read_bytes() for row in lock['files']}
        outputs = module.item_outputs(lock, files, ROOT)
        self.compare('content/generated/data/items/fuchsia-gym/fuchsia-gym-machines.json',
                     json.loads(outputs['fuchsia-gym-machines.json']))
        module = load('imports/story/import-vermilion-gym-assets.py')
        lock = module.read_lock()
        sources, local_inputs = module.authenticated_inputs(lock, RAW, root=ROOT)
        self.assertNotIn('content/generated/data/machine-learning/ssanne.json', local_inputs)
        outputs = module.build(lock, sources, local_inputs)
        self.compare('content/generated/ui/vermilion-gym-assets/assets.json', json.loads(outputs['assets.json']))
        outputs = load('imports/items/import-safari-items.py').generate(ROOT)
        self.compare('content/generated/data/items/safari/machines.json',
                     json.loads(outputs['content/generated/data/items/safari/machines.json']))
        for family in ('silph', 'saffron-gym'):
            with self.subTest(family=family):
                _, _, machines = load(f'imports/items/import-{family}-catalogs.py').metadata(RawSource(), ROOT)
                self.compare(f'content/generated/data/regions/{family}/machines.json', machines)
        _, machines = load('imports/items/import-cinnabar-items.py').convert(RawSource(), ROOT)
        self.compare('content/generated/data/items/cinnabar/machines.json', machines)
        with patch('subprocess.check_output', read_source):
            outputs = load('imports/story/import-viridian-gym-rules.py').generate(ROOT, ROOT)
            self.compare('content/generated/data/regions/viridian-gym/machines.json', outputs['machines.json'])
            outputs = load('imports/story/import-victory-road-rules.py').generate(RAW, ROOT)
            self.compare('content/generated/data/regions/victory-road/machines.json', outputs['machines.json'])

    def test_sevii_species_machine_outputs_use_direct_shared_views(self):
        with patch('subprocess.check_output', read_source):
            document, _ = load('imports/pokemon/import-event-island-species.py').generate(ROOT, ROOT)
            self.compare('content/generated/data/pokemon/event-island.json', document)

    def test_current_rosters_replace_historical_species_chains(self):
        from runtime_json import runtime_json_bytes
        from shared_trainers import trainer_json
        from item_catalog import item_json
        packages = [('story/import-seven-island-rules.py', 'seven-island'),
                    ('story/import-league-rematch-rules.py', 'league-rematch'),
                    ('trainers/import-trainer-tower-rules.py', 'trainer-tower')]
        for script, region in packages:
            with self.subTest(region=region), patch('subprocess.check_output', read_source):
                result = load('imports/' + script).generate(ROOT, ROOT)
                documents = result[0] if isinstance(result, tuple) else result
                for name, value in documents.items():
                    expected = json.loads((ROOT / 'content/generated/data/regions' / region / name).read_bytes())
                    if name == 'trainers.json':
                        actual = json.loads(trainer_json(ROOT / 'content/generated', value, check=True))
                    elif region == 'trainer-tower':
                        actual = json.loads(json.dumps(value, default=item_json))
                    else:
                        actual = json.loads(runtime_json_bytes(value, sort_keys=True, default=item_json))
                    self.assertEqual(expected, actual, region + '/' + name)

    def test_sevii_item_machine_outputs_use_direct_shared_views(self):
        with patch('subprocess.check_output', read_source):
            for script, family, resource in [('imports/items/import_four_island_items.py', 'four-island-items', 'data/items/four-island.json'),
                                   ('imports/items/import-six-island-items.py', 'six-island-items', 'data/items/six-island.json'),
                                   ('imports/story/import-five-island-rules.py', 'five-island-rules', 'data/regions/five-island/items.json')]:
                with self.subTest(family=family):
                    module = load(script)
                    generate = module.generate_items if family == 'five-island-rules' else module.generate
                    document, _ = generate(ROOT, ROOT)
                    self.compare('content/generated/' + resource, document)
            # This legacy producer has no function boundary. Evaluate only its
            # source-read/compile portion, excluding all output/lock writes.
            path = ROOT / 'scripts/imports/items/import_sevii_ruby_items.py'
            tree = ast.parse(path.read_text(), filename=str(path))
            statements = []
            for node in tree.body:
                if (isinstance(node, ast.Expr) and isinstance(node.value, ast.Call) and
                    isinstance(node.value.func, ast.Attribute) and
                    isinstance(node.value.func.value, ast.Name) and node.value.func.value.id == 'OUT'):
                    continue
                if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == 'output'
                                                         for target in node.targets):
                    break
                statements.append(node)
            tree.body = statements
            namespace = {'__file__': str(path), '__name__': 'ruby_machine_source_fixture'}
            exec(compile(tree, str(path), 'exec'), namespace)
            self.compare('content/generated/data/items/sevii-ruby.json', namespace['document'])


if __name__ == '__main__':
    unittest.main()
