#!/usr/bin/env python3
"""Pinned Smokescreen helper discovery, dynamic indexed resources and old-pack parity."""
import sys
sys.dont_write_bytecode=True
import argparse,io,json,struct,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
parser=argparse.ArgumentParser(add_help=False)
for name in ['root','reference','generated','previous','importer']:parser.add_argument('--'+name,type=Path)
a,rest=parser.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=(a.root or Path(__file__).resolve().parents[2]).resolve();REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations';PREVIOUS=a.previous or GENERATED
path=ROOT/'scripts/imports/battle/import-source-elemental-animations.py';imp=types.ModuleType('smokescreen_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
class SmokescreenImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_exact_root_discovery_has_one_raw_dependency_and_two_counted_templates(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_SmokescreenImpact'}):old,_=imp.discover_scripts(REFERENCE)
  old={r['moveId']:r for r in old};self.assertEqual({108},set(self.roots)-set(old))
  for key,value in old.items():self.assertEqual(value,self.roots[key])
  root=self.roots[108];self.assertEqual('Move_SMOKESCREEN',root['label']);self.assertEqual(['sSmokescreenImpactSpriteTemplate'],root['spriteDependencies']);self.assertEqual(55019,root['spriteTags']['TAG_SMOKESCREEN'])
  creates=[c['args'][0] for c in root['commands'] if c['op']=='createsprite'];self.assertEqual(['gBlackBallSpriteTemplate']+['gBlackSmokeSpriteTemplate']*12,creates)
  self.assertEqual([['AnimTask_SmokescreenImpact','2']],[c['args'] for c in root['commands'] if c['op']=='createvisualtask']);self.assertEqual([['ANIM_TAG_BLACK_SMOKE'],['ANIM_TAG_BLACK_BALL']],[c['args'] for c in root['commands'] if c['op']=='loadspritegfx'])
 def test_local_oam_recipe_and_all_four_source_frame_flips(self):
  r=self.recipes['sSmokescreenImpactSpriteTemplate'];self.assertEqual(('smokescreen-impact','SpriteCB_SmokescreenImpact',16,16),(r['asset'],r['callback'],r['width'],r['height']));self.assertFalse(r['blend'] or r['affineDoubleSize']);self.assertEqual([],r['affineAnimations']);self.assertEqual([-1]*4,r['loopStarts']);self.assertEqual([[dict(tile=t,duration=4,hFlip=bool(q&1),vFlip=bool(q&2)) for t in [0,4,8]] for q in range(4)],r['animations'])
  smoke=self.recipes['gBlackSmokeSpriteTemplate'];self.assertEqual(('black-smoke','AnimBlackSmoke',32,16),(smoke['asset'],smoke['callback'],smoke['width'],smoke['height']));self.assertEqual('AnimThrowProjectile',self.recipes['gBlackBallSpriteTemplate']['callback'])
 def test_indexed_pixels_are_exact_source_tiles_and_png_palette(self):
  indexed={r['id']:r for r in self.pack['indexedAssets']}
  for stem,size,tag in [('smokescreen_impact',(16,48),'TAG_SMOKESCREEN'),('black_smoke',(32,16),'ANIM_TAG_BLACK_SMOKE')]:
   with Image.open(REFERENCE/'graphics/battle_anims/sprites'/f'{stem}.png') as im:
    self.assertEqual(('P',size),(im.mode,im.size));rgb=im.getpalette()[:48];colors=[sum((rgb[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];indices=[im.getpixel((tx+x,ty+y)) for ty in range(0,im.height,8) for tx in range(0,im.width,8) for y in range(8) for x in range(8)];name=stem.replace('_','-')
    self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.pixels[name+'.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels[name+'.pal16']);self.assertEqual(tag,indexed[name]['paletteTag'])
 def test_source_lifetime_paths_and_registration_mutations_fail(self):
  read=imp.read
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_BlackSmoke, ANIM_TAG_BLACK_BALL}',b'{gBattleAnimSpritePal_BlackBall, ANIM_TAG_BLACK_BALL}'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_BlackSmoke, 0x0100,',b'{gBattleAnimSpriteGfx_BlackSmoke, 0x0080,'),('src/battle_anim_smokescreen.c',b'#define TAG_SMOKESCREEN 55019',b'#define TAG_SMOKESCREEN 55020'),('src/battle_anim_smokescreen.c',b'.size = 0x180',b'.size = 0x100'),('src/battle_anim_smokescreen.c',b'FreeSpriteTilesByTag(sSmokescreenImpactSpriteSheet.tag);',b'FreeSpriteTilesByTag(0);'),('src/battle_anim_smokescreen.c',b'.priority = 1,',b'.priority = 2,'),('src/graphics.c',b'smokescreen_impact.gbapal.lz',b'other.gbapal.lz'),('src/battle_anim_effects_3.c',b'BATTLER_COORD_Y_PIC_OFFSET) + 8,\n        FALSE);',b'BATTLER_COORD_Y_PIC_OFFSET) + 8,\n        TRUE);')]:
   self.assertIn(old,read(REFERENCE,path))
   def mutated(reference,name):
    data=read(reference,name);return data.replace(old,new) if name==path else data
   with self.subTest(path=path,old=old),patch.object(imp,'read',side_effect=mutated),self.assertRaises(ValueError):imp.discover_scripts(REFERENCE)
 def test_previous_pack_rows_recipes_and_pixel_bytes_are_unchanged(self):
  previous=json.loads((PREVIOUS/'elemental.json').read_bytes())
  for row in previous['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in previous['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for key,value in previous['subroutines'].items():self.assertEqual(value,self.pack['subroutines'][key])
  for group in ['assets','indexedAssets']:
   current={r['id']:r for r in self.pack[group]}
   for row in previous[group]:
    self.assertEqual(row,current[row['id']])
    for field in ['path','indicesPath','palettePath']:
     if field in row:self.assertEqual((PREVIOUS/row[field]).read_bytes(),self.pixels[row[field].removeprefix('elemental-').removesuffix('.rgb555')])
 def test_generated_outputs_match_the_explicit_conversion(self):
  self.assertEqual(self.data,(GENERATED/'elemental.json').read_bytes())
  for name,data in self.pixels.items():self.assertEqual(data,(GENERATED/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
