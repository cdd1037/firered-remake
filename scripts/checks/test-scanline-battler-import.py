#!/usr/bin/env python3
"""Four native scanline consumers: authored graph, ABI and shared material ownership."""
import argparse,importlib.util,json,re,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
class ScanlineBattlerImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'physical.json').read_text());cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']};cls.importer=module('scanline_battler_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py');cls.source=(REFERENCE/'data/battle_anim_scripts.s').read_text()
 def test_every_authored_command_and_called_dirt_helper_matches_pinned_source(self):
  lines=self.source.splitlines();self.assertTrue(self.roots)
  for move in [91,151,166,349]:
   root=self.roots[move];commands=root['commands']+[row for c in root['commands'] if c['op']=='call' for row in self.pack['subroutines'][c['args'][0]]]
   for c in commands:
    op,*tail=lines[c['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
  dig=self.roots[91];self.assertEqual(['DigSetUp','DigUnleash'],dig['commands'][0]['args']);self.assertEqual(5,sum(c['op']=='call' and c['args']==['DigThrowDirt'] for c in dig['commands']))
  self.assertTrue(any(c['op']=='monbg_static' for c in dig['commands']));self.assertTrue(any(c['op']=='clearmonbg_static' for c in dig['commands']))
 def test_four_static_sprite_recipes_are_direct_source_not_approximated_callbacks(self):
  for name,file,callback in self.importer.SCANLINE_BATTLER_RECIPES:
   source=(REFERENCE/'src'/file).read_text();self.assertEqual(self.importer.elemental.recipe(source,name,callback),self.recipes[name]);self.assertEqual([0],self.recipes[name]['loopStarts'])
   with self.assertRaises(ValueError):self.importer.elemental.recipe(source.replace('.callback = '+callback+',','.callback = Unknown,'),name,callback)
  self.assertEqual('mud-sand',self.recipes['gDirtPlumeSpriteTemplate']['asset']);self.assertEqual('hollow-orb',self.recipes['gDragonDanceOrbSpriteTemplate']['asset'])
 def test_every_new_material_pixel_and_palette_matches_source_registration(self):
  for stem,symbol,size,shape in self.importer.SCANLINE_BATTLER_MATERIALS:
   row=next(r for r in self.pack['indexedAssets'] if r['id']==stem.replace('_','-'))
   with Image.open(REFERENCE/'graphics/battle_anims/sprites'/f'{stem}.png') as image:
    self.assertEqual(('P',shape),(image.mode,image.size));palette=image.getpalette()[:48];indices=list(image.tobytes());tiles=[image.getpixel((x+dx,y+dy)) for y in range(0,shape[1],8) for x in range(0,shape[0],8) for dy in range(8) for dx in range(8)]
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
   self.assertEqual(size,len(tiles)//2);self.assertEqual(bytes(tiles[i]|tiles[i+1]<<4 for i in range(0,len(tiles),2)),(GENERATED/row['indicesPath']).read_bytes());self.assertEqual(struct.pack('<16H',*colors),(GENERATED/row['palettePath']).read_bytes());self.assertEqual(struct.pack('<HH',*shape)+struct.pack('<'+str(len(indices))+'H',*[colors[i]|(0x8000 if i else 0) for i in indices]),(GENERATED/('physical-'+stem.replace('_','-')+'.rgb555')).read_bytes())
 def test_changed_palette_or_sheet_registration_is_rejected(self):
  raw=self.importer.read_sources(REFERENCE);registration=raw['src/data/battle_anim.h']
  for stem,symbol,size,shape in self.importer.SCANLINE_BATTLER_MATERIALS:
   pattern=rb'(\{gBattleAnimSpriteGfx_'+symbol.encode()+rb',\s*)0x[0-9a-fA-F]+'
   with self.assertRaises(ValueError):self.importer.utility_materials({**raw,'src/data/battle_anim.h':re.sub(pattern,rb'\g<1>0x0010',registration)},self.importer.SCANLINE_BATTLER_MATERIALS)
   with self.assertRaises(ValueError):self.importer.utility_materials({**raw,'src/data/battle_anim.h':registration.replace(('gBattleAnimSpritePal_'+symbol+',').encode(),b'gBattleAnimSpritePal_Unknown,')},self.importer.SCANLINE_BATTLER_MATERIALS)
 def test_pencil_internal_sound_and_shared_hollow_orb_palette_have_metadata(self):
  sketch=self.roots[166];self.assertIn('SE_M_SKETCH',sketch['soundEffects']);self.assertNotIn('SE_M_SKETCH',[c['args'][0] for c in sketch['commands'] if c['op']=='playsewithpan'])
  self.assertIn('ANIM_TAG_HOLLOW_ORB',self.roots[349]['spriteTags']);self.assertFalse(any(a['id']=='hollow-orb' for a in self.pack['assets']))
  self.assertEqual(6,sum(c['op']=='createsprite' and c['args'][0]=='gDragonDanceOrbSpriteTemplate' for c in self.roots[349]['commands']))
if __name__=='__main__':unittest.main()
