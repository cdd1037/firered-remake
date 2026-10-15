#!/usr/bin/env python3
"""Read-only regeneration of direct TM/HM projections using retained raw inputs.

The fixture adapter supplies pinned local bytes to existing Git-reading source
parsers. It does not claim Git authentication, execute upstream programs, or
write generated resources. Machine membership is compared exactly with runtime.
"""
import ast
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts'))
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def load(script):
    spec = importlib.util.spec_from_file_location(script.replace('-', '_'), ROOT / 'scripts' / script)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def read_source(command, **kwargs):
    if command[:2] != ['git', 'show'] or len(command) != 3:
        raise AssertionError('Unexpected source command: ' + repr(command))
    commit, relative = command[2].split(':', 1)
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


class LateMachineImporterTests(unittest.TestCase):
    def compare(self, relative, value):
        expected = references(json.loads((ROOT / relative).read_bytes()))
        actual = references(value)
        self.assertTrue(expected, 'Empty machine-view assertion: ' + relative)
        self.assertEqual(expected, actual, relative)

    def test_midgame_machine_outputs_use_direct_shared_views(self):
        module = load('import-fuchsia-gym-items.py')
        lock = json.loads((ROOT / 'scripts/fuchsia-gym-items.lock.json').read_bytes())
        files = {row['path']: (RAW / row['path']).read_bytes() for row in lock['files']}
        outputs = module.item_outputs(lock, files, ROOT)
        self.compare('content/generated/fuchsia-gym-items/fuchsia-gym-machines.json',
                     json.loads(outputs['fuchsia-gym-machines.json']))
        module = load('import-vermilion-gym-assets.py')
        lock = module.read_lock()
        sources, local_inputs = module.authenticated_inputs(lock, RAW, root=ROOT)
        self.assertNotIn('content/generated/ssanne-machines/ssanne-machines.json', local_inputs)
        outputs = module.build(lock, sources, local_inputs)
        self.compare('content/generated/vermilion-gym-assets/assets.json', json.loads(outputs['assets.json']))
        outputs = load('import-safari-items.py').generate(ROOT)
        self.compare('content/generated/safari-items/machines.json',
                     json.loads(outputs['content/generated/safari-items/machines.json']))
        for family in ('silph', 'saffron-gym'):
            with self.subTest(family=family):
                _, _, machines = load(f'import-{family}-catalogs.py').metadata(RawSource(), ROOT)
                self.compare(f'content/generated/{family}-catalogs/machines.json', machines)
        _, machines = load('import-cinnabar-items.py').convert(RawSource(), ROOT)
        self.compare('content/generated/cinnabar-items/machines.json', machines)
        with patch('subprocess.check_output', read_source):
            outputs = load('import-viridian-gym-rules.py').generate(ROOT, ROOT)
            self.compare('content/generated/viridian-gym-rules/machines.json', outputs['machines.json'])
            outputs, _ = load('import-victory-road-rules.py').generate(ROOT, ROOT, ROOT)
            self.compare('content/generated/victory-road-rules/machines.json', outputs['machines.json'])

    def test_sevii_species_machine_outputs_use_direct_shared_views(self):
        # These converters expose a real raw-input route. Keep the test at that
        # boundary instead of pretending the project is the upstream Git tree.
        for family in ('celio-pokemon', 'cerulean-cave-pokemon'):
            with self.subTest(family=family), patch('subprocess.run',
                    side_effect=AssertionError('Raw conversion invoked a process')):
                documents = load(f'import-{family}.py').generate(None, ROOT, raw=RAW)
                self.compare(f'content/generated/{family}/machines.json', documents['machines.json'])
        with patch('subprocess.check_output', read_source):
            for family in ('seven-island-rules',):
                with self.subTest(family=family):
                    documents = load(f'import-{family}.py').generate(ROOT, ROOT)
                    self.compare(f'content/generated/{family}/machines.json', documents['machines.json'])
            document, _ = load('import-event-island-species.py').generate(ROOT, ROOT)
            self.compare('content/generated/event-island-species/catalog.json', document)

    def test_sevii_item_machine_outputs_use_direct_shared_views(self):
        with patch('subprocess.check_output', read_source):
            for script, family in [('import_four_island_items.py', 'four-island-items'),
                                   ('import-six-island-items.py', 'six-island-items'),
                                   ('import-five-island-rules.py', 'five-island-rules')]:
                with self.subTest(family=family):
                    module = load(script)
                    generate = module.generate_items if family == 'five-island-rules' else module.generate
                    document, _ = generate(ROOT, ROOT)
                    self.compare(f'content/generated/{family}/items.json', document)
            # This legacy producer has no function boundary. Evaluate only its
            # source-read/compile portion, excluding all output/lock writes.
            path = ROOT / 'scripts/import_sevii_ruby_items.py'
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
            self.compare('content/generated/sevii-ruby-items/items.json', namespace['document'])


if __name__ == '__main__':
    unittest.main()
