#!/usr/bin/env python3
"""Source-pixel oracle for the shared overworld Itemfinder sheet."""
import sys
from pathlib import Path
import unittest
from PIL import Image
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from image_io import indexed_png

class ItemfinderPixels(unittest.TestCase):
    def test_source_indices_use_player_palette_zero(self):
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        w, h, indices, _ = indexed_png((raw / 'graphics/itemfinder/spr_tiles.png').read_bytes())
        self.assertEqual((16, 80), (w, h))
        rows = (raw / 'graphics/object_events/palettes/player.pal').read_text().splitlines()[3:]
        colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in row.split()) for row in rows]
        expected = bytes(c for index in indices for c in (*colors[index], 0 if index == 0 else 255))
        with Image.open(ROOT / 'content/generated/sprites/overworld/itemfinder.png') as image:
            self.assertEqual((w, h), image.size)
            self.assertEqual(expected, image.convert('RGBA').tobytes())

if __name__ == '__main__':
    unittest.main()
