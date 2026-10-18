#!/usr/bin/env python3
"""Original Bide branches and simultaneous Thrash task closure, without new art."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('locked_pair_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class LockedPairAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_both_full_roots_keep_authored_commands_and_bide_branches(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in [37,117]:
   root=self.rows[move];self.assertTrue(root['soundEffects']);self.assertTrue(any(c['op']=='end' for c in root['commands']))
   for c in root['commands']:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
  bide=self.rows[117];self.assertEqual(['BideSetUp','BideUnleash'],bide['commands'][0]['args']);self.assertTrue(set(bide['commands'][0]['args'])<=bide['labels'].keys())
  self.assertEqual(['gBasicHitSplatSpriteTemplate'],self.rows[37]['spriteDependencies'])
  self.assertNotIn(80,self.rows);self.assertNotIn(253,self.rows)
 def test_thrashing_affine_loop_is_source_task_program_not_sprite_animation(self):
  self.assertEqual([('frame',-10,9,7,0),('frame',20,-20,7,0),('frame',-20,20,7,0),('frame',10,-9,7,0),('loop',0,0,0,2),('end',0,0,0,0)],[(c['op'],c['x'],c['y'],c['duration'],c['count']) for c in self.pack['taskAffinePrograms']['sThrashMoveMonAffineAnimCmds']])
  self.assertEqual({'ANIM_TAG_IMPACT','ANIM_TAG_HANDS_AND_FEET'},set(self.rows[37]['spriteTags']));self.assertEqual({'ANIM_TAG_IMPACT'},set(self.rows[117]['spriteTags']))
 def test_each_motion_dependency_fails_closed_without_affecting_bide(self):
  for name in imp.LOCKED_PAIR_TASKS:
   with patch.object(imp,'TASKS',imp.TASKS-{name}):
    data,blocked=imp.build(REFERENCE);rows={r['moveId'] for r in json.loads(data)['scripts']};self.assertNotIn(37,rows);self.assertIn(117,rows);self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==37))
 def test_task_abi_and_move_turn_semantics_cannot_be_inferred(self):
  for name in imp.LOCKED_PAIR_TASKS:
   raw=dict(self.raw);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace((name+', 2').encode(),name.encode())
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(37,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('callback-arguments:'+name,next(r['missing'] for r in blocked if r['moveId']==37))
  raw=dict(self.raw);text=raw['src/data/battle_moves.h'].decode();block=re.search(r'\[MOVE_BIDE\].*?\n    \},',text,re.S)[0];self.assertIn('EFFECT_BIDE',block);raw['src/data/battle_moves.h']=text.replace(block,block.replace('EFFECT_BIDE','EFFECT_HIT')).encode()
  with patch.object(imp,'read_sources',return_value=raw):
   data,blocked=imp.build(REFERENCE);self.assertNotIn(117,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('invocation:move-turn',next(r['missing'] for r in blocked if r['moveId']==117))
 def test_targeted_regeneration_is_exact(self):
  data,_=imp.build(REFERENCE);self.assertEqual(data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
