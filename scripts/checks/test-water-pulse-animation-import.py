#!/usr/bin/env python3
"""Water Pulse graph, raw-child recipe and source material dependencies."""
import importlib.util,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('water_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class WaterPulseImportTests(unittest.TestCase):
 def test_authored_root_retains_five_floats_three_counted_rings_and_shared_tasks(self):
  blob,_=imp.build(REFERENCE);pack=json.loads(blob);root=next(r for r in pack['scripts'] if r['moveId']==352)
  creates=[c for c in root['commands'] if c['op']=='createsprite']
  floats=[c for c in creates if c['args'][0]=='gWaterPulseBubbleSpriteTemplate'];rings=[c for c in creates if c['args'][0]=='gWaterPulseRingSpriteTemplate']
  self.assertEqual(5,len(floats));self.assertEqual(3,len(rings));self.assertTrue(all(c['args'][3:]==['0','0','40','15'] for c in rings))
  self.assertEqual(['0','1','0','0','1'],[c['args'][-1] for c in floats]);self.assertFalse(any(c['args'][0]=='gWaterPulseRingBubbleSpriteTemplate' for c in creates))
  self.assertEqual({'SE_M_BUBBLE3','SE_M_GIGA_DRAIN'},set(root['soundEffects']));self.assertEqual({'ANIM_TAG_SMALL_BUBBLES','ANIM_TAG_BLUE_RING_2'},set(root['spriteTags']))
 def test_raw_child_frames_affine_and_shared_pixels_preserve_exact_source(self):
  blob,pixels=imp.build(REFERENCE);recipes={r['template']:r for r in json.loads(blob)['spriteRecipes']}
  bubble=recipes['gWaterPulseBubbleSpriteTemplate'];child=recipes['gWaterPulseRingBubbleSpriteTemplate'];ring=recipes['gWaterPulseRingSpriteTemplate']
  self.assertEqual(('small-bubbles',8,8,[]),(bubble['asset'],bubble['width'],bubble['height'],bubble['affineAnimations']))
  self.assertEqual(bubble['animations'],child['animations']);self.assertEqual([8,9],[x[0]['tile'] for x in child['animations']]);self.assertEqual([-1,-1],child['loopStarts'])
  self.assertEqual([256,224],[p[0]['x'] for p in child['affineAnimations']]);self.assertEqual([-10,-8],[p[1]['x'] for p in child['affineAnimations']]);self.assertTrue(all(p[1]['duration']==15 and p[-1]['op']=='end' for p in child['affineAnimations']))
  self.assertEqual(('blue-ring-2',16,32,True),(ring['asset'],ring['width'],ring['height'],ring['affineDoubleSize']));self.assertEqual([5,-10,10,-10,10,-10,10],[c['x'] for c in ring['affineAnimations'][0][:-1]])
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/gold_ring.png') as im:indices=im.tobytes()
  palette=[int(c) for row in (REFERENCE/'graphics/battle_anims/sprites/blue_ring_2.pal').read_text().splitlines()[3:] for c in row.split()]
  expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))|(32768 if i else 0) for i in indices]
  self.assertEqual((16,32),struct.unpack_from('<HH',pixels['blue-ring-2']));self.assertEqual(expected,list(struct.unpack_from('<512H',pixels['blue-ring-2'],4)))
  data=imp.read(REFERENCE,'src/data/battle_anim.h').decode();gfx=imp.read(REFERENCE,'src/graphics.c').decode()
  self.assertRegex(data,r'\{gBattleAnimSpriteGfx_GoldRing,\s*0x0100,\s*ANIM_TAG_BLUE_RING_2\}');self.assertRegex(data,r'\{gBattleAnimSpritePal_BlueRing2,\s*ANIM_TAG_BLUE_RING_2\}')
  self.assertIn('gBattleAnimSpritePal_BlueRing2[] = INCBIN_U32("graphics/battle_anims/sprites/blue_ring_2.gbapal.lz")',gfx)
 def test_unknown_callback_or_recipe_dependency_is_not_silently_discovered(self):
  original=imp.read
  def read(ref,path):
   data=original(ref,path)
   return data.replace(b'gWaterPulseRingSpriteTemplate,',b'gMissingWaterPulseRingSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):blob,_=imp.build(REFERENCE)
  self.assertNotIn(352,{r['moveId'] for r in json.loads(blob)['scripts']})
  source='\n'.join(original(REFERENCE,'src/'+p).decode() for p in ['battle_anim_water.c','battle_anim_effects_2.c'])
  with self.assertRaises(ValueError):imp.recipe(source.replace('.callback = AnimWaterPulseRingBubble,','.callback = MissingWaterPulseChild,'),'gWaterPulseRingBubbleSpriteTemplate','AnimWaterPulseRingBubble')
if __name__=='__main__':unittest.main()
