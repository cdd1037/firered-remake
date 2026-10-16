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
  self.assertEqual({7,52,53,56,190,324,341,84,86,9,55,85,121,152,192,48,103,319,71,72,202,105,51,61,92,141,145,83,250,328,44,128,116,203,8,58,181,82,126,172,225,204,343,231,342,75,129,223,200},{s['moveId'] for s in json.loads(data)['scripts']})
  for name,blob in assets.items():self.assertEqual(blob,(ROOT/('content/generated/battle/animations/elemental-'+name+'.rgb555')).read_bytes())
 def test_vortex_shared_callback_and_concatenated_sand_tiles(self):
  data,assets=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  for name in ['gSwirlingDirtSpriteTemplate','gWhirlpoolSpriteTemplate','gFireSpinSpriteTemplate']:self.assertEqual('AnimParticleInVortex',recipes[name]['callback'])
  self.assertEqual((8,40),struct.unpack_from('<HH',assets['mud-sand']))
  indices=[]
  for name in ['mud_sand_0','mud_sand_1']:
   with Image.open(REFERENCE/('graphics/battle_anims/sprites/'+name+'.png')) as image:
    for y in range(0,image.height,8):
     for x in range(0,image.width,8):indices.extend(image.crop((x,y,x+8,y+8)).tobytes())
  rows=(REFERENCE/'graphics/battle_anims/sprites/mud_sand.pal').read_text().splitlines()[3:];palette=[int(c) for row in rows for c in row.split()]
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<320H',assets['mud-sand'],4)))
 def test_bite_affine_table_and_shared_clamp_palette(self):
  data,assets=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  for name in ['gSharpTeethSpriteTemplate','gClampJawSpriteTemplate']:
   recipe=recipes[name];self.assertEqual('AnimBite',recipe['callback']);self.assertEqual((64,64),(recipe['width'],recipe['height']));self.assertEqual(list(range(0,256,32)),[a[0]['rotation'] for a in recipe['affineAnimations']])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/sharp_teeth.png') as image:palette=image.getpalette()
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/clamp.png') as image:indices=image.tobytes()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<4096H',assets['clamp'],4)))
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
 def test_ice_sound_task_retains_source_no_priority_abi(self):
  data,_=imp.build(REFERENCE);scripts={s['moveId']:s for s in json.loads(data)['scripts']}
  task=next(c for c in scripts[58]['commands'] if c['op']=='createsoundtask')
  self.assertEqual(['SoundTask_LoopSEAdjustPanning','SE_M_BUBBLE_BEAM2','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','4','4','0','10'],task['args'])
  self.assertIn('waitsound',[c['op'] for c in scripts[181]['commands']])
  original=imp.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'createsoundtask SoundTask_LoopSEAdjustPanning, SE_M_BUBBLE_BEAM2',b'createsoundtask SoundTask_Unknown, SE_M_BUBBLE_BEAM2') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
  self.assertNotIn(58,{s['moveId'] for s in json.loads(data)['scripts']})
 def test_energy_and_ice_frame_programs_are_source_derived(self):
  data,assets=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  energy=recipes['gEndureEnergySpriteTemplate'];self.assertEqual([(0,4),(8,12),(16,4),(24,4)],[(f['tile'],f['duration']) for f in energy['animations'][0]])
  self.assertEqual((16,128),struct.unpack_from('<HH',assets['focus-energy']))
  ice=recipes['gIceCrystalHitLargeSpriteTemplate'];self.assertEqual([(206,206,0),(5,5,10),(0,0,6),(0,0,0)],[(c['x'],c['y'],c['duration']) for c in ice['affineAnimations'][0]])
 def test_fire_bundle_uses_shared_source_palette_and_sound_arguments(self):
  data,assets=imp.build(REFERENCE);scripts={s['moveId']:s for s in json.loads(data)['scripts']}
  task=next(c for c in scripts[126]['commands'] if c['op']=='createsoundtask');self.assertEqual(['SoundTask_FireBlast','SE_M_FLAME_WHEEL','SE_M_FLAME_WHEEL2'],task['args'])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/fire.png') as image:palette=image.getpalette()
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/fire_plume.png') as image:indices=image.tobytes()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<5120H',assets['fire-plume'],4)))
 def test_tail_material_and_heart_dependencies_stay_shared(self):
  data,assets=imp.build(REFERENCE);pack=json.loads(data);scripts={s['moveId']:s for s in pack['scripts']}
  self.assertNotIn(106,scripts)
  for move in [231,342]:
   commands=scripts[move]['commands'];self.assertIn('AnimTask_MetallicShine',[c['args'][0] for c in commands if c['op']=='createvisualtask']);self.assertIn('AnimTask_SetGrayscaleOrOriginalPal',[c['args'][0] for c in commands if c['op']=='createvisualtask'])
  self.assertEqual((16,16),struct.unpack_from('<HH',assets['magenta-heart']))
  self.assertNotIn('metallic',assets);self.assertNotIn('item-bag',assets)
  self.assertIn('ANIM_TAG_ITEM_BAG',[c['args'][0] for c in scripts[343]['commands'] if c['op']=='loadspritegfx'])
 def test_shared_sine_projectiles_and_unused_explosion_load_are_materialized(self):
  data,assets=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']};scripts={s['moveId']:s for s in pack['scripts']}
  for name in ['gSwiftStarSpriteTemplate','gRazorLeafCutterSpriteTemplate']:self.assertEqual('AnimTranslateLinearSingleSineWave',recipes[name]['callback'])
  self.assertEqual((32,128),struct.unpack_from('<HH',assets['explosion-6']))
  self.assertIn('ANIM_TAG_EXPLOSION_6',[c['args'][0] for c in scripts[223]['commands'] if c['op']=='loadspritegfx'])
 def test_psychic_ring_recipes_reuse_source_callbacks_and_palette(self):
  data,assets=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  self.assertEqual('TranslateAnimSpriteToTargetMonLocation',recipes['gGoldRingSpriteTemplate']['callback'])
  ring=recipes['gPsywaveRingSpriteTemplate'];self.assertEqual('AnimToTargetInSinWave',ring['callback']);self.assertTrue(ring['affineDoubleSize']);self.assertEqual((16,32),(ring['width'],ring['height']))
  rows=(REFERENCE/'graphics/battle_anims/sprites/blue_ring.pal').read_text().splitlines()[3:];palette=[int(c) for row in rows for c in row.split()]
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/gold_ring.png') as image:indices=image.tobytes()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<512H',assets['blue-ring'],4)))
 def test_unknown_source_callback_is_rejected(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'.callback = AnimEmberFlare,',b'.callback = UnsupportedFlare,') if path=='src/battle_anim_fire.c' else data
  with patch.object(imp,'read',side_effect=read):
   with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
