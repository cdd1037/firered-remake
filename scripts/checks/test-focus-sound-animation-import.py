"""Focused source recipes/materials and callback-sound contracts; no full producer."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from indexed_material_expectations import decode_indexed
from unittest.mock import patch
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);candidate=a.candidate/Path(path).name if a.candidate else ROOT/path;exec(compile(candidate.read_text(),str(ROOT/path),'exec'),m.__dict__);return m
physical=module('focus_physical','scripts/imports/battle/import-source-physical-animations.py');general=module('focus_general','scripts/imports/battle/import-source-general-animations.py')
class FocusSoundImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.raw=physical.read_sources(RAW);cls.assets=physical.utility_materials(cls.raw,physical.FOCUS_SOUND_MATERIALS)
 def test_every_source_sprite_and_affine_recipe_remains_literal(self):
  expected={'gOpeningEyeSpriteTemplate':(32,32,3,0),'gWhiteHaloSpriteTemplate':(64,64,1,0),'gTealAlertSpriteTemplate':(32,32,1,0),'gLockOnTargetSpriteTemplate':(32,32,1,0),'gLockOnMoveTargetSpriteTemplate':(16,16,1,0),'gSnoreZSpriteTemplate':(32,32,1,0),'gJaggedMusicNoteSpriteTemplate':(32,32,1,0),'gUproarRingSpriteTemplate':(64,64,1,2)}
  for name,file,callback in physical.FOCUS_SOUND_RECIPES:
   row=physical.elemental.recipe(self.raw['src/'+file].decode(),name,callback);self.assertEqual(expected[name],(row['width'],row['height'],len(row['animations'][0]),len(row['affineAnimations'])));self.assertEqual(callback,row['callback'])
  program=physical.task_affine_program(self.raw['src/battle_anim_effects_2.c'].decode(),'sUproarAffineAnimCmds');self.assertEqual([(-12,8,4),(20,-20,4),(-8,12,4),(0,0,0)],[(c['x'],c['y'],c['duration']) for c in program])
 def test_all_new_indexed_source_pixels_and_palettes_match(self):
  for stem,symbol,size,shape in physical.FOCUS_SOUND_MATERIALS:
   png=self.raw['graphics/battle_anims/sprites/'+stem+'.png']
   with Image.open(io.BytesIO(png)) as im:indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];pal=im.getpalette()[:48];raster=list(im.tobytes())
   name=stem.replace('_','-');self.assertEqual(size,len(self.assets[1][name+'.4bpp']));self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.assets[1][name+'.4bpp']);self.assertEqual(struct.pack('<16H',*[sum((pal[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),self.assets[1][name+'.pal16']);self.assertEqual(tuple((sum((pal[3*i+c]//8)<<(5*c) for c in range(3))|32768) if i else 0 for i in raster),decode_indexed(*shape,self.assets[1][name+'.4bpp'],self.assets[1][name+'.pal16']))
 def test_thin_ring_canonical_indices_and_palette_match_every_source_pixel(self):
  read=lambda p:(RAW/p).read_bytes();rows,blobs=general.thin_ring_indexed(read);self.assertEqual(['thin-ring'],[x['id'] for x in rows]);self.assertEqual((64,64),(rows[0]['width'],rows[0]['height']));self.assertEqual(2048,len(blobs['general-thin-ring.4bpp']));self.assertEqual(32,len(blobs['general-thin-ring.pal16']))
  with Image.open(io.BytesIO(read('graphics/battle_anims/sprites/thin_ring.png'))) as image:
   palette=image.getpalette()[:48];raster=list(image.tobytes());tiles=[v for y in range(0,64,8) for x in range(0,64,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
  colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
  self.assertEqual(bytes(tiles[i]|tiles[i+1]<<4 for i in range(0,len(tiles),2)),blobs['general-thin-ring.4bpp']);self.assertEqual(struct.pack('<16H',*colors),blobs['general-thin-ring.pal16']);self.assertEqual(tuple((colors[i]|32768) if i else 0 for i in raster),decode_indexed(64,64,blobs['general-thin-ring.4bpp'],blobs['general-thin-ring.pal16']))
  for name,data in blobs.items():self.assertEqual(data,(ROOT/'content/generated/battle/animations'/name).read_bytes())
  for path,old,new in [('src/data/battle_anim.h',b'gBattleAnimSpriteGfx_ThinRing, 0x0800',b'gBattleAnimSpriteGfx_ThinRing, 0x0400'),('src/graphics.c',b'thin_ring.gbapal.lz',b'opening_eye.gbapal.lz')]:
   source=read(path);self.assertIn(old,source)
   with self.assertRaises(ValueError):general.thin_ring_indexed(lambda p:source.replace(old,new) if p==path else read(p))
 def test_callback_sounds_are_source_dependencies_and_mutations_fail(self):
  commands=[dict(op='createsprite',args=['gLockOnTargetSpriteTemplate','ANIM_ATTACKER','40'])];one=self.raw['src/battle_anim_effects_1.c'].decode();two=self.raw['src/battle_anim_effects_2.c'].decode();result=physical.physical_sound_effects(RAW,commands,two,one);self.assertEqual({'SE_M_LOCK_ON','SE_M_LEER'},result.keys())
  for sound in ['SE_M_LOCK_ON','SE_M_LEER']:
   with self.assertRaises(ValueError):physical.physical_sound_effects(RAW,commands,two,one.replace(sound,'SE_M_HORN_ATTACK'))
 def test_malformed_source_registration_and_palette_or_shape_fail(self):
  for file,old,new in [('src/data/battle_anim.h','gBattleAnimSpriteGfx_LockOn, 0x0280','gBattleAnimSpriteGfx_LockOn, 0x0200'),('src/graphics.c','jagged_music_note.gbapal.lz','music_notes_2.gbapal.lz')]:
   raw=dict(self.raw);self.assertIn(old,raw[file].decode());raw[file]=raw[file].decode().replace(old,new).encode()
   with self.assertRaises(ValueError):physical.utility_materials(raw,physical.FOCUS_SOUND_MATERIALS)
  for mode,size,color in [('P',(8,8),0),('RGB',(32,32),0),('P',(32,32),16)]:
   raw=dict(self.raw);buf=io.BytesIO();im=Image.new(mode,size,color)
   if mode=='P':im.putpalette(list(range(256))*3)
   im.save(buf,format='PNG');raw['graphics/battle_anims/sprites/teal_alert.png']=buf.getvalue()
   with self.assertRaises(ValueError):physical.utility_materials(raw,physical.FOCUS_SOUND_MATERIALS)
if __name__=='__main__':unittest.main()
