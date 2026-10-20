#!/usr/bin/env python3
"""Offline glyph completeness, fixed metrics and source-preservation checks."""
import argparse
import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from PIL import Image, ImageFont

DEFAULT_ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser()
p.add_argument('--root', type=Path, default=DEFAULT_ROOT)
args, remaining = p.parse_known_args()
ROOT = args.root
spec = importlib.util.spec_from_file_location('cjk_import', Path(__file__).resolve().parents[1] / 'imports/presentation/import-cjk-font.py')
font_import = importlib.util.module_from_spec(spec); spec.loader.exec_module(font_import)
FONT_DIRECTORY = ROOT / 'content/raw/fonts/fusion-pixel/2026.10.16'
NORMAL_FONT = FONT_DIRECTORY / 'fusion-pixel-12px-monospaced-zh_hans.otf'
SMALL_FONT = FONT_DIRECTORY / 'fusion-pixel-10px-monospaced-zh_hans.otf'
SOURCE_PATH = ROOT / 'content/generated/ui/font/source-font.json'
SOURCE = json.loads(SOURCE_PATH.read_text())


class CjkFontTests(unittest.TestCase):
    def test_help_circle_dot_uses_original_pixels_and_face_metrics(self):
        raw = ROOT / 'content/raw/pokefirered/037335f4/graphics/fonts'
        for face in SOURCE['fonts']:
            with self.subTest(face=face['id']):
                glyph = next(g for g in face['glyphs'] if g['character'] == '⊙')
                small = face['id'] == 'small'
                self.assertEqual(glyph['sourceGlyphId'], 0x115)
                self.assertEqual(glyph['advance'], 8 if small else 10)
                columns, cell_width = (32, 8) if small else (16, 16)
                source = Image.open(raw / ('latin_' + face['id'] + '.png'))
                atlas = Image.open(SOURCE_PATH.parent / face['texturePath']).convert('RGBA')
                colors = [(0, 0, 0, 0), tuple(SOURCE['foregroundRgba']),
                          tuple(SOURCE['shadowRgba']), (0, 0, 0, 0)]
                for y in range(glyph['height']):
                    for x in range(glyph['width']):
                        role = source.getpixel(((0x115 % columns) * cell_width + x,
                                                (0x115 // columns) * 16 + y))
                        self.assertEqual(atlas.getpixel((glyph['x'] + x, glyph['y'] + y)), colors[role])

    def convert(self, text, normal_size=12, small_size=10, **kwargs):
        return font_import.convert(NORMAL_FONT, SMALL_FONT, SOURCE,
                                   font_import.literal_characters(text), normal_size, small_size, **kwargs)

    def test_native_face_cmaps_include_chinese_and_exclude_actual_missing(self):
        for path, size in ((NORMAL_FONT, 12), (SMALL_FONT, 10)):
            with self.subTest(size=size):
                characters = font_import.unicode_characters(path, size)
                self.assertTrue(set(map(ord, '中文精灵宝可梦训练师')) <= characters)
                self.assertNotIn(0x10FFFF, characters)
                self.assertNotIn(ord('𠮷'), characters)
        self.assertTrue(set(map(ord, '靓黾')) <= font_import.unicode_characters(NORMAL_FONT, 12))
        self.assertFalse(set(map(ord, '靓黾')) & font_import.unicode_characters(SMALL_FONT, 10))
        with self.assertRaisesRegex(ValueError, 'Expected Fusion Pixel 12px'):
            font_import.unicode_characters(SMALL_FONT, 12)
        with self.assertRaisesRegex(ValueError, 'Expected Fusion Pixel 12px'):
            font_import.unicode_characters(ROOT / 'src/Game.Godot/Assets/ProbeFont.ttc', 12)

    def test_naming_inventory_covers_untranslated_han_without_synthetic_missing_glyphs(self):
        allowed = font_import.naming_characters(NORMAL_FONT, SMALL_FONT)
        mapped = (font_import.unicode_characters(NORMAL_FONT, 12) &
                  font_import.unicode_characters(SMALL_FONT, 10))
        self.assertEqual(allowed & set(range(0x3400, 0x4DC0)), mapped & set(range(0x3400, 0x4DC0)))
        self.assertEqual(allowed & set(range(0x4E00, 0xA000)), mapped & set(range(0x4E00, 0xA000)))
        self.assertTrue(set(map(ord, '龘朴竍〇·𠹳𧚄𭕄')) <= allowed)
        self.assertFalse(set(map(ord, '䶵𠮷靓黾')) & allowed)
        self.assertNotIn(0x302A, allowed)  # Combining tone mark is not name punctuation.

    def test_preserves_source_catalog_and_files_including_all_latin_advances(self):
        before = copy.deepcopy(SOURCE)
        paths = [SOURCE_PATH] + [SOURCE_PATH.parent / (face + '.png') for face in ('normal', 'small')]
        original_bytes = [path.read_bytes() for path in paths]
        result = self.convert('ABC RED é…⊙中，')
        self.assertEqual(SOURCE, before)
        self.assertEqual([path.read_bytes() for path in paths], original_bytes)
        self.assertEqual(set(result), set(font_import.OUTPUT_NAMES))
        catalog = json.loads(result['cjk-font.json'])
        for face in catalog['fonts']:
            self.assertEqual({glyph['codepoint'] for glyph in face['glyphs']}, {ord('中'), ord('，')})

    def test_complete_glyphs_have_two_source_color_roles_and_bounded_metrics(self):
        outputs = self.convert('中文精灵宝可梦训练师，。！？')
        catalog = json.loads(outputs['cjk-font.json'])
        colors = {(0, 0, 0, 0), tuple(SOURCE['foregroundRgba']), tuple(SOURCE['shadowRgba'])}
        for face in catalog['fonts']:
            image = Image.open(io.BytesIO(outputs['cjk-' + face['id'] + '.png'])).convert('RGBA')
            self.assertEqual(image.size, (face['textureWidth'], face['textureHeight']))
            self.assertEqual(face['baseline'], 14 if face['id'] == 'normal' else 13)
            self.assertTrue(set(image.get_flattened_data()) <= colors)
            for glyph in face['glyphs']:
                self.assertEqual(glyph['height'], face['pixelSize'])
                self.assertEqual(glyph['advance'], glyph['width'])
                self.assertEqual(glyph['advance'], 12 if face['id'] == 'normal' else 10)
                cell = image.crop((glyph['x'], glyph['y'], glyph['x'] + glyph['width'], glyph['y'] + glyph['height']))
                self.assertIsNotNone(cell.getbbox())
                self.assertIn(tuple(SOURCE['foregroundRgba']), set(cell.get_flattened_data()))
                self.assertIn(tuple(SOURCE['shadowRgba']), set(cell.get_flattened_data()))

    def test_fixed_baseline_keeps_punctuation_below_character_tops(self):
        outputs = self.convert('中。')
        for face in json.loads(outputs['cjk-font.json'])['fonts']:
            image = Image.open(io.BytesIO(outputs['cjk-' + face['id'] + '.png'])).convert('RGBA')
            bounds = {}
            for glyph in face['glyphs']:
                cell = image.crop((glyph['x'], glyph['y'], glyph['x'] + glyph['width'], glyph['y'] + glyph['height']))
                bounds[glyph['codepoint']] = cell.getbbox()
            self.assertGreater(bounds[ord('。')][1], bounds[ord('中')][1] + 4)

    def test_native_overhang_is_preserved_and_non_native_sizes_are_rejected(self):
        for normal, small in ((11, 10), (13, 10), (12, 9), (12, 11)):
            with self.subTest(normal=normal, small=small):
                with self.assertRaisesRegex(ValueError, 'native 12px normal / 10px small'):
                    self.convert('中文；', normal_size=normal, small_size=small)
        outputs = self.convert('朴竍；')
        for face in json.loads(outputs['cjk-font.json'])['fonts']:
            image = Image.open(io.BytesIO(outputs['cjk-' + face['id'] + '.png'])).convert('RGBA')
            for glyph in face['glyphs']:
                if glyph['codepoint'] not in set(map(ord, '朴竍')):
                    continue
                self.assertEqual(glyph['advance'], 12 if face['id'] == 'normal' else 10)
                self.assertEqual(glyph['width'], 12 if face['id'] == 'normal' else 11)
                if face['id'] == 'small':
                    self.assertIn(tuple(SOURCE['shadowRgba']),
                                  [image.getpixel((glyph['x'] + 10, glyph['y'] + y))
                                   for y in range(glyph['height'])])
        native = ImageFont.truetype(str(NORMAL_FONT), 12, layout_engine=ImageFont.Layout.BASIC)
        with self.assertRaisesRegex(ValueError, 'U\\+4E2D exceeds native'):
            font_import.rasterize(native, ord('中'), 10, 10)

    def test_missing_glyph_and_empty_supplement_are_explicit_failures(self):
        with self.assertRaisesRegex(ValueError, 'lacks U\\+10FFFF'):
            self.convert('中\U0010ffff')
        with self.assertRaisesRegex(ValueError, 'no supplementary glyphs'):
            self.convert('ABC')
        with self.assertRaisesRegex(ValueError, 'small face lacks U\\+9753, U\\+9EFE'):
            self.convert('中靓黾', allowed_missing={})
        missing = {}
        outputs = self.convert('中靓黾', missing_report=missing)
        self.assertEqual(missing, {'normal': [], 'small': [ord('靓'), ord('黾')]})
        faces = {f['id']: f for f in json.loads(outputs['cjk-font.json'])['fonts']}
        self.assertEqual({g['codepoint'] for g in faces['normal']['glyphs']}, set(map(ord, '中靓黾')))
        self.assertEqual({g['codepoint'] for g in faces['small']['glyphs']}, {ord('中')})

    def test_deterministic_output_and_no_runtime_provenance(self):
        self.assertEqual(self.convert('中文'), self.convert('中文'))
        manifest = json.loads(self.convert('中文')['cjk-font.json'])
        self.assertEqual(set(manifest), {'schemaVersion', 'foregroundRgba', 'shadowRgba', 'fonts'})
        self.assertNotIn(str(FONT_DIRECTORY), json.dumps(manifest))

    def test_shelf_packing_preserves_every_cell_without_overlap(self):
        shared = font_import.naming_characters(NORMAL_FONT, SMALL_FONT) & set(range(0x4E00, 0xA000))
        outputs = self.convert(''.join(chr(cp) for cp in sorted(shared)[:300]))
        for face in json.loads(outputs['cjk-font.json'])['fonts']:
            rows = {}
            for glyph in face['glyphs']:
                self.assertLessEqual(glyph['x'] + glyph['width'], face['textureWidth'])
                self.assertLessEqual(glyph['y'] + glyph['height'], face['textureHeight'])
                intervals = rows.setdefault(glyph['y'], [])
                self.assertTrue(all(glyph['x'] >= end or glyph['x'] + glyph['width'] <= start
                                    for start, end in intervals))
                intervals.append((glyph['x'], glyph['x'] + glyph['width']))
            self.assertGreater(len(rows), 1)

    def test_supplementary_han_rasterizes_both_faces_without_emoji_substitution(self):
        outputs = self.convert('𠹳𧚄𭕄龘')
        for face in json.loads(outputs['cjk-font.json'])['fonts']:
            self.assertEqual({glyph['codepoint'] for glyph in face['glyphs']}, set(map(ord, '𠹳𧚄𭕄龘')))
        with self.assertRaisesRegex(ValueError, 'lacks U\\+1F600'):
            self.convert('😀')
        with self.assertRaisesRegex(ValueError, 'lacks U\\+4DB5, U\\+20BB7'):
            self.convert('𠮷䶵')

    def test_pack_inventory_reads_values_and_omits_source_control_spelling(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); (root / 'source').mkdir()
            (root / 'field-menu.json').write_text(json.dumps({'菜单键名': '背包'}))
            (root / 'source' / 'story.json').write_text(json.dumps({'源符号': '欢迎{PLAYER}\\n来到世界。$'}))
            actual = font_import.pack_characters(root)
            self.assertEqual(actual, set(map(ord, '背包欢迎来到世界。')))
        with self.assertRaisesRegex(ValueError, 'invalid Unicode'):
            font_import.literal_characters('\ud800')
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError, 'no JSON input files'):
                font_import.pack_characters(temp)

    def test_native_foreground_matches_release_mask_pixel_for_pixel(self):
        outputs = self.convert('中龘朴竍，。；')
        for face in json.loads(outputs['cjk-font.json'])['fonts']:
            path, size, baseline = ((NORMAL_FONT, 12, 10) if face['id'] == 'normal'
                                    else (SMALL_FONT, 10, 9))
            native = ImageFont.truetype(str(path), size, layout_engine=ImageFont.Layout.BASIC)
            image = Image.open(io.BytesIO(outputs['cjk-' + face['id'] + '.png'])).convert('RGBA')
            for glyph in face['glyphs']:
                with self.subTest(face=face['id'], codepoint=glyph['codepoint']):
                    mask, (left, top) = native.getmask2(chr(glyph['codepoint']), mode='1', anchor='ls')
                    expected_foreground = {(x + left, y + top + baseline)
                                           for y in range(mask.size[1]) for x in range(mask.size[0])
                                           if mask[y * mask.size[0] + x]}
                    expected_shadow = {(x + 1, y + 1) for x, y in expected_foreground} - expected_foreground
                    actual_foreground, actual_shadow = set(), set()
                    for y in range(glyph['height']):
                        for x in range(glyph['width']):
                            pixel = image.getpixel((glyph['x'] + x, glyph['y'] + y))
                            if pixel == tuple(SOURCE['foregroundRgba']):
                                actual_foreground.add((x, y))
                            elif pixel == tuple(SOURCE['shadowRgba']):
                                actual_shadow.add((x, y))
                    self.assertEqual(actual_foreground, expected_foreground)
                    self.assertEqual(actual_shadow, expected_shadow)

    def test_invalid_source_geometry_palette_and_inventory_are_rejected(self):
        for change, error in (
                (lambda source: source.update(schemaVersion=2), 'Unsupported source'),
                (lambda source: source['fonts'][0].update(pixelSize=15), 'height/baseline differs'),
                (lambda source: source['fonts'][1].update(baseline=12), 'height/baseline differs'),
                (lambda source: source.update(shadowRgba=source['foregroundRgba']), 'source foreground/shadow'),
                (lambda source: source['fonts'][0]['glyphs'].append(source['fonts'][0]['glyphs'][0]),
                 'original source glyph inventory')):
            source = copy.deepcopy(SOURCE)
            change(source)
            with self.subTest(error=error):
                with self.assertRaisesRegex(ValueError, error):
                    font_import.convert(NORMAL_FONT, SMALL_FONT, source, {ord('中')})

    def test_cli_reports_deferred_missing_and_preserves_complete_release_notices(self):
        notices = font_import.license_outputs(NORMAL_FONT, SMALL_FONT)
        self.assertIn('OFL.txt', notices)
        self.assertTrue(any(name.startswith('LICENSES/') for name in notices))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); pack = root / 'text'; pack.mkdir(); out = root / 'font'
            (pack / 'example.json').write_text(json.dumps({'text': '中靓黾'}))
            command = ['import-cjk-font.py', '--normal-font', str(NORMAL_FONT),
                       '--small-font', str(SMALL_FONT), '--source-font', str(SOURCE_PATH),
                       '--text-pack', str(pack), '--out', str(out)]
            def run(*extra):
                output = io.StringIO()
                with patch.object(sys, 'argv', command + list(extra)), contextlib.redirect_stdout(output):
                    font_import.main()
                return json.loads(output.getvalue())
            imported = run()
            self.assertEqual(imported['missingCodepoints'], {'normal': [], 'small': ['U+9753', 'U+9EFE']})
            self.assertEqual(imported['files'], len(font_import.OUTPUT_NAMES) + len(notices))
            for name, data in notices.items():
                self.assertEqual((out / name).read_bytes(), data)
            self.assertEqual(run('--check')['status'], 'checked')
            notice = next(name for name in notices if name.startswith('LICENSES/'))
            (out / notice).write_text('incomplete license')
            with self.assertRaisesRegex(ValueError, 'outputs missing/different: LICENSES/'):
                run('--check')


if __name__ == '__main__':
    unittest.main(argv=[sys.argv[0], *remaining])
