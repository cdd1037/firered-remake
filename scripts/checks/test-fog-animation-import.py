#!/usr/bin/env python3
import importlib.util,json,struct,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('fog_import',ROOT/'scripts/imports/battle/import-source-fog-animations.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class FogImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data,cls.blobs=module.build(ROOT/'content/raw/pokefirered/037335f4');cls.pack=json.loads(cls.data)
 def test_complete_pair(self):self.assertEqual([r['moveId'] for r in self.pack['scripts']],[54,123])
 def test_shared_callback_and_two_frame_program(self):
  for r in self.pack['spriteRecipes']:
   self.assertEqual(r['callback'],'InitSwirlingFogAnim');self.assertEqual((r['width'],r['height'],r['blend']),(32,16,True));self.assertEqual([(f['tile'],f['duration'])for f in r['animations'][0]],[(0,8),(8,8)]);self.assertEqual(r['loopStarts'],[0])
 def test_shared_sheet_distinct_source_palettes(self):
  white=self.blobs['fog-mist-cloud.pal16'];purple=self.blobs['fog-purple-gas-cloud.pal16'];self.assertEqual(len(white),32);self.assertNotEqual(white,purple);self.assertEqual(len(self.blobs['fog-cloud.4bpp']),512)
  self.assertEqual({r['indicesPath'] for r in self.pack['indexedAssets']},{'fog-cloud.4bpp'});self.assertEqual([(r['width'],r['height']) for r in self.pack['indexedAssets']],[(32,32),(32,32)])
 def test_exact_cloud_operands_and_priority_command(self):
  self.assertEqual(self.pack['subroutines']['MistCloud'][0]['args'][3:],['0','-24','48','240','0','1']);self.assertEqual(self.pack['subroutines']['SmogCloud'][0]['args'][3:],['0','-24','48','240','1','0'])
  smog=self.pack['scripts'][1];self.assertEqual([r['args']for r in smog['commands']if r['op']=='splitbgprio_all'],[[]]);self.assertEqual(set(smog['soundEffects']),{'SE_M_MIST','SE_M_TOXIC'})
if __name__=='__main__':unittest.main()
