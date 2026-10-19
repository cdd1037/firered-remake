#!/usr/bin/env python3
"""Exact source roots, offset frame tables, raw-child closure and indexed materials."""
import importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
from indexed_material_expectations import decode_indexed
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('impact_emission_import',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ImpactEmissionImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads(imp.build(REFERENCE)[0]);cls.rows={s['moveId']:s for s in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_four_complete_roots_have_exact_source_scripts_children_and_load_only_material(self):
  self.assertTrue({6,65,140,238}<=set(self.rows));self.assertNotIn(348,self.rows)
  self.assertEqual(['gFlashingHitSplatSpriteTemplate'],self.rows[65]['spriteDependencies']);self.assertEqual(['gBarrageBallSpriteTemplate'],self.rows[140]['spriteDependencies']);self.assertEqual(['whirlwind-lines'],self.rows[65]['materialDependencies'])
  for move,creates,tasks in [(6,3,1),(65,4,2),(140,1,3),(238,4,1)]:
   commands=self.rows[move]['commands'];self.assertEqual(creates,sum(c['op']=='createsprite' for c in commands));self.assertEqual(tasks,sum(c['op']=='createvisualtask' for c in commands));self.assertEqual('end',commands[-1]['op']);self.assertFalse(any(c['op'] in {'call','goto','jumpargeq','jumpifmoveturn','choosetwoturnanim'} for c in commands))
  self.assertEqual(['ANIM_TAG_IMPACT','ANIM_TAG_WHIRLWIND_LINES'],[c['args'][0] for c in self.rows[65]['commands'] if c['op']=='loadspritegfx'])
  self.assertEqual({'SE_M_SWAGGER','SE_M_STRENGTH'},set(self.rows[140]['soundEffects']))
  self.assertEqual({'SE_M_RAZOR_WIND2','SE_M_PAY_DAY'},set(self.rows[6]['soundEffects']))
 def test_offset_frame_table_does_not_include_unrelated_fist_and_foot_frames(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']};hand=recipes['gCrossChopHandSpriteTemplate']
  self.assertEqual([[dict(tile=48,duration=1,hFlip=False,vFlip=False)],[dict(tile=48,duration=1,hFlip=True,vFlip=False)]],hand['animations']);self.assertEqual([-1,-1],hand['loopStarts'])
  self.assertEqual(('cross-impact',True),(recipes['gCrossImpactSpriteTemplate']['asset'],recipes['gCrossImpactSpriteTemplate']['blend']))
  self.assertEqual('SpriteCallbackDummy',recipes['gBarrageBallSpriteTemplate']['callback']);self.assertEqual(2,len(recipes['gBarrageBallSpriteTemplate']['affineAnimations']))
  self.assertEqual(4,len(recipes['gFlashingHitSplatSpriteTemplate']['affineAnimations']))
  for name in ['gCoinThrowSpriteTemplate','gFallingCoinSpriteTemplate']:
   self.assertEqual([[dict(tile=8,duration=1,hFlip=False,vFlip=False)]],recipes[name]['animations']);self.assertEqual((16,16),(recipes[name]['width'],recipes[name]['height']))
 def test_indexed_materials_preserve_tile_order_all_palette_entries_and_rgb555(self):
  metadata,pixels=imp.build_indexed_assets(REFERENCE);metadata=[r for r in metadata if r['id'] in {'coin','cross-impact','red-ball','whirlwind-lines'}]
  self.assertEqual({'coin','cross-impact','red-ball','whirlwind-lines'},{r['id'] for r in metadata})
  for row in metadata:
   name=row['id'];stem=name.replace('-','_');image=Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png']));palette=image.getpalette();indices=list(image.tobytes());ordered=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
   self.assertEqual(bytes(ordered[i]|ordered[i+1]<<4 for i in range(0,len(ordered),2)),pixels[name+'.4bpp'])
   self.assertEqual([sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)],list(struct.unpack('<16H',pixels[name+'.pal16'])))
   self.assertEqual(image.size,(row['width'],row['height']));self.assertEqual([(sum((palette[i*3+c]//8)<<(5*c) for c in range(3))|32768) if i else 0 for i in indices],list(decode_indexed(image.width,image.height,pixels[name+'.4bpp'],pixels[name+'.pal16'])))
   self.assertEqual('ANIM_TAG_'+stem.upper(),row['paletteTag']);self.assertEqual('physical-'+name+'.4bpp',row['indicesPath']);self.assertEqual('physical-'+name+'.pal16',row['palettePath'])
 def test_unknown_callback_or_graphics_dependency_keeps_entire_root_out(self):
  for move,old,new in [(65,b'createvisualtask AnimTask_DrillPeckHitSplats,',b'createvisualtask UnknownDrillTask,'),(140,b'createvisualtask AnimTask_BarrageBall,',b'createvisualtask UnknownBarrageTask,'),(6,b'createsprite gCoinThrowSpriteTemplate,',b'createsprite UnknownCoin,'),(238,b'createsprite gCrossImpactSpriteTemplate,',b'createsprite UnknownCross,'),(65,b'loadspritegfx ANIM_TAG_WHIRLWIND_LINES',b'loadspritegfx ANIM_TAG_UNKNOWN_WHIRLWIND')]:
   raw=dict(self.raw);self.assertIn(old,raw['data/battle_anim_scripts.s']);raw['data/battle_anim_scripts.s']=raw['data/battle_anim_scripts.s'].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):data,blocked=imp.build(REFERENCE)
   self.assertNotIn(move,{s['moveId'] for s in json.loads(data)['scripts']})
   gaps=[b for b in blocked if b['moveId']==move]
   if gaps:self.assertTrue(gaps[0]['missing'])
 def test_wrong_offset_and_source_graphics_or_palette_registration_fail_closed(self):
  for path,old,new in [('src/battle_anim_fight.c',b'.anims = &sAnims_HandsAndFeet[3],',b'.anims = &sAnims_HandsAndFeet[2],'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Coin, 0x0200, ANIM_TAG_COIN}',b'{gBattleAnimSpriteGfx_Coin, 0x0100, ANIM_TAG_COIN}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_WhirlwindLines, ANIM_TAG_WHIRLWIND_LINES}',b'{gBattleAnimSpritePal_Coin, ANIM_TAG_WHIRLWIND_LINES}'),('src/graphics.c',b'"graphics/battle_anims/sprites/red_ball.gbapal.lz"',b'"graphics/battle_anims/sprites/coin.gbapal.lz"')]:
   raw=dict(self.raw);self.assertIn(old,raw[path]);raw[path]=raw[path].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):
     imp.build(REFERENCE) if path.startswith('src/battle_anim_') else imp.build_indexed_assets(REFERENCE)
 def test_generated_metadata_has_no_root_allowlist_or_raw_callback_runtime_dependency(self):
  self.assertNotIn('Move_DRILL_PECK',imp.SPRITES);self.assertNotIn(65,imp.NEW)
  for row in self.pack['indexedAssets']:
   self.assertNotIn('/',row['indicesPath']);self.assertNotIn('/',row['palettePath'])
if __name__=='__main__':unittest.main()
