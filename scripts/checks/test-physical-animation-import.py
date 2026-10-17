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
 def test_wish_reuses_existing_gold_and_granting_assets_with_exact_source_program(self):
  pack=json.loads(imp.build(REFERENCE)[0]);script=next(s for s in pack['scripts'] if s['moveId']==273)
  self.assertEqual({'SE_M_HEAL_BELL':188,'SE_M_REFLECT':200},script['soundEffects'])
  self.assertEqual({'ANIM_TAG_GOLD_STARS':10233,'ANIM_TAG_SPARKLE_2':10049},script['spriteTags'])
  self.assertEqual([['SE_M_REFLECT','SOUND_PAN_TARGET','SOUND_PAN_ATTACKER','-3','0']],[c['args'] for c in script['commands'] if c['op']=='panse_adjustall'])
  self.assertEqual([['gWishStarSpriteTemplate','ANIM_ATTACKER','40']],[c['args'] for c in script['commands'] if c['op']=='createsprite' and c['args'][0]=='gWishStarSpriteTemplate'])
  self.assertEqual([['60']],[c['args'] for c in script['commands'] if c['op']=='delay'])
  self.assertEqual(2,sum(c['op']=='createsprite' and c['args'][0]=='gGrantingStarsSpriteTemplate' for c in pack['subroutines']['GrantingStarsEffect']))
  recipe=next(r for r in pack['spriteRecipes'] if r['template']=='gWishStarSpriteTemplate')
  self.assertEqual(('gold-stars','AnimWishStar',16,16,False,[]),(recipe['asset'],recipe['callback'],recipe['width'],recipe['height'],recipe['affineDoubleSize'],recipe['affineAnimations']))
  self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],recipe['animations']);self.assertEqual([0],recipe['loopStarts'])
 def test_wish_missing_called_dependency_or_load_keeps_root_out(self):
  for old,new in [(b'createsprite gWishStarSpriteTemplate,',b'createsprite gUnknownWishSpriteTemplate,'),(b'createsprite gGrantingStarsSpriteTemplate,',b'createsprite gUnknownGrantingStarTemplate,'),(b'loadspritegfx ANIM_TAG_SPARKLE_2',b'loadspritegfx ANIM_TAG_UNKNOWN_WISH')]:
   raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(273,{s['moveId'] for s in json.loads(data)['scripts']})
 def test_wish_template_source_shape_and_dummy_frames_fail_closed(self):
  import re
  raw=imp.read_sources(REFERENCE);source=raw['src/battle_anim_effects_3.c'];block=re.search(rb'const struct SpriteTemplate gWishStarSpriteTemplate =\s*\{.*?\};',source,re.S)[0]
  for old,new in [(b'AffineOff',b'AffineNormal'),(b'AnimWishStar,',b'UnknownWishStar,'),(b'gDummySpriteAnimTable',b'UnknownFrameTable')]:
   changed=dict(raw);changed['src/battle_anim_effects_3.c']=source.replace(block,block.replace(old,new))
   with patch.object(imp,'read_sources',return_value=changed):
    with self.assertRaisesRegex(ValueError,'star|Star'):imp.build(REFERENCE)
 def test_adjustall_sound_is_shared_generated_dependency_not_a_callback_fallback(self):
  self.assertEqual({'SE_M_REFLECT':200},imp.elemental.sound_effects(REFERENCE,[dict(op='panse_adjustall',args=['SE_M_REFLECT','63','-64','-3','0'])]))
  with self.assertRaisesRegex(ValueError,'Undefined source sound dependency'):imp.elemental.sound_effects(REFERENCE,[dict(op='panse_adjustall',args=['SE_UNKNOWN_WISH','63','-64','-3','0'])])
 def test_meteor_source_dependency_closure_and_authored_schedule(self):
  pack=json.loads(imp.build(REFERENCE)[0]);script=next(s for s in pack['scripts'] if s['moveId']==309)
  self.assertEqual({'SE_M_BARRIER':201,'SE_M_VITAL_THROW2':116},script['soundEffects'])
  self.assertEqual({'ANIM_TAG_GOLD_STARS':10233,'ANIM_TAG_HANDS_AND_FEET':10143,'ANIM_TAG_IMPACT':10135},script['spriteTags'])
  self.assertEqual(16,pack['backgroundSymbols']['BG_COSMIC'])
  self.assertEqual([['-48','-64','72','32','30'],['-112','-64','8','32','30'],['-80','-64','40','32','30']],[c['args'][3:] for c in script['commands'] if c['op']=='createsprite' and c['args'][0]=='gMeteorMashStarSpriteTemplate'])
  self.assertEqual(['10','40','20','10'],[c['args'][0] for c in script['commands'] if c['op']=='delay'])
  self.assertEqual(1,sum(c['op']=='restorebg' for c in script['commands']))
  for name,size,callback in [('gMeteorMashStarSpriteTemplate',16,'AnimMeteorMashStar'),('gMiniTwinklingStarSpriteTemplate',8,'AnimMiniTwinklingStar')]:
   r=next(r for r in pack['spriteRecipes'] if r['template']==name)
   self.assertEqual(('gold-stars',callback,size,size,False,[]),(r['asset'],r['callback'],r['width'],r['height'],r['affineDoubleSize'],r['affineAnimations']))
   self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([0],r['loopStarts'])
 def test_gold_star_mixed_obj_sizes_share_exact_registered_source_sheet(self):
  blob=imp.build_assets(REFERENCE)['gold-stars'];self.assertEqual((16,24),struct.unpack_from('<HH',blob));self.assertEqual(4+0xc0*4,len(blob))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/gold_stars.png') as image:
   palette=image.getpalette();indices=list(image.tobytes())
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<384H',blob,4)))
 def test_meteor_shape_and_registration_mutations_fail_closed(self):
  for path,old,new in [('src/battle_anim_effects_3.c',b'.oam = &gOamData_AffineOff_ObjNormal_16x16,\n    .anims = gDummySpriteAnimTable,\n    .images = NULL,\n    .affineAnims = gDummySpriteAffineAnimTable,\n    .callback = AnimMeteorMashStar,',b'.oam = &gOamData_AffineNormal_ObjNormal_16x16,\n    .anims = gDummySpriteAnimTable,\n    .images = NULL,\n    .affineAnims = gDummySpriteAffineAnimTable,\n    .callback = AnimMeteorMashStar,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_GoldStars, 0x00c0, ANIM_TAG_GOLD_STARS}',b'{gBattleAnimSpriteGfx_GoldStars, 0x0100, ANIM_TAG_GOLD_STARS}')]:
   raw=imp.read_sources(REFERENCE);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaisesRegex(ValueError,'star|Star'):imp.build(REFERENCE)
 def test_meteor_missing_parent_callback_is_a_dependency_gap(self):
  raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createsprite gMeteorMashStarSpriteTemplate,',b'createsprite gUnknownMeteorSpriteTemplate,',1)
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(309,{s['moveId'] for s in json.loads(data)['scripts']});self.assertIn('callback:gUnknownMeteorSpriteTemplate',next(s for s in blocked if s['moveId']==309)['missing'])
 def test_bullet_seed_retains_authored_particles_and_callback_only_sound(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);script=next(s for s in pack['scripts'] if s['moveId']==331)
  self.assertEqual({'SE_M_HORN_ATTACK':159},script['soundEffects'])
  self.assertFalse(any(c['op'].startswith('playse') for c in script['commands']))
  particles=[c for c in script['commands'] if c['op']=='createsprite']
  self.assertEqual(10,len(particles));self.assertTrue(all(c['args']==['gBulletSeedSpriteTemplate','ANIM_TARGET','2','20','0'] for c in particles))
  self.assertEqual(9,sum(c['op']=='delay' and c['args']==['5'] for c in script['commands']))
  recipe=next(r for r in pack['spriteRecipes'] if r['template']=='gBulletSeedSpriteTemplate')
  self.assertEqual(('seed',16,16,False),(recipe['asset'],recipe['width'],recipe['height'],recipe['affineDoubleSize']))
  self.assertEqual([{'op':'frame','x':0,'y':0,'rotation':20,'duration':1,'target':0},{'op':'jump','x':0,'y':0,'rotation':0,'duration':0,'target':0}],recipe['affineAnimations'][0])
  self.assertEqual(4+0x180*4,len(imp.build_assets(REFERENCE)['seed']))
 def test_bullet_seed_callback_sound_is_extracted_not_a_fallback(self):
  raw=imp.read_sources(REFERENCE)
  raw['src/battle_anim_effects_2.c']=raw['src/battle_anim_effects_2.c'].replace(b'PlaySE12WithPanning(SE_M_HORN_ATTACK,',b'PlaySE12WithPanning(SE_M_UNKNOWN_SEED,')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaisesRegex(ValueError,'Undefined source sound dependency'):imp.build(REFERENCE)
 def test_bullet_seed_source_oam_and_registered_sheet_are_not_approximated(self):
  for path,old,new in [('src/battle_anim_effects_2.c',b'.oam = &gOamData_AffineNormal_ObjNormal_16x16,\n    .anims = gDummySpriteAnimTable,\n    .images = NULL,\n    .affineAnims = sBulletSeedAffineAnimTable,',b'.oam = &gOamData_AffineOff_ObjNormal_16x16,\n    .anims = gDummySpriteAnimTable,\n    .images = NULL,\n    .affineAnims = sBulletSeedAffineAnimTable,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Seed, 0x0180, ANIM_TAG_SEED}',b'{gBattleAnimSpriteGfx_Seed, 0x0100, ANIM_TAG_SEED}')]:
   raw=imp.read_sources(REFERENCE);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaisesRegex(ValueError,'[Ss]eed source'):imp.build(REFERENCE)
 def test_regeneration_matches_generated_only_runtime_input(self):
  data,_=imp.build(REFERENCE)
  self.assertEqual(data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
  scripts=json.loads(data)['scripts'];self.assertEqual({273,309,154,292,162,305,331,28,189,5,25,299,42,333,40,41,131,232,306,337,20,35,2,3,4,125,198,167,66,11,21,22,23,26,27,29,34,36,38,64,67,70,77,78,79,136,150,289,300,303,339,111,207,321},{s['moveId'] for s in scripts})
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
   self.assertEqual(imp.physical_sound_effects(REFERENCE,commands,imp.read_sources(REFERENCE)['src/battle_anim_effects_2.c'].decode()),root['soundEffects'])
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
 def test_shared_stinger_callback_closes_exact_three_roots_with_existing_materials(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);scripts={s['moveId']:s for s in pack['scripts']};recipes={r['template']:r for r in pack['spriteRecipes']}
  stinger=recipes['gLinearStingerSpriteTemplate'];self.assertEqual(('needle','AnimTranslateStinger',16,16,False,False,[]),(stinger['asset'],stinger['callback'],stinger['width'],stinger['height'],stinger['blend'],stinger['affineDoubleSize'],stinger['affineAnimations']))
  for move,particles in [(40,1),(41,2),(131,3)]:
   script=scripts[move];self.assertEqual(particles,sum(c['op']=='createsprite' and c['args'][0]=='gLinearStingerSpriteTemplate' for c in script['commands']))
   self.assertIn('ANIM_TAG_NEEDLE',script['spriteTags']);self.assertIn('SE_M_RAZOR_WIND2',script['soundEffects'])
  self.assertEqual({'needle':'elemental-needle.rgb555','poison-bubble':'elemental-poison-bubble.rgb555'},{a['id']:a['path'] for a in pack['assets'] if a['id'] in {'needle','poison-bubble'}})
  poison=pack['subroutines']['PoisonBubblesEffect'];self.assertEqual(6,sum(c['op']=='createsprite' for c in poison));self.assertEqual(6,sum(c['op']=='playsewithpan' and c['args'][0]=='SE_M_TOXIC' for c in poison))
  hit=recipes['gHandleInvertHitSplatSpriteTemplate'];self.assertEqual('AnimHitSplatHandleInvert',hit['callback']);self.assertEqual(4,len(hit['affineAnimations']));self.assertTrue(hit['blend']);self.assertFalse(hit['affineDoubleSize'])
 def test_missing_stinger_wrapper_or_poison_callback_cannot_admit_partial_roots(self):
  for callback,blocked_roots in [('gLinearStingerSpriteTemplate',{40,41,131}),('gHandleInvertHitSplatSpriteTemplate',{41,131}),('gPoisonBubbleSpriteTemplate',{40})]:
   raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(('createsprite '+callback).encode(),b'createsprite MissingNeedleDependency')
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertFalse(blocked_roots&{s['moveId'] for s in json.loads(data)['scripts']})
 def test_stinger_and_inverted_hit_recipes_reject_unauthenticated_callbacks(self):
  for file,template,callback in [('battle_anim_bug.c','gLinearStingerSpriteTemplate','AnimTranslateStinger'),('battle_anim_normal.c','gHandleInvertHitSplatSpriteTemplate','AnimHitSplatHandleInvert')]:
   source=imp.read_sources(REFERENCE)['src/'+file].decode()
   with self.assertRaises(ValueError):imp.elemental.recipe(source.replace('.callback = '+callback, '.callback = UnknownNeedleCallback'),template,callback)
 def test_arc_projectiles_share_exact_callback_and_three_authored_particles(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  for move,template,asset,size,sound in [(42,'gPinMissileSpriteTemplate','needle',16,'SE_M_JUMP_KICK'),(333,'gIcicleSpearSpriteTemplate','icicle-spear',32,'SE_M_ICY_WIND')]:
   r=recipes[template];self.assertEqual(('AnimMissileArc',asset,size,size,False,False,[]),(r['callback'],r['asset'],r['width'],r['height'],r['blend'],r['affineDoubleSize'],r['affineAnimations']))
   script=next(s for s in pack['scripts'] if s['moveId']==move)
   particles=[c['args'][3:] for c in script['commands'] if c['op']=='createsprite' and c['args'][0]==template]
   self.assertEqual([['20','-8','-8','-8','20','-32'],['20','-8','8','8','20','-40'],['20','-8','0','0','20','-32']],particles)
   self.assertEqual(3,sum(c['op']=='createsprite' and c['args'][0]=='gBasicHitSplatSpriteTemplate' for c in script['commands']))
   self.assertEqual(3,sum(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ShakeMon2' for c in script['commands']))
   self.assertEqual({'ANIM_TAG_IMPACT','ANIM_TAG_'+asset.upper().replace('-','_')},set(script['spriteTags']))
   self.assertEqual({'SE_M_HORN_ATTACK',sound},set(script['soundEffects']))
 def test_icicle_registered_sheet_preserves_source_palette_indices(self):
  blob=imp.build_assets(REFERENCE)['icicle-spear'];self.assertEqual((32,32),struct.unpack_from('<HH',blob));self.assertEqual(2052,len(blob))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/icicle_spear.png') as image:indices=list(image.tobytes());palette=image.getpalette()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<1024H',blob,4)))
  self.assertEqual(blob,(ROOT/'content/generated/battle/animations/physical-icicle-spear.rgb555').read_bytes())
 def test_missing_arc_callback_or_icicle_load_cannot_admit_partial_root(self):
  for old,new,expected in [(b'createsprite gPinMissileSpriteTemplate',b'createsprite MissingArc',{42}),(b'createsprite gIcicleSpearSpriteTemplate',b'createsprite MissingArc',{333}),(b'loadspritegfx ANIM_TAG_ICICLE_SPEAR',b'loadspritegfx ANIM_TAG_UNKNOWN_ICICLE',{333})]:
   raw=imp.read_sources(REFERENCE);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,_=imp.build(REFERENCE)
   self.assertFalse(expected&{s['moveId'] for s in json.loads(data)['scripts']})
 def test_arc_templates_and_registered_palette_must_match_source_oam_contract(self):
  raw=imp.read_sources(REFERENCE)
  for old,new in [(b'.callback = AnimMissileArc',b'.callback = UnknownArc'),(b'&gOamData_AffineNormal_ObjNormal_16x16',b'&gOamData_AffineOff_ObjNormal_16x16'),(b'&gOamData_AffineNormal_ObjNormal_32x32',b'&gOamData_AffineDouble_ObjNormal_32x32')]:
   changed=dict(raw);changed['src/battle_anim_bug.c']=raw['src/battle_anim_bug.c'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):
    with self.assertRaisesRegex(ValueError,'Arc-projectile source template'):imp.build(REFERENCE)
  changed=dict(raw);changed['src/data/battle_anim.h']=raw['src/data/battle_anim.h'].replace(b'{gBattleAnimSpritePal_IcicleSpear, ANIM_TAG_ICICLE_SPEAR}',b'{gBattleAnimSpritePal_IcicleSpear, ANIM_TAG_NEEDLE}')
  with patch.object(imp,'read_sources',return_value=changed):
   with self.assertRaisesRegex(ValueError,'Icicle source sheet or palette'):imp.build_assets(REFERENCE)
 def test_spinning_family_uses_shared_frames_and_exact_affine_programs(self):
  data,blocked=imp.build(REFERENCE);pack=json.loads(data)
  self.assertTrue({5,25,299}<={r['moveId'] for r in pack['scripts']})
  for name,delta in [('gMegaPunchKickSpriteTemplate',-4),('gSpinningHandOrFootSpriteTemplate',-8)]:
   recipe=next(r for r in pack['spriteRecipes'] if r['template']==name)
   self.assertEqual(('hands-and-feet','AnimSpinningKickOrPunch',32,32,True),(recipe['asset'],recipe['callback'],recipe['width'],recipe['height'],recipe['affineDoubleSize']))
   self.assertEqual([0,16,32,48,48],[r[0]['tile'] for r in recipe['animations']]);self.assertEqual([-1]*5,recipe['loopStarts'])
   self.assertEqual([('frame',256,256,0,0,0),('frame',delta,delta,20,1,0),('jump',0,0,0,0,1)],[(r['op'],r['x'],r['y'],r['rotation'],r['duration'],r['target']) for r in recipe['affineAnimations'][0]])
  self.assertIn('FireSpreadEffect',pack['subroutines'])
 def test_impact_helper_preserves_scoped_return_and_exact_target_side_predicates(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);body=pack['subroutines']['SetImpactBackground'];labels=pack['subroutineLabels']['SetImpactBackground']
  self.assertEqual({'SetImpactBackgroundRet':5,'SetImpactOpponentBG':6,'SetImpactPlayerBG':8},labels)
  self.assertEqual(['AnimTask_IsContest','AnimTask_IsTargetPlayerSide'],[r['args'][0] for r in body if r['op']=='createvisualtask'])
  self.assertEqual([['7','0','SetImpactOpponentBG'],['7','1','SetImpactPlayerBG']],[r['args'] for r in body if r['op']=='jumpargeq'])
  self.assertEqual('return',body[labels['SetImpactBackgroundRet']]['op']);self.assertEqual('goto',body[-1]['op'])
  self.assertEqual({'BG_IMPACT_OPPONENT':4,'BG_IMPACT_PLAYER':5,'BG_COSMIC':16},pack['backgroundSymbols'])
  self.assertFalse(any('Contests' in str(r) for r in body));self.assertNotIn('BG_IMPACT_CONTESTS',pack['backgroundSymbols'])
 def test_helper_missing_targets_cycles_and_nested_calls_are_not_admitted(self):
  raw=imp.read_sources(REFERENCE)
  for replacement in [b'goto MissingImpactLabel',b'goto SetImpactOpponentBG',b'call SetImpactBackground']:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'goto SetImpactBackgroundRet',replacement)
   with patch.object(imp,'read_sources',return_value=changed):data,blocked=imp.build(REFERENCE)
   self.assertFalse({5,25}&{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn(299,{r['moveId'] for r in json.loads(data)['scripts']})
   self.assertTrue(all('control-flow:subroutine' in next(r for r in blocked if r['moveId']==move)['missing'] for move in [5,25]))
 def test_contest_dead_branch_folding_requires_source_false_invariant(self):
  read=imp.elemental.read
  def changed(reference,path):
   data=read(reference,path)
   return data.replace(b'bool8 IsContest(void)\n{\n    return FALSE;',b'bool8 IsContest(void)\n{\n    return TRUE;') if path=='src/battle_anim.c' else data
  with patch.object(imp.elemental,'read',side_effect=changed):data,blocked=imp.build(REFERENCE)
  self.assertFalse({5,25}&{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn(299,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_impact_backgrounds_share_native_tiles_palette_and_keep_distinct_maps(self):
  atlas=imp.module('physical_impact_backgrounds',ROOT/'scripts/imports/battle/import-source-animation-backgrounds.py');files=atlas.build(REFERENCE)
  manifest=json.loads(files['manifest.json']);blob=files['atlas.bin'];moves={r['id']:r for r in manifest['moves']}
  self.assertNotIn(6,moves);self.assertEqual(moves[4]['tiles'],moves[5]['tiles']);self.assertEqual(moves[4]['palette'],moves[5]['palette']);self.assertNotEqual(moves[4]['tilemap'],moves[5]['tilemap'])
  def read(part):start,count=part;return blob[start:start+count]
  self.assertEqual(atlas.indexed_tiles((REFERENCE/'graphics/battle_anims/backgrounds/impact.png').read_bytes())[0],read(moves[4]['tiles']))
  for id,name in [(4,'opponent'),(5,'player')]:self.assertEqual((REFERENCE/f'graphics/battle_anims/backgrounds/impact_{name}.bin').read_bytes(),read(moves[id]['tilemap']))
 def test_dirt_roots_reuse_one_random_callback_and_existing_mixed_sheet(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  for move,name,sub,size,tile,loop in [(28,'gSandAttackDirtSpriteTemplate','SandAttackDirt',8,0,0),(189,'gMudSlapMudSpriteTemplate','MudSlapMud',16,1,-1)]:
   root=next(r for r in pack['scripts'] if r['moveId']==move);recipe=next(r for r in pack['spriteRecipes'] if r['template']==name)
   self.assertEqual(('mud-sand','AnimDirtScatter',size,size,False),(recipe['asset'],recipe['callback'],recipe['width'],recipe['height'],recipe['affineDoubleSize']))
   self.assertEqual([[dict(tile=tile,duration=1,hFlip=False,vFlip=False)]],recipe['animations']);self.assertEqual([loop],recipe['loopStarts']);self.assertEqual([],recipe['affineAnimations'])
   self.assertEqual(6,sum(c['op']=='call' and c['args']==[sub] for c in root['commands']))
   particles=[c for c in pack['subroutines'][sub] if c['op']=='createsprite'];self.assertEqual(5,len(particles));self.assertTrue(all(c['args'][0]==name and c['args'][3:6]==['15','15','20'] for c in particles))
   self.assertEqual(5,len({tuple(c['args'][6:8]) for c in particles}),'Source authors varied the ignored target offsets; retain them as shared argument writes.')
   self.assertEqual(['ANIM_TAG_MUD_SAND'],[c['args'][0] for c in root['commands'] if c['op']=='loadspritegfx'])
 def test_dirt_templates_require_source_affine_off_oam_and_shared_tags(self):
  import re
  raw=imp.read_sources(REFERENCE)
  for template,size in [('gSandAttackDirtSpriteTemplate',8),('gMudSlapMudSpriteTemplate',16)]:
   source=raw['src/battle_anim_ground.c'];block=re.search(rb'const struct SpriteTemplate '+template.encode()+rb' =\s*\{.*?\};',source,re.S)[0]
   for old,new in [(f'AffineOff_ObjNormal_{size}x{size}','AffineNormal_ObjNormal_'+str(size)+'x'+str(size)),('ObjNormal_','ObjBlend_'),('ANIM_TAG_MUD_SAND','ANIM_TAG_IMPACT'),('AnimDirtScatter','UnknownDirtCallback')]:
    changed=dict(raw);changed['src/battle_anim_ground.c']=source.replace(block,block.replace(old.encode(),new.encode()))
    with patch.object(imp,'read_sources',return_value=changed):
     with self.assertRaisesRegex(ValueError,'Dirt-scatter source template'):imp.build(REFERENCE)
 def test_uncovered_dirt_callback_keeps_both_roots_out(self):
  raw=imp.read_sources(REFERENCE);changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createsprite gSandAttackDirtSpriteTemplate',b'createsprite UnknownDirt',1).replace(b'createsprite gMudSlapMudSpriteTemplate',b'createsprite UnknownDirt',1)
  with patch.object(imp,'read_sources',return_value=changed):data,blocked=imp.build(REFERENCE)
  self.assertFalse({28,189}&{r['moveId'] for r in json.loads(data)['scripts']})
  self.assertTrue(all('callback:UnknownDirt' in next(r for r in blocked if r['moveId']==move)['missing'] for move in [28,189]))
 def test_fang_shared_sheet_and_distinct_source_end_lifetimes(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  for name,durations,affine in [('gFangSpriteTemplate',[8,16,4,4],True),('gSuperFangSpriteTemplate',[2,2,2,2],False)]:
   r=recipes[name];self.assertEqual(('fang-attack',32,32,False,affine),(r['asset'],r['width'],r['height'],r['blend'],r['affineDoubleSize']))
   self.assertEqual([0,16,32,48],[f['tile'] for f in r['animations'][0]]);self.assertEqual(durations,[f['duration'] for f in r['animations'][0]]);self.assertEqual([-1],r['loopStarts']);self.assertEqual(int(affine),len(r['affineAnimations']))
  self.assertEqual([(512,512,0),(-32,-32,8)],[(c['x'],c['y'],c['duration']) for c in recipes['gFangSpriteTemplate']['affineAnimations'][0] if c['op']=='frame'])
  blob=imp.build_assets(REFERENCE)['fang-attack'];self.assertEqual((32,128),struct.unpack_from('<HH',blob));self.assertEqual(8196,len(blob))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/fang_attack.png') as image:indices=list(image.tobytes());palette=image.getpalette()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<4096H',blob,4)))
 def test_missing_fang_template_or_sheet_cannot_admit_root(self):
  raw=imp.read_sources(REFERENCE)
  for old,new in [(b'createsprite gFangSpriteTemplate',b'createsprite UnknownFang'),(b'createsprite gSuperFangSpriteTemplate',b'createsprite UnknownFang'),(b'loadspritegfx ANIM_TAG_FANG_ATTACK',b'loadspritegfx ANIM_TAG_UNKNOWN_FANG')]:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):data,_=imp.build(REFERENCE)
   ids={r['moveId'] for r in json.loads(data)['scripts']};self.assertFalse(({305} if b'gFangSprite' in old else {162} if b'gSuperFang' in old else {162,305})&ids)
 def test_fang_source_shape_palette_and_callback_are_verified(self):
  raw=imp.read_sources(REFERENCE)
  for path,old,new in [('src/battle_anim_effects_3.c',b'.callback = AnimFang,',b'.callback = UnknownFang,'),('src/battle_anim_effects_1.c',b'.callback = AnimSuperFang,',b'.callback = UnknownFang,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_FangAttack, 0x0800, ANIM_TAG_FANG_ATTACK}',b'{gBattleAnimSpriteGfx_FangAttack, 0x0400, ANIM_TAG_FANG_ATTACK}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_FangAttack, ANIM_TAG_FANG_ATTACK}',b'{gBattleAnimSpritePal_FangAttack, ANIM_TAG_IMPACT}')]:
   self.assertIn(old,raw[path]);changed=dict(raw);changed[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):
    with self.assertRaisesRegex(ValueError,'Fang source'):imp.build(REFERENCE)
 def test_shared_contest_comparison_fold_preserves_task_and_remaps_labels(self):
  task=dict(op='createvisualtask',args=['AnimTask_IsContest','2'],line=1);compare=dict(op='jumpargeq',args=['7','1','Dead'],line=2);end=dict(op='return',args=[],line=3)
  rows,labels=imp.elemental.fold_firered_contest_branches(REFERENCE,'Dead:\n', [task,compare,end],{'Later':2})
  self.assertEqual([task,end],rows);self.assertEqual({'Later':1},labels)
  rows,labels=imp.elemental.fold_firered_contest_branches(REFERENCE,'Dead:\n',[task,dict(compare,args=['7','0','Dead']),end],{'Later':2})
  self.assertEqual(dict(compare,op='goto',args=['Dead']),rows[1]);self.assertEqual({'Later':2},labels)
  unrelated=dict(task,args=['AnimTask_IsTargetPlayerSide','2']);rows,_=imp.elemental.fold_firered_contest_branches(REFERENCE,'Dead:\n',[unrelated,compare,end],{})
  self.assertEqual(compare,rows[1])
 def test_contest_comparison_alternate_entry_cannot_bypass_predicate(self):
  rows=[dict(op='createvisualtask',args=['AnimTask_IsContest','2']),dict(op='jumpargeq',args=['7','1','Dead']),dict(op='return',args=[])]
  with self.assertRaisesRegex(ValueError,'alternate entry'):imp.elemental.fold_firered_contest_branches(REFERENCE,'Dead:\n',rows,{'BypassPredicate':1})
 def test_contest_task_arg_write_and_destroy_are_part_of_fold_proof(self):
  read=imp.elemental.read
  for old,new in [(b'gBattleAnimArgs[ARG_RET_ID] = FALSE;',b'gBattleAnimArgs[ARG_RET_ID] = TRUE;'),(b'DestroyAnimVisualTask(taskId);',b'DestroyTask(taskId);')]:
   def changed(reference,path):
    data=read(reference,path)
    return data.replace(old,new) if path=='src/battle_anim_utility_funcs.c' else data
   rows=[dict(op='createvisualtask',args=['AnimTask_IsContest','2']),dict(op='jumpargeq',args=['7','1','Dead'])]
   with patch.object(imp.elemental,'read',side_effect=changed):
    with self.assertRaisesRegex(ValueError,'FireRed contest predicate'):imp.elemental.fold_firered_contest_branches(REFERENCE,'Dead:\n',rows,{})
 def test_multi_strike_recipes_preserve_unmirrored_swipe_and_hand_frame_parity(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']};swipe=recipes['gFurySwipesSpriteTemplate'];hand=recipes['gArmThrustHandSpriteTemplate']
  self.assertEqual(('swipe','AnimFurySwipes',32,32,False),(swipe['asset'],swipe['callback'],swipe['width'],swipe['height'],swipe['affineDoubleSize']));self.assertEqual([],swipe['affineAnimations'])
  self.assertEqual([[0,16,32,48]]*2,[[f['tile'] for f in a] for a in swipe['animations']]);self.assertEqual([[False]*4,[True]*4],[[f['hFlip'] for f in a] for a in swipe['animations']]);self.assertTrue(all(f['duration']==4 for a in swipe['animations'] for f in a))
  self.assertEqual(('hands-and-feet','AnimArmThrustHit',False),(hand['asset'],hand['callback'],hand['affineDoubleSize']));self.assertEqual([0,16,32,48,48],[a[0]['tile'] for a in hand['animations']]);self.assertEqual([False,False,False,False,True],[a[0]['hFlip'] for a in hand['animations']]);self.assertEqual([],hand['affineAnimations'])
 def test_arm_branch_and_two_authored_swipes_remain_per_invocation(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);rows={r['moveId']:r for r in pack['scripts']};arm=rows[292];swipe=rows[154]
  self.assertEqual(2,sum(c['op']=='createsprite' and c['args'][0]=='gFurySwipesSpriteTemplate' for c in swipe['commands']));self.assertEqual([['16','0','1'],['-16','0','0']],[c['args'][3:] for c in swipe['commands'] if c['op']=='createsprite' and c['args'][0]=='gFurySwipesSpriteTemplate'])
  self.assertEqual([['ArmThrustRight','ArmThrustLeft']],[c['args'] for c in arm['commands'] if c['op']=='choosetwoturnanim']);self.assertEqual([['10','-8','14','3']],[c['args'][3:] for c in arm['commands'] if c['op']=='createsprite' and c['args'][0]=='gArmThrustHandSpriteTemplate']);self.assertEqual({'SE_M_DOUBLE_SLAP','SE_M_SWAGGER'},set(arm['soundEffects']))
 def test_swipe_registered_sheet_keeps_even_unsampled_source_tiles(self):
  blob=imp.build_assets(REFERENCE)['swipe'];self.assertEqual((32,160),struct.unpack_from('<HH',blob));self.assertEqual(10244,len(blob))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/swipe.png') as image:indices=list(image.tobytes());palette=image.getpalette()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices];self.assertEqual(expected,list(struct.unpack_from('<5120H',blob,4)))
 def test_missing_strike_callback_or_sheet_blocks_only_complete_root(self):
  raw=imp.read_sources(REFERENCE)
  for old,new,missing in [(b'createsprite gFurySwipesSpriteTemplate',b'createsprite MissingStrike',{154}),(b'createsprite gArmThrustHandSpriteTemplate',b'createsprite MissingStrike',{292}),(b'loadspritegfx ANIM_TAG_SWIPE',b'loadspritegfx ANIM_TAG_UNKNOWN_SWIPE',{154})]:
   changed=dict(raw);changed['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):data,_=imp.build(REFERENCE)
   self.assertFalse(missing&{r['moveId'] for r in json.loads(data)['scripts']})
 def test_strike_source_oam_callback_and_registration_fail_closed(self):
  raw=imp.read_sources(REFERENCE)
  for path,old,new in [('src/battle_anim_fight.c',b'.callback = AnimArmThrustHit,',b'.callback = UnknownStrike,'),('src/battle_anim_effects_2.c',b'.callback = AnimFurySwipes,',b'.callback = UnknownStrike,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Swipe, 0x0a00, ANIM_TAG_SWIPE}',b'{gBattleAnimSpriteGfx_Swipe, 0x0800, ANIM_TAG_SWIPE}')]:
   self.assertIn(old,raw[path]);changed=dict(raw);changed[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):
    with self.assertRaisesRegex(ValueError,'strike source|Swipe source'):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
