"""Memento's exact source-only root adds no sprite, map, tile or palette art."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('memento_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
NAMES={'AnimTask_InitMementoShadow','AnimTask_MoveAttackerMementoShadow','AnimTask_MementoHandleBg','AnimTask_MoveTargetMementoShadow'}
class MementoAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TASKS',imp.TASKS-NAMES):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_one_new_root_no_new_art_or_other_script_changes(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual({262},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for key in set(self.pack)-{'scripts'}:self.assertEqual(self.old[key],self.pack[key])
  self.assertEqual(self.oldpixels,self.pixels)
 def test_exact_pinned_commands_and_callback_order(self):
  row=self.roots[262];self.assertEqual('Move_MEMENTO',row['label']);self.assertEqual({},row['spriteTags']);self.assertEqual({'SE_M_PSYBEAM','SE_M_PSYBEAM2'},set(row['soundEffects']))
  self.assertFalse(any(key in row for key in ['spriteDependencies','materialDependencies','overlayDependencies']))
  self.assertEqual(['AnimTask_InitMementoShadow','AnimTask_MoveAttackerMementoShadow','AnimTask_MementoHandleBg','AnimTask_MoveTargetMementoShadow'],[c['args'][0] for c in row['commands'] if c['op']=='createvisualtask'])
  lines=imp.read(a.reference,'data/battle_anim_scripts.s').decode().splitlines()
  for c in row['commands']:
   text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual([['ANIM_TARGET'],['ANIM_TARGET']],[c['args'] for c in row['commands'] if c['op'] in {'monbg_static','clearmonbg_static'}])
 def test_missing_callback_or_bad_width_is_not_discovered(self):
  for name in NAMES:
   with patch.object(imp,'TASKS',imp.TASKS-{name}):rows,_=imp.discover_scripts(a.reference)
   self.assertNotIn(262,{r['moveId'] for r in rows})
  original=imp.read
  for replacement in ['AnimTask_MoveTargetMementoShadow, 5, 0','AnimTask_MoveTargetMementoShadow, 2']:
   def read(reference,path):
    value=original(reference,path);return value.replace(b'AnimTask_MoveTargetMementoShadow, 5\n',(replacement+'\n').encode()) if path=='data/battle_anim_scripts.s' else value
   with patch.object(imp,'read',side_effect=read):rows,_=imp.discover_scripts(a.reference)
   self.assertNotIn(262,{r['moveId'] for r in rows})
 def test_generated_pack_is_the_current_candidate(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
