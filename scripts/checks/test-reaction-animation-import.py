#!/usr/bin/env python3
"""Exact whole-root reaction closures and their shared-player/material boundaries."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('reaction_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ReactionAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_four_complete_original_roots_include_called_ice_and_audio(self):
  self.assertTrue({161,263,265,348}<=self.rows.keys());source=self.raw['data/battle_anim_scripts.s'].decode()
  for move in [161,263,265,348]:
   row=self.rows[move];body=source.split(row['label']+':\n',1)[1].split('\n\tend',1)[0];commands=[]
   for line in body.splitlines():
    line=line.split('@',1)[0].strip()
    if not line:continue
    op,*tail=line.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];commands.append((op,args))
   commands.append(('end',[]));self.assertEqual(commands,[(c['op'],c['args']) for c in row['commands']]);self.assertTrue(row['soundEffects'])
  self.assertIn('IceCrystalEffectShort',self.pack['subroutines']);self.assertEqual(7,sum(c['op']=='createsprite' for c in self.pack['subroutines']['IceCrystalEffectShort']));self.assertIn('SE_M_ICY_WIND',self.rows[161]['soundEffects']);self.assertIn('SE_M_TRI_ATTACK',self.rows[161]['soundEffects'])
  self.assertEqual(['gFacadeSweatDropSpriteTemplate'],self.rows[263]['spriteDependencies']);self.assertEqual(['gLeafBladeSpriteTemplate'],self.rows[348]['spriteDependencies'])
 def test_source_frame_affine_and_raw_child_recipes_remain_exact(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']};triangle=recipes['gTriAttackTriangleSpriteTemplate']
  self.assertEqual(('tri-attack-triangle','AnimTriAttackTriangle',64,64,True),(triangle['asset'],triangle['callback'],triangle['width'],triangle['height'],triangle['affineDoubleSize']));self.assertEqual([(0,0,5,40),(0,0,10,10),(0,0,15,10),(0,0,20,40)],[(c['x'],c['y'],c['rotation'],c['duration']) for c in triangle['affineAnimations'][0][:-1]])
  leaf=recipes['gLeafBladeSpriteTemplate'];self.assertEqual([28,32,20,28,16,16,28],[a[0]['tile'] for a in leaf['animations']]);self.assertEqual([3,5],[i for i,a in enumerate(leaf['animations']) if a[0]['hFlip']]);self.assertEqual([-1]*7,leaf['loopStarts']);self.assertEqual([],leaf['affineAnimations'])
  self.assertEqual('AnimFacadeSweatDrop',recipes['gFacadeSweatDropSpriteTemplate']['callback']);self.assertEqual('tag-hand',recipes['gSmellingSaltsHandSpriteTemplate']['asset']);self.assertEqual('smellingsalt-effect',recipes['gSmellingSaltExclamationSpriteTemplate']['asset'])
  for name,expected in [('sFacadeSquishAffineAnimCmds',[(-16,16,6),(16,-16,12),(-16,16,6)]),('sSmellingSaltsSquishAffineAnimCmds',[(0,-16,6),(0,16,6)])]:
   program=self.pack['taskAffinePrograms'][name];self.assertEqual(expected,[(c['x'],c['y'],c['duration']) for c in program[:-1]]);self.assertEqual('end',program[-1]['op'])
 def test_new_indexed_art_uses_registered_pixels_and_finger_palette_alias(self):
  metadata,pixels=imp.build_indexed_assets(REFERENCE);rgb=imp.build_assets(REFERENCE);rows={r['id']:r for r in metadata}
  for stem,symbol,size,shape,pal_symbol,pal_stem in imp.REACTION_MATERIALS:
   name=stem.replace('_','-');image=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png']));palette=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+pal_stem+'.png'])).getpalette();ordered=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()];self.assertEqual(shape,image.size)
   self.assertEqual(size,len(pixels[name+'.4bpp']));self.assertEqual(bytes(ordered[i]|ordered[i+1]<<4 for i in range(0,len(ordered),2)),pixels[name+'.4bpp']);self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)],list(struct.unpack('<16H',pixels[name+'.pal16'])));self.assertEqual([sum((palette[i*3+c]//8)<<(c*5) for c in range(3))+(32768 if i else 0) for i in image.tobytes()],list(struct.unpack_from('<'+str(image.width*image.height)+'H',rgb[name],4)));self.assertEqual('ANIM_TAG_'+stem.upper(),rows[name]['paletteTag'])
  self.assertFalse({'leaf','razor-leaf'}&{a['id'] for a in self.pack['assets']})
 def test_missing_callback_or_retained_arguments_leave_whole_roots_out(self):
  mutations=[(161,b'createsprite gTriAttackTriangleSpriteTemplate,',b'createsprite UnknownTriangle,'),(263,b'createvisualtask AnimTask_SquishAndSweatDroplets, 2, ANIM_ATTACKER, 3',b'createvisualtask AnimTask_SquishAndSweatDroplets, 2, ANIM_ATTACKER'),(265,b'createsprite gSmellingSaltsHandSpriteTemplate, ANIM_TARGET, 2, ANIM_TARGET, 0, 2',b'createsprite gSmellingSaltsHandSpriteTemplate, ANIM_TARGET, 2, ANIM_TARGET, 0'),(348,b'createvisualtask AnimTask_LeafBlade,',b'createvisualtask UnknownLeafBlade,')]
  for move,old,new in mutations:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']})
 def test_endeavor_retained_power_is_a_source_effect_gap_not_a_fake_receipt(self):
  self.assertNotIn(283,self.rows);self.assertEqual(['invocation:retained-move-power'],next(s['missing'] for s in self.blocked if s['moveId']==283))
  task=dict(op='createvisualtask',args=['AnimTask_ShakeTargetBasedOnMovePowerOrDmg','5','FALSE','0','6','1','0'])
  self.assertTrue(imp.requires_uncommitted_power('EFFECT_ENDEAVOR',[task],self.raw));self.assertFalse(imp.requires_uncommitted_power('EFFECT_HIT',[task],self.raw));task['args'][2]='TRUE';self.assertFalse(imp.requires_uncommitted_power('EFFECT_ENDEAVOR',[task],self.raw))
  raw=dict(self.raw);raw['data/battle_scripts_1.s']=raw['data/battle_scripts_1.s'].replace(b'BattleScript_EffectEndeavor::',b'BattleScript_EffectEndeavor::\n\tdamagecalc')
  with patch.object(imp,'read_sources',return_value=raw):
   with self.assertRaisesRegex(ValueError,'power-provenance'):imp.build(REFERENCE)
 def test_source_palette_registration_and_color_cycle_fail_closed(self):
  for path,old,new in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Finger, ANIM_TAG_TAG_HAND}',b'{gBattleAnimSpritePal_SweatDrop, ANIM_TAG_TAG_HAND}'),('src/graphics.c',b'"graphics/battle_anims/sprites/finger.gbapal.lz"',b'"graphics/battle_anims/sprites/tag_hand.gbapal.lz"'),('src/battle_anim_effects_3.c',b'RGB(28, 25,  1)',b'RGB(28, 25,  2)')]:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaisesRegex(ValueError,'Reaction|Facade'):imp.build(REFERENCE)
if __name__=='__main__':unittest.main()
