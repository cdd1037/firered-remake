"""Spite owns one source root, one allocated palette tag, and no new art."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('spite_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
CALLBACK='AnimTask_SpiteTargetShadow'
class SpiteAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TASKS',imp.TASKS-{CALLBACK}):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_only_one_root_is_new_and_every_material_byte_is_unchanged(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual({180},set(self.roots)-set(old));self.assertEqual('Move_SPITE',self.roots[180]['label'])
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for key in set(self.pack)-{'scripts'}:self.assertEqual(self.old[key],self.pack[key])
  self.assertEqual(self.oldpixels,self.pixels)
 def test_exact_script_commands_palette_tag_and_source_lines(self):
  row=self.roots[180];self.assertEqual({'ANIM_TAG_BENT_SPOON':10097},row['spriteTags']);self.assertEqual({'SE_M_PSYBEAM'},set(row['soundEffects']))
  self.assertFalse(any(key in row for key in ['spriteDependencies','materialDependencies','overlayDependencies']))
  self.assertEqual(['fadetobg','playsewithpan','waitbgfadein','monbg','createvisualtask','createvisualtask','loopsewithpan','waitforvisualfinish','restorebg','waitbgfadein','clearmonbg','end'],[c['op'] for c in row['commands']])
  lines=imp.read(a.reference,'data/battle_anim_scripts.s').decode().splitlines()
  for c in row['commands']:
   text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual([CALLBACK,'2'],row['commands'][5]['args']);self.assertEqual(['ANIM_DEF_PARTNER'],row['commands'][3]['args']);self.assertEqual(['ANIM_TARGET'],row['commands'][10]['args'])
 def test_missing_callback_or_sound_and_malformed_callback_width_fail_closed(self):
  for attr,value in [('TASKS',imp.TASKS-{CALLBACK}),('SOUNDS',imp.SOUNDS-{'SE_M_PSYBEAM'})]:
   with patch.object(imp,attr,value):rows,_=imp.discover_scripts(a.reference)
   self.assertNotIn(180,{r['moveId'] for r in rows})
  original=imp.read
  for replacement in [CALLBACK+', 2, 0',CALLBACK+', 256',CALLBACK+', -1']:
   def read(reference,path):
    value=original(reference,path);return value.replace((CALLBACK+', 2\n').encode(),(replacement+'\n').encode()) if path=='data/battle_anim_scripts.s' else value
   with patch.object(imp,'read',side_effect=read):rows,_=imp.discover_scripts(a.reference)
   self.assertNotIn(180,{r['moveId'] for r in rows})
 def test_generated_pack_equals_current_pinned_candidate(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
