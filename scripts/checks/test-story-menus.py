#!/usr/bin/env python3
"""Static menu import boundaries and the current raw data, with no compiler invocation."""
import json
import shutil
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from story_menus import extract_menus, extract_list_menus


class StoryMenuImportTests(unittest.TestCase):
    def test_current_raw_preserves_grid_and_reads_tower_options(self):
        rows = extract_menus(ROOT / 'content/raw/pokefirered/037335f4')
        old = json.loads((ROOT / 'content/generated/ui/grid-menus.json').read_text())['menus']
        self.assertEqual(rows[0], old[0])
        self.assertEqual([o['text'] for o in rows[1]['options']], ['YES', 'NO', 'INFO'])
        self.assertEqual([o['text'] for o in rows[2]['options']], ['SINGLE', 'DOUBLE', 'KNOCKOUT', 'MIXED', 'EXIT'])
        profile = next(row for row in rows if row['id'] == 'MULTICHOICE_YES_NO_INFO_2')
        self.assertEqual(profile['sourceId'], 18)
        self.assertEqual([o['text'] for o in profile['options']], ['YES', 'NO', 'INFO'])
        self.assertEqual(len({r['sourceId'] for r in rows}), len(rows))

    def test_badge_list_is_source_ordered_four_row_suspendable_menu(self):
        menu, = extract_list_menus(ROOT / 'content/raw/pokefirered/037335f4', ('LISTMENU_BADGES',))
        self.assertEqual(menu['id'], 'LISTMENU_BADGES')
        self.assertEqual(menu['sourceId'], 0)
        self.assertEqual([o['text'] for o in menu['options']],
                         ['BOULDERBADGE', 'CASCADEBADGE', 'THUNDERBADGE', 'RAINBOWBADGE',
                          'SOULBADGE', 'MARSHBADGE', 'VOLCANOBADGE', 'EARTHBADGE', 'EXIT'])
        self.assertEqual(menu['listLayout'], dict(left=1, top=1, visibleRows=4, heightTiles=7, keepOpen=True))
        installed = json.loads((ROOT / 'content/generated/data/story.json').read_text())['Compilation']['Menus']['LISTMENU_BADGES']
        self.assertEqual(installed['ListLayout']['VisibleRows'], menu['listLayout']['visibleRows'])
        self.assertEqual([o['Text'] for o in installed['Options']], [o['text'] for o in menu['options']])

    def test_powder_list_preserves_source_prices_controls_and_seven_rows(self):
        menu, = extract_list_menus(ROOT / 'content/raw/pokefirered/037335f4', ('LISTMENU_BERRY_POWDER',))
        self.assertEqual(menu['sourceId'], 5)
        self.assertEqual(menu['listLayout'], dict(left=16, top=1, visibleRows=7, heightTiles=12, keepOpen=False))
        self.assertEqual(len(menu['options']), 12)
        self.assertEqual(menu['options'][-1]['text'], 'EXIT')
        self.assertEqual([o['text'].split('{FONT_SMALL}')[-1] for o in menu['options'][:-1]],
                         ['50', '80', '50', '300', '1,000', '1,000', '1,000', '1,000', '1,000', '1,000', '3,000'])
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        with tempfile.TemporaryDirectory() as name:
            alternative = Path(name)
            for relative in ('src/field_specials.c', 'src/strings.c', 'include/constants/menu.h', 'charmap.txt', 'src/text.c'):
                target = alternative / relative; target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(raw / relative, target)
            strings = alternative / 'src/strings.c'
            strings.write_text(strings.read_text().replace(menu['options'][0]['text'], 'CUSTOM{CLEAR_TO 0x20}{FONT_SMALL}17'))
            changed, = extract_list_menus(alternative, ('LISTMENU_BERRY_POWDER',))
            self.assertEqual(changed['options'][0]['text'], 'CUSTOM{CLEAR_TO 0x20}{FONT_SMALL}17')

    def test_powder_gift_keeps_source_flag_before_capacity_and_no_local_earning(self):
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        source = (raw / 'data/maps/CeruleanCity_House5/scripts.inc').read_text()
        self.assertLess(source.index('setflag FLAG_GOT_POWDER_JAR'), source.index('giveitem ITEM_POWDER_JAR'))
        self.assertIn('goto_if_eq VAR_RESULT, FALSE, EventScript_BagIsFull', source)
        self.assertNotIn('Script_GiveBerryPowder', source)

    def test_empty_raw_fails(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(FileNotFoundError):
                extract_menus(Path(root))

    def test_missing_and_duplicate_selection_fail(self):
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        for ids in [(), ('MISSING_MENU',), ('MULTICHOICE_YES_NO_INFO',) * 2]:
            with self.assertRaises(ValueError):
                extract_menus(raw, ids)


if __name__ == '__main__':
    unittest.main()
