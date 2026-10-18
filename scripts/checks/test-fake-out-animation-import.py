#!/usr/bin/env python3
"""Canonical Fake Out root closure; no downloads or managed compilation."""
import importlib.util,json,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('fake_out_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class FakeOutImportTests(unittest.TestCase):
 def test_original_root_uses_shared_window_palette_affine_and_sound_owners(self):
  data,blocked=imp.build(REFERENCE);pack=json.loads(data);script=next(s for s in pack['scripts'] if s['moveId']==252)
  self.assertEqual('Move_FAKE_OUT',script['label'])
  self.assertEqual([
   ('playsewithpan',['SE_M_FLATTER','0']),('createvisualtask',['AnimTask_FakeOut','5']),('waitforvisualfinish',[]),
   ('playsewithpan',['SE_M_SKETCH','SOUND_PAN_TARGET']),('createvisualtask',['AnimTask_ShakeMon2','2','ANIM_TARGET','4','0','5','1']),
   ('createvisualtask',['AnimTask_StretchTargetUp','3']),('waitforvisualfinish',[]),
   ('createsprite',['gSimplePaletteBlendSpriteTemplate','ANIM_ATTACKER','2','F_PAL_BG','3','16','0','RGB_WHITE']),('end',[])],
   [(c['op'],c['args']) for c in script['commands']])
  self.assertEqual({'SE_M_FLATTER','SE_M_SKETCH'},set(script['soundEffects']))
  self.assertEqual({},script['spriteTags'])
  self.assertEqual([10,10,0],[c['duration'] for c in pack['taskAffinePrograms']['sAffineAnims_StretchBattlerUp']])
  self.assertFalse(any(c['op']=='loadspritegfx' for c in script['commands']))
 def test_unknown_missing_or_extra_task_operands_never_admit_root(self):
  for old,new in [(b'AnimTask_FakeOut, 5',b'AnimTask_UnknownFakeOut, 5'),(b'AnimTask_FakeOut, 5',b'AnimTask_FakeOut, 5, 1'),(b'AnimTask_StretchTargetUp, 3',b'AnimTask_UnknownStretch, 3')]:
   raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(252,{s['moveId'] for s in json.loads(data)['scripts']})
 def test_task_capability_is_real_dependency_not_a_move_id_allowlist(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_FakeOut'}):data,_=imp.build(REFERENCE)
  self.assertNotIn(252,{s['moveId'] for s in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
