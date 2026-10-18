"""Source closure, exact material reuse and typed affine bytes for expulsion roots."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('expulsion_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
class ExpulsionImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!='gWhirlwindLineSpriteTemplate'}),patch.object(imp,'TASKS',imp.TASKS-imp.EXPULSION_TASKS):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_only_three_new_roots_leave_every_previous_row_recipe_and_pixel_unchanged(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual({18,46,100},set(self.roots)-set(old))
  for k,row in old.items():self.assertEqual(row,self.roots[k])
  before={r['template']:r for r in self.old['spriteRecipes']};after={r['template']:r for r in self.pack['spriteRecipes']};self.assertEqual({'gWhirlwindLineSpriteTemplate'},set(after)-set(before))
  for k,row in before.items():self.assertEqual(row,after[k])
  for k,row in self.old['subroutines'].items():self.assertEqual(row,self.pack['subroutines'][k])
  self.assertEqual({'SetPsychicBackground','UnsetPsychicBackground'},set(self.pack['subroutines'])-set(self.old['subroutines']))
  self.assertEqual(self.oldpixels,self.pixels);self.assertEqual(self.old['assets'],self.pack['assets']);self.assertEqual(self.old['indexedAssets'],self.pack['indexedAssets'])
  for k,row in self.old['taskAffinePrograms'].items():self.assertEqual(row,self.pack['taskAffinePrograms'][k])
  self.assertEqual({'sAffineAnim_Teleport'},set(self.pack['taskAffinePrograms'])-set(self.old['taskAffinePrograms']))
 def test_whirlwind_reuses_physical_indexed_material_and_native_frame_sequence(self):
  r=next(r for r in self.pack['spriteRecipes'] if r['template']=='gWhirlwindLineSpriteTemplate');self.assertEqual(('whirlwind-lines','AnimWhirlwindLine',32,16,False,False),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([],r['affineAnimations']);self.assertEqual([-1],r['loopStarts']);self.assertEqual([(0,False),(8,False),(16,False),(8,True),(0,True)],[(f['tile'],f['hFlip']) for f in r['animations'][0]]);self.assertTrue(all(f['duration']==1 and not f['vFlip'] for f in r['animations'][0]));self.assertNotIn('whirlwind-lines',self.pixels)
  physical=json.loads((a.generated/'physical.json').read_bytes());materials=[m for m in physical['indexedAssets'] if m['id']=='whirlwind-lines'];self.assertEqual(1,len(materials));m=materials[0];self.assertEqual((32,48),(m['width'],m['height']));self.assertEqual(0x300,len((a.generated/m['indicesPath']).read_bytes()));self.assertEqual(32,len((a.generated/m['palettePath']).read_bytes()))
 def test_teleport_affine_duration_is_u8_and_shared_background_helpers_stay_source_exact(self):
  rows=self.pack['taskAffinePrograms']['sAffineAnim_Teleport'];self.assertEqual([(64,-4,0,20),(0,0,0,200),(0,0,0,0)],[(r['x'],r['y'],r['rotation'],r['duration']) for r in rows]);self.assertEqual(['frame','frame','end'],[r['op'] for r in rows]);self.assertEqual(['BG_PSYCHIC'],self.pack['subroutines']['SetPsychicBackground'][0]['args']);self.assertEqual(['7','0xFFFF'],self.pack['subroutines']['UnsetPsychicBackground'][2]['args'])
  text='const union AffineAnimCmd sample[] = {\nAFFINEANIMCMD_FRAME(0,0,-1,-56),\nAFFINEANIMCMD_END,\n};';r=imp.task_affine_program(text,'sample');self.assertEqual((255,200),(r[0]['rotation'],r[0]['duration']))
  for invalid in [-129,256]:
   with self.assertRaises(ValueError):imp.task_affine_program(text.replace('-56',str(invalid)),'sample')
 def test_roar_keeps_native_two_cry_selector_and_existing_noise_scale_helpers(self):
  commands=self.roots[46]['commands'];self.assertIn(['SoundTask_PlayDoubleCry','2','ANIM_ATTACKER','DOUBLE_CRY_ROAR'],[c['args'] for c in commands]);self.assertIn(['AnimTask_ScaleMonAndRestore','5','-5','-5','10','ANIM_ATTACKER','1'],[c['args'] for c in commands]);self.assertIn(['AnimTask_SlideOffScreen','5','ANIM_TARGET','2'],[c['args'] for c in commands]);self.assertEqual(self.old['subroutines']['RoarEffect'],self.pack['subroutines']['RoarEffect'])
 def test_removing_each_adapter_closes_only_the_roots_that_need_it(self):
  for callback,moves in [('AnimTask_SlideOffScreen',{18,46}),('AnimTask_Teleport',{100})]:
   with patch.object(imp,'TASKS',imp.TASKS-{callback}):data,_=imp.build(a.reference)
   rows={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual(moves,set(self.roots)-set(rows))
 def test_source_callback_drift_fails_before_publication(self):
  original=imp.read
  cases=[('src/battle_anim_mon_movement.c',b'gSprites[spriteId].x2 += gTasks[taskId].data[1];',b'gSprites[spriteId].x2 -= gTasks[taskId].data[1];'),('src/battle_anim_psychic.c',b'if (++task->data[2] > 19)',b'if (++task->data[2] > 18)'),('src/battle_anim_flying.c',b'sprite->data[1] = 0x0ccc;',b'sprite->data[1] = 0x0ccd;'),('include/constants/sound.h',b'DOUBLE_CRY_ROAR  2',b'DOUBLE_CRY_ROAR  0')]
  for path,old,new in cases:
   def changed(root,name):
    blob=original(root,name)
    if name==path:self.assertIn(old,blob);return blob.replace(old,new)
    return blob
   with patch.object(imp,'read',changed),self.assertRaises(ValueError):imp.build(a.reference)
 def test_generated_pack_and_all_preserved_materials_match_changed_producer(self):
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
