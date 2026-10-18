#!/usr/bin/env python3
"""Profile/Mail template source closure, static menu and settled pixel checks."""
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import unittest
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from easy_chat_templates import templates, profile, layout
from story_menus import extract_menus
from world_source import WorldSource
import ui_tiles

RAW = ROOT / 'content/raw/pokefirered/037335f4'


def load(relative):
    spec = importlib.util.spec_from_file_location(Path(relative).stem, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class OverlaySource:
    def __init__(self, replacements):
        self.replacements = replacements
    def text(self, name):
        return self.replacements.get(name, (RAW / name).read_text())


class EasyChatProfileImportTests(unittest.TestCase):
    def test_two_templates_use_source_geometry_texts_and_actual_word_count(self):
        source = WorldSource(RAW)
        p, m = templates(source)
        self.assertEqual((p['id'], p['type'], p['columns'], p['rows'], p['wordCount'], p['frameId']), ('profile', 0, 2, 2, 4, 0))
        self.assertEqual((m['id'], m['type'], m['columns'], m['rows'], m['wordCount'], m['frameId']), ('mail', 4, 2, 5, 9, 2))
        self.assertEqual(p['titleTextSymbol'], 'gText_Profile')
        self.assertIsNone(m['titleTextSymbol'])
        self.assertEqual(p['cancelTextSymbols'], ['gText_QuitEditing'])
        self.assertEqual(m['cancelTextSymbols'], ['gText_StopGivingPkmnMail'])
        definitions = load('scripts/imports/story/import-mail-composer.py').contracts(source)
        texts = {row['symbol']: row['text'] for row in definitions['texts']}
        self.assertEqual([texts[s] for s in p['instructionTextSymbols']], ['Combine four words or phrases', 'and make your profile.'])
        self.assertEqual([texts[s] for s in p['confirmTextSymbols']], ['Your profile', 'is as shown. Okay?'])
        self.assertEqual(texts['gText_QuitEditing'], 'Quit editing?')
        words = {w['value']: w['text'] for w in load('scripts/imports/story/import-mail-composer.py').vocabulary(source)['words']}
        self.assertEqual([words[v] for v in profile(source)['defaultWords']], ['I AM', 'A', 'POKéMON', 'FRIEND'])
        self.assertEqual([words[v] for v in profile(source)['specialPhrase']], ['MYSTERY', 'EVENT', 'IS', 'EXCITING'])

    def test_template_and_phrase_values_come_from_supplied_source(self):
        path = 'src/easy_chat.c'
        changed = OverlaySource({path: (RAW / path).read_text().replace('EC_WORD_FRIEND,', 'EC_WORD_YOU,', 1)})
        self.assertNotEqual(profile(WorldSource(RAW))['defaultWords'], profile(changed)['defaultWords'])
        path = 'src/easy_chat_3.c'
        changed = OverlaySource({path: (RAW / path).read_text().replace('.left = 3,\n        .top = 4,', '.left = 4,\n        .top = 4,', 1)})
        self.assertEqual(layout(changed)[0]['fieldX'], 49)
        path = 'src/easy_chat_2.c'
        changed = OverlaySource({path: (RAW / path).read_text().replace('.type = EASY_CHAT_TYPE_PROFILE,', '.type = EASY_CHAT_TYPE_BATTLE_START,', 1)})
        with self.assertRaisesRegex(ValueError, 'Missing or duplicate'):
            templates(changed)

    def test_profile_menu_is_the_shared_source_static_menu(self):
        rows = {row['id']: row for row in extract_menus(RAW)}
        menu = rows['MULTICHOICE_YES_NO_INFO_2']
        self.assertEqual(menu['sourceId'], 18)
        self.assertEqual([o['text'] for o in menu['options']], ['YES', 'NO', 'INFO'])
        self.assertNotEqual(menu['sourceId'], rows['MULTICHOICE_YES_NO_INFO']['sourceId'])
        recipe = json.loads((ROOT / 'tools/import-story/recipe.json').read_text())
        self.assertIn('EASY_CHAT_TYPE_PROFILE', recipe['constantSymbols'])
        self.assertIn('data/scripts/mystery_event_club.inc', [r['path'] for r in recipe['scripts']])

    def test_profile_and_mail_surfaces_match_source_pixel_layers(self):
        outputs = load('scripts/imports/presentation/import-mail-presentation.py').generate(RAW)
        catalog = json.loads(outputs['ui/mail-presentation/catalog.json'])
        self.assertEqual(catalog['easyChat']['titleRgb'], [57, 206, 255])
        self.assertEqual(catalog['easyChat']['titleShadowRgb'], [173, 173, 239])
        w, h, indices, palette = ui_tiles.indexed_png((RAW / 'graphics/easy_chat/window.png').read_bytes())
        tiles = ui_tiles.tile_list((RAW / 'graphics/easy_chat/window.png').read_bytes())
        frame = ui_tiles.tile_list((RAW / 'graphics/easy_chat/text_input_frame.png').read_bytes())
        colors = ui_tiles.colors(palette)
        orange = ui_tiles.palette((RAW / 'graphics/easy_chat/text_input_frame_orange.pal').read_bytes())
        green = ui_tiles.palette((RAW / 'graphics/easy_chat/text_input_frame_green.pal').read_bytes())
        tilemap = struct.unpack('<640H', (RAW / 'graphics/easy_chat/window.bin').read_bytes())
        def pixel(tile, palette, x, y):
            value = tile[y % 8 * 8 + x % 8]
            return (*palette[value], 255) if value else None
        for template in catalog['templates']:
            left, top, width, height = [template['frame' + name] for name in ('Left', 'Top', 'Width', 'Height')]
            self.assertEqual((template['fieldX'], template['fieldY']), (left * 8 + 17, top * 8 + 2))
            self.assertEqual((template['titleX'], template['titleY'], template['titleWidth']), (56, 2, 128))
            for mode, identifier in template['backgroundIds'].items():
                actual = Image.open(io.BytesIO(outputs['ui/mail-presentation/' + identifier + '.png'])).convert('RGBA')
                expected = bytearray()
                for y in range(160):
                    for x in range(240):
                        tx, ty = x // 8, y // 8
                        entry = tilemap[ty * 32 + tx]
                        px, py = (7 - x % 8 if entry & 1024 else x % 8), (7 - y % 8 if entry & 2048 else y % 8)
                        color = (*colors[tiles[entry & 1023][py * 8 + px]], 255)
                        if left <= tx < left + width and top <= ty < top + height or 32 <= x < 224 and 88 <= y < 104:
                            color = (255, 255, 255, 255)
                        n = None
                        if top <= ty < top + height and left - 1 <= tx <= left + width:
                            n = 5 if tx == left - 1 else 7 if tx == left + width else 0
                        elif template['id'] == 'profile' and ty in (top - 1, top + height) and left - 1 <= tx <= left + width:
                            n = (1 if tx == left - 1 else 3 if tx == left + width else 2) + (8 if ty == top + height else 0)
                        if n is not None:
                            color = pixel(frame[n], orange, x, y) or color
                        if mode != 'fields' and ty >= 10:
                            l, r = (1, 23) if mode == 'groups' else (0, 29)
                            if l < tx < r and 88 <= y < 152: color = (255, 255, 255, 255)
                            if l <= tx <= r:
                                n = (1 if tx == l else 3 if tx == r else 2) if ty == 10 else (9 if tx == l else 11 if tx == r else 10) if ty == 19 else (5 if tx == l else 7 if tx == r else 0)
                                color = pixel(frame[n], green, x, y) or color
                        expected.extend(color)
                self.assertEqual(actual.size, (240, 160))
                self.assertEqual(actual.tobytes(), bytes(expected), identifier)
        self.assertEqual(len([a for a in catalog['assets'] if a['id'].startswith('easy-chat-profile-')]), 3)
        self.assertFalse(any('profile' in path and not path.startswith('ui/mail-presentation/') for path in outputs))


if __name__ == '__main__':
    unittest.main()
