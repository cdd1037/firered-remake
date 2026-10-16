#!/usr/bin/env python3
"""Business-preserving trainer projection, publication and reference semantics."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import subprocess
import sys

from shared_trainers import (RESOURCE, publish_trainers, read_trainers,
    resolve_trainers, runtime_trainer, trainer_document)

ROOT = _PROJECT_ROOT


class SharedTrainerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.trainer = copy.deepcopy(next(iter(read_trainers(ROOT / 'content/generated').values())))

    def test_first_sevii_import_publishes_shared_trainers_at_generated_root(self):
        generated = self.root / 'content/generated'
        package = generated / 'data/regions/first-sevii'
        command = [sys.executable, str(ROOT / 'scripts/imports/story/import-first-sevii-rules.py'),
                   '--raw-root', str(ROOT / 'content/raw/pokefirered/037335f4'),
                   '--out', str(package)]
        subprocess.run(command, check=True, capture_output=True, text=True)
        regional = json.loads((package / 'trainers.json').read_text())
        shared = read_trainers(generated)
        self.assertTrue(regional['trainers'])
        self.assertEqual(set(regional['trainers']), set(shared))
        self.assertTrue(all(isinstance(value, str) for value in regional['trainers']))
        self.assertFalse((package.parent / RESOURCE).exists())
        before = {path: path.read_bytes() for path in generated.rglob('*.json')}
        subprocess.run([*command, '--check'], check=True, capture_output=True, text=True)
        self.assertEqual(before, {path: path.read_bytes() for path in generated.rglob('*.json')})
        shared_path = generated / RESOURCE
        saved = shared_path.read_bytes()
        shared_path.unlink()
        result = subprocess.run([*command, '--check'], capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn('Missing shared trainer', result.stderr)
        self.assertFalse(shared_path.exists())
        shared_path.write_bytes(saved)

    def test_projection_removes_only_named_definition_provenance(self):
        self.trainer['source'] = {'path': 'trainer.h'}
        self.trainer['moneySource'] = {'path': 'money.c'}
        self.trainer['party'][0]['source'] = {'path': 'party.h'}
        self.trainer['party'][0]['speciesNameSource'] = {'path': 'names.h'}
        self.trainer['additionalBusinessData'] = {'source': 'must survive'}
        original = copy.deepcopy(self.trainer)
        result = runtime_trainer(self.trainer)
        self.assertEqual(original, self.trainer)
        self.assertEqual({'source': 'must survive'}, result['additionalBusinessData'])
        self.assertNotIn('source', result)
        self.assertNotIn('moneySource', result)
        self.assertNotIn('source', result['party'][0])
        self.assertNotIn('speciesNameSource', result['party'][0])

    def test_document_preserves_battle_dispatch_source_and_other_fields(self):
        battle = {'source': {'path': 'data/scripts/trainers.inc', 'startLine': 10}}
        document = dict(trainers=[self.trainer], battles=[battle], custom={'source': 'keep'}, resolvedMoves=[1], sourceCommit='import-only', profile='import-only', completeSourceRoster=False)
        output = trainer_document(self.root, document)
        self.assertEqual([self.trainer['id']['value']], output['trainers'])
        self.assertEqual([battle], output['battles'])
        self.assertEqual({'source': 'keep'}, output['custom'])
        self.assertNotIn('resolvedMoves', output)
        self.assertFalse({'sourceCommit', 'profile', 'completeSourceRoster'} & output.keys())
        self.assertEqual([self.trainer], resolve_trainers(self.root, output))

    def test_derived_move_proofs_are_not_runtime_documents(self):
        output = trainer_document(self.root, dict(trainers=[self.trainer], resolvedMoves=[1]))
        self.assertNotIn('resolvedMoves', output)

    def test_metadata_differences_do_not_conflict(self):
        publish_trainers(self.root, [self.trainer])
        self.trainer['moneySource'] = {'startLine': 999}
        publish_trainers(self.root, [self.trainer])

    def test_business_conflicts_fail_without_replacing_resource(self):
        publish_trainers(self.root, [self.trainer])
        original = (self.root / RESOURCE).read_bytes()
        self.trainer['encodedTrainerName'][0] ^= 1
        with self.assertRaisesRegex(ValueError, 'Conflicting shared trainer business data'):
            publish_trainers(self.root, [self.trainer])
        self.assertEqual(original, (self.root / RESOURCE).read_bytes())

    def test_duplicate_ids_and_numeric_ids_fail(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate shared trainer'):
            publish_trainers(self.root, [self.trainer, self.trainer])
        other = copy.deepcopy(self.trainer)
        other['id']['value'] = 'pret:TRAINER_TEST_OTHER'
        with self.assertRaisesRegex(ValueError, 'Duplicate source trainer ID'):
            publish_trainers(self.root, [self.trainer, other])

    def test_check_is_read_only_and_requires_existing_value(self):
        with self.assertRaisesRegex(ValueError, 'Missing shared trainer'):
            publish_trainers(self.root, [self.trainer], check=True)
        self.assertFalse((self.root / RESOURCE).exists())
        publish_trainers(self.root, [self.trainer])
        original = (self.root / RESOURCE).read_bytes()
        publish_trainers(self.root, [self.trainer], check=True)
        self.assertEqual(original, (self.root / RESOURCE).read_bytes())

    def test_references_are_strict_and_ordered(self):
        second = copy.deepcopy(self.trainer)
        second['id']['value'] = 'pret:TRAINER_TEST_SECOND'
        second['sourceTrainerId'] += 10000
        publish_trainers(self.root, [self.trainer, second])
        self.assertEqual([second, self.trainer], resolve_trainers(self.root, {'trainers': [second['id']['value'], self.trainer['id']['value']]}))
        for references in [[], [self.trainer], ['pret:TRAINER_MISSING'], [self.trainer['id']['value']] * 2]:
            with self.assertRaises(ValueError): resolve_trainers(self.root, {'trainers': references})

    def test_encoded_names_order_flags_and_empty_move_slots_survive(self):
        shared = read_trainers(ROOT / 'content/generated')
        candidate = next(t for t in shared.values() if any(m['value'] == 'pret:MOVE_NONE' for p in t['party'] for m in p['customMoves']))
        output = trainer_document(self.root, {'trainers': [candidate]})
        self.assertEqual(candidate, resolve_trainers(self.root, output)[0])

    def test_local_raw_source_recreates_every_current_shared_definition(self):
        path = ROOT / 'scripts/imports/trainers/import-shared-trainers.py'
        spec = importlib.util.spec_from_file_location('shared_trainer_importer', path)
        importer = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(importer)
        generated = ROOT / 'content/generated'
        ids = importer.reference_ids(generated)
        definitions = importer.generate(ROOT / 'content/raw/pokefirered/037335f4', ids)
        self.assertEqual({row['id']['value']: row for row in definitions}, read_trainers(generated))
        publish_trainers(self.root, definitions)
        self.assertEqual((generated / RESOURCE).read_bytes(), (self.root / RESOURCE).read_bytes())


if __name__ == '__main__': unittest.main()
