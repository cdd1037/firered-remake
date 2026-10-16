#!/usr/bin/env python3
"""Source-backed elemental import and RGB555 checks; no reference code execution."""
import importlib.util,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('elemental_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ElementalImportTests(unittest.TestCase):
 def test_runtime_pack_and_pixels_regenerate(self):
  data,assets=imp.build(REFERENCE)
  self.assertEqual(data,(ROOT/'content/generated/battle/animations/elemental.json').read_bytes())
  self.assertEqual({7,52,53},{s['moveId'] for s in json.loads(data)['scripts']})
  for name,blob in assets.items():self.assertEqual(blob,(ROOT/('content/generated/battle/animations/elemental-'+name+'.rgb555')).read_bytes())
 def test_small_ember_pixels_match_palette_and_transparency(self):
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/small_ember.png') as image:
   palette=image.getpalette();indices=image.tobytes()
  blob=(ROOT/'content/generated/battle/animations/elemental-small-ember.rgb555').read_bytes()
  self.assertEqual((32,160),struct.unpack_from('<HH',blob))
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<5120H',blob,4)))
 def test_source_frames_preserve_loops_and_subroutine_timing(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  recipes={r['template']:r for r in pack['spriteRecipes']}
  flame=recipes['gFlamethrowerFlameSpriteTemplate'];self.assertEqual([0],flame['loopStarts']);self.assertEqual([16,32,48],[f['tile'] for f in flame['animations'][0]]);self.assertTrue(all(f['duration']==2 for f in flame['animations'][0]))
  self.assertEqual(['createsprite','delay','createsprite','delay','return'],[c['op'] for c in pack['subroutines']['FlamethrowerCreateFlames']])
  self.assertEqual(['16','32','48'],[str(f['tile']) for f in recipes['gFireSpreadSpriteTemplate']['animations'][1]])
  self.assertTrue(all(f['hFlip'] and f['vFlip'] for f in recipes['gFireSpreadSpriteTemplate']['animations'][1]))
 def test_unknown_source_callback_is_rejected(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'.callback = AnimEmberFlare,',b'.callback = UnsupportedFlare,') if path=='src/battle_anim_fire.c' else data
  with patch.object(imp,'read',side_effect=read):
   with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
