#!/usr/bin/env python3
"""Pinned squash roots, raw-child ownership and exact shared material conversion."""
import argparse,io,json,re,struct,sys,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
from indexed_material_expectations import assert_source_pixels
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);p.add_argument('--previous',type=Path);p.add_argument('--importer',type=Path)
a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=a.root;REF=a.reference or ROOT/'content/raw/pokefirered/037335f4';OUT=a.generated or ROOT/'content/generated/battle/animations';canonical=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
imp=types.ModuleType('squash_import');imp.__file__=str(canonical);exec(compile((a.importer or canonical).read_text(),str(canonical),'exec'),imp.__dict__)
class SquashWaterFireImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(REF);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_complete_source_roots_and_exact_operands(self):
  lines=imp.read(REF,'data/battle_anim_scripts.s').decode().splitlines()
  for move in (284,323):
   rows=self.roots[move]['commands'];self.assertTrue(rows);self.assertEqual('end',rows[-1]['op'])
   for c in rows:
    op,*tail=lines[c['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
   for file in ('physical.json','background-scripts.json'):
    other=json.loads((ROOT/'content/generated/battle/animations'/file).read_text());self.assertNotIn(move,{r['moveId'] for r in other['scripts']})
 def test_explicit_raw_dependencies_and_source_recipe_abi(self):
  self.assertEqual(['gEruptionLaunchRockSpriteTemplate'],self.roots[284]['spriteDependencies'])
  self.assertEqual(['gSmallWaterOrbSpriteTemplate','gWaterHitSplatSpriteTemplate'],self.roots[323]['spriteDependencies'])
  for name,asset,callback,size in [('gEruptionLaunchRockSpriteTemplate','warm-rock','AnimEruptionLaunchRock',16),('gEruptionFallingRockSpriteTemplate','warm-rock','AnimEruptionFallingRock',32),('gSmallWaterOrbSpriteTemplate','glowy-blue-orb','AnimSmallWaterOrb',8)]:
   r=self.recipes[name];self.assertEqual((asset,callback,size,size),(r['asset'],r['callback'],r['width'],r['height']));self.assertEqual([0],r['loopStarts']);self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([],r['affineAnimations'])
  self.assertEqual(128,self.recipes['gWaterHitSplatSpriteTemplate']['affineAnimations'][3][0]['x'])
 def test_material_pixels_indices_palette_and_shape_match_source(self):
  for name,png,size,blue in [('warm-rock','warm_rock',(32,168),False),('glowy-blue-orb','glowy_red_orb',(8,8),True)]:
   with Image.open(io.BytesIO(imp.read(REF,'graphics/battle_anims/sprites/'+png+'.png'))) as im:
    self.assertEqual(size,im.size);raster=list(im.tobytes());indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];palette=im.getpalette()[:48]
   if blue:palette=[int(v) for line in imp.read(REF,'graphics/battle_anims/sprites/glowy_blue_orb.pal').decode().splitlines()[3:] for v in line.split()]
   colors=[sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)]
   self.assertEqual(struct.pack('<16H',*colors),self.pixels[name+'.pal16']);self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.pixels[name+'.4bpp'])
   assert_source_pixels(self,self.pack,self.pixels,name,size,raster,colors,'ANIM_TAG_'+name.upper().replace('-','_'))
   for suffix in ('.4bpp','.pal16'):self.assertEqual(self.pixels[name+suffix],(OUT/('elemental-'+name+suffix)).read_bytes())
 def test_unreviewed_raw_children_and_affine_handoff_fail_closed(self):
  mutations=[('src/battle_anim_fire.c','CreateSprite(&gEruptionLaunchRockSpriteTemplate,','CreateSprite(&gEruptionFallingRockSpriteTemplate,'),('src/battle_anim_water.c','CreateSprite(&gSmallWaterOrbSpriteTemplate,','CreateSprite(&gUnknownOrbSpriteTemplate,'),('src/battle_anim_water.c','StartSpriteAffineAnim(&gSprites[sprite->data[1]], 3);','StartSpriteAffineAnim(&gSprites[sprite->data[1]], 2);')]
  read=imp.read
  for path,old,new in mutations:
   original=read(REF,path);self.assertIn(old.encode(),original);changed=original.replace(old.encode(),new.encode())
   with patch.object(imp,'read',side_effect=lambda ref,p:changed if p==path else read(ref,p)):
    with self.assertRaisesRegex(ValueError,'Squash raw|Water Spout'):imp.discover_scripts(REF)
 def test_sheet_size_palette_alias_and_callback_identity_fail_closed(self):
  mutations=[('src/data/battle_anim.h','{gBattleAnimSpriteGfx_WarmRock, 0x0a80, ANIM_TAG_WARM_ROCK}','{gBattleAnimSpriteGfx_WarmRock, 0x0800, ANIM_TAG_WARM_ROCK}'),('src/data/battle_anim.h','{gBattleAnimSpritePal_GlowyBlueOrb, ANIM_TAG_GLOWY_BLUE_ORB}','{gBattleAnimSpritePal_GlowyRedOrb, ANIM_TAG_GLOWY_BLUE_ORB}'),('src/battle_anim_fire.c','.callback = AnimEruptionLaunchRock,','.callback = AnimEruptionFallingRock,')]
  read=imp.read
  for path,old,new in mutations:
   original=read(REF,path);self.assertIn(old.encode(),original);changed=original.replace(old.encode(),new.encode())
   with patch.object(imp,'read',side_effect=lambda ref,p:changed if p==path else read(ref,p)):
    with self.assertRaisesRegex(ValueError,'Squash source registration|Elemental recipe differs'):imp.build(REF)
 def test_regeneration_and_optional_prior_ownership_are_exact(self):
  self.assertEqual(self.data,(OUT/'elemental.json').read_bytes())
  if a.previous:
   old=json.loads((a.previous/'elemental.json').read_text());old_roots={r['moveId']:r for r in old['scripts']};self.assertEqual({284,323},self.roots.keys()-old_roots.keys())
   for move,row in old_roots.items():self.assertEqual(row,self.roots[move])
   for key,field in [('spriteRecipes','template'),('assets','id'),('indexedAssets','id')]:
    current={r[field]:r for r in self.pack[key]}
    for row in old[key]:self.assertEqual(row,current[row[field]])
   for row in old['assets']+old['indexedAssets']:
    for key,path in row.items():
     if key=='path' or key.endswith('Path'):self.assertEqual((a.previous/path).read_bytes(),(OUT/path).read_bytes())
if __name__=='__main__':unittest.main()
