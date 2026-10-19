#!/usr/bin/env python3
"""Focused source graphs, shared callback ABI, registered prefix pixels and BG bindings."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from indexed_material_expectations import decode_indexed
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
class ItemWindImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'physical.json').read_text());cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']};cls.importer=module('item_wind_importer',ROOT/'scripts/imports/battle/import-source-physical-animations.py');cls.source=(REFERENCE/'data/battle_anim_scripts.s').read_text()
 def test_exact_source_graphs_and_boolean_branch_preserve_source_direction(self):
  lines=self.source.splitlines()
  for move in [271,278,318]:
   root=self.roots[move]
   for c in root['commands']:
    op,*tail=lines[c['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op in {'jumprettrue','jumpretfalse'}:args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
    self.assertEqual((op,args),(c['op'],c['args']))
  wind=self.roots[318];self.assertEqual(['7','1','SilverWindOnPlayer'],next(c['args'] for c in wind['commands'] if c['op']=='jumpargeq'));self.assertEqual(18,sum(c['op']=='createsprite' for c in wind['commands']));self.assertEqual(['speed-dust'],self.roots[271]['materialDependencies'])
 def test_complete_background_roots_have_one_canonical_catalog_owner(self):
  background=json.loads((GENERATED/'background-scripts.json').read_text())['scripts'];self.assertTrue(background)
  owned={row['moveId'] for row in background};self.assertEqual(len(background),len(owned));self.assertTrue(set(self.roots).isdisjoint(owned),'Complete physical/background programs must not publish duplicate roots')
  self.assertEqual(len(self.pack['scripts']),len(self.roots))
 def test_recipes_are_direct_source_and_use_one_flying_particle_callback(self):
  for name,file,callback in self.importer.ITEM_WIND_RECIPES:
   source=(REFERENCE/'src'/file).read_text();self.assertEqual(self.importer.elemental.recipe(source,name,callback),self.recipes[name])
   with self.assertRaises(ValueError):self.importer.elemental.recipe(source.replace('.callback = '+callback+',','.callback = UnreviewedCallback,'),name,callback)
  for size in ['Big','Medium','Small']:self.assertEqual('AnimFlyingParticle',self.recipes['gSilverWind'+size+'SparkSpriteTemplate']['callback'])
  self.assertEqual(self.recipes['gAromatherapyBigFlowerSpriteTemplate']['callback'],self.recipes['gSilverWindBigSparkSpriteTemplate']['callback'])
 def test_attacker_stretch_is_exact_shared_target_stretch_with_participant_substitution(self):
  source=(REFERENCE/'src/battle_anim_effects_2.c').read_text()
  def body(name):return re.search(r'void '+name+r'\(u8 taskId\)\s*\{(.*?)^\}',source,re.M|re.S)[1]
  self.assertEqual(body('AnimTask_StretchTargetUp').replace('ANIM_TARGET','ANIM_ATTACKER'),body('AnimTask_StretchAttackerUp'))
  query=re.search(r'void AnimTask_GetTargetSide\(u8 taskId\)\s*\{(.*?)^\}',(REFERENCE/'src/battle_anim_utility_funcs.c').read_text(),re.M|re.S)[1]
  self.assertEqual('gBattleAnimArgs[7]=GetBattlerSide(gBattleAnimTarget);DestroyAnimVisualTask(taskId);',re.sub(r'\s+','',query))
 def test_every_registered_pixel_index_palette_and_prefix_matches_source(self):
  for stem,symbol,count,shape in self.importer.ITEM_WIND_MATERIALS:
   row=next(r for r in self.pack['indexedAssets'] if r['id']==stem.replace('_','-'));size=(shape[0],count*2//shape[0])
   with Image.open(REFERENCE/'graphics/battle_anims/sprites'/f'{stem}.png') as im:
    self.assertEqual(('P',shape),(im.mode,im.size));palette=im.getpalette()[:48];indices=list(im.crop((0,0,*size)).tobytes());tiled=[im.getpixel((x+dx,y+dy)) for y in range(0,size[1],8) for x in range(0,size[0],8) for dy in range(8) for dx in range(8)]
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual('ANIM_TAG_'+stem.upper(),row['paletteTag']);self.assertEqual(size,(row['width'],row['height']));self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),(GENERATED/row['indicesPath']).read_bytes());self.assertEqual(struct.pack('<16H',*colors),(GENERATED/row['palettePath']).read_bytes());self.assertEqual(tuple((colors[i]|32768) if i else 0 for i in indices),decode_indexed(*size,(GENERATED/row['indicesPath']).read_bytes(),(GENERATED/row['palettePath']).read_bytes()))
 def test_source_registration_rejects_changed_sheet_size_and_preserves_full_sheets(self):
  raw=self.importer.read_sources(REFERENCE);metadata,files=self.importer.utility_materials(raw,self.importer.ITEM_WIND_MATERIALS);registration=raw['src/data/battle_anim.h']
  def assert_full_sheet(stem,shape,rows,blobs):
   name=stem.replace('_','-');row=next(r for r in rows if r['id']==name);self.assertEqual(shape,(row['width'],row['height']))
   with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+stem+'.png'])) as image:palette=image.getpalette()[:48];raster=list(image.tobytes())
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
   self.assertEqual(tuple((colors[i]|32768) if i else 0 for i in raster),decode_indexed(*shape,blobs[name+'.4bpp'],blobs[name+'.pal16']))
  for stem,symbol,size,shape in self.importer.ITEM_WIND_MATERIALS:
   pattern=rb'(\{gBattleAnimSpriteGfx_'+symbol.encode()+rb',\s*)0x[0-9a-fA-F]+';bad={**raw,'src/data/battle_anim.h':re.sub(pattern,rb'\g<1>0x0010',registration)}
   with self.assertRaises(ValueError):self.importer.utility_materials(bad,self.importer.ITEM_WIND_MATERIALS)
   if stem!='sparkle_6':assert_full_sheet(stem,shape,metadata,files)
  for group in [self.importer.UTILITY_MATERIALS,self.importer.FOCUS_SOUND_MATERIALS]:
   old_rows,old_files=self.importer.utility_materials(raw,group)
   for stem,symbol,size,shape in group:assert_full_sheet(stem,shape,old_rows,old_files)
 def test_bug_background_reuses_highspeed_tiles_and_opposite_maps_with_only_its_palette(self):
  source=(REFERENCE/'src/data/battle_anim.h').read_text();constants=(REFERENCE/'include/constants/battle_anim.h').read_text();graphics=(REFERENCE/'src/graphics.c').read_text()
  for side in ['Opponent','Player']:
   row=re.search(r'\[BG_BUG_'+side.upper()+r'\]\s*=\s*\{([^}]+)\}',source)[1]
   self.assertEqual(['gBattleAnimBgImage_Highspeed','gBattleAnimBgPalette_Bug','gBattleAnimBgTilemap_Highspeed'+side],[v.strip() for v in row.split(',') if v.strip()])
   self.assertIn('BG_BUG_'+side.upper(),self.pack['backgroundSymbols'])
  self.assertIn('"graphics/battle_anims/backgrounds/bug.gbapal.lz"',graphics)
if __name__=='__main__':unittest.main()
