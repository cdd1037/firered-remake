#!/usr/bin/env python3
"""Present's bounded source graph, signed branch predicate and shared materials."""
import sys
sys.dont_write_bytecode=True
import argparse,json,types,unittest
from pathlib import Path
from unittest.mock import patch
parser=argparse.ArgumentParser(add_help=False)
for name in ['root','reference','generated','previous','importer']:parser.add_argument('--'+name,type=Path)
a,rest=parser.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=(a.root or Path(__file__).resolve().parents[2]).resolve();REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations';PREVIOUS=a.previous or GENERATED
path=ROOT/'scripts/imports/battle/import-source-physical-animations.py';imp=types.ModuleType('present_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
class PresentImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,_=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_only_present_enters_the_shared_predicate_closure(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_IsHealingMove'}):data,_=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({217},set(self.roots)-set(old))
  for key,value in old.items():self.assertEqual(value,self.roots[key])
  row=self.roots[217];self.assertEqual('Move_PRESENT',row['label']);commands=row['commands']
  self.assertIn(['AnimTask_IsHealingMove','2'],[c['args'] for c in commands if c['op']=='createvisualtask'])
  self.assertEqual([['gPresentSpriteTemplate','ANIM_TARGET','2','0','-5','10','2','-1']],[c['args'] for c in commands if c['op']=='createsprite' and c['args'][0]=='gPresentSpriteTemplate'])
  closure=commands+[c for block in self.pack['subroutines'].values() for c in block]
  self.assertEqual(9,sum(c['op']=='createsprite' and c['args'][0]=='gPresentHealParticleSpriteTemplate' for c in closure))
  self.assertTrue(any(c['op']=='call' and c['args']==['HealingEffect2'] for c in closure))
 def test_exact_finite_frames_and_affine_program(self):
  bag=self.recipes['gPresentSpriteTemplate'];heal=self.recipes['gPresentHealParticleSpriteTemplate']
  for recipe,asset,callback,size in [(bag,'item-bag','AnimPresent',32),(heal,'green-sparkle','AnimPresentHealParticle',16)]:
   self.assertEqual((asset,callback,size,size),(recipe['asset'],recipe['callback'],recipe['width'],recipe['height']));self.assertFalse(recipe['blend']);self.assertFalse(recipe.get('affineDoubleSize',False));self.assertEqual([-1],recipe['loopStarts']);self.assertFalse(any(recipe.get('frameLoops',[])))
  self.assertEqual([[dict(tile=0,duration=30,hFlip=False,vFlip=False)]],bag['animations'])
  self.assertEqual([[dict(tile=t,duration=4,hFlip=False,vFlip=False) for t in [0,4,8,12]]],heal['animations']);self.assertEqual([],heal.get('affineAnimations',[]))
  self.assertEqual(2,len(bag['affineAnimations']));self.assertEqual([3,6],[len([c for c in p if c['op']=='frame']) for p in bag['affineAnimations']])
 def test_source_sign_predicate_and_sprite_callback_corruption_fail_closed(self):
  original=imp.read_sources
  for path,old,new in [('src/battle_anim_effects_3.c',b'if (gAnimMoveDmg > 0)',b'if (gAnimMoveDmg >= 0)'),('src/battle_anim_effects_1.c',b'.callback = AnimPresent,',b'.callback = Other,')]:
   self.assertIn(old,original(REFERENCE)[path])
   def changed(ref):
    raw=original(ref);raw[path]=raw[path].replace(old,new);return raw
   with self.subTest(path=path),patch.object(imp,'read_sources',side_effect=changed),self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_existing_material_owners_and_prior_rows_are_unchanged(self):
  previous=json.loads((PREVIOUS/'physical.json').read_bytes())
  for row in previous['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in previous['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for group in ['assets','indexedAssets']:self.assertEqual(previous[group],self.pack[group])
  self.assertEqual(self.data,(GENERATED/'physical.json').read_bytes())
if __name__=='__main__':unittest.main()
