#!/usr/bin/env python3
"""Independent source-pixel checks for native Pokédex UI components."""
import importlib.util
import io
import json
from pathlib import Path
import re
import struct
import sys
import unittest
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
RAW=ROOT/'content/raw/pokefirered/037335f4'
OUT=ROOT/'content/generated/sprites/pokemon/pokedex-menu'
sys.path.insert(0,str(ROOT/'scripts/imports/presentation'))
from pokedex_visual_data import build_visuals

class PokedexVisualTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec=importlib.util.spec_from_file_location('dex_visual_import',ROOT/'scripts/imports/presentation/import-pokedex-menu.py')
        cls.menu=importlib.util.module_from_spec(spec);spec.loader.exec_module(cls.menu)
        cls.sources=cls.menu.read_sources(RAW)
        cls.visuals,cls.outputs,_=build_visuals(cls.sources)
        cls.atlas=Image.open(io.BytesIO(cls.outputs['visual-atlas.png'])).convert('RGBA')
    def sprite(self,key):
        s=self.visuals['sprites'][key];x,y=s['sourceX'],s['sourceY']
        return self.atlas.crop((x,y,x+s['width'],y+s['height']))
    @staticmethod
    def pal(path):
        return [tuple((int(c)//8)*8+(int(c)//8)//4 for c in row.split()) for row in (RAW/path).read_text().splitlines()[3:]]
    @staticmethod
    def source_blit(raw,offset,width,height,pal):
        # Independent oracle: pack actual GBA tile memory, then execute the pinned
        # blit.c nibble-address equation against its shifted pointer (32 bytes/tile).
        packed=bytearray()
        for ty in range(0,raw.height,8):
            for tx in range(0,raw.width,8):
                for y in range(8):
                    for x in range(0,8,2):
                        a,b=raw.getpixel((tx+x,ty+y)),raw.getpixel((tx+x+1,ty+y))
                        if raw.mode=='P':a,b=a&15,b&15
                        else:a,b=15-a//17,15-b//17
                        packed.append(a|(b<<4))
        pixels=[]
        for y in range(height):
            for x in range(width):
                address=offset*32+((x>>1)&3)+((x>>3)<<5)+(((y>>3)*16)<<5)+(y&7)*4
                index=(packed[address]>>((x&1)*4))&15
                pixels.extend((*pal[index],0 if index==0 else 255))
        return bytes(pixels)
    def test_all_type_badges_match_source_tile_memory_blitter(self):
        raw=Image.open(RAW/'graphics/interface/menu_info.png');pal=self.pal('graphics/interface/pokemon_types.pal')
        rows=re.findall(r'\[(TYPE_\w+) \+ 1\]\s*=\s*\{\s*(\d+),\s*(\d+),\s*(0x[0-9A-Fa-f]+)\s*\}',(RAW/'src/list_menu.c').read_text())
        self.assertEqual(18,len(rows))
        for key,w,h,offset in rows:
            expected=self.source_blit(raw,int(offset,16),int(w),int(h),pal)
            self.assertEqual(expected,self.sprite(key).tobytes(),key)
        # Explicit independently inspected source-sheet anchor: PSYCHIC is the
        # second badge at y64, not the POISON badge at x0 or a two-row shift.
        psychic=raw.crop((32,64,64,76))
        expected=bytes(c for v in psychic.tobytes() for i in [v&15] for c in (*pal[i],0 if i==0 else 255))
        self.assertEqual(expected,self.sprite('TYPE_PSYCHIC').tobytes())
        self.assertNotEqual(self.sprite('TYPE_POISON').tobytes(),self.sprite('TYPE_PSYCHIC').tobytes())
    def test_all_circle_tiles_flips_and_selected_colors(self):
        words=struct.unpack('<64H',(RAW/'graphics/pokedex/page_icon_tilemap.bin').read_bytes())
        for mode in ['kanto','national']:
            raw=Image.open(RAW/f'graphics/pokedex/{mode}_dex_bgtiles.png');pal=self.pal(f'graphics/pokedex/{mode}_dex_bgpals.pal')[80:96]
            pal[2]=(198,181,140);pal[8]=(214,198,165)
            for y in range(64):
                for x in range(64):
                    word=words[y//8*8+x//8];n=word&1023;dx=x%8;dy=y%8
                    if word&1024:dx=7-dx
                    if word&2048:dy=7-dy
                    index=raw.getpixel((n%(raw.width//8)*8+dx,n//(raw.width//8)*8+dy))&15
                    self.assertEqual((*pal[index],255),self.sprite(mode+'/circle/0').getpixel((x,y)))
    def test_all_keypad_glyphs_match_source_tile_memory_blitter(self):
        raw=Image.open(RAW/'graphics/fonts/keypad_icons.png');pal=self.pal('graphics/pokedex/kanto_dex_bgpals.pal')
        rows=re.findall(r'\[CHAR_(\w+)\]\s*=\s*\{\s*(0x[0-9A-Fa-f]+),\s*(\d+),\s*(\d+)\s*\}',(RAW/'src/text.c').read_text())
        self.assertEqual(13,len(rows))
        for key,offset,w,h in rows:
            expected=self.source_blit(raw,int(offset,16),int(w),int(h),pal)
            self.assertEqual(expected,self.sprite('kanto/key/'+key).tobytes(),key)
    def test_window_and_category_zero_indices_are_transparent(self):
        for key,path in [('kanto/mini','mini_page.png'),('national/mini','mini_page.png'),
                         ('category/GrasslandPkmn','cat_icon_grassland.png')]:
            raw=Image.open(RAW/'graphics/pokedex'/path)
            expected=[0 if (v&15)==0 else 255 for v in raw.tobytes()]
            self.assertEqual(expected,list(self.sprite(key).getchannel('A').tobytes()))

    def test_source_page_columns_and_regeneration(self):
        self.assertEqual(2,self.visuals["fadeStep"]);self.assertEqual(23,self.visuals["fadeFrames"])
        columns=self.visuals['pageTurnColumns']
        self.assertEqual([30]*30,columns[0]);self.assertEqual(list(range(30)),columns[9])
        self.assertEqual([5,11,17,23,29]+[30]*25,columns[1])
        self.assertEqual(self.outputs,build_visuals(self.sources)[1])
        self.assertEqual((OUT/'visual-atlas.png').read_bytes(),self.outputs['visual-atlas.png'])
        self.assertEqual(json.loads((OUT/'pokedex-menu.json').read_text())['visuals'],self.visuals)

if __name__=='__main__':unittest.main()
