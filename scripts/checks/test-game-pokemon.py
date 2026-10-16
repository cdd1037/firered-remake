import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import importlib.util
import json
import re
from pathlib import Path
import unittest

ROOT = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location('game_pokemon', ROOT / 'scripts/imports/pokemon/import-game-pokemon.py')
compiler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compiler)

class GamePokemonAdmissionsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.policy = json.loads((ROOT / 'data/game-pokemon.json').read_text())
        cls.canonical = json.loads((ROOT / 'content/generated/data/pokemon/canonical/catalog.json').read_text())

    def test_exact_admissions_compile(self):
        self.assertEqual(compiler.compile_admissions(self.policy, self.canonical), self.policy)
        for roster in ('owned', 'opponents', 'trainerTowerOpponents'):
            species = {row['id']['value'] for row in self.policy[roster]}
            self.assertTrue(species)
            self.assertEqual(len(species), len(self.policy[roster]))
            self.assertIn('pret:SPECIES_DUNSPARCE', species)
            self.assertNotIn('pret:SPECIES_TREECKO', species)
        self.assertLessEqual({row['id']['value'] for row in self.policy['owned']},
                             {row['id']['value'] for row in self.policy['opponents']})
        generated = json.loads((ROOT / 'content/generated/data/pokemon/game/catalog.json').read_text())
        self.assertEqual(generated, self.policy)

    def test_pattern_bush_families_have_canonical_evolutions_and_breeding(self):
        owned = {row['id']['value']: row for row in self.policy['owned']}
        breeding = {row['species']['value']: row for row in compiler.compile_breeding(ROOT, self.policy, self.canonical)['species']}
        for symbol, base in [('LEDYBA', 'LEDYBA'), ('LEDIAN', 'LEDYBA'), ('SPINARAK', 'SPINARAK'),
                             ('ARIADOS', 'SPINARAK'), ('HERACROSS', 'HERACROSS')]:
            identity = 'pret:SPECIES_' + symbol
            self.assertIn(identity, owned)
            self.assertEqual(breeding[identity]['baseEggSpecies']['value'], 'pret:SPECIES_' + base)
        for symbol, target, level in [('LEDYBA', 'LEDIAN', 18), ('SPINARAK', 'ARIADOS', 22)]:
            self.assertEqual(owned['pret:SPECIES_' + symbol]['evolutions'],
                [dict(level=level, target=dict(value='pret:SPECIES_' + target))])

    def test_pattern_bush_trainer_species_are_opponents_only(self):
        for symbol in ('SNUBBULL', 'PINSIR'):
            identity = 'pret:SPECIES_' + symbol
            self.assertNotIn(identity, {row['id']['value'] for row in self.policy['owned']})
            self.assertIn(identity, {row['id']['value'] for row in self.policy['opponents']})
            self.assertNotIn(identity, {row['species']['value'] for row in compiler.compile_breeding(ROOT, self.policy, self.canonical)['species']})

    def test_unknown_species_form_and_incorrect_evolution_rejected(self):
        for field, value in [('id', {'value': 'pret:SPECIES_MISSING'}), ('formScope', 'unknown')]:
            policy = copy.deepcopy(self.policy)
            policy['owned'][0][field] = value
            with self.assertRaises(ValueError): compiler.compile_admissions(policy, self.canonical)
        policy = copy.deepcopy(self.policy)
        policy['owned'][0]['evolutions'][0]['level'] = 1
        with self.assertRaises(ValueError): compiler.compile_admissions(policy, self.canonical)

    def test_unadmitted_evolution_target_and_duplicates_rejected(self):
        policy = copy.deepcopy(self.policy)
        policy['owned'].pop(1)
        with self.assertRaises(ValueError): compiler.compile_admissions(policy, self.canonical)
        policy = copy.deepcopy(self.policy)
        policy['owned'].append(policy['owned'][0])
        with self.assertRaises(ValueError): compiler.compile_admissions(policy, self.canonical)

    def test_breeding_is_flat_and_uses_original_machine_order(self):
        data = compiler.compile_breeding(ROOT, self.policy, self.canonical)
        self.assertEqual({row['species']['value'] for row in data['species']},
                         {row['id']['value'] for row in self.policy['owned']})
        self.assertEqual(len(data['species']), len(self.policy['owned']))
        self.assertEqual(len(data['allMachineMoves']), 58)
        self.assertEqual(data['allMachineMoves'][-1]['value'], 'pret:MOVE_DIVE')
        self.assertTrue(all(set(row) == {'species','baseEggSpecies','eggMoves'} for row in data['species']))

    def test_dunsparce_admission_preserves_source_breeding_data(self):
        species = 'pret:SPECIES_DUNSPARCE'
        definition = next(row for row in self.canonical['species'] if row['id']['value'] == species)
        metadata = next(row for row in self.canonical['metadata'] if row['id']['value'] == species)
        self.assertEqual(definition['sourceSpeciesId'], 206)
        self.assertEqual(definition['catchRate'], 190)
        self.assertEqual(definition['growthRate'], 'mediumFast')
        self.assertEqual(metadata['sourceEvolutions'], [])
        self.assertEqual(metadata['eggGroups'], ['EGG_GROUP_FIELD', 'EGG_GROUP_FIELD'])
        self.assertEqual(metadata['eggCycles'], 20)
        breeding = compiler.compile_breeding(ROOT, self.policy, self.canonical)
        row = next(row for row in breeding['species'] if row['species']['value'] == species)
        self.assertEqual(row['baseEggSpecies']['value'], species)
        self.assertEqual([move['value'] for move in row['eggMoves']], ['pret:MOVE_' + move for move in
            ('BIDE', 'ANCIENT_POWER', 'ROCK_SLIDE', 'BITE', 'HEADBUTT', 'ASTONISH', 'CURSE')])
        generated = json.loads((ROOT / 'content/generated/data/pokemon/game/breeding.json').read_text())
        self.assertEqual(generated, breeding)

    def test_moltres_admission_preserves_source_metadata_and_complete_moves(self):
        identity = 'pret:SPECIES_MOLTRES'
        for roster in ('owned', 'opponents'):
            row = next(row for row in self.policy[roster] if row['id']['value'] == identity)
            self.assertEqual(row, dict(id=dict(value=identity), evolutions=[]))
        self.assertNotIn(identity, {row['id']['value'] for row in self.policy['trainerTowerOpponents']})
        definition = next(row for row in self.canonical['species'] if row['id']['value'] == identity)
        metadata = next(row for row in self.canonical['metadata'] if row['id']['value'] == identity)
        self.assertEqual(definition['sourceSpeciesId'], 146)
        self.assertEqual(definition['catchRate'], 3)
        self.assertEqual(definition['growthRate'], 'slow')
        self.assertEqual(definition['genderRatio'], 255)
        self.assertEqual(definition['ability1'], 'ABILITY_PRESSURE')
        self.assertEqual(metadata['eggGroups'], ['EGG_GROUP_UNDISCOVERED'] * 2)
        self.assertEqual(metadata['eggCycles'], 80)
        self.assertEqual(metadata['sourceEvolutions'], [])
        source = (ROOT / 'content/raw/pokefirered/037335f4/src/data/pokemon/level_up_learnsets.h').read_text()
        body = re.search(r'sMoltresLevelUpLearnset\[\]\s*=\s*\{(.*?)\};', source, re.S)[1]
        expected = [dict(level=int(level), move=dict(value='pret:' + move)) for level, move in
                    re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)', body)]
        self.assertEqual(definition['levelUpMoves'], expected)
        self.assertEqual([row['move']['value'] for row in expected if row['level'] <= 50][-4:],
                         ['pret:MOVE_' + move for move in ('FIRE_SPIN', 'AGILITY', 'ENDURE', 'FLAMETHROWER')])
        breeding = compiler.compile_breeding(ROOT, self.policy, self.canonical)
        row = next(row for row in breeding['species'] if row['species']['value'] == identity)
        self.assertEqual(row, dict(species=dict(value=identity), baseEggSpecies=dict(value=identity), eggMoves=[]))

    def test_moltres_machine_compatibility_matches_every_source_slot(self):
        source = (ROOT / 'content/raw/pokefirered/037335f4/src/data/pokemon/tmhm_learnsets.h').read_text()
        body = re.search(r'\[SPECIES_MOLTRES\]\s*=\s*TMHM_LEARNSET\((.*?)\),', source, re.S)[1]
        expected = {'pret:ITEM_' + machine for machine in re.findall(r'TMHM\(([TH]M\d\d)_\w+\)', body)}
        catalog = json.loads((ROOT / 'content/generated/data/machines.json').read_text())
        row = next(row for row in catalog['compatibility'] if row['species']['value'] == 'pret:SPECIES_MOLTRES')
        self.assertEqual(146, row['sourceSpeciesId'])
        self.assertEqual(expected, {item['value'] for item in row['compatibleItems']})
        self.assertIn('pret:ITEM_HM02', expected)
        self.assertIn('pret:ITEM_HM06', expected)
        self.assertNotIn('pret:ITEM_HM03', expected)
        self.assertNotIn('pret:ITEM_HM08', expected)

if __name__ == '__main__': unittest.main()
