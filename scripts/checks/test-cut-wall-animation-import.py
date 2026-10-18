#!/usr/bin/env python3
"""Pinned shared-particle, wall branch and committed-counter root closure."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('cut_wall_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class CutWallAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_complete_roots_preserve_source_commands_and_macro_branch_targets(self):
  source=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move,count in [(210,29),(239,44),(280,64)]:
   row=self.rows[move];self.assertEqual(count,len(row['commands']));self.assertTrue(row['soundEffects']);self.assertTrue(any(c['op']=='end' for c in row['commands']))
   for c in row['commands']:
    text=source[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op=='jumpreteq':op,args='jumpargeq',['7',*args]
    if op=='jumpretfalse':op,args='jumpargeq',['7','0',*args]
    self.assertEqual((op,args),(c['op'],c['args']))
   for c in row['commands']:
    targets=c['args'] if c['op']=='choosetwoturnanim' else c['args'][-1:] if c['op'] in {'jumpargeq','goto'} else []
    for target in targets:self.assertIn(target,row['labels'])
  self.assertEqual(['BrickBreakNormal','BrickBreakShatteredWall'],next(c['args'] for c in self.rows[280]['commands'] if c['op']=='choosetwoturnanim'))
 def test_twister_rock_pointer_tail_and_leaf_animation_are_exact(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']};rock=recipes['gTwisterRockSpriteTemplate'];self.assertEqual([64,80],[a[0]['tile'] for a in rock['animations']]);self.assertEqual([-1,-1],rock['loopStarts']);self.assertEqual([],rock['affineAnimations'])
  leaf=recipes['gTwisterLeafSpriteTemplate'];old=imp.elemental.recipe(self.raw['src/battle_anim_effects_1.c'].decode(),'gRazorLeafParticleSpriteTemplate','AnimRazorLeafParticle');self.assertEqual(old['animations'],leaf['animations']);self.assertEqual(old['loopStarts'],leaf['loopStarts']);self.assertEqual('AnimMoveTwisterParticle',leaf['callback'])
 def test_existing_canonical_materials_are_reused_without_duplicate_conversion(self):
  self.assertNotIn('blue-light-wall',imp.build_assets(REFERENCE));self.assertNotIn('leaf',imp.build_assets(REFERENCE));self.assertNotIn('cut',imp.build_assets(REFERENCE))
  recipes={r['template']:r for r in self.pack['spriteRecipes']};self.assertEqual(('blue-light-wall',64,True),(recipes['gBrickBreakWallSpriteTemplate']['asset'],recipes['gBrickBreakWallSpriteTemplate']['width'],recipes['gBrickBreakWallSpriteTemplate']['blend']));self.assertEqual('torn-metal',recipes['gBrickBreakWallShardSpriteTemplate']['asset'])
  registration=self.raw['src/data/battle_anim.h'].decode();self.assertRegex(registration,r'\{gBattleAnimSpritePal_BlueLightWall,\s*ANIM_TAG_TORN_METAL\}');self.assertRegex(registration,r'\{gBattleAnimSpritePal_BlueLightWall,\s*ANIM_TAG_BLUE_LIGHT_WALL\}')
 def test_missing_each_shared_dependency_blocks_its_whole_root(self):
  for move,name,target in [(210,'AnimTask_IsFuryCutterHitRight','TASKS'),(210,'AnimTask_GetFuryCutterHitCount','TASKS'),(239,'gTwisterLeafSpriteTemplate','SPRITES'),(239,'gTwisterRockSpriteTemplate','SPRITES'),(280,'gBrickBreakWallSpriteTemplate','SPRITES'),(280,'gBrickBreakWallShardSpriteTemplate','SPRITES')]:
   with patch.object(imp,target,getattr(imp,target)-{name}):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
 def test_missing_callback_operands_are_not_filled_from_retained_registers(self):
  for move,old,new,name in [(239,b'ANIM_TARGET, 2, 120, 70, 5, 70, 30',b'ANIM_TARGET, 2, 120, 70, 5, 70','gTwisterLeafSpriteTemplate'),(280,b'ANIM_TARGET, 0, 0, 90, 10',b'ANIM_TARGET, 0, 0, 90','gBrickBreakWallSpriteTemplate'),(210,b'AnimTask_GetFuryCutterHitCount, 2',b'AnimTask_GetFuryCutterHitCount','AnimTask_GetFuryCutterHitCount')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('callback-arguments:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
 def test_changed_pointer_or_callback_cannot_clamp_or_alias_material(self):
  for key,old,new in [('src/battle_anim_rock.c',b'.anims = &sAnims_BasicRock[4],',b'.anims = &sAnims_BasicRock[6],'),('src/battle_anim_fight.c',b'.callback = AnimBrickBreakWall,',b'.callback = AnimBrickBreakWallShard,')]:
   raw=dict(self.raw);self.assertIn(old,raw[key]);raw[key]=raw[key].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
