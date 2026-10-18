#!/usr/bin/env python3
"""Weather Ball closes five native branches with one new canonical material."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('weather_ball_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class WeatherBallImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.raw=imp.read_sources(REFERENCE);cls.pack=json.loads(imp.build(REFERENCE)[0]);cls.row=next(r for r in cls.pack['scripts'] if r['moveId']==311)
 def test_original_five_branch_graph_and_ice_helper_are_complete(self):
  row=self.row;self.assertEqual('Move_WEATHER_BALL',row['label']);self.assertLessEqual(len(row['commands']),128)
  branches=['WeatherBallNormal','WeatherBallFire','WeatherBallWater','WeatherBallSandstorm','WeatherBallIce']
  self.assertEqual(branches,list(row['labels']));self.assertEqual([['7',str(i),label] for i,label in enumerate(branches)],[c['args'] for c in row['commands'] if c['op']=='jumpargeq'])
  self.assertEqual(5,sum(c['op']=='end' for c in row['commands']));self.assertEqual(['IceCrystalEffectShort'],[c['args'][0] for c in row['commands'] if c['op']=='call'])
  helper=self.pack['subroutines']['IceCrystalEffectShort'];sprites=[c for c in helper if c['op']=='createsprite']
  self.assertEqual([['gIceCrystalHit'+size+'SpriteTemplate','ANIM_TARGET','2',str(x),str(y),'0'] for size,x,y in [('Large',-10,-10),('Small',10,20),('Large',-5,10),('Small',17,-12),('Small',-15,15),('Small',0,0),('Large',20,2)]],[c['args'] for c in sprites])
  self.assertEqual(7,sum(c['op']=='playsewithpan' for c in helper));self.assertEqual([['4']]*6,[c['args'] for c in helper if c['op']=='delay']);self.assertEqual('return',helper[-1]['op'])
  self.assertEqual(['AnimTask_GetWeather','2'],next(c['args'] for c in row['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_GetWeather'))
  self.assertEqual(10044,row['commands'][0]['line']);self.assertEqual(10139,row['commands'][-1]['line']);self.assertNotIn('programReferences',row)
 def test_only_weather_root_is_new_and_prior_rows_and_material_bytes_are_preserved(self):
  names=set(imp.WEATHER_BALL_WIDTHS)
  with patch.object(imp,'SPRITES',imp.SPRITES-names),patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_GetWeather'}),patch.object(imp,'NEW',imp.NEW-names-{'AnimTask_GetWeather'}):old=json.loads(imp.build(REFERENCE)[0])
  before={r['moveId']:r for r in old['scripts']};after={r['moveId']:r for r in self.pack['scripts']};self.assertEqual({311},set(after)-set(before));self.assertEqual(before,{k:v for k,v in after.items() if k!=311})
  assets=imp.build_assets(REFERENCE);indexed,files=imp.build_indexed_assets(REFERENCE)
  with patch.object(imp,'WEATHER_BALL_MATERIALS',[]):old_assets=imp.build_assets(REFERENCE);old_indexed,old_files=imp.build_indexed_assets(REFERENCE)
  self.assertEqual({'weather-ball'},set(assets)-set(old_assets));self.assertEqual(old_assets,{k:v for k,v in assets.items() if k!='weather-ball'});self.assertEqual(old_indexed,[r for r in indexed if r['id']!='weather-ball']);self.assertEqual(old_files,{k:v for k,v in files.items() if not k.startswith('weather-ball.')})
 def test_six_source_recipes_keep_pointer_slice_and_shared_material_owners(self):
  recipes={r['template']:r for r in imp.weather_ball_recipes(self.raw)};self.assertEqual(6,len(recipes))
  for kind,asset,size in [('Up','weather-ball',32),('NormalDown','weather-ball',32),('FireDown','small-ember',32),('WaterDown','small-bubbles',16),('RockDown','rocks',32),('IceDown','hail',16)]:
   r=recipes['gWeatherBall'+kind+'SpriteTemplate'];self.assertEqual(asset,r['asset']);self.assertEqual((size,size),(r['width'],r['height']));self.assertFalse(r['blend']);self.assertFalse(r['affineDoubleSize']);self.assertEqual('AnimWeatherBallUp' if kind=='Up' else 'AnimWeatherBallDown',r['callback'])
  rock=recipes['gWeatherBallRockDownSpriteTemplate'];self.assertEqual([32,48,64,80],[a[0]['tile'] for a in rock['animations']]);self.assertEqual([-1]*4,rock['loopStarts']);self.assertEqual(2,len(rock['affineAnimations']))
  water=recipes['gWeatherBallWaterDownSpriteTemplate'];self.assertEqual(4,water['animations'][0][0]['tile']);self.assertEqual(336,water['affineAnimations'][0][0]['x']);self.assertEqual(15,water['affineAnimations'][0][1]['duration'])
  indexed={r['id']:r for r in self.pack['indexedAssets']};self.assertIn('weather-ball',indexed)
  for name in ['small-ember','small-bubbles','rocks','hail']:self.assertNotIn(name,indexed)
  elemental=json.loads((ROOT/'content/generated/battle/animations/elemental.json').read_text());owners={r['id'] for r in elemental['assets']}
  self.assertTrue({'small-ember','small-bubbles','rocks','hail'}<=owners)
 def test_new_indexed_material_is_exact_registered_png_palette_and_tile_order(self):
  assets,indexed,files=imp.utility_materials(self.raw,imp.WEATHER_BALL_MATERIALS);self.assertEqual(['weather-ball'],list(assets));self.assertEqual(1,len(indexed));row=indexed[0];self.assertEqual(('ANIM_TAG_WEATHER_BALL',32,32),(row['paletteTag'],row['width'],row['height']))
  with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/weather_ball.png'])) as image:
   self.assertEqual('P',image.mode);self.assertEqual((32,32),image.size);colors=image.getpalette()[:48];indices=[v for y in range(0,32,8) for x in range(0,32,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
  self.assertEqual(512,len(files['weather-ball.4bpp']));self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,1024,2)),files['weather-ball.4bpp']);self.assertEqual(struct.pack('<16H',*[sum((colors[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),files['weather-ball.pal16'])
  self.assertEqual(imp.registered_sprite_pixels(self.raw,'gBattleAnimSpriteGfx_WeatherBall','weather_ball'),assets['weather-ball'])
 def test_source_callback_controller_mask_precedence_and_enum_mutations_fail_closed(self):
  imp.validate_weather_ball_sources(self.raw)
  cases=[('src/battle_anim_mons.c',b'sprite->data[1] = -40;',b'sprite->data[1] = -39;'),('src/battle_anim_mons.c',b'x = (u16)gBattleAnimArgs[4] + 30;',b'x = (u16)gBattleAnimArgs[4] + 31;'),('src/battle_anim_effects_3.c',b'if (gWeatherMoveAnim & B_WEATHER_SUN)',b'if (gWeatherMoveAnim & B_WEATHER_RAIN)'),('include/constants/battle_anim.h',b'#define ANIM_WEATHER_SUN 1',b'#define ANIM_WEATHER_SUN 5'),('include/constants/battle.h',b'#define B_WEATHER_SUN_TEMPORARY       (1 << 5)',b'#define B_WEATHER_SUN_TEMPORARY       (1 << 4)')]
  for path,old,new in cases:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with self.assertRaises(ValueError):imp.validate_weather_ball_sources(raw)
  raw=dict(self.raw);raw['src/battle_anim_rock.c']=raw['src/battle_anim_rock.c'].replace(b'.anims = &sAnims_BasicRock[2],',b'.anims = &sAnims_BasicRock[1],')
  with self.assertRaises(ValueError):imp.weather_ball_recipes(raw)
 def test_incomplete_abi_and_unknown_branch_callback_are_not_admitted(self):
  for old,new in [(b'createvisualtask AnimTask_GetWeather, 2',b'createvisualtask AnimTask_GetWeather, 2, 0'),(b'gWeatherBallNormalDownSpriteTemplate, ANIM_TARGET, 2, -30, -100, 25, 1, 0, 0',b'gWeatherBallNormalDownSpriteTemplate, ANIM_TARGET, 2, -30, -100, 25, 1, 0'),(b'gWeatherBallIceDownSpriteTemplate, ANIM_TARGET',b'gUnknownWeatherIceSpriteTemplate, ANIM_TARGET')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(311,{r['moveId'] for r in json.loads(data)['scripts']});self.assertTrue(any(r['moveId']==311 for r in blocked))
if __name__=='__main__':unittest.main()
