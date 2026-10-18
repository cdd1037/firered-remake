#!/usr/bin/env python3
"""Native visibility roots and exact shared dependency/material contracts."""
import sys
sys.dont_write_bytecode=True
import argparse,importlib.util,json,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('visibility_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');bg=module('visibility_background',ROOT/'scripts/imports/battle/import-source-background-scripts.py');bg.elemental=imp
RING='gThinRingShrinkingSpriteTemplate'
class VisibilityFourImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.background=bg.build(a.reference);cls.bgpack=json.loads(cls.background);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.bgroots={r['moveId']:r for r in cls.bgpack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=RING}),patch.object(imp,'TASKS',imp.TASKS-imp.VISIBILITY_TASKS):
   data,pixels=imp.build(a.reference);cls.previous=json.loads(data);cls.previous_pixels=pixels;cls.oldbg=json.loads(bg.build(a.reference))
 def mutate_contract(self,path,old,new,move):
  original=imp.read;self.assertIn(old,original(a.reference,path));commands=(self.bgroots if move==185 else self.roots)[move]['commands']
  with patch.object(imp,'read',side_effect=lambda reference,name:original(reference,name).replace(old,new) if name==path else original(reference,name)):imp.visibility_contracts(a.reference,commands)
 def test_dependency_closure_adds_exact_four_complete_roots_and_preserves_prior_rows(self):
  for current,previous,expected in [(self.roots,self.previous['scripts'],{293,316,347}),(self.bgroots,self.oldbg['scripts'],{185})]:
   old={r['moveId']:r for r in previous};self.assertEqual(expected,current.keys()-old.keys());self.assertTrue(old.keys()<=current.keys())
   for key,row in old.items():self.assertEqual(row,current[key])
  old={r['template']:r for r in self.previous['spriteRecipes']};self.assertEqual({RING},self.recipes.keys()-old.keys())
  for name,row in old.items():self.assertEqual(row,self.recipes[name])
 def test_ring_reuses_exact_existing_thin_ring_material_without_new_assets(self):
  self.assertEqual(self.previous['assets'],self.pack['assets']);self.assertEqual(self.previous['indexedAssets'],self.pack['indexedAssets']);self.assertEqual(self.previous_pixels,self.pixels)
  r=self.recipes[RING];self.assertEqual(('thin-ring','AnimSpriteOnMonPos',64,64,True,True),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([0],r['loopStarts']);self.assertEqual([{'tile':0,'duration':1,'hFlip':False,'vFlip':False}],r['animations'][0]);self.assertEqual([('frame',512,512,0),('frame',-16,-16,30),('end',0,0,0)],[(c['op'],c['x'],c['y'],c['duration']) for c in r['affineAnimations'][0]])
 def test_authored_wait_signal_background_and_ring_count_are_preserved(self):
  faint=self.bgroots[185]['commands'];self.assertEqual('Move_FAINT_ATTACK',self.bgroots[185]['label']);self.assertNotIn(185,self.roots);self.assertEqual([['7','0x1000']],[c['args'] for c in faint if c['op']=='setarg']);self.assertEqual(['BG_DARK'],next(c['args'] for c in faint if c['op']=='fadetobg'));self.assertEqual(3,sum(c['op']=='createsprite' and c['args'][0]==RING for c in self.roots[347]['commands']));self.assertEqual(['ANIM_ATTACKER'],next(c['args'] for c in self.roots[347]['commands'] if c['op']=='visible'))
 def test_missing_visibility_task_or_ring_removes_complete_root(self):
  for move,task in [(293,'AnimTask_SetCamouflageBlend'),(316,'AnimTask_OdorSleuthMovement'),(347,'AnimTask_SetAllNonAttackersInvisiblity')]:
   with patch.object(imp,'TASKS',imp.TASKS-{task}):self.assertNotIn(move,{r['moveId'] for r in json.loads(imp.build(a.reference)[0])['scripts']})
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=RING}):self.assertNotIn(347,{r['moveId'] for r in json.loads(imp.build(a.reference)[0])['scripts']})
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_SetAttackerInvisibleWaitForSignal'}):self.assertNotIn(185,{r['moveId'] for r in json.loads(bg.build(a.reference))['scripts']})
 def test_dual_flag_or_predicate_and_command_presence_guard_are_verified(self):
  for old,new in [(b'.invisible || !gSprites[',b'.invisible && !gSprites['),(b'if (spriteId != SPRITE_NONE)',b'if (spriteId == SPRITE_NONE)')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutate_contract('src/battle_anim.c',old,new,347)
 def test_signal_task_count_and_saved_logical_bit_are_verified(self):
  for old,new in [(b'--gAnimVisualTaskCount;',b'++gAnimVisualTaskCount;'),(b'if (gBattleAnimArgs[7] == 0x1000)',b'if (gBattleAnimArgs[7] == 0xFFFF)'),(b'DestroyTask(taskId);',b'DestroyAnimVisualTask(taskId);')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutate_contract('src/battle_anim_utility_funcs.c',old,new,185)
 def test_terrain_colors_and_visibility_palette_selection_are_verified(self):
  for old,new,move in [(b'RGB(12, 24, 2)',b'RGB(12, 23, 2)',293),(b'switch (gBattleTerrain)',b'switch (gBattleAnimArgs[0])',293),(b'&& IsBattlerSpriteVisible(battler)',b'&& IsBattlerSpritePresent(battler)',347)]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutate_contract('src/battle_anim_utility_funcs.c',old,new,move)
 def test_fade_delay_narrowing_and_final_flag_write_are_verified(self):
  for old,new in [(b'== (u8)gTasks[taskId].data[0]',b'== gTasks[taskId].data[0]'),(b'gSprites[gBattlerSpriteIds[gBattleAnimAttacker]].invisible = TRUE;',b'gSprites[gBattlerSpriteIds[gBattleAnimAttacker]].invisible = FALSE;')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutate_contract('src/battle_anim_dark.c',old,new,185)
 def test_clone_failure_cleanup_global_allocation_and_counter_are_verified(self):
  for path,old,new in [('src/battle_anim_effects_3.c',b'if (spriteId2 < 0)',b'if (spriteId2 > 0)'),('src/battle_anim_effects_3.c',b'if (++sprite->data[2] == 60)',b'if (++sprite->data[2] == 61)'),('src/battle_anim_effects_3.c',b'gTasks[sprite->data[6]].data[sprite->data[7]]--;',b'gTasks[sprite->data[6]].data[sprite->data[7]]++;'),('src/battle_anim_mons.c',b'gSprites[i] = gSprites[spriteId];',b'gSprites[i].x = gSprites[spriteId].x;')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutate_contract(path,old,new,316)
 def test_generated_parity(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes());self.assertEqual(self.background,(a.generated/'background-scripts.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
