#!/usr/bin/env python3
"""Complete Flail source root, existing impact material and callback ABI closure."""
import importlib.util,json,re,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('flail_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class FlailAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_complete_root_preserves_every_source_command(self):
  row=self.rows[175];self.assertEqual(13,len(row['commands']));self.assertTrue(row['soundEffects']);source=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for c in row['commands']:
   text=source[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  shake=next(c for c in row['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ShakeTargetBasedOnMovePowerOrDmg');self.assertEqual('FALSE',shake['args'][2]);self.assertEqual({'ANIM_TAG_IMPACT'},set(row['spriteTags']))
 def test_random_impact_reuses_complete_shared_impact_affine_tables(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']};r=recipes['gRandomPosHitSplatSpriteTemplate'];basic=recipes['gBasicHitSplatSpriteTemplate'];self.assertEqual('impact',r['asset']);self.assertEqual(4,len(r['affineAnimations']));self.assertEqual(basic['affineAnimations'],r['affineAnimations']);self.assertEqual(basic['animations'],r['animations']);self.assertEqual(imp.elemental.recipe(self.raw['src/battle_anim_normal.c'].decode(),'gRandomPosHitSplatSpriteTemplate','AnimHitSplatRandom'),r)
  self.assertNotIn('impact',imp.build_assets(REFERENCE));self.assertNotIn('impact',[r['id'] for r in self.pack['indexedAssets']])
 def test_missing_callbacks_block_complete_root(self):
  for name,target in [('AnimTask_FlailMovement','TASKS'),('gRandomPosHitSplatSpriteTemplate','SPRITES')]:
   with patch.object(imp,target,getattr(imp,target)-{name}):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(175,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==175))
 def test_retained_argument_registers_cannot_supply_omitted_operands(self):
  for old,new,callback in [(b'createvisualtask AnimTask_FlailMovement, 2, ANIM_ATTACKER',b'createvisualtask AnimTask_FlailMovement, 2','AnimTask_FlailMovement'),(b'createsprite gRandomPosHitSplatSpriteTemplate, ANIM_TARGET, 3, ANIM_TARGET, 3',b'createsprite gRandomPosHitSplatSpriteTemplate, ANIM_TARGET, 3, ANIM_TARGET','gRandomPosHitSplatSpriteTemplate')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(175,[r['moveId'] for r in json.loads(data)['scripts']]);self.assertIn('callback-arguments:'+callback,next(r['missing'] for r in blocked if r['moveId']==175))
 def test_changed_source_callback_fails_closed(self):
  raw=dict(self.raw);raw['src/battle_anim_normal.c']=raw['src/battle_anim_normal.c'].replace(b'.callback = AnimHitSplatRandom,',b'.callback = AnimHitSplatBasic,')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
