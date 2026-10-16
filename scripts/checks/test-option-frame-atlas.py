#!/usr/bin/env python3
"""Offline option-frame regeneration and exact logical-image regression checks."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

ROOT = _PROJECT_ROOT
SPEC = importlib.util.spec_from_file_location('option_frames', ROOT / 'scripts/imports/presentation/import-option-frames.py')
FRAMES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(FRAMES)


class OptionFrameAtlasTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.lock = FRAMES.load_lock()
        cls.sources = FRAMES.read_sources(cls.lock, ROOT / 'content/raw/pokefirered/037335f4')
        cls.outputs = FRAMES.convert(cls.lock, cls.sources)
        cls.catalog = json.loads(cls.outputs['option-frames.json'])

    def test_regeneration_is_deterministic_and_matches_generated_files(self):
        self.assertEqual(self.outputs, FRAMES.convert(self.lock, self.sources))
        self.assertEqual(set(self.outputs), {'frames.png', 'option-frames.json', 'manifest.json'})
        directory = ROOT / 'content/generated/ui/option-frames'
        self.assertEqual(set(self.outputs), {str(p.relative_to(directory)) for p in directory.rglob('*') if p.is_file()})
        for name, data in self.outputs.items():
            self.assertEqual(data, (directory / name).read_bytes(), name)

    def test_every_logical_image_and_nine_slice_matches_source_rgba(self):
        atlas = Image.open(io.BytesIO(self.outputs['frames.png'])).convert('RGBA')
        self.assertLessEqual(max(atlas.size), 1024)
        self.assertEqual(len(self.catalog['assets']), 40)
        self.assertEqual(len({a['texturePath'] for a in self.catalog['assets']}), 5)
        source = self.sources
        tiles = FRAMES.pixels.tile_list(source['graphics/battle_interface/textbox.png'])
        palette = FRAMES.pixels.palette(source['graphics/battle_interface/textbox1.pal'])
        for asset in self.catalog['assets']:
            frame = source[f"graphics/text_window/type{asset['frameIndex'] + 1}.png"]
            w, h, indices, rgb = FRAMES.pixels.indexed_png(frame)
            frame_palette = FRAMES.pixels.colors(rgb)
            if asset['kind'] == 'frame':
                expected = Image.frombytes('RGBA', (w, h), bytes(FRAMES.pixels.rgba(indices, frame_palette)))
            else:
                frame_tiles = FRAMES.pixels.tile_list(frame)
                mapped = list(tiles)
                mapped[18:27] = frame_tiles
                mapped[34:43] = frame_tiles
                top = {'battle-message': 14, 'action-menu': 34, 'move-menu': 54}[asset['kind']]
                rgba = FRAMES.pixels.render_map(mapped, source['graphics/battle_interface/textbox.bin'],
                                               {0: palette, 1: frame_palette}, 240, 48, top)
                expected = Image.frombytes('RGBA', (240, 48), bytes(rgba))
            if 'region' in asset:
                r = asset['region']
                actual = atlas.crop((r['x'], r['y'], r['x'] + r['width'], r['y'] + r['height']))
            else:
                self.assertTrue(asset['frameIndex'] == 0 or asset['kind'] == 'battle-message')
                actual = Image.open(ROOT / asset['texturePath']).convert('RGBA')
            self.assertEqual(expected.size, actual.size)
            # Compare all channels even when alpha is zero; visual equality is insufficient.
            self.assertEqual(expected.tobytes(), actual.tobytes(), asset)
            self.assertEqual((asset['x'], asset['y']), (0, 0 if asset['kind'] == 'frame' else 112))
            if asset['kind'] == 'frame':
                for y in range(0, 24, 8):
                    for x in range(0, 24, 8):
                        self.assertEqual(expected.crop((x, y, x + 8, y + 8)).tobytes(),
                                         actual.crop((x, y, x + 8, y + 8)).tobytes())

    def test_import_replaces_only_owned_legacy_images(self):
        with tempfile.TemporaryDirectory(prefix='option-frame-regeneration-') as temp:
            output = Path(temp)
            old = output / 'type2'
            old.mkdir()
            for kind in ('frame', 'action-menu', 'move-menu'):
                (old / (kind + '.png')).write_bytes(b'old output')
            (old / 'keep.txt').write_text('not owned by this importer')
            subprocess.run([sys.executable, str(ROOT / 'scripts/imports/presentation/import-option-frames.py'), '--output', temp], check=True)
            self.assertEqual([p.name for p in old.iterdir()], ['keep.txt'])
            for name, data in self.outputs.items():
                self.assertEqual(data, (output / name).read_bytes())


if __name__ == '__main__':
    unittest.main()
