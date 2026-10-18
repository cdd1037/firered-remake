#!/usr/bin/env python3
"""Complete water/root/status source dependency and material checks."""
import importlib.util,json,unittest,struct,io
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
spec=importlib.util.spec_from_file_location('water_root_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
NAMES={'gHydroCannonChargeSpriteTemplate','gHydroCannonBeamSpriteTemplate','gGuardRingSpriteTemplate','gIngrainRootSpriteTemplate','gIngrainOrbSpriteTemplate','gPoisonGasCloudSpriteTemplate'}
class WaterRootImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_four_complete_roots_follow_the_original_scripts(self):
  for move,label in [(139,'POISON_GAS'),(219,'SAFEGUARD'),(275,'INGRAIN'),(308,'HYDRO_CANNON')]:
   self.assertEqual('Move_'+label,self.roots[move]['label']);self.assertEqual('end',self.roots[move]['commands'][-1]['op'])
  self.assertEqual(6,sum(c['op']=='createsprite' for c in self.roots[139]['commands']))
  self.assertEqual(3,sum(c['op']=='createsprite' for c in self.roots[219]['commands']))
  self.assertEqual(7,sum(c['op']=='createsprite' for c in self.roots[275]['commands']))
  self.assertEqual(6,sum(c['op']=='call' and c['args']==['HydroCannonBeam'] for c in self.roots[308]['commands']))
  self.assertEqual(5,sum(c['op']=='createsprite' for c in self.pack['subroutines']['HydroCannonBeam']))
 def test_shared_frame_affine_and_cloud_material_metadata(self):
  self.assertEqual([3,0],[f['tile'] for f in self.recipes['gIngrainOrbSpriteTemplate']['animations'][0]])
  self.assertEqual([256,512],[p[0]['x'] for p in self.recipes['gGuardRingSpriteTemplate']['affineAnimations']])
  self.assertEqual([50,10,20,0],[c['duration'] for c in self.recipes['gHydroCannonChargeSpriteTemplate']['affineAnimations'][0]])
  self.assertEqual('purple-gas-cloud',self.recipes['gPoisonGasCloudSpriteTemplate']['asset'])
  self.assertEqual([0,8],[f['tile'] for f in self.recipes['gPoisonGasCloudSpriteTemplate']['animations'][0]])
 def test_guard_ring_pixels_are_exact_source_palette_quantization(self):
  with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/guard_ring.png'))) as image:
   self.assertEqual((64,32),image.size);pal=image.getpalette()[:48];indices=list(image.tobytes())
  blob=self.pixels['guard-ring'];self.assertEqual((64,32),struct.unpack('<HH',blob[:4]))
  expected=[(0x8000 if i else 0)|sum((pal[i*3+c]//8)<<(c*5) for c in range(3)) for i in indices]
  self.assertEqual(expected,list(struct.unpack('<2048H',blob[4:])))
 def test_adapter_removal_closes_exactly_four_roots_and_keeps_every_previous_row(self):
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):data,pixels=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({139,219,275,308},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for key,blob in pixels.items():self.assertEqual(blob,self.pixels[key])
 def test_wrong_guard_registration_and_template_callback_fail_closed(self):
  original=imp.read
  for path,before,after in [('src/data/battle_anim.h',b'{gBattleAnimSpritePal_GuardRing, ANIM_TAG_GUARD_RING}',b'{gBattleAnimSpritePal_Orbs, ANIM_TAG_GUARD_RING}'),('src/battle_anim_water.c',b'.callback = AnimHydroCannonCharge,',b'.callback = AnimHydroCannonBeam,')]:
   def changed(ref,name):
    data=original(ref,name);return data.replace(before,after) if name==path else data
   with patch.object(imp,'read',side_effect=changed):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_no_new_generic_or_unrelated_roots_from_primitive_admission(self):
  for move in [57,151,182,197,215,287,297,312,318,330,347,349]:self.assertNotIn(move,self.roots)
if __name__=='__main__':unittest.main()
