#!/usr/bin/env python3
"""Assist/Sweet Scent complete discovery, exact materials, and prior-row preservation."""
import sys
sys.dont_write_bytecode=True
import argparse,hashlib,io,json,struct,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);p.add_argument('--previous',type=Path);p.add_argument('--importer',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=a.root.resolve();a.reference=a.reference or ROOT/'content/raw/pokefirered/037335f4';a.generated=a.generated or ROOT/'content/generated/battle/animations';path=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
# The override supports external integration staging while resolving all shared
# producer dependencies from the selected checkout. No importer is duplicated.
imp=types.ModuleType('petal_paw_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
NAMES={'gAssistPawprintSpriteTemplate','gSweetScentPetalSpriteTemplate'}
class PetalPawImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_exactly_two_callback_roots_and_existing_rows_preserved(self):
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):old_data,old_pixels=imp.build(a.reference)
  old=json.loads(old_data);roots={r['moveId']:r for r in old['scripts']};self.assertEqual({230,274},set(self.roots)-set(roots))
  for key,value in roots.items():self.assertEqual(value,self.roots[key])
  recipes={r['template']:r for r in old['spriteRecipes']};self.assertEqual(NAMES,set(self.recipes)-set(recipes))
  for key,value in recipes.items():self.assertEqual(value,self.recipes[key])
  self.assertEqual(old_pixels,self.pixels)
 def test_authored_helper_palette_and_sound_programs(self):
  paw=self.roots[274];petal=self.roots[230];self.assertEqual('Move_ASSIST',paw['label']);self.assertEqual('Move_SWEET_SCENT',petal['label'])
  self.assertEqual([[str(n) for n in v] for v in [[112,-16,140,128,36],[208,128,-16,48,36],[-16,112,256,-16,36],[108,128,84,-16,36],[-16,56,256,56,36]]],[c['args'][3:] for c in paw['commands'] if c['op']=='createsprite'])
  self.assertEqual(4,sum(c['op']=='playsewithpan' for c in paw['commands']));self.assertEqual('end',paw['commands'][-1]['op']);self.assertEqual({'SE_M_SCRATCH':148},paw['soundEffects'])
  self.assertEqual([['0'],['SOUND_PAN_TARGET']],[c['args'] for c in petal['commands'] if c['op']=='setpan']);self.assertEqual([['SweetScentEffect'],['SweetScentEffect']],[c['args'] for c in petal['commands'] if c['op']=='call'])
  self.assertEqual([['AnimTask_BlendColorCycle','2','F_PAL_DEF_SIDE','1','5','5','13','RGB(31, 21, 21)']],[c['args'] for c in petal['commands'] if c['op']=='createvisualtask']);self.assertEqual(11,sum(c['op']=='createsprite' for c in self.pack['subroutines']['SweetScentEffect']));self.assertEqual({'SE_M_SWEET_SCENT':229},petal['soundEffects'])
 def test_missing_palette_task_closes_sweet_scent(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_BlendColorCycle'}):rows,_=imp.discover_scripts(a.reference)
  ids={r['moveId'] for r in rows};self.assertNotIn(230,ids);self.assertIn(274,ids)
 def test_source_frame_shapes_and_unused_animation(self):
  paw=self.recipes['gAssistPawprintSpriteTemplate'];petal=self.recipes['gSweetScentPetalSpriteTemplate'];self.assertEqual('AnimAssistPawprint',paw['callback']);self.assertEqual('AnimSweetScentPetal',petal['callback']);self.assertEqual([0],paw['loopStarts']);self.assertEqual([0,0,-1],petal['loopStarts']);self.assertEqual([8,8,1],list(map(len,petal['animations'])))
  self.assertTrue(all(f['hFlip'] for f in petal['animations'][1]));self.assertEqual([0,1,2,3,3,2,0,1],[f['tile'] for f in petal['animations'][0]]);self.assertTrue(all(f['duration']==8 for row in petal['animations'] for f in row))
  for r in [paw,petal]:self.assertEqual([],r['affineAnimations']);self.assertFalse(r['blend']);self.assertFalse(r['affineDoubleSize'])
 def test_material_pixels_indices_and_palettes_match_every_source_pixel(self):
  for asset,shape in [('paw-print',(32,32)),('pink-petal',(8,32))]:
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+asset.replace('-','_')+'.png'))) as source:
    self.assertEqual(shape,source.size);palette=source.getpalette()[:48];colors=[sum((palette[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(shape,struct.unpack_from('<HH',self.pixels[asset]));self.assertEqual([colors[i]|(32768 if i else 0) for i in source.tobytes()],list(struct.unpack_from('<'+'H'*(shape[0]*shape[1]),self.pixels[asset],4)))
    tiled=[source.getpixel((tx+x,ty+y)) for ty in range(0,shape[1],8) for tx in range(0,shape[0],8) for y in range(8) for x in range(8)];self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),self.pixels[asset+'.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels[asset+'.pal16'])
 def test_corrupt_callbacks_and_material_registration_fail(self):
  original=imp.read
  for source,old,new in [('src/battle_anim_effects_3.c',b'.callback = AnimAssistPawprint,',b'.callback = Other,'),('src/battle_anim_effects_3.c',b'.callback = AnimSweetScentPetal,',b'.callback = Other,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_PawPrint, 0x0200,',b'{gBattleAnimSpriteGfx_PawPrint, 0x0100,')]:
   def read(reference,path):
    data=original(reference,path);return data.replace(old,new) if path==source else data
   with self.subTest(source=source,old=old),patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(a.reference)
 def test_previous_rows_and_materials_remain_byte_identical(self):
  old=json.loads(((a.previous or a.generated)/'elemental.json').read_bytes())
  for row in old['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for key,value in old['subroutines'].items():self.assertEqual(value,self.pack['subroutines'][key])
  for group in ['assets','indexedAssets']:
   current={r['id']:r for r in self.pack[group]}
   for row in old[group]:
    self.assertEqual(row,current[row['id']])
    for field in ['path','indicesPath','palettePath']:
     if field in row:self.assertEqual(((a.previous or a.generated)/row[field]).read_bytes(),self.pixels[row[field].removeprefix('elemental-').removesuffix('.rgb555')])
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
