#!/usr/bin/env python3
"""Source closure for Fly/Bounce/Dive, with shared water-helper ownership."""
import importlib.util,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
s=importlib.util.spec_from_file_location('round_shadow_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(s);s.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class RoundShadowChargeAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_complete_three_roots_and_every_reachable_source_command(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in (19,291,340):
   root=self.rows[move];self.assertEqual(1,sum(c['op']=='choosetwoturnanim' for c in root['commands']));commands=list(root['commands']);seen=set();pending=[c['args'][0] for c in commands if c['op']=='call']
   while pending:
    label=pending.pop()
    if label in seen:continue
    seen.add(label);helper=self.pack['subroutines'][label];commands.extend(helper);pending.extend(c['args'][0] for c in helper if c['op']=='call')
   for c in commands:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertIn('RisingWaterHitEffect',self.pack['subroutines']);self.assertEqual([['ANIM_ATTACKER']],[c['args'] for c in self.rows[291]['commands'] if c['op']=='visible'])
 def test_exact_affine_recipes_and_shared_dependency_ownership(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for name,file,callback in imp.ROUND_SHADOW_RECIPES:
   r=recipes[name];self.assertEqual((callback,64,64,False),(r['callback'],r['width'],r['height'],r['blend']));self.assertEqual(callback!='AnimFlyBallAttack',r['affineDoubleSize']);self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([0],r['loopStarts'])
  self.assertEqual([50,216],[p[0]['rotation'] for p in recipes['gFlyBallAttackSpriteTemplate']['affineAnimations']]);self.assertEqual([],recipes['gDiveWaterSplashSpriteTemplate']['affineAnimations'])
  self.assertFalse({'gWaterHitSplatSpriteTemplate','gSmallDriftingBubblesSpriteTemplate'}&recipes.keys());self.assertFalse({'water-impact','small-bubbles'}&{r['id'] for r in self.pack['assets']+self.pack['indexedAssets']})
  self.assertEqual({'ANIM_TAG_ROUND_SHADOW','ANIM_TAG_IMPACT'},set(self.rows[19]['spriteTags']));self.assertEqual({'ANIM_TAG_ROUND_SHADOW','ANIM_TAG_IMPACT'},set(self.rows[340]['spriteTags']));self.assertEqual({'ANIM_TAG_ROUND_SHADOW','ANIM_TAG_SPLASH','ANIM_TAG_SWEAT_BEAD','ANIM_TAG_WATER_IMPACT','ANIM_TAG_SMALL_BUBBLES'},set(self.rows[291]['spriteTags']))
 def test_materials_are_exact_source_size_and_indexed_palette(self):
  assets=imp.build_assets(REFERENCE);indexed,pixels=imp.build_indexed_assets(REFERENCE)
  for name in ['round-shadow','splash']:
   self.assertEqual((64,64),struct.unpack_from('<HH',assets[name]));self.assertEqual(8196,len(assets[name]));self.assertEqual(2048,len(pixels[name+'.4bpp']));self.assertEqual(32,len(pixels[name+'.pal16']));self.assertEqual(1,sum(r['id']==name for r in indexed))
 def test_missing_callback_or_helper_blocks_entire_root(self):
  for move,name in [(19,'gFlyBallUpSpriteTemplate'),(19,'gFlyBallAttackSpriteTemplate'),(340,'gBounceBallShrinkSpriteTemplate'),(340,'gBounceBallLandSpriteTemplate'),(291,'gDiveBallSpriteTemplate'),(291,'gDiveWaterSplashSpriteTemplate'),(291,'gWaterHitSplatSpriteTemplate'),(291,'gSmallDriftingBubblesSpriteTemplate')]:
   with patch.object(imp,'SPRITES',imp.SPRITES-{name}):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
 def test_bad_source_abi_effect_and_graphics_fail_closed(self):
  for move,name in [(19,'gFlyBallUpSpriteTemplate'),(340,'gBounceBallLandSpriteTemplate'),(291,'gDiveWaterSplashSpriteTemplate')]:
   raw=dict(self.raw);text=raw['data/battle_anim_scripts.s'].decode();pattern=r'(createsprite '+name+r',[^\n]+)';line=re.search(pattern,text)[0];raw['data/battle_anim_scripts.s']=text.replace(line,line+', 99',1).encode()
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback-arguments:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
  for symbol,move in [('FLY',19),('DIVE',291),('BOUNCE',340)]:
   raw=dict(self.raw);text=raw['src/data/battle_moves.h'].decode();block=re.search(r'\[MOVE_'+symbol+r'\].*?\n    \},',text,re.S)[0];raw['src/data/battle_moves.h']=text.replace(block,block.replace('EFFECT_SEMI_INVULNERABLE','EFFECT_HIT')).encode()
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('invocation:move-turn',next(r['missing'] for r in blocked if r['moveId']==move))
  for name in ['RoundShadow','Splash']:
   raw=dict(self.raw);old=('{gBattleAnimSpriteGfx_'+name+', 0x0800,').encode();self.assertIn(old,raw['src/data/battle_anim.h']);raw['src/data/battle_anim.h']=raw['src/data/battle_anim.h'].replace(old,old.replace(b'0x0800',b'0x0400'))
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_regeneration_is_exact(self):self.assertEqual(self.data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
