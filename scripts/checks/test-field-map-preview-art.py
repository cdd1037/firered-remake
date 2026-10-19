#!/usr/bin/env python3
"""Independent source preview cells/palettes/names and importer rejection cases."""
import argparse
import base64
import io
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
from PIL import Image

SCRIPT_ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project-root', type=Path, default=SCRIPT_ROOT)
parser.add_argument('--raw', type=Path)
parser.add_argument('--generated', type=Path)
args, rest = parser.parse_known_args()
ROOT = args.project_root
RAW = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = args.generated or ROOT / 'content/generated'
sys.path.insert(0, str(ROOT / 'scripts/lib'))
sys.path.insert(0, str(SCRIPT_ROOT / 'scripts/lib'))
from field_map_preview_art import compile_outputs
from field_map_preview_metadata import compile_metadata
from world_source import WorldSource

PREVIEWS = 'src/map_preview_screen.c'
PNG = 'graphics/map_preview/mt_moon/tiles.png'
TILEMAP = 'graphics/map_preview/mt_moon/tilemap.bin'


def source():
    return WorldSource(RAW)


def document(src=None):
    return json.loads(compile_outputs(src or source())['maps/map-previews.json'])


def independent_registry():
    text = (RAW / PREVIEWS).read_text()
    table = text.split('sMapPreviewScreenData[MPS_COUNT] = {', 1)[1].split('\n};', 1)[0]
    declarations = dict(re.findall(r'static const u8 (\w+)\[\] = INCBIN_U8\("([^"]+)"\);', text))
    rows = []
    for body in table.split('    [')[1:]:
        fields = {}
        for line in body.splitlines():
            match = re.fullmatch(r'\s*\.(\w+) = (\w+),?', line)
            if match:
                fields[match[1]] = match[2]
        fields['base'] = declarations[fields['tilesptr']].rsplit('/', 1)[0]
        rows.append(fields)
    if not rows:
        raise AssertionError('Empty source map preview registry')
    return rows


