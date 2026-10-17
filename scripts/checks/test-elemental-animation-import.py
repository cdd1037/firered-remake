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
 def test_hail_task_child_dependencies_and_native_material(self):
  data,pixels=imp.build(REFERENCE);pack=json.loads(data);row=next(r for r in pack['scripts'] if r['moveId']==258)
  self.assertEqual(['sHailParticleSpriteTemplate','gIceCrystalHitLargeSpriteTemplate'],row['spriteDependencies'])
  self.assertEqual({'SE_M_HAIL'},set(row['soundEffects']))
  recipes={r['template']:r for r in pack['spriteRecipes']};hail=recipes[row['spriteDependencies'][0]]
  self.assertEqual(('AnimHailBegin','hail',16,16,False),(hail['callback'],hail['asset'],hail['width'],hail['height'],hail['blend']))
  self.assertEqual([256,240,224],[p[0]['x'] for p in hail['affineAnimations']])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/hail.png') as image:
   palette=image.getpalette();expected=[sum((palette[index*3+c]//8)<<(5*c) for c in range(3))+(32768 if index else 0) for index in image.tobytes()]
  self.assertEqual((16,16),struct.unpack_from('<HH',pixels['hail']));self.assertEqual(expected,list(struct.unpack_from('<256H',pixels['hail'],4)))

 def test_hail_raw_creation_or_handoff_changes_are_rejected(self):
  original=imp.read
  for before,after in [(b'CreateSprite(&sHailParticleSpriteTemplate,',b'CreateSprite(&gUnknownHailSpriteTemplate,'),(b'.callback = AnimHailContinue;',b'.callback = UnknownHailContinue;')]:
   def read(reference,path):
    data=original(reference,path);return data.replace(before,after) if path=='src/battle_anim_ice.c' else data
   with patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)

 def test_rock_roots_preserve_shared_fragment_and_packed_mode_dependencies(self):
  data,_=imp.build(REFERENCE);rows={r['moveId']:r for r in json.loads(data)['scripts']}
  for move,count in [(249,8),(350,4)]:
   fragments=[c for c in rows[move]['commands'] if c['op']=='createsprite' and c['args'][0]=='gRockFragmentSpriteTemplate']
   self.assertEqual(count,len(fragments))
  projectile=next(c for c in rows[350]['commands'] if c['op']=='createsprite' and c['args'][0]=='gRockBlastRockSpriteTemplate')
  self.assertEqual(['16','0','0','0','25','257'],projectile['args'][3:]);self.assertEqual({'SE_M_SWAGGER','SE_M_ROCK_THROW'},set(rows[350]['soundEffects']))
  original=imp.read
  def read(reference,path):
   data=original(reference,path);return data.replace(b'gRockFragmentSpriteTemplate,',b'gMissingRockFragmentSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
  self.assertFalse({249,350}&{r['moveId'] for r in json.loads(data)['scripts']})

 def test_rock_fragments_and_projectile_share_source_rocks_without_new_pixels(self):
  blob,pixels=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(blob)['spriteRecipes']}
  fragment=recipes['gRockFragmentSpriteTemplate'];projectile=recipes['gRockBlastRockSpriteTemplate']
  self.assertEqual(('rocks',32,32,False),(fragment['asset'],fragment['width'],fragment['height'],fragment['blend']))
  self.assertEqual([32,48,64],[frames[0]['tile'] for frames in fragment['animations']]);self.assertEqual([],fragment['affineAnimations'])
  self.assertEqual(('rocks',32,32,False),(projectile['asset'],projectile['width'],projectile['height'],projectile['affineDoubleSize']))
  self.assertEqual([251,5],[program[0]['rotation'] for program in projectile['affineAnimations']]);self.assertTrue(all(program[-1]['op']=='jump' and program[-1]['target']==0 for program in projectile['affineAnimations']))
  self.assertEqual((32,192),struct.unpack_from('<HH',pixels['rocks']))
  source=imp.read(REFERENCE,'src/battle_anim_rock.c').decode()
  with self.assertRaises(ValueError):imp.recipe(source.replace('.callback = AnimRockFragment,','.callback = MissingRockCallback,'),'gRockFragmentSpriteTemplate','AnimRockFragment')

 def test_healing_callbacks_share_ring_and_preserve_affine_loop_program(self):
  data,pixels=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']}
  egg=recipes['gSoftBoiledEggSpriteTemplate'];milk=recipes['gMilkBottleSpriteTemplate']
  self.assertEqual(('AnimSoftBoiledEgg',32,32,True,True),(egg['callback'],egg['width'],egg['height'],egg['blend'],egg['affineDoubleSize']))
  self.assertEqual(('AnimMilkBottle',32,32,True,False),(milk['callback'],milk['width'],milk['height'],milk['blend'],milk['affineDoubleSize']))
  self.assertEqual([0,1],[c['target'] for c in egg['affineAnimations'][2] if c['op']=='loop'])
  self.assertNotIn('gThinRingExpandingSpriteTemplate',recipes)
  self.assertNotIn('thin-ring',pixels)
  roots={r['moveId']:r for r in pack['scripts']}
  self.assertTrue(any(c['op']=='setarg' and c['args']==['7','0xFFFF'] for c in roots[135]['commands']))
  for move in [135,208]:self.assertTrue(any(c['op']=='call' and c['args']==['HealingEffect2'] for c in roots[move]['commands']))

 def test_reachable_graphics_tags_retain_original_numeric_operands(self):
  import re
  data,_=imp.build(REFERENCE);pack=json.loads(data)
  tags={name:10000+int(value) for name,value in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',imp.read(REFERENCE,'include/constants/battle_anim.h').decode(),re.M)}
  for row in pack['scripts']:
   flat=row['commands']+[c for call in row['commands'] if call['op']=='call' for c in pack['subroutines'][call['args'][0]]]
   names={c['args'][0] for c in flat if c['op']=='loadspritegfx'}
   if any(c['op']=='createvisualtask' and c['args'][0] in {'AnimTask_MusicNotesRainbowBlend','AnimTask_MusicNotesClearRainbowBlend'} for c in flat):names|={'ANIM_TAG_MUSIC_NOTES','ANIM_TAG_BENT_SPOON','ANIM_TAG_SPHERE_TO_CUBE','ANIM_TAG_LARGE_FRESH_EGG'}
   self.assertEqual({name:tags[name] for name in names},row['spriteTags'])

 def test_healing_pixels_match_original_indexed_palette(self):
  _,pixels=imp.build(REFERENCE)
  for name,shape in [('breaking-egg',(32,96)),('milk-bottle',(32,32))]:
   with Image.open(REFERENCE/('graphics/battle_anims/sprites/'+name.replace('-','_')+'.png')) as image:
    palette=image.getpalette();expected=[sum((palette[i*3+c]>>3)<<(5*c) for c in range(3))|(32768 if i else 0) for i in image.tobytes()]
   blob=pixels[name];self.assertEqual(shape,struct.unpack_from('<HH',blob));self.assertEqual(expected,list(struct.unpack_from('<'+'H'*len(expected),blob,4)))

 def test_runtime_pack_and_pixels_regenerate(self):
  data,assets=imp.build(REFERENCE)
  self.assertEqual(data,(ROOT/'content/generated/battle/animations/elemental.json').read_bytes())
  self.assertEqual({160,176,258,352,47,187,320,135,208,249,350,7,52,53,56,190,324,341,84,86,9,55,85,121,152,192,48,103,319,71,72,202,105,51,61,92,141,145,83,250,328,44,128,116,203,8,58,181,82,126,172,225,204,343,231,342,75,129,223,200,235,147,178,88,157},{s['moveId'] for s in json.loads(data)['scripts']})
  for name,blob in assets.items():self.assertEqual(blob,(ROOT/('content/generated/battle/animations/elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'content/generated/battle/animations/elemental-'+name+'.rgb555')).read_bytes())
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
 def test_rock_sheet_and_intrinsic_controller_dependencies(self):
  data,assets=imp.build(REFERENCE);pack=json.loads(data);recipes={r['template']:r for r in pack['spriteRecipes']};rock=recipes['gFallingRockSpriteTemplate']
  self.assertEqual((32,32),(rock['width'],rock['height']));self.assertEqual([32,48,64],[a[0]['tile'] for a in rock['animations']]);self.assertEqual(struct.unpack('<HH',assets['rocks'][:4]),(32,192));self.assertNotIn('gShakeMonOrTerrainSpriteTemplate',recipes)
  for move in (88,157):
   root=next(r for r in pack['scripts'] if r['moveId']==move);self.assertIn('SE_M_ROCK_THROW',root['soundEffects']);controller=next(c for c in root['commands'] if c['op']=='createsprite' and c['args'][0]=='gShakeMonOrTerrainSpriteTemplate');self.assertEqual('1',controller['args'][-1])
 def test_snow_recipes_and_spike_source_palette(self):
  blob,assets=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(blob)['spriteRecipes']}
  self.assertEqual('AnimSwirlingSnowball',recipes['gSwirlingSnowballSpriteTemplate']['callback'])
  spike=recipes['gIceGroundSpikeSpriteTemplate'];self.assertEqual('AnimWaveFromCenterOfTarget',spike['callback']);self.assertEqual((8,16),(spike['width'],spike['height']));self.assertTrue(spike['blend'])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/ice_spikes.png') as image:indices=image.tobytes()
  palette=[int(c) for row in (REFERENCE/'graphics/battle_anims/sprites/ice_crystals.pal').read_text().splitlines()[3:] for c in row.split()]
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual((8,64),struct.unpack_from('<HH',assets['ice-spikes']));self.assertEqual(expected,list(struct.unpack_from('<512H',assets['ice-spikes'],4)))
 def test_music_roots_keep_dynamic_tags_and_shared_palette_tasks(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);rows={r['moveId']:r for r in pack['scripts']}
  for move in [47,187,320]:
   row=rows[move];self.assertTrue({'ANIM_TAG_MUSIC_NOTES','ANIM_TAG_BENT_SPOON','ANIM_TAG_SPHERE_TO_CUBE','ANIM_TAG_LARGE_FRESH_EGG'}<=set(row['spriteTags']))
   tasks=[c['args'][0] for c in row['commands'] if c['op']=='createvisualtask'];self.assertIn('AnimTask_MusicNotesRainbowBlend',tasks);self.assertIn('AnimTask_MusicNotesClearRainbowBlend',tasks)
  self.assertEqual({'BellyDrumRight','BellyDrumLeft'},{c['args'][0] for c in rows[187]['commands'] if c['op']=='call'})
 def test_music_affine_signed_literals_and_eight_direct_frame_variants(self):
  data,_=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']};r=recipes['gWavyMusicNotesSpriteTemplate']
  self.assertEqual(8,len(r['animations']));self.assertEqual([(12,12),(-12,-12)],[(c['x'],c['y']) for c in r['affineAnimations'][0][:2]])
  self.assertTrue(all(a[0]['vFlip'] for a in r['animations'][6:]));self.assertEqual('AnimBellyDrumHand',recipes['gBellyDrumHandSpriteTemplate']['callback'])
 def test_music_indexed_tiles_and_palette_match_every_source_pixel(self):
  data,blobs=imp.build(REFERENCE);rows=[r for r in json.loads(data)['indexedAssets'] if r['id'] in {'music-notes','purple-hand-outline'}];self.assertEqual(2,len(rows))
  for row in rows:
   name=row['id'];image=Image.open(REFERENCE/('graphics/battle_anims/sprites/'+name.replace('-','_')+'.png'));indices=image.tobytes();pal=image.getpalette();packed=blobs[name+'.4bpp'];colors=struct.unpack('<16H',blobs[name+'.pal16'])
   self.assertEqual(tuple(image.size),(row['width'],row['height']))
   for y in range(image.height):
    for x in range(image.width):
     address=((y//8)*(image.width//8)+x//8)*32+(y%8)*4+(x%8)//2;index=(packed[address]>>((x&1)*4))&15;self.assertEqual(indices[y*image.width+x],index);self.assertEqual(sum((pal[index*3+c]//8)<<(5*c) for c in range(3)),colors[index])
 def test_missing_music_callback_does_not_admit_partial_roots(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path);return data.replace(b'gWavyMusicNotesSpriteTemplate,',b'gUnreviewedMusicSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
  ids={r['moveId'] for r in json.loads(data)['scripts']};self.assertFalse({47,320}&ids);self.assertIn(187,ids)
 def test_conversion_roots_share_source_sheet_and_exact_pause_alpha_protocol(self):
  data,blobs=imp.build(REFERENCE);pack=json.loads(data);rows={r['moveId']:r for r in pack['scripts']};recipes={r['template']:r for r in pack['spriteRecipes']}
  for move,callback,tiles in [(160,'AnimConversion',[3,2,1,0]),(176,'AnimConversion2',[0,1,2,3])]:
   row=rows[move];sprites=[c for c in row['commands'] if c['op']=='createsprite'];self.assertEqual(16,len(sprites));r=recipes[sprites[0]['args'][0]]
   self.assertEqual((callback,8,8,True,True),(r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual(tiles,[f['tile'] for f in r['animations'][0]]);self.assertEqual(512,r['affineAnimations'][0][0]['x'])
   self.assertIn('ANIM_TAG_CONVERSION',row['spriteTags']);self.assertEqual({'SE_M_SWIFT','SE_M_BARRIER'},set(row['soundEffects']))
  self.assertEqual(list(range(60,136,5)),[int(c['args'][-1]) for c in rows[176]['commands'] if c['op']=='createsprite'])
  self.assertEqual(128,len(blobs['conversion.4bpp']));self.assertEqual(32,len(blobs['conversion.pal16']))
 def test_conversion_indexed_art_matches_original_source_every_pixel(self):
  _,blobs=imp.build(REFERENCE)
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/conversion.png') as image:
   self.assertEqual((8,32),image.size);indices=image.tobytes();palette=image.getpalette();packed=blobs['conversion.4bpp'];colors=struct.unpack('<16H',blobs['conversion.pal16'])
  for address,index in enumerate(indices):
   self.assertEqual(index,(packed[address//2]>>((address&1)*4))&15)
   self.assertEqual(sum((palette[index*3+c]//8)<<(5*c) for c in range(3)),colors[index])
 def test_conversion_missing_sprite_or_flash_task_excludes_incomplete_root(self):
  original=imp.read
  for before,after,excluded in [(b'gConversionSpriteTemplate,',b'gUnknownConversionSpriteTemplate,',{160}),(b'AnimTask_Conversion2AlphaBlend,',b'AnimTask_UnknownConversionBlend,',{176}),(b'AnimTask_FlashAnimTagWithColor,',b'AnimTask_UnknownFlash,',{160})]:
   def read(reference,path):
    data=original(reference,path);return data.replace(before,after) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp,'read',side_effect=read):data,_=imp.build(REFERENCE)
   self.assertFalse(excluded&{r['moveId'] for r in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
