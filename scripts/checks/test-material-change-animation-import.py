#!/usr/bin/env python3
"""Focused source-only material-change producer checks; no managed build or writes."""
import importlib.util,json,os,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(os.environ.get('FIRERED_TEST_ROOT',Path(__file__).resolve().parents[2]))
PATH=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
CANDIDATE=Path(os.environ.get('FIRERED_IMPORT_CANDIDATE',PATH))
spec=importlib.util.spec_from_file_location('material_elemental',PATH);imp=importlib.util.module_from_spec(spec);exec(compile(CANDIDATE.read_text(),str(PATH),'exec'),imp.__dict__)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class MaterialChangeImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={r['moveId']:r for r in cls.pack['scripts']}
 def test_exact_roots_and_callback_sounds(self):
  transform=self.rows[144];substitute=self.rows[164]
  self.assertEqual('Move_TRANSFORM',transform['label']);self.assertEqual('Move_SUBSTITUTE',substitute['label'])
  self.assertEqual(['monbg','playsewithpan','waitplaysewithpan','createvisualtask','waitforvisualfinish','clearmonbg','end'],[c['op'] for c in transform['commands']])
  self.assertEqual(['AnimTask_TransformMon','2','0'],transform['commands'][3]['args'])
  self.assertEqual(['playsewithpan','createvisualtask','end'],[c['op'] for c in substitute['commands']]);self.assertEqual(['AnimTask_MonToSubstitute','2'],substitute['commands'][1]['args'])
  self.assertEqual({'SE_M_TELEPORT','SE_M_MINIMIZE'},set(transform['soundEffects']))
  self.assertEqual({'SE_M_ATTRACT','SE_M_BUBBLE2'},set(substitute['soundEffects']))
  self.assertEqual({},transform['spriteTags']);self.assertEqual({},substitute['spriteTags'])
 def test_only_two_roots_added_and_prior_packs_and_pixels_stay_identical(self):
  with patch.object(imp,'TASKS',imp.TASKS-imp.MATERIAL_CHANGE_TASKS):olddata,oldpixels=imp.build(REFERENCE)
  old=json.loads(olddata);before={r['moveId']:r for r in old['scripts']}
  self.assertEqual({144,164},self.rows.keys()-before.keys());self.assertFalse(before.keys()-self.rows.keys())
  for key,row in before.items():self.assertEqual(row,self.rows[key],key)
  for key in old.keys()-{'scripts'}:self.assertEqual(old[key],self.pack[key],key)
  self.assertEqual(oldpixels.keys(),self.pixels.keys())
  for name,data in self.pixels.items():self.assertEqual(oldpixels[name],data,name)
 def test_task_source_constants_and_child_sounds_fail_closed(self):
  original=imp.read
  for before,after in [(b'gTasks[taskId].data[1] += 0x60;',b'gTasks[taskId].data[1] += 0x61;'),
   (b'gSprites[spriteId].y2 = -200;',b'gSprites[spriteId].y2 = -199;'),
   (b'PlaySE12WithPanning(SE_M_BUBBLE2, BattleAnimAdjustPanning(SOUND_PAN_ATTACKER));',b'PlaySE12WithPanning(SE_M_BUBBLE, BattleAnimAdjustPanning(SOUND_PAN_ATTACKER));'),
   (b'if (stretch == 15)',b'if (stretch == 14)')]:
   def read(reference,path):
    data=original(reference,path);return data.replace(before,after) if path=='src/battle_anim_effects_3.c' else data
   with patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_move_task_mode_and_operand_width_fail_closed(self):
  original=imp.read
  for before,after in [(b'createvisualtask AnimTask_TransformMon, 2, 0',b'createvisualtask AnimTask_TransformMon, 2, 255'),
   (b'createvisualtask AnimTask_MonToSubstitute, 2',b'createvisualtask AnimTask_MonToSubstitute, 2, 0')]:
   def read(reference,path):
    data=original(reference,path);return data.replace(before,after) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
