#!/usr/bin/env python3
import importlib.util,json,struct,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('general_import',ROOT/'scripts/imports/battle/import-source-general-animations.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class GeneralImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.data,cls.blobs=module.build(ROOT/'content/raw/pokefirered/037335f4');cls.pack=json.loads(cls.data)
 def test_named_root_has_no_move_id(self):
  self.assertEqual([r['label'] for r in self.pack['generalScripts']],['General_HeldItemEffect','General_WishHeal'])
  for root in self.pack['generalScripts']:self.assertNotIn('moveId',root);self.assertEqual(root['commands'][-1]['op'],'end')
 def test_exact_star_script_operands(self):
  rows=self.pack['subroutines']['GrantingStarsEffect'];self.assertEqual([r['args'][3:] for r in rows if r['op']=='createsprite'],[['-15','0','0','0','32','60'],['12','-5','0','0','32','60']])
 def test_native_sheet_shapes(self):
  self.assertEqual(struct.unpack('<HH',self.blobs['general-sparkle-2.rgb555'][:4]),(32,256));self.assertEqual(struct.unpack('<HH',self.blobs['general-thin-ring.rgb555'][:4]),(64,64))
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
  self.assertEqual(set(self.blobs),{'general-sparkle-2.rgb555','general-thin-ring.rgb555'})
  self.assertNotIn('moveId',self.pack['generalScripts'][1])
if __name__=='__main__':unittest.main()
