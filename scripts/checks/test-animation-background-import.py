#!/usr/bin/env python3
"""Independent source-index and binary-layout checks; never runs upstream code."""
import importlib.util
import hashlib
import json
from pathlib import Path
import struct
import unittest
from unittest.mock import patch
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
REFERENCE = ROOT / 'content/raw/pokefirered/037335f4'
spec = importlib.util.spec_from_file_location('background_import', ROOT / 'scripts/imports/battle/import-source-animation-backgrounds.py')
imp = importlib.util.module_from_spec(spec); spec.loader.exec_module(imp)


class BackgroundImportTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.files = imp.build(REFERENCE)
        cls.manifest = json.loads(cls.files['manifest.json'])
        cls.atlas = cls.files['atlas.bin']
    def block(self, record, name):
        offset, length = record[name]
        return self.atlas[offset:offset + length]
    def test_generated_pack_rebuilds_exactly_without_runtime_source_metadata(self):
        for name, content in self.files.items():
            self.assertEqual(content, (ROOT / 'content/generated/battle/animations/backgrounds' / name).read_bytes())
        self.assertEqual({'schemaVersion','basePalette','terrains','moves'}, set(self.manifest))
        for row in self.manifest['terrains'] + self.manifest['moves']:
            self.assertEqual({'id','tiles','tilemap','palette'}, set(row))
    def test_tiles_keep_nibbles_source_order_and_num_tiles_truncation(self):
        grass = next(r for r in self.manifest['terrains'] if r['id'] == 0)
        packed = self.block(grass, 'tiles'); self.assertEqual(98 * 32, len(packed))
        with Image.open(REFERENCE / 'graphics/battle_terrain/grass/terrain.png') as image:
            for tile in range(98):
                for y in range(8):
                    for x in range(8):
                        value = packed[tile * 32 + y * 4 + x // 2] >> ((x % 2) * 4) & 15
                        self.assertEqual(image.getpixel((tile % 8 * 8 + x, tile // 8 * 8 + y)), value)
    def test_partial_move_maps_are_not_expanded_or_repeated(self):
        moves = {r['id']: r for r in self.manifest['moves']}
        self.assertEqual(moves[0]['tiles'], moves[1]['tiles'])
        self.assertEqual(moves[0]['tilemap'], moves[1]['tilemap'])
        self.assertEqual(2048, moves[1]['tilemap'][1]); self.assertEqual(1280, moves[3]['tilemap'][1])
        for id, name in [(1,'dark'), (3,'psychic')]:
            self.assertEqual((REFERENCE / ('graphics/battle_anims/backgrounds/' + name + '.bin')).read_bytes(), self.block(moves[id], 'tilemap'))
        psychic = self.block(moves[3], 'tiles')
        self.assertEqual(1, psychic[0] & 15)
        self.assertEqual(0, struct.unpack_from('<H', self.block(moves[3], 'tilemap'), 30 * 2)[0])
    def test_palette_bank_ownership_and_source_rgb555_conversion(self):
        for row in self.manifest['terrains']: self.assertEqual(96, row['palette'][1])
        for row in self.manifest['moves']: self.assertEqual(32, row['palette'][1])
        base = self.block(self.manifest, 'basePalette'); self.assertEqual(64, len(base))
        self.assertEqual(0, struct.unpack_from('<H', base)[0]); self.assertEqual(0x7fff, struct.unpack_from('<H', base, 2)[0])
        for id, name in [(1,'dark'),(3,'psychic')]:
            row = next(r for r in self.manifest['moves'] if r['id'] == id)
            with Image.open(REFERENCE / ('graphics/battle_anims/backgrounds/' + name + '.png')) as image: colors = image.getpalette()
            expected = [sum((colors[i*3+c]//8) << (c*5) for c in range(3)) for i in range(16)]
            self.assertEqual(expected, list(struct.unpack('<16H', self.block(row, 'palette'))))
    def test_existing_scene_textures_match_canonical_raw_vectors(self):
        vectors = json.loads((ROOT / 'tests/Game.Presentation.Tests/Fixtures/source-background-vectors.json').read_bytes())
        expected = {row['id']: row['sha256'] for row in vectors['terrains']}
        for folder, filename in [('route1','route1-battle-terrain.json'), ('mtmoon','mtmoon-battle-terrain.json'),
                                 ('gym','gym-battle-terrain.json'), ('city','city-battle-terrain.json')]:
            base = ROOT / 'content/generated/battle/terrain' / folder
            document = json.loads((base / filename).read_bytes())
            assets = {row['id']: row for row in document['assets']}
            scenes = [(row['terrainId'], row.get('sceneTexture', row['id'] + '-scene')) for row in document.get('variants', [])]
            if not scenes: scenes = [(document['display']['terrainId'], document['display']['preferredTexture'])]
            self.assertTrue(scenes)
            for terrain, name in scenes:
                with Image.open(base / assets[name]['texturePath']) as image:
                    self.assertEqual((240,112), image.size)
                    rgba = image.convert('RGBA').tobytes(); pixels = list(zip(rgba[0::4], rgba[1::4], rgba[2::4], rgba[3::4]))
                self.assertTrue(all(a == 255 for _,_,_,a in pixels))
                values = [r//8 | (g//8)<<5 | (b//8)<<10 | 32768 for r,g,b,_ in pixels]
                digest = hashlib.sha256(struct.pack('<' + 'H' * len(values), *values)).hexdigest()
                self.assertEqual(expected[terrain], digest, folder + '/' + name)
    def test_source_loader_geometry_change_is_rejected(self):
        original = imp.read
        def changed(reference, path):
            data = original(reference, path)
            return data.replace(b'.mapBaseIndex = 26,\n        .screenSize = 1', b'.mapBaseIndex = 26,\n        .screenSize = 0') if path == 'src/battle_bg.c' else data
        with patch.object(imp, 'read', side_effect=changed):
            with self.assertRaisesRegex(ValueError, 'initialization/load'): imp.build(REFERENCE)
    def test_source_num_tiles_and_map_references_must_agree(self):
        original = imp.read
        def changed(reference, path):
            data = original(reference, path)
            return data.replace(b'-num_tiles 98 -Wnum_tiles', b'-num_tiles 1 -Wnum_tiles') if path == 'graphics_file_rules.mk' else data
        with patch.object(imp, 'read', side_effect=changed):
            with self.assertRaisesRegex(ValueError, 'tilemap reference'): imp.build(REFERENCE)


if __name__ == '__main__': unittest.main()
