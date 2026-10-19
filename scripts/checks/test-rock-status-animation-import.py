#!/usr/bin/env python3
"""Four complete pinned roots, reused rocks, indexed palette aliases and ABI closure."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
from indexed_material_expectations import decode_indexed
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('rock_status_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class RockStatusAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_complete_roots_retain_every_source_command(self):
  source=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move,count in [(50,14),(197,13),(246,31),(317,30)]:
   row=self.rows[move];self.assertEqual(count,len(row['commands']));self.assertTrue(row['soundEffects'])
   for c in row['commands']:
    text=source[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
 def test_recipes_retain_source_shape_frames_and_callbacks(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for name,file,callback in imp.ROCK_STATUS_RECIPES:
   r=recipes[name];self.assertEqual(callback,r['callback']);self.assertFalse(r['blend']);self.assertFalse(r['affineDoubleSize']);self.assertEqual([],r['affineAnimations'])
   rock='RockSpriteTemplate' in name
   self.assertEqual(64 if 'RedX' in name else 32,r['width']);self.assertEqual(r['width'],r['height'])
   self.assertEqual([[i*16] for i in range(6)] if rock else [[0,16,32,48,64]] if 'Sparkle' in name else [[0]],[[f['tile'] for f in a] for a in r['animations']])
   self.assertEqual([0] if 'RedX' in name else [-1]*(6 if rock else 1),r['loopStarts'])
  self.assertEqual('elemental-rocks.rgb555',next(r['path'] for r in self.pack['assets'] if r['id']=='rocks'))
  self.assertEqual('walls-sparkle-4.rgb555',next(r['path'] for r in self.pack['assets'] if r['id']=='sparkle-4'))
  self.assertNotIn('sparkle-4',{r['id'] for r in self.pack['indexedAssets']})
 def test_indexed_art_uses_registered_palette_aliases(self):
  metadata,pixels=imp.build_indexed_assets(REFERENCE)
  for stem,symbol,size,shape,pal_symbol,pal_stem in imp.ROCK_STATUS_MATERIALS:
   name=stem.replace('_','-');im=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png']));pal=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+pal_stem+'.png'])).getpalette();ordered=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()]
   self.assertEqual(shape,im.size);self.assertEqual(size,len(pixels[name+'.4bpp']));self.assertEqual(bytes(ordered[i]|ordered[i+1]<<4 for i in range(0,len(ordered),2)),pixels[name+'.4bpp'])
   colors=[sum((pal[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(colors,list(struct.unpack('<16H',pixels[name+'.pal16'])));self.assertEqual([(colors[i]|32768) if i else 0 for i in im.tobytes()],list(decode_indexed(im.width,im.height,pixels[name+'.4bpp'],pixels[name+'.pal16'])))
  walls=imp.module('rock_status_walls',ROOT/'scripts/imports/battle/import-source-wall-animations.py');_,shared=walls.build(REFERENCE)
  source=imp.hit.rgb555(self.raw['graphics/battle_anims/sprites/sparkle_4.png'],(32,160),imp.reaction_palette(self.raw,'sparkle_3'))
  self.assertEqual(source,shared['walls-sparkle-4.rgb555'])
 def test_missing_callbacks_and_retained_arguments_reject_complete_root(self):
  mutations=[(50,b'createvisualtask AnimTask_GrowAndGrayscale, 5',b'createvisualtask AnimTask_GrowAndGrayscale, 5, 0'),(197,b'createsprite gSpinningSparkleSpriteTemplate, ANIM_ATTACKER, 13, 20, -20',b'createsprite gSpinningSparkleSpriteTemplate, ANIM_ATTACKER, 13, 20'),(246,b'createsprite gAncientPowerRockSpriteTemplate, ANIM_ATTACKER, 2, 20, 32, -48, 50, 2',b'createsprite gAncientPowerRockSpriteTemplate, ANIM_ATTACKER, 2, 20, 32, -48, 50'),(317,b'createsprite gRedXSpriteTemplate, ANIM_TARGET, 5, ANIM_TARGET, 50',b'createsprite UnknownRedX, ANIM_TARGET, 5, ANIM_TARGET, 50')]
  for move,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']});self.assertIn(move,{r['moveId'] for r in blocked})
 def test_source_registration_palette_alias_and_callback_fail_closed(self):
  mutations=[('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_XSign, 0x0800, ANIM_TAG_X_SIGN}',b'{gBattleAnimSpriteGfx_XSign, 0x0400, ANIM_TAG_X_SIGN}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Sparkle3, ANIM_TAG_SPARKLE_4}',b'{gBattleAnimSpritePal_Sparkle4, ANIM_TAG_SPARKLE_4}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_HollowOrb, ANIM_TAG_X_SIGN}',b'{gBattleAnimSpritePal_XSign, ANIM_TAG_X_SIGN}'),('src/battle_anim_rock.c',b'.callback = AnimRockTomb,',b'.callback = AnimRockTomb_Step,')]
  for path,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):
     imp.build(REFERENCE) if path.startswith('src/battle_anim_') else imp.build_indexed_assets(REFERENCE)
if __name__=='__main__':unittest.main()
