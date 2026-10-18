#!/usr/bin/env python3
"""Pinned source closure, raw materials and fail-closed producer checks."""
import argparse,io,json,re,struct,sys,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image

sys.dont_write_bytecode=True
parser=argparse.ArgumentParser(add_help=False)
parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2])
parser.add_argument('--reference',type=Path);parser.add_argument('--generated',type=Path)
parser.add_argument('--importer',type=Path)
args,remaining=parser.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=args.root;REFERENCE=args.reference or ROOT/'content/raw/pokefirered/037335f4'
GENERATED=args.generated or ROOT/'content/generated/battle/animations'
canonical=ROOT/'scripts/imports/battle/import-source-physical-animations.py'
imp=types.ModuleType('damage_family_import');imp.__file__=str(canonical)
exec(compile((args.importer or canonical).read_text(),str(canonical),'exec'),imp.__dict__)

class DamageFamilyImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(cls.data)
  cls.roots={s['moveId']:s for s in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
  cls.raw=imp.read_sources(REFERENCE)

 def test_complete_roots_preserve_source_lines_operands_and_single_ownership(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in [229,245,279,314]:
   root=self.roots[move];self.assertTrue(root['commands']);self.assertTrue(any(c['op']=='end' for c in root['commands']))
   for row in root['commands']:
    op,*tail=lines[row['line']-1].split('@',1)[0].strip().split(None,1)
    values=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op=='createvisualtask' and values[0]=='AnimTask_AirCutterProjectile':
     self.assertEqual('6 * 256',values[4]);values[4]='1536'
    if op=='jumprettrue':op='jumpargeq';values=['7','1',*values]
    self.assertEqual((op,values),(row['op'],row['args']))
   self.assertNotIn(move,{r['moveId'] for r in self.blocked})
   for name in ['background-scripts.json','elemental.json']:
    other=json.loads((ROOT/'content/generated/battle/animations'/name).read_text())
    self.assertNotIn(move,{r['moveId'] for r in other['scripts']})

 def test_raw_children_callback_sound_and_task_affine_are_explicit_dependencies(self):
  self.assertEqual(['gSpeedDustSpriteTemplate'],self.roots[245]['spriteDependencies'])
  self.assertEqual(['gAirWaveProjectileSpriteTemplate'],self.roots[314]['spriteDependencies'])
  self.assertEqual({'SE_M_BLIZZARD2':147,'SE_M_CUT':121},self.roots[314]['soundEffects'])
  self.assertEqual([dict(op='frame',x=96,y=-13,rotation=0,duration=8,target=0,count=0),dict(op='end',x=0,y=0,rotation=0,duration=0,target=0,count=0)],self.pack['taskAffinePrograms']['sStretchAttackerAffineAnimCmds'])
  self.assertEqual(1,sum(c['op']=='setarg' and c['args']==['7','0x1000'] for c in self.roots[245]['commands']))
  scratch=self.recipes['gRevengeSmallScratchSpriteTemplate'];swipe=self.recipes['gRevengeBigScratchSpriteTemplate']
  self.assertEqual(([0,16,32],[4,4,4]),([f['tile'] for f in scratch['animations'][0]],[f['duration'] for f in scratch['animations'][0]]))
  self.assertEqual(([0,64],[6,6]),([f['tile'] for f in swipe['animations'][0]],[f['duration'] for f in swipe['animations'][0]]))
  self.assertTrue(scratch['animations'][1][0]['vFlip']);self.assertTrue(swipe['animations'][1][0]['hFlip'] and swipe['animations'][1][0]['vFlip'])
  ring=self.recipes['gRapidSpinSpriteTemplate'];self.assertEqual([0],ring['loopStarts']);self.assertEqual([0,8,16],[f['tile'] for f in ring['animations'][0]])

 def test_three_materials_match_registered_4bpp_tiles_and_source_palette_alias(self):
  assets,indexed,files=imp.damage_family_materials(self.raw)
  for stem,gfx,size,shape,palette_name,palette_stem in imp.DAMAGE_FAMILY_MATERIALS:
   name=stem.replace('_','-')
   with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png'])) as image:
    raster=list(image.tobytes());tiles=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
   with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+palette_stem+'.png'])) as image:palette=image.getpalette()[:48]
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
   self.assertEqual(size,len(files[name+'.4bpp']));self.assertEqual(bytes(tiles[i]|tiles[i+1]<<4 for i in range(0,len(tiles),2)),files[name+'.4bpp'])
   self.assertEqual(struct.pack('<16H',*colors),files[name+'.pal16'])
   self.assertEqual(shape,struct.unpack_from('<HH',assets[name]));self.assertEqual([colors[i]|(32768 if i else 0) for i in raster],list(struct.unpack_from('<'+str(len(raster))+'H',assets[name],4)))
   self.assertEqual(assets[name],(GENERATED/('physical-'+name+'.rgb555')).read_bytes())
   self.assertEqual(files[name+'.4bpp'],(GENERATED/('physical-'+name+'.4bpp')).read_bytes())
   self.assertEqual(files[name+'.pal16'],(GENERATED/('physical-'+name+'.pal16')).read_bytes())
  self.assertEqual(files['purple-scratch.pal16'],files['purple-swipe.pal16'])

 def test_missing_callbacks_and_materials_keep_roots_out(self):
  for move,old,new in [(229,b'createsprite gRapidSpinSpriteTemplate,',b'createsprite gUnknownRapidSpriteTemplate,'),(245,b'createvisualtask AnimTask_SpeedDust,',b'createvisualtask AnimTask_UnknownDust,'),(279,b'loadspritegfx ANIM_TAG_PURPLE_SWIPE',b'loadspritegfx ANIM_TAG_UNKNOWN_SWIPE'),(314,b'createsprite gAirCutterSliceSpriteTemplate,',b'createsprite gUnknownSliceSpriteTemplate,')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']});self.assertTrue(next(r for r in blocked if r['moveId']==move)['missing'])

 def test_changed_registration_palette_linkage_and_callback_sound_fail_closed(self):
  mutations=[('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_RapidSpin, 0x0300, ANIM_TAG_RAPID_SPIN}',b'{gBattleAnimSpriteGfx_RapidSpin, 0x0200, ANIM_TAG_RAPID_SPIN}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_PurpleScratch, ANIM_TAG_PURPLE_SWIPE}',b'{gBattleAnimSpritePal_PurpleSwipe, ANIM_TAG_PURPLE_SWIPE}'),('src/battle_anim_effects_2.c',b'PlaySE12WithPanning(SE_M_BLIZZARD2, BattleAnimAdjustPanning(-SOUND_PAN_TARGET));',b'PlaySE12WithPanning(SE_M_CUT, BattleAnimAdjustPanning(-SOUND_PAN_TARGET));')]
  for path,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaisesRegex(ValueError,'Damage source|Air Cutter callback'):imp.build(REFERENCE)

 def test_regeneration_matches_complete_generated_physical_pack(self):
  self.assertEqual(self.data,(GENERATED/'physical.json').read_bytes())
  self.assertEqual(len(self.pack['spriteRecipes']),len(self.recipes))

if __name__=='__main__':unittest.main()
