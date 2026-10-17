#!/usr/bin/env python3
"""Fire/Leech Seed source-root closure, registration, exact pixels and preservation."""
import importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('fire_seed_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
TEMPLATES={'gFirePlumeSpriteTemplate','gLargeFlameSpriteTemplate','gLargeFlameScatterSpriteTemplate','gLeechSeedSpriteTemplate'}
class FireSeedImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  blob,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(blob);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_complete_roots_retain_original_multiplicity_sounds_and_material_control(self):
  cases=[(73,'LEECH_SEED',{'gLeechSeedSpriteTemplate':3},{'ANIM_TAG_SEED'}),
   (221,'SACRED_FIRE',{'gFirePlumeSpriteTemplate':8,'gLargeFlameSpriteTemplate':3,'gLargeFlameScatterSpriteTemplate':8},{'ANIM_TAG_FIRE','ANIM_TAG_FIRE_PLUME'}),
   (307,'BLAST_BURN',{'gFirePlumeSpriteTemplate':24,'gBasicHitSplatSpriteTemplate':1},{'ANIM_TAG_FIRE_PLUME','ANIM_TAG_IMPACT'})]
  for move,label,multiplicity,tags in cases:
   root=self.roots[move];self.assertEqual('Move_'+label,root['label']);self.assertEqual(tags,set(root['spriteTags']))
   actual={name:sum(c['op']=='createsprite' and c['args'][0]==name for c in root['commands']) for name in multiplicity};self.assertEqual(multiplicity,actual)
   self.assertEqual('end',root['commands'][-1]['op'])
  seed=self.roots[73]['commands'];self.assertEqual([35]*3,[int(c['args'][7]) for c in seed if c['op']=='createsprite']);self.assertEqual([-32,-40,-37],[int(c['args'][8]) for c in seed if c['op']=='createsprite'])
  self.assertEqual({'SE_M_POISON_POWDER','SE_M_TAIL_WHIP'},set(self.roots[73]['soundEffects']))
  self.assertEqual({'SE_M_SACRED_FIRE','SE_M_SACRED_FIRE2','SE_M_FLAME_WHEEL2'},set(self.roots[221]['soundEffects']))
  self.assertEqual({'SE_M_SACRED_FIRE','SE_M_FLAME_WHEEL2'},set(self.roots[307]['soundEffects']))
  for move,count in [(221,4),(307,2)]:self.assertEqual(count,sum(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_InvertScreenColor' for c in self.roots[move]['commands']))
  blast=self.roots[307]['commands'];self.assertIn(['ANIM_DEF_PARTNER'],[c['args'] for c in blast if c['op']=='monbg']);self.assertIn(['12','8'],[c['args'] for c in blast if c['op']=='setalpha']);self.assertEqual(['clearmonbg','blendoff','end'],[c['op'] for c in blast[-3:]])
 def test_exact_frame_and_affine_programs_and_source_shapes(self):
  def frames(tiles,duration):return [dict(tile=t,duration=duration,hFlip=False,vFlip=False) for t in tiles]
  for name in TEMPLATES:
   r=self.recipes[name];seed=name=='gLeechSeedSpriteTemplate';plume=name=='gFirePlumeSpriteTemplate';affine=name=='gLargeFlameSpriteTemplate'
   self.assertEqual(('seed' if seed else 'fire-plume' if plume else 'fire',16 if seed else 32,16 if seed else 32,False,False),(r['asset'],r['width'],r['height'],r['blend'],r['affineDoubleSize']))
   self.assertEqual('AnimLeechSeed' if seed else 'AnimFirePlume' if plume else 'AnimLargeFlame',r['callback'])
   self.assertEqual([frames([0],1),frames([4,8],7)] if seed else [frames(range(0,80 if plume else 128,16),5 if plume else 3)],r['animations'])
   self.assertEqual([-1,0] if seed else [0],r['loopStarts'])
   expected=[[dict(op='frame',x=50,y=256,rotation=0,duration=0,target=0),dict(op='frame',x=32,y=0,rotation=0,duration=7,target=0),dict(op='end',x=0,y=0,rotation=0,duration=0,target=0)]] if affine else []
   self.assertEqual(expected,r['affineAnimations'])
 def test_fire_sheet_preserves_every_palette_color_and_transparent_index(self):
  with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/fire.png'))) as image:
   self.assertEqual(('P',(32,256)),(image.mode,image.size));palette=image.getpalette();expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))|(32768 if i else 0) for i in image.tobytes()]
  self.assertEqual((32,256),struct.unpack_from('<HH',self.pixels['fire']));self.assertEqual(expected,list(struct.unpack_from('<8192H',self.pixels['fire'],4)))
  self.assertEqual([dict(id='fire',path='elemental-fire.rgb555')],[row for row in self.pack['assets'] if row['id']=='fire'])
  self.assertFalse(any(row['id']=='seed' for row in self.pack['assets']),'Leech Seed uses the existing physical seed sheet; it must not introduce a second material copy.')
 def test_only_three_new_roots_and_prior_recipes_subroutines_pixels_remain_identical(self):
  templates={name:value for name,value in imp.TEMPLATES.items() if name not in TEMPLATES}
  with patch.dict(imp.TEMPLATES,templates,clear=True):blob,pixels=imp.build(REFERENCE)
  baseline=json.loads(blob);old={r['moveId']:r for r in baseline['scripts']};self.assertEqual({73,221,307},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for field in ['subroutines','taskAffinePrograms']:
   for name,value in baseline[field].items():self.assertEqual(value,self.pack[field][name])
  for field,key in [('spriteRecipes','template'),('assets','id'),('indexedAssets','id')]:
   current={r[key]:r for r in self.pack[field]}
   for row in baseline[field]:self.assertEqual(row,current[row[key]])
  for name,blob in pixels.items():self.assertEqual(blob,self.pixels[name])
  self.assertGreater(len(old),50);self.assertGreater(len(pixels),60)
 def test_unknown_callback_and_missing_script_dependency_fail_closed(self):
  original=imp.read
  for move,template in [(73,'gLeechSeedSpriteTemplate'),(221,'gLargeFlameSpriteTemplate'),(307,'gFirePlumeSpriteTemplate')]:
   def read(reference,path):
    data=original(reference,path);return data.replace((template+',').encode(),b'gMissingFireSeedSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp,'read',side_effect=read):blob,_=imp.build(REFERENCE)
   self.assertNotIn(move,{r['moveId'] for r in json.loads(blob)['scripts']})
  for template in TEMPLATES:
   file,callback=imp.TEMPLATES[template];source=original(REFERENCE,'src/'+file).decode()
   with self.assertRaises(ValueError):imp.recipe(source.replace('.callback = '+callback+',','.callback = MissingFireSeedCallback,'),template,callback)
 def test_wrong_source_sheet_registration_palette_and_path_are_rejected(self):
  original=imp.read
  changes=[('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Fire, 0x1000,',b'{gBattleAnimSpriteGfx_Fire, 0x0800,'),
   ('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Fire, ANIM_TAG_FIRE_PLUME}',b'{gBattleAnimSpritePal_Seed, ANIM_TAG_FIRE_PLUME}'),
   ('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Seed, 0x0180,',b'{gBattleAnimSpriteGfx_Seed, 0x0080,'),
   ('src/graphics.c',b'"graphics/battle_anims/sprites/fire.4bpp.lz"',b'"graphics/battle_anims/sprites/seed.4bpp.lz"')]
  for target,old,new in changes:
   def read(reference,path):
    data=original(reference,path);return data.replace(old,new) if path==target else data
   with patch.object(imp,'read',side_effect=read):
    with self.assertRaisesRegex(ValueError,'Fire/seed source'):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
