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
  self.assertEqual({7,52,53,56,190,324,341,84,86,9,55,85,121,152,192,48,103,319,71,72,202,105,51,61,92,141,145},{s['moveId'] for s in json.loads(data)['scripts']})
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
 def test_shared_sine_and_electric_shapes_are_source_derived(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  for name in ['gHydroPumpOrbSpriteTemplate','gMudShotOrbSpriteTemplate']:
   recipe=recipes[name];self.assertEqual('AnimToTargetInSinWave',recipe['callback']);self.assertEqual((16,16),(recipe['width'],recipe['height']));self.assertTrue(recipe['blend'])
  wave=recipes['gThunderWaveSpriteTemplate'];self.assertEqual((32,16),(wave['width'],wave['height']))
  electricity=pack['subroutines']['ElectricityEffect'];self.assertEqual(8,sum(c['op']=='createsprite' for c in electricity));self.assertEqual(7,sum(c['op']=='delay' for c in electricity))
 def test_electric_split_art_uses_shared_source_palette(self):
  _,assets=imp.build(REFERENCE);rows=(REFERENCE/'graphics/battle_anims/sprites/spark.pal').read_text().splitlines()[3:];palette=[int(c) for row in rows for c in row.split()]
  for asset,png,shape in [('spark-thin','spark_0',(8,64)),('spark-wide','spark_1',(16,64)),('spark-h','spark_h',(32,32))]:
   with Image.open(REFERENCE/('graphics/battle_anims/sprites/'+png+'.png')) as image:indices=image.tobytes()
   self.assertEqual(shape,struct.unpack_from('<HH',assets[asset]));expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
   self.assertEqual(expected,list(struct.unpack_from('<'+str(len(indices))+'H',assets[asset],4)))
 def test_affine_programs_preserve_source_commands(self):
  data,_=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  orb=recipes['gThunderboltOrbSpriteTemplate']['affineAnimations'][0]
  self.assertEqual([('frame',232,232,0),('frame',-8,-8,10),('frame',8,8,10),('jump',0,0,0)],[(c['op'],c['x'],c['y'],c['duration']) for c in orb]);self.assertEqual(1,orb[-1]['target'])
  drop=recipes['gWaterGunDropletSpriteTemplate'];self.assertTrue(drop['affineDoubleSize']);self.assertEqual((-16,16,6),tuple(drop['affineAnimations'][0][0][k] for k in ['x','y','duration']))
 def test_small_bubbles_preserve_source_concatenated_tile_addresses(self):
  _,assets=imp.build(REFERENCE);blob=assets['ice-crystals'];self.assertEqual((8,112),struct.unpack_from('<HH',blob))
  rows=(REFERENCE/'graphics/battle_anims/sprites/ice_crystals.pal').read_text().splitlines()[3:];palette=[int(c) for row in rows for c in row.split()]
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/ice_crystals_4.png') as image:indices=image.tobytes()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<128H',blob,4+12*64*2)))
 def test_discovery_excludes_root_with_uncovered_command(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'Move_ABSORB:\n',b'Move_ABSORB:\n\tunsupported_command 0\n') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
  ids={s['moveId'] for s in json.loads(data)['scripts']};self.assertNotIn(71,ids);self.assertIn(72,ids);self.assertNotIn(120,ids);self.assertNotIn(153,ids);self.assertNotIn(209,ids)
 def test_ring_duration_is_unsigned_source_field(self):
  data,_=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  ring=recipes['gSupersonicRingSpriteTemplate'];self.assertEqual(200,ring['affineAnimations'][0][1]['duration']);self.assertEqual([0],ring['loopStarts']);self.assertEqual((16,32),(ring['width'],ring['height']))
 def test_poison_and_water_shared_palettes_match_source_tables(self):
  _,assets=imp.build(REFERENCE)
  for asset,png,palette_png in [('toxic-bubble','toxic_bubble','poison_bubble'),('bubble','bubble','rain_drops')]:
   with Image.open(REFERENCE/('graphics/battle_anims/sprites/'+png+'.png')) as image:indices=image.tobytes()
   with Image.open(REFERENCE/('graphics/battle_anims/sprites/'+palette_png+'.png')) as image:palette=image.getpalette()
   expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
   self.assertEqual(expected,list(struct.unpack_from('<'+str(len(indices))+'H',assets[asset],4)))
 def test_negative_needle_rotation_uses_unsigned_source_byte(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  self.assertEqual(223,recipes['gLeechLifeNeedleSpriteTemplate']['affineAnimations'][0][0]['rotation'])
  self.assertFalse({124,188}&{s['moveId'] for s in pack['scripts']},'Out-of-range source sludge animation index is not admitted.')
 def test_unknown_source_callback_is_rejected(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'.callback = AnimEmberFlare,',b'.callback = UnsupportedFlare,') if path=='src/battle_anim_fire.c' else data
  with patch.object(imp,'read',side_effect=read):
   with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
