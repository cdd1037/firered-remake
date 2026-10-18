#!/usr/bin/env python3
"""Keep native punch commands and approved safe initialization distinctly owned."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('punch_background',ROOT/'scripts/imports/battle/import-source-background-scripts.py');background=importlib.util.module_from_spec(spec);spec.loader.exec_module(background)
elemental=background.elemental
CALLBACK='AnimTask_AttackerPunchWithTrace'
def discover():
 e,_=elemental.discover_scripts(a.reference);b=json.loads(background.build(a.reference))
 return {r['moveId']:r for r in e},{r['moveId']:r for r in b['scripts']}
class PunchTraceAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.elemental,cls.background=discover();cls.source=elemental.read(a.reference,'data/battle_anim_scripts.s').decode()
 def test_exact_pair_has_one_producer_and_no_new_material_owner(self):
  self.assertIn(325,self.elemental);self.assertNotIn(325,self.background);self.assertIn(183,self.background);self.assertNotIn(183,self.elemental)
  self.assertFalse(self.elemental.keys()&self.background.keys())
  self.assertEqual('Move_SHADOW_PUNCH',self.elemental[325]['label']);self.assertEqual('Move_MACH_PUNCH',self.background[183]['label'])
  for row in [self.elemental[325],self.background[183]]:
   self.assertEqual({'ANIM_TAG_IMPACT','ANIM_TAG_HANDS_AND_FEET','ANIM_TAG_BENT_SPOON'},row['spriteTags'].keys());self.assertEqual(10097,row['spriteTags']['ANIM_TAG_BENT_SPOON'])
   self.assertFalse(any(c['op']=='loadspritegfx' and c['args']==['ANIM_TAG_BENT_SPOON'] for c in row['commands']))
   self.assertFalse(any(k in row for k in ['spriteDependencies','materialDependencies','overlayDependencies']))
   self.assertEqual({'gBasicHitSplatSpriteTemplate','gFistFootSpriteTemplate'},{c['args'][0] for c in row['commands'] if c['op']=='createsprite'})
 def test_native_commands_and_tints_are_preserved_while_policy_stays_in_callback(self):
  lines=self.source.splitlines()
  for row,tint in [(self.background[183],['RGB(8, 9, 28)','10']),(self.elemental[325],['RGB_BLACK','13'])]:
   task=[c for c in row['commands'] if c['op']=='createvisualtask' and c['args'][0]==CALLBACK];self.assertEqual(1,len(task));self.assertEqual([CALLBACK,'2',*tint],task[0]['args'])
   for c in row['commands']:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op=='jumprettrue':op,args='jumpargeq',['7','1',args[0]]
    self.assertEqual((op,args),(c['op'],c['args']))
   self.assertIn(['9','8'],[c['args'] for c in row['commands'] if c['op']=='setalpha']);self.assertIn(['6'],[c['args'] for c in row['commands'] if c['op']=='delay'])
  self.assertEqual({'BG_HIGHSPEED_PLAYER','BG_HIGHSPEED_OPPONENT'},{c['args'][0] for c in self.background[183]['commands'] if c['op']=='fadetobg'})
  self.assertEqual(['BG_GHOST'],next(c['args'] for c in self.elemental[325]['commands'] if c['op']=='fadetobg'))
  # This is the historical defect evidence, not a claim that the importer executes it.
  native=elemental.read(a.reference,'src/battle_anim_mons.c').decode();self.assertIn('gSprites[task->tBattlerSpriteId].x2 -= task->tBattlerSpriteId;',native);self.assertIn('CpuCopy32(&gPlttBufferUnfaded[src], &gPlttBufferFaded[dest], PLTT_SIZE_4BPP);',native)
 def test_removing_callback_removes_only_its_owned_pair(self):
  with patch.object(elemental,'TASKS',elemental.TASKS-{CALLBACK}):e,b=discover()
  self.assertEqual({325},self.elemental.keys()-e.keys());self.assertEqual({183},self.background.keys()-b.keys())
  for old,new in [(self.elemental,e),(self.background,b)]:
   self.assertFalse(new.keys()-old.keys());self.assertTrue(all(row==old[k] for k,row in new.items()))
 def test_malformed_coefficient_width_and_unknown_callback_are_rejected(self):
  original=elemental.read
  for suffix in ['RGB_BLACK, -1','RGB_BLACK, 17','RGB_BLACK','RGB_BLACK, 13, 7']:
   replacements=[(f'{CALLBACK}, 2, RGB_BLACK, 13'.encode(),f'{CALLBACK}, 2, {suffix}'.encode()),(f'{CALLBACK}, 2, RGB(8, 9, 28), 10'.encode(),f'{CALLBACK}, 2, {suffix}'.encode())]
   def read(reference,path):
    data=original(reference,path)
    if path=='data/battle_anim_scripts.s':
     for before,after in replacements:self.assertEqual(1,data.count(before));data=data.replace(before,after)
    return data
   with patch.object(elemental,'read',side_effect=read):e,b=discover()
   self.assertNotIn(325,e);self.assertNotIn(183,b)
  def read(reference,path):
   data=original(reference,path);return data.replace((CALLBACK+',').encode(),b'AnimTask_UnknownPunchTrace,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(elemental,'read',side_effect=read):e,b=discover()
  self.assertNotIn(325,e);self.assertNotIn(183,b)
 def test_generated_rows_are_the_current_pinned_discovery(self):
  for file,move,expected in [('elemental.json',325,self.elemental[325]),('background-scripts.json',183,self.background[183])]:
   rows={r['moveId']:r for r in json.loads((a.generated/file).read_text())['scripts']};self.assertEqual(expected,rows[move])
if __name__=='__main__':unittest.main()
