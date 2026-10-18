#!/usr/bin/env python3
"""Rollout complete raw-child closure and existing local materials; no Ice Ball fallback."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('rollout_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class RolloutAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']}
 def test_complete_root_retains_every_source_command_raw_child_and_callback_sound(self):
  row=self.rows[205];self.assertEqual('Move_ROLLOUT',row['label']);self.assertEqual(15,len(row['commands']));lines=imp.read(REFERENCE,'data/battle_anim_scripts.s').decode().splitlines()
  for c in row['commands']:
   text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(['gRolloutMudSpriteTemplate','gRolloutRockSpriteTemplate'],row['spriteDependencies']);self.assertEqual({'SE_M_HEADBUTT':155,'SE_M_DIG':168,'SE_M_MEGA_KICK2':134},row['soundEffects'])
  self.assertEqual({'ANIM_TAG_IMPACT','ANIM_TAG_MUD_SAND','ANIM_TAG_ROCKS'},set(row['spriteTags']));self.assertNotIn(301,self.rows)
 def test_raw_children_use_exact_existing_sheets_shapes_and_dummy_frames(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for name,asset,size in [('gRolloutMudSpriteTemplate','mud-sand',8),('gRolloutRockSpriteTemplate','rocks',32)]:
   r=recipes[name];self.assertEqual('AnimRolloutParticle',r['callback']);self.assertEqual(asset,r['asset']);self.assertEqual(size,r['width']);self.assertEqual(size,r['height']);self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([0],r['loopStarts']);self.assertEqual([],r['affineAnimations']);self.assertFalse(r['blend']);self.assertFalse(r['affineDoubleSize'])
   self.assertEqual(self.pixels[asset],(ROOT/'content/generated/battle/animations'/('elemental-'+asset+'.rgb555')).read_bytes())
 def test_absent_task_or_power_adapter_keeps_full_root_unadmitted(self):
  for callback in ['AnimTask_Rollout','AnimTask_ShakeTargetBasedOnMovePowerOrDmg']:
   with patch.object(imp,'TASKS',imp.TASKS-{callback}):data,_=imp.build(REFERENCE)
   self.assertNotIn(205,[r['moveId'] for r in json.loads(data)['scripts']])
 def test_raw_owner_counter_and_material_registration_mutations_fail_closed(self):
  original=imp.read
  for path,old,new in [('src/battle_anim_rock.c','DestroySprite(sprite);','DestroyAnimSprite(sprite);'),('src/battle_anim_rock.c','++task->data[11];','++task->data[10];'),('src/battle_anim_rock.c','if (var0 > 4)','if (var0 > 5)'),('src/data/battle_anim.h','{gBattleAnimSpriteGfx_Rocks, 0x0C00, ANIM_TAG_ROCKS}','{gBattleAnimSpriteGfx_Rocks, 0x0400, ANIM_TAG_ROCKS}'),('data/battle_anim_scripts.s','createvisualtask AnimTask_Rollout, 2','createvisualtask AnimTask_Rollout, 2, 0')]:
   raw=original(REFERENCE,path);self.assertIn(old.encode(),raw)
   def altered(reference,name):return raw.replace(old.encode(),new.encode()) if name==path else original(reference,name)
   with patch.object(imp,'read',side_effect=altered):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_power_register_uses_existing_rollout_override_with_no_rock_modifier(self):
  commands=imp.read(REFERENCE,'src/battle_script_commands.c').decode();body=re.search(r'static void Cmd_rolloutdamagecalculation\(void\)\s*\{(.*?)^\}',commands,re.S|re.M)[1]
  for code in ['rolloutTimer = 5;','rolloutTimerStartValue = 5;','--gDisableStructs[gBattlerAttacker].rolloutTimer','gDynamicBasePower = gBattleMoves[gCurrentMove].power;','gDynamicBasePower *= 2;','STATUS2_DEFENSE_CURL']:self.assertIn(code,body)
  self.assertIn('gBattleScripting.animTurn, gBattleMovePower, gBattleMoveDamage',commands)
  pokemon=imp.read(REFERENCE,'src/pokemon.c').decode();self.assertIn('gBattleMovePower = powerOverride;',pokemon)
  modifiers=pokemon[pokemon.index('if (type == TYPE_ELECTRIC && AbilityBattleEffects'):pokemon.index('// Self-destruct / Explosion cut defense in half')]
  self.assertEqual({'TYPE_ELECTRIC','TYPE_FIRE','TYPE_GRASS','TYPE_WATER','TYPE_BUG'},set(re.findall(r'type == (TYPE_\w+)',modifiers)))
  move=re.search(r'\[MOVE_ROLLOUT\].*?\n    \},',imp.read(REFERENCE,'src/data/battle_moves.h').decode(),re.S)[0];self.assertIn('.type = TYPE_ROCK',move)
if __name__=='__main__':unittest.main()
