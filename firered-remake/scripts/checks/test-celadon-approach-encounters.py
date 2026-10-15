#!/usr/bin/env python3
"""Exercise the real Celadon encounter CLI with isolated raw inputs and outputs."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
IMPORTER = ROOT / 'scripts/imports/story/import-celadon-approach-encounters.py'
RAW = ROOT / 'content/raw/pokefirered/037335f4'
CURRENT = ROOT / 'content/generated/data/encounters/celadon-approach.json'
WILD = 'src/data/wild_encounters.json'
SPECIES = 'src/data/pokemon/species_info.h'
POKEMON = 'src/pokemon.c'


class CeladonEncounterTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix='celadon-encounters-')
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.raw, self.output = self.root / 'raw', self.root / 'output/encounters.json'
        for relative in (WILD, SPECIES, POKEMON):
            target = self.raw / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(RAW / relative, target)

    def invoke(self, *args, succeeds=True):
        result = subprocess.run([sys.executable, str(IMPORTER), '--raw', str(self.raw),
                                 '--out', str(self.output), *args], cwd=self.root,
                                capture_output=True, text=True)
        self.assertEqual(result.returncode == 0, succeeds, result.stdout + result.stderr)
        return result

    def mutate_wild(self, change):
        path = self.raw / WILD
        document = json.loads(path.read_bytes())
        change(document['wild_encounter_groups'][0])
        path.write_text(json.dumps(document), encoding='utf-8')

    def test_real_raw_matches_preserved_resource_and_is_repeatable(self):
        self.assertFalse(self.output.exists())
        self.invoke()
        first = self.output.read_bytes()
        document = json.loads(first)
        self.assertEqual(json.loads(CURRENT.read_bytes()), document)
        self.assertNotIn('excludedTables', document)
        self.assertTrue(all(set(row) == {'species', 'common', 'rare'} for row in document['heldItems']))
        self.invoke()
        self.assertEqual(first, self.output.read_bytes())
        self.invoke('--check')

    def test_edits_follow_firered_fields_and_weights_not_leafgreen(self):
        def change(group):
            for field in group['fields']:
                if field['type'] == 'land_mons':
                    field['encounter_rates'][0:2] = [19, 21]
            for row in group['encounters']:
                if row['base_label'] == 'sRoute8_FireRed':
                    row['land_mons']['encounter_rate'] = 22
                    row['land_mons']['mons'][0].update(species='SPECIES_RATTATA', min_level=16, max_level=19)
                if row['base_label'] == 'sRoute7_LeafGreen':
                    row['land_mons']['mons'][0]['species'] = 'SPECIES_BULBASAUR'
        self.mutate_wild(change)
        self.invoke()
        doc = json.loads(self.output.read_bytes())
        self.assertEqual(22, doc['tables'][0]['rawEncounterRate'])
        self.assertEqual({'index': 0, 'species': {'value': 'pret:SPECIES_RATTATA'},
                          'minimumLevel': 16, 'maximumLevel': 19, 'weight': 19}, doc['tables'][0]['slots'][0])
        self.assertEqual(21, doc['tables'][0]['slots'][1]['weight'])
        self.assertEqual('pret:SPECIES_PIDGEY', doc['tables'][1]['slots'][0]['species']['value'])
        self.assertIn('pret:SPECIES_RATTATA', [r['species'] for r in doc['heldItems']])

    def test_held_items_come_from_species_source(self):
        path = self.raw / SPECIES
        text = path.read_text(encoding='utf-8')
        start = text.index('[SPECIES_MEOWTH]')
        end = text.index('\n    },', start)
        text = text[:start] + text[start:end].replace('.itemRare = ITEM_NUGGET,', '.itemRare = ITEM_POTION,') + text[end:]
        path.write_text(text, encoding='utf-8')
        self.invoke()
        held = {r['species']: r for r in json.loads(self.output.read_bytes())['heldItems']}
        self.assertEqual('ITEM_POTION', held['pret:SPECIES_MEOWTH']['rare'])

    def test_missing_source_does_not_replace_existing_output(self):
        self.invoke()
        before = self.output.read_bytes()
        (self.raw / SPECIES).unlink()
        result = self.invoke(succeeds=False)
        self.assertIn('species_info.h', result.stderr)
        self.assertEqual(before, self.output.read_bytes())

    def test_missing_or_duplicate_firered_table_fails_before_publish(self):
        original = (self.raw / WILD).read_bytes()
        for duplicate in (False, True):
            with self.subTest(duplicate=duplicate):
                (self.raw / WILD).write_bytes(original)
                def change(group):
                    row = next(r for r in group['encounters'] if r['base_label'] == 'sRoute8_FireRed')
                    if duplicate:
                        group['encounters'].append(row)
                    else:
                        group['encounters'].remove(row)
                self.mutate_wild(change)
                self.invoke(succeeds=False)
                self.assertFalse(self.output.exists())

    def test_mismatched_slots_and_changed_draw_policy_are_rejected(self):
        self.mutate_wild(lambda g: next(f for f in g['fields'] if f['type'] == 'land_mons')['encounter_rates'].pop())
        self.invoke(succeeds=False)
        shutil.copyfile(RAW / WILD, self.raw / WILD)
        path = self.raw / POKEMON
        path.write_text(path.read_text(encoding='utf-8').replace('u16 rnd = Random() % 100;', 'u16 rnd = Random() % 99;'), encoding='utf-8')
        self.invoke(succeeds=False)
        self.assertFalse(self.output.exists())

    def test_check_detects_stale_output_without_overwriting(self):
        self.invoke()
        before = self.output.read_bytes()
        self.mutate_wild(lambda g: next(r for r in g['encounters'] if r['base_label'] == 'sRoute8_FireRed')['land_mons'].update(encounter_rate=22))
        self.invoke('--check', succeeds=False)
        self.assertEqual(before, self.output.read_bytes())


if __name__ == '__main__':
    unittest.main(verbosity=2)
