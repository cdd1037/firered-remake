#!/usr/bin/env python3
"""Pinned friendship branches, contact roots, indexed pixels and ABI closure."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('friendship_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class FriendshipContactAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_all_four_roots_and_every_source_command_are_preserved(self):
  source=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move,count in [(122,7),(165,16),(216,121),(218,98)]:
   row=self.rows[move];self.assertEqual(count,len(row['commands']));self.assertTrue(row['soundEffects'])
   for command in row['commands']:
    text=source[command['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op=='jumpreteq':op='jumpargeq';args=['7']+args
    self.assertEqual((op,args),(command['op'],command['args']))
  self.assertEqual({'ReturnWeak','ReturnMedium','ReturnStrong','ReturnStrongest','ReturnContinue'},set(self.rows[216]['labels']))
  self.assertEqual(8,len(self.pack['subroutines']['ReturnStrongestHit']))
  self.assertTrue(any(c['args'][:1]==['AnimTask_TraceMonBlended'] and c['args'][2:]==['0','4','5','1'] for c in self.rows[216]['commands']))
  self.assertEqual(['SE_M_LICK'],list(self.rows[122]['soundEffects']))
 def test_exact_new_frames_and_shared_task_affine_program(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for template,asset,callback,w,h,tiles,duration in [('gLickSpriteTemplate','lick','AnimLick',16,32,[[0,8,16,24,32]],2),('gMovementWavesSpriteTemplate','movement-waves','AnimMovementWaves',32,32,[[0,16,32,16],[16,32,16,0]],8),('gWeakFrustrationAngerMarkSpriteTemplate','anger','AnimWeakFrustrationAngerMark',16,16,[[0]],1)]:
   r=recipes[template];self.assertEqual((asset,callback,w,h),(r['asset'],r['callback'],r['width'],r['height']));self.assertEqual(tiles,[[f['tile'] for f in seq] for seq in r['animations']]);self.assertTrue(all(f['duration']==duration for seq in r['animations'] for f in seq));self.assertEqual([],r['affineAnimations']);self.assertFalse(r['affineDoubleSize']);self.assertFalse(r['blend'])
  waves=recipes['gMovementWavesSpriteTemplate'];self.assertTrue(all(f['hFlip'] for f in waves['animations'][1]));self.assertFalse(any(f['hFlip'] for f in waves['animations'][0]));self.assertEqual([-1,-1],waves['loopStarts'])
  affine=self.pack['taskAffinePrograms']['sStrongFrustrationAffineAnimCmds'];self.assertEqual([('frame',0,-15,7,0),('frame',0,15,7,0),('loop',0,0,0,2),('end',0,0,0,0)],[(c['op'],c['x'],c['y'],c['duration'],c['count']) for c in affine])
 def test_registered_indexed_art_and_source_palette_are_exact(self):
  metadata,pixels=imp.build_indexed_assets(REFERENCE);rgb=imp.build_assets(REFERENCE);rows={r['id']:r for r in metadata}
  for stem,symbol,size,shape,pal_symbol,pal_stem in imp.FRIENDSHIP_CONTACT_MATERIALS:
   name=stem.replace('_','-');image=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png']));palette=image.getpalette();ordered=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];self.assertEqual(shape,image.size)
   self.assertEqual(size,len(pixels[name+'.4bpp']));self.assertEqual(bytes(ordered[i]|ordered[i+1]<<4 for i in range(0,len(ordered),2)),pixels[name+'.4bpp']);self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)],list(struct.unpack('<16H',pixels[name+'.pal16'])));self.assertEqual('ANIM_TAG_'+stem.upper(),rows[name]['paletteTag']);self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3))+(32768 if i else 0) for i in image.tobytes()],list(struct.unpack_from('<'+str(image.width*image.height)+'H',rgb[name],4)))
 def test_source_predicate_thresholds_result_and_macro_fail_closed(self):
  mutations=[('src/battle_anim_effects_3.c',b'if (gAnimFriendship > 60 && gAnimFriendship < 92)',b'if (gAnimFriendship >= 60 && gAnimFriendship < 92)'),('src/battle_anim_mons.c',b'if (gAnimFriendship <= 30)',b'if (gAnimFriendship < 30)'),('src/battle_anim_effects_3.c',b'gBattleAnimArgs[ARG_RET_ID] = 3;',b'gBattleAnimArgs[ARG_RET_ID] = 2;'),('src/battle_anim_mons.c',b'powerLevel = 3;',b'powerLevel = 2;'),('asm/macros/battle_anim_script.inc',b'jumpargeq ARG_RET_ID, \\value, \\ptr',b'jumpargeq 6, \\value, \\ptr')]
  for path,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with self.assertRaisesRegex(ValueError,'friendship|return-register'):imp.validate_friendship_predicates(raw)
 def test_missing_callbacks_and_retained_arguments_reject_whole_root(self):
  mutations=[(122,b'createsprite gLickSpriteTemplate,',b'createsprite UnknownLick,'),(165,b'createsprite gMovementWavesSpriteTemplate, ANIM_ATTACKER, 2, 0, 0, 2',b'createsprite gMovementWavesSpriteTemplate, ANIM_ATTACKER, 2, 0, 0'),(216,b'createvisualtask AnimTask_GetReturnPowerLevel, 2',b'createvisualtask AnimTask_GetReturnPowerLevel, 2, 0'),(216,b'createvisualtask AnimTask_TraceMonBlended, 2, 0, 4, 5, 1',b'createvisualtask AnimTask_TraceMonBlended, 2, 0, 4, 5'),(218,b'createvisualtask AnimTask_StrongFrustrationGrowAndShrink,',b'createvisualtask UnknownStrongFrustration,')]
  for move,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']})
  self.assertNotIn(283,self.rows);self.assertEqual(['invocation:retained-move-power'],next(s['missing'] for s in self.blocked if s['moveId']==283))
 def test_material_registration_and_callback_shape_fail_closed(self):
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Lick, 0x0500, ANIM_TAG_LICK}',b'{gBattleAnimSpriteGfx_Lick, 0x0400, ANIM_TAG_LICK}'),('src/battle_anim_ghost.c',b'.callback = AnimLick,',b'.callback = AnimLick_Step,')]:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
