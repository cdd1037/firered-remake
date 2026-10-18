#!/usr/bin/env python3
"""Four complete source closures, shared indexed leaves, and original affine/palette ABI."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('orbital_physical','import-source-physical-animations.py')
elemental=module('orbital_elemental','import-source-elemental-animations.py')
background=module('orbital_background','import-source-animation-backgrounds.py')
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class OrbitalParticleImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads(imp.build(REFERENCE)[0]);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_four_complete_roots_retain_every_original_command_and_audio_dependency(self):
  self.assertTrue({237,310,327,345}<=self.rows.keys());source=self.raw['data/battle_anim_scripts.s'].decode()
  for move in [237,310,327,345]:
   row=self.rows[move];body=source.split(row['label']+':\n',1)[1].split('\n\tend',1)[0];commands=[]
   for line in body.splitlines():
    line=line.split('@',1)[0].strip()
    if not line:continue
    op,*tail=line.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];commands.append((op,args))
   commands.append(('end',[]));self.assertEqual(commands,[(c['op'],c['args']) for c in row['commands']]);self.assertEqual('end',row['commands'][-1]['op']);self.assertTrue(row['soundEffects'])
  self.assertEqual(17,self.pack['backgroundSymbols']['BG_IN_AIR'])
 def test_source_templates_affine_program_and_magical_colors_are_exact(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for template,callback,double in [('gHiddenPowerOrbSpriteTemplate','AnimOrbitFast',False),('gHiddenPowerOrbScatterSpriteTemplate','AnimOrbitScatter',True)]:
   r=recipes[template];self.assertEqual(('red-orb',callback,16,16,False,double),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([dict(op='frame',x=128,y=128,rotation=0,duration=0,target=0),dict(op='frame',x=8,y=8,rotation=0,duration=1,target=0),dict(op='jump',x=0,y=0,rotation=0,duration=0,target=1)],r['affineAnimations'][0])
  spray=recipes['gSprayWaterDropletSpriteTemplate'];self.assertEqual(('sweat-bead','AnimSprayWaterDroplet',8,8,[]),(spray['asset'],spray['callback'],spray['width'],spray['height'],spray['affineAnimations']))
  program=self.pack['taskAffinePrograms']['sAffineAnims_StretchBattlerUp'];self.assertEqual([('frame',10,-13,10),('frame',-10,13,10),('end',0,0,0)],[(c['op'],c['x'],c['y'],c['duration']) for c in program])
  raw=dict(self.raw);raw['src/battle_anim_effects_1.c']=raw['src/battle_anim_effects_1.c'].replace(b'RGB(31, 19, 0)',b'RGB(31, 18, 0)')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaisesRegex(ValueError,'Magical Leaf'):imp.build(REFERENCE)
 def test_new_indexed_pixels_use_registered_palette_alias_and_tile_order(self):
  metadata,pixels=imp.build_indexed_assets(REFERENCE);rgb=imp.build_assets(REFERENCE);rows={r['id']:r for r in metadata}
  for name,size,pal in [('red-orb',(16,16),'red_orb'),('sweat-bead',(8,8),'splash')]:
   image=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+name.replace('-','_')+'.png']));palette=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+pal+'.png'])).getpalette();indices=list(image.tobytes());ordered=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
   self.assertEqual(size,image.size);self.assertEqual(bytes(ordered[i]|ordered[i+1]<<4 for i in range(0,len(ordered),2)),pixels[name+'.4bpp']);self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)],list(struct.unpack('<16H',pixels[name+'.pal16'])));self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3))+(32768 if i else 0) for i in indices],list(struct.unpack_from('<'+str(len(indices))+'H',rgb[name],4)));self.assertEqual('ANIM_TAG_'+name.upper().replace('-','_'),rows[name]['paletteTag'])
 def test_shared_leaf_indexed_upgrade_has_one_owner_and_identical_rgb_pixels(self):
  pack,pixels=elemental.build(REFERENCE);pack=json.loads(pack);indexed={r['id']:r for r in pack['indexedAssets']}
  self.assertFalse({'leaf','razor-leaf'}&{r['id'] for r in self.pack['assets']})
  for name,size in [('leaf',(16,144)),('razor-leaf',(32,16))]:
   image=Image.open(REFERENCE/'graphics/battle_anims/sprites'/str(name.replace('-','_')+'.png'));palette=image.getpalette();indices=list(image.tobytes());self.assertEqual(size,(indexed[name]['width'],indexed[name]['height']));self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3))+(32768 if i else 0) for i in indices],list(struct.unpack_from('<'+str(len(indices))+'H',pixels[name],4)));self.assertEqual(16,len(pixels[name+'.pal16'])//2)
 def test_unknown_dependencies_or_retained_argument_abi_leave_whole_root_unsupported(self):
  for move,old,new in [(237,b'createsprite gHiddenPowerOrbSpriteTemplate,',b'createsprite UnknownOrb,'),(310,b'createsprite gSprayWaterDropletSpriteTemplate,',b'createsprite UnknownSpray,'),(327,b'createvisualtask AnimTask_MoveSkyUppercutBg, 5, 55',b'createvisualtask AnimTask_MoveSkyUppercutBg, 5'),(345,b'createvisualtask AnimTask_CycleMagicalLeafPal, 5',b'createvisualtask UnknownLeafPalette, 5')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']})
 def test_wrong_source_palette_alias_registration_or_background_tag_fails_closed(self):
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Splash, ANIM_TAG_SWEAT_BEAD}',b'{gBattleAnimSpritePal_RedOrb, ANIM_TAG_SWEAT_BEAD}'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_RedOrb, 0x0080, ANIM_TAG_RED_ORB}',b'{gBattleAnimSpriteGfx_RedOrb, 0x0100, ANIM_TAG_RED_ORB}'),('src/graphics.c',b'"graphics/battle_anims/sprites/splash.gbapal.lz"',b'"graphics/battle_anims/sprites/red_orb.gbapal.lz"')]:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
  self.assertIn('BG_IN_AIR',background.MOVE_BACKGROUNDS);files=background.build(REFERENCE);self.assertTrue(files)
if __name__=='__main__':unittest.main()
