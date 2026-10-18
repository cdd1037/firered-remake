#!/usr/bin/env python3
"""Volt Tackle complete source closure, raw dependencies and shared-pack parity."""
import sys
sys.dont_write_bytecode=True
import argparse,json,types,unittest
from pathlib import Path
from unittest.mock import patch
p=argparse.ArgumentParser(add_help=False)
for name in ['root','reference','generated','previous','importer']:p.add_argument('--'+name,type=Path)
a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=(a.root or Path(__file__).resolve().parents[2]).resolve();REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations';PREVIOUS=a.previous or GENERATED
path=ROOT/'scripts/imports/battle/import-source-elemental-animations.py';imp=types.ModuleType('volt_tackle_import');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
class VoltTackleImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_only_volt_tackle_enters_from_its_reviewed_callbacks(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_VoltTackleBolt','AnimTask_VoltTackleAttackerReappear'}):data,_=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({344},set(self.roots)-set(old))
  for k,r in old.items():self.assertEqual(r,self.roots[k])
  root=self.roots[344];self.assertEqual(['gVoltTackleBoltSpriteTemplate'],root['spriteDependencies']);c=root['commands'];self.assertEqual([['AnimTask_VoltTackleBolt','5',str(i)] for i in range(5)],[r['args'] for r in c if r['op']=='createvisualtask' and r['args'][0]=='AnimTask_VoltTackleBolt']);self.assertEqual([['gVoltTackleOrbSlideSpriteTemplate','ANIM_ATTACKER','1']],[r['args'] for r in c if r['op']=='createsprite' and r['args'][0]=='gVoltTackleOrbSlideSpriteTemplate']);self.assertEqual(9,sum(r['op']=='waitforvisualfinish' for r in c))
 def test_source_geometry_rotation_and_unused_animation_variants_are_preserved(self):
  orb=self.recipes['gVoltTackleOrbSlideSpriteTemplate'];bolt=self.recipes['gVoltTackleBoltSpriteTemplate'];self.assertEqual(('circle-of-light',64,64,True,False),(orb['asset'],orb['width'],orb['height'],orb['blend'],orb['affineDoubleSize']));self.assertEqual(self.recipes['gGrowingChargeOrbSpriteTemplate']['affineAnimations'],orb['affineAnimations']);self.assertEqual(('spark-thin',8,16,False,True),(bolt['asset'],bolt['width'],bolt['height'],bolt['blend'],bolt['affineDoubleSize']));self.assertEqual([-1]*4,bolt['loopStarts']);self.assertEqual([[dict(tile=i*2,duration=3,hFlip=False,vFlip=False)] for i in range(4)],bolt['animations']);self.assertEqual([[dict(op='frame',x=256,y=256,rotation=64,duration=0,target=0),dict(op='end',x=0,y=0,rotation=0,duration=0,target=0)]],bolt['affineAnimations'])
 def test_malformed_source_callbacks_dependencies_and_argument_widths_fail_closed(self):
  original=imp.read
  for old,new in [(b'.callback = AnimVoltTackleBolt,',b'.callback = Other,'),(b'CreateSprite(&gVoltTackleBoltSpriteTemplate,',b'CreateSprite(&gWrongTemplate,')]:
   self.assertIn(old,original(REFERENCE,'src/battle_anim_electric.c'))
   def read(ref,path):
    value=original(ref,path);return value.replace(old,new) if path=='src/battle_anim_electric.c' else value
   with patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)
  for old,new in [(b'createvisualtask AnimTask_VoltTackleBolt, 5, 0',b'createvisualtask AnimTask_VoltTackleBolt, 5'),(b'createsprite gVoltTackleOrbSlideSpriteTemplate, ANIM_ATTACKER, 1',b'createsprite gVoltTackleOrbSlideSpriteTemplate, ANIM_ATTACKER, 1, 0')]:
   self.assertIn(old,original(REFERENCE,'data/battle_anim_scripts.s'))
   def read(ref,path):
    value=original(ref,path);return value.replace(old,new) if path=='data/battle_anim_scripts.s' else value
   with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
   self.assertNotIn(344,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_all_previous_roots_recipes_materials_and_generated_bytes_are_unchanged(self):
  old=json.loads((PREVIOUS/'elemental.json').read_bytes())
  for row in old['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for key in ['assets','indexedAssets','subroutines','backgroundSymbols','taskAffinePrograms']:self.assertEqual(old[key],self.pack[key])
  self.assertEqual(self.data,(GENERATED/'elemental.json').read_bytes())
  for name,data in self.pixels.items():
   filename='elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555';self.assertEqual(data,(GENERATED/filename).read_bytes());self.assertEqual(data,(PREVIOUS/filename).read_bytes())
if __name__=='__main__':unittest.main()
