"""Frozen cube's literal four-part source sheet, palette, raw child and root closure."""
import argparse,importlib.util,io,json,struct,sys,unittest
from pathlib import Path
from indexed_material_expectations import decode_indexed
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--raw-overlay',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
path=ROOT/'scripts/imports/battle/import-source-physical-animations.py';spec=importlib.util.spec_from_file_location('frozen_physical',path);physical=importlib.util.module_from_spec(spec);exec(compile((a.candidate/path.name if a.candidate else path).read_text(),str(path),'exec'),physical.__dict__)
def read(path):
 overlay=a.raw_overlay/path if a.raw_overlay else None
 return (overlay if overlay and overlay.exists() else RAW/path).read_bytes()
INPUTS=['src/battle_anim_status_effects.c','src/data/battle_anim.h','src/graphics.c','graphics_file_rules.mk','graphics/battle_anims/sprites/ice_cube.pal']+['graphics/battle_anims/sprites/ice_cube_'+str(i)+'.png' for i in range(4)]
def raw():return {p:read(p) for p in INPUTS}
def independent():
 colors=[tuple(map(int,line.split())) for line in read(INPUTS[4]).decode().splitlines()[3:]];palette=[(r//8)|((g//8)<<5)|((b//8)<<10) for r,g,b in colors];tiles=[]
 for i in range(4):
  with Image.open(io.BytesIO(read(INPUTS[5+i]))) as image:
   for row in range(image.height//8):
    for col in range(image.width//8):tiles.append([image.getpixel((col*8+x,row*8+y)) for y in range(8) for x in range(8)])
 indices=sum(tiles,[]);packed=bytes(indices[i]+16*indices[i+1] for i in range(0,len(indices),2));raster=[tiles[(y//8)*8+x//8][(y%8)*8+x%8] for y in range(144) for x in range(64)];visible=tuple((palette[v]|32768) if v else 0 for v in raster)
 return visible,packed,struct.pack('<16H',*palette)
class FrozenCubeImportTests(unittest.TestCase):
 def test_literal_subsprite_recipe_and_task_registration(self):
  recipe=physical.frozen_cube_recipe(raw());self.assertEqual('ice-cube',recipe['asset']);self.assertEqual('SpriteCallbackDummy',recipe['callback']);self.assertEqual([(64,64,0),(64,32,64),(32,64,96),(32,32,128)],[(p['width'],p['height'],p['tileOffset']) for p in recipe['subsprites']]);self.assertIn('AnimTask_FrozenIceCube',physical.TASKS);self.assertIn('AnimTask_FrozenIceCube',physical.NEW)
 def test_all_concatenated_indices_palette_and_visible_pixels_match(self):
  indexed,files=physical.frozen_cube_materials(raw());visible,packed,palette=independent();self.assertEqual(visible,decode_indexed(64,144,files['ice-cube.4bpp'],files['ice-cube.pal16']));self.assertEqual(packed,files['ice-cube.4bpp']);self.assertEqual(palette,files['ice-cube.pal16']);self.assertEqual(4608,len(packed));self.assertEqual([dict(id='ice-cube',width=64,height=144,indicesPath='physical-ice-cube.4bpp',palettePath='physical-ice-cube.pal16',paletteTag='ANIM_TAG_ICE_CUBE')],indexed)
 def test_source_shape_linkage_and_concat_mutations_fail(self):
  for path,old,new,method in [('src/battle_anim_status_effects.c',b'.tileOffset = 128',b'.tileOffset = 127',physical.frozen_cube_recipe),('src/battle_anim_status_effects.c',b'SetSubspriteTables(&gSprites[spriteId], sFrozenIceCubeSubspriteTable);',b'SetSubspriteTables(&gSprites[spriteId], NULL);',physical.frozen_cube_recipe),('src/data/battle_anim.h',b'gBattleAnimSpriteGfx_IceCube, 0x1200',b'gBattleAnimSpriteGfx_IceCube, 0x1000',physical.frozen_cube_materials),('src/graphics.c',b'ice_cube.gbapal.lz',b'ice_crystals.gbapal.lz',physical.frozen_cube_materials),('graphics_file_rules.mk',b'ice_cube_3.4bpp',b'ice_cube_0.4bpp',physical.frozen_cube_materials)]:
   data=raw();self.assertIn(old,data[path]);data[path]=data[path].replace(old,new)
   with self.assertRaises(ValueError):method(data)
  data=raw();image=Image.new('P',(32,32));stream=io.BytesIO();image.save(stream,format='PNG');data[INPUTS[5]]=stream.getvalue()
  with self.assertRaises(ValueError):physical.frozen_cube_materials(data)
 def test_actual_generated_root_raw_child_and_every_material_byte(self):
  folder=ROOT/'content/generated/battle/animations';pack=json.loads((folder/'physical.json').read_text());roots={r['moveId']:r for r in pack['scripts']};self.assertIn(329,roots,'Actual producer did not close Sheer Cold');row=roots[329];self.assertEqual(['sFrozenIceCubeSpriteTemplate'],row['spriteDependencies']);self.assertEqual([['AnimTask_FrozenIceCube','2']],[c['args'] for c in row['commands'] if c['op']=='createvisualtask']);self.assertIn('ANIM_TAG_ICE_CUBE',row['spriteTags']);self.assertEqual(physical.frozen_cube_recipe(raw()),next(r for r in pack['spriteRecipes'] if r['template']=='sFrozenIceCubeSpriteTemplate'))
  visible,packed,palette=independent()
  for name,data in [('physical-ice-cube.4bpp',packed),('physical-ice-cube.pal16',palette)]:self.assertEqual(data,(folder/name).read_bytes(),name)
  self.assertEqual(visible,decode_indexed(64,144,(folder/'physical-ice-cube.4bpp').read_bytes(),(folder/'physical-ice-cube.pal16').read_bytes()))
  source=read('data/battle_anim_scripts.s').decode().split('Move_SHEER_COLD:\n',1)[1].split('\nMove_',1)[0];expected=[]
  for line in source.splitlines():
   line=line.split('@')[0].strip()
   if not line:continue
   op,*tail=line.split(None,1);expected.append((op,[s.strip() for s in tail[0].split(',')] if tail else []))
  self.assertEqual(expected,[(c['op'],c['args']) for c in row['commands']])
if __name__=='__main__':unittest.main()
