import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import importlib.util
import json
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
        self.assertEqual([len(self.policy[k]) for k in ('owned', 'opponents', 'trainerTowerOpponents')], [189, 212, 204])

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
        self.assertEqual(len(data['species']), 189)
        self.assertEqual(len(data['allMachineMoves']), 58)
        self.assertEqual(data['allMachineMoves'][-1]['value'], 'pret:MOVE_DIVE')
        self.assertTrue(all(set(row) == {'species','baseEggSpecies','eggMoves'} for row in data['species']))

if __name__ == '__main__': unittest.main()