class FieldMapPreviewArtTests(unittest.TestCase):
    def test_every_registry_row_type_name_flag_and_art_binding_matches_source(self):
        actual = document()
        expected = independent_registry()
        names = {r['id']: r.get('name') for r in json.loads((RAW / 'src/data/region_map/region_map_sections.json').read_bytes())['map_sections']}
        self.assertEqual({r['mapsec'] for r in expected}, {r['regionSection'] for r in actual['entries']})
        kinds = {'MPS_TYPE_CAVE': 'cave', 'MPS_TYPE_FOREST': 'forest'}
        self.assertEqual({'cave', 'forest'}, {entry['type'] for entry in actual['entries']})
        arts = {}
        for row, entry in zip(expected, actual['entries'], strict=True):
            self.assertEqual((row['mapsec'], names[row['mapsec']], kinds[row['type']]),
                             (entry['regionSection'], entry['name'], entry['type']))
            self.assertEqual({'regionSection', 'name', 'type', 'art'}, set(entry))
            arts.setdefault(row['base'], entry['art'])
            self.assertEqual(arts[row['base']], entry['art'])
        self.assertEqual(len(set(arts.values())), len(actual['art']))

    def test_every_emitted_cell_flip_palette_and_pixel_matches_independently(self):
        actual = document()
        rows = independent_registry()
        checked = set()
        for source_row, entry in zip(rows, actual['entries'], strict=True):
            if entry['art'] in checked:
                continue
            checked.add(entry['art'])
            art = actual['art'][entry['art']]
            with self.subTest(section=entry['regionSection']):
                self.assertEqual((256, 160), (art['width'], art['height']))
                png = Image.open(RAW / source_row['base'] / 'tiles.png')
                colors = png.getpalette()
                expected_palette = [colors[n] // 8 + colors[n + 1] // 8 * 32 + colors[n + 2] // 8 * 1024
                                    for n in range(0, len(colors), 3)]
                self.assertEqual(expected_palette, art['palette'])
                cells = struct.unpack('<640H', (RAW / source_row['base'] / 'tilemap.bin').read_bytes())
                pixels = bytearray()
                for y in range(160):
                    for x in range(256):
                        cell = cells[y // 8 * 32 + x // 8]
                        tile, bank = cell % 1024, cell // 4096
                        sx = x % 8 if not cell & 1024 else 7 - x % 8
                        sy = y % 8 if not cell & 2048 else 7 - y % 8
                        pixel = png.getpixel((tile % (png.width // 8) * 8 + sx, tile // (png.width // 8) * 8 + sy)) % 16
                        self.assertIn(bank, (0, 13, 14))
                        if bank == 0:
                            self.assertEqual(0, pixel)
                            pixels.append(0)
                        else:
                            pixels.append(pixel + (bank - 13) * 16)
                self.assertEqual(bytes(pixels), base64.b64decode(art['indices8Bpp'], validate=True))
                self.assertGreater(sum(index & 15 != 0 for index in pixels), 0)
                self.assertGreater(sum(index & 15 == 0 for index in pixels), 0)
        self.assertEqual(len(actual['art']), len(checked))

    def test_source_label_uses_shared_normal_font_and_actual_palette_colors(self):
        text = (RAW / PREVIEWS).read_text()
        self.assertIn('xctr = 104 - GetStringWidth(FONT_NORMAL, gStringVar4, 0);', text)
        self.assertIn('FONT_NORMAL, xctr / 2, 2, 0, 0, color', text)
        self.assertRegex(text, r'\.width = 13,\s+\.height = 2,\s+\.paletteNum = 14,')
        chars = {}
        for spelling, value in re.findall(r"^'((?:\\.|[^'])*)'\s*=\s*([0-9A-F]{2})\s*$", (RAW / 'charmap.txt').read_text().split('@ Hiragana')[0], re.M):
            chars["'" if spelling == "\\'" else spelling] = int(value, 16)
        widths_text = (RAW / 'src/text.c').read_text().split('static const u8 sFontNormalLatinGlyphWidths[] =', 1)[1].split('};', 1)[0]
        widths = [int(value) for value in re.findall(r'\d+', widths_text)]
        for entry in document()['entries']:
            width = sum(widths[chars[c]] for c in entry['name'])
            self.assertGreater(width, 0)
            self.assertLessEqual(width, 104)
        for art in document()['art']:
            self.assertEqual([32734, 0, 27482], [art['palette'][i] for i in (17, 20, 19)])

    def test_staged_art_exactly_matches_conversion(self):
        self.assertEqual(compile_outputs(source())['maps/map-previews.json'], (GENERATED / 'maps/map-previews.json').read_bytes())

    def test_registry_change_drives_new_section_without_an_allowlist(self):
        src = source()
        src.data[PREVIEWS] = src.data[PREVIEWS].replace(b'.mapsec = MAPSEC_MT_MOON,', b'.mapsec = MAPSEC_NEW_CAVE,')
        path = 'src/data/region_map/region_map_sections.json'
        src.data[path] = src.data[path].replace(b'"MAPSEC_MT_MOON"', b'"MAPSEC_NEW_CAVE"')
        entries = document(src)['entries']
        self.assertIn('MAPSEC_NEW_CAVE', [r['regionSection'] for r in entries])
        self.assertNotIn('MAPSEC_MT_MOON', [r['regionSection'] for r in entries])

    def test_forest_registry_change_drives_new_section_without_an_allowlist(self):
        src = source()
        src.data[PREVIEWS] = src.data[PREVIEWS].replace(b'.mapsec = MAPSEC_VIRIDIAN_FOREST,', b'.mapsec = MAPSEC_NEW_FOREST,')
        path = 'src/data/region_map/region_map_sections.json'
        src.data[path] = src.data[path].replace(b'"MAPSEC_VIRIDIAN_FOREST"', b'"MAPSEC_NEW_FOREST"')
        entries = {r['regionSection']: r for r in document(src)['entries']}
        self.assertEqual('forest', entries['MAPSEC_NEW_FOREST']['type'])
        self.assertNotIn('MAPSEC_VIRIDIAN_FOREST', entries)

    def test_forest_art_aliases_share_only_art_and_retain_region_names(self):
        entries = {r['regionSection']: r for r in document()['entries']}
        viridian, pattern = [entries['MAPSEC_' + name] for name in ('VIRIDIAN_FOREST', 'PATTERN_BUSH')]
        self.assertEqual(viridian['art'], pattern['art'])
        self.assertNotEqual(viridian['name'], pattern['name'])
        warehouse, berry = [entries['MAPSEC_' + name] for name in ('ROCKET_WAREHOUSE', 'BERRY_FOREST')]
        self.assertNotEqual(warehouse['name'], berry['name'])
        self.assertNotEqual(warehouse['art'], berry['art'])

    def test_every_map_metadata_type_and_actual_flag_matches_source_independently(self):
        actual = compile_metadata(source())
        registry = {row['mapsec']: row for row in independent_registry()}
        checked = set()
        for path in sorted((RAW / 'data/maps').glob('*/map.json')):
            events = json.loads(path.read_bytes())
            row = registry.get(events['region_map_section'])
            if row is None:
                self.assertNotIn(path.parent.name, actual)
                continue
            checked.add(path.parent.name)
            flag = row['flagId']
            if row['type'] == 'MPS_TYPE_FOREST':
                text = path.with_name('scripts.inc').read_text()
                callback = re.search(r'map_script MAP_SCRIPT_ON_TRANSITION, (\w+)', text)
                flag = None
                if callback:
                    body = text.split(callback[1] + '::', 1)[1].strip().splitlines()
                    self.assertRegex(body[0], r'^setworldmapflag FLAG_[A-Z0-9_]+$')
                    flag = body[0].split()[1]
            self.assertEqual(dict(type=row['type'].removeprefix('MPS_TYPE_').lower(), visitFlag=flag),
                             actual[path.parent.name])
        self.assertTrue(checked)
        self.assertEqual(checked, set(actual))
        self.assertEqual('FLAG_WORLD_MAP_FIVE_ISLAND_ROCKET_WAREHOUSE', actual['FiveIsland_RocketWarehouse']['visitFlag'])
        self.assertIsNone(actual['PokemonMansion_2F']['visitFlag'])
        self.assertEqual('forest', actual['PokemonMansion_2F']['type'])

    def test_forest_entry_producer_uses_actual_script_changes_without_region_exceptions(self):
        src = source()
        path = 'data/maps/FiveIsland_RocketWarehouse/scripts.inc'
        src.data[path] = src.data[path].replace(b'setworldmapflag FLAG_WORLD_MAP_FIVE_ISLAND_ROCKET_WAREHOUSE',
                                               b'setworldmapflag FLAG_WORLD_MAP_POWER_PLANT')
        self.assertEqual('FLAG_WORLD_MAP_POWER_PLANT', compile_metadata(src)['FiveIsland_RocketWarehouse']['visitFlag'])

    def test_forest_entry_missing_conditional_duplicate_or_unknown_producer_fails(self):
        original = b'setworldmapflag FLAG_WORLD_MAP_VIRIDIAN_FOREST'
        for replacement in (b'end', b'call NewConditionalFlag', b'setworldmapflag FLAG_UNKNOWN',
                            original + b'\n\t' + original):
            src = source()
            path = 'data/maps/ViridianForest/scripts.inc'
            src.data[path] = src.data[path].replace(original, replacement, 1)
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                compile_metadata(src)

    def test_later_callback_flag_producer_unknown_operation_and_cycles_fail(self):
        for replacement in (b'setworldmapflag FLAG_WORLD_MAP_POWER_PLANT', b'special UnknownEffect',
                            b'call PowerPlant_EventScript_ShowZapdos', b'call PowerPlant_OnTransition'):
            src = source()
            path = 'data/maps/PowerPlant/scripts.inc'
            src.data[path] = src.data[path].replace(b'clearflag FLAG_HIDE_ZAPDOS', replacement)
            with self.subTest(replacement=replacement), self.assertRaises(ValueError):
                compile_metadata(src)

    def test_later_resume_callback_cannot_overwrite_duration_flag(self):
        src = source()
        path = 'data/maps/PowerPlant/scripts.inc'
        src.data[path] = src.data[path].replace(b'specialvar VAR_RESULT, GetBattleOutcome',
                                               b'setworldmapflag FLAG_WORLD_MAP_POWER_PLANT')
        with self.assertRaisesRegex(ValueError, 'Ambiguous forest entry visit flag'):
            compile_metadata(src)

    def test_missing_art_input_fails(self):
        with tempfile.TemporaryDirectory(prefix='map-preview-missing-') as temporary:
            root = Path(temporary)
            for name in (PREVIEWS, 'include/map_preview_screen.h', 'src/data/region_map/region_map_sections.json', 'include/constants/flags.h'):
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes((RAW / name).read_bytes())
            with self.assertRaises(FileNotFoundError):
                compile_outputs(WorldSource(root))

    def test_forests_retain_the_same_source_art_validation(self):
        rows = [row for row in independent_registry() if row['type'] == 'MPS_TYPE_FOREST']
        self.assertTrue(rows)
        for base in sorted({row['base'] for row in rows}):
            with self.subTest(base=base):
                src = source()
                path = base + '/tilemap.bin'
                src.data[path] = src.data[path][:-2]
                with self.assertRaisesRegex(ValueError, '256x160'):
                    compile_outputs(src)

    def test_short_tilemap_fails(self):
        src = source()
        src.data[TILEMAP] = src.data[TILEMAP][:-2]
        with self.assertRaisesRegex(ValueError, '256x160'):
            compile_outputs(src)

    def test_out_of_range_tile_fails(self):
        src = source()
        src.data[TILEMAP] = struct.pack('<H', 0xD3FF) + src.data[TILEMAP][2:]
        with self.assertRaisesRegex(ValueError, 'exceeds emitted'):
            compile_outputs(src)

    def test_unused_palette_bank_fails(self):
        src = source()
        src.data[TILEMAP] = struct.pack('<H', 0xF001) + src.data[TILEMAP][2:]
        with self.assertRaisesRegex(ValueError, 'unloaded palette'):
            compile_outputs(src)

    def test_unknown_flag_and_missing_name_fail(self):
        src = source()
        src.data[PREVIEWS] = src.data[PREVIEWS].replace(b'.flagId = FLAG_WORLD_MAP_MT_MOON_1F,', b'.flagId = FLAG_UNKNOWN,')
        with self.assertRaisesRegex(ValueError, 'Unknown source preview visit flag'):
            compile_outputs(src)
        src = source()
        path = 'src/data/region_map/region_map_sections.json'
        src.data[path] = src.data[path].replace(b'"name": "MT. MOON"', b'"name": null')
        with self.assertRaisesRegex(ValueError, 'preview name'):
            compile_outputs(src)

    def test_out_of_palette_index_fails(self):
        src = source()
        image = Image.open(io.BytesIO(src.data[PNG]))
        image.load()
        image.putpixel((0, 0), 32)
        encoded = io.BytesIO()
        image.save(encoded, format='PNG', bits=8)
        src.data[PNG] = encoded.getvalue()
        with self.assertRaisesRegex(ValueError, 'palette banks'):
            compile_outputs(src)


if __name__ == '__main__':
    unittest.main(argv=[sys.argv[0], *rest])
