#!/usr/bin/env python3
"""Synthetic, offline regressions for importer palette roles and frame geometry."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import io
from pathlib import Path
import unittest

from PIL import Image, PngImagePlugin

ROOT = _PROJECT_ROOT


def importer(name):
    spec = importlib.util.spec_from_file_location(name.replace('-', '_'), ROOT / 'scripts' / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


STARTER = importer('imports/presentation/import-starter-art')
PLAYER = importer('imports/presentation/import-player-gender-art')
CYCLE = importer('imports/presentation/import-city-cycle-art')
FONT = importer('imports/presentation/import-source-font')
CAPTURE = importer('imports/pokemon/import-capture-dex')
FOREST = importer('imports/pokemon/import-forest-capture-dex')
FONT_PALETTE = [144, 200, 255, 56, 56, 56, 216, 216, 216, 255, 255, 255]
# Equal RGB values at different indices catch accidental color-based reconstruction.
SPRITE_PALETTE = [37, 53, 91] * 16


def indexed_png(size, pixels, palette, bits, transparency=None):
    image = Image.frombytes('P', size, pixels)
    image.putpalette(palette)
    metadata = PngImagePlugin.PngInfo()
    metadata.add_text('Description', 'Synthetic semantic regression')
    output = io.BytesIO()
    options = {'transparency': transparency} if transparency is not None else {}
    image.save(output, format='PNG', bits=bits, pnginfo=metadata, **options)
    return output.getvalue()


def footprint_png(pixels):
    image = Image.frombytes('L', (16, 16), pixels).convert('1')
    output = io.BytesIO()
    image.save(output, format='PNG')
    return output.getvalue()


class ImporterPngSemanticsTests(unittest.TestCase):
    def test_indexed_sheets_keep_indices_and_frame_geometry(self):
        for module, size, args in (
            (STARTER, (64, 64), ()), (PLAYER, (144, 32), ()),
            (CYCLE, (288, 32), (288,)), (CYCLE, (384, 32), (384,)),
        ):
            with self.subTest(importer=module.__name__, size=size):
                expected = bytes((x // 8 + y * 3) % 16 for y in range(size[1]) for x in range(size[0]))
                encoded = indexed_png(size, expected, SPRITE_PALETTE, 4, 0)
                self.assertEqual(expected, module.indexed_png(encoded, *args))
                wrong_size = indexed_png((size[0], 16), expected[:size[0] * 16], SPRITE_PALETTE, 4, 0)
                with self.assertRaises(ValueError):
                    module.indexed_png(wrong_size, *args)

    def test_sprite_source_transparency_is_palette_index_zero(self):
        for module, size, args in ((STARTER, (64, 64), ()), (PLAYER, (144, 32), ()), (CYCLE, (288, 32), ())):
            with self.subTest(importer=module.__name__):
                encoded = indexed_png(size, bytes(size[0] * size[1]), SPRITE_PALETTE, 4, 1)
                with self.assertRaises(ValueError):
                    module.indexed_png(encoded, *args)
        indices = bytes(range(16)) * 256
        colors = [(i * 7, i * 11, i * 13) for i in range(16)]
        pixels = STARTER.rgba_pixels(indices, colors)
        self.assertEqual(bytes(c for i in indices for c in (*colors[i], 0 if i == 0 else 255)), pixels)

    def test_font_roles_keep_all_four_indices_and_both_atlas_sizes(self):
        for height in (256, 512):
            with self.subTest(height=height):
                expected = bytes((x // 8 + y // 16) % 4 for y in range(height) for x in range(256))
                encoded = indexed_png((256, height), expected, FONT_PALETTE, 2)
                self.assertEqual(expected, FONT.decode_indices(encoded, height))
                with self.assertRaises(ValueError):
                    FONT.decode_indices(encoded, 256 if height == 512 else 512)

    def test_font_rejects_changed_role_palette(self):
        palette = FONT_PALETTE.copy()
        palette[0] += 1
        encoded = indexed_png((256, 256), bytes(256 * 256), palette, 2)
        with self.assertRaises(ValueError):
            FONT.decode_indices(encoded, 256)

    def test_footprints_invert_black_and_white_without_reordering(self):
        pixels = bytes(0 if (x + y * 5) % 7 < 3 else 255 for y in range(16) for x in range(16))
        expected = bytes(1 if pixel == 0 else 0 for pixel in pixels)
        encoded = footprint_png(pixels)
        for module in (CAPTURE, FOREST):
            with self.subTest(importer=module.__name__):
                self.assertEqual(expected, module.footprint_indices(encoded))

    def test_forest_keeps_empty_footprints_while_first_capture_rejects_them(self):
        for white in (0, 255):
            with self.subTest(pixel=white):
                encoded = footprint_png(bytes([white]) * 256)
                self.assertEqual(bytes([1 if white == 0 else 0]) * 256, FOREST.footprint_indices(encoded))
                with self.assertRaisesRegex(ValueError, 'Empty footprint'):
                    CAPTURE.footprint_indices(encoded)

    def test_footprints_require_one_bit_grayscale(self):
        encoded = indexed_png((16, 16), bytes(256), SPRITE_PALETTE, 4, 0)
        for module in (CAPTURE, FOREST):
            with self.subTest(importer=module.__name__):
                with self.assertRaises(ValueError):
                    module.footprint_indices(encoded)

    def test_encoder_wrappers_keep_dimensions_and_all_rgba_channels(self):
        for encoder, size in (
            (STARTER.rgba_png, (64, 64)), (PLAYER.rgba_png, (144, 32)),
            (CYCLE.rgba_png, (288, 32)), (lambda pixels: CYCLE.rgba_png(pixels, 384), (384, 32)),
            (lambda pixels: FONT.encode_png(pixels, 8, 13), (8, 13)),
        ):
            with self.subTest(size=size):
                pixels = bytes(channel for i in range(size[0] * size[1])
                               for channel in (i % 256, (i * 3) % 256, (i * 7) % 256, 0 if i % 3 == 0 else 255))
                with Image.open(io.BytesIO(encoder(pixels))) as image:
                    self.assertEqual(size, image.size)
                    self.assertIn(image.mode, ('P', 'RGBA'))
                    self.assertEqual(pixels, image.convert('RGBA').tobytes())
                with self.assertRaises(ValueError):
                    encoder(pixels[:-1])


if __name__ == '__main__':
    unittest.main()
