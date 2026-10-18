#!/usr/bin/env python3
"""Focused source closure, shared recipes and indexed electric/Wisp material parity."""
import argparse,importlib.util,json,re,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
class ElectricWispImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'elemental.json').read_text());cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']};cls.importer=module('electric_wisp_importer',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');cls.source=(REFERENCE/'data/battle_anim_scripts.s').read_text()
 def test_exact_source_roots_commands_and_callback_sounds(self):
  self.assertEqual({261,268,351},set(self.roots)&{209,261,268,344,351});lines=self.source.splitlines()
  for move in [261,268,351]:
   root=self.roots[move];commands=root['commands']+[c for label in {r['args'][0] for r in root['commands'] if r['op']=='call'} for c in self.pack['subroutines'][label]]
   for c in commands:
    op,*tail=lines[c['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertIn('SE_M_FLAME_WHEEL',self.roots[261]['soundEffects']);self.assertIn('SE_M_THUNDERBOLT',self.roots[351]['soundEffects'])
 def test_raw_children_are_generated_dependencies_of_the_existing_catalog(self):
  self.assertEqual(['gElectricChargingParticlesSpriteTemplate'],self.roots[268]['spriteDependencies'])
  self.assertEqual(['gElectricChargingParticlesSpriteTemplate','sShockWaveProgressingBoltSpriteTemplate','gLightningSpriteTemplate'],self.roots[351]['spriteDependencies'])
  self.assertEqual('spark-thin',self.recipes['sShockWaveProgressingBoltSpriteTemplate']['asset']);self.assertEqual('lightning',self.recipes['gLightningSpriteTemplate']['asset'])
  self.assertNotIn('electric-wisp', {r['id'] for r in self.pack['assets']})
 def test_recipes_are_direct_source_and_finite_charge_loop_is_preserved(self):
  for name in ['gElectricChargingParticlesSpriteTemplate','gGrowingChargeOrbSpriteTemplate','gElectricPuffSpriteTemplate','gGrowingShockWaveOrbSpriteTemplate','sShockWaveProgressingBoltSpriteTemplate','gWillOWispOrbSpriteTemplate','gWillOWispFireSpriteTemplate']:
   file,callback=self.importer.TEMPLATES[name];source=(REFERENCE/'src'/file).read_text();self.assertEqual(self.importer.recipe(source,name,callback),self.recipes[name])
   with self.assertRaises(ValueError):self.importer.recipe(source.replace('.callback = '+callback+',','.callback = UnreviewedCallback,'),name,callback)
  charge=self.recipes['gGrowingChargeOrbSpriteTemplate'];self.assertEqual([0,10],[c['target'] for c in charge['affineAnimations'][0] if c['op']=='loop']);self.assertEqual(charge['affineAnimations'],self.recipes['gGrowingShockWaveOrbSpriteTemplate']['affineAnimations'])
  wisp=self.recipes['gWillOWispOrbSpriteTemplate'];self.assertEqual(wisp['animations'][2],wisp['animations'][3]);self.assertEqual([0,-1,-1,-1],wisp['loopStarts'])
 def test_every_new_index_palette_entry_and_rgb_pixel_matches_pinned_art(self):
  for stem,size,pal in [('electric_orbs',(8,32),'electric_orbs'),('electricity',(32,128),'electric_orbs'),('wisp_orb',(16,112),'wisp_orb'),('wisp_fire',(32,128),'wisp_orb')]:
   row=next(r for r in self.pack['indexedAssets'] if r['id']==stem.replace('_','-'))
   with Image.open(REFERENCE/'graphics/battle_anims/sprites'/f'{stem}.png') as im:
    self.assertEqual(('P',size),(im.mode,im.size));indices=list(im.tobytes());tiled=[im.getpixel((x+dx,y+dy)) for y in range(0,size[1],8) for x in range(0,size[0],8) for dy in range(8) for dx in range(8)]
   with Image.open(REFERENCE/'graphics/battle_anims/sprites'/f'{pal}.png') as im:palette=im.getpalette()[:48]
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual('ANIM_TAG_'+stem.upper(),row['paletteTag']);self.assertEqual(size,(row['width'],row['height']));self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),(GENERATED/row['indicesPath']).read_bytes());self.assertEqual(struct.pack('<16H',*colors),(GENERATED/row['palettePath']).read_bytes());self.assertEqual(struct.pack('<HH',*size)+struct.pack('<'+str(len(indices))+'H',*[colors[i]|(0x8000 if i else 0) for i in indices]),(GENERATED/('elemental-'+stem.replace('_','-')+'.rgb555')).read_bytes())
 def test_source_sprite_registration_and_palette_aliases_are_exact(self):
  registration=(REFERENCE/'src/data/battle_anim.h').read_text();graphics=(REFERENCE/'src/graphics.c').read_text()
  for stem,symbol,size,palette in [('electric_orbs','ElectricOrbs','0080','ElectricOrbs'),('electricity','Electricity','0800','ElectricOrbs'),('wisp_orb','WispOrb','0380','WispOrb'),('wisp_fire','WispFire','0800','WispOrb')]:
   tag='ANIM_TAG_'+stem.upper();self.assertRegex(registration,r'\{gBattleAnimSpriteGfx_'+symbol+r', 0x'+size+', '+tag+r'\}');self.assertRegex(registration,r'\{gBattleAnimSpritePal_'+palette+', '+tag+r'\}');self.assertIn('gBattleAnimSpriteGfx_'+symbol+'[] = INCBIN_U32("graphics/battle_anims/sprites/'+stem+'.4bpp.lz");',graphics)
 def test_discovery_never_requires_or_rewrites_generated_materials(self):
  rows,subs=self.importer.discover_scripts(REFERENCE);actual={r['moveId']:r for r in rows}
  for move in [261,268,351]:self.assertEqual(self.roots[move],actual[move])
  self.assertNotIn(209,actual);self.assertNotIn(344,actual)
if __name__=='__main__':unittest.main()
