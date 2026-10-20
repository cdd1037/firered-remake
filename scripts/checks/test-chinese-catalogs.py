"""Focused pinned-source catalog extraction tests; no project writes or builds."""
import copy
import json
import os
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "lib"))
from unittest.mock import patch

import chinese_catalogs as catalogs


ROOT = Path(os.environ.get('FIRERED_PROJECT_ROOT',
                          str(Path(__file__).resolve().parents[2])))
ZH = ROOT / 'content/raw/PretFRLG/a04f6317'


class ParserTests(unittest.TestCase):
    def test_selects_named_chinese_array_after_english_array(self):
        text = '''
        const u8 gSpeciesNamesEnglish[][10] = {
            [SPECIES_BULBASAUR] = _("BULBASAUR"),
        };
        const u8 gSpeciesNames[][10] = {
            [SPECIES_BULBASAUR] = _("妙蛙种子"),
        };
        '''
        self.assertEqual(catalogs._names(text, 'gSpeciesNames', 'SPECIES_'),
                         {'SPECIES_BULBASAUR': '妙蛙种子'})

    def test_multiline_literals_preserve_source_controls_and_comment_markers(self):
        text = r'''
        // ignored declaration: const u8 fake[] = _("bad");
        const u8 gText[] = _("中文\\路径\n" "{PLAYER}/*literal*/");
        '''
        self.assertEqual(catalogs._declarations(text),
                         {'gText': r'中文\\路径\n{PLAYER}/*literal*/'})

    def test_duplicate_declarations_are_not_silently_overwritten(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate text declaration'):
            catalogs._declarations('const u8 gText[] = _("甲"); const u8 gText[] = _("乙");')

    def test_nested_structs_and_control_braces_are_safe(self):
        text = '''
        const struct Trainer gTrainers[] = {
            [TRAINER_BEN] = {
                .trainerName = _("{PLAYER}小明"),
                .items = {{1, 2}, {3, 4}},
            },
        };
        '''
        rows = catalogs._structs(text, 'gTrainers', 'TRAINER_')
        self.assertEqual(catalogs._field_literal(rows['TRAINER_BEN'], 'trainerName'), '{PLAYER}小明')

    def test_help_transform_uses_existing_glyph_and_line_rules(self):
        self.assertEqual(catalogs._help_text(r'{CIRCLE_1}{PLUS}{PLAYER}\n下页\p结束$'),
                         '1+{PLAYER}\n下页\n结束')

    def test_opening_preserves_page_breaks_as_form_feed(self):
        self.assertEqual(catalogs._opening_text(r'你好{PLAYER}\l继续\p新页$'),
                         '你好{PLAYER}\n继续\f新页')


class CatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.result = catalogs.collect_catalogs(ROOT, ZH)

    def test_groups_are_sparse_subsets_of_actual_original_keys(self):
        result = self.result
        self.assertEqual(set(result['groups']), set(result['originals']))
        self.assertTrue(result['groups'])
        for group, values in result['groups'].items():
            with self.subTest(group=group):
                self.assertLessEqual(set(values), set(result['originals'][group]))
                self.assertTrue(all(catalogs.HAN.search(value) for value in values.values()))
                report = result['coverage'][group]
                bins = [report[key] for key in (
                    'translated', 'unchanged', 'missing', 'empty', 'changedWithoutHan')]
                flat = [key for bucket in bins for key in bucket]
                self.assertEqual(len(flat), len(set(flat)))
                self.assertEqual(set(flat), set(result['originals'][group]))
                self.assertEqual(set(report['translated']), set(values))

    def test_species_use_chinese_array_and_source_numeric_identity(self):
        self.assertEqual(self.result['groups']['species-names']['1'], '妙蛙种子')
        self.assertEqual(self.result['references']['species-names']['1']['symbol'], 'gSpeciesNames')
        self.assertEqual(self.result['references']['species-names']['1']['field'], 'SPECIES_BULBASAUR')

    def test_shared_item_and_machine_description_selection_matches_recipe(self):
        refs = self.result['references']
        self.assertEqual(refs['item-descriptions']['pret:ITEM_TM36']['path'], 'src/move_descriptions.c')
        self.assertEqual(refs['machine-descriptions']['pret:ITEM_TM36']['field'], 'description_english')
        self.assertEqual(refs['machine-descriptions']['pret:ITEM_TM01']['path'], 'src/move_descriptions.c')
        self.assertEqual(refs['machine-descriptions']['pret:ITEM_TM_CASE']['field'], 'description_english')

    def test_source_escapes_remain_raw_for_core_normalizer(self):
        value = self.result['groups']['species-descriptions']['1']
        self.assertIn(r'\n', value)
        self.assertNotIn('\n', value)
        self.assertIn('\n', self.result['originals']['species-descriptions']['1'])

    def test_untranslated_easy_chat_and_tower_are_explicit_gaps(self):
        for group in ('easy-chat-words', 'trainer-tower-names'):
            self.assertEqual(self.result['groups'][group], {})
            report = self.result['coverage'][group]
            self.assertGreater(report['total'], 0)
            self.assertEqual(report['total'], len(report['unchanged']))
        self.assertIn('EC_GROUP_TRAINER', self.result['groups']['easy-chat-groups'])

    def test_pounds_are_not_relabeled_as_kilograms(self):
        self.assertNotIn('gText_Lbs', self.result['groups']['pokedex-ui'])
        self.assertIn('gText_Lbs', self.result['coverage']['pokedex-ui']['changedWithoutHan'])

    def test_chinese_gameplay_fields_never_control_description_join(self):
        original_text = catalogs._Source.text

        def changed_text(source, path):
            text = original_text(source, path)
            if source.root == ZH and path == 'src/data/trainers.h':
                return text.replace('TRAINER_CLASS_YOUNGSTER', 'TRAINER_CLASS_NOT_A_RULE')
            return text

        original_json = catalogs._json

        def changed_json(path):
            result = original_json(path)
            if path == ZH / 'src/data/items.json':
                result = copy.deepcopy(result)
                for row in result['items']:
                    if 'moveId' in row:
                        row['moveId'] = 'NotARealMove'
                    row['price'] = -987654
            return result

        with patch.object(catalogs._Source, 'text', changed_text), patch.object(catalogs, '_json', changed_json):
            changed = catalogs.collect_catalogs(ROOT, ZH)
        self.assertEqual(changed['groups'], self.result['groups'])

    def test_duplicate_selected_item_id_fails_closed(self):
        original_json = catalogs._json

        def duplicate(path):
            result = original_json(path)
            if path == ZH / 'src/data/items.json':
                result = copy.deepcopy(result)
                result['items'].append(next(row for row in result['items'] if row['itemId'] == 'ITEM_POTION'))
            return result

        with patch.object(catalogs, '_json', duplicate):
            with self.assertRaisesRegex(ValueError, 'Duplicate selected itemId'):
                catalogs.collect_catalogs(ROOT, ZH)

    def test_chinese_pointer_order_does_not_reassign_natures(self):
        original_text = catalogs._Source.text

        def reversed_table(source, path):
            text = original_text(source, path)
            if source.root == ZH and path == 'src/data/text/nature_names.h':
                body = catalogs._table(text, 'gNatureNamePointers')
                return text.replace(body, '\n'.join(reversed(body.splitlines())))
            return text

        with patch.object(catalogs._Source, 'text', reversed_table):
            changed = catalogs.collect_catalogs(ROOT, ZH)
        self.assertEqual(changed['groups']['nature-names'], self.result['groups']['nature-names'])


class UiGroupTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # In the product this module lives beside chinese_catalogs. The staged
        # check imports the independently owned shared scanner from its stage.
        from source_text_collector import collect_declarations
        cls.zh = collect_declarations(ZH)
        cls.baseline = collect_declarations(ROOT / 'content/raw/pokefirered/037335f4')
        cls.result = catalogs.collect_ui_groups(ROOT, ZH, cls.zh)

    def test_group_inventory_and_reachable_help_selection(self):
        self.assertEqual(set(self.result['groups']), {
            'help', 'opening', 'opening-names', 'teachy-tv', 'quest-log',
            'quest-log-map-names', 'quest-log-departures', 'story-menus',
        })
        help_doc = json.loads((ROOT / 'content/generated/ui/help/help.json').read_text())
        self.assertLess(len(self.result['originals']['help']), len(help_doc['texts']))
        for group, values in self.result['groups'].items():
            self.assertLessEqual(set(values), set(self.result['originals'][group]))
            self.assertEqual([], self.result['coverage'][group]['missing'])

    def test_baseline_declared_text_matches_existing_producer_transform(self):
        transforms = {
            'help': catalogs._help_text,
            'opening': catalogs._opening_text,
            'opening-names': catalogs._opening_text,
            'teachy-tv': lambda value: value,
            'quest-log': catalogs._decode,
            'story-menus': lambda value: value,
        }
        for group, transform in transforms.items():
            for key, expected in self.result['originals'][group].items():
                with self.subTest(group=group, key=key):
                    symbol = self.result['references'][group][key]['symbol']
                    self.assertEqual(transform(self.baseline.texts[symbol]), expected)

    def test_map_labels_do_not_join_by_english_text(self):
        original_json = catalogs._json

        def relabel(path):
            result = original_json(path)
            if path == ROOT / 'content/generated/data/quest-log.json':
                result = copy.deepcopy(result)
                result['mapNames'] = {key: 'ANY DISPLAY LABEL' for key in result['mapNames']}
                for row in result['departures']:
                    row['place'] = 'ANY PLACE LABEL'
                    row['regionName'] = 'ANY REGION LABEL'
            return result

        with patch.object(catalogs, '_json', relabel):
            changed = catalogs.collect_ui_groups(ROOT, ZH, self.zh)
        for group in ('quest-log-map-names', 'quest-log-departures'):
            self.assertEqual(changed['groups'][group], self.result['groups'][group])

    def test_preset_names_return_candidates_without_claiming_encoding_acceptance(self):
        candidates = self.result['groups']['opening-names']
        self.assertEqual(set(candidates), set(self.result['originals']['opening-names']))
        self.assertTrue(all(catalogs.HAN.search(value) for value in candidates.values()))


if __name__ == '__main__':
    unittest.main()
