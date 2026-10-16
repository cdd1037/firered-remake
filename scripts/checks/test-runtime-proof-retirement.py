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
            if command[:2] == ['git', '-C']:
                self.assertEqual(['git', '-C', str(ROOT), 'show'], command[:4])
                self.assertEqual(5, len(command))
            else:
                self.assertEqual(['git', 'show'], command[:2])
                self.assertEqual(3, len(command))
                self.assertEqual(ROOT, kwargs.get('cwd'))
            commit, path = command[-1].split(':', 1)
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
                expected_names = {'trainers.json'}
                if family == 'seven-island':
                    expected_names |= {'encounters.json', 'items.json'}
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
                    path = command[-1].split(':', 1)[1]
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
                    message = 'Unrecognized party fields' \
                        if damaged == 'party' else ('First trainer command' if family == 'seven-island' else 'Trainer script differs')
                    with self.assertRaisesRegex(ValueError, message):
                        module.generate(ROOT, ROOT)

    def test_seafoam_outputs_keep_business_data_without_unused_rules_report(self):
        module = load('imports/story/import-seafoam-rules.py')

        raw = ROOT / 'content/raw/pokefirered/037335f4'
        documents, files = module.generate(raw)
        self.assertEqual({'trainers.json'}, documents.keys())
        for name, document in documents.items():
            actual = module.trainer_json(ROOT / 'content/generated', document, check=True)
            self.assertEqual((ROOT / 'content/generated/data/regions/seafoam' / name).read_bytes(), actual)
        for path, data in files.items():
            self.assertEqual((ROOT / path).read_bytes(), data, path)
        original = module.WorldSource.text
        for damaged in ('encounter-weights', 'trainer-party'):
            def broken_source(source, path):
                data = original(source, path)
                if damaged == 'encounter-weights' and path == 'src/data/wild_encounters.json':
                    value = json.loads(data)
                    fields = next(group['fields'] for group in value['wild_encounter_groups'] if group['label'] == 'gWildMonHeaders')
                    next(field for field in fields if field['type'] == 'land_mons')['encounter_rates'][0] = 21
                    return json.dumps(value)
                if damaged == 'trainer-party' and path == 'src/data/trainer_parties.h':
                    return data.replace('.species = SPECIES_', '.invalidSpecies = SPECIES_')
                return data
            with self.subTest(damaged=damaged), patch.object(module.WorldSource, 'text', broken_source):
                with self.assertRaises(ValueError): module.generate(raw)

    def test_early_field_rules_use_raw_without_stage_documents(self):
        module = load('imports/story/import-early-field-rules.py')
        source = module.WorldSource(ROOT / 'content/raw/pokefirered/037335f4')
        headers = module.land_headers(source)
        self.assertEqual(12, len(headers))
        self.assertEqual(module.LAND_MAPS, {row['map'] for row in headers})
        self.assertTrue(all(set(row) == {'map', 'land_mons'} for row in headers))

    def test_field_services_regenerate_only_live_business_documents(self):
        module = load('imports/story/import-field-services.py')
        from generated_output_compare import check_item_outputs
        from shared_trainers import trainer_json
        outputs = module.generate(ROOT / 'content/raw/pokefirered/037335f4')
        self.assertEqual(14, len(outputs))
        self.assertFalse(any(name.endswith(('contracts.json', 'catalog.json', 'manifest.json', 'npc-source.json')) for name in outputs))
        self.assertEqual(9, len(outputs['maps/regions/npc-trades/trades.json']))
        self.assertEqual([178, 44], [len(row['cells']) for row in outputs['maps/regions/route16-fly-house/passage.json']['upperFootAdmissions']])
        self.assertEqual(4, len(outputs['maps/regions/original-tm-acquisition/services.json']))
        encoded = {name: value if isinstance(value, bytes) else trainer_json(ROOT / 'content/generated', value, check=True)
                   if name.endswith('trainers.json') else module.encode(value) for name, value in outputs.items()}
        check_item_outputs(ROOT / 'content/generated', encoded)

    def test_retired_species_overlays_leave_live_encounters_and_trainers(self):
        from runtime_json import runtime_json_bytes
        from safari_features import compile_safari_encounters
        from shared_trainers import trainer_json
        from world_source import WorldSource
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        generated = ROOT / 'content/generated'
        for family in ('safari', 'surf', 'fuchsia-route'):
            with self.subTest(family=family):
                prefix = generated / 'data/pokemon' / family
                overlay = prefix / 'catalog.json' if family == 'fuchsia-route' else prefix.with_suffix('.json')
                self.assertFalse(overlay.exists())
                self.assertFalse((prefix / 'source-fixtures.json').exists())
                relative = {'safari': 'maps/regions/safari/encounters.json',
                            'surf': 'data/encounters/surf.json',
                            'fuchsia-route': 'data/pokemon/fuchsia-route/trainers.json'}[family]
                target = generated / relative
                before = target.read_bytes(), target.stat().st_mtime_ns
                if family == 'safari':
                    data = runtime_json_bytes(compile_safari_encounters(WorldSource(raw)), sort_keys=True)
                elif family == 'surf':
                    data = load('imports/story/import-surf-encounters.py').build(ROOT)
                else:
                    module = load('imports/trainers/import-fuchsia-route-trainers.py')
                    self.assertEqual(relative, module.RESOURCE)
                    data = trainer_json(generated, module.generate(raw), check=True)
                self.assertEqual(before[0], data, relative)
                self.assertEqual(before, (target.read_bytes(), target.stat().st_mtime_ns))

    def test_mail_compilers_preserve_live_values_and_pixels(self):
        from generated_output_compare import check_item_outputs
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        generated = ROOT / 'content/generated'
        for name, count in [('story/import-npc-trade-mail.py', 9), ('presentation/import-mail-presentation.py', 48)]:
            with self.subTest(compiler=name):
                outputs = load('imports/' + name).generate(raw)
                self.assertEqual(count, len(outputs))
                self.assertFalse(any(Path(p).name in ('contracts.json', 'manifest.json') for p in outputs))
                check_item_outputs(generated, outputs)

    def test_tower_outputs_preserve_runtime_values(self):
        module = load('imports/trainers/import-trainer-tower-rules.py')

        def read_source(command, **kwargs):
            if command[:2] == ['git', '-C']:
                self.assertEqual(['git', '-C', str(ROOT), 'show'], command[:4])
                self.assertEqual(5, len(command))
            else:
                self.assertEqual(['git', 'show'], command[:2])
                self.assertEqual(3, len(command))
                self.assertEqual(ROOT, kwargs.get('cwd'))
            commit, path = command[-1].split(':', 1)
            self.assertEqual(module.COMMIT, commit)
            self.assertFalse(Path(path).is_absolute())
            self.assertNotIn('..', Path(path).parts)
            return (ROOT / module.RAW / path).read_bytes()

        with patch.object(module.subprocess, 'check_output', side_effect=read_source):
            outputs, raw = module.generate(ROOT, ROOT)
        self.assertTrue(raw)
        self.assertEqual({'catalog.json', 'items.json'}, outputs.keys())
        self.assertFalse((ROOT / module.BASE / 'creation-vectors.json').exists())
        for path, document in outputs.items():
            # Item metadata can be a shared Python value object before serialization.
            actual = (json.dumps(document, sort_keys=True, separators=(',', ':'),
                                 ensure_ascii=False, default=module.item_json) + '\n').encode()
            self.assertEqual((ROOT / module.BASE / path).read_bytes(), actual, path)


if __name__ == '__main__':
    unittest.main(verbosity=2)
