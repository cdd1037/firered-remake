#!/usr/bin/env python3
"""Independent pinned-source checks for the ordinary all-rod runtime document."""
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
IMPORT = ROOT / 'scripts/imports/story/import-fishing-encounters.py'
OUTPUT = ROOT / 'content/generated/data/encounters/fishing.json'


class FishingImportTests(unittest.TestCase):
    def test_regeneration_is_deterministic_and_check_rejects_change(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'fishing.json'
            command = [sys.executable, str(IMPORT), '--raw', str(RAW), '--out', str(output)]
            for _ in range(2):
                result = subprocess.run(command, cwd=temporary, capture_output=True, text=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(OUTPUT.read_bytes(), output.read_bytes())
            self.assertEqual(0, subprocess.run(command + ['--check'], capture_output=True).returncode)
            output.write_text('{}')
            self.assertNotEqual(0, subprocess.run(command + ['--check'], capture_output=True).returncode)

    def test_all_source_slots_and_current_sevii_gap_are_exact(self):
        source = json.loads((RAW / 'src/data/wild_encounters.json').read_text())
        group = next(g for g in source['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
        output = json.loads(OUTPUT.read_text())
        tables = {(row['rod'], row['table']['map']['value']): row['table'] for row in output['tables']}
        self.assertEqual(138, len(tables))
        field = next(f for f in group['fields'] if f['type'] == 'fishing_mons')
        expected = set()
        for encounter in group['encounters']:
            if not encounter['base_label'].endswith('_FireRed') or 'fishing_mons' not in encounter or encounter['map'].startswith('MAP_SAFARI_ZONE_'):
                continue
            stable = re.sub(r'route(\d+)', r'route-\1', 'map.' + encounter['map'][4:].lower().replace('_', '-'))
            for rod, indexes, weights in [('old', [0, 1], [70, 30]), ('good', [2, 3, 4], [60, 20, 20]), ('super', [5, 6, 7, 8, 9], [40, 40, 15, 4, 1])]:
                expected.add((rod, stable))
                table = tables[(rod, stable)]
                self.assertEqual(encounter['fishing_mons']['encounter_rate'], table['rawEncounterRate'])
                self.assertEqual(indexes, [s['index'] for s in table['slots']])
                self.assertEqual(weights, [s['weight'] for s in table['slots']])
                for slot in table['slots']:
                    raw = encounter['fishing_mons']['mons'][slot['index']]
                    self.assertEqual(('pret:' + raw['species'], raw['min_level'], raw['max_level']),
                                     (slot['species']['value'], slot['minimumLevel'], slot['maximumLevel']))
        self.assertEqual(expected, set(tables))
        for rod in ['old', 'good', 'super']:
            self.assertEqual(46, sum(key[0] == rod for key in tables))
            self.assertIn((rod, 'map.fuchsia-city'), tables)
        tables = {name: table for (rod, name), table in tables.items() if rod == 'good'}
        regions = ['first-sevii', 'sevii-ruby', 'four-island', 'five-island', 'six-island', 'seven-island',
                   'cape-brink-tutor', 'original-tm-acquisition', 'lost-cave']
        loaded = {m['stableId'] for name in regions for m in json.loads((ROOT / 'content/generated/maps/regions' / name / 'catalog.json').read_text())['maps']}
        existing = {'map.two-island-cape-brink', 'map.five-island-memorial-pillar', 'map.five-island-resort-gorgeous', 'map.five-island-water-labyrinth'}
        added = loaded.intersection(tables) - existing
        self.assertTrue(added)
        self.assertIn('map.six-island-green-path', added)
        self.assertEqual({'pret:SPECIES_HORSEA', 'pret:SPECIES_MAGIKARP', 'pret:SPECIES_POLIWAG', 'pret:SPECIES_GOLDEEN'},
                         {s['species']['value'] for m in added for s in tables[m]['slots']})
        for name in added:
            self.assertEqual([(5, 15)] * 3, [(s['minimumLevel'], s['maximumLevel']) for s in tables[name]['slots']])
        for name in ['map.one-island-treasure-beach', 'map.six-island-outcast-island']:
            self.assertIn(name, tables)
            self.assertNotIn(name, loaded)


if __name__ == '__main__':
    unittest.main(verbosity=2)
