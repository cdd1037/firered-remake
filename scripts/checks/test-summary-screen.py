#!/usr/bin/env python3
"""Focused Summary conversion checks; no engine/audio process is started."""
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('summary_import', ROOT/'scripts/imports/presentation/import-summary-screen.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
RAW=ROOT/'content/raw/pokefirered/037335f4'

class SummaryScreenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.outputs=module.convert(RAW)
    def image(self,name):return Image.open(io.BytesIO(self.outputs[name])).convert('RGBA')
    def test_generated_outputs_reproduce_exactly(self):
        for name,data in self.outputs.items():
            self.assertEqual(data,(ROOT/'content/generated/ui/summary-screen'/name).read_bytes(),name)
    def test_five_native_pages_have_complete_canvas(self):
        for page in module.PAGES:
            im=self.image(page+'.png');self.assertEqual(im.size,(240,160));self.assertEqual(im.getextrema()[3],(255,255))
    def test_shiny_palette_is_distinct(self):
        for page in module.PAGES:
            self.assertNotEqual(self.outputs[page+'.png'],self.outputs[page+'-shiny.png'])
    def test_move_details_retain_right_hand_move_rows(self):
        a=self.image('moves.png');b=self.image('moves_info.png')
        self.assertEqual(a.crop((120,18,240,128)).tobytes(),b.crop((120,18,240,128)).tobytes())
        self.assertNotEqual(a.crop((0,56,120,160)).tobytes(),b.crop((0,56,120,160)).tobytes())
    def test_native_move_cursors_have_two_paletted_frames(self):
        for side in ('left','right'):
            im=self.image('cursor-'+side+'.png');self.assertEqual(im.size,(64,64));self.assertEqual(im.getextrema()[3],(0,255))
            self.assertNotEqual(im.crop((0,0,64,32)).tobytes(),im.crop((0,32,64,64)).tobytes())
    def test_source_move_and_ability_descriptions(self):
        text=json.loads(self.outputs['text.json'])
        self.assertEqual(text['abilities']['ABILITY_OVERGROW'],'Ups GRASS moves in a pinch.')
        self.assertIn('cut down thin trees',text['moves']['MOVE_CUT'])
        self.assertGreater(len(text['moves']),300);self.assertGreater(len(text['abilities']),70)
    def test_display_names_preserve_source_abbreviations_and_empty_values(self):
        names=json.loads(self.outputs['display-names.json'])
        for group,symbol,value in [('types','TYPE_FIGHTING','FIGHT'),('types','TYPE_ELECTRIC','ELECTR'),
                                   ('types','TYPE_PSYCHIC','PSYCHC'),('types','TYPE_MYSTERY','???'),
                                   ('abilities','ABILITY_NONE','-------'),('abilities','ABILITY_COMPOUND_EYES','COMPOUNDEYES'),
                                   ('abilities','ABILITY_LIGHTNING_ROD','LIGHTNINGROD')]:
            with self.subTest(symbol=symbol):self.assertEqual(names[group][symbol],value)
        self.assertEqual(names['noHeldItem'],'NONE')
        self.assertNotIn('items',names)
    def test_name_tables_reject_missing_duplicate_and_unparsed_rows(self):
        for body in ['', '[TYPE_FIRE] = _("FIRE"), [TYPE_FIRE] = _("FIRE"),',
                     '[TYPE_FIRE] = _(" "),', '[TYPE_FIRE] = UNREVIEWED_MACRO,']:
            with self.subTest(body=body),self.assertRaises(ValueError):
                module.source_name_table('const u8 gTypeNames[1][8] = {\n'+body+'\n};','gTypeNames','TYPE_')
    def test_species_facing_does_not_merge_inline_none_into_bulbasaur(self):
        facing=json.loads(self.outputs['text.json'])['facing']
        self.assertNotIn('SPECIES_NONE',facing)
        self.assertTrue(facing['SPECIES_BULBASAUR'])
        self.assertTrue(facing['SPECIES_MEWTWO'])
        self.assertFalse(facing['SPECIES_UNOWN'])
        self.assertEqual(len(facing),386)
    def test_hp_and_exp_use_native_twelve_tile_strips(self):
        for name in ('hp-green','hp-yellow','hp-red','exp'):
            self.assertEqual(self.image(name+'.png').size,(96,8))
        self.assertNotEqual(self.outputs['hp-red.png'],self.outputs['hp-green.png'])
    def test_missing_inputs_fail_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(FileNotFoundError):module.convert(Path(directory))

if __name__=='__main__':unittest.main()
