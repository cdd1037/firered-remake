#!/usr/bin/env python3
"""Small regressions for offline PNG index and transparent-color semantics."""
import io
import unittest
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
            self.assertEqual(image.tobytes(), pixels)

    def test_indexed_input_requires_palette_and_tile_geometry(self):
        for mode, size in [('RGB', (8, 8)), ('P', (9, 8))]:
            image = Image.new(mode, size)
            buffer = io.BytesIO(); image.save(buffer, format='PNG')
            with self.assertRaises(ValueError): indexed_png(buffer.getvalue())


if __name__ == '__main__': unittest.main()
