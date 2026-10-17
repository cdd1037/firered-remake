#!/usr/bin/env python3
import importlib.util,io,json,struct,unittest
from PIL import Image
from unittest.mock import patch
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('flying_import',ROOT/'scripts/imports/battle/import-source-flying-animations.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class FlyingImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data,cls.blobs=module.build(ROOT/'content/raw/pokefirered/037335f4');cls.pack=json.loads(cls.data)
 def test_complete_source_roots(self):self.assertEqual([r['moveId'] for r in self.pack['scripts']],[16,17,211])
 def test_indexed_native_geometry(self):
  row=next(r for r in self.pack['indexedAssets'] if r['id']=='gust');self.assertEqual((row['width'],row['height'],row['paletteTag']),(32,64,'ANIM_TAG_GUST'));self.assertEqual(len(self.blobs[row['indicesPath']]),1024);self.assertEqual(len(self.blobs[row['palettePath']]),32);self.assertEqual(self.pack['assets'],[])
 def test_affine_delay_program(self):
  recipes={r['callback']:r for r in self.pack['spriteRecipes']};self.assertEqual(recipes['AnimEllipticalGust']['affineAnimations'],[])
  program,=recipes['AnimGustToTarget']['affineAnimations'];self.assertEqual([(r['op'],r['x'],r['y'],r['duration']) for r in program],[('frame',16,256,0),('frame',10,0,24),('end',0,0,0)])
 def test_palette_task_and_numeric_dependencies(self):
  for root in self.pack['scripts']:
   task,=[c for c in root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_AnimateGustTornadoPalette'];self.assertEqual(task['args'][1:],['5','1','70']);self.assertIn('ANIM_TAG_GUST',root['spriteTags']);self.assertTrue(all(isinstance(v,int) for v in root['soundEffects'].values()))
 def test_air_wave_direct_source_frame_mapping(self):
  recipe=next(r for r in self.pack['spriteRecipes'] if r['callback']=='AnimAirWaveCrescent');self.assertEqual((32,16),(recipe['width'],recipe['height']));self.assertEqual([0],recipe['loopStarts']);self.assertEqual([(3,False,False),(3,True,False),(3,False,True),(3,True,True)],[(r['duration'],r['hFlip'],r['vFlip']) for r in recipe['animations'][0]])
  row=next(r for r in self.pack['indexedAssets'] if r['id']=='air-wave-2');self.assertEqual(256,len(self.blobs[row['indicesPath']]));self.assertEqual(32,len(self.blobs[row['palettePath']]))
 def test_air_wave_indices_and_colors_match_every_original_pixel(self):
  tiles=self.blobs['flying-air-wave-2.4bpp'];palette=struct.unpack('<16H',self.blobs['flying-air-wave-2.pal16'])
  with Image.open(io.BytesIO(module.shared.read(ROOT/'content/raw/pokefirered/037335f4','graphics/battle_anims/sprites/air_wave_2.png'))) as image:
   colors=image.getpalette()
   for y in range(16):
    for x in range(32):
     offset=((y//8)*4+x//8)*32+(y%8)*4+(x%8)//2;index=(tiles[offset]>>(4*(x%2)))&15;self.assertEqual(image.getpixel((x,y)),index);self.assertEqual(sum((colors[index*3+c]//8)<<(5*c) for c in range(3)),palette[index])
 def test_raw_control_insertion_cannot_masquerade_as_a_normalized_frame_index(self):
  original=module.shared.read
  def read(ref,path):
   data=original(ref,path)
   return data.replace(b'sAffineAnim_AirWaveCrescent[] =\n{',b'sAffineAnim_AirWaveCrescent[] =\n{\n    ANIMCMD_JUMP(0),') if path=='src/battle_anim_flying.c' else data
  with patch.object(module.shared,'read',side_effect=read):
   with self.assertRaisesRegex(ValueError,'four direct source FRAME'):module.build(ROOT/'content/raw/pokefirered/037335f4')
if __name__=='__main__':unittest.main()
