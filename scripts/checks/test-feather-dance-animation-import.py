"""One source-derived root, existing WhiteFeather indexed art, native callback contracts."""
import argparse,importlib.util,json,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');p.add_argument('--skip-generated',action='store_true');a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('feather_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
TEMPLATE='gFallingFeatherSpriteTemplate'
class FeatherDanceImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=TEMPLATE}):old,cls.oldpixels=imp.build(a.reference)
  cls.old=json.loads(old)
 def test_exact_one_new_root_and_recipe_preserve_all_existing_content(self):
  old={r['moveId']:r for r in self.old['scripts']};self.assertEqual({297},set(self.roots)-set(old))
  for k,row in old.items():self.assertEqual(row,self.roots[k])
  before={r['template']:r for r in self.old['spriteRecipes']};after={r['template']:r for r in self.pack['spriteRecipes']};self.assertEqual({TEMPLATE},set(after)-set(before))
  for k,row in before.items():self.assertEqual(row,after[k])
  for key in set(self.pack)-{'scripts','spriteRecipes'}:self.assertEqual(self.old[key],self.pack[key])
  self.assertEqual(self.oldpixels,self.pixels)
 def test_native_eight_feathers_use_existing_indexed_material_and_zero_duration_frame_programs(self):
  root=self.roots[297];self.assertEqual('Move_FEATHER_DANCE',root['label']);self.assertEqual(['white-feather'],root['materialDependencies']);self.assertEqual(['ANIM_TAG_WHITE_FEATHER'],list(root['spriteTags']))
  sprites=[c for c in root['commands'] if c['op']=='createsprite'];self.assertEqual(8,len(sprites));self.assertEqual([64,32,0,224,128,192,160,96],[int(c['args'][5]) for c in sprites]);self.assertTrue(all(c['args'][:5]==[TEMPLATE,'ANIM_TARGET','0','0','-16'] and c['args'][6:]==['2','104','11304','32','1'] for c in sprites))
  recipe=next(r for r in self.pack['spriteRecipes'] if r['template']==TEMPLATE);self.assertEqual(('white-feather','AnimFallingFeather',32,32,False,False),(recipe['asset'],recipe['callback'],recipe['width'],recipe['height'],recipe['blend'],recipe['affineDoubleSize']));self.assertEqual([],recipe['affineAnimations']);self.assertEqual([-1,-1],recipe['loopStarts']);self.assertEqual([[{'tile':0,'duration':0,'hFlip':False,'vFlip':False}],[{'tile':16,'duration':0,'hFlip':True,'vFlip':False}]],recipe['animations'])
  asset=next(r for r in self.pack['indexedAssets'] if r['id']=='white-feather');self.assertEqual((32,64),(asset['width'],asset['height']));self.assertEqual(1024,len(self.pixels['white-feather.4bpp']));self.assertEqual(32,len(self.pixels['white-feather.pal16']))
 def test_source_manual_affine_width_coordinate_fall_and_destructor_drift_fail_before_publication(self):
  original=imp.read
  for old,new in [(b'.oam = &gOamData_AffineNormal_ObjNormal_32x32,',b'.oam = &gOamData_AffineOff_ObjNormal_32x32,'),(b'u16 unk8;',b's16 unk8;'),(b'GetBattlerSpriteCoord(battler, BATTLER_COORD_ATTR_HEIGHT)',b'GetBattlerSpriteCoord(battler, BATTLER_COORD_X_2)'),(b'data->unk8 += data->unk6;',b'data->unk8 -= data->unk6;'),(b'if (sprite->data[0]-- <= 0)',b'if (sprite->data[0]-- < 0)')]:
   def changed(root,path):
    blob=original(root,path)
    if path=='src/battle_anim_flying.c':self.assertIn(old,blob);return blob.replace(old,new)
    return blob
   with patch.object(imp,'read',changed),self.assertRaises(ValueError):imp.build(a.reference)
 def test_generated_pack_is_exact_candidate_and_all_prior_material_bytes_match(self):
  if a.skip_generated:self.skipTest('Explicit staging run: canonical generated publication belongs to parent')
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
