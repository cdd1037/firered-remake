#!/usr/bin/env python3
"""Raw Tower feature checks: no generated resource is used as a compiler input."""
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from world_source import WorldSource
from pokemon_tower_features import compile_tower


class TowerFeaturesTests(unittest.TestCase):
    def compile(self, edit=None):
        source = WorldSource(ROOT / 'content/raw/pokefirered/037335f4')
        if edit:
            path, old, new = edit
            self.assertIn(old, source.text(path))
            source.data[path] = source.text(path).replace(old, new).encode()
        with tempfile.TemporaryDirectory() as directory:
            result = compile_tower(source, Path(directory))
            self.assertEqual(source.read('graphics/weather/fog_horizontal.png'),
                (Path(directory)/'maps/regions/pokemon-tower/presentation/fog-horizontal-source.png').read_bytes())
        return {Path(path).name: doc for path,doc in result.items()}

    def test_live_gameplay(self):
        docs = self.compile()
        d = docs['dependencies.json']; c = docs['pokemon-tower.json']
        self.assertEqual(['MOVE_HEADBUTT','MOVE_LEER','MOVE_FOCUS_ENERGY','MOVE_BONEMERANG'], d['marowak']['moves'])
        self.assertEqual((30,31,'female','NATURE_SERIOUS'), tuple(d['marowak'][k] for k in ['level','fixedIv','gender','nature']))
        self.assertEqual(5,len(d['wildEncounters']))
        self.assertEqual(10,len(d['pickups']))
        self.assertEqual(['ITEM_SOOTHE_BELL'],[p['item'] for p in d['pickups'] if p.get('event',{}).get('underfoot')])
        self.assertEqual([3,4,5,6,7],c['fog']['floors'])
        self.assertEqual(dict(map='MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE',warpId=255,x=4,y=7),c['dynamicExit']['rescueWarp'])
        self.assertEqual(9,len(docs['mechanics.json']['healing']['purifiedCoordinates']))

    def test_reward_quantity_is_compiled_from_script(self):
        docs = self.compile(('data/maps/LavenderTown_VolunteerPokemonHouse/scripts.inc',
            'ITEM_POKE_FLUTE, 1, MUS_OBTAIN_KEY_ITEM','ITEM_POKE_FLUTE, 2, MUS_OBTAIN_KEY_ITEM'))
        self.assertEqual(2,docs['dependencies.json']['reward']['quantity'])

    def test_rescue_coordinates_are_compiled_from_script(self):
        docs = self.compile(('data/maps/PokemonTower_7F/scripts.inc',
            'warp MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE, 4, 7',
            'warp MAP_LAVENDER_TOWN_VOLUNTEER_POKEMON_HOUSE, 5, 6'))
        self.assertEqual((5,6),tuple(docs['pokemon-tower.json']['dynamicExit']['rescueWarp'][k] for k in ['x','y']))

    def test_missing_heal_implementation_fails(self):
        with self.assertRaisesRegex(ValueError,'Unsupported party healing'):
            self.compile(('src/script_pokemon_util.c','MON_DATA_STATUS','MON_DATA_UNUSED'))


if __name__ == '__main__':
    unittest.main()
