#!/usr/bin/env python3
"""Source-backed physical-family import checks; no compiler or third-party execution."""
import importlib.util,json,unittest,struct
from PIL import Image
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('physical_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class PhysicalImportTests(unittest.TestCase):
 def test_regeneration_matches_generated_only_runtime_input(self):
  data,_=imp.build(REFERENCE)
  self.assertEqual(data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
  scripts=json.loads(data)['scripts'];self.assertEqual({2,66,11,21,22,23,26,27,29,34,36,38,64,67,70,77,78,79,136,289},{s['moveId'] for s in scripts})
  for s in scripts:
   self.assertEqual('end',s['commands'][-1]['op']);self.assertLessEqual(len(s['commands']),32)
 def test_generated_slam_and_whip_pixels_match_source_indices_and_palettes(self):
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/slam_hit.png') as image:
   indices=list(image.tobytes());slam=image.getpalette()
  lines=(REFERENCE/'graphics/battle_anims/sprites/whip_hit.pal').read_text().splitlines()[3:]
  whip=[int(c) for row in lines for c in row.split()]
  for name,palette in [('slam-hit',slam),('whip-hit',whip)]:
   blob=(ROOT/('content/generated/battle/animations/physical-'+name+'.rgb555')).read_bytes()
   self.assertEqual((32,256),struct.unpack_from('<HH',blob))
   pixels=struct.unpack_from('<8192H',blob,4)
   expected=[]
   for index in indices:
    r,g,b=palette[index*3:index*3+3]
    expected.append((r//8)+(g//8)*32+(b//8)*1024+(32768 if index else 0))
   self.assertEqual(expected,list(pixels))
 def test_powder_frames_loop_and_palette_variants_match_source(self):
  data,_=imp.build(REFERENCE);recipes=json.loads(data)['spriteRecipes']
  powders=[r for r in recipes if r['callback']=='AnimMovePowderParticle']
  self.assertEqual(3,len(powders))
  for recipe in powders:
   self.assertEqual((8,16),(recipe['width'],recipe['height']))
   self.assertEqual([0],recipe['loopStarts'])
   self.assertEqual(list(range(0,16,2)),[f['tile'] for f in recipe['animations'][0]])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/poison_powder.png') as image:
   indices=list(image.tobytes());poison=image.getpalette()
  for name in ['poison-powder','sleep-powder','stun-spore']:
   palette=poison if name=='poison-powder' else [int(c) for row in (REFERENCE/('graphics/battle_anims/sprites/'+name.replace('-','_')+'.pal')).read_text().splitlines()[3:] for c in row.split()]
   blob=(ROOT/('content/generated/battle/animations/physical-'+name+'.rgb555')).read_bytes()
   self.assertEqual((8,128),struct.unpack_from('<HH',blob))
   expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))+(32768 if i else 0) for i in indices]
   self.assertEqual(expected,list(struct.unpack_from('<1024H',blob,4)))
 def test_unknown_callback_fails_closed_at_discovery(self):
  raw=imp.read_sources(REFERENCE)
  raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(b'createvisualtask AnimTask_RotateMonSpriteToSide, 2, 6, 384, ANIM_TARGET, 2',b'createvisualtask UnimplementedRotation, 2, 6, 384, ANIM_TARGET, 2')
  with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(67,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertIn('callback:UnimplementedRotation',next(b for b in blocked if b['moveId']==67)['missing'])
 def test_unsupported_art_is_never_replaced_by_impact(self):
  _,blocked=imp.build(REFERENCE)
  slam=next(b for b in blocked if b['moveId']==282)
  self.assertIn('graphics:ANIM_TAG_SLAM_HIT_2',slam['missing'])
if __name__=='__main__':unittest.main()
