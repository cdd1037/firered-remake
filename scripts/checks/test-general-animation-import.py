#!/usr/bin/env python3
import importlib.util,json,struct,unittest
from PIL import Image
from indexed_material_expectations import decode_indexed
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('general_import',ROOT/'scripts/imports/battle/import-source-general-animations.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class GeneralImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data,cls.blobs=module.build(ROOT/'content/raw/pokefirered/037335f4');cls.pack=json.loads(cls.data)
 def test_named_root_has_no_move_id(self):
  self.assertEqual([r['label'] for r in self.pack['generalScripts']],['General_HeldItemEffect','General_WishHeal','General_CastformChange','Special_SubstituteToMon','Special_MonToSubstitute','General_Rain','General_FutureSightHit','General_DoomDesireHit','General_FocusPunchSetUp','General_Sun','General_Sandstorm','General_Hail'])
  for root in self.pack['generalScripts']:self.assertNotIn('moveId',root);self.assertEqual(root['commands'][-1]['op'],'goto' if root.get('programReferences') else 'end')
 def test_substitute_specials_keep_exact_shared_callback_and_direction(self):
  rows={r['label']:r for r in self.pack['generalScripts']}
  for label,arg in [('Special_SubstituteToMon','TRUE'),('Special_MonToSubstitute','FALSE')]:
   root=rows[label]
   self.assertEqual([(c['op'],c['args']) for c in root['commands']],[('createvisualtask',['AnimTask_SwapMonSpriteToFromSubstitute','2',arg]),('end',[])])
   self.assertEqual(root['soundEffects'],{});self.assertEqual(root['spriteTags'],{})
 def test_exact_star_script_operands(self):
  rows=self.pack['subroutines']['GrantingStarsEffect'];self.assertEqual([r['args'][3:] for r in rows if r['op']=='createsprite'],[['-15','0','0','0','32','60'],['12','-5','0','0','32','60']])
 def test_native_sheet_shapes(self):
  self.assertEqual(struct.unpack('<HH',self.blobs['general-sparkle-2.rgb555'][:4]),(32,256))
  row=next(r for r in self.pack['indexedAssets'] if r['id']=='thin-ring');self.assertEqual((64,64),(row['width'],row['height']))
  with Image.open(ROOT/'content/raw/pokefirered/037335f4/graphics/battle_anims/sprites/thin_ring.png') as image:
   indices=image.tobytes();palette=image.getpalette()[:48]
  colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
  self.assertEqual(struct.pack('<16H',*colors),self.blobs[row['palettePath']])
  self.assertEqual(tuple(colors[i]|32768 if i else 0 for i in indices),decode_indexed(64,64,self.blobs[row['indicesPath']],self.blobs[row['palettePath']]))
  self.assertFalse(any(r['id']=='thin-ring' for r in self.pack['assets']))
 def test_shared_recipe_programs(self):
  star,ring=self.pack['spriteRecipes'];self.assertEqual(star['loopStarts'],[0]);self.assertEqual([f['tile'] for f in star['animations'][0]],list(range(0,128,16)));self.assertTrue(ring['affineDoubleSize']);self.assertEqual(ring['affineAnimations'][0][-1]['op'],'end');self.assertEqual(ring['affineAnimations'][0][1]['duration'],30)
 def test_only_actual_sound_constants(self):
  self.assertEqual(set(self.pack['generalScripts'][0]['soundEffects']),{'SE_M_TAKE_DOWN','SE_M_MORNING_SUN','SE_SHINY'})
 def test_wish_general_uses_exact_palette_stars_and_healing_program(self):
  root=self.pack['generalScripts'][1];commands=root['commands']
  self.assertEqual([c['args'][0] for c in commands if c['op']=='loadspritegfx'],['ANIM_TAG_BLUE_STAR','ANIM_TAG_SPARKLE_2'])
  self.assertEqual([c['args'][0] for c in commands if c['op']=='call'],['GrantingStarsEffect','HealingEffect'])
  self.assertEqual([c['args'][3:] for c in commands if c['op']=='createsprite'],[['F_PAL_BG','3','0','10','RGB_BLACK'],['F_PAL_BG','3','10','0','RGB_BLACK']])
  self.assertEqual(set(root['soundEffects']),{'SE_M_MEGA_KICK','SE_M_ABSORB_2'})
  healing=self.pack['subroutines']['HealingEffect']
  self.assertEqual([c['args'][3:] for c in healing if c['op']=='createsprite'],[['0','-5','0','0'],['-15','10','0','0'],['-15','-15','0','0'],['10','-5','0','0']])
  self.assertEqual([c['args'] for c in healing if c['op']=='delay'],[['7']]*4)
 def test_general_wish_has_no_move_root_or_new_material_copy(self):
  self.assertEqual(set(self.blobs),{'general-sparkle-2.rgb555','general-thin-ring.4bpp','general-thin-ring.pal16'})
  self.assertNotIn('moveId',self.pack['generalScripts'][1])
 def test_castform_branch_preserves_true_operand_labels_and_both_source_paths(self):
  root=self.pack['generalScripts'][2];rows=root['commands']
  self.assertEqual(root['labels'],{'General_CastformChange':0,'CastformChangeContinue':3,'CastformChangeSkipAnim':10})
  self.assertEqual(rows[1]['args'],['7','TRUE','CastformChangeSkipAnim']);self.assertEqual(rows[1]['op'],'jumpargeq')
  self.assertEqual(rows[6]['args'],['AnimTask_TransformMon','2','1']);self.assertEqual(rows[10]['args'],['AnimTask_CastformGfxChange','2','1'])
  self.assertEqual(root['soundEffects'],{'SE_M_MINIMIZE':197,'SE_M_TELEPORT':196})
  self.assertEqual(rows[5]['args'],['SE_M_MINIMIZE','SOUND_PAN_ATTACKER','48'])
 def test_branched_general_parser_rejects_unknown_missing_cyclic_and_unreachable_source(self):
  cases=['General_Test:\n goto Missing\n end\n',
   'General_Test:\n goto General_Test\n',
   'General_Test:\n end\n delay 1\n',
   'General_Test:\n mystery\n end\n',
   'General_Test:\n monbg ANIM_ATTACKER\n']
  for source in cases:
   with self.subTest(source=source),self.assertRaises(ValueError):module.branched_program(source,'General_Test')
 def test_general_compiler_ends_at_next_named_general_root(self):
  rows,labels=module.branched_program('General_Test:\n jumpreteq TRUE, Hidden\n end\nHidden:\n end\nGeneral_Next:\n mystery\n','General_Test')
  self.assertEqual(len(rows),3);self.assertEqual(labels,{'General_Test':0,'Hidden':2})
if __name__=='__main__':unittest.main()
