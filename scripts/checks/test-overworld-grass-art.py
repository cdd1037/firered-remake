#!/usr/bin/env python3
"""Independent source binding, full RGBA and input-closure checks for grass overlays."""
import argparse
import importlib.util
import io
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
spec = importlib.util.spec_from_file_location('shared_overworld_grass_test',
    options.importer or ROOT / 'scripts/imports/presentation/import-shared-overworld.py')
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)
SHEETS = (('TallGrass', 'tall_grass', (16, 80), 16, 5),
          ('JumpTallGrass', 'jump_tall_grass', (16, 32), 8, 4))
INPUTS = ('src/data/field_effects/field_effect_objects.h',
          'src/data/object_events/object_event_graphics.h', 'data/field_effect_scripts.s',
          'graphics/field_effects/palettes/general_1.pal',
          'graphics/field_effects/pics/tall_grass.png',
          'graphics/field_effects/pics/jump_tall_grass.png')


def source_copy(destination):
    for name in INPUTS:
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(RAW / name, target)


class OverworldGrassArt(unittest.TestCase):
    def test_source_template_frames_and_palette_binding(self):
        objects = (RAW / INPUTS[0]).read_text()
        graphics = (RAW / INPUTS[1]).read_text()
        scripts = (RAW / INPUTS[2]).read_text()
        self.assertRegex(objects, r'gSpritePalette_GeneralFieldEffect1\s*=\s*\{\s*\.data = gFieldEffectObjectPalette1, \.tag = FLDEFF_PAL_TAG_GENERAL_1\s*\}')
        self.assertIn('gFieldEffectObjectPalette1[] = INCBIN_U16("graphics/field_effects/palettes/general_1.gbapal")', graphics)
        for symbol, stem, _, frame_height, count in SHEETS:
            with self.subTest(effect=symbol):
                template = re.search(r'\b' + 'gFieldEffectObjectTemplate_' + symbol + r'\s*=\s*\{(.*?)\};', objects, re.S)[1]
                self.assertIn('.paletteTag = FLDEFF_PAL_TAG_GENERAL_1,', template)
                self.assertIn(f'.oam = &gObjectEventBaseOam_16x{frame_height},', template)
                self.assertIn(f'.images = sPicTable_{symbol},', template)
                self.assertIn(f'gFieldEffectObjectPic_{symbol}[] = INCBIN_U16("graphics/field_effects/pics/{stem}.4bpp")', graphics)
                self.assertIn(f'loadfadedpal_callnative gSpritePalette_GeneralFieldEffect1, FldEff_{symbol}', scripts)
                frames = re.findall(r'overworld_frame\(gFieldEffectObjectPic_' + symbol + r',\s*(\d+),\s*(\d+),\s*(\d+)\)', objects)
                self.assertEqual([(2, frame_height // 8, frame) for frame in range(count)],
                                 [tuple(map(int, row)) for row in frames])

    def test_every_installed_pixel_uses_general1_rgb555_and_index_zero_alpha(self):
        lines = (RAW / INPUTS[3]).read_text().splitlines()
        self.assertEqual(['JASC-PAL', '0100', '16'], lines[:3])
        colors = [tuple((channel // 8) * 8 + (channel // 8) // 4
                        for channel in map(int, line.split())) for line in lines[3:]]
        self.assertEqual(16, len(colors))
        for _, stem, size, frame_height, count in SHEETS:
            with self.subTest(sheet=stem):
                with Image.open(RAW / f'graphics/field_effects/pics/{stem}.png') as source:
                    self.assertEqual('P', source.mode)
                    self.assertEqual(size, source.size)
                    indices = bytes(index % 16 for index in source.tobytes())
                expected = bytes(channel for index in indices for channel in (*colors[index], 255 if index else 0))
                path = GENERATED / 'sprites/overworld' / ('field-' + stem.replace('_', '-') + '.png')
                with Image.open(path) as installed:
                    self.assertEqual(size, installed.size)
                    actual = installed.convert('RGBA')
                    self.assertEqual(expected, actual.tobytes())
                    for frame in range(count):
                        self.assertTrue(actual.crop((0, frame * frame_height, 16, (frame + 1) * frame_height)).getbbox())

    def test_bounded_raw_inputs_regenerate_both_installed_sheets(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source_copy(root)
            outputs = importer.grass_outputs(root)
        self.assertEqual({'field-tall-grass.png', 'field-jump-tall-grass.png'}, set(outputs))
        for name, data in outputs.items():
            with Image.open(io.BytesIO(data)) as actual, Image.open(GENERATED / 'sprites/overworld' / name) as expected:
                self.assertEqual(expected.size, actual.size)
                self.assertEqual(expected.convert('RGBA').tobytes(), actual.convert('RGBA').tobytes())

    def test_palette_data_is_supplied_by_raw_not_embedded_png_or_generated(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source_copy(root)
            colors = [(8 * index, 8 * (15 - index), 128) for index in range(16)]
            (root / INPUTS[3]).write_text('JASC-PAL\n0100\n16\n' + '\n'.join(' '.join(map(str, c)) for c in colors) + '\n')
            original = Image.open(root / INPUTS[5])
            modified = original.copy()
            modified.putpixel((0, 0), 28)  # 4bpp input keeps the low nibble.
            modified.save(root / INPUTS[5])
            outputs = importer.grass_outputs(root)
            with Image.open(io.BytesIO(outputs['field-jump-tall-grass.png'])) as image:
                self.assertEqual((99, 24, 132, 255), image.convert('RGBA').getpixel((0, 0)))

    def test_missing_required_input_fails(self):
        for name in INPUTS:
            with self.subTest(input=name), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                source_copy(root)
                (root / name).unlink()
                with self.assertRaises(FileNotFoundError):
                    importer.grass_outputs(root)

    def test_changed_palette_template_and_frame_binding_fail(self):
        changes = (
            (INPUTS[0], '.paletteTag = FLDEFF_PAL_TAG_GENERAL_1,', '.paletteTag = FLDEFF_PAL_TAG_GENERAL_0,'),
            (INPUTS[0], 'overworld_frame(gFieldEffectObjectPic_JumpTallGrass, 2, 1, 3)', 'overworld_frame(gFieldEffectObjectPic_JumpTallGrass, 2, 1, 2)'),
            (INPUTS[0], '.oam = &gObjectEventBaseOam_16x16,', '.oam = &gObjectEventBaseOam_16x8,'),
            (INPUTS[1], 'graphics/field_effects/palettes/general_1.gbapal', 'graphics/field_effects/palettes/general_0.gbapal'),
            (INPUTS[2], 'gSpritePalette_GeneralFieldEffect1, FldEff_JumpTallGrass', 'gSpritePalette_GeneralFieldEffect0, FldEff_JumpTallGrass'))
        for file, before, after in changes:
            with self.subTest(binding=before), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                source_copy(root)
                path = root / file
                self.assertIn(before, path.read_text())
                path.write_text(path.read_text().replace(before, after))
                with self.assertRaisesRegex(ValueError, 'Grass source'):
                    importer.grass_outputs(root)

    def test_wrong_image_geometry_and_palette_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source_copy(root)
            Image.new('P', (16, 16)).save(root / INPUTS[5])
            with self.assertRaisesRegex(ValueError, 'Grass source image geometry'):
                importer.grass_outputs(root)
            source_copy(root)
            (root / INPUTS[3]).write_text('JASC-PAL\n0100\n1\n0 0 0\n')
            with self.assertRaisesRegex(ValueError, 'Grass source palette'):
                importer.grass_outputs(root)


if __name__ == '__main__':
    unittest.main(argv=[sys.argv[0], *remaining])
