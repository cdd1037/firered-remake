#!/usr/bin/env python3
"""Small regressions for offline PNG index and transparent-color semantics."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import io
import unittest
import random
from PIL import Image
from image_io import indexed_png, png


class ImageIoTests(unittest.TestCase):
    def test_palette_indices_survive_duplicate_palette_colors(self):
        for bits in (4, 8):
            values = bytes(i % (16 if bits == 4 else 32) for i in range(64))
            image = Image.frombytes('P', (8, 8), values)
            palette = [37, 73, 109] * (16 if bits == 4 else 256)
            image.putpalette(palette)
            buffer = io.BytesIO()
            image.save(buffer, format='PNG', bits=bits, transparency=0)
            width, height, indices, colors = indexed_png(buffer.getvalue())
            self.assertEqual((width, height), (8, 8))
            self.assertEqual(indices, bytes(i & 15 for i in values))
            self.assertTrue(all(color == (37, 73, 109) for color in colors))

    def test_rgba_encoder_keeps_transparent_rgb_and_partial_alpha(self):
        pixels = bytes([13, 27, 39, 0, 71, 91, 113, 127, 151, 173, 199, 255] * 64)
        with Image.open(io.BytesIO(png(24, 8, pixels))) as image:
            self.assertEqual(image.size, (24, 8))
            self.assertEqual(image.convert('RGBA').tobytes(), pixels)

    def test_encoder_uses_exact_palette_and_falls_back_above_256_colors(self):
        for count in (16, 256, 257):
            colors = [(i % 256, i // 256, 39, i % 256) for i in range(count)]
            pixels = bytes(channel for color in colors * 64 for channel in color)
            with Image.open(io.BytesIO(png(count, 64, pixels))) as image:
                self.assertEqual(image.convert('RGBA').tobytes(), pixels)
                if count > 256: self.assertEqual(image.mode, 'RGBA')
                else: self.assertIn(image.mode, ('P', 'RGBA'))

    def test_palette_path_keeps_same_rgb_with_different_alpha(self):
        colors = [(13, 27, 39, alpha) for alpha in (0, 31, 127, 255)]
        rng = random.Random(42)
        pixels = bytes(channel for _ in range(4096) for channel in rng.choice(colors))
        with Image.open(io.BytesIO(png(64, 64, pixels))) as image:
            self.assertEqual(image.mode, 'P')
            self.assertEqual(image.convert('RGBA').tobytes(), pixels)

    def test_indexed_input_requires_palette_and_tile_geometry(self):
        for mode, size in [('RGB', (8, 8)), ('P', (9, 8))]:
            image = Image.new(mode, size)
            buffer = io.BytesIO(); image.save(buffer, format='PNG')
            with self.assertRaises(ValueError): indexed_png(buffer.getvalue())


if __name__ == '__main__': unittest.main()
