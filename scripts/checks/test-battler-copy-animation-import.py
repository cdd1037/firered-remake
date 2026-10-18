"""Narrow source admission and material preservation for battler-copy callbacks."""
import argparse,importlib.util,json,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('battler_copy_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
ROOTS={101,102,104};TASKS={'AnimTask_NightShadeClone','AnimTask_DoubleTeam','AnimTask_ShrinkTargetCopy'}
class BattlerCopyImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!='gMimicOrbSpriteTemplate'}),patch.object(imp,'TASKS',imp.TASKS-TASKS):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_exact_three_roots_preserve_every_previous_row_recipe_and_material(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual(ROOTS,set(self.roots)-set(old))
  for k,row in old.items():self.assertEqual(row,self.roots[k])
  oldr={r['template']:r for r in self.old['spriteRecipes']};newr={r['template']:r for r in self.pack['spriteRecipes']};self.assertEqual({'gMimicOrbSpriteTemplate'},set(newr)-set(oldr))
  for k,row in oldr.items():self.assertEqual(row,newr[k])
  for k in set(self.pack)-{'scripts','spriteRecipes'}:self.assertEqual(self.old[k],self.pack[k])
  self.assertEqual(self.oldpixels,self.pixels)
 def test_native_commands_are_unrewritten_and_allocated_tag_has_no_material(self):
  self.assertEqual(['Move_NIGHT_SHADE','Move_MIMIC','Move_DOUBLE_TEAM'],[self.roots[k]['label'] for k in [101,102,104]])
  self.assertEqual(['ANIM_TAG_BENT_SPOON'],list(self.roots[104]['spriteTags']));self.assertFalse(any(c['op']=='loadspritegfx' for c in self.roots[104]['commands']));self.assertNotIn('materialDependencies',self.roots[104]);self.assertNotIn('spriteDependencies',self.roots[104]);self.assertEqual(['BG_GHOST'],next(c['args'] for c in self.roots[101]['commands'] if c['op']=='fadetobg'))
  mimic=self.roots[102]['commands'];self.assertIn({'op':'monbg_static','args':['ANIM_DEF_PARTNER'],'line':7727},mimic);self.assertEqual(['7','0xFFFF'],next(c['args'] for c in mimic if c['op']=='setarg'));self.assertEqual(['clearmonbg_static','blendoff','end'],[c['op'] for c in mimic[-3:]])
 def test_missing_callback_closes_only_dependent_roots(self):
  for task,move in [('AnimTask_NightShadeClone',101),('AnimTask_DoubleTeam',104),('AnimTask_ShrinkTargetCopy',102)]:
   with patch.object(imp,'TASKS',imp.TASKS-{task}):data,_=imp.build(a.reference)
   rows={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({move},set(self.roots)-set(rows));self.assertTrue(all(row==self.roots[k] for k,row in rows.items()))
 def test_mimic_uses_shared_orbs_and_distinct_zero_then_retained_affine(self):
  r=next(r for r in self.pack['spriteRecipes'] if r['template']=='gMimicOrbSpriteTemplate');self.assertEqual('orbs',r['asset']);self.assertEqual('AnimMimicOrb',r['callback']);self.assertTrue(r['affineDoubleSize']);self.assertEqual([{'tile':8,'duration':1,'hFlip':False,'vFlip':False}],r['animations'][0]);self.assertEqual([-1],r['loopStarts']);self.assertEqual((0,0,0),(r['affineAnimations'][0][0]['x'],r['affineAnimations'][0][0]['y'],r['affineAnimations'][0][0]['duration']));self.assertEqual((-16,-16,1),(r['affineAnimations'][1][0]['x'],r['affineAnimations'][1][0]['y'],r['affineAnimations'][1][0]['duration']))
 def test_generated_pack_equals_local_pinned_import(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
