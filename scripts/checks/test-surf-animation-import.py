"""Focused Surf indexed 512x256 overlays, source operands and real root closure."""
import argparse,importlib.util,io,json,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);candidate=a.candidate/Path(path).name if a.candidate else ROOT/path;exec(compile(candidate.read_text(),str(ROOT/path),'exec'),m.__dict__);return m
background=module('surf_background','scripts/imports/battle/import-source-animation-backgrounds.py');physical=module('surf_physical','scripts/imports/battle/import-source-physical-animations.py')
def read(path):return (RAW/path).read_bytes()
class SurfImportTests(unittest.TestCase):
 def test_source_owner_shape_and_alternate_palette_are_literal(self):
  self.assertEqual(['surf-player','surf-opponent','muddy-water-player','muddy-water-opponent'],background.surf_overlay_inputs(read));self.assertIn('AnimTask_CreateSurfWave',physical.TASKS);self.assertIn('AnimTask_CreateSurfWave',physical.NEW)
  tiles,pal=background.indexed_tiles(read('graphics/battle_anims/backgrounds/water.png'));self.assertLessEqual(len(tiles),8192);mud=background.palette(read('graphics/battle_anims/backgrounds/water_muddy.pal'));self.assertEqual(32,len(pal));self.assertEqual(32,len(mud));self.assertNotEqual(pal,mud)
  for side in ['player','opponent']:
   data=read('graphics/battle_anims/backgrounds/water_'+side+'.bin');self.assertEqual(4096,len(data));entries=struct.unpack('<2048H',data)
   self.assertTrue(all((entry&1023)*32<len(tiles) and entry>>12 in (0,1,8) for entry in entries))
 def test_source_material_and_screen_size_mutations_fail(self):
  for path,old,new in [('src/graphics.c',b'water.4bpp.lz',b'water_muddy.4bpp.lz'),('src/graphics.c',b'water_player.bin.lz',b'water_opponent.bin.lz'),('src/battle_anim_water.c',b'SetAnimBgAttribute(1, BG_ANIM_SCREEN_SIZE, 1);',b'SetAnimBgAttribute(1, BG_ANIM_SCREEN_SIZE, 0);'),('src/battle_anim_water.c',b'LoadCompressedPalette(gBattleAnimBgPalette_MuddyWater,',b'LoadCompressedPalette(gBattleAnimBgPalette_Surf,')]:
   data=read(path);self.assertIn(old,data)
   with self.assertRaises(ValueError):background.surf_overlay_inputs(lambda p:data.replace(old,new) if p==path else read(p))
 def test_actual_generated_roots_and_every_indexed_material_byte_match_source(self):
  folder=ROOT/'content/generated/battle/animations';pack=json.loads((folder/'physical.json').read_text());roots={r['moveId']:r for r in pack['scripts']};manifest=json.loads((folder/'backgrounds/manifest.json').read_text());atlas=(folder/'backgrounds/atlas.bin').read_bytes();overlays={r['id']:r for r in manifest['overlays']}
  tiles,pal=background.indexed_tiles(read('graphics/battle_anims/backgrounds/water.png'));tiles=tiles.ljust(8192,b'\0');mud=background.palette(read('graphics/battle_anims/backgrounds/water_muddy.pal'))
  for move,family,arg in [(57,'surf','FALSE'),(330,'muddy-water','TRUE')]:
   self.assertIn(move,roots,'Actual producer did not close Surf family');self.assertEqual([family+'-player',family+'-opponent'],roots[move]['overlayDependencies']);self.assertEqual([['AnimTask_CreateSurfWave','2',arg]],[c['args'] for c in roots[move]['commands'] if c['op']=='createvisualtask'])
   for side in ['player','opponent']:
    row=overlays[family+'-'+side];self.assertEqual(1,row['screenSize']);self.assertEqual(8,row['paletteBank'])
    for name,expected in [('tiles',tiles),('tilemap',read('graphics/battle_anims/backgrounds/water_'+side+'.bin')),('palette',pal if move==57 else mud)]:
     offset,length=row[name];self.assertEqual(expected,atlas[offset:offset+length],family+' '+side+' '+name)
if __name__=='__main__':unittest.main()
