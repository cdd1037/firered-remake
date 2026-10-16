#!/usr/bin/env python3
"""Exact native source/destination SELECT cursor extraction; no renderer-authored substitute."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("battle_move_order_cursors", ROOT / "scripts/imports/presentation/import-ui-corrections.py")
IMPORTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(IMPORTER)

class BattleMoveOrderCursorTests(unittest.TestCase):
    def setUp(self):
        self.lock = json.loads((ROOT / "scripts/recipes/presentation/ui-corrections.lock.json").read_bytes())
        raw = ROOT / "content/raw/pokefirered/037335f4"
        self.sources = {path: (raw / path).read_bytes() for path in IMPORTER.PATHS}

    def test_exact_native_cursor_tiles_and_existing_assets(self):
        outputs = IMPORTER.convert(self.lock, self.sources)
        tiles = IMPORTER.codec.tile_list(self.sources["graphics/battle_interface/textbox.png"])
        palette = IMPORTER.codec.palette(self.sources["graphics/battle_interface/textbox1.pal"])
        for name, first in [("battle-selection-cursor", 1), ("battle-swap-source-cursor", 30), ("battle-swap-destination-cursor", 28)]:
            expected = IMPORTER.codec.png(8, 16, IMPORTER.codec.rgba(tiles[first] + tiles[first + 1], palette))
            self.assertEqual(expected, outputs[name + ".png"])
        self.assertIn("pc-submenu-background.png", outputs)
        self.assertEqual(4, len(json.loads(outputs["ui-corrections.json"])["assets"]))

    def test_changed_source_cursor_contract_rejects_import(self):
        self.sources["src/battle_controller_player.c"] = self.sources["src/battle_controller_player.c"].replace(
            b"MoveSelectionCreateCursorAt(gMultiUsePlayerCursor, 27);", b"MoveSelectionCreateCursorAt(gMultiUsePlayerCursor, 25);")
        with self.assertRaises(ValueError):
            IMPORTER.convert(self.lock, self.sources)

if __name__ == "__main__":
    unittest.main()
