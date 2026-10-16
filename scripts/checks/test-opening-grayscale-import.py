#!/usr/bin/env python3
"""Independent gbagfx grayscale-index regression for every intro L-mode input."""
import importlib.util
import io
import struct
import tempfile
from pathlib import Path
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('intro_import', ROOT / 'scripts/imports/presentation/import-intro-movie.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

def grayscale_inputs(raw):
    result = set()
    for path in raw.rglob('*.png'):
        with Image.open(path) as image:
            if image.mode == 'L':
                result.add(str(path.relative_to(raw)))
    return result

class IntroGrayscaleImportTests(unittest.TestCase):
    def test_every_grayscale_asset_uses_inverted_four_bit_indices(self):
        raw = module.RAW / 'graphics/intro'
        # Explicit output/palette ownership: a newly introduced L-mode input must be covered.
        cases = {'game_freak/game_freak.png': ('gf-name.png', 'game_freak/logo.png')}
        grayscale = grayscale_inputs(raw)
        self.assertEqual(grayscale, set(cases))
        generated = module.generate()
        for source, (output, palette_source) in cases.items():
            with self.subTest(source=source):
                original = Image.open(raw / source)
                actual = Image.open(io.BytesIO(generated[output])).convert('RGBA')
                palette = Image.open(raw / palette_source).getpalette()
                self.assertEqual(original.size, actual.size)
                pixels = list(original.getdata())
                self.assertIn(255, pixels)
                self.assertIn(0, pixels)
                # Reference: main.c !hasPalette and gfx.c ConvertToTiles4Bpp.
                for gray, pixel in zip(pixels, actual.getdata()):
                    index = (255 - gray) // 17
                    rgb5 = [palette[index * 3 + c] // 8 for c in range(3)]
                    expected = tuple(v * 8 + v // 4 for v in rgb5) + (0 if index == 0 else 255,)
                    self.assertEqual(expected, pixel)
                    if gray == 255:
                        self.assertEqual(0, pixel[3])
                    elif gray == 0:
                        self.assertEqual(255, pixel[3])

    def test_title_border_grayscale_matches_raw_indices(self):
        spec = importlib.util.spec_from_file_location('opening_import', ROOT / 'scripts/imports/presentation/import-opening-presentation.py')
        opening = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(opening)
        raw = opening.RAW / 'graphics/title_screen'
        grayscale = grayscale_inputs(raw)
        self.assertEqual({'border_bg.png'}, grayscale)
        original = Image.open(raw / 'border_bg.png')
        palette = opening.palette(raw / 'firered/background.pal')
        actual = opening.rgba(original, palette=palette)
        for gray, pixel in zip(original.getdata(), actual.getdata()):
            index = (255 - gray) // 17
            rgb5 = [palette[index * 3 + c] // 8 for c in range(3)]
            self.assertEqual(tuple(v * 8 + v // 4 for v in rgb5) + (0 if index == 0 else 255,), pixel)

    def test_wireless_signal_atlas_against_raw_indices_and_tilemap(self):
        spec = importlib.util.spec_from_file_location('trade_import', ROOT / 'scripts/imports/presentation/import-npc-trade-scene.py')
        trade = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(trade)
        raw = module.RAW / 'graphics/trade'
        original = Image.open(raw / 'wireless_signal.png')
        self.assertEqual('L', original.mode)
        self.assertEqual({0, 255}, {min(original.getdata()), max(original.getdata())})
        tilemap = list(struct.unpack('<1024H', (raw / 'wireless_signal.bin').read_bytes()))
        with tempfile.TemporaryDirectory() as folder:
            manifest = trade.convert(module.RAW, Path(folder))
            atlas = Image.open(Path(folder) / 'atlas.png')
            for direction in ['send', 'receive']:
                rect = next(a for a in manifest['assets'] if a['id'] == 'signal_' + direction)
                palette = [tuple(map(int, line.split())) for line in (raw / ('wireless_signal_' + direction + '.pal')).read_text().splitlines()[3:]]
                for frame in range(16):
                    for y in range(128):
                        for x in range(128):
                            sx = x + 56
                            entry = tilemap[y // 8 * 32 + sx // 8]
                            tile = entry & 1023
                            tx = 7 - sx % 8 if entry & 1024 else sx % 8
                            ty = 7 - y % 8 if entry & 2048 else y % 8
                            gray = original.getpixel((tile % (original.width // 8) * 8 + tx, tile // (original.width // 8) * 8 + ty))
                            index = (255 - gray) // 17
                            rgb = palette[frame * 16 + index]
                            expected = tuple((c // 8) * 255 // 31 for c in rgb) + (0 if index == 0 else 255,)
                            actual = atlas.getpixel((rect['x'] + frame % 4 * 128 + x, rect['y'] + frame // 4 * 128 + y))
                            self.assertEqual(expected, actual)

if __name__ == '__main__':
    unittest.main()
