#!/usr/bin/env python3
"""Focused original HELP import parity and source-driven mutation checks."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('help_import', ROOT / 'scripts/imports/presentation/import-help-system.py')
help_import = importlib.util.module_from_spec(spec)
spec.loader.exec_module(help_import)

class ChangedSource:
    def __init__(self, changes):
        self.source = help_import.Source(ROOT)
        self.changes = changes
    def text(self, path):
        value = self.source.text(path)
        for old, new in self.changes.get(path, []):
            if old not in value:
                raise AssertionError('Missing mutation target ' + old)
            value = value.replace(old, new)
        return value

class HelpImportTests(unittest.TestCase):
    def test_complete_original_document_matches_generated(self):
        d = help_import.generate(help_import.Source(ROOT))
        self.assertEqual(d, json.loads((ROOT / help_import.BASE).read_bytes()))
        self.assertEqual(len(d['texts']), 369)
        self.assertEqual(sum(len(t['entries']) for t in d['topics'].values()), 177)
        self.assertEqual(len(d['maps']), 425)
        self.assertIn('Select “AREA”', d['texts']['Help_Text_HowToReadPokedex'])
        for contexts in d['contexts'].values():
            for topic in contexts:
                self.assertEqual(len(topic['entries']), len(set(topic['entries'])))

    def test_flags_and_map_metadata_are_source_driven(self):
        source = ChangedSource({'src/help_system.c': [('return FlagGet(FLAG_GOT_BICYCLE);', 'return FlagGet(FLAG_TEST_HELP);'), ('GetKantoPokedexCount(1) > 1', 'GetKantoPokedexCount(1) > 3')],
            'data/maps/PalletTown/map.json': [('"MAP_TYPE_TOWN"', '"MAP_TYPE_INDOOR"')]})
        d = help_import.generate(source)
        bicycle = next(e for e in d['topics']['TOPIC_HOW_TO_DO']['entries'] if e['id'] == 'HELP_RIDING_BICYCLE')
        self.assertEqual(bicycle['condition'], dict(kind='flag', value='FLAG_TEST_HELP'))
        self.assertEqual(d['maps']['MAP_PALLET_TOWN']['context'], 'HELPCONTEXT_INDOORS')
        switch = next(e for e in d['topics']['TOPIC_HOW_TO_DO']['entries'] if e['id'] == 'HELP_USING_SWITCH')
        self.assertEqual(switch['condition'], dict(kind='caughtAbove', value='3'))

    def test_text_order_and_hm_predicates_are_source_driven(self):
        source = ChangedSource({'data/text/help_system.inc': [('Greetings! This is the HELP System.', 'Original text mutation.')],
            'src/help_system.c': [('FlagGet(FLAG_GOT_HM01)', 'FlagGet(FLAG_TEST_HM)'),
                ('    TOPIC_ABOUT_GAME, \n    TOPIC_WHAT_TO_DO,', '    TOPIC_WHAT_TO_DO, \n    TOPIC_ABOUT_GAME,')]})
        d = help_import.generate(source)
        self.assertTrue(d['texts']['Help_Text_Greetings'].startswith('Original text mutation.'))
        self.assertEqual(d['hmFlags'][0], 'FLAG_TEST_HM')
        self.assertEqual(d['contexts']['HELPCONTEXT_POKECENTER'][0]['id'], 'TOPIC_WHAT_TO_DO')

if __name__ == '__main__':
    unittest.main()
