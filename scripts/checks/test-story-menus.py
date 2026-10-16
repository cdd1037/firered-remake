#!/usr/bin/env python3
"""Static menu import boundaries and the current raw data, with no compiler invocation."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from story_menus import extract_menus


class StoryMenuImportTests(unittest.TestCase):
    def test_current_raw_preserves_grid_and_reads_tower_options(self):
        rows = extract_menus(ROOT / 'content/raw/pokefirered/037335f4')
        old = json.loads((ROOT / 'content/generated/ui/grid-menus.json').read_text())['menus']
        self.assertEqual(rows[0], old[0])
        self.assertEqual([o['text'] for o in rows[1]['options']], ['YES', 'NO', 'INFO'])
        self.assertEqual([o['text'] for o in rows[2]['options']], ['SINGLE', 'DOUBLE', 'KNOCKOUT', 'MIXED', 'EXIT'])
        self.assertEqual(len({r['sourceId'] for r in rows}), len(rows))

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
