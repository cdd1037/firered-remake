#!/usr/bin/env python3
"""Source-based checks for the current item admission and stock compiler."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest
from item_catalog import ROOT, RAW, item_json
from item_effects import read_effects

spec = importlib.util.spec_from_file_location('current_items', Path(__file__).with_name('import-current-items.py'))
compiler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compiler)


class CurrentItemTests(unittest.TestCase):
    def test_current_output_is_reproducible_and_bounded(self):
        catalog = json.loads(json.dumps(compiler.compile_catalog(ROOT), default=item_json))
        self.assertEqual(catalog, json.loads((ROOT / compiler.TARGET).read_text()))
        self.assertEqual(151, len(catalog['items']))
        self.assertEqual(14, len(catalog['shops']))
        self.assertEqual('pret:ITEM_POKE_BALL', catalog['items'][0]['item'])
        self.assertEqual(99, catalog['maximumTransactionQuantity'])
        self.assertEqual(999999, catalog['maximumMoney'])
        self.assertNotIn('pret:ITEM_HM08', {item['item'] for item in catalog['items']})
        self.assertEqual(['pret:ITEM_POKE_BALL', 'pret:ITEM_POTION', 'pret:ITEM_ANTIDOTE', 'pret:ITEM_PARALYZE_HEAL'],
                         [item['value'] for item in catalog['shops'][0]['items']])
        effects = read_effects(ROOT / RAW)
        for item in catalog['items']:
            if item['effectBytes']:
                self.assertEqual(effects[item['item'].removeprefix('pret:')], item['effectBytes'])
            self.assertEqual({'item', 'description', 'healHp', 'cureStatus', 'effectBytes', 'useSupport'}, set(item))
        self.assertTrue(all(set(shop) == {'id', 'items'} for shop in catalog['shops']))

    def test_admission_retains_deliberately_unimplemented_use(self):
        items = {item['item'].identity: item for item in compiler.compile_catalog(ROOT)['items']}
        self.assertEqual('noFieldUse', items['pret:ITEM_FULL_RESTORE']['useSupport'])
        self.assertEqual([], items['pret:ITEM_FULL_RESTORE']['effectBytes'])
        self.assertEqual('deferredSleep', items['pret:ITEM_AWAKENING']['useSupport'])
        self.assertEqual([0, 0, 0, 32, 0, 0], items['pret:ITEM_AWAKENING']['effectBytes'])
        self.assertEqual(20, items['pret:ITEM_POTION']['healHp'])
        self.assertEqual(64, items['pret:ITEM_PARALYZE_HEAL']['cureStatus'])

    def test_recipe_rejects_duplicates_unknown_business_and_use(self):
        original = compiler.read_recipe(ROOT)
        for replacement, error in [({'item': original['items'][0]['item']}, 'Duplicate current item'),
                                   ({'item': 'pret:ITEM_NONE'}, 'Unknown item'),
                                   ({'useSupport': 'inventedUse'}, 'Unknown item use'),
                                   ({'price': 1}, 'entry schema')]:
            recipe = copy.deepcopy(original)
            recipe['items'][1].update(replacement)
            with self.subTest(replacement=replacement), self.assertRaisesRegex(ValueError, error):
                compiler.compile_catalog(ROOT, recipe)


if __name__ == '__main__':
    unittest.main()
