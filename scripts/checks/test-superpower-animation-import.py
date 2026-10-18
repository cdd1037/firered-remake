#!/usr/bin/env python3
"""Superpower source-only discovery, material fidelity and prior-pack parity."""
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
path=ROOT/'scripts/imports/battle/import-source-physical-animations.py';imp=types.ModuleType('superpower_importer');imp.__file__=str(path);exec(compile((a.importer or path).read_text(),str(a.importer or path),'exec'),imp.__dict__)
class SuperpowerImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,_=imp.build(REFERENCE);cls.pixels={**imp.build_assets(REFERENCE),**imp.build_indexed_assets(REFERENCE)[1]};cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_only_superpower_enters_discovery_from_the_three_source_callbacks(self):
  with patch.object(imp,'SPRITES',imp.SPRITES-set(imp.SUPERPOWER_WIDTHS)):data,_=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({276},set(self.roots)-set(old))
  for key,value in old.items():self.assertEqual(value,self.roots[key])
  c=self.roots[276]['commands'];self.assertEqual('Move_SUPERPOWER',self.roots[276]['label'])
  sprites=[r['args'] for r in c if r['op']=='createsprite'];self.assertEqual([
   ['gSuperpowerOrbSpriteTemplate','ANIM_TARGET','2','ANIM_ATTACKER'],
   ['gShakeMonOrTerrainSpriteTemplate','ANIM_ATTACKER','2','4','1','180','1'],
   *[['gSuperpowerRockSpriteTemplate','ANIM_ATTACKER','41',*map(str,args)] for args in [[200,96,1,120],[20,248,4,112],[130,160,2,104],[160,192,0,96],[60,288,3,88]]],
   ['gSuperpowerFireballSpriteTemplate','ANIM_TARGET','3','ANIM_ATTACKER']],sprites)
  self.assertEqual([['20'],['40'],['8'],['8'],['8'],['8'],['74'],['16'],['1']],[r['args'] for r in c if r['op']=='delay'])
  self.assertEqual([['SoundTask_PlaySE2WithPanning','5','SE_M_EARTHQUAKE','0'],['AnimTask_ShakeMon2','2','ANIM_TARGET','8','0','16','1']],[r['args'] for r in c if r['op']=='createvisualtask'])
  self.assertEqual(1,sum(r['op']=='waitforvisualfinish' for r in c))
 def test_static_frames_and_orb_affine_program_are_exact(self):
  for name,asset,callback,size,orb in [('Orb','circle-of-light','AnimSuperpowerOrb',64,True),('Rock','flat-rock','AnimSuperpowerRock',16,False),('Fireball','meteor','AnimSuperpowerFireball',64,False)]:
   r=self.recipes['gSuperpower'+name+'SpriteTemplate'];self.assertEqual((asset,callback,size,size,orb,orb),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r.get('affineDoubleSize',False)));self.assertEqual([0],r['loopStarts']);self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertFalse(any(r.get('frameLoops',[])))
   if orb:self.assertEqual([[dict(op='frame',x=32,y=32,rotation=0,duration=0,target=0),dict(op='frame',x=4,y=4,rotation=0,duration=64,target=0),dict(op='frame',x=-6,y=-6,rotation=0,duration=8,target=0),dict(op='frame',x=6,y=6,rotation=0,duration=8,target=0),dict(op='jump',x=0,y=0,rotation=0,duration=0,target=2)]],r['affineAnimations'])
   else:self.assertEqual([],r.get('affineAnimations',[]))
 def test_rgb_indices_and_palette_match_all_pinned_meteor_and_rock_pixels(self):
  raw=imp.read_sources(REFERENCE)
  for stem,w,h,size in [('meteor',64,64,0x800),('flat_rock',16,80,0x280)]:
   name=stem.replace('_','-')
   with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/'+stem+'.png'])) as im:
    self.assertEqual(('P',(w,h)),(im.mode,im.size));palette=im.getpalette()[:48];colors=[sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)]
    self.assertEqual((w,h),struct.unpack_from('<HH',self.pixels[name]));self.assertEqual([colors[i]|(32768 if i else 0) for i in im.tobytes()],list(struct.unpack_from('<'+str(w*h)+'H',self.pixels[name],4)))
    tiled=[im.getpixel((tx+x,ty+y)) for ty in range(0,h,8) for tx in range(0,w,8) for y in range(8) for x in range(8)];self.assertEqual(bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2)),self.pixels[name+'.4bpp']);self.assertEqual(struct.pack('<16H',*colors),self.pixels[name+'.pal16']);self.assertEqual(size,len(self.pixels[name+'.4bpp']))
 def test_source_callback_material_and_operand_mutations_fail_closed(self):
  original=imp.read_sources
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Meteor, 0x0800,',b'{gBattleAnimSpriteGfx_Meteor, 0x0100,'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_FlatRock, ANIM_TAG_FLAT_ROCK}',b'{gBattleAnimSpritePal_Meteor, ANIM_TAG_FLAT_ROCK}'),('src/graphics.c',b'sprites/meteor.4bpp.lz',b'sprites/other.4bpp.lz'),('src/battle_anim_fight.c',b'.callback = AnimSuperpowerOrb,',b'.callback = Other,')]:
   self.assertIn(old,original(REFERENCE)[path])
   def changed(ref):
    data=original(ref);data[path]=data[path].replace(old,new);return data
   with self.subTest(path=path),patch.object(imp,'read_sources',side_effect=changed),self.assertRaises(ValueError):
    if path.endswith('fight.c'):imp.build(REFERENCE)
    else:imp.build_assets(REFERENCE)
  old=b'createsprite gSuperpowerRockSpriteTemplate, ANIM_ATTACKER, 41, 200, 96, 1, 120';self.assertIn(old,original(REFERENCE)['data/battle_anim_scripts.s'])
  def short(ref):
   data=original(ref);data['data/battle_anim_scripts.s']=data['data/battle_anim_scripts.s'].replace(old,old.rsplit(b',',1)[0]);return data
  with patch.object(imp,'read_sources',side_effect=short):data,blocked=imp.build(REFERENCE)
  self.assertNotIn(276,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback-arguments:gSuperpowerRockSpriteTemplate',str(blocked))
 def test_prior_roots_recipes_materials_and_generated_results_remain_exact(self):
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
