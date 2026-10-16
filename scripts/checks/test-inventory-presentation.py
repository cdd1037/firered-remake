#!/usr/bin/env python3
"""Independent pixel oracle and input-boundary checks for TM Case and PC hand assets."""
import importlib.util
import io
import json
import struct
import sys
import unittest
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/lib'))
from world_source import WorldSource
spec=importlib.util.spec_from_file_location('inventory',ROOT/'scripts/imports/presentation/import-inventory-presentation.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
RAW=ROOT/'content/raw/pokefirered/037335f4'


def palette(path):
    rows=(RAW/path).read_text().splitlines()[3:]
    return [tuple((int(c)>>3)*8+(int(c)>>5) for c in row.split()) for row in rows]


def source_image(path):
    image=Image.open(RAW/path)
    values=list(image.get_flattened_data())
    return image.size, [15-v//17 if image.mode=='L' else v&15 for v in values]


class InventoryPresentationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.outputs=module.generate(WorldSource(RAW))
        cls.document=json.loads(cls.outputs['inventory.json'])

    def decoded(self,name): return Image.open(io.BytesIO(self.outputs[name+'.png'])).convert('RGBA')

    def test_every_case_pixel_matches_independent_tile_bank_flip_and_transparency_oracle(self):
        (tw,th),indices=source_image('graphics/tm_case/tm_case.png')
        for gender in ('male','female'):
            colors=palette('graphics/tm_case/menu_'+gender+'.pal')
            for mode,binname in [('', 'menu.bin'),('-shaded','menu.bin'),('-front','tm_case.bin')]:
                words=struct.unpack('<1024H',(RAW/'graphics/tm_case'/binname).read_bytes())
                expected=[]
                for y in range(160):
                    for x in range(240):
                        word=words[(y//8)*32+x//8];tile=word&1023
                        bank=3 if mode=='-shaded' and y>=96 else word>>12
                        sx=tile%8*8+(7-x%8 if word&1024 else x%8)
                        sy=tile//8*8+(7-y%8 if word&2048 else y%8)
                        index=indices[sy*tw+sx]
                        expected.append((*colors[bank*16+index],255) if index else (0,0,0,0) if mode=='-front' else (*colors[0],255))
                self.assertEqual(expected,list(self.decoded('case-'+gender+mode).get_flattened_data()))
        self.assertNotEqual(self.outputs['case-male.png'],self.outputs['case-female.png'])
        self.assertNotEqual(self.outputs['case-male.png'],self.outputs['case-male-shaded.png'])

    def test_disc_and_hand_each_pixel_uses_source_palette_and_frame(self):
        colors=palette('graphics/tm_case/disc_types_1.pal')+palette('graphics/tm_case/disc_types_2.pal')
        _,disc=source_image('graphics/tm_case/disc.png')
        for type_index in range(17):
            expected=[(*colors[type_index*16+i],0 if i==0 else 255) for i in disc]
            self.assertEqual(expected,list(self.decoded('discs').crop((type_index*32,0,type_index*32+32,64)).get_flattened_data()))
        for name,source,pal in [('hand-single','cursor','misc2'),('hand-multi','cursor','misc1'),('hand-shadow','cursor_shadow','misc2')]:
            _,pixels=source_image('graphics/pokemon_storage/'+source+'.png')
            colors=palette('graphics/pokemon_storage/'+pal+'.pal')
            self.assertEqual([(*colors[i],0 if i==0 else 255) for i in pixels],list(self.decoded(name).get_flattened_data()))
        self.assertEqual([0,3,6,8,11,13,14,15,16,15,14,13,11,8,6,3,0],self.document['shiftOffsets'])
        expected=[]
        for r,g,b,a in self.decoded('discs').get_flattened_data():
            channels=[(c>>3)*3//4 for c in (r,g,b)]
            expected.append(tuple((c<<3)|(c>>2) for c in channels)+(a,))
        self.assertEqual(expected,list(self.decoded('discs-tutorial').get_flattened_data()))

    def test_generated_only_outputs_match_current_and_contain_no_provenance(self):
        for name,data in self.outputs.items():
            self.assertEqual(data,(ROOT/'content/generated/ui/inventory'/name).read_bytes(),name)
        text=self.outputs['inventory.json'].decode()
        for forbidden in ['sourceCommit','sourcePath','sha256','037335f4','content/raw','lock','history']:
            self.assertNotIn(forbidden,text)
        self.assertEqual({'schemaVersion','assets','discTypes','shiftOffsets','arrowOffsets','statForeground','statShadow'},set(self.document))

    def test_out_of_range_tile_palette_and_changed_animation_fail_closed(self):
        tiles=Image.new('P',(8,8));colors=[(0,0,0)]*64
        with self.assertRaises(ValueError):module.background(tiles,struct.pack('<1024H',*([1]*1024)),colors)
        with self.assertRaises(ValueError):module.background(tiles,struct.pack('<1024H',*([0x4000]*1024)),colors)
        with self.assertRaises(ValueError):module.background(tiles,b'',colors)
        class Altered(WorldSource):
            def text(self,path):
                text=super().text(path)
                return text.replace('#define DISC_Y_MOVE 10','#define DISC_Y_MOVE 11') if path=='src/tm_case.c' else text
        with self.assertRaises(ValueError):module.generate(Altered(RAW))


if __name__=='__main__': unittest.main(verbosity=2)
