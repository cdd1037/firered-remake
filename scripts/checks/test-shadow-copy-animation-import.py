"""Nightmare admission preserves existing roots and uses the shared FireRed contest fold."""
import argparse,importlib.util,json,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('shadow_copy_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
class ShadowCopyImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_NightmareClone'}):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_exact_closed_root_preserves_all_other_resources(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual({171},set(self.roots)-set(old));self.assertEqual('Move_NIGHTMARE',self.roots[171]['label'])
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for key in set(self.pack)-{'scripts'}:self.assertEqual(self.old[key],self.pack[key])
  self.assertEqual(self.oldpixels,self.pixels)
 def test_full_root_keeps_native_line_identity_and_folds_only_dead_contest_branch(self):
  row=self.roots[171];self.assertEqual({},row['spriteTags']);self.assertEqual({'SE_M_NIGHTMARE'},set(row['soundEffects']));self.assertNotIn('spriteDependencies',row);self.assertNotIn('materialDependencies',row)
  self.assertEqual(['fadetobg','waitbgfadein','monbg','createvisualtask','createvisualtask','playsewithpan','waitforvisualfinish','clearmonbg','restorebg','waitbgfadein','end'],[c['op'] for c in row['commands']])
  self.assertEqual(['BG_GHOST'],row['commands'][0]['args']);self.assertEqual(['ANIM_DEF_PARTNER'],row['commands'][2]['args']);self.assertEqual(['AnimTask_NightmareClone','2'],row['commands'][3]['args']);self.assertEqual(3145,row['commands'][3]['line'])
  self.assertFalse(any(c['op']=='jumpifcontest' for c in row['commands']))
 def test_missing_callback_and_sound_fail_closed(self):
  for attr,value in [('TASKS',imp.TASKS-{'AnimTask_NightmareClone'}),('SOUNDS',imp.SOUNDS-{'SE_M_NIGHTMARE'})]:
   with patch.object(imp,attr,value):data,_=imp.build(a.reference)
   self.assertNotIn(171,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_generated_pack_equals_local_candidate(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
