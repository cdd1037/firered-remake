#!/usr/bin/env python3
"""Maintained Fissure/Seismic source graph, shared recipe and indexed BG3 checks."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
class EarthBackgroundImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'physical.json').read_text());cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.importer=module('earth_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');cls.bg=module('earth_backgrounds',ROOT/'scripts/imports/battle/import-source-animation-backgrounds.py');cls.source=(REFERENCE/'data/battle_anim_scripts.s').read_text()
 def test_complete_graph_commands_and_scatter_helpers_match_pinned_source(self):
  lines=self.source.splitlines()
  for move in [69,90]:
   self.assertIn(move,self.roots);root=self.roots[move];rows=root['commands']+[r for c in root['commands'] if c['op']=='call' for r in self.pack['subroutines'][c['args'][0]]]
   for row in rows:
    op,*tail=lines[row['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op=='jumpreteq':op='jumpargeq';args=['7',*args]
    self.assertEqual((op,args),(row['op'],row['args']))
  self.assertEqual(['0','1','2'],[c['args'][1] for c in self.roots[69]['commands'] if c['op']=='jumpargeq']);self.assertEqual(9,sum(c['op']=='call' for c in self.roots[69]['commands']))
  self.assertEqual(3,sum(c['op']=='call' for c in self.roots[90]['commands']));self.assertTrue(any(c['op']=='setarg' and c['args']==['7','0xFFF'] for c in self.roots[69]['commands']))
 def test_scatter_frame_and_affine_recipe_are_shared_source_tables(self):
  source=(REFERENCE/'src/battle_anim_rock.c').read_text();expected=self.importer.elemental.recipe(source,'gRockScatterSpriteTemplate','AnimRockScatter');actual=next(r for r in self.pack['spriteRecipes'] if r['template']=='gRockScatterSpriteTemplate');self.assertEqual(expected,actual);self.assertEqual('rocks',actual['asset']);self.assertEqual([251,5],[p[0]['rotation'] for p in actual['affineAnimations']])
  with self.assertRaises(ValueError):self.importer.elemental.recipe(source.replace('.callback = AnimRockScatter,','.callback = Unknown,'),'gRockScatterSpriteTemplate','AnimRockScatter')
  self.assertEqual(1,sum(r['id']=='rocks' for r in self.pack['assets']));self.assertEqual('elemental-rocks.rgb555',next(r['path'] for r in self.pack['assets'] if r['id']=='rocks'))
 def test_fissure_bg_tiles_palette_and_full_screenblock_bytes_match_source(self):
  tags=(REFERENCE/'include/constants/battle_anim.h').read_text();id=int(re.search(r'^#define BG_FISSURE\s+(\d+)',tags,re.M)[1]);manifest=json.loads((GENERATED/'backgrounds/manifest.json').read_text());row=next(r for r in manifest['moves'] if r['id']==id);atlas=(GENERATED/'backgrounds/atlas.bin').read_bytes()
  tiles,palette=self.bg.indexed_tiles((REFERENCE/'graphics/battle_anims/backgrounds/fissure.png').read_bytes());tilemap=(REFERENCE/'graphics/battle_anims/backgrounds/fissure.bin').read_bytes();self.assertEqual(4096,len(tilemap))
  for key,data in [('tiles',tiles),('tilemap',tilemap),('palette',palette)]:offset,size=row[key];self.assertEqual(data,atlas[offset:offset+size])
  self.assertEqual(id,self.pack['backgroundSymbols']['BG_FISSURE']);self.assertIn('BG_FISSURE',self.bg.MOVE_BACKGROUNDS)
 def test_physical_and_background_source_owners_remain_disjoint(self):
  background=json.loads((GENERATED/'background-scripts.json').read_text());self.assertFalse(set(self.roots)&{r['moveId'] for r in background['scripts']})
if __name__=='__main__':unittest.main()
