#!/usr/bin/env python3
"""Complete weather roots, literal subsprite tables and indexed palette ownership."""
import importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
def module(filename,name):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/filename);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('import-source-elemental-animations.py','weather_elemental')
bg=module('import-source-animation-backgrounds.py','weather_background')
class WeatherImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  cls.files=bg.build(REFERENCE);cls.background=json.loads(cls.files['manifest.json']);cls.sand=next(r for r in cls.background['overlays'] if r['id']=='sandstorm')
 def block(self,key):
  start,length=self.sand[key];return self.files['atlas.bin'][start:start+length]
 def test_complete_four_roots_preserve_shared_helpers_and_raw_child_dependencies(self):
  for move,name in [(201,'SANDSTORM'),(240,'RAIN_DANCE'),(241,'SUNNY_DAY'),(257,'HEAT_WAVE')]:
   r=self.roots[move];self.assertEqual('Move_'+name,r['label']);self.assertEqual('end',r['commands'][-1]['op'])
  for move,flag in [(201,'FALSE'),(257,'TRUE')]:
   r=self.roots[move];self.assertEqual(['sandstorm'],r['overlayDependencies']);sprites=[c for c in r['commands'] if c['op']=='createsprite'];self.assertEqual(7,len(sprites));self.assertEqual({'gFlyingSandCrescentSpriteTemplate'},{c['args'][0] for c in sprites})
   self.assertIn(dict(op='createvisualtask',args=['AnimTask_LoadSandstormBackground','5',flag],line=6459 if move==201 else 3533),r['commands'])
  rain=self.roots[240];self.assertEqual(['gRainDropSpriteTemplate'],rain['spriteDependencies']);self.assertEqual(2,sum(c['op']=='createvisualtask' and c['args']==['AnimTask_CreateRaindrops','2','0','3','120'] for c in rain['commands']))
  sun=self.roots[241];self.assertEqual(4,sum(c['op']=='call' and c['args']==['SunnyDayLightRay'] for c in sun['commands']));self.assertIn('SE_M_PETAL_DANCE',sun['soundEffects']);self.assertEqual(['SE_M_PETAL_DANCE','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','+1','0'],next(c['args'] for c in sun['commands'] if c['op']=='panse_adjustnone'))
  self.assertEqual('gSunlightRaySpriteTemplate',self.pack['subroutines']['SunnyDayLightRay'][0]['args'][0])
 def test_source_subsprite_layout_retains_native_tile_offsets_and_table_priority(self):
  r=self.recipes['gFlyingSandCrescentSpriteTemplate'];self.assertEqual((32,16,False,[]),(r['width'],r['height'],r['blend'],r['affineAnimations']))
  self.assertEqual([dict(x=-16,y=0,width=32,height=16,tileOffset=0,priority=1),dict(x=16,y=0,width=32,height=16,tileOffset=8,priority=1)],r['subsprites'])
 def test_frame_and_affine_programs_preserve_exact_source_timings(self):
  r=self.recipes['gRainDropSpriteTemplate'];self.assertEqual([0,8,16,24,32,40,48],[f['tile'] for f in r['animations'][0]]);self.assertEqual([2,2,2,6,2,2,2],[f['duration'] for f in r['animations'][0]]);self.assertEqual([-1],r['loopStarts']);self.assertEqual([],r['affineAnimations'])
  s=self.recipes['gSunlightRaySpriteTemplate'];self.assertTrue(s['blend']);self.assertFalse(s['affineDoubleSize']);self.assertEqual([('frame',80,80,0,0,0),('frame',2,2,10,1,0),('jump',0,0,0,0,1)],[tuple(c[k] for k in ['op','x','y','rotation','duration','target']) for c in s['affineAnimations'][0]])
 def test_three_indexed_materials_match_every_source_nibble_and_palette_entry(self):
  indexed={r['id']:r for r in self.pack['indexedAssets']}
  for stem,shape in [('flying_dirt',(32,32)),('rain_drops',(16,224)),('sunlight',(32,32))]:
   name=stem.replace('_','-');r=indexed[name];self.assertEqual(('ANIM_TAG_'+stem.upper(),*shape),(r['paletteTag'],r['width'],r['height']))
   with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/'+stem+'.png'))) as image:
    values=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];colors=image.getpalette()[:48]
   self.assertEqual(bytes(values[i]|values[i+1]<<4 for i in range(0,len(values),2)),self.pixels[name+'.4bpp']);self.assertEqual(struct.pack('<16H',*[sum((colors[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),self.pixels[name+'.pal16'])
 def test_sand_overlay_uses_flying_dirt_palette_and_zero_padded_compressed_gfx(self):
  self.assertEqual(8,self.sand['paletteBank']);self.assertEqual(8192,len(self.block('tiles')));self.assertEqual(2048,len(self.block('tilemap')));self.assertEqual(32,len(self.block('palette')))
  with Image.open(io.BytesIO(bg.read(REFERENCE,'graphics/battle_anims/backgrounds/sandstorm_brew.png'))) as image:
   values=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
  packed=bytes(values[i]|values[i+1]<<4 for i in range(0,len(values),2));self.assertEqual(packed+bytes(8192-len(packed)),self.block('tiles'));self.assertEqual(self.pixels['flying-dirt.pal16'],self.block('palette'));self.assertEqual(bg.read(REFERENCE,'graphics/battle_anims/backgrounds/sandstorm_brew.bin'),self.block('tilemap'));self.assertEqual({8},{v>>12 for v in struct.unpack('<1024H',self.block('tilemap'))})
 def test_removing_new_callbacks_opens_exactly_four_roots_without_changing_old_rows(self):
  names={'gFlyingSandCrescentSpriteTemplate','gRainDropSpriteTemplate','gSunlightRaySpriteTemplate'}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in names}),patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_LoadSandstormBackground','AnimTask_BlendBackground','AnimTask_MoveHeatWaveTargets','AnimTask_CreateRaindrops'}):data,pixels=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({201,240,241,257},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for key,data in pixels.items():self.assertEqual(data,self.pixels[key])
 def test_wrong_raw_child_subsprite_geometry_and_registered_palette_fail_closed(self):
  original=imp.read
  for target,old,new,reason in [('src/battle_anim_water.c',b'CreateSprite(&gRainDropSpriteTemplate,',b'CreateSprite(&gUnknownDropTemplate,','Rain raw child'),('src/battle_anim_rock.c',b'.tileOffset = 8,',b'.tileOffset = 9,','Sand crescent source subsprite'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_FlyingDirt, ANIM_TAG_FLYING_DIRT}',b'{gBattleAnimSpritePal_FlyingDirt, ANIM_TAG_SUNLIGHT}','Source indexed registration')]:
   def changed(ref,path):
    data=original(ref,path)
    if path==target:self.assertIn(old,data);return data.replace(old,new)
    return data
   with patch.object(imp,'read',side_effect=changed):
    with self.assertRaisesRegex(ValueError,reason):imp.build(REFERENCE)
 def test_overlay_palette_declaration_must_be_the_literal_source_dependency(self):
  original=bg.read
  def changed(ref,path):
   data=original(ref,path);return data.replace(b'LoadCompressedPalette(gBattleAnimSpritePal_FlyingDirt,',b'LoadCompressedPalette(gBattleAnimSpritePal_Sunlight,') if path=='src/battle_anim_rock.c' else data
  with patch.object(bg,'read',side_effect=changed):
   with self.assertRaisesRegex(ValueError,'Sandstorm BG1'):bg.build(REFERENCE)
if __name__=='__main__':unittest.main()
