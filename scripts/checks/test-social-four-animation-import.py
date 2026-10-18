#!/usr/bin/env python3
"""Bounded social-root source closure, exact materials and prior-pack parity."""
import sys
sys.dont_write_bytecode=True
import argparse,importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');p.add_argument('--previous',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('social_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');bg=module('social_background',ROOT/'scripts/imports/battle/import-source-animation-backgrounds.py')
NAMES={'gThoughtBubbleSpriteTemplate','gTauntFingerSpriteTemplate','gFollowMeFingerSpriteTemplate','gRedHeartProjectileSpriteTemplate','gRedHeartRisingSpriteTemplate'}
ROOTS={213,259,266,269}
def block(atlas,row):return atlas[row[0]:sum(row)]
def values(row,atlas):return {k:block(atlas,v) if isinstance(v,list) and len(v)==2 else v for k,v in row.items()}
class SocialFourImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(a.reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']};cls.background=bg.build(a.reference)
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):data,_=imp.build(a.reference)
  cls.previous=json.loads(data)
 def mutated(self,path,old,new,producer=imp):
  original=producer.read;self.assertIn(old,original(a.reference,path))
  def read(reference,name):
   value=original(reference,name);return value.replace(old,new) if name==path else value
  with patch.object(producer,'read',side_effect=read):return producer.build(a.reference)
 def test_complete_dependency_difference_is_exactly_four_roots(self):
  old={r['moveId']:r for r in self.previous['scripts']};self.assertEqual(ROOTS,set(self.roots)-set(old));self.assertEqual(set(old),set(self.roots)-ROOTS)
  for key,value in old.items():self.assertEqual(value,self.roots[key])
  oldrecipes={r['template']:r for r in self.previous['spriteRecipes']};self.assertEqual(NAMES,set(self.recipes)-set(oldrecipes))
  for name,recipe in oldrecipes.items():self.assertEqual(recipe,self.recipes[name])
 def test_all_four_authored_roots_retain_complete_commands_and_shared_dependencies(self):
  expected={213:('ATTRACT',15),259:('TORMENT',2),266:('FOLLOW_ME',1),269:('TAUNT',4)}
  for move,(name,count) in expected.items():
   row=self.roots[move];self.assertEqual('Move_'+name,row['label']);self.assertEqual('end',row['commands'][-1]['op']);self.assertEqual(count,sum(c['op']=='createsprite' for c in row['commands']))
  self.assertEqual(['attract'],self.roots[213]['overlayDependencies']);self.assertEqual(['gThoughtBubbleSpriteTemplate'],self.roots[259]['spriteDependencies']);self.assertEqual(179,self.roots[259]['soundEffects']['SE_M_METRONOME'])
  self.assertEqual([(-12,8,4),(20,-20,4),(-8,12,4)],[(c['x'],c['y'],c['duration']) for c in self.pack['taskAffinePrograms']['sAffineAnims_Torment'][:-1]])
 def test_missing_raw_child_recipe_or_task_closes_torment(self):
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!='gThoughtBubbleSpriteTemplate'}):data,_=imp.build(a.reference)
  self.assertEqual(set(self.roots)-{259,269},{r['moveId'] for r in json.loads(data)['scripts']})
  with patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_TormentAttacker'}):data,_=imp.build(a.reference)
  self.assertEqual(set(self.roots)-{259},{r['moveId'] for r in json.loads(data)['scripts']})
 def test_finger_assets_use_registered_shared_finger_palette(self):
  with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/finger.png'))) as source:palette=source.getpalette()[:48]
  colors=[sum((palette[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
  for asset,shape in [('finger',(32,32)),('finger-2',(32,96))]:
   with Image.open(io.BytesIO(imp.read(a.reference,'graphics/battle_anims/sprites/'+asset.replace('-','_')+'.png'))) as source:
    self.assertEqual(shape,source.size);self.assertEqual(shape,struct.unpack_from('<HH',self.pixels[asset]));self.assertEqual([colors[i]|(32768 if i else 0) for i in source.tobytes()],list(struct.unpack_from('<'+'H'*(shape[0]*shape[1]),self.pixels[asset],4)))
 def test_exact_frame_and_affine_ownership(self):
  thought=self.recipes['gThoughtBubbleSpriteTemplate'];self.assertEqual([-1]*4,thought['loopStarts']);self.assertEqual([[0,16,32,48],[0,16,32,48],[48,32,16,0],[48,32,16,0]],[[f['tile'] for f in x] for x in thought['animations']]);self.assertEqual([True,False,True,False],[x[0]['hFlip'] for x in thought['animations']]);self.assertTrue(all(f['duration']==2 for x in thought['animations'] for f in x))
  taunt=self.recipes['gTauntFingerSpriteTemplate'];self.assertEqual([1,1,7,7],[len(x) for x in taunt['animations']]);follow=self.recipes['gFollowMeFingerSpriteTemplate'];self.assertFalse(follow['affineDoubleSize']);self.assertEqual(2,len(follow['affineAnimations']));self.assertEqual('loop',follow['affineAnimations'][1][2]['op']);self.assertEqual(2,follow['affineAnimations'][1][2]['target'])
 def test_bad_material_registration_and_callback_fail_closed(self):
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Finger, ANIM_TAG_FINGER_2}',b'{gBattleAnimSpritePal_ThoughtBubble, ANIM_TAG_FINGER_2}'),('src/graphics.c',b'graphics/battle_anims/sprites/finger_2.4bpp.lz',b'graphics/battle_anims/sprites/finger.4bpp.lz'),('src/battle_anim_effects_1.c',b'.callback = AnimFollowMeFinger,',b'.callback = MissingFollowFinger,'),('src/battle_anim_effects_2.c',b'.callback = AnimRedHeartRising,',b'.callback = MissingRisingHeart,')]:
   with self.subTest(path=path),self.assertRaises(ValueError):self.mutated(path,old,new)
 def test_torment_implicit_sound_raw_callback_and_counter_fail_closed(self):
  for old,new in [(b'PlaySE12WithPanning(SE_M_METRONOME, BattleAnimAdjustPanning(SOUND_PAN_ATTACKER));',b'PlaySE12WithPanning(SE_M_METRONOME, BattleAnimAdjustPanning(SOUND_PAN_TARGET));'),(b'gSprites[spriteId].callback = SpriteCallbackDummy;',b'gSprites[spriteId].callback = AnimThoughtBubble;'),(b'gSprites[i].callback = TormentAttacker_Callback;',b'gSprites[i].callback = SpriteCallbackDummy;'),(b'task->data[6] = j;',b'task->data[6] = 6;')]:
   with self.subTest(old=old),self.assertRaises(ValueError):self.mutated('src/battle_anim_effects_3.c',old,new)
 def test_attract_overlay_exact_bytes_and_material_identity(self):
  m=json.loads(self.background['manifest.json']);atlas=self.background['atlas.bin'];row=next(x for x in m['overlays'] if x['id']=='attract');tiles,palette=bg.indexed_tiles(bg.read(a.reference,'graphics/battle_anims/backgrounds/attract.png'));self.assertEqual(tiles.ljust(8192,b'\0'),block(atlas,row['tiles']));self.assertEqual(palette,block(atlas,row['palette']));self.assertEqual(bg.read(a.reference,'graphics/battle_anims/backgrounds/attract.bin'),block(atlas,row['tilemap']));self.assertEqual(8,row['paletteBank'])
  with self.assertRaises(ValueError):self.mutated('src/graphics.c',b'graphics/battle_anims/backgrounds/attract.gbapal.lz',b'graphics/battle_anims/backgrounds/ghost.gbapal.lz',bg)
 def test_generated_or_explicit_baseline_rows_recipes_pixels_and_bg_blocks_match(self):
  baseline=a.previous or a.generated
  old=json.loads((baseline/'elemental.json').read_bytes())
  for row in old['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in old['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  for group in ['assets','indexedAssets']:
   for row in old[group]:
    self.assertIn(row,self.pack[group])
    for field in ['path','indicesPath','palettePath']:
     if field in row:self.assertEqual((baseline/row[field]).read_bytes(),self.pixels[row[field].removeprefix('elemental-').removesuffix('.rgb555')])
  m=json.loads((baseline/'backgrounds/manifest.json').read_bytes());atlas=(baseline/'backgrounds/atlas.bin').read_bytes();current=json.loads(self.background['manifest.json']);newatlas=self.background['atlas.bin'];self.assertEqual(block(atlas,m['basePalette']),block(newatlas,current['basePalette']))
  for group in ['terrains','moves','overlays']:
   new={r['id']:r for r in current[group]}
   for row in m[group]:self.assertEqual(values(row,atlas),values(new[row['id']],newatlas))
 def test_repeated_build_and_generated_parity(self):
  data,pixels=imp.build(a.reference);self.assertEqual(self.data,data);self.assertEqual(self.pixels,pixels);self.assertEqual(self.background,bg.build(a.reference));self.assertEqual(data,(a.generated/'elemental.json').read_bytes())
  for name,blob in pixels.items():self.assertEqual(blob,(a.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
  for name,blob in self.background.items():self.assertEqual(blob,(a.generated/'backgrounds'/name).read_bytes())
if __name__=='__main__':unittest.main()
