#!/usr/bin/env python3
import importlib.util,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('flying_import',ROOT/'scripts/imports/battle/import-source-flying-animations.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class FlyingImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data,cls.blobs=module.build(ROOT/'content/raw/pokefirered/037335f4');cls.pack=json.loads(cls.data)
 def test_complete_source_roots(self):self.assertEqual([r['moveId'] for r in self.pack['scripts']],[16,17,211])
 def test_indexed_native_geometry(self):
  row,=self.pack['indexedAssets'];self.assertEqual((row['width'],row['height'],row['paletteTag']),(32,64,'ANIM_TAG_GUST'));self.assertEqual(len(self.blobs[row['indicesPath']]),1024);self.assertEqual(len(self.blobs[row['palettePath']]),32);self.assertEqual(self.pack['assets'],[])
 def test_affine_delay_program(self):
  recipes={r['callback']:r for r in self.pack['spriteRecipes']};self.assertEqual(recipes['AnimEllipticalGust']['affineAnimations'],[])
  program,=recipes['AnimGustToTarget']['affineAnimations'];self.assertEqual([(r['op'],r['x'],r['y'],r['duration']) for r in program],[('frame',16,256,0),('frame',10,0,24),('end',0,0,0)])
 def test_palette_task_and_numeric_dependencies(self):
  for root in self.pack['scripts']:
   task,=[c for c in root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_AnimateGustTornadoPalette'];self.assertEqual(task['args'][1:],['5','1','70']);self.assertIn('ANIM_TAG_GUST',root['spriteTags']);self.assertTrue(all(isinstance(v,int) for v in root['soundEffects'].values()))
if __name__=='__main__':unittest.main()
