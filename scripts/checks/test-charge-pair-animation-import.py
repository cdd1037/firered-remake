#!/usr/bin/env python3
"""Complete Razor Wind/Skull Bash roots; inherited local gust, air-wave and impact art."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
s=importlib.util.spec_from_file_location('charge_pair_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(s);s.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ChargePairAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_complete_roots_and_helper_retain_every_authored_command(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move,labels in [(13,['RazorWindSetUp','RazorWindUnleash']),(130,['SkullBashSetUp','SkullBashAttack'])]:
   root=self.rows[move];self.assertEqual(labels,root['commands'][0]['args']);self.assertTrue(set(labels)<=root['labels'].keys());self.assertTrue(root['soundEffects'])
   for c in root['commands']+(self.pack['subroutines']['SkullBashSetUpHeadDown'] if move==130 else []):
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(2,sum(c['op']=='call' and c['args']==['SkullBashSetUpHeadDown'] for c in self.rows[130]['commands']));self.assertEqual([['SE_BANG']],[c['args'] for c in self.rows[130]['commands'] if c['op']=='playse'])
 def test_tornado_uses_source_affine_and_existing_material(self):
  r=next(r for r in self.pack['spriteRecipes'] if r['template']=='gRazorWindTornadoSpriteTemplate');self.assertEqual(('gust','AnimRazorWindTornado',32,64,False,False),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']))
  self.assertEqual([('frame',16,256,0),('frame',4,0,40),('end',0,0,0)],[(c['op'],c['x'],c['y'],c['duration']) for c in r['affineAnimations'][0]])
  self.assertEqual({'ANIM_TAG_GUST','ANIM_TAG_AIR_WAVE_2','ANIM_TAG_IMPACT'},set(self.rows[13]['spriteTags']));self.assertEqual({'ANIM_TAG_IMPACT'},set(self.rows[130]['spriteTags']))
  self.assertFalse(any(r['id'] in {'gust','air-wave-2'} for r in self.pack['assets']+self.pack['indexedAssets']))
 def test_unreviewed_callback_and_wrong_abi_block_whole_root(self):
  for move,name,container in [(13,'gRazorWindTornadoSpriteTemplate','SPRITES'),(130,'gSlideMonToOffsetAndBackSpriteTemplate','SPRITES'),(130,'AnimTask_SkullBashPosition','TASKS')]:
   with patch.object(imp,container,getattr(imp,container)-{name}):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
  for move,old,new,name in [(13,b'32, 0, 16, 16, 0, 7, 40',b'32, 0, 16, 16, 0, 7','gRazorWindTornadoSpriteTemplate'),(130,b'AnimTask_SkullBashPosition, 2, 0',b'AnimTask_SkullBashPosition, 2','AnimTask_SkullBashPosition'),(130,b'ANIM_ATTACKER, -24, 0, 0, 10, 0',b'ANIM_ATTACKER, -24, 0, 0, 10','gSlideMonToOffsetAndBackSpriteTemplate')]:
   raw=dict(self.raw);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new,1)
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback-arguments:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
 def test_charge_effect_contract_and_dummy_template_cannot_be_inferred(self):
  for symbol,move in [('RAZOR_WIND',13),('SKULL_BASH',130)]:
   raw=dict(self.raw);text=raw['src/data/battle_moves.h'].decode();block=re.search(r'\[MOVE_'+symbol+r'\].*?\n    \},',text,re.S)[0];raw['src/data/battle_moves.h']=text.replace(block,block.replace('EFFECT_'+symbol,'EFFECT_HIT')).encode()
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('invocation:move-turn',next(r['missing'] for r in blocked if r['moveId']==move))
  raw=dict(self.raw);raw['src/battle_anim_mon_movement.c']=raw['src/battle_anim_mon_movement.c'].replace(b'.callback = SlideMonToOffsetAndBack,',b'.callback = UnknownSlide,')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaisesRegex(ValueError,'Slide-and-back source template'):imp.build(REFERENCE)
 def test_regeneration_is_exact(self):self.assertEqual(self.data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
