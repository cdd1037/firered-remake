#!/usr/bin/env python3
"""Secret Power keeps authenticated source tail edges and existing material owners."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('secret_power_physical','import-source-physical-animations.py')
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class SecretPowerImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.raw=imp.read_sources(REFERENCE);cls.pack=json.loads(imp.build(REFERENCE)[0]);cls.row=next(s for s in cls.pack['scripts'] if s['moveId']==290)
 def test_native_selector_is_eleven_commands_with_canonical_source_tail_labels(self):
  row=self.row;self.assertEqual('Move_SECRET_POWER',row['label']);self.assertEqual(11,len(row['commands']));self.assertFalse(row['labels'])
  targets=['NEEDLE_ARM','MAGICAL_LEAF','MUD_SHOT','WATERFALL','SURF','BUBBLE_BEAM','ROCK_THROW','BITE','STRENGTH','SLAM']
  self.assertEqual(['Move_'+name for name in targets],row['programReferences'])
  expected=[('createvisualtask',['AnimTask_GetBattleTerrain','5'])]+[('jumpargeq',['0',str(i),'Move_'+name]) for i,name in enumerate(targets[:-1])]+[('goto',['Move_SLAM'])]
  self.assertEqual(expected,[(c['op'],c['args']) for c in row['commands']]);self.assertEqual(list(range(9843,9854)),[c['line'] for c in row['commands']])
  self.assertFalse(row['soundEffects']);self.assertFalse(row['spriteTags']);self.assertNotIn('spriteDependencies',row)
 def test_all_ten_existing_children_and_helpers_keep_per_program_bounds(self):
  rows={r['label']:r for r in self.pack['scripts']}
  for file in ['elemental.json']:
   rows.update({r['label']:r for r in json.loads((ROOT/'content/generated/battle/animations'/file).read_text())['scripts']})
  self.assertEqual([302,345,341,127,57,61,88,44,70,21],[rows[label]['moveId'] for label in self.row['programReferences']])
  self.assertGreater(sum(len(rows[label]['commands']) for label in self.row['programReferences']),128)
  for label in self.row['programReferences']:self.assertLessEqual(len(rows[label]['commands']),128)
  self.assertTrue(any(c['op']=='call' for label in self.row['programReferences'] for c in rows[label]['commands']))
 def test_terrain_task_pointer_transfer_and_synchronous_initializer_are_source_proven(self):
  # Each mutation keeps valid C syntax but changes a critical source ABI fact.
  cases=[('src/battle_anim_utility_funcs.c',b'gBattleAnimArgs[0] = gBattleTerrain;',b'gBattleAnimArgs[7] = gBattleTerrain;'),
   ('src/battle_anim_utility_funcs.c',b'gBattleAnimArgs[0] = gBattleTerrain;\n    DestroyAnimVisualTask(taskId);',b'gBattleAnimArgs[0] = gBattleTerrain;'),
   ('src/battle_anim.c',b'    taskFunc(taskId);\n    gAnimVisualTaskCount++;',b'    gAnimVisualTaskCount++;'),
   ('src/battle_anim.c',b'sBattleAnimScriptPtr = T2_READ_PTR(sBattleAnimScriptPtr + 3);',b'sBattleAnimScriptPtr = T2_READ_PTR(sBattleAnimScriptPtr + 4);'),
   ('include/constants/battle.h',b'#define BATTLE_TERRAIN_PLAIN        9',b'#define BATTLE_TERRAIN_PLAIN        10')]
  for path,old,new in cases:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(290,{s['moveId'] for s in json.loads(data)['scripts']});self.assertTrue(any(s['moveId']==290 for s in blocked))
 def test_unknown_selector_task_missing_tail_and_retained_argument_shape_fail_closed(self):
  for old,new in [(b'createvisualtask AnimTask_GetBattleTerrain, 5',b'createvisualtask UnknownTerrainTask, 5'),(b'createvisualtask AnimTask_GetBattleTerrain, 5',b'createvisualtask AnimTask_GetBattleTerrain, 5, 0'),(b'goto Move_SLAM\n\nMove_TWISTER:',b'goto Move_MISSING\n\nMove_TWISTER:')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,_=imp.build(REFERENCE)
   self.assertNotIn(290,{s['moveId'] for s in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
