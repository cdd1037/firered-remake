#!/usr/bin/env python3
"""Psycho Boost dependency and source recipe checks; does not write generated content."""
import argparse,json,sys,types,unittest
from pathlib import Path
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--staged',type=Path);ARGS,REST=p.parse_known_args();ROOT=ARGS.root

def load(name,file):
 rel=Path('scripts/imports/battle')/file;path=(ARGS.staged or ROOT)/rel;m=types.ModuleType(name);m.__file__=str(ROOT/rel);exec(compile(path.read_text(),str(path),'exec'),m.__dict__);return m

class PsychoBoostImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.raw=ROOT/'content/raw/pokefirered/037335f4';cls.elemental=load('psycho_elemental','import-source-elemental-animations.py');cls.background=load('psycho_background','import-source-background-scripts.py');cls.background.elemental=cls.elemental
  cls.pack=json.loads(cls.background.build(cls.raw));cls.row=next(r for r in cls.pack['scripts'] if r['moveId']==354);cls.source=(cls.raw/'src/battle_anim_psychic.c').read_text();cls.recipe=cls.elemental.recipe(cls.source,'gPsychoBoostOrbSpriteTemplate','AnimPsychoBoost')
 def test_root_is_complete_and_contains_callback_only_sound(self):
  self.assertEqual('Move_PSYCHO_BOOST',self.row['label']);self.assertEqual({'SE_M_LEER':185,'SE_M_PSYBEAM2':193,'SE_M_TELEPORT':196},self.row['soundEffects']);self.assertEqual(['UnsetPsychicBackground'],[c['args'][0] for c in self.row['commands'] if c['op']=='call']);self.assertIn('UnsetPsychicBackground',self.pack['subroutines']);self.assertEqual(3,self.pack['backgroundSymbols']['BG_PSYCHIC'])
 def test_recipe_keeps_local_indexed_material_and_both_native_affine_programs(self):
  r=self.recipe;self.assertEqual(('circle-of-light',64,64,True,True),(r['asset'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([0],r['loopStarts']);self.assertEqual([11,2],[len(p) for p in r['affineAnimations']]);self.assertEqual([0,4,0,7],[c['target'] for c in r['affineAnimations'][0] if c['op']=='loop']);self.assertEqual((-20,24,15),tuple(r['affineAnimations'][1][0][k] for k in ['x','y','duration']))
 def test_unreviewed_affine_restart_cannot_masquerade_as_change(self):
  original=self.elemental.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'ChangeSpriteAffineAnim(sprite, 1);',b'StartSpriteAffineAnim(sprite, 1);') if path=='src/battle_anim_psychic.c' else data
  self.elemental.read=read
  try:
   with self.assertRaisesRegex(ValueError,'retained affine handoff'):self.background.build(self.raw)
  finally:self.elemental.read=original
 def test_missing_or_changed_callback_sound_is_rejected(self):
  original=self.elemental.read
  def read(reference,path):
   data=original(reference,path)
   return data.replace(b'PlaySE12WithPanning(SE_M_TELEPORT, BattleAnimAdjustPanning(-64));',b'PlaySE12WithPanning(SE_M_LEER, BattleAnimAdjustPanning(-64));') if path=='src/battle_anim_psychic.c' else data
  self.elemental.read=read
  try:
   with self.assertRaisesRegex(ValueError,'callback sound'):self.background.build(self.raw)
  finally:self.elemental.read=original

if __name__=='__main__':unittest.main(argv=[sys.argv[0],*REST])
