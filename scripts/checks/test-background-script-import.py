import importlib.util,json,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('background_scripts', ROOT/'scripts/imports/battle/import-source-background-scripts.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=imp.ROOT/'content/raw/pokefirered/037335f4'
class BackgroundScriptImportTests(unittest.TestCase):
 def test_self_stat_variants_keep_source_background_closure_task_data_and_sound(self):
  pack=json.loads(imp.build(REFERENCE));rows={r['moveId']:r for r in pack['scripts']}
  self.assertEqual({'SE_M_HEADBUTT','SE_M_TAKE_DOWN'},set(rows[96]['soundEffects']));self.assertEqual({'SE_M_METRONOME'},set(rows[133]['soundEffects']))
  for move in [96,133]:self.assertEqual(['SetPsychicBackground','UnsetPsychicBackground'],[c['args'][0] for c in rows[move]['commands'] if c['op']=='call'])
  commands=pack['taskAffinePrograms']['sAffineAnim_MeditateStretchAttacker'];self.assertEqual([(-8,10,16),(18,-18,16),(-20,16,8)],[(c['x'],c['y'],c['duration']) for c in commands[:-1]]);self.assertEqual('end',commands[-1]['op'])
  self.assertEqual(['gQuestionMarkSpriteTemplate','ANIM_ATTACKER','20'],next(c['args'] for c in rows[133]['commands'] if c['op']=='createsprite'))
 def test_self_stat_variants_unknown_dependencies_and_changed_task_handoff_fail_closed(self):
  original=imp.elemental.read
  for before,after,move in [(b'AnimTask_MeditateStretchAttacker,',b'AnimTask_UnknownMeditate,',96),(b'gQuestionMarkSpriteTemplate,',b'gUnknownQuestionMarkSpriteTemplate,',133)]:
   def read(reference,path):
    data=original(reference,path);return data.replace(before,after) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(move,{r['moveId'] for r in json.loads(imp.build(REFERENCE))['scripts']})
  def read(reference,path):
   data=original(reference,path);return data.replace(b'PrepareAffineAnimInTaskData(task, spriteId, sAffineAnim_MeditateStretchAttacker);',b'PrepareAffineAnimInTaskData(task, spriteId, gUnknownAffine);') if path=='src/battle_anim_psychic.c' else data
  with patch.object(imp.elemental,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)

 def test_dependency_discovery_and_source_symbols(self):
  pack=json.loads(imp.build(REFERENCE));self.assertEqual({96,133,158,59,60,87,93,94,95,138,149,168,177,196,242,248,322},{r['moveId'] for r in pack['scripts']});self.assertEqual({'BG_IMPACT_OPPONENT':4,'BG_IMPACT_PLAYER':5,'BG_DARK':1,'BG_PSYCHIC':3,'BG_THUNDER':11,'BG_COSMIC':16,'BG_SKY':18,'BG_ICE':15,'BG_HIGHSPEED_PLAYER':10,'BG_HIGHSPEED_OPPONENT':9},pack['backgroundSymbols']);self.assertNotIn('spriteRecipes',pack);self.assertNotIn('assets',pack);self.assertEqual(imp.build(REFERENCE),(ROOT/'content/generated/battle/animations/background-scripts.json').read_bytes())
  for script in pack['scripts']:
   commands=script['commands']+[c for name in dict.fromkeys(c['args'][0] for c in script['commands'] if c['op']=='call') for c in pack['subroutines'][name]];ops=[c['op'] for c in commands];self.assertGreaterEqual(ops.count('waitbgfadein'),2);self.assertEqual(1,ops.count('restorebg'))
 def test_uncovered_background_and_sprite_fail_closed(self):
  original=imp.elemental.read
  for old,new,missing in [(b'fadetobg BG_DARK',b'fadetobg BG_UNKNOWN',{168,242}),(b'createsprite gSharpTeethSpriteTemplate',b'createsprite UnknownSprite',{242})]:
   def read(reference,path):
    data=original(reference,path);return data.replace(old,new) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp.elemental,'read',side_effect=read):
    self.assertFalse(missing&{r['moveId'] for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_sound_dependencies_are_reachable_numeric_source_operands(self):
  scripts={s['moveId']:s for s in json.loads(imp.build(REFERENCE))['scripts']}
  self.assertEqual({'SE_M_PSYBEAM':182,'SE_M_PSYBEAM2':193},scripts[60]['soundEffects'])
  self.assertEqual({'SE_M_SUPERSONIC':177},scripts[95]['soundEffects'])
  self.assertEqual({'SE_M_PSYBEAM':182,'SE_M_TELEPORT':196},scripts[149]['soundEffects'])
  with self.assertRaises(ValueError):imp.elemental.sound_effects(REFERENCE,[{'op':'playsewithpan','args':['SE_UNKNOWN','0']}])
 def test_sliding_dependencies_preserve_live_arguments_and_sound_players(self):
  pack=json.loads(imp.build(REFERENCE));scripts={s['moveId']:s for s in pack['scripts']}
  self.assertEqual({'SE_M_COSMIC_POWER':236,'SE_M_MORNING_SUN':221},scripts[322]['soundEffects'])
  for move,velocity in [(87,['-256','0','1','-1']),(322,['0','128','0','-1'])]:
   commands=scripts[move]['commands'];sliding=[c for c in commands if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_StartSlidingBg']
   self.assertEqual(1,len(sliding));self.assertEqual(velocity,sliding[0]['args'][2:])
   self.assertIn(['7','0xFFFF'],[c['args'] for c in commands if c['op']=='setarg'])
  source=imp.elemental.read(REFERENCE,'sound/song_table.inc').decode()
  self.assertRegex(source,r'song se_m_cosmic_power, 2, 2')
  self.assertRegex(source,r'song se_m_morning_sun, 1, 1')
 def test_sky_helper_folds_only_verified_firered_contest_branch(self):
  pack=json.loads(imp.build(REFERENCE));helper=pack['subroutines']['SetSkyBg'];self.assertEqual(['fadetobg','waitbgfadeout','createvisualtask','waitbgfadein','return'],[c['op'] for c in helper]);self.assertEqual(['BG_SKY'],helper[0]['args']);self.assertNotIn('BG_SKY_CONTESTS',pack['backgroundSymbols'])
  original=imp.elemental.read
  for path,old,new in [('data/battle_anim_scripts.s',b'SetSkyBgContest:\n',b'MissingSkyContest:\n'),('src/battle_anim.c',b'static void Cmd_jumpifcontest(void)\n{\n    sBattleAnimScriptPtr += 5;',b'static void Cmd_jumpifcontest(void)\n{\n    sBattleAnimScriptPtr += 6;')]:
   def read(ref,p):
    data=original(ref,p);return data.replace(old,new) if p==path else data
   with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(177,{r['moveId'] for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_background_rows_carry_reachable_numeric_sprite_tags(self):
  pack=json.loads(imp.build(REFERENCE));aero=next(r for r in pack['scripts'] if r['moveId']==177);self.assertIn('ANIM_TAG_AIR_WAVE_2',aero['spriteTags'])
  for row in pack['scripts']:
   flat=row['commands']+[c for label in dict.fromkeys(c['args'][0] for c in row['commands'] if c['op']=='call') for c in pack['subroutines'][label]]
   self.assertEqual({c['args'][0] for c in flat if c['op']=='loadspritegfx'},row['spriteTags'].keys());self.assertTrue(all(10000<=v<=65535 for v in row['spriteTags'].values()))
 def test_exact_branch_and_operand_dependencies(self):
  rows={r['moveId']:r for r in json.loads(imp.build(REFERENCE))['scripts']};r=rows[59];self.assertEqual({'BlizzardAgainstPlayer':23,'BlizzardContinue':5},r['labels']);self.assertIn(['7','1','BlizzardAgainstPlayer'],[c['args'] for c in r['commands'] if c['op']=='jumpargeq']);self.assertEqual({'ANIM_TAG_ICE_CRYSTALS':10141,'ANIM_TAG_ICE_SPIKES':10142},rows[196]['spriteTags'])
 def test_missing_branch_target_is_not_admitted(self):
  original=imp.elemental.read
  def read(root,path):
   data=original(root,path);return data.replace(b'jumprettrue BlizzardAgainstPlayer',b'jumprettrue MissingIceLabel') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(59,{r['moveId']:r for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_branch_cycles_are_not_admitted(self):
  original=imp.elemental.read
  def read(root,path):
   data=original(root,path);return data.replace(b'BlizzardAgainstPlayer:\n\tfadetobg BG_HIGHSPEED_PLAYER\n\tgoto BlizzardContinue',b'BlizzardAgainstPlayer:\n\tfadetobg BG_HIGHSPEED_PLAYER\n\tgoto BlizzardAgainstPlayer') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(59,{r['moveId']:r for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_unknown_background_and_callback_fail_closed(self):
  original=imp.elemental.read
  for old,new,move in [(b'fadetobg BG_ICE',b'fadetobg BG_UNKNOWN',196),(b'gSwirlingSnowballSpriteTemplate,',b'gUnknownSnowballSpriteTemplate,',59)]:
   def read(root,path):
    data=original(root,path);return data.replace(old,new) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(move,{r['moveId']:r for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_hyper_fang_uses_shared_predicate_fold_and_side_selected_impact(self):
  pack=json.loads(imp.build(REFERENCE));r=next(r for r in pack['scripts'] if r['moveId']==158);commands=r['commands']
  self.assertEqual(['AnimTask_IsContest','AnimTask_IsTargetPlayerSide','AnimTask_ShakeMon'],[c['args'][0] for c in commands if c['op']=='createvisualtask'])
  self.assertEqual([['7','0','HyperFangOnOpponent']],[c['args'] for c in commands if c['op']=='jumpargeq'])
  self.assertEqual({'BG_IMPACT_PLAYER','BG_IMPACT_OPPONENT'},{c['args'][0] for c in commands if c['op']=='fadetobg'})
  self.assertNotIn('HyperFangInContest',r['labels']);self.assertNotIn('BG_IMPACT_CONTESTS',pack['backgroundSymbols'])
  self.assertEqual({'ANIM_TAG_FANG_ATTACK':10192},r['spriteTags']);self.assertEqual({'SE_M_BITE','SE_M_LEER'},set(r['soundEffects']))
 def test_hyper_fang_predicate_bypass_or_missing_reachable_material_is_rejected(self):
  original=imp.elemental.read
  for old,new in [(b'\tjumprettrue HyperFangInContest',b'BypassFangPredicate:\n\tjumprettrue HyperFangInContest'),(b'loadspritegfx ANIM_TAG_FANG_ATTACK',b'loadspritegfx ANIM_TAG_UNKNOWN_FANG'),(b'createsprite gFangSpriteTemplate',b'createsprite UnknownFang'),(b'HyperFangInContest:\n',b'MissingFangContest:\n')]:
   def read(root,path):
    data=original(root,path);return data.replace(old,new) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp.elemental,'read',side_effect=read):self.assertNotIn(158,{r['moveId'] for r in json.loads(imp.build(REFERENCE))['scripts']})
if __name__=='__main__':unittest.main()
