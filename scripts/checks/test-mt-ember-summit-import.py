#!/usr/bin/env python3
"""Pinned summit geometry and source-palette regressions, entirely offline."""
import json
from pathlib import Path
import re
import struct
import sys
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from map_runtime import runtime_cells

RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'
MAPS = ('MtEmber_SummitPath_1F', 'MtEmber_SummitPath_2F', 'MtEmber_SummitPath_3F', 'MtEmber_Summit')


def slug(name):
    return re.sub(r'([a-z])([A-Z])', r'\1-\2', name).replace('_', '-').lower()


class MtEmberSummitImportTests(unittest.TestCase):
    def test_runtime_map_events_and_every_block_match_pinned_inputs(self):
        layouts = json.loads((RAW / 'data/layouts/layouts.json').read_text())['layouts']
        for name in MAPS:
            with self.subTest(map=name):
                source = json.loads((RAW / f'data/maps/{name}/map.json').read_text())
                layout = next(row for row in layouts if row.get('id') == source['layout'])
                actual = json.loads((GENERATED / f'maps/regions/sevii-ruby/{slug(name)}.json').read_text())
                self.assertEqual(source, actual['events'])
                self.assertEqual((layout['width'], layout['height']), (actual['width'], actual['height']))
                for border in (False, True):
                    path = layout['border_filepath' if border else 'blockdata_filepath']
                    expected = [block for (block,) in struct.iter_unpack('<H', (RAW / path).read_bytes())]
                    cells = list(runtime_cells(actual, border=border))
                    self.assertEqual(expected, [cell['rawBlock'] for cell in cells])
                    self.assertTrue(cells)

    def test_rock_smash_templates_match_shared_field_catalog(self):
        source = json.loads((RAW / 'data/maps/MtEmber_SummitPath_2F/map.json').read_text())
        field = json.loads((GENERATED / 'data/services/field-rock-smash/field.json').read_text())
        rocks = [row for row in field['rocks'] if row['sourceMapSymbol'] == source['id']]
        self.assertEqual(list(range(1, 10)), [row['sourceLocalId'] for row in rocks])
        self.assertEqual([f'FLAG_TEMP_{flag:X}' for flag in range(0x12, 0x1b)], [row['hideFlag'] for row in rocks])
        for actual, event in zip(rocks, source['object_events']):
            for field_name, source_name in [('x', 'x'), ('y', 'y'), ('elevation', 'elevation'),
                ('graphicsId', 'graphics_id'), ('scriptId', 'script'), ('movementType', 'movement_type'),
                ('movementRangeX', 'movement_range_x'), ('movementRangeY', 'movement_range_y'), ('hideFlag', 'flag')]:
                self.assertEqual(event[source_name], actual[field_name])

    def test_moltres_overworld_uses_npc_blue_and_all_nine_source_frames(self):
        info = (RAW / 'src/data/object_events/object_event_graphics_info.h').read_text()
        body = re.search(r'\bgObjectEventGraphicsInfo_Moltres = \{(.*?)\n\};', info, re.S)[1]
        self.assertIn('.paletteTag = OBJ_EVENT_PAL_TAG_NPC_BLUE,', body)
        self.assertIn('.width = 32,', body)
        self.assertIn('.height = 32,', body)
        tables = (RAW / 'src/data/object_events/object_event_pic_tables.h').read_text()
        frames = re.search(r'\bsPicTable_Moltres\[\] = \{(.*?)\n\};', tables, re.S)[1]
        indices = [int(index) for index in re.findall(r'overworld_frame\(gObjectEventPic_Moltres, 4, 4, (\d+)\)', frames)]
        self.assertEqual([0] * 9, indices)
        graphics = (RAW / 'src/data/object_events/object_event_graphics.h').read_text()
        movement = (RAW / 'src/event_object_movement.c').read_text()
        palette = re.search(r'\{\s*(\w+)\s*,\s*OBJ_EVENT_PAL_TAG_NPC_BLUE\s*\}', movement)[1]
        path = re.search(r'\b' + palette + r'\[\] = INCBIN_U16\("(.*?)"\)', graphics)[1].replace('.gbapal', '.pal')
        colors = [tuple((int(value) >> 3) * 8 + (int(value) >> 5) for value in row.split())
                  for row in (RAW / path).read_text().splitlines()[3:]]
        self.assertEqual(16, len(colors))
        with Image.open(RAW / 'graphics/object_events/pics/pokemon/moltres.png') as image:
            self.assertEqual('P', image.mode)
            self.assertEqual((32, 32), image.size)
            frame = Image.new('RGBA', image.size)
            frame.putdata([(*colors[value], 0 if value == 0 else 255) for value in image.get_flattened_data()])
            expected = Image.new('RGBA', (32 * len(indices), 32))
            for index in range(len(indices)): expected.paste(frame, (32 * index, 0))
        sprite = json.loads((GENERATED / 'sprites/overworld/catalog.json').read_text())['OBJ_EVENT_GFX_MOLTRES']
        self.assertEqual((32, 32, 9, 16, 24), tuple(sprite[key] for key in ('frameWidth', 'frameHeight', 'frameCount', 'anchorX', 'anchorY')))
        region = sprite['region']
        with Image.open(GENERATED / 'sprites/overworld' / sprite['texturePath']) as atlas:
            actual = atlas.convert('RGBA').crop((region['x'], region['y'], region['x'] + region['width'], region['y'] + region['height']))
        self.assertEqual(expected.size, actual.size)
        self.assertEqual(expected.tobytes(), actual.tobytes())


if __name__ == '__main__':
    unittest.main()
