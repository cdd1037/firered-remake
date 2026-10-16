#!/usr/bin/env python3
"""Source-backed physical-family import checks; no compiler or third-party execution."""
import importlib.util,json,unittest,struct
from PIL import Image
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('physical_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class PhysicalImportTests(unittest.TestCase):
 def test_regeneration_matches_generated_only_runtime_input(self):
  data,_=imp.build(REFERENCE)
  self.assertEqual(data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
  scripts=json.loads(data)['scripts'];self.assertEqual({232,306,337,20,35,2,3,4,125,198,167,66,11,21,22,23,26,27,29,34,36,38,64,67,70,77,78,79,136,150,289,300,303,339,111,207,321},{s['moveId'] for s in scripts})
  for s in scripts:
   self.assertEqual('end',s['commands'][-1]['op']);self.assertLessEqual(len(s['commands']),128)
 def test_shared_goto_retains_branch_and_one_level_calls(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  for move in [20,35]:
   script=next(s for s in pack['scripts'] if s['moveId']==move)
   self.assertEqual('goto',script['commands'][1]['op'])
   target=script['labels']['BindWrap'];self.assertEqual(2,target)
   self.assertEqual('playsewithpan',script['commands'][target]['op'])
   self.assertEqual(['BindWrapSqueezeTarget','BindWrapSqueezeTarget'],[c['args'][0] for c in script['commands'] if c['op']=='call'])
  squeeze=pack['subroutines']['BindWrapSqueezeTarget']
  self.assertEqual(['AnimTask_ScaleMonAndRestore','5','10','-5','5','ANIM_TARGET','0'],squeeze[0]['args'])
 def test_missing_goto_label_is_not_flattened_to_generic_motion(self):
  raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'goto BindWrap',b'goto MissingPhysicalLabel')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertFalse({20,35}&{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertTrue(all('control-flow:unterminated' in next(b for b in blocked if b['moveId']==move)['missing'] for move in [20,35]))
 def test_generated_slam_and_whip_pixels_match_source_indices_and_palettes(self):
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/slam_hit.png') as image:
   indices=list(image.tobytes());slam=image.getpalette()
  lines=(REFERENCE/'graphics/battle_anims/sprites/whip_hit.pal').read_text().splitlines()[3:]
  whip=[int(c) for row in lines for c in row.split()]
  for name,palette in [('slam-hit',slam),('whip-hit',whip)]:
   blob=(ROOT/('content/generated/battle/animations/physical-'+name+'.rgb555')).read_bytes()
   self.assertEqual((32,256),struct.unpack_from('<HH',blob))
   pixels=struct.unpack_from('<8192H',blob,4)
   expected=[]
   for index in indices:
    r,g,b=palette[index*3:index*3+3]
    expected.append((r//8)+(g//8)*32+(b//8)*1024+(32768 if index else 0))
   self.assertEqual(expected,list(pixels))
 def test_powder_frames_loop_and_palette_variants_match_source(self):
  data,_=imp.build(REFERENCE);recipes=json.loads(data)['spriteRecipes']
  powders=[r for r in recipes if r['callback']=='AnimMovePowderParticle']
  self.assertEqual(3,len(powders))
  for recipe in powders:
   self.assertEqual((8,16),(recipe['width'],recipe['height']))
   self.assertEqual([0],recipe['loopStarts'])
   self.assertEqual(list(range(0,16,2)),[f['tile'] for f in recipe['animations'][0]])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/poison_powder.png') as image:
   indices=list(image.tobytes());poison=image.getpalette()
  for name in ['poison-powder','sleep-powder','stun-spore']:
   palette=poison if name=='poison-powder' else [int(c) for row in (REFERENCE/('graphics/battle_anims/sprites/'+name.replace('-','_')+'.pal')).read_text().splitlines()[3:] for c in row.split()]
   blob=(ROOT/('content/generated/battle/animations/physical-'+name+'.rgb555')).read_bytes()
   self.assertEqual((8,128),struct.unpack_from('<HH',blob))
   expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
   self.assertEqual(expected,list(struct.unpack_from('<1024H',blob,4)))
 def test_unknown_callback_fails_closed_at_discovery(self):
  raw=imp.read_sources(REFERENCE)
  raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createvisualtask AnimTask_RotateMonSpriteToSide, 2, 6, 384, ANIM_TARGET, 2',b'createvisualtask UnimplementedRotation, 2, 6, 384, ANIM_TARGET, 2')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(67,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('callback:UnimplementedRotation',next(b for b in blocked if b['moveId']==67)['missing'])
 def test_unsupported_art_is_never_replaced_by_impact(self):
  _,blocked=imp.build(REFERENCE)
  slam=next(b for b in blocked if b['moveId']==282)
  self.assertIn('graphics:ANIM_TAG_SLAM_HIT_2',slam['missing'])
 def test_task_affine_program_is_parsed_as_task_data_not_sprite_affine(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  self.assertEqual({'sSplashEffectAffineAnimCmds','sGrowAndShrinkAffineAnimCmds','sSlackOffSquishAffineAnimCmds','DefenseCurlDeformMonAffineAnimCmds'},set(pack['taskAffinePrograms']))
  commands=pack['taskAffinePrograms']['sSplashEffectAffineAnimCmds']
  self.assertEqual([(-6,4,8),(10,-10,8),(-4,6,8),(0,0,0)],[(c['x'],c['y'],c['duration']) for c in commands])
  self.assertEqual('end',commands[-1]['op'])
  source='const union AffineAnimCmd test[] = {\n AFFINEANIMCMD_FRAME(0x100, 128, -1, 0),\n AFFINEANIMCMD_LOOP(0),\n AFFINEANIMCMD_FRAME(-3, 4, 0, 2),\n AFFINEANIMCMD_LOOP(3),\n AFFINEANIMCMD_JUMP(1),\n AFFINEANIMCMD_END,\n};'
  parsed=imp.task_affine_program(source,'test')
  self.assertEqual(['frame','loop','frame','loop','jump','end'],[c['op'] for c in parsed]);self.assertEqual(255,parsed[0]['rotation']);self.assertEqual(3,parsed[3]['count']);self.assertEqual(1,parsed[4]['target'])
  with self.assertRaises(ValueError):imp.task_affine_program(source.replace('JUMP(1)','JUMP(100)'),'test')
  with self.assertRaises(ValueError):imp.task_affine_program(source.replace('AFFINEANIMCMD_LOOP(3)','UNKNOWN_AFFINE(3)'),'test')
 def test_mud_sport_closure_keeps_static_tile_recipe_and_exact_shared_pixels(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  recipe=next(r for r in pack['spriteRecipes'] if r['template']=='gMudsportMudSpriteTemplate')
  self.assertEqual(('AnimMudSportDirt','mud-sand',16,16),(recipe['callback'],recipe['asset'],recipe['width'],recipe['height']))
  script=next(s for s in pack['scripts'] if s['moveId']==300)
  self.assertEqual(16,sum(c['op']=='createsprite' for c in script['commands']))
  self.assertEqual(10,sum(c['op']=='waitplaysewithpan' and c['args'][1]=='0' for c in script['commands']))
  mud=imp.build_assets(REFERENCE)['mud-sand']
  self.assertEqual((ROOT/'content/generated/battle/animations/elemental-mud-sand.rgb555').read_bytes(),mud)
  self.assertEqual((ROOT/'content/generated/battle/animations/physical-mud-sand.rgb555').read_bytes(),mud)
 def test_missing_splash_or_mud_callback_does_not_admit_a_partial_move(self):
  raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createsprite gMudsportMudSpriteTemplate',b'createsprite MissingMudSportParticle')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertIn(150,{s['moveId'] for s in json.loads(data)['scripts']});self.assertNotIn(300,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('callback:MissingMudSportParticle',next(b for b in blocked if b['moveId']==300)['missing'])
 def test_growth_breath_and_slack_off_share_source_tasks_and_existing_healing_pixels(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  programs=pack['taskAffinePrograms']
  self.assertEqual(48,sum(c['duration'] for c in programs['sGrowAndShrinkAffineAnimCmds']))
  self.assertEqual(56,sum(c['duration'] for c in programs['sSlackOffSquishAffineAnimCmds']))
  puff=next(r for r in pack['spriteRecipes'] if r['template']=='gBreathPuffSpriteTemplate')
  self.assertEqual(('AnimBreathPuff',16,16),(puff['callback'],puff['width'],puff['height']))
  self.assertEqual([0,4,8,12],[f['tile'] for f in puff['animations'][0]])
  self.assertEqual([4,40,4,4],[f['duration'] for f in puff['animations'][0]])
  self.assertTrue(all(f['hFlip'] for f in puff['animations'][0]));self.assertFalse(any(f['hFlip'] for f in puff['animations'][1]))
  healing=next(a for a in pack['assets'] if a['id']=='blue-star');self.assertEqual('elemental-blue-star.rgb555',healing['path'])
  self.assertEqual(imp.build_assets(REFERENCE)['breath'],(ROOT/'content/generated/battle/animations/physical-breath.rgb555').read_bytes())
  root=next(s for s in pack['scripts'] if s['moveId']==303)
  self.assertIn('HealingEffect',[c['args'][0] for c in root['commands'] if c['op']=='call'])
  self.assertEqual(4,sum(c['op']=='createsprite' for c in pack['subroutines']['HealingEffect']))
 def test_finite_sprite_loops_retain_region_and_count_without_expanding_frames(self):
  raw=imp.read_sources(REFERENCE)['src/battle_anim_effects_3.c'].decode()
  def parse(body):
   import re
   source=re.sub(r'(sEclipsingOrbAnimCmds\[\] =\s*\{).*?(\};)',lambda m:m[1]+'\n'+body+'\n'+m[2],raw,flags=re.S)
   return imp.sprite_recipe(source,'gEclipsingOrbSpriteTemplate','eclipsing-orb','AnimSpriteOnMonPos')
  marked=parse('ANIMCMD_FRAME(0, 2),\nANIMCMD_LOOP(0),\nANIMCMD_FRAME(16, 1, .hFlip = TRUE),\nANIMCMD_FRAME(32, 0, .vFlip = TRUE),\nANIMCMD_LOOP(2),\nANIMCMD_FRAME(48, 2),\nANIMCMD_END,')
  self.assertEqual(4,len(marked['animations'][0]));self.assertEqual([[dict(at=3,start=1,count=2)]],marked['frameLoops'])
  sequence=parse('ANIMCMD_FRAME(0, 1),\nANIMCMD_LOOP(1),\nANIMCMD_FRAME(16, 1),\nANIMCMD_LOOP(2),\nANIMCMD_END,')
  self.assertEqual([[dict(at=1,start=0,count=1),dict(at=2,start=1,count=2)]],sequence['frameLoops'])
  jump=parse('ANIMCMD_FRAME(0, 1),\nANIMCMD_FRAME(16, 2),\nANIMCMD_LOOP(1),\nANIMCMD_FRAME(32, 1),\nANIMCMD_JUMP(3),')
  self.assertEqual([2],jump['loopStarts']);self.assertEqual([[dict(at=2,start=0,count=1)]],jump['frameLoops'])
  for body in ['ANIMCMD_FRAME(0, 1),\nANIMCMD_LOOP(64),\nANIMCMD_END,','ANIMCMD_FRAME(0, 1),\nANIMCMD_LOOP(1),\nANIMCMD_LOOP(3),\nANIMCMD_END,','ANIMCMD_FRAME(0, 1),\nANIMCMD_LOOP(1),\nANIMCMD_JUMP(1),']:
   with self.assertRaises(ValueError):parse(body)
 def test_defense_curl_has_the_complete_deformation_palette_and_finite_orb_closure(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);script=next(s for s in pack['scripts'] if s['moveId']==111)
  grayscale=[c for c in script['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_SetGrayscaleOrOriginalPal']
  self.assertEqual(['0','1'],[c['args'][-1] for c in grayscale])
  orb=next(r for r in pack['spriteRecipes'] if r['template']=='gEclipsingOrbSpriteTemplate')
  self.assertEqual([[dict(at=7,start=0,count=1)]],orb['frameLoops']);self.assertEqual(7,len(orb['animations'][0]))
  self.assertEqual(['frame','frame','loop','end'],[c['op'] for c in pack['taskAffinePrograms']['DefenseCurlDeformMonAffineAnimCmds']])
  self.assertEqual(imp.build_assets(REFERENCE)['eclipsing-orb'],(ROOT/'content/generated/battle/animations/physical-eclipsing-orb.rgb555').read_bytes())
 def test_defense_curl_is_rejected_if_a_required_palette_callback_is_missing(self):
  raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createvisualtask AnimTask_SetGrayscaleOrOriginalPal',b'createvisualtask MissingGrayscale')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(111,{s['moveId'] for s in json.loads(data)['scripts']});self.assertIn('callback:MissingGrayscale',next(b for b in blocked if b['moveId']==111)['missing'])
 def test_attached_callbacks_reuse_frame_and_affine_recipes_without_admitting_unclosed_roots(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);roots={s['moveId'] for s in pack['scripts']}
  self.assertTrue({207,321}<=roots);self.assertFalse(roots&{99,137,184,218,259,269})
  eye=next(r for r in pack['spriteRecipes'] if r['template']=='gEyeSparkleSpriteTemplate');anger=next(r for r in pack['spriteRecipes'] if r['template']=='gAngerMarkSpriteTemplate')
  self.assertEqual([0,4,8,4,0],[f['tile'] for f in eye['animations'][0]]);self.assertEqual([4]*5,[f['duration'] for f in eye['animations'][0]])
  self.assertEqual('AnimAngerMark',anger['callback']);self.assertFalse(anger['affineDoubleSize'])
  self.assertEqual([(11,11,8),(-11,-11,8),(0,0,0)],[(c['x'],c['y'],c['duration']) for c in anger['affineAnimations'][0]])
 def test_sprite_sheet_registration_limits_loaded_tiles_instead_of_loading_unused_png_art(self):
  assets=imp.build_assets(REFERENCE)
  self.assertEqual((16,48),struct.unpack_from('<HH',assets['eye-sparkle']));self.assertEqual((16,16),struct.unpack_from('<HH',assets['anger']))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/eye_sparkle.png') as image:
   indices=list(image.tobytes());palette=image.getpalette();self.assertEqual((16,64),image.size);self.assertTrue(any(indices[16*48:]))
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices[:16*48]]
  self.assertEqual(expected,list(struct.unpack_from('<768H',assets['eye-sparkle'],4)))
  for name in ['eye-sparkle','anger']:self.assertEqual(assets[name],(ROOT/('content/generated/battle/animations/physical-'+name+'.rgb555')).read_bytes())
  raw=imp.read_sources(REFERENCE);raw['src/data/battle_anim.h']=raw['src/data/battle_anim.h'].replace(b'gBattleAnimSpriteGfx_EyeSparkle, 0x0180',b'gBattleAnimSpriteGfx_EyeSparkle, 0x0190')
  with self.assertRaises(ValueError):imp.registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_EyeSparkle','eye_sparkle')
 def test_attached_move_closures_are_rejected_when_other_required_callbacks_are_missing(self):
  raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createsprite gAngerMarkSpriteTemplate',b'createsprite MissingAnger').replace(b'createvisualtask AnimTask_RockMonBackAndForth',b'createvisualtask MissingRock')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertFalse({207,321}&{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('callback:MissingAnger',next(b for b in blocked if b['moveId']==207)['missing']);self.assertIn('callback:MissingRock',next(b for b in blocked if b['moveId']==321)['missing'])
 def test_ordinary_multihit_keeps_both_source_branches_and_shared_callbacks(self):
  data,_=imp.build(REFERENCE);roots={s['moveId']:s for s in json.loads(data)['scripts']}
  for move,name in [(3,'DoubleSlap'),(4,'CometPunch')]:
   root=roots[move];choice=next(c for c in root['commands'] if c['op']=='choosetwoturnanim')
   self.assertEqual([name+'Left',name+'Right'],choice['args'])
   for side,x in [('Left','-8'),('Right','8')]:
    first=root['commands'][root['labels'][name+side]]
    self.assertEqual('gBasicHitSplatSpriteTemplate',first['args'][0]);self.assertEqual(x,first['args'][3])
   self.assertEqual(2,sum(c['op']=='goto' and c['args']==[name+'Continue'] for c in root['commands']))
   self.assertEqual(2 if move==4 else 0,sum(c['op']=='createsprite' and c['args'][0]=='gFistFootSpriteTemplate' for c in root['commands']))
 def test_multihit_rejects_an_unclosed_branch_or_cycle_instead_of_using_the_other_side(self):
  raw=imp.read_sources(REFERENCE)
  for old,new in [(b'choosetwoturnanim DoubleSlapLeft, DoubleSlapRight',b'choosetwoturnanim DoubleSlapLeft, MissingRight'),
                  (b'choosetwoturnanim DoubleSlapLeft, DoubleSlapRight',b'choosetwoturnanim DoubleSlapLeft'),
                  (b'goto DoubleSlapContinue',b'goto DoubleSlapLeft')]:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(3,{s['moveId'] for s in json.loads(data)['scripts']});self.assertIn(4,{s['moveId'] for s in json.loads(data)['scripts']})
   self.assertTrue(any(x.startswith('control-flow:') for x in next(b for b in blocked if b['moveId']==3)['missing']))
 def test_complete_pixels_do_not_admit_an_unsupported_move_turn_invocation(self):
  data,blocked=imp.build(REFERENCE);self.assertNotIn(251,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('invocation:move-turn',next(b for b in blocked if b['moveId']==251)['missing'])
  raw=imp.read_sources(REFERENCE);raw['src/data/battle_moves.h']=raw['src/data/battle_moves.h'].replace(b'[MOVE_DOUBLE_SLAP] =\n    {\n        .effect = EFFECT_MULTI_HIT,',b'[MOVE_DOUBLE_SLAP] =\n    {\n        .effect = EFFECT_BEAT_UP,')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(3,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('invocation:move-turn',next(b for b in blocked if b['moveId']==3)['missing'])
 def test_conditional_move_turn_retains_three_kick_branches_and_fallthrough(self):
  data,_=imp.build(REFERENCE);roots={s['moveId']:s for s in json.loads(data)['scripts']};root=roots[167]
  self.assertEqual([['0','TripleKickLeft'],['1','TripleKickRight']],[c['args'] for c in root['commands'] if c['op']=='jumpifmoveturn'])
  self.assertEqual(['TripleKickCenter'],root['commands'][7]['args'])
  for label,x,y,affine in [('Left','-16','-16','2'),('Right','8','0','2'),('Center','0','-8','1')]:
   index=root['labels']['TripleKick'+label];fist,hit=root['commands'][index:index+2]
   self.assertEqual('gFistFootSpriteTemplate',fist['args'][0]);self.assertEqual(['20','1','1'],fist['args'][5:])
   self.assertEqual(['gBasicHitSplatSpriteTemplate','ANIM_TARGET','3',x,y,'ANIM_TARGET',affine],hit['args'])
  self.assertEqual(3,sum(c['op']=='goto' and c['args']==['TripleKickContinue'] for c in root['commands']))
 def test_conditional_branch_rejects_unknown_targets_cycles_and_nonbyte_checks(self):
  raw=imp.read_sources(REFERENCE)
  for old,new in [(b'jumpifmoveturn 0, TripleKickLeft',b'jumpifmoveturn 0, MissingKickLeft'),
                  (b'jumpifmoveturn 0, TripleKickLeft',b'jumpifmoveturn 256, TripleKickLeft'),
                  (b'jumpifmoveturn 0, TripleKickLeft',b'jumpifmoveturn -1, TripleKickLeft'),
                  (b'jumpifmoveturn 0, TripleKickLeft',b'jumpifmoveturn 0'),
                  (b'goto TripleKickContinue',b'goto TripleKickLeft')]:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):data,blocked=imp.build(REFERENCE)
   roots={s['moveId'] for s in json.loads(data)['scripts']};self.assertNotIn(167,roots);self.assertTrue({3,4}<=roots)
   self.assertTrue(any(x.startswith('control-flow:') for x in next(b for b in blocked if b['moveId']==167)['missing']))
 def test_conditional_move_turn_requires_its_exact_source_invocation_effect(self):
  raw=imp.read_sources(REFERENCE);raw['src/data/battle_moves.h']=raw['src/data/battle_moves.h'].replace(b'[MOVE_TRIPLE_KICK] =\n    {\n        .effect = EFFECT_TRIPLE_KICK,',b'[MOVE_TRIPLE_KICK] =\n    {\n        .effect = EFFECT_MULTI_HIT,')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(167,{s['moveId'] for s in json.loads(data)['scripts']});self.assertIn('invocation:move-turn',next(b for b in blocked if b['moveId']==167)['missing'])
 def test_bone_closures_share_source_linear_affine_recipe_and_generated_dependencies(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);roots={s['moveId']:s for s in pack['scripts']}
  self.assertTrue({125,198}<=roots.keys());self.assertNotIn(155,roots)
  bone=next(r for r in pack['spriteRecipes'] if r['template']=='gSpinningBoneSpriteTemplate')
  self.assertEqual(('bone','AnimBoneHitProjectile',32,32),(bone['asset'],bone['callback'],bone['width'],bone['height']))
  self.assertFalse(bone['affineDoubleSize']);self.assertFalse(bone['blend'])
  self.assertEqual([dict(op='frame',x=0,y=0,rotation=20,duration=1,target=0),dict(op='jump',x=0,y=0,rotation=0,duration=0,target=0)],bone['affineAnimations'][0])
  for move in [125,198]:
   root=roots[move];projectile=next(c for c in root['commands'] if c['op']=='createsprite' and c['args'][0]=='gSpinningBoneSpriteTemplate')
   self.assertEqual(['ANIM_ATTACKER','2','-42','-25','0','0','15'],projectile['args'][1:])
   self.assertEqual(180,root['soundEffects']['SE_M_BONEMERANG']);self.assertEqual(10000,root['spriteTags']['ANIM_TAG_BONE'])
   self.assertEqual(1,sum(c['op']=='createsprite' and c['args'][0]=='gBasicHitSplatSpriteTemplate' for c in root['commands']))
   self.assertEqual(move==125,any(c['op']=='splitbgprio' for c in root['commands']))
   self.assertEqual(move==125,any(c['op']=='createsprite' and c['args'][0]=='gComplexPaletteBlendSpriteTemplate' for c in root['commands']))
 def test_bone_pixels_are_exact_registered_source_sheet_without_runtime_raw_dependency(self):
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/bone.png') as image:
   self.assertEqual((32,32),image.size);indices=list(image.tobytes());palette=image.getpalette()
  blob=imp.build_assets(REFERENCE)['bone'];self.assertEqual((32,32),struct.unpack_from('<HH',blob));self.assertEqual(2052,len(blob))
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<1024H',blob,4)))
  self.assertEqual(blob,(ROOT/'content/generated/battle/animations/physical-bone.rgb555').read_bytes())
 def test_bone_callback_graph_or_required_sound_and_tag_cannot_fall_back(self):
  raw=imp.read_sources(REFERENCE);changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createsprite gSpinningBoneSpriteTemplate',b'createsprite MissingBoneProjectile')
  with patch.object(imp,'read_sources',return_value=changed):data,_=imp.build(REFERENCE)
  self.assertFalse({125,198}&{s['moveId'] for s in json.loads(data)['scripts']})
  changed=dict(raw);changed['include/constants/battle_anim.h']=raw['include/constants/battle_anim.h'].replace(b'#define ANIM_TAG_BONE ',b'#define REMOVED_TAG_BONE ')
  with patch.object(imp,'read_sources',return_value=changed):
   with self.assertRaisesRegex(ValueError,'Undefined source sprite tag'):imp.build(REFERENCE)
  changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'SE_M_BONEMERANG',b'SE_M_MISSING_BONE')
  with patch.object(imp,'read_sources',return_value=changed):
   with self.assertRaisesRegex(ValueError,'Undefined source sound'):imp.build(REFERENCE)
  changed=dict(raw);changed['src/battle_anim_ground.c']=raw['src/battle_anim_ground.c'].replace(b'.callback = AnimBoneHitProjectile,',b'.callback = UnknownBoneCallback,')
  with patch.object(imp,'read_sources',return_value=changed):
   with self.assertRaisesRegex(ValueError,'Elemental recipe differs'):imp.build(REFERENCE)
 def test_physical_sound_and_tag_metadata_covers_actual_root_and_called_dependencies(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  for root in pack['scripts']:
   commands=root['commands']+[row for c in root['commands'] if c['op']=='call' for row in pack['subroutines'][c['args'][0]]]
   self.assertEqual(imp.elemental.sound_effects(REFERENCE,commands),root['soundEffects'])
   self.assertEqual({c['args'][0] for c in commands if c['op']=='loadspritegfx'},set(root['spriteTags']))
  slack=next(s for s in pack['scripts'] if s['moveId']==303)
  self.assertIn('SE_M_ABSORB_2',slack['soundEffects'])
 def test_claw_trio_uses_one_shared_nonaffine_recipe_and_retains_unused_loads(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);roots={s['moveId']:s for s in pack['scripts']}
  self.assertTrue({232,306,337}<=roots.keys());claw=next(r for r in pack['spriteRecipes'] if r['template']=='gClawSlashSpriteTemplate')
  self.assertEqual(('AnimClawSlash','claw-slash',32,32),(claw['callback'],claw['asset'],claw['width'],claw['height']))
  self.assertEqual(2,len(claw['animations']));self.assertEqual([],claw['affineAnimations']);self.assertFalse(claw['affineDoubleSize']);self.assertFalse(claw['blend'])
  for index,frames in enumerate(claw['animations']):
   self.assertEqual([0,16,32,48,64],[f['tile'] for f in frames]);self.assertEqual([4]*5,[f['duration'] for f in frames]);self.assertTrue(all(f['hFlip']==bool(index) and not f['vFlip'] for f in frames))
  crush=roots[306];self.assertEqual(['ANIM_TAG_BLUE_LIGHT_WALL','ANIM_TAG_CLAW_SLASH','ANIM_TAG_TORN_METAL'],[c['args'][0] for c in crush['commands'] if c['op']=='loadspritegfx'])
  for root in [roots[232],roots[337]]:
   self.assertEqual([['-4','1','10','3','1']]*2,[c['args'][3:] for c in root['commands'] if c['op']=='createsprite' and c['args'][0]=='gShakeMonOrTerrainSpriteTemplate'])
 def test_claw_and_torn_metal_pixels_use_registered_sizes_and_palette_alias(self):
  assets=imp.build_assets(REFERENCE)
  self.assertEqual((32,160),struct.unpack_from('<HH',assets['claw-slash']));self.assertEqual((128,32),struct.unpack_from('<HH',assets['torn-metal']))
  palette=[tuple(map(int,row.split())) for row in (REFERENCE/'graphics/battle_anims/sprites/blue_light_wall.pal').read_text().splitlines()[3:]]
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/torn_metal.png') as image:indices=list(image.tobytes())
  expected=[sum((palette[i][c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<4096H',assets['torn-metal'],4)))
  raw=imp.read_sources(REFERENCE);raw['src/data/battle_anim.h']=raw['src/data/battle_anim.h'].replace(b'gBattleAnimSpritePal_BlueLightWall, ANIM_TAG_TORN_METAL',b'gBattleAnimSpritePal_Missing, ANIM_TAG_TORN_METAL')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaisesRegex(ValueError,'palette alias'):imp.build_assets(REFERENCE)
  raw=imp.read_sources(REFERENCE);self.assertEqual(assets['claw-slash'],imp.registered_sprite_pixels(raw,'gBattleAnimSpriteGfx_ClawSlash','claw_slash'))
 def test_dragon_claw_reuses_identical_fire_vortex_and_retains_rgb_expression(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);root=next(s for s in pack['scripts'] if s['moveId']==337)
  elemental=json.loads((ROOT/'content/generated/battle/animations/elemental.json').read_bytes())
  self.assertEqual(next(r for r in elemental['spriteRecipes'] if r['template']=='gFireSpinSpriteTemplate'),next(r for r in pack['spriteRecipes'] if r['template']=='gFireSpinSpriteTemplate'))
  blends=[c for c in root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_BlendBattleAnimPal']
  self.assertEqual(7,len(blends[0]['args']));self.assertEqual('RGB(31, 19, 0)',blends[0]['args'][-1])
  self.assertEqual(['DragonClawFireSpiral']*2,[c['args'][0] for c in root['commands'] if c['op']=='call'])
  spiral=pack['subroutines']['DragonClawFireSpiral'];self.assertTrue(all(c['args'][-1]=='ANIM_ATTACKER' for c in spiral if c['op']=='createsprite'))
 def test_visual_sound_wrappers_resolve_source_symbols_generically(self):
  data,_=imp.build(REFERENCE);root=next(s for s in json.loads(data)['scripts'] if s['moveId']==337)
  self.assertEqual(2,sum(c['op']=='createvisualtask' and c['args'][0]=='SoundTask_PlaySE1WithPanning' for c in root['commands']))
  for name in ['SoundTask_PlaySE1WithPanning','SoundTask_PlaySE2WithPanning']:
   self.assertEqual({'SE_M_RAZOR_WIND':root['soundEffects']['SE_M_RAZOR_WIND']},imp.elemental.sound_effects(REFERENCE,[dict(op='createvisualtask',args=[name,'5','SE_M_RAZOR_WIND','SOUND_PAN_TARGET'])]))
   with self.assertRaisesRegex(ValueError,'Undefined source sound'):imp.elemental.sound_effects(REFERENCE,[dict(op='createvisualtask',args=[name,'5','SE_M_UNKNOWN_CLAW','SOUND_PAN_TARGET'])])
 def test_missing_claw_required_callback_or_load_blocks_the_complete_root(self):
  raw=imp.read_sources(REFERENCE)
  for old,new,expected in [(b'createsprite gClawSlashSpriteTemplate',b'createsprite MissingClaw',{232,306,337}),(b'createsprite gShakeMonOrTerrainSpriteTemplate',b'createsprite MissingTerrain',{232,337}),(b'createvisualtask SoundTask_PlaySE1WithPanning',b'createvisualtask MissingSoundWrapper',{337}),(b'loadspritegfx ANIM_TAG_TORN_METAL',b'loadspritegfx ANIM_TAG_UNKNOWN_CLAW',{306})]:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):data,_=imp.build(REFERENCE)
   self.assertFalse(expected&{s['moveId'] for s in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
