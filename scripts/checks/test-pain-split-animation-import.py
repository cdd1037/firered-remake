#!/usr/bin/env python3
"""Pain Split source discovery, finite frames, indexed art and prior-pack parity."""
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
path=ROOT/'scripts/imports/battle/import-source-physical-animations.py';imp=types.ModuleType('pain_split_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
class PainSplitImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,_=imp.build(REFERENCE);cls.pixels={**imp.build_assets(REFERENCE),**imp.build_indexed_assets(REFERENCE)[1]};cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_only_pain_split_enters_discovery_with_three_exact_paired_waves(self):
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_PainSplitMovement'}):data,_=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({220},set(self.roots)-set(old))
  for key,value in old.items():self.assertEqual(value,self.roots[key])
  row=self.roots[220];self.assertEqual('Move_PAIN_SPLIT',row['label']);commands=row['commands'];sprites=[c['args'] for c in commands if c['op']=='createsprite'];tasks=[c['args'] for c in commands if c['op']=='createvisualtask']
  self.assertEqual([['gPainSplitProjectileSpriteTemplate',selector,'2',str(x),'-42',selector] for x in [-8,-24,8] for selector in ['ANIM_ATTACKER','ANIM_TARGET']],sprites)
  self.assertEqual([['AnimTask_PainSplitMovement','2',selector,str(mode)] for mode in range(3) for selector in ['ANIM_ATTACKER','ANIM_TARGET']],tasks)
  self.assertEqual([['10']]*3,[c['args'] for c in commands if c['op']=='delay']);self.assertEqual([['SE_M_SWAGGER2','0']]*3,[c['args'] for c in commands if c['op']=='playsewithpan']);self.assertEqual(2,sum(c['op']=='waitforvisualfinish' for c in commands))
 def test_finite_source_frame_program_and_geometry_are_exact(self):
  r=self.recipes['gPainSplitProjectileSpriteTemplate'];self.assertEqual(('pain-split','AnimPainSplitProjectile',16,16),(r['asset'],r['callback'],r['width'],r['height']));self.assertFalse(r['blend']);self.assertEqual([],r.get('affineAnimations',[]));self.assertEqual([-1],r['loopStarts']);self.assertFalse(any(r.get('frameLoops',[])))
  self.assertEqual([[dict(tile=t,duration=d,hFlip=False,vFlip=False) for t,d in [(0,5),(4,9),(8,5)]]],r['animations'])
 def test_rgb_indices_and_palette_match_every_pinned_pixel(self):
  raw=imp.read_sources(REFERENCE)
  with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/pain_split.png'])) as image:
   self.assertEqual(('P',(16,48)),(image.mode,image.size));palette=image.getpalette()[:48];colors=[sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)]
   expected=[colors[i]|(32768 if i else 0) for i in image.tobytes()];self.assertEqual((16,48),struct.unpack_from('<HH',self.pixels['pain-split']));self.assertEqual(expected,list(struct.unpack_from('<768H',self.pixels['pain-split'],4)))
   tiled=[image.getpixel((tx+x,ty+y)) for ty in range(0,48,8) for tx in range(0,16,8) for y in range(8) for x in range(8)]
   self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),self.pixels['pain-split.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels['pain-split.pal16']);self.assertEqual(384,len(self.pixels['pain-split.4bpp']))
 def test_source_material_and_callback_corruption_fail_closed(self):
  original=imp.read_sources
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_PainSplit, 0x0180,',b'{gBattleAnimSpriteGfx_PainSplit, 0x0100,'),('src/graphics.c',b'sprites/pain_split.4bpp.lz',b'sprites/other.4bpp.lz'),('src/battle_anim_effects_3.c',b'.callback = AnimPainSplitProjectile,',b'.callback = Other,')]:
   self.assertIn(old,original(REFERENCE)[path])
   def changed(ref):
    data=original(ref);data[path]=data[path].replace(old,new);return data
   with self.subTest(path=path),patch.object(imp,'read_sources',side_effect=changed),self.assertRaises(ValueError):
    if path.endswith('effects_3.c'):imp.build(REFERENCE)
    else:imp.build_assets(REFERENCE)
 def test_prior_rows_materials_and_generated_results_are_exact(self):
  previous=json.loads((PREVIOUS/'physical.json').read_bytes())
  for row in previous['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in previous['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for name,value in previous['subroutines'].items():self.assertEqual(value,self.pack['subroutines'][name])
  for group in ['assets','indexedAssets']:
   current={r['id']:r for r in self.pack[group]}
   for row in previous[group]:
    self.assertEqual(row,current[row['id']])
    for field in ['path','indicesPath','palettePath']:
     if field in row and row[field].startswith('physical-'):self.assertEqual((PREVIOUS/row[field]).read_bytes(),self.pixels[row[field].removeprefix('physical-').removesuffix('.rgb555')])
  self.assertEqual(self.data,(GENERATED/'physical.json').read_bytes())
  for name,data in self.pixels.items():self.assertEqual(data,(GENERATED/('physical-'+name if name.endswith(('.4bpp','.pal16','.pal48')) else 'physical-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
