#!/usr/bin/env python3
"""Five source roots, exact palette aliasing, and unchanged pre-existing programs."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
relative='scripts/imports/battle/import-source-physical-animations.py';spec=importlib.util.spec_from_file_location('orb_defense_physical',ROOT/relative);physical=importlib.util.module_from_spec(spec)
if a.candidate:exec(compile((a.candidate/relative).read_text(),str(ROOT/relative),'exec'),physical.__dict__)
else:spec.loader.exec_module(physical)
class OrbDefenseImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.raw=physical.read_sources(RAW)
 def test_source_material_registration_and_gray_blue_alias(self):
  rgb,indexed,pixels=physical.orb_defense_materials(self.raw)
  self.assertEqual({'protect','shadow-ball','gray-orb','blue-orb','red-orb-2'},rgb.keys());self.assertEqual(5,len(indexed))
  for alias in ['blue-orb','red-orb-2']:
   self.assertEqual(pixels['gray-orb.4bpp'],pixels[alias+'.4bpp']);self.assertEqual(96,len(pixels[alias+'.4bpp']));self.assertEqual(32,len(pixels[alias+'.pal16']));self.assertNotEqual(pixels['gray-orb.pal16'],pixels[alias+'.pal16']);self.assertNotEqual(rgb['gray-orb'],rgb[alias])
  self.assertNotEqual(pixels['blue-orb.pal16'],pixels['red-orb-2.pal16'])
  self.assertEqual(96,len(pixels['gray-orb.4bpp']));self.assertEqual(2048,len(pixels['protect.4bpp']));self.assertEqual(512,len(pixels['shadow-ball.4bpp']))
  self.assertEqual({'ANIM_TAG_PROTECT','ANIM_TAG_SHADOW_BALL','ANIM_TAG_GRAY_ORB','ANIM_TAG_BLUE_ORB','ANIM_TAG_RED_ORB_2'},{r['paletteTag'] for r in indexed})
 def test_wrong_source_registration_palette_or_image_shape_is_rejected(self):
  for old,new in [('gBattleAnimSpriteGfx_GrayOrb, 0x0060, ANIM_TAG_BLUE_ORB','gBattleAnimSpriteGfx_GrayOrb, 0x0080, ANIM_TAG_BLUE_ORB'),('gBattleAnimSpritePal_BlueOrb, ANIM_TAG_BLUE_ORB','gBattleAnimSpritePal_GrayOrb, ANIM_TAG_BLUE_ORB'),('gBattleAnimSpriteGfx_GrayOrb, 0x0060, ANIM_TAG_RED_ORB_2','gBattleAnimSpriteGfx_GrayOrb, 0x0080, ANIM_TAG_RED_ORB_2'),('gBattleAnimSpritePal_RedOrb2, ANIM_TAG_RED_ORB_2','gBattleAnimSpritePal_GrayOrb, ANIM_TAG_RED_ORB_2')]:
   raw=dict(self.raw);text=raw['src/data/battle_anim.h'].decode();self.assertIn(old,text);raw['src/data/battle_anim.h']=text.replace(old,new).encode()
   with self.assertRaises(ValueError):physical.orb_defense_materials(raw)
  for palette in ['blue_orb','red_orb_2']:
   raw=dict(self.raw);raw['graphics/battle_anims/sprites/'+palette+'.pal']=b'not a palette'
   with self.assertRaises(ValueError):physical.orb_defense_materials(raw)
  raw=dict(self.raw);raw['graphics/battle_anims/sprites/gray_orb.png']=raw['graphics/battle_anims/sprites/shadow_ball.png']
  with self.assertRaises(ValueError):physical.orb_defense_materials(raw)
 def test_original_recipes_and_task_programs_are_shared_player_inputs(self):
  recipes={name:physical.elemental.recipe(self.raw['src/'+file].decode(),name,callback) for name,file,callback in physical.ORB_DEFENSE_RECIPES}
  self.assertEqual(5,len(recipes));stock=recipes['gStockpileAbsorptionOrbSpriteTemplate'];swallow=recipes['gSwallowBlueOrbSpriteTemplate']
  self.assertEqual('gray-orb',stock['asset']);self.assertTrue(stock['affineDoubleSize']);self.assertEqual([320,-14,0],[c['x'] for c in stock['affineAnimations'][0]])
  self.assertEqual('blue-orb',swallow['asset']);self.assertFalse(swallow.get('affineDoubleSize',False));self.assertEqual([],swallow['affineAnimations'])
  source=self.raw['src/battle_anim_effects_3.c'].decode()
  stock=physical.task_affine_program(source,'sStockpileDeformMonAffineAnimCmds');swallow=physical.task_affine_program(source,'sSwallowDeformMonAffineAnimCmds')
  self.assertEqual([12,12,12,0,0],[c['duration'] for c in stock]);self.assertEqual(1,stock[3]['count']);self.assertEqual([20,20,6,20,20,0],[c['duration'] for c in swallow])
 def test_changed_callback_or_task_handoff_is_rejected(self):
  physical.validate_orb_defense_callbacks(self.raw)
  for file,old,new in [('battle_anim_effects_1.c','sprite->data[0] = gBattleAnimArgs[2];','sprite->data[0] = gBattleAnimArgs[1];'),('battle_anim_effects_3.c','sprite->data[1] -= 96;','sprite->data[1] -= 95;'),('battle_anim_effects_3.c','sStockpileDeformMonAffineAnimCmds);','sSwallowDeformMonAffineAnimCmds);')]:
   raw=dict(self.raw);text=raw['src/'+file].decode();self.assertIn(old,text);raw['src/'+file]=text.replace(old,new).encode()
   with self.assertRaises(ValueError):physical.validate_orb_defense_callbacks(raw)
 def test_swallow_animation_turn_comes_from_preconsumption_source_count(self):
  source=(RAW/'src/battle_script_commands.c').read_text();body=source.split('static void Cmd_stockpiletohpheal(void)\n{',1)[1].split('\n}',1)[0]
  self.assertIn('gBattleScripting.animTurn = gDisableStructs[gBattlerAttacker].stockpileCounter;',body)
  final=body.rsplit('gBattleScripting.animTurn',1)[1];self.assertLess(final.index('stockpileCounter;'),final.index('stockpileCounter = 0;'))
  scripts=self.raw['data/battle_scripts_1.s'].decode();swallow=scripts.split('BattleScript_EffectSwallow::',1)[1].split('\nBattleScript_',1)[0]
  self.assertLess(swallow.index('ppreduce'),swallow.index('stockpiletohpheal'));self.assertIn('goto BattleScript_PresentHealTarget',swallow)
  animations=self.raw['data/battle_anim_scripts.s'].decode();self.assertEqual(2,animations.split('Move_STOCKPILE:\n',1)[1].split('\nStockpileAbsorb:',1)[0].count('call StockpileAbsorb'))
 def test_full_producer_adds_only_five_roots_and_preserves_prior_rows(self):
  data,_=physical.build(RAW);pack=json.loads(data);templates={r[0] for r in physical.ORB_DEFENSE_RECIPES}
  with patch.object(physical,'SPRITES',physical.SPRITES-templates),patch.object(physical,'TASKS',physical.TASKS-physical.ORB_DEFENSE_TASKS),patch.object(physical,'NEW',physical.NEW-templates-physical.ORB_DEFENSE_TASKS):prior=json.loads(physical.build(RAW)[0])
  old={r['moveId']:r for r in prior['scripts']};new={r['moveId']:r for r in pack['scripts']};self.assertEqual({179,182,247,254,256},new.keys()-old.keys())
  for key in old:self.assertEqual(old[key],new[key],str(key))
  self.assertIn(255,old)  # Charged-root support is independent of these five callbacks.
  for key,value in prior['subroutines'].items():self.assertEqual(value,pack['subroutines'][key])
  for move,label in [(179,'REVERSAL'),(182,'PROTECT'),(247,'SHADOW_BALL'),(254,'STOCKPILE'),(256,'SWALLOW')]:self.assertEqual('Move_'+label,new[move]['label'])
  self.assertEqual({'SwallowContinue','SwallowGood','SwallowBest'},set(new[256]['labels']));self.assertIn('SwallowEffect',pack['subroutines']);self.assertIn('HealingEffect',pack['subroutines']);self.assertIn('StockpileAbsorb',pack['subroutines'])
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in [179,182,247,254,256]:
   for c in new[move]['commands']:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  folder=a.generated or ROOT/'content/generated/battle/animations';self.assertEqual(data,(folder/'physical.json').read_bytes())
  for key,blob in {**physical.build_assets(RAW),**physical.build_indexed_assets(RAW)[1]}.items():
   name='physical-'+key if key.endswith(('.4bpp','.pal16','.pal48')) else 'physical-'+key+'.rgb555';self.assertEqual(blob,(folder/name).read_bytes(),name)
if __name__=='__main__':unittest.main()
