#!/usr/bin/env python3
"""Cycling Road final feature equivalence and raw-source mutation regression tests."""
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from cycling_road_features import compile_cycling_road, land_encounters, trainer_battles
from world_source import WorldSource


class CyclingRoadFeatureTests(unittest.TestCase):
    def test_final_business_values_match(self):
        docs = compile_cycling_road(WorldSource(RAW))
        self.assertEqual({'trainers.json', 'land-encounters.json'}, docs.keys())
        for name, document in docs.items():
            self.assertEqual(json.loads((ROOT / 'content/generated/maps/regions/cycling-road' / name).read_text()), document)

    def test_raw_encounter_rate_change_reaches_output(self):
        class ChangedSource(WorldSource):
            def json(self, path):
                document = super().json(path)
                if path == 'src/data/wild_encounters.json':
                    row = next(row for group in document['wild_encounter_groups'] for row in group.get('encounters', [])
                               if row['base_label'] == 'sRoute17_FireRed')
                    row['land_mons']['encounter_rate'] += 1
                return document
        original = land_encounters(WorldSource(RAW))
        changed = land_encounters(ChangedSource(RAW))
        self.assertEqual(original[0]['rawEncounterRate'] + 1, changed[0]['rawEncounterRate'])
        self.assertEqual(original[1], changed[1])

    def test_missing_raw_trainer_rejected(self):
        class ChangedSource(WorldSource):
            def text(self, path):
                text = super().text(path)
                return text.replace('TRAINER_BIKER_LAO', 'TRAINER_MISSING_LAO') if path == 'data/scripts/trainers.inc' else text
        with self.assertRaises((ValueError, KeyError)):
            trainer_battles(ChangedSource(RAW))


if __name__ == '__main__':
    unittest.main(verbosity=2)
