#!/usr/bin/env python3
"""Exercise only three encounter producers, without rebuilding region presentation."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'


def load(relative):
    spec = importlib.util.spec_from_file_location('encounter_import', ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RawSource:
    def text(self, path):
        return (RAW / path).read_text(encoding='utf-8')

    def json(self, path):
        return json.loads(self.text(path))


class RegionalEncounterImportTests(unittest.TestCase):
    def assert_business_only(self, document):
        self.assertEqual({'schemaVersion', 'tables', 'heldItems', 'heldItemDrawBound'}, set(document))
        for row in document['heldItems']:
            self.assertEqual({'species', 'common', 'rare'}, set(row))
        self.assertEqual({s['species']['value'] for t in document['tables'] for s in t['slots']},
                         {r['species'] for r in document['heldItems']})

    def test_real_route_and_cave_cli_reproduces_current_business_data(self):
        for family in ('route11', 'rocktunnel'):
            with self.subTest(family=family), tempfile.TemporaryDirectory() as temporary:
                output = Path(temporary) / 'nested/encounters.json'
                command = [sys.executable, str(ROOT / f'scripts/imports/story/import-{family}-encounters.py'),
                           '--raw', str(RAW), '--out', str(output)]
                for _ in range(2):
                    result = subprocess.run(command, cwd=temporary, capture_output=True, text=True)
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    document = json.loads(output.read_bytes())
                    self.assert_business_only(document)
                    self.assertEqual(json.loads((ROOT / f'content/generated/data/encounters/{family}.json').read_bytes()), document)

    def test_safari_encounter_conversion_needs_no_provenance_provider(self):
        module = load('scripts/imports/maps/import-safari-region.py')
        document = module.encounter_contract(RawSource())
        self.assert_business_only(document)
        self.assertEqual(json.loads((ROOT / 'content/generated/maps/regions/safari/encounters.json').read_bytes()), document)

    def test_missing_land_table_rejected(self):
        class EmptySource(RawSource):
            def json(self, path):
                return {'wild_encounter_groups': []}
        with self.assertRaisesRegex(ValueError, 'Missing exact FireRed Safari table'):
            load('scripts/imports/maps/import-safari-region.py').encounter_contract(EmptySource())


if __name__ == '__main__':
    unittest.main(verbosity=2)
