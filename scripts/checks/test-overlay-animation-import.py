#!/usr/bin/env python3
"""Exact indexed BG1 overlay materials, complete roots and raw-child dependencies."""
import importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
def module(filename,name):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/filename);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('import-source-elemental-animations.py','overlay_elemental')
bg=module('import-source-animation-backgrounds.py','overlay_background')
class OverlayImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  cls.files=bg.build(REFERENCE);cls.background=json.loads(cls.files['manifest.json']);cls.overlays={r['id']:r for r in cls.background['overlays']}
 def block(self,row,key):
  start,length=row[key];return self.files['atlas.bin'][start:start+length]
 def test_four_complete_scripts_keep_source_commands_and_real_material_lifetimes(self):
  for move,name,count in [(114,'HAZE',8),(137,'GLARE',16),(184,'SCARY_FACE',16),(296,'MIST_BALL',18)]:
   row=self.roots[move];self.assertEqual('Move_'+name,row['label']);self.assertEqual(count,len(row['commands']));self.assertEqual('end',row['commands'][-1]['op']);self.assertFalse(any(c['op']=='call' for c in row['commands']))
   self.assertEqual(['scary-face-opponent','scary-face-player'] if move in (137,184) else ['fog'],row['overlayDependencies'])
  self.assertEqual(['gGlareEyeDotSpriteTemplate'],self.roots[137]['spriteDependencies'])
  self.assertEqual({'ANIM_TAG_SMALL_BUBBLES','ANIM_TAG_WHITE_FEATHER'},set(self.roots[296]['spriteTags']))
  self.assertEqual(['white-feather'],self.roots[296]['materialDependencies'])
  self.assertEqual(1,sum(c['op']=='createsprite' and c['args'][0]=='gMistBallSpriteTemplate' for c in self.roots[296]['commands']))
  self.assertFalse(any(c['op']=='createsprite' and 'Feather' in c['args'][0] for c in self.roots[296]['commands']))
  for move in [114,296]:self.assertIn('SE_M_HAZE',self.roots[move]['soundEffects'])
 def test_dummy_sprite_frames_remain_lifetime_independent_and_unaffined(self):
  for name,asset,width,callback in [('gGlareEyeDotSpriteTemplate','small-red-eye',8,'AnimGlareEyeDot'),('gMistBallSpriteTemplate','small-bubbles',16,'AnimThrowMistBall')]:
   r=self.recipes[name];self.assertEqual((asset,width,width,callback,False,False),(r['asset'],r['width'],r['height'],r['callback'],r['blend'],r['affineDoubleSize']))
   self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([0],r['loopStarts']);self.assertEqual([],r['affineAnimations'])
 def test_overlay_pack_deduplicates_source_loads_without_flattening_or_map_rewrites(self):
  self.assertEqual({'schemaVersion','basePalette','terrains','moves','overlays'},set(self.background))
  self.assertTrue({'scary-face-player','scary-face-opponent','fog'}<=set(self.overlays))
  for row in self.overlays.values():
   size=row.get('screenSize',0);self.assertIn(size,(0,1));self.assertEqual({'id','tiles','tilemap','palette','paletteBank'}|({'screenSize'} if size else set()),set(row));self.assertEqual(8,row['paletteBank']);self.assertEqual(2048*(size+1),len(self.block(row,'tilemap')));self.assertEqual(32,len(self.block(row,'palette')))
   # Every screenblock entry must reference actual tiles and a source palette bank.
   entries=struct.unpack('<'+'H'*(len(self.block(row,'tilemap'))//2),self.block(row,'tilemap'))
   self.assertTrue(all((entry&1023)*32<len(self.block(row,'tiles')) and entry>>12 in (0,1,8) for entry in entries))
  for identity in ('surf-player','surf-opponent','muddy-water-player','muddy-water-opponent'):
   row=self.overlays[identity];self.assertEqual(1,row['screenSize'])
   self.assertEqual(bg.read(REFERENCE,'graphics/battle_anims/backgrounds/water_'+identity.rsplit('-',1)[1]+'.bin'),self.block(row,'tilemap'))
  a,b=self.overlays['scary-face-player'],self.overlays['scary-face-opponent'];self.assertEqual(a['tiles'],b['tiles']);self.assertEqual(a['palette'],b['palette']);self.assertNotEqual(a['tilemap'],b['tilemap'])
  self.assertEqual(8192,len(self.block(a,'tiles')));self.assertEqual(2048,len(self.block(self.overlays['fog'],'tiles')))
  for id,stem in [('scary-face-player','scary_face_player'),('scary-face-opponent','scary_face_opponent'),('fog','fog')]:self.assertEqual(bg.read(REFERENCE,'graphics/battle_anims/backgrounds/'+stem+'.bin'),self.block(self.overlays[id],'tilemap'))
  entries=struct.unpack('<1024H',self.block(a,'tilemap'));self.assertEqual({0,1,8},{v>>12 for v in entries});self.assertEqual(75,max(v&1023 for v in entries));self.assertEqual({8},{v>>12 for v in entries[:640]})
 def test_every_tile_nibble_and_both_palette_sources_match_source_bytes(self):
  for identity,path in [('scary-face-player','graphics/battle_anims/backgrounds/scary_face.png'),('fog','graphics/weather/fog_horizontal.png')]:
   row=self.overlays[identity];tiles=self.block(row,'tiles')
   with Image.open(io.BytesIO(bg.read(REFERENCE,path))) as image:
    count=image.width*image.height//64
    for tile in range(count):
     for y in range(8):
      for x in range(8):self.assertEqual(image.getpixel((tile%(image.width//8)*8+x,tile//(image.width//8)*8+y)),tiles[tile*32+y*4+x//2]>>(4*(x%2))&15)
    self.assertEqual(bytes(len(tiles)-count*32),tiles[count*32:]);colors=image.getpalette()[:48]
   if identity=='fog':colors=[int(x) for line in bg.read(REFERENCE,'graphics/weather/default.pal').decode().splitlines()[3:] for x in line.split()]
   expected=struct.pack('<16H',*[sum((colors[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])
   self.assertEqual(expected,self.block(row,'palette'))
 def test_red_eye_and_unused_feather_keep_indexed_palette_tags(self):
  indexed={r['id']:r for r in self.pack['indexedAssets']}
  for stem,shape in [('small_red_eye',(8,8)),('white_feather',(32,64))]:
   name=stem.replace('_','-');row=indexed[name];self.assertEqual(('ANIM_TAG_'+stem.upper(),*shape),(row['paletteTag'],row['width'],row['height']))
   with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/'+stem+'.png'))) as image:
    values=[i for y in range(0,image.height,8) for x in range(0,image.width,8) for i in image.crop((x,y,x+8,y+8)).tobytes()];colors=image.getpalette()[:48]
   self.assertEqual(bytes(values[i]|values[i+1]<<4 for i in range(0,len(values),2)),self.pixels[name+'.4bpp'])
   self.assertEqual(struct.pack('<16H',*[sum((colors[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),self.pixels[name+'.pal16'])
 def test_missing_callbacks_and_changed_raw_child_reject_incomplete_roots(self):
  tasks=imp.TASKS-{'AnimTask_ScaryFace','AnimTask_HazeScrollingFog','AnimTask_MistBallFog','AnimTask_GlareEyeDots'}
  with patch.object(imp,'TASKS',tasks):data,pixels=imp.build(REFERENCE)
  before=json.loads(data);old={r['moveId']:r for r in before['scripts']};self.assertEqual({114,137,184,296},set(self.roots)-set(old));self.assertGreater(len(old),50)
  for id,row in old.items():self.assertEqual(row,self.roots[id])
  for key,data in pixels.items():self.assertEqual(data,self.pixels[key])
  original=imp.read
  def changed(ref,path):
   data=original(ref,path);return data.replace(b'CreateSprite(&gGlareEyeDotSpriteTemplate,',b'CreateSprite(&gUnknownEyeSpriteTemplate,') if path=='src/battle_anim_effects_3.c' else data
  with patch.object(imp,'read',side_effect=changed):
   with self.assertRaisesRegex(ValueError,'Glare raw child'):imp.build(REFERENCE)
 def test_source_hardware_software_alias_and_fog_direct_load_contracts_fail_closed(self):
  original=bg.read
  for target,old,new in [('src/battle_intro.c',b'((struct BgCnt *)&sBgCnt)->charBaseBlock = value;',b'SetBgAttribute(bgId, BG_ATTR_CHARBASEINDEX, value);'),('src/bg.c',b'offset = destOffset + offset;',b'offset = destOffset;'),('src/battle_anim_ice.c',b'gWeatherFogHorizontalTiles, 0x800,',b'gWeatherFogHorizontalTiles, 0x400,')]:
   def changed(ref,path):
    data=original(ref,path);return data.replace(old,new) if path==target else data
   with patch.object(bg,'read',side_effect=changed):
    with self.assertRaises(ValueError):bg.build(REFERENCE)
if __name__=='__main__':unittest.main()
