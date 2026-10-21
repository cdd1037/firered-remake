#!/usr/bin/env python3
"""Reviewed support/status roots, producer ownership and exact local source materials."""
import sys
sys.dont_write_bytecode=True
import argparse,importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from indexed_material_expectations import assert_preserved_materials, assert_source_pixels
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');p.add_argument('--previous',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('support_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');bg=module('support_background',ROOT/'scripts/imports/battle/import-source-background-scripts.py');bg.elemental=imp
NAMES={'gHelpingHandClapSpriteTemplate','gFastFlyingMusicNotesSpriteTemplate','sImprisonOrbSpriteTemplate'}
class SupportStatusImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.background=bg.build(a.reference);cls.bgpack=json.loads(cls.background);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.bgroots={r['moveId']:r for r in cls.bgpack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):
   data,_=imp.build(a.reference);cls.previous=json.loads(data);cls.oldbg=json.loads(bg.build(a.reference))
 def mutated(self,path,old,new,producer=imp):
  original=imp.read;self.assertIn(old,original(a.reference,path))
  def read(reference,name):
   value=original(reference,name);return value.replace(old,new) if name==path else value
  with patch.object(imp,'read',side_effect=read):return producer.build(a.reference)
 def test_shared_dependency_closure_adds_two_elemental_and_one_background_root(self):
  for current,previous,expected in [(self.roots,self.previous['scripts'],{270,298}),(self.bgroots,self.oldbg['scripts'],{286})]:
   old={r['moveId']:r for r in previous};self.assertEqual(expected,current.keys()-old.keys());self.assertTrue(old.keys()<=current.keys())
   for key,row in old.items():self.assertEqual(row,current[key])
  old={r['template']:r for r in self.previous['spriteRecipes']};self.assertEqual(NAMES,self.recipes.keys()-old.keys())
  for name,row in old.items():self.assertEqual(row,self.recipes[name])
 def test_complete_roots_keep_authored_phase_order_and_load_only_duck(self):
  self.assertEqual('Move_HELPING_HAND',self.roots[270]['label']);self.assertEqual('Move_TEETER_DANCE',self.roots[298]['label']);self.assertEqual('Move_IMPRISON',self.bgroots[286]['label'])
  self.assertEqual([19,14,20],[int(c['args'][0]) for c in self.roots[270]['commands'] if c['op']=='delay']);self.assertEqual(['duck'],self.roots[298]['materialDependencies']);self.assertEqual(5,sum(c['op']=='createsprite' for c in self.roots[298]['commands']));self.assertEqual(['sImprisonOrbSpriteTemplate'],self.bgroots[286]['spriteDependencies']);self.assertEqual(['SetPsychicBackground','UnsetPsychicBackground'],[c['args'][0] for c in self.bgroots[286]['commands'] if c['op']=='call']);self.assertNotIn(286,self.roots)
 def test_missing_raw_child_or_task_removes_imprison(self):
  for key in ['sImprisonOrbSpriteTemplate','gRedXSpriteTemplate']:
   with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=key}),patch.object(imp,'SHARED_SPRITES',imp.SHARED_SPRITES-{key}),patch.object(bg.physical,'SPRITES',bg.physical.SPRITES-{key}):
    self.assertNotIn(286,{r['moveId'] for r in json.loads(bg.build(a.reference))['scripts']})
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_ImprisonOrbs'}):self.assertNotIn(286,{r['moveId'] for r in json.loads(bg.build(a.reference))['scripts']})
 def test_raw_attempt_count_failure_guard_and_cleanup_are_source_verified(self):
  for old,new in [(b'if (++task->data[2] == 5)',b'if (++task->data[2] == 4)'),(b'if (++task->data[1] == 32)',b'if (++task->data[1] == 30)'),(b'if (spriteId != MAX_SPRITES)',b'if (spriteId == MAX_SPRITES)'),(b'DestroySprite(&gSprites[task->data[i]]);',b'DestroyAnimSprite(&gSprites[task->data[i]]);')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutated('src/battle_anim_psychic.c',old,new,bg)
 def test_teeter_leaf_uses_static_coordinate_queries(self):
  with self.assertRaises(ValueError):self.mutated('src/battle_anim_effects_1.c',b'sprite->x = GetBattlerSpriteCoord(gBattleAnimAttacker, BATTLER_COORD_X_2) + gBattleAnimArgs[1];',b'sprite->x = gSprites[gBattlerSpriteIds[gBattleAnimAttacker]].x + gBattleAnimArgs[1];')
 def test_registered_finger_palette_and_hollow_orb_pixels_are_exact(self):
  for asset,shape,pal in [('tag_hand',(32,64),'finger'),('hollow_orb',(16,16),'hollow_orb')]:
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+pal+'.png'))) as im:palette=im.getpalette()[:48]
   colors=[sum((palette[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+asset+'.png'))) as im:
    self.assertEqual(shape,im.size);assert_source_pixels(self,self.pack,self.pixels,asset.replace('_','-'),shape,im.tobytes(),colors,'ANIM_TAG_'+asset.upper());indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()]
   stem=asset.replace('_','-');self.assertEqual(struct.pack('<16H',*colors),self.pixels[stem+'.pal16']);self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.pixels[stem+'.4bpp'])
 def test_bad_material_registration_or_callback_fails_closed(self):
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Finger, ANIM_TAG_TAG_HAND}',b'{gBattleAnimSpritePal_HollowOrb, ANIM_TAG_TAG_HAND}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_HollowOrb, ANIM_TAG_HOLLOW_ORB}',b'{gBattleAnimSpritePal_Finger, ANIM_TAG_HOLLOW_ORB}'),('src/battle_anim_effects_3.c',b'.callback = AnimHelpingHandClap,',b'.callback = SpriteCallbackDummy,'),('src/battle_anim_effects_1.c',b'.callback = AnimFlyingMusicNotes,',b'.callback = AnimSlowFlyingMusicNotes,')]:
   with self.subTest(path=path),self.assertRaises(ValueError):self.mutated(path,old,new)
 def test_source_frame_affine_and_raw_template_contracts(self):
  clap=self.recipes['gHelpingHandClapSpriteTemplate'];orb=self.recipes['sImprisonOrbSpriteTemplate'];note=self.recipes['gFastFlyingMusicNotesSpriteTemplate'];self.assertEqual((32,32,False),(clap['width'],clap['height'],clap['blend']));self.assertEqual((16,16,True),(orb['width'],orb['height'],orb['blend']));self.assertEqual('SpriteCallbackDummy',orb['callback']);self.assertEqual([0],clap['loopStarts']);self.assertEqual([0],orb['loopStarts']);self.assertEqual(8,len(note['animations']));self.assertEqual([0,4,8,12,16,20,0,4],[x[0]['tile'] for x in note['animations']]);self.assertEqual([10,10,41,10,10,10,10,10],[x[0]['duration'] for x in note['animations']]);self.assertEqual([False]*6+[True]*2,[x[0]['vFlip'] for x in note['animations']]);self.assertEqual([12,-12],[c['x'] for c in note['affineAnimations'][0][:2]]);self.assertEqual([16,16],[c['duration'] for c in note['affineAnimations'][0][:2]]);self.assertEqual('jump',note['affineAnimations'][0][2]['op']);self.assertTrue(note['affineDoubleSize'])
 def test_generated_parity(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes());self.assertEqual(self.background,(a.generated/'background-scripts.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
 def test_current_or_explicit_prior_rows_recipes_and_materials_are_preserved(self):
  baseline=a.previous or a.generated
  for file,rows in [('elemental.json',self.roots),('background-scripts.json',self.bgroots)]:
   old=json.loads((baseline/file).read_bytes())
   for row in old['scripts']:self.assertEqual(row,rows[row['moveId']])
   if file=='elemental.json':
    for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
    assert_preserved_materials(self,old,self.pack,baseline,self.pixels,'elemental-')
if __name__=='__main__':unittest.main()
