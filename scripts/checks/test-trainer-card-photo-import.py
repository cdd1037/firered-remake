#!/usr/bin/env python3
"""Source-authored card colors, sticker pixels and original printer script contracts."""
from pathlib import Path
import importlib.util
import json
import unittest
import sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
RAW=ROOT/'content/raw/pokefirered/037335f4'
sys.path.insert(0,str(ROOT/'scripts/lib'))
import ui_tiles

class TrainerCardPhotoImportTests(unittest.TestCase):
    def test_source_sticker_pixels_and_star(self):
        raw=Image.open(RAW/'graphics/trainer_card/stickers.png')
        data=raw.tobytes()
        for kind in range(2):
            for tier in range(1,5):
                pal=ui_tiles.palette((RAW/f'graphics/trainer_card/stickers{tier}.pal').read_bytes())
                expected=bytes(v for y in range(16) for x in range(16)
                    for v in (*pal[data[(kind*16+y)*16+x]],255 if data[(kind*16+y)*16+x] else 0))
                actual=Image.open(ROOT/f'content/generated/ui/trainer-card/sticker-{kind}-{tier}.png').convert('RGBA').tobytes()
                self.assertEqual(expected,actual)
        tiles=ui_tiles.tile_list((RAW/'graphics/trainer_card/tiles.png').read_bytes())
        expected=ui_tiles.rgba(tiles[143],ui_tiles.palette((RAW/'graphics/trainer_card/star.pal').read_bytes()))
        self.assertEqual(expected,Image.open(ROOT/'content/generated/ui/trainer-card/star.png').convert('RGBA').tobytes())

    def test_original_printer_payment_tint_flash_and_snapshot_order(self):
        data=json.loads((ROOT/'content/generated/data/story.json').read_text())['Compilation']
        self.assertEqual(['NORMAL','BLACK','PINK','SEPIA'],[r['Text'] for r in data['Menus']['MULTICHOICE_TRAINER_CARD_ICON_TINT']['Options']])
        programs=data['Programs'];p=programs['CeladonCity_GameCorner_EventScript_PhotoPrinter']
        instructions=p['Instructions']
        def index(op):return next(i for i,v in enumerate(instructions) if v['Opcode']==op)
        self.assertLess(index('checkmoney'),index('removemoney'))
        self.assertLess(index('removemoney'),index('multichoice'))
        menu=instructions[index('multichoice')]
        self.assertEqual(['21','0','MULTICHOICE_TRAINER_CARD_ICON_TINT','TRUE'],menu['Args'])
        take=programs['CeladonCity_GameCorner_EventScript_PrintPhoto']['Instructions']
        self.assertEqual(['FLDEFF_PHOTO_FLASH'],next(i for i in take if i['Opcode']=='dofieldeffect')['Args'])
        self.assertEqual(['60'],next(i for i in take if i['Opcode']=='delay')['Args'])
        self.assertEqual(['UpdateTrainerCardPhotoIcons'],next(i for i in take if i['Opcode']=='special')['Args'])
        self.assertFalse([v for v in data['UnsupportedInstructions'] if 'trainer_card.inc' in str(v)])

if __name__=='__main__':unittest.main()
