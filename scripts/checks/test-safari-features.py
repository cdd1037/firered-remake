#!/usr/bin/env python3
"""Validate live Safari contracts from raw source, without building map presentation."""
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from world_source import WorldSource
from world_selection import load_selection
from source_features import feature_worlds, script_blocks
from safari_features import compile_safari

RAW = ROOT / 'content/raw/pokefirered/037335f4'


class SafariContractsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.src = WorldSource(RAW)
        manifest = load_selection(ROOT / 'data/world-maps.json')
        names = [m['sourceName'] for m in manifest if m['path'].startswith('maps/regions/safari/')]
        cls.worlds = feature_worlds(cls.src, names)
        cls.scripts = script_blocks(cls.src, names)

    def test_retained_business_values(self):
        result = compile_safari(self.src, self.worlds, self.scripts, {})
        baseline = json.loads((ROOT / 'content/generated/maps/regions/safari/contracts.json').read_text())
        expected = {k: baseline[k] for k in result}
        for row in expected['pickups']:
            row.pop('sourceRef', None)
            row.pop('lines', None)
        for row in expected['hiddenItems']:
            row.pop('sourceRef', None)
        # Pickup rows have independent map/event identities, not positional semantics.
        for key in ('pickups', 'hiddenItems'):
            expected[key].sort(key=lambda row: json.dumps(row, sort_keys=True))
            result[key].sort(key=lambda row: json.dumps(row, sort_keys=True))
        self.assertEqual(expected, result)
        self.assertEqual({'schemaVersion', 'entryFee', 'startingBalls', 'startingSteps',
                          'pickups', 'hiddenItems'}, result.keys())
        for row in result['pickups']:
            self.assertEqual({'map', 'localId', 'item', 'flag', 'x', 'y', 'label'}, row.keys())
        for row in result['hiddenItems']:
            self.assertEqual({'map', 'declaration'}, row.keys())

    def test_raw_session_change_is_rejected(self):
        class ChangedSource(WorldSource):
            def text(self, path):
                text = super().text(path)
                return text.replace('gNumSafariBalls = 30;', 'gNumSafariBalls = 31;') if path == 'src/safari_zone.c' else text
        with self.assertRaisesRegex(ValueError, 'Safari source session contract differs'):
            compile_safari(ChangedSource(RAW), self.worlds, self.scripts, {})

    def test_raw_pickup_script_change_reaches_final_contract(self):
        class ChangedSource(WorldSource):
            def text(self, path):
                text = super().text(path)
                return text.replace('finditem ITEM_GOLD_TEETH', 'finditem ITEM_NUGGET') if path == 'data/scripts/item_ball_scripts.inc' else text
        changed_source = ChangedSource(RAW)
        changed_scripts = script_blocks(changed_source, [w['sourceName'] for w in self.worlds])
        result = compile_safari(changed_source, self.worlds, changed_scripts, {})
        pickup = next(r for r in result['pickups'] if r['label'] == 'SafariZone_West_EventScript_ItemGoldTeeth')
        self.assertEqual('ITEM_NUGGET', pickup['item'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
