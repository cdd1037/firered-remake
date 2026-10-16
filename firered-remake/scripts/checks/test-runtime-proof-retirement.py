#!/usr/bin/env python3
"""Verify retained output parity and retired proof output boundaries.

Git-reading parsers receive retained raw bytes through a read-only adapter;
this checks conversion parity, not Git authentication or original-C execution.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT / 'scripts/lib'))


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), ROOT / 'scripts' / name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RuntimeProofRetirementTests(unittest.TestCase):
    def _regional_source_reader(self, module):
        def read_source(command, **kwargs):
            self.assertEqual(['git', 'show'], command[:2])
            self.assertEqual(3, len(command))
            commit, path = command[2].split(':', 1)
            self.assertEqual(module.COMMIT, commit)
            self.assertFalse(Path(path).is_absolute())
            self.assertNotIn('..', Path(path).parts)
            return (ROOT / 'content/raw/pokefirered/037335f4' / path).read_bytes()
        return read_source

    def test_league_and_seven_outputs_keep_business_data_without_unused_reports(self):
        for family in ('pokemon-league', 'league-rematch', 'seven-island'):
            with self.subTest(family=family):
                module = load('imports/story/import-' + family + '-rules.py')
                with patch.object(module.subprocess, 'check_output', side_effect=self._regional_source_reader(module)):
                    result = module.generate(ROOT, ROOT)
                documents = result if isinstance(result, dict) else result[0]
                expected_names = {'trainers.json', 'pokemon.json'}
                if family == 'seven-island':
                    expected_names |= {'encounters.json', 'machines.json', 'items.json'}
                self.assertEqual(expected_names, documents.keys())
                directory = ROOT / 'content/generated/data/regions' / family
                self.assertFalse((directory / 'rules.json').exists())
                for name, document in documents.items():
                    if name == 'trainers.json':
                        actual = module.trainer_json(ROOT / 'content/generated', document, check=True)
                    else:
                        actual = module.runtime_json_bytes(document, sort_keys=True, default=getattr(module, 'item_json', None))
                    self.assertEqual((directory / name).read_bytes(), actual, name)

                arguments = [module.__file__, str(ROOT), str(ROOT), str(ROOT), '--check']
                if family == 'seven-island':
                    arguments = [module.__file__, str(ROOT), str(ROOT), '--dependency-root', str(ROOT), '--check']
                with patch.object(module.subprocess, 'check_output', side_effect=self._regional_source_reader(module)), \
                     patch.object(sys, 'argv', arguments), patch.object(sys, 'stdout', io.StringIO()) as output:
                    module.main()
                self.assertTrue(json.loads(output.getvalue())['checked'])

    def test_league_and_seven_reject_damaged_business_inputs(self):
        for family in ('pokemon-league', 'league-rematch', 'seven-island'):
            module = load('imports/story/import-' + family + '-rules.py')
            read_source = self._regional_source_reader(module)
            for damaged in ('party', 'battle-script'):
                def broken_source(command, **kwargs):
                    data = read_source(command, **kwargs)
                    path = command[2].split(':', 1)[1]
                    if damaged == 'party' and path == 'src/data/trainer_parties.h':
                        self.assertIn(b'.species = SPECIES_', data)
                        return data.replace(b'.species = SPECIES_', b'.unsupportedSpecies = SPECIES_')
                    if damaged == 'battle-script':
                        if family == 'seven-island' and path == 'data/scripts/trainers.inc':
                            original = b'trainerbattle_single TRAINER_CRUSH_GIRL_CYNDY'
                            self.assertIn(original, data)
                            return data.replace(original, b'trainerbattle_unsupported TRAINER_CRUSH_GIRL_CYNDY')
                        if family != 'seven-island' and path == 'data/maps/PokemonLeague_LoreleisRoom/scripts.inc':
                            self.assertIn(b'trainerbattle_no_intro ', data)
                            return data.replace(b'trainerbattle_no_intro ', b'trainerbattle_single ')
                    return data

                with self.subTest(family=family, damaged=damaged), \
                     patch.object(module.subprocess, 'check_output', side_effect=broken_source):
                    message = ('Unexpected member fields' if family == 'seven-island' else 'Unknown member field') \
                        if damaged == 'party' else ('First trainer command' if family == 'seven-island' else 'Trainer script differs')
                    with self.assertRaisesRegex(ValueError, message):
                        module.generate(ROOT, ROOT)

    def test_seafoam_outputs_keep_business_data_without_unused_rules_report(self):
        module = load('imports/story/import-seafoam-rules.py')

        def read_source(command, **kwargs):
            self.assertEqual(['git', 'show'], command[:2])
            self.assertEqual(3, len(command))
            commit, path = command[2].split(':', 1)
            self.assertEqual(module.COMMIT, commit)
            self.assertFalse(Path(path).is_absolute())
            self.assertNotIn('..', Path(path).parts)
            return (ROOT / module.RAW / path).read_bytes()

        with patch.object(module.subprocess, 'check_output', side_effect=read_source):
            documents, _, files = module.generate(ROOT, ROOT)
        self.assertEqual({'trainers.json', 'pokemon.json', 'machines.json'}, documents.keys())
        self.assertFalse((ROOT / module.BASE / 'rules.json').exists())
        for name, document in documents.items():
            if name == 'trainers.json':
                document = json.loads(module.trainer_json(ROOT / 'content/generated', document, check=True))
            self.assertEqual(json.loads((ROOT / module.BASE / name).read_bytes()), document, name)
        for path, data in files.items():
            self.assertEqual((ROOT / path).read_bytes(), data, path)
        for damaged in ('static-command', 'learnset'):
            def broken_source(command, **kwargs):
                data = read_source(command, **kwargs)
                if damaged == 'static-command' and command[2].endswith('data/maps/SeafoamIslands_B4F/scripts.inc'):
                    return data.replace(b'setwildbattle SPECIES_ARTICUNO, 50', b'setwildbattle SPECIES_ARTICUNO, 49')
                if damaged == 'learnset' and command[2].endswith('src/data/pokemon/level_up_learnsets.h'):
                    return data.replace(b'LEVEL_UP_MOVE(', b'INVALID_MOVE(')
                return data
            # Let the malformed learnset reach its parser, rather than stop at
            # the shared-byte consistency check against the unmodified raw file.
            shared = {path: value for path, value in module.SHARED_INPUTS.items()
                      if path != 'src/data/pokemon/level_up_learnsets.h'}
            with self.subTest(damaged=damaged), patch.object(module.subprocess, 'check_output', side_effect=broken_source), \
                 patch.dict(module.SHARED_INPUTS, shared, clear=True):
                message = 'Original static command' if damaged == 'static-command' else 'Expected source natural moves'
                with self.assertRaisesRegex(ValueError, message):
                    module.generate(ROOT, ROOT)

    def test_mtmoon_dependencies_keep_business_values_and_source_validation(self):
        module = load('imports/maps/import-mtmoon-region.py')
        source = module.Source(ROOT / 'content/raw/pokefirered/037335f4')
        layouts = source.json('data/layouts/layouts.json')['layouts']
        worlds = []
        # Dependency conversion needs source events and dimensions, not rendered PNGs.
        for name, _, _ in module.MAPS:
            path = f'data/maps/{name}/map.json'
            events = source.json(path)
            layout = next(row for row in layouts if row.get('id') == events['layout'])
            worlds.append(dict(events=events, width=layout['width'], height=layout['height'],
                               sourceRef=source.ref(path, events['id'])))
        scripts = module.script_inventory(source, worlds)
        constants = module.constants_inventory(source, worlds, scripts)
        actual = module.dependency_inventory(source, worlds, scripts, constants)
        expected = json.loads((ROOT / 'content/generated/maps/regions/mtmoon/dependencies.json').read_text())
        self.assertEqual(expected, actual)
        self.assertTrue(actual['trainers'] and actual['encounters'] and actual['pickups'])
        self.assertTrue({'moves', 'species', 'items'}.isdisjoint(actual))
        # Omitted inventories must not hide malformed referenced raw declarations.
        for path in ('src/data/pokemon/level_up_learnsets.h',
                     'src/data/battle_moves.h', 'src/data/items.json'):
            with self.subTest(path=path):
                original = source.data[path]
                try:
                    source.data[path] = (b'{"items":[]}' if path.endswith('.json') else b'')
                    with self.assertRaises(ValueError):
                        module.dependency_inventory(source, worlds, scripts, constants)
                finally:
                    source.data[path] = original

    def test_pokemon_outputs_are_only_runtime_catalogs(self):
        for family in ('safari', 'surf', 'fuchsia-route'):
            with self.subTest(family=family):
                prefix = 'content/generated/data/pokemon/' + family
                module = load('imports/pokemon/import-' + family + '-pokemon.py')
                outputs = module.generate(ROOT)
                if family == 'fuchsia-route':
                    trainer_path = prefix + '/trainers.json'
                    outputs[trainer_path] = module.trainer_json(ROOT / 'content/generated', json.loads(outputs[trainer_path]), check=True)
                expected = {prefix + ('/catalog.json' if family == 'fuchsia-route' else '.json')}
                if family == 'fuchsia-route':
                    expected.add(prefix + '/trainers.json')
                self.assertEqual(expected, outputs.keys())
                self.assertFalse((ROOT / prefix / 'source-fixtures.json').exists())
                for path, data in outputs.items():
                    self.assertEqual((ROOT / path).read_bytes(), data, path)

    def test_tower_outputs_preserve_runtime_values(self):
        module = load('imports/trainers/import-trainer-tower-rules.py')

        def read_source(command, **kwargs):
            self.assertEqual(['git', 'show'], command[:2])
            self.assertEqual(3, len(command))
            commit, path = command[2].split(':', 1)
            self.assertEqual(module.COMMIT, commit)
            self.assertFalse(Path(path).is_absolute())
            self.assertNotIn('..', Path(path).parts)
            return (ROOT / module.RAW / path).read_bytes()

        with patch.object(module.subprocess, 'check_output', side_effect=read_source):
            outputs, raw = module.generate(ROOT, ROOT)
        self.assertTrue(raw)
        self.assertEqual({'catalog.json', 'pokemon.json', 'items.json'}, outputs.keys())
        self.assertFalse((ROOT / module.BASE / 'creation-vectors.json').exists())
        for path, document in outputs.items():
            # Item metadata can be a shared Python value object before serialization.
            actual = (json.dumps(document, sort_keys=True, separators=(',', ':'),
                                 ensure_ascii=False, default=module.item_json) + '\n').encode()
            self.assertEqual((ROOT / module.BASE / path).read_bytes(), actual, path)


if __name__ == '__main__':
    unittest.main(verbosity=2)
