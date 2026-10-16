#!/usr/bin/env python3
"""Source-based checks for the current item admission and stock compiler."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import copy
import importlib.util
import json
from pathlib import Path
import unittest
from item_catalog import ROOT, RAW, item_json
from item_effects import read_effects

spec = importlib.util.spec_from_file_location('current_items', (_PROJECT_ROOT / 'scripts/imports/items/import-current-items.py'))
compiler = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compiler)


class CurrentItemTests(unittest.TestCase):
    def test_current_output_is_reproducible_and_bounded(self):
        catalog = json.loads(json.dumps(compiler.compile_catalog(ROOT), default=item_json))
        self.assertEqual(catalog, json.loads((ROOT / compiler.TARGET).read_text()))
        self.assertEqual(len(compiler.read_recipe(ROOT)['items']), len(catalog['items']))
        self.assertEqual(14, len(catalog['shops']))
        self.assertEqual('pret:ITEM_POKE_BALL', catalog['items'][0]['item'])
        self.assertEqual(99, catalog['maximumTransactionQuantity'])
        self.assertEqual(999999, catalog['maximumMoney'])
        self.assertEqual({'limits', 'questLimits', 'maximumTransactionQuantity', 'maximumMoney', 'items', 'shops'}, set(catalog))
        self.assertNotIn('pret:ITEM_HM08', {item['item'] for item in catalog['items']})
        self.assertEqual(['pret:ITEM_POKE_BALL', 'pret:ITEM_POTION', 'pret:ITEM_ANTIDOTE', 'pret:ITEM_PARALYZE_HEAL'],
                         [item['value'] for item in catalog['shops'][0]['items']])
        effects = read_effects(ROOT / RAW)
        for item in catalog['items']:
            if item['effectBytes']:
                self.assertEqual(effects[item['item'].removeprefix('pret:')], item['effectBytes'])
            self.assertEqual({'item', 'description', 'healHp', 'cureStatus', 'effectBytes', 'useSupport'}, set(item))
        self.assertTrue(all(set(shop) == {'id', 'items'} for shop in catalog['shops']))

    def test_current_item_icons_match_source_pixels_after_atlas_packing(self):
        selected = {item['item'] for item in compiler.read_recipe(ROOT)['items'] if item.get('icon')}
        self.assertTrue(selected)
        self.assertEqual(len(selected), compiler.import_icons(ROOT, check=True))
        icons = json.loads((ROOT / 'content/generated/data/items/current-icons.json').read_text())['icons']
        self.assertEqual(selected, {icon['item']['value'] for icon in icons})
        self.assertTrue({'pret:ITEM_AMULET_COIN', 'pret:ITEM_EXP_SHARE'} <= selected)
        for icon in icons:
            self.assertEqual((24, 24), (icon['width'], icon['height']))

    def test_explicit_admission_has_no_historical_count_gate(self):
        recipe = copy.deepcopy(compiler.read_recipe(ROOT))
        recipe['shops'] = recipe['shops'][:1]
        recipe['items'] = recipe['items'][:4]
        catalog = compiler.compile_catalog(ROOT, recipe)
        self.assertEqual(4, len(catalog['items']))
        self.assertEqual(1, len(catalog['shops']))
        recipe['items'] = []
        with self.assertRaisesRegex(ValueError, 'Invalid stock admission'):
            compiler.compile_catalog(ROOT, recipe)

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
                                   ({'price': 1}, 'entry schema'), ({'icon': 'true'}, 'Invalid icon admission')]:
            recipe = copy.deepcopy(original)
            recipe['items'][1].update(replacement)
            with self.subTest(replacement=replacement), self.assertRaisesRegex(ValueError, error):
                compiler.compile_catalog(ROOT, recipe)


if __name__ == '__main__':
    unittest.main()
