#!/usr/bin/env python3
"""Check summit land conversion independently, including invalid source inputs."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
SCRIPT = ROOT / 'scripts/imports/story/import-mt-ember-summit-encounters.py'
spec = importlib.util.spec_from_file_location('summit_encounters', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SummitEncounterImportTests(unittest.TestCase):
    def setUp(self):
        self.source = json.loads((RAW / module.WILD).read_bytes())
        self.group = next(g for g in self.source['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')

    def row(self):
        return next(e for e in self.group['encounters'] if e['base_label'] == 'sMtEmberSummitPath2F_FireRed')

    def test_cli_repeats_without_old_outputs_or_raw_mutation(self):
        before = (RAW / module.WILD).read_bytes()
        expected = (ROOT / module.OUTPUT).read_bytes()
        with tempfile.TemporaryDirectory() as temporary:
            command = [sys.executable, str(SCRIPT), '--raw-root', str(RAW), '--project-root', temporary]
            for _ in range(2):
                result = subprocess.run(command, cwd=temporary, capture_output=True, text=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(expected, (Path(temporary) / module.OUTPUT).read_bytes())
            result = subprocess.run(command + ['--check'], cwd=temporary, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(before, (RAW / module.WILD).read_bytes())

    def test_missing_or_duplicate_fire_red_row_rejected(self):
        row = self.row()
        self.group['encounters'].append(copy.deepcopy(row))
        with self.assertRaisesRegex(ValueError, 'Expected one MAP_MT_EMBER_SUMMIT_PATH_2F FireRed table'):
            module.convert(json.dumps(self.source))
        self.group['encounters'] = [e for e in self.group['encounters'] if e['base_label'] != row['base_label']]
        with self.assertRaisesRegex(ValueError, 'Expected one MAP_MT_EMBER_SUMMIT_PATH_2F FireRed table'):
            module.convert(json.dumps(self.source))

    def test_malformed_slots_levels_rates_and_species_rejected(self):
        original = self.row()['land_mons']
        cases = [dict(encounter_rate=0), dict(encounter_rate=True), dict(mons=original['mons'][:-1])]
        for invalid in cases:
            changed = copy.deepcopy(original)
            changed.update(invalid)
            self.row()['land_mons'] = changed
            with self.assertRaises(ValueError):
                module.convert(json.dumps(self.source))
        for field, value in [('min_level', 0), ('max_level', 101), ('min_level', True), ('species', 'MOLTRES')]:
            changed = copy.deepcopy(original)
            changed['mons'][0][field] = value
            self.row()['land_mons'] = changed
            with self.assertRaises(ValueError):
                module.convert(json.dumps(self.source))

    def test_source_weights_are_required_and_not_repaired(self):
        weights = next(f for f in self.group['fields'] if f['type'] == 'land_mons')['encounter_rates']
        weights[0] += 1
        with self.assertRaisesRegex(ValueError, 'Invalid land slot weights'):
            module.convert(json.dumps(self.source))

    def test_missing_raw_source_fails_without_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            out = Path(temporary) / 'output'
            result = subprocess.run([sys.executable, str(SCRIPT), '--raw-root', str(Path(temporary) / 'missing'),
                                     '--project-root', str(out)], capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse((out / module.OUTPUT).exists())


if __name__ == '__main__':
    unittest.main(verbosity=2)
