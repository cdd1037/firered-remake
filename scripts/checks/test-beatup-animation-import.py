#!/usr/bin/env python3
"""Beat Up's dedicated source branches and contributor-owned animation boundary."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('beatup_physical_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py')
imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'

class BeatUpImportTests(unittest.TestCase):
 def test_dedicated_root_keeps_both_two_impact_branches_and_shared_materials(self):
  pack=json.loads(imp.build(REFERENCE)[0]);root=next(s for s in pack['scripts'] if s['moveId']==251)
  self.assertEqual('Move_BEAT_UP',root['label'])
  self.assertEqual({'ANIM_TAG_IMPACT':10135,'ANIM_TAG_HANDS_AND_FEET':10143},root['spriteTags'])
  self.assertEqual({'SE_M_VITAL_THROW2':116},root['soundEffects'])
  self.assertEqual([['BeatUpLeft','BeatUpRight']],[c['args'] for c in root['commands'] if c['op']=='choosetwoturnanim'])
  self.assertEqual({'gBasicHitSplatSpriteTemplate','gFistFootSpriteTemplate'},{c['args'][0] for c in root['commands'] if c['op']=='createsprite'})
  self.assertEqual({'AnimTask_ShakeMon'},{c['args'][0] for c in root['commands'] if c['op']=='createvisualtask'})
  for label,offsets in [('BeatUpLeft',[('-20','-20'),('8','0')]),('BeatUpRight',[('12','-20'),('-12','0')])]:
   branch=[]
   for c in root['commands'][root['labels'][label]:]:
    branch.append(c)
    if c['op']=='goto':break
   self.assertEqual(['BeatUpContinue'],branch[-1]['args'])
   self.assertEqual([['gBasicHitSplatSpriteTemplate','ANIM_TARGET','2',x,y,'ANIM_TARGET','2'] for x,y in offsets],
    [c['args'] for c in branch if c['op']=='createsprite' and c['args'][0]=='gBasicHitSplatSpriteTemplate'])
   self.assertEqual([['gFistFootSpriteTemplate','ANIM_TARGET','3',x,str(int(y)+8),'8','1','0'] for x,y in offsets],
    [c['args'] for c in branch if c['op']=='createsprite' and c['args'][0]=='gFistFootSpriteTemplate'])
   self.assertEqual([['AnimTask_ShakeMon','2','ANIM_TARGET','3','0','6','1']]*2,[c['args'] for c in branch if c['op']=='createvisualtask'])
   self.assertEqual([['8']],[c['args'] for c in branch if c['op']=='delay'])
   self.assertEqual(2,sum(c['op']=='playsewithpan' for c in branch))
  self.assertEqual(['waitforvisualfinish','clearmonbg','blendoff','end'],[c['op'] for c in root['commands'][root['labels']['BeatUpContinue']:root['labels']['BeatUpContinue']+4]])

 def test_missing_branch_callback_or_material_rejects_whole_root(self):
  raw=imp.read_sources(REFERENCE);source=raw['data/battle_anim_scripts.s'];start=source.index(b'Move_BEAT_UP:');end=source.index(b'\nMove_',start+1);original=source[start:end]
  cases=[(b'BeatUpLeft, BeatUpRight',b'BeatUpLeft, MissingBeatUp'),(b'BeatUpLeft, BeatUpRight',b'BeatUpLeft'),
   (b'goto BeatUpContinue',b'goto BeatUpLeft'),(b'gBasicHitSplatSpriteTemplate',b'UnknownBeatUpImpact'),
   (b'gFistFootSpriteTemplate',b'UnknownBeatUpFist'),(b'AnimTask_ShakeMon',b'UnknownBeatUpShake'),
   (b'loadspritegfx ANIM_TAG_IMPACT',b'loadspritegfx ANIM_TAG_UNKNOWN_BEATUP')]
  for before,after in cases:
   self.assertIn(before,original);changed=dict(raw);changed['data/battle_anim_scripts.s']=source[:start]+original.replace(before,after)+source[end:]
   with patch.object(imp,'read_sources',return_value=changed):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(251,{s['moveId'] for s in json.loads(data)['scripts']},str(before))
   self.assertTrue(next(b for b in blocked if b['moveId']==251)['missing'])

 def test_source_animation_turn_counts_eligible_executions_and_not_party_slots(self):
  scripts=(REFERENCE/'data/battle_scripts_1.s').read_text();body=scripts.split('BattleScript_EffectBeatUp::',1)[1].split('BattleScript_EffectSemiInvulnerable::',1)[0]
  order=['setbyte gBattleCommunication, 0','trydobeatup BattleScript_BeatUpEnd, BattleScript_ButItFailed','printstring STRINGID_PKMNATTACK','adjustnormaldamage','attackanimation','datahpupdate BS_TARGET','tryfaintmon BS_TARGET','moveendto MOVEEND_NEXT_TARGET','goto BattleScript_BeatUpLoop']
  self.assertEqual(sorted(body.index(x) for x in order),[body.index(x) for x in order]);self.assertEqual(1,len(re.findall(r'^\s*attackanimation$',body,re.M)))
  self.assertNotIn('multihit',body.lower())
  commands=(REFERENCE/'src/battle_script_commands.c').read_text();member=commands.split('static void Cmd_trydobeatup(void)\n{',1)[1].split('\nstatic void ',1)[0]
  for expression in ['gBattleMons[gBattlerTarget].hp == 0','gBattleCommunication[0] < PARTY_SIZE','MON_DATA_HP','MON_DATA_SPECIES_OR_EGG) != SPECIES_EGG','!GetMonData(&party[gBattleCommunication[0]], MON_DATA_STATUS)','gBattleCommunication[0]++']:
   self.assertIn(expression,member)
  self.assertNotIn('animTurn',member)
  animation=commands.split('static void Cmd_attackanimation(void)\n{',1)[1].split('\nstatic void ',1)[0]
  self.assertRegex(animation,r'BtlController_EmitMoveAnimation\(BUFFER_A, gCurrentMove, gBattleScripting\.animTurn,[^;]+;\s*gBattleScripting\.animTurn\+\+;')
  main=(REFERENCE/'src/battle_main.c').read_text();self.assertIn('gBattleScripting.animTurn = 0;',main)
  move=(REFERENCE/'src/data/battle_moves.h').read_text().split('[MOVE_BEAT_UP] =',1)[1].split('    },',1)[0]
  self.assertIn('EFFECT_BEAT_UP',move);self.assertNotIn('FLAG_MAKES_CONTACT',move)

if __name__=='__main__':unittest.main()
