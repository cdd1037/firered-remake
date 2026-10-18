"""Dedicated delayed-attack setup, exact source closure and separate hit ownership."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
rel='scripts/imports/battle/import-source-physical-animations.py';spec=importlib.util.spec_from_file_location('doom_physical_import',ROOT/rel);imp=importlib.util.module_from_spec(spec)
if a.candidate:exec(compile((a.candidate/rel).read_text(),str(ROOT/rel),'exec'),imp.__dict__)
else:spec.loader.exec_module(imp)
class DoomDesireImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_only_setup_root_is_added_without_any_material_or_old_program_change(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'GetIsDoomDesireHitTurn'}):old,_=imp.build(REFERENCE)
  old=json.loads(old);roots={r['moveId']:r for r in old['scripts']};self.assertEqual({353},set(self.rows)-set(roots))
  for move,row in roots.items():self.assertEqual(row,self.rows[move])
  for key in set(old)-{'scripts'}:self.assertEqual(old[key],self.pack[key])
 def test_complete_setup_preserves_every_authored_instruction_and_no_hit_dependencies(self):
  row=self.rows[353];self.assertEqual('Move_DOOM_DESIRE',row['label']);self.assertEqual({},row['labels']);self.assertEqual({},row['spriteTags']);self.assertEqual({'SE_M_PSYBEAM':182},row['soundEffects']);self.assertNotIn('spriteDependencies',row);self.assertNotIn('materialDependencies',row)
  source=self.raw['data/battle_anim_scripts.s'].decode();lines=source.splitlines();self.assertEqual(17,len(row['commands']))
  for c in row['commands']:
   text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(['GetIsDoomDesireHitTurn','2'],row['commands'][0]['args']);self.assertEqual('end',row['commands'][-1]['op']);self.assertEqual([9747,9763],[row['commands'][i]['line'] for i in [0,-1]])
  self.assertFalse(any(c['op'] in {'goto','jumpargeq','call','loadspritegfx'} for c in row['commands']))
  self.assertEqual(['FALSE','TRUE'],[c['args'][-1] for c in row['commands'] if c['args'][:1]==['AnimTask_SetGrayscaleOrOriginalPal']])
  self.assertNotIn('General_DoomDesireHit',self.pack['subroutines']);self.assertNotIn('UnusedDoomDesireHit',row['labels'])
 def test_delayed_impact_has_a_separate_general_animation_table_entry(self):
  source=self.raw['data/battle_anim_scripts.s'].decode();battle=self.raw['data/battle_scripts_1.s'].decode()
  self.assertEqual('Move_DOOM_DESIRE',re.findall(r'^\s*\.4byte (Move_\w+)$',source,re.M)[353]);self.assertRegex(source,r'\.4byte General_DoomDesireHit\s+@ B_ANIM_DOOM_DESIRE_HIT')
  setup=battle.split('BattleScript_EffectFutureSight::',1)[1].split('\nBattleScript_',1)[0];self.assertLess(setup.index('trysetfutureattack'),setup.index('attackanimation'));self.assertEqual(1,setup.count('attackanimation'))
  self.assertRegex(battle,r'BattleScript_FutureHitAnimDoomDesire::\s+playanimation BS_ATTACKER, B_ANIM_DOOM_DESIRE_HIT')
 def test_predicate_retains_return_register_above_two_and_destroys_task(self):
  source=self.raw['src/battle_anim_dark.c'].decode();body=re.search(r'void GetIsDoomDesireHitTurn\(u8 taskId\)\s*\{(.*?)\n\}',source,re.S)[1]
  self.assertEqual('if(gAnimMoveTurn<2)gBattleAnimArgs[ARG_RET_ID]=FALSE;if(gAnimMoveTurn==2)gBattleAnimArgs[ARG_RET_ID]=TRUE;DestroyAnimVisualTask(taskId);',re.sub(r'\s+','',body))
  main=(REFERENCE/'src/battle_main.c').read_text();finish=main.split('static void HandleAction_ActionFinished(void)\n{',1)[1].split('\n}',1)[0];self.assertIn('gBattleScripting.animTurn = 0;',finish)
  command=(REFERENCE/'src/battle_script_commands.c').read_text().split('static void Cmd_attackanimation(void)\n{',1)[1].split('\n}',1)[0];self.assertIn('BtlController_EmitMoveAnimation(BUFFER_A, gCurrentMove, gBattleScripting.animTurn,',command)
 def test_each_missing_callback_or_sprite_keeps_complete_setup_out(self):
  for name,kind in [('GetIsDoomDesireHitTurn','TASKS'),('AnimTask_SetGrayscaleOrOriginalPal','TASKS'),('AnimTask_ScaleMonAndRestore','TASKS'),('gSimplePaletteBlendSpriteTemplate','SPRITES')]:
   with patch.object(imp,kind,getattr(imp,kind)-{name}):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(353,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_wrong_predicate_abi_or_effect_cannot_supply_move_turn(self):
  for path,old,new,reason in [('data/battle_anim_scripts.s',b'GetIsDoomDesireHitTurn, 2',b'GetIsDoomDesireHitTurn, 2, 0','callback-arguments:GetIsDoomDesireHitTurn'),('src/data/battle_moves.h',b'[MOVE_DOOM_DESIRE] =\n    {\n        .effect = EFFECT_FUTURE_SIGHT,',b'[MOVE_DOOM_DESIRE] =\n    {\n        .effect = EFFECT_HIT,','invocation:move-turn')]:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(353,{r['moveId']:r for r in json.loads(data)['scripts']});self.assertIn(reason,next(r['missing'] for r in blocked if r['moveId']==353))
 def test_generated_pack_is_exact(self):self.assertEqual(self.data,(a.generated or ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
