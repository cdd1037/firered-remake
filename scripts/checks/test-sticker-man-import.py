#!/usr/bin/env python3
"""Pinned Sticker Man source-to-generated contracts and story selection checks."""
import importlib.util
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from story_menus import extract_menus


def module(name, relative):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


class StickerManImportTests(unittest.TestCase):
    def test_source_milestone_metadata(self):
        importer = module('sticker_region', 'scripts/lib/icefall_features.py')
        class Source:
            def text(self, path): return (RAW / path).read_text()
        actual = importer.sticker_milestones(Source())
        expected = json.loads((ROOT / 'content/generated/maps/regions/four-island/contracts.json').read_text())['stickerMilestones']
        self.assertEqual(actual, expected)
        self.assertEqual([r['minimumCounts'] for r in actual['categories']],
                         [[1, 40, 100, 200], [1, 100, 200, 300], [1, 20, 50, 100]])

    def test_source_selection_preserves_lines_and_printer_is_separately_enabled(self):
        importer = module('sticker_story', 'scripts/imports/story/import-story.py')
        entry = {'path': 'data/scripts/trainer_card.inc', 'labelPrefixes': ['FourIsland_House2_']}
        original = (RAW / entry['path']).read_text().splitlines()
        selected = importer.selected_source(entry).splitlines()
        self.assertEqual(len(original), len(selected))
        start = original.index('FourIsland_House2_EventScript_StickerMan::')
        self.assertEqual(selected[start:], original[start:])
        self.assertFalse(any(line.strip() for line in selected[:start]))
        compiled = json.loads((ROOT / 'content/generated/data/story.json').read_text())['Compilation']
        programs = compiled['Programs']
        self.assertIn('FourIsland_House2_EventScript_StickerMan', programs)
        self.assertIn('CeladonCity_GameCorner_EventScript_PhotoPrinter', programs)
        self.assertFalse([d for d in compiled['UnsupportedInstructions'] if 'trainer_card.inc' in str(d)])

    def test_all_source_brag_menus(self):
        rows = {r['id']: r for r in extract_menus(RAW)}
        self.assertEqual([o['text'] for o in rows['MULTICHOICE_HOF_EGGS_VICTORIES_QUIT']['options']],
                         ['HALL OF FAME', 'EGGS', 'VICTORIES', 'QUIT'])
        for name in ['HOF', 'EGGS', 'VICTORIES', 'HOF_EGGS', 'HOF_VICTORIES', 'EGGS_VICTORIES', 'HOF_EGGS_VICTORIES']:
            self.assertEqual(rows['MULTICHOICE_' + name + '_QUIT']['options'][-1]['text'], 'QUIT')


if __name__ == '__main__':
    unittest.main()
