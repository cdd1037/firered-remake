import importlib.util,json,struct,unittest
from pathlib import Path
from PIL import Image
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('wall_import',ROOT/'scripts/imports/battle/import-source-wall-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=imp.ROOT/'content/raw/pokefirered/037335f4'
class WallImportTests(unittest.TestCase):
 def test_committed_runtime_pack_and_assets_regenerate(self):
  data,blobs=imp.build(REFERENCE);folder=ROOT/'content/generated/battle/animations';self.assertEqual(data,(folder/'walls.json').read_bytes())
  for name,blob in blobs.items():self.assertEqual(blob,(folder/name).read_bytes())
 def test_complete_source_roots_and_palette_tags(self):
  data,blobs=imp.build(REFERENCE);pack=json.loads(data)
  self.assertEqual({112,113,115,243,277},{s['moveId'] for s in pack['scripts']})
  self.assertEqual({'ANIM_TAG_'+c.upper()+'_LIGHT_WALL' for c in imp.WALLS.values()},{a['paletteTag'] for a in pack['indexedAssets']})
  self.assertEqual(1,len({a['indicesPath'] for a in pack['indexedAssets']}));self.assertEqual(7,len(pack['spriteRecipes']))
  for s in pack['scripts']:
   for c in s['commands']:
    if c['op']=='createsprite':self.assertIn(c['args'][5],s['spriteTags']) if c['args'][0] in imp.WALLS else None
 def test_packed_indices_retain_exact_source_tile_identity(self):
  _,blobs=imp.build(REFERENCE);data=blobs['walls-light-wall.4bpp'];self.assertEqual(2048,len(data));indices=[n for b in data for n in (b&15,b>>4)]
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/green_light_wall.png') as im:
   expected=[i for y in range(0,64,8) for x in range(0,64,8) for i in im.crop((x,y,x+8,y+8)).tobytes()];palette=im.getpalette()[:48]
  self.assertEqual(expected,indices)
  colors=struct.unpack('<16H',blobs['walls-green-light-wall.pal16']);self.assertEqual(tuple(sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)),colors);self.assertLess(len(set(colors)),16)
 def test_shared_sparkle_palette_and_source_frames(self):
  data,blobs=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(data)['spriteRecipes']}
  self.assertEqual([(0,3),(16,3),(32,3),(48,3),(64,3)],[(f['tile'],f['duration']) for f in recipes['gReflectSparkleSpriteTemplate']['animations'][0]])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/sparkle_3.png') as im:palette=im.getpalette()
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/sparkle_4.png') as im:indices=im.tobytes()
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
  self.assertEqual(expected,list(struct.unpack_from('<5120H',blobs['walls-sparkle-4.rgb555'],4)))
 def test_uncovered_sprite_removes_only_dependent_roots(self):
  original=imp.shared.read
  def read(reference,path):
   data=original(reference,path);return data.replace(b'createsprite gReflectSparkleSpriteTemplate',b'createsprite UnknownSparkle') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp.shared,'read',side_effect=read):data,_=imp.build(REFERENCE)
  self.assertEqual({112,113,243,277},{s['moveId'] for s in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
