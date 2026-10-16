#!/usr/bin/env python3
"""Five Island emits live battle/item/encounter data without historical roster proofs."""
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from item_catalog import item_json
from shared_trainers import trainer_json


def load():
    path = ROOT / 'scripts/imports/story/import-five-island-rules.py'
    spec = importlib.util.spec_from_file_location('five_island_rules', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class FiveIslandRuleSourceTests(unittest.TestCase):
    def test_live_outputs_match_without_predecessor_or_canonical_inputs(self):
        module = load()
        original = Path.read_bytes
        def raw_only(path):
            self.assertNotIn('predecessor', str(path))
            self.assertNotIn('/content/generated/', str(path))
            return original(path)
        with patch.object(Path, 'read_bytes', raw_only):
            docs = module.generate(RAW, Path('/missing-dependency'))
            encounters = module.generate_encounters(RAW)
        self.assertEqual({'trainers.json'}, docs.keys())
        actual = json.loads(trainer_json(ROOT / 'content/generated', docs['trainers.json'], check=True))
        expected = json.loads((ROOT / 'content/generated/data/regions/five-island/trainers.json').read_text())
        self.assertEqual(expected, actual)
        self.assertEqual({'src/Game.Domain/Encounters/FiveIslandEncounterTables.cs',
                          'src/Game.Domain/Encounters/FiveIslandWildSource.cs'}, encounters.keys())
        for path, text in encounters.items():
            self.assertEqual((ROOT / path).read_text(), text)

    def test_item_business_values_unchanged(self):
        doc, evidence = load().generate_items(RAW, ROOT)
        self.assertIsNone(evidence)
        actual = json.loads(json.dumps(doc, default=item_json))
        expected = json.loads((ROOT / 'content/generated/data/regions/five-island/items.json').read_text())
        self.assertEqual(expected, actual)


if __name__ == '__main__':
    unittest.main(verbosity=2)
