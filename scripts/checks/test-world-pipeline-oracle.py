#!/usr/bin/env python3
"""Independent raw/final world invariants; optional temporary old-final parity oracle.

Only author-selection expansion is shared; raw decoding and pixel comparison
are independent. The optional baseline is verification-only and
must never become compiler input or a checked-in historical hash manifest.
"""
import argparse
import base64
import json
from pathlib import Path
import re
import struct
import sys
import unittest
import zlib
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from world_selection import load_selection
RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
BASELINE = None
NPC_GENERATED = None


def read(path):
    return json.loads(path.read_bytes())


def units(data, size):
    if len(data) % size:
        raise AssertionError('Misaligned source binary')
    return struct.unpack('<' + {2: 'H', 4: 'I'}[size] * (len(data) // size), data)


class LogicalImages:
    """Independent unpacker preserving every RGBA byte, including transparent RGB."""
    def __init__(self, root):
        self.root = root
        self.atlas = root / 'maps/atlases'
        self.catalog = read(self.atlas / 'catalog.json') if (self.atlas / 'catalog.json').exists() else None
        self.pages = {}

    def read(self, path):
        # Prefer packed final output when available, even if staging PNGs remain.
        if self.catalog is None or path not in self.catalog['images']:
            if not (self.root / path).is_file():
                raise AssertionError('Missing logical image: ' + path)
            with Image.open(self.root / path) as image:
                return image.convert('RGBA')
        entry = self.catalog['images'][path]
        data = (self.atlas / 'indices.bin').read_bytes()
        indices = units(zlib.decompress(data[entry['offset']:entry['offset'] + entry['length']]), 2)
        image = Image.new('RGBA', (entry['width'], entry['height']))
        assert len(indices) == image.width * image.height // 256
        for i, index in enumerate(indices):
            page_index, tile_index = divmod(index, self.catalog['tilesPerPage'])
            descriptor = self.catalog['pages'][entry['pack']][page_index]
            key = descriptor['path']
            if key not in self.pages:
                with Image.open(self.atlas / key) as page:
                    self.pages[key] = page.convert('RGBA')
            x, y = tile_index % self.catalog['pageColumns'] * 16, tile_index // self.catalog['pageColumns'] * 16
            image.paste(self.pages[key].crop((x, y, x + 16, y + 16)),
                        (i % (image.width // 16) * 16, i // (image.width // 16) * 16))
        return image


class WorldPipelineOracle(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.selection = load_selection(ROOT / 'data/world-maps.json')
        cls.index = read(GENERATED / 'maps/world.json')
        cls.layouts = {x['id']: x for x in read(RAW / 'data/layouts/layouts.json')['layouts'] if 'id' in x}
        cls.docs = {r['path']: read(GENERATED / r['path']) for r in cls.selection}
        headers = (RAW / 'src/data/tilesets/headers.h').read_text()
        declarations = (RAW / 'src/data/tilesets/metatiles.h').read_text()
        cls.attributes = {}
        for name, body in re.findall(r'const struct Tileset (\w+)\s*=\s*\{(.*?)\};', headers, re.S):
            symbol = re.search(r'\.metatileAttributes\s*=\s*(\w+)', body)[1]
            path = re.search(r'\b' + symbol + r'\[\]\s*=\s*INCBIN_U32\("([^"]+)"\)', declarations)[1]
            cls.attributes[name] = RAW / path

    def test_index_identity_and_explicit_variants(self):
        self.assertTrue(self.selection)
        expected = []
        ordinary = {}
        variants = set()
        for row in self.selection:
            raw = read(RAW / 'data/maps' / row['sourceName'] / 'map.json')
            entry = dict(stableId=row['stableId'], path=row['path'], sourceMapSymbol=raw['id'])
            if 'variant' in row:
                entry['variant'] = row['variant']
                entry['layout'] = row['layout']
                self.assertNotIn((row['stableId'], row['variant']), variants)
                variants.add((row['stableId'], row['variant']))
            else:
                self.assertNotIn(row['stableId'], ordinary)
                ordinary[row['stableId']] = row
            self.assertEqual(row['stableId'], self.index['mapSymbols'][raw['id']])
            # Existing opening/Viridian rendering uses the original player sheet.
            entry['playerTexturePath'] = 'sprites/overworld/' + ('opening-player.png'
                if row['path'].split('/')[2] in ('opening', 'viridian') else 'red-normal.png')
            entry['usesLocalBorder'] = row.get('usesLocalBorder', row.get('renderBorder', True))
            expected.append(entry)
        self.assertEqual(expected, self.index['maps'])
        self.assertTrue(all(map_id in ordinary for map_id, _ in variants))
        # These are gameplay layout choices, not redundant copies.
        for floor in range(1, 9):
            map_id = f'map.trainer-tower-{floor}f'
            self.assertTrue({(map_id, 'double'), (map_id, 'knockout')} <= variants)
            self.assertNotIn('variant', ordinary[map_id])
        for floor in (3, 4):
            self.assertIn((f'map.seafoam-islands-b{floor}f', 'current-stopped'), variants)
        self.assertIn(('map.three-island-dunsparce-tunnel', 'dug-out'), variants)

    def test_raw_blocks_events_and_script_endpoints(self):
        for row in self.selection:
            with self.subTest(path=row['path']):
                doc = self.docs[row['path']]
                raw = read(RAW / 'data/maps' / row['sourceName'] / 'map.json')
                layout = self.layouts[row.get('layout', raw['layout'])]
                self.assertEqual((row['stableId'], raw['id'], row['sourceName']),
                                 (doc['stableId'], doc['sourceMapSymbol'], doc['sourceMapName']))
                self.assertEqual((layout['width'], layout['height'], layout['border_width'], layout['border_height']),
                                 (doc['width'], doc['height'], doc['borderWidth'], doc['borderHeight']))
                self.assertEqual((RAW / layout['blockdata_filepath']).read_bytes(), base64.b64decode(doc['blocks'], validate=True))
                self.assertEqual((RAW / layout['border_filepath']).read_bytes(), base64.b64decode(doc['borderBlocks'], validate=True))
                self.assertEqual(raw, doc['events'])  # All ordered events, warp indices and script roots.
                blocks = units(base64.b64decode(doc['blocks']), 2) + units(base64.b64decode(doc['borderBlocks']), 2)
                self.assertEqual(sorted({x & 1023 for x in blocks}), [x[0] for x in doc['metatiles']])
                primary, secondary = (units(self.attributes[layout[k]].read_bytes(), 4) for k in ('primary_tileset', 'secondary_tileset'))
                for mid, attributes in doc['metatiles']:
                    self.assertEqual(primary[mid] if mid < 640 else secondary[mid - 640], attributes)
                paths = [f'data/maps/{row["sourceName"]}/{leaf}' for leaf in ('scripts.inc', 'text.inc')
                         if (RAW / 'data/maps' / row['sourceName'] / leaf).is_file()]
                self.assertEqual(paths, doc['scriptPaths'])
                self.assertTrue(paths)
                self.assertEqual({x[1] & 511 for x in doc['metatiles']}, {x['id'] for x in doc['behaviors']})

    def test_actual_bill_and_layout_business_oracles(self):
        by_identity = {(r['stableId'], r.get('variant')): self.docs[r['path']] for r in self.selection}
        bill = by_identity['map.route-25-sea-cottage', None]['events']['object_events']
        human = next(x for x in bill if x['local_id'] == 'LOCALID_BILL_HUMAN')
        self.assertEqual(('OBJ_EVENT_GFX_BILL', 7, 5, 'Route25_SeaCottage_EventScript_Bill'),
                         (human['graphics_id'], human['x'], human['y'], human['script']))
        for (map_id, variant), doc in by_identity.items():
            if variant is None:
                continue
            original = by_identity[map_id, None]
            self.assertEqual(original['events']['warp_events'], doc['events']['warp_events'])
            self.assertEqual((original['width'], original['height']), (doc['width'], doc['height']))
            self.assertNotEqual(original['blocks'], doc['blocks'], (map_id, variant))

    def assert_previous_final_semantics_and_rgba(self):
        old_images, new_images = LogicalImages(BASELINE), LogicalImages(GENERATED)
        compared = compared_maps = 0
        old_borders = {read(p)['mapPath']: (p, read(p))
                       for p in (BASELINE / 'maps/borders').glob('*/border.json')}
        for row in self.selection:
            with self.subTest(path=row['path']):
                old = read(BASELINE / row['path'])
                new = self.docs[row['path']]
                if row['path'] in old_borders:
                    border_path, border_doc = old_borders[row['path']]
                    extra = []
                    for art in border_doc['artifacts']:
                        suffix = 'border.png' if art['path'] == 'map.png' else 'border-' + art['path']
                        extra.append(dict(art, path=Path(row['path']).stem + '.' + suffix))
                    # Only ownership/path of already-consumed opening border art changes.
                    self.assertEqual(old['artifacts'] + extra, new['artifacts'])
                    self.assertEqual(old, dict(new, artifacts=old['artifacts']))
                    for before_art, after_art in zip(border_doc['artifacts'], extra):
                        before_path = (border_path.parent / before_art['path']).relative_to(BASELINE).as_posix()
                        after_path = (Path(row['path']).parent / after_art['path']).as_posix()
                        before, after = old_images.read(before_path), new_images.read(after_path)
                        self.assertEqual(before.size, after.size, after_path)
                        self.assertEqual(before.tobytes(), after.tobytes(), after_path)
                        compared += 1
                else:
                    self.assertEqual(old, new)
                for art in old['artifacts']:
                    path = (Path(row['path']).parent / art['path']).as_posix()
                    before, after = old_images.read(path), new_images.read(path)
                    self.assertEqual(before.size, after.size, path)
                    self.assertEqual(before.tobytes(), after.tobytes(), path)
                    compared += 1
                compared_maps += 1
        self.assertEqual(len(self.selection), compared_maps)
        self.assertGreater(compared, 0)
        print(f'Previous-final comparison: {compared_maps} map payloads, {compared} exact RGBA images')


    def assert_previous_final_npc_frames(self):
        old_root = BASELINE / 'sprites/overworld'
        new_root = NPC_GENERATED / 'sprites/overworld'
        old, new = read(old_root / 'catalog.json'), read(new_root / 'catalog.json')
        self.assertTrue(old)
        self.assertEqual(set(old), set(new))
        frames = 0
        for graphics_id in sorted(old):
            with self.subTest(graphics=graphics_id):
                before, after = old[graphics_id], new[graphics_id]
                semantic = lambda row: {k: v for k, v in row.items() if k not in ('texturePath', 'region')}
                self.assertEqual(semantic(before), semantic(after))
                strips = []
                for root, row in ((old_root, before), (new_root, after)):
                    region = row['region']
                    self.assertEqual((row['frameWidth'] * row['frameCount'], row['frameHeight']),
                                     (region['width'], region['height']))
                    with Image.open(root / row['texturePath']) as page:
                        self.assertGreaterEqual(region['x'], 0)
                        self.assertGreaterEqual(region['y'], 0)
                        self.assertLessEqual(region['x'] + region['width'], page.width)
                        self.assertLessEqual(region['y'] + region['height'], page.height)
                        strips.append(page.convert('RGBA').crop((region['x'], region['y'],
                            region['x'] + region['width'], region['y'] + region['height'])))
                for frame in range(before['frameCount']):
                    box = (frame * before['frameWidth'], 0, (frame + 1) * before['frameWidth'], before['frameHeight'])
                    self.assertEqual(strips[0].crop(box).tobytes(), strips[1].crop(box).tobytes(),
                                     f'{graphics_id} frame {frame}: palette-resolved RGBA differs')
                    frames += 1
        # The opening player has deliberately different palette treatment and is
        # consumed directly, outside the NPC catalog.
        with Image.open(old_root / 'opening-player.png') as before, Image.open(new_root / 'opening-player.png') as after:
            self.assertEqual(before.size, after.size)
            self.assertEqual(before.convert('RGBA').tobytes(), after.convert('RGBA').tobytes())
        print(f'Previous-final NPC comparison: {len(old)} logical graphics, {frames} exact RGBA frames; anchors/animation semantics and opening-player pixels equal')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated', type=Path, default=GENERATED)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--npc-generated', type=Path, help='Also compare raw-rebuilt NPC logical sprites in this generated root')
    args, remaining = parser.parse_known_args()
    GENERATED, BASELINE, NPC_GENERATED = args.generated, args.baseline, args.npc_generated
    if NPC_GENERATED is not None and BASELINE is None:
        parser.error('--npc-generated requires --baseline')
    if BASELINE is not None:
        WorldPipelineOracle.test_previous_final_semantics_and_rgba = WorldPipelineOracle.assert_previous_final_semantics_and_rgba
    if NPC_GENERATED is not None:
        WorldPipelineOracle.test_previous_final_npc_frames = WorldPipelineOracle.assert_previous_final_npc_frames
    unittest.main(argv=[__file__, *remaining])
