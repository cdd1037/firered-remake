import importlib.util,io,json,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('horn',ROOT/'scripts/imports/battle/import-source-horn-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class HornImportTests(unittest.TestCase):
 def test_complete_roots_and_folded_contest_branch_relocation(self):
  data,_=imp.build(REFERENCE);pack=json.loads(data);scripts={r['moveId']:r for r in pack['scripts']};self.assertEqual({30,31,32,224},scripts.keys())
  drill=scripts[32];self.assertEqual(5,drill['labels']['HornDrillContinue']);self.assertEqual('waitbgfadein',drill['commands'][5]['op']);self.assertNotIn('jumpifcontest',[c['op'] for c in drill['commands']]);self.assertNotIn('fireRedContestTargets',drill);self.assertEqual({'BG_DRILL':7},pack['backgroundSymbols'])
  self.assertEqual(['SE_BANG'],[c['args'][0] for c in drill['commands'] if c['op']=='playse']);self.assertIn('SE_BANG',drill['soundEffects'])
  self.assertEqual({'FuryAttackRight':4,'FuryAttackLeft':9,'FuryAttackContinue':14},scripts[31]['labels'])
 def test_missing_dead_target_excludes_incomplete_root(self):
  original=imp.shared.read
  def read(ref,path):
   data=original(ref,path);return data.replace(b'HornDrillInContest:\n',b'DeletedContestLabel:\n') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp.shared,'read',side_effect=read):self.assertEqual({30,31,224},{r['moveId'] for r in json.loads(imp.build(REFERENCE)[0])['scripts']})
 def test_source_engine_branch_change_is_rejected(self):
  original=imp.shared.read
  def read(ref,path):
   data=original(ref,path);return data.replace(b'static void Cmd_jumpifcontest(void)\n{\n    sBattleAnimScriptPtr += 5;',b'static void Cmd_jumpifcontest(void)\n{\n    sBattleAnimScriptPtr += 6;') if path=='src/battle_anim.c' else data
  with patch.object(imp.shared,'read',side_effect=read):
   with self.assertRaisesRegex(ValueError,'engine contest-branch invariant'):imp.build(REFERENCE)
 def test_indexed_material_matches_all_source_pixels(self):
  _,blobs=imp.build(REFERENCE);tiles=blobs['horn-hit.4bpp'];palette=struct.unpack('<16H',blobs['horn-hit.pal16'])
  with Image.open(io.BytesIO(imp.shared.read(REFERENCE,'graphics/battle_anims/sprites/horn_hit.png'))) as image:
   colors=image.getpalette()
   for y in range(32):
    for x in range(32):
     offset=((y//8)*4+x//8)*32+(y%8)*4+(x%8)//2;index=(tiles[offset]>>(4*(x%2)))&15;self.assertEqual(image.getpixel((x,y)),index);self.assertEqual(sum((colors[index*3+c]//8)<<(5*c) for c in range(3)),palette[index])
 def test_megahorn_reuses_folding_and_retains_affine_source_selection(self):
  data,blobs=imp.build(REFERENCE);pack=json.loads(data);row=next(r for r in pack['scripts'] if r['moveId']==224);self.assertEqual(7,row['labels']['MegahornContinue']);self.assertEqual('waitbgfadein',row['commands'][7]['op']);self.assertNotIn('jumpifcontest',[c['op'] for c in row['commands']]);recipe=next(r for r in pack['spriteRecipes'] if r['callback']=='AnimMegahornHorn');self.assertTrue(recipe['affineDoubleSize']);self.assertEqual([30,157,94],[a[0]['rotation'] for a in recipe['affineAnimations']])
  with Image.open(io.BytesIO(imp.shared.read(REFERENCE,'graphics/battle_anims/sprites/horn_hit_2.png'))) as image:
   colors=image.getpalette();palette=struct.unpack('<16H',blobs['horn-hit-2.pal16']);tiles=blobs['horn-hit-2.4bpp']
   for y in range(16):
    for x in range(32):
     offset=((y//8)*4+x//8)*32+(y%8)*4+(x%8)//2;index=(tiles[offset]>>(4*(x%2)))&15;self.assertEqual(image.getpixel((x,y)),index);self.assertEqual(sum((colors[index*3+c]//8)<<(5*c) for c in range(3)),palette[index])
if __name__=='__main__':unittest.main()
