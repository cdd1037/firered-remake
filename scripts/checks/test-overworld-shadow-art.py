#!/usr/bin/env python3
"""Independent source-pixel and graphics-selection checks for jumping shadows."""
import argparse
import importlib.util
import io
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import unittest
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--raw', type=Path)
parser.add_argument('--generated', type=Path)
parser.add_argument('--importer', type=Path)
options, remaining = parser.parse_known_args()
ROOT = options.project_root
RAW = options.raw or ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = options.generated or ROOT / 'content/generated'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
importer_path = options.importer or ROOT / 'scripts/imports/presentation/import-shared-overworld.py'
spec = importlib.util.spec_from_file_location('shared_overworld_shadow_test', importer_path)
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)
SHADOWS = (('s', 'small', (8, 8)), ('m', 'medium', (16, 8)),
           ('l', 'large', (32, 8)), ('xl', 'extra_large', (64, 32)))



class OverworldShadowArt(unittest.TestCase):
    def test_all_shadow_pixels_use_source_indices_and_player_palette_zero(self):
        # RGB555 display expansion rounds by bit replication, not linear scaling.
        colors = [tuple((value >> 3) << 3 | (value >> 3) >> 2 for value in map(int, row.split()))
                  for row in (RAW / 'graphics/object_events/palettes/player.pal').read_text().splitlines()[3:]]
        for key, name, size in SHADOWS:
            with self.subTest(shadow=key):
                with Image.open(RAW / f'graphics/field_effects/pics/shadow_{name}.png') as source:
                    self.assertEqual('P', source.mode)
                    self.assertEqual(size, source.size)
                    indices = bytes(index % 16 for index in source.tobytes())
                self.assertEqual({0, 15}, set(indices))
                expected = bytes(channel for index in indices
                                 for channel in (*colors[index], 0 if index == 0 else 255))
                with Image.open(GENERATED / f'sprites/overworld/field-shadow-{key}.png') as output:
                    self.assertEqual(size, output.size)
                    self.assertEqual(expected, output.convert('RGBA').tobytes())

    def test_every_catalog_graphics_uses_its_source_shadow_size(self):
        catalog = json.loads((GENERATED / 'sprites/overworld/catalog.json').read_bytes())
        self.assertTrue(catalog)
        root = RAW / 'src/data/object_events'
        pointers = (root / 'object_event_graphics_info_pointers.h').read_text()
        info = (root / 'object_event_graphics_info.h').read_text()
        constants = (RAW / 'include/constants/event_objects.h').read_text()
        for identity, row in catalog.items():
            symbol = re.search(r'\[' + re.escape(identity) + r'\]\s*=\s*&(\w+)\b', pointers)[1]
            body = re.search(r'\b' + symbol + r'\s*=\s*\{(.*?)\n\};', info, re.S)[1]
            shadow = re.search(r'\.shadowSize\s*=\s*(SHADOW_SIZE_\w+)', body)[1]
            value = int(re.search(r'^#define\s+' + shadow + r'\s+(\d+)\b', constants, re.M)[1])
            self.assertEqual(value, row['shadowSize'], identity)
            self.assertIn(value, range(4))
        self.assertEqual(1, catalog['OBJ_EVENT_GFX_RED_NORMAL']['shadowSize'])
        self.assertEqual(0, catalog['OBJ_EVENT_GFX_PUSHABLE_BOULDER']['shadowSize'])

    def test_shadow_import_regenerates_installed_pixels(self):
        colors = [tuple((int(c) >> 3) << 3 | (int(c) >> 3) >> 2 for c in row.split())
                  for row in (RAW / 'graphics/object_events/palettes/player.pal').read_text().splitlines()[3:]]
        outputs = importer.shadow_outputs(RAW, colors)
        self.assertEqual({f'field-shadow-{key}.png' for key, _, _ in SHADOWS}, set(outputs))
        for name, data in outputs.items():
            with Image.open(io.BytesIO(data)) as actual, Image.open(GENERATED / 'sprites/overworld' / name) as expected:
                self.assertEqual(expected.size, actual.size)
                self.assertEqual(expected.convert('RGBA').tobytes(), actual.convert('RGBA').tobytes())

    def test_shadow_import_rejects_missing_or_wrong_geometry(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            pictures = root / 'graphics/field_effects/pics'
            pictures.mkdir(parents=True)
            with self.assertRaises(FileNotFoundError):
                importer.shadow_outputs(root, [(0, 0, 0)] * 16)
            for _, name, _ in SHADOWS:
                shutil.copyfile(RAW / f'graphics/field_effects/pics/shadow_{name}.png', pictures / f'shadow_{name}.png')
            Image.new('P', (16, 16)).save(pictures / 'shadow_small.png')
            with self.assertRaisesRegex(ValueError, 'shadow source format'):
                importer.shadow_outputs(root, [(0, 0, 0)] * 16)


if __name__ == '__main__':
    unittest.main(argv=[sys.argv[0], *remaining])
