#!/usr/bin/env python3
"""Encore/Flatter complete discovery, exact materials, and prior-row preservation."""
import sys
sys.dont_write_bytecode=True
import argparse,hashlib,io,json,struct,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);p.add_argument('--previous',type=Path);p.add_argument('--importer',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=a.root.resolve();a.reference=a.reference or ROOT/'content/raw/pokefirered/037335f4';a.generated=a.generated or ROOT/'content/generated/battle/animations';path=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
# The override supports external integration staging while resolving all shared
# producer dependencies from the selected checkout. No importer is duplicated.
imp=types.ModuleType('spotlight_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
NAMES={'gSpotlightSpriteTemplate','gFlatterSpotlightSpriteTemplate','gClappingHandSpriteTemplate','gClappingHand2SpriteTemplate','gFlatterConfettiSpriteTemplate'}
class SpotlightImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_exact_two_closed_roots_and_prior_definitions_preserved(self):
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):old_data,old_pixels=imp.build(a.reference)
  old=json.loads(old_data);roots={r['moveId']:r for r in old['scripts']};self.assertEqual({227,260},set(self.roots)-set(roots))
  for key,value in roots.items():self.assertEqual(value,self.roots[key])
  recipes={r['template']:r for r in old['spriteRecipes']};self.assertEqual(NAMES,set(self.recipes)-set(recipes))
  for key,value in recipes.items():self.assertEqual(value,self.recipes[key])
  self.assertEqual(old_pixels,self.pixels)
 def test_authored_calls_sound_dependencies_and_window_fades(self):
  encore=self.roots[227];flatter=self.roots[260];self.assertEqual('Move_ENCORE',encore['label']);self.assertEqual('Move_FLATTER',flatter['label'])
  self.assertEqual({'SE_M_ENCORE2':216,'SE_M_ENCORE':215},encore['soundEffects']);self.assertEqual({'SE_M_ENCORE2':216,'SE_M_FLATTER':222},flatter['soundEffects'])
  self.assertEqual(5,sum(c['op']=='createsprite' for c in encore['commands']));self.assertEqual(19,sum(c['op']=='call' and c['args']==['CreateFlatterConfetti'] for c in flatter['commands']))
  helper=self.pack['subroutines']['CreateFlatterConfetti'];self.assertEqual(['createsprite','createsprite','return'],[c['op'] for c in helper]);self.assertEqual([['gFlatterConfettiSpriteTemplate','ANIM_ATTACKER','40','ANIM_ATTACKER'],['gFlatterConfettiSpriteTemplate','ANIM_ATTACKER','40','ANIM_TARGET']],[c['args'] for c in helper[:-1]])
  for root in [encore,flatter]:
   self.assertEqual([['AnimTask_HardwarePaletteFade','2','248','3','0','10','0'],['AnimTask_HardwarePaletteFade','2','248','3','10','0','1']],[c['args'] for c in root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_HardwarePaletteFade'])
   self.assertEqual('AnimTask_RemoveSpotlight',root['commands'][-2]['args'][0]);self.assertEqual('end',root['commands'][-1]['op'])
 def test_missing_shared_callback_keeps_both_roots_closed(self):
  for task in ['AnimTask_CreateSpotlight','AnimTask_RemoveSpotlight','AnimTask_HardwarePaletteFade','SoundTask_PlaySE2WithPanning']:
   with patch.object(imp,'TASKS',imp.TASKS-{task}):rows,_=imp.discover_scripts(a.reference)
   self.assertFalse({227,260}&{r['moveId'] for r in rows})
  with patch.object(imp,'SHARED_SPRITES',imp.SHARED_SPRITES-{'gVerticalDipSpriteTemplate'}):rows,_=imp.discover_scripts(a.reference)
  self.assertNotIn(260,{r['moveId'] for r in rows});self.assertIn(227,{r['moveId'] for r in rows})
 def test_exact_affine_double_size_masks_and_hand_crop(self):
  for name in NAMES:
   r=self.recipes[name];mask=name in {'gSpotlightSpriteTemplate','gFlatterSpotlightSpriteTemplate'};hand='ClappingHand' in name
   self.assertEqual(64 if mask else 32 if hand else 8,r['width']);self.assertEqual(r['width'],r['height']);self.assertEqual(mask,r['affineDoubleSize']);self.assertFalse(r['blend']);self.assertEqual([0],r['loopStarts'])
   self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations'])
   if mask:self.assertEqual([[(0,384,0),(16,0,20),(0,0,0)],[(320,384,0),(-16,0,19),(0,0,0)]],[[(c['x'],c['y'],c['duration']) for c in row] for row in r['affineAnimations']])
   else:self.assertEqual([],r['affineAnimations'])
 def test_material_indices_rgb555_and_external_spotlight_palette(self):
  for asset,shape,palette_name in [('spotlight',(64,64),'pokeball'),('confetti',(8,96),'confetti')]:
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+asset+'.png'))) as source,Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+palette_name+'.png'))) as palette:
    self.assertEqual(shape,source.size);p=palette.getpalette()[:48];colors=[sum((p[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(shape,struct.unpack_from('<HH',self.pixels[asset]));self.assertEqual([colors[i]|(32768 if i else 0) for i in source.tobytes()],list(struct.unpack_from('<'+'H'*(shape[0]*shape[1]),self.pixels[asset],4)))
    tiled=[source.getpixel((tx+x,ty+y)) for ty in range(0,shape[1],8) for tx in range(0,shape[0],8) for y in range(8) for x in range(8)];self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),self.pixels[asset+'.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels[asset+'.pal16'])
 def test_corrupted_callbacks_registration_and_callback_sound_reject(self):
  original=imp.read
  for source,old,new in [('src/battle_anim_effects_3.c',b'.callback = AnimSpotlight,',b'.callback = Other,'),('src/battle_anim_effects_3.c',b'.callback = AnimFlatterConfetti,',b'.callback = Other,'),('src/battle_anim_effects_3.c',b'PlaySE1WithPanning(SE_M_ENCORE,',b'PlaySE2WithPanning(SE_M_ENCORE,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Spotlight, 0x0800,',b'{gBattleAnimSpriteGfx_Spotlight, 0x0100,'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Pokeball, ANIM_TAG_SPOTLIGHT}',b'{gBattleAnimSpritePal_Confetti, ANIM_TAG_SPOTLIGHT}')]:
   self.assertIn(old,original(a.reference,source))
   def read(reference,path):
    data=original(reference,path);return data.replace(old,new) if path==source else data
   with self.subTest(source=source,old=old),patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(a.reference)
 def test_existing_roots_and_materials_remain_byte_identical(self):
  old=json.loads(((a.previous or a.generated)/'elemental.json').read_bytes())
  for row in old['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for key,value in old['subroutines'].items():self.assertEqual(value,self.pack['subroutines'][key])
  for group in ['assets','indexedAssets']:
   current={r['id']:r for r in self.pack[group]}
   for row in old[group]:
    self.assertEqual(row,current[row['id']])
    for field in ['path','indicesPath','palettePath']:
     if field in row:self.assertEqual(((a.previous or a.generated)/row[field]).read_bytes(),self.pixels[row[field].removeprefix('elemental-').removesuffix('.rgb555')])
  self.assertEqual(self.data,(a.generated/'elemental.json').read_bytes())
  for name,blob in self.pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
