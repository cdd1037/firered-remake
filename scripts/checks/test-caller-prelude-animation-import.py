#!/usr/bin/env python3
"""Caller-prelude source closure and preservation of every existing row/material."""
import sys
sys.dont_write_bytecode=True
import argparse,importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');p.add_argument('--previous',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
spec=importlib.util.spec_from_file_location('caller_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
NAMES={'gMetronomeFingerSpriteTemplate','gLetterZSpriteTemplate'};ROOTS={118,214}
class CallerPreludeImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):data,cls.previous_pixels=imp.build(a.reference)
  cls.previous=json.loads(data)
 def test_exactly_two_roots_and_recipes_without_alias_roots(self):
  old={r['moveId']:r for r in self.previous['scripts']};self.assertEqual(ROOTS,set(self.roots)-set(old));self.assertEqual(set(old),set(self.roots)-ROOTS)
  for k,v in old.items():self.assertEqual(v,self.roots[k])
  for name in ['Move_MIRROR_MOVE','Move_NATURE_POWER']:self.assertNotIn(name,{r['label'] for r in self.roots.values()})
  oldrecipes={r['template']:r for r in self.previous['spriteRecipes']};self.assertEqual(NAMES,set(self.recipes)-set(oldrecipes))
  for name,r in oldrecipes.items():self.assertEqual(r,self.recipes[name])
  for key in set(self.pack)-{'scripts','spriteRecipes'}:self.assertEqual(self.previous[key],self.pack[key])
  self.assertEqual(self.previous_pixels,self.pixels)
 def test_authored_source_commands_dependencies_and_sound_order(self):
  metro=self.roots[118];sleep=self.roots[214];self.assertEqual('Move_METRONOME',metro['label']);self.assertEqual('Move_SLEEP_TALK',sleep['label'])
  self.assertEqual(['gThoughtBubbleSpriteTemplate','gMetronomeFingerSpriteTemplate'],[c['args'][0] for c in metro['commands'] if c['op']=='createsprite'])
  self.assertEqual([['gLetterZSpriteTemplate','ANIM_TARGET','2','0','20','5',str(v)] for v in [-1]*3+[-5]*3+[-3]*3],[c['args'] for c in sleep['commands'] if c['op']=='createsprite'])
  self.assertEqual([['AnimTask_SwayMon','5','0','4','4096','2','ANIM_ATTACKER']],[c['args'] for c in sleep['commands'] if c['op']=='createvisualtask'])
  self.assertEqual({'SE_M_METRONOME':179,'SE_M_TAIL_WHIP':160},metro['soundEffects']);self.assertEqual({'SE_M_SNORE':190},sleep['soundEffects'])
  for row in [metro,sleep]:self.assertEqual(['waitforvisualfinish','end'],[c['op'] for c in row['commands'][-2:]])
 def test_missing_shared_bubble_or_sway_closes_dependent_root(self):
  for names,tasks,missing in [({'gThoughtBubbleSpriteTemplate'},set(),{118,259,269}),(set(),{'AnimTask_SwayMon'},{214})]:
   with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in names}),patch.object(imp,'TASKS',imp.TASKS-tasks):data,_=imp.build(a.reference)
   actual={r['moveId'] for r in json.loads(data)['scripts']};self.assertTrue(missing.isdisjoint(actual));self.assertEqual(set(self.roots)-actual,missing) if names else self.assertEqual(self.roots[118],next(r for r in json.loads(data)['scripts'] if r['moveId']==118))
 def test_distinct_letter_and_metronome_programs(self):
  metro=self.recipes['gMetronomeFingerSpriteTemplate'];follow=self.recipes['gFollowMeFingerSpriteTemplate'];self.assertTrue(metro['affineDoubleSize']);self.assertFalse(follow['affineDoubleSize']);self.assertEqual(follow['affineAnimations'],metro['affineAnimations'])
  letter=self.recipes['gLetterZSpriteTemplate'];rest=self.recipes['gSleepLetterZSpriteTemplate'];self.assertEqual('AnimLetterZ',letter['callback']);self.assertNotEqual(rest['callback'],letter['callback']);self.assertEqual([{'tile':0,'duration':3,'hFlip':False,'vFlip':False}],letter['animations'][0]);self.assertEqual([-1],letter['loopStarts']);self.assertFalse(letter['affineDoubleSize']);self.assertEqual([(-7,-7,253,16),(7,7,3,16)],[(c['x'],c['y'],c['rotation'],c['duration']) for c in letter['affineAnimations'][0][:-1]]);self.assertEqual('jump',letter['affineAnimations'][0][-1]['op'])
 def test_every_source_material_pixel_matches_existing_owner(self):
  for asset,shape in [('finger',(32,32)),('thought-bubble',(32,128)),('letter-z',(32,32))]:
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+asset.replace('-','_')+'.png'))) as source:
    palette=source.getpalette()[:48];colors=[sum((palette[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(shape,source.size)
    if asset in self.pixels:
     self.assertEqual(shape,struct.unpack_from('<HH',self.pixels[asset]));self.assertEqual([colors[i]|(32768 if i else 0) for i in source.tobytes()],list(struct.unpack_from('<'+'H'*(shape[0]*shape[1]),self.pixels[asset],4)))
    if asset+'.4bpp' in self.pixels:
     tiled=[]
     for ty in range(0,shape[1],8):
      for tx in range(0,shape[0],8):
       for y in range(8):
        for x in range(8):tiled.append(source.getpixel((tx+x,ty+y)))
     self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),self.pixels[asset+'.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels[asset+'.pal16'])
 def test_corrupt_callback_fails_before_publication(self):
  original=imp.read
  for file,callback in [('battle_anim_effects_1.c','AnimMetronomeFinger'),('battle_anim_effects_3.c','AnimLetterZ')]:
   def read(reference,name):
    value=original(reference,name);return value.replace(('.callback = '+callback+',').encode(),b'.callback = UnsupportedCallback,') if name=='src/'+file else value
   with self.subTest(callback=callback),patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(a.reference)
 def test_generated_and_explicit_previous_packs_are_preserved(self):
  baseline=a.previous or a.generated;old=json.loads((baseline/'elemental.json').read_bytes())
  for row in old['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for group in ['assets','indexedAssets']:
   self.assertEqual(old[group],self.pack[group])
   for row in old[group]:
    for field in ['path','indicesPath','palettePath']:
     if field in row:self.assertEqual((baseline/row[field]).read_bytes(),self.pixels[row[field].removeprefix('elemental-').removesuffix('.rgb555')])
  data,pixels=imp.build(a.reference);self.assertEqual(self.data,data);self.assertEqual(self.pixels,pixels);self.assertEqual(data,(a.generated/'elemental.json').read_bytes())
  for name,blob in pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
if __name__=='__main__':unittest.main()
