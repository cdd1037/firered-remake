#!/usr/bin/env python3
"""Independent source map/mask pixels, safe fishing bounds, and exact menu regeneration."""
import importlib.util
import io
import json
from pathlib import Path
import re
import sys
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
OUT = ROOT / 'content/generated/sprites/pokemon/pokedex-menu'
sys.path.insert(0, str(ROOT / 'scripts/imports/presentation'))
import pokedex_area_data as area


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


class PokedexAreaImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.menu = module('area_menu_test', 'scripts/imports/presentation/import-pokedex-menu.py')
        cls.codec = module('area_codec_test', 'scripts/imports/pokemon/import-forest-capture-dex.py')
        cls.sources = cls.menu.read_sources(RAW)
        cls.species = {n: int(v) for n, v in re.findall(r'^#define SPECIES_(\w+)\s+(\d+)\s*$',
                       cls.sources['include/constants/species.h'].decode(), re.M)}
        cls.areas, cls.outputs, cls.assets = area.build_areas(cls.sources, cls.species, cls.codec)
        cls.atlas = Image.open(io.BytesIO(cls.outputs['area-atlas.png'])).convert('RGBA')
        cls.palette = [tuple((int(c) >> 3) for c in row.split())
                       for row in cls.sources['graphics/pokedex/kanto_dex_bgpals.pal'].decode().splitlines()[3:19]]

    @staticmethod
    def display(rgb):
        return tuple((v << 3) | (v >> 2) for v in rgb)

    def crop(self, sprite):
        x, y = sprite['sourceX'], sprite['sourceY']
        return self.atlas.crop((x, y, x + sprite['width'], y + sprite['height']))

    def test_all_source_map_rgba_and_ellipse_match_atlas(self):
        for region in self.areas['regions']:
            name = re.sub(r'([a-z])([A-Z])', r'\1_\2', region['id']).lower()
            raw = Image.open(io.BytesIO(self.sources['graphics/pokedex/map_' + name + '.png']))
            expected = bytes(c for i in raw.tobytes() for c in (*self.display(self.palette[i & 15]), 255))
            self.assertEqual(expected, self.crop(region['sprite']).tobytes(), region['id'])
        raw = Image.open(io.BytesIO(self.sources['graphics/pokedex/blit_wide_ellipse.png']))
        expected = bytes(c for v in raw.tobytes() for i in [15 - v // 17]
                         for c in (*self.display(self.palette[i]), 0 if i == 0 else 255))
        self.assertEqual(expected, self.crop(self.areas['unknownSprite']).tobytes())

    def test_all_marker_masks_and_five_bit_alpha_match_source(self):
        raw_tiles = Image.open(io.BytesIO(self.sources['graphics/pokedex/kanto_dex_bgtiles.png']))
        background = Image.new('RGB', (240, 160), self.palette[raw_tiles.getpixel((0, 8)) & 15])
        for region in self.areas['regions']:
            rgba = self.crop(region['sprite'])
            rgb5 = Image.frombytes('RGB', rgba.size, bytes(c >> 3 for p in rgba.get_flattened_data() for c in p[:3]))
            background.paste(rgb5, (region['x'], region['y']))
        overlay = self.palette[raw_tiles.getpixel((0, 15 * 8)) & 15]
        text = self.sources['src/pokedex_area_markers.c'].decode()
        shape_names = ['MARKER_CIRCULAR', 'MARKER_SMALL_H', 'MARKER_SMALL_V', 'MARKER_MED_H',
                       'MARKER_MED_V', 'MARKER_LARGE_H', 'MARKER_LARGE_V']
        geometry = {name: (shape_names.index(shape), int(x) + 104, int(y) + 32)
                    for name, shape, x, y in re.findall(r'\[(DEX_AREA_\w+)\]\s*=\s*\{\s*(MARKER_\w+),\s*(-?\d+),\s*(-?\d+)\s*\}', text)}
        for marker in self.areas['markers']:
            shape, x, y = geometry[marker['id']]
            mask = Image.open(io.BytesIO(self.sources[f'graphics/pokedex/area_markers/marker_{shape}.png']))
            expected = bytearray()
            for dy in range(mask.height):
                for dx in range(mask.width):
                    if mask.getpixel((dx, dy)) == 0:
                        expected.extend((0, 0, 0, 0))
                    else:
                        backdrop = background.getpixel((x + dx, y + dy))
                        expected.extend((*self.display(tuple(min(31, (a * 12 + b * 8) // 16) for a, b in zip(overlay, backdrop))), 255))
            self.assertEqual(bytes(expected), self.crop(marker['sprite']).tobytes(), marker['id'])

    def test_regeneration_is_deterministic_and_only_one_area_file_is_live(self):
        second = area.build_areas(self.sources, self.species, self.codec)
        self.assertEqual((self.areas, self.outputs, self.assets), second)
        retained = json.loads((OUT / 'pokedex-menu.json').read_bytes())
        self.assertEqual(self.areas, retained['areas'])
        self.assertEqual(self.outputs['area-atlas.png'], (OUT / 'area-atlas.png').read_bytes())
        self.assertEqual(['area-atlas.png'], sorted(p.name for p in OUT.glob('area-*.png')))
        self.assertEqual(1, len(self.assets))

    def test_only_fire_red_headers_and_all_nine_altering_cave_sets_are_used(self):
        cave = [e for e in self.areas['encounters'] if e['alteringCaveSet'] >= 0]
        self.assertEqual(list(range(9)), [e['alteringCaveSet'] for e in cave])
        self.assertEqual([[41], [179], [204], [228], [216], [190], [213], [234], [235]], [e['species'] for e in cave])
        modified = dict(self.sources)
        wild = json.loads(modified['src/data/wild_encounters.json'])
        for row in wild['wild_encounter_groups'][0]['encounters']:
            if row['base_label'].endswith('_LeafGreen'):
                for value in row.values():
                    if isinstance(value, dict) and 'mons' in value:
                        for mon in value['mons']:
                            mon['species'] = 'SPECIES_BULBASAUR'
        modified['src/data/wild_encounters.json'] = json.dumps(wild).encode()
        self.assertEqual(self.areas, area.build_areas(modified, self.species, self.codec)[0])
        self.assertFalse(any(1 in e['species'] for e in self.areas['encounters']))

    def test_fishing_scans_exactly_ten_slots_and_rejects_an_overread_fixture(self):
        modified = dict(self.sources)
        wild = json.loads(modified['src/data/wild_encounters.json'])
        row = next(r for r in wild['wild_encounter_groups'][0]['encounters']
                   if r['base_label'].endswith('_FireRed') and 'fishing_mons' in r)
        self.assertEqual(10, len(row['fishing_mons']['mons']))
        row['fishing_mons']['mons'].append(dict(min_level=5, max_level=5, species='SPECIES_BULBASAUR'))
        modified['src/data/wild_encounters.json'] = json.dumps(wild).encode()
        with self.assertRaisesRegex(ValueError, 'Wild table size differs'):
            area.build_areas(modified, self.species, self.codec)
        modified = dict(self.sources)
        modified['include/wild_encounter.h'] = re.sub(rb'(#define FISH_WILD_COUNT\s+)10', rb'\g<1>12', modified['include/wild_encounter.h'])
        with self.assertRaisesRegex(ValueError, 'Approved fishing table size changed'):
            area.build_areas(modified, self.species, self.codec)


if __name__ == '__main__':
    unittest.main()
