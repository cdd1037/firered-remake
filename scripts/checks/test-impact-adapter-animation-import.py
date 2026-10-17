#!/usr/bin/env python3
"""Complete root dependency closure, exact indexed input colors and preservation."""
import importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('impact_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
FAMILIES=[(132,'CONSTRICT','gConstrictBindingSpriteTemplate','AnimConstrictBinding','tendrils',64,32,4),(146,'DIZZY_PUNCH','gDizzyPunchDuckSpriteTemplate','AnimDizzyPunchDuck','duck',16,16,12),(282,'KNOCK_OFF','gKnockOffStrikeSpriteTemplate','AnimKnockOffStrike','slam-hit-2',64,64,1),(302,'NEEDLE_ARM','gNeedleArmSpikeSpriteTemplate','AnimNeedleArmSpike','green-spike',16,16,20),(338,'FRENZY_PLANT','gFrenzyPlantRootSpriteTemplate','AnimFrenzyPlantRoot','roots',32,32,9)]
class ImpactImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  blob,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(blob);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def flat(self,move):
  root=self.roots[move];return root['commands']+[c for call in root['commands'] if call['op']=='call' for c in self.pack['subroutines'][call['args'][0]]]
 def test_complete_roots_preserve_original_multiplicity_sounds_and_control(self):
  for move,label,template,callback,asset,w,h,count in FAMILIES:
   self.assertEqual('Move_'+label,self.roots[move]['label']);self.assertEqual(count,sum(c['op']=='createsprite' and c['args'][0]==template for c in self.flat(move)))
   recipe=self.recipes[template];self.assertEqual((callback,asset,w,h,False,False),(recipe['callback'],recipe['asset'],recipe['width'],recipe['height'],recipe['blend'],recipe['affineDoubleSize']))
  self.assertIn(dict(op='setarg',args=['7','0xFFFF'],line=next(c['line'] for c in self.flat(132) if c['op']=='setarg')),self.flat(132))
  self.assertEqual({'SE_M_BIND':163,'SE_M_SCRATCH':148},self.roots[132]['soundEffects'])
  self.assertEqual({'SE_M_COMET_PUNCH':132,'SE_M_VITAL_THROW2':116},self.roots[146]['soundEffects'])
  self.assertEqual({'SE_M_COMET_PUNCH':132,'SE_M_VITAL_THROW':115},self.roots[282]['soundEffects'])
  self.assertEqual({'SE_M_HORN_ATTACK':159,'SE_M_VITAL_THROW2':116},self.roots[302]['soundEffects'])
  self.assertEqual({'SE_M_DOUBLE_SLAP':127,'SE_M_SCRATCH':148},self.roots[338]['soundEffects'])
  self.assertFalse(any('ItemBag' in c['args'][0] for c in self.flat(282) if c['op']=='createsprite'))
  self.assertEqual(1,sum(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ShakeMonInPlace' for c in self.flat(282)))
 def test_frames_affine_restarts_and_duck_material_remain_exact(self):
  constrain=self.recipes['gConstrictBindingSpriteTemplate'];self.assertEqual([-1,-1],constrain['loopStarts'])
  for i,frames in enumerate(constrain['animations']):
   self.assertEqual([dict(tile=t,duration=4,hFlip=bool(i),vFlip=False) for t in [0,32,64,96]],frames)
  for i,program in enumerate(constrain['affineAnimations']):
   sign=1 if i==0 else -1
   self.assertEqual([dict(op='frame',x=256*sign,y=256,rotation=0,duration=0,target=0),dict(op='frame',x=-11*sign,y=0,rotation=0,duration=6,target=0),dict(op='frame',x=11*sign,y=0,rotation=0,duration=6,target=0),dict(op='end',x=0,y=0,rotation=0,duration=0,target=0)],program)
  knock=self.recipes['gKnockOffStrikeSpriteTemplate'];self.assertEqual([0,64],[f['tile'] for f in knock['animations'][0]]);self.assertEqual([252,4],[p[1]['rotation'] for p in knock['affineAnimations']]);self.assertEqual([8,8],[p[1]['duration'] for p in knock['affineAnimations']])
  roots=self.recipes['gFrenzyPlantRootSpriteTemplate'];self.assertEqual([4,4,3,3],list(map(len,roots['animations'])));self.assertEqual([False,True,False,True],[a[0]['hFlip'] for a in roots['animations']]);self.assertTrue(all(f['duration']==7 for a in roots['animations'] for f in a))
  self.assertEqual([dict(id='duck',path='elemental-duck.rgb555')],[row for row in self.pack['assets'] if row['id']=='duck'])
 def test_new_materials_preserve_every_palette_color_and_transparent_index(self):
  for asset,w,h in [('duck',16,48),('tendrils',64,128),('slam-hit-2',64,128),('green-spike',16,16),('roots',32,128)]:
   with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/'+asset.replace('-','_')+'.png'))) as image:
    self.assertEqual(('P',(w,h)),(image.mode,image.size));palette=image.getpalette();expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))|(32768 if i else 0) for i in image.tobytes()]
   self.assertEqual((w,h),struct.unpack_from('<HH',self.pixels[asset]));self.assertEqual(expected,list(struct.unpack_from('<'+str(w*h)+'H',self.pixels[asset],4)))
 def test_unrelated_roots_subroutines_recipes_and_pixels_are_preserved(self):
  templates={name:value for name,value in imp.TEMPLATES.items() if name not in {r[2] for r in FAMILIES}}
  with patch.dict(imp.TEMPLATES,templates,clear=True):blob,pixels=imp.build(REFERENCE)
  baseline=json.loads(blob);old={r['moveId']:r for r in baseline['scripts']}
  self.assertEqual({132,146,282,302,338},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for field in ['subroutines','taskAffinePrograms']:
   for name,value in baseline[field].items():self.assertEqual(value,self.pack[field][name])
  for field,key in [('spriteRecipes','template'),('assets','id'),('indexedAssets','id')]:
   current={r[key]:r for r in self.pack[field]}
   for row in baseline[field]:self.assertEqual(row,current[row[key]])
  for name,blob in pixels.items():self.assertEqual(blob,self.pixels[name])
  self.assertGreater(len(old),50);self.assertGreater(len(pixels),60)
 def test_missing_callback_or_dependency_fails_closed(self):
  original=imp.read
  for move,label,template,callback,asset,w,h,count in FAMILIES:
   def read(reference,path):
    data=original(reference,path);return data.replace((template+',').encode(),b'gUnknownImpactSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp,'read',side_effect=read):blob,_=imp.build(REFERENCE)
   self.assertNotIn(move,{r['moveId'] for r in json.loads(blob)['scripts']})
   source=original(REFERENCE,'src/'+imp.TEMPLATES[template][0]).decode()
   with self.assertRaises(ValueError):imp.recipe(source.replace('.callback = '+callback+',','.callback = MissingImpactCallback,'),template,callback)
 def test_wrong_source_registration_is_rejected(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path);return data.replace(b'{gBattleAnimSpriteGfx_GreenSpike, 0x0080,',b'{gBattleAnimSpriteGfx_GreenSpike, 0x0100,') if path=='src/data/battle_anim.h' else data
  with patch.object(imp,'read',side_effect=read):
   with self.assertRaisesRegex(ValueError,'Impact source registration'):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
