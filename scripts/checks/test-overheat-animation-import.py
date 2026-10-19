#!/usr/bin/env python3
"""Pinned Overheat closure and shared palette buffer ownership import checks."""
import argparse,importlib.util,io,json,re,struct,unittest
from PIL import Image
from indexed_material_expectations import decode_indexed
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
spec=importlib.util.spec_from_file_location('overheat_import',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
class OverheatImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blobs=imp.build(REFERENCE);cls.data=json.loads(data);cls.row=next(r for r in cls.data['scripts'] if r['label']=='Move_OVERHEAT')
 def test_source_root_keeps_all_callbacks_delays_palette_targets_and_flame_rings(self):
  self.assertEqual(315,self.row['moveId']);self.assertEqual(79,len(self.row['commands']));self.assertEqual({'SE_M_DRAGON_RAGE','SE_M_FLAME_WHEEL2','SE_M_FIRE_PUNCH'},set(self.row['soundEffects']))
  c=self.row['commands'];flames=[x for x in c if x['op']=='createsprite' and x['args'][0]=='gOverheatFlameSpriteTemplate'];self.assertEqual(24,len(flames));self.assertEqual([str(i) for _ in range(3) for i in range(0,256,32)],[x['args'][4] for x in flames]);self.assertEqual(['-20']*8+['0']*8+['10']*8,[x['args'][7] for x in flames]);self.assertEqual(['2']*5+['66']*3,[x['args'][2] for x in flames[:8]])
  tasks=[x['args'] for x in c if x['op']=='createvisualtask' and x['args'][0] in imp.PALETTE_BACKUP_TASKS]
  self.assertEqual([['AnimTask_AllocBackupPalBuffer','5'],['AnimTask_CopyPalUnfadedToBackup','5','0','1'],['AnimTask_CopyPalFadedToUnfaded','5','0'],['AnimTask_CopyPalUnfadedToBackup','5','1','0'],['AnimTask_CopyPalFadedToUnfaded','5','1'],['AnimTask_CopyPalUnfadedFromBackup','5','0','1'],['AnimTask_CopyPalUnfadedFromBackup','5','1','0'],['AnimTask_FreeBackupPalBuffer','5']],tasks)
 def test_flame_reuses_exact_existing_small_ember_sheet_palette_and_frame_program(self):
  recipe=next(r for r in self.data['spriteRecipes'] if r['template']=='gOverheatFlameSpriteTemplate');self.assertEqual(('AnimOverheatFlame','small-ember',32,32,False,False),(recipe['callback'],recipe['asset'],recipe['width'],recipe['height'],recipe['blend'],recipe['affineDoubleSize']));self.assertEqual([0],recipe['loopStarts']);self.assertEqual([],recipe['affineAnimations']);self.assertEqual([{'tile':i*16,'duration':4,'hFlip':False,'vFlip':False} for i in range(5)],recipe['animations'][0])
  prior=json.loads((ROOT/'content/generated/battle/animations/elemental.json').read_text());old=next(r for r in prior['spriteRecipes'] if r['template']=='gOutrageFlameSpriteTemplate');self.assertEqual(old['animations'],recipe['animations']);self.assertEqual(old['loopStarts'],recipe['loopStarts'])
  for suffix in ['.4bpp','.pal16']:self.assertEqual((ROOT/('content/generated/battle/animations/elemental-small-ember'+suffix)).read_bytes(),self.blobs['small-ember'+suffix])
  with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/small_ember.png'))) as image:
   pixels=list(image.tobytes());pal=image.getpalette()[:48];indices=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
  packed=self.blobs['small-ember.4bpp'];colors=[sum((pal[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(indices,[(packed[i//2]>>((i&1)*4))&15 for i in range(len(indices))]);self.assertEqual(colors,list(struct.unpack('<16H',self.blobs['small-ember.pal16'])));self.assertEqual(tuple(colors[i]|32768 if i else 0 for i in pixels),decode_indexed(32,160,packed,self.blobs['small-ember.pal16']))
 def test_source_task_buffer_width_selection_size_and_callback_contracts_fail_closed(self):
  original=imp.read
  changes=[('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_SmallEmber, 0x0A00, ANIM_TAG_SMALL_EMBER}',b'{gBattleAnimSpriteGfx_SmallEmber, 0x0800, ANIM_TAG_SMALL_EMBER}'),('src/graphics.c',b'graphics/battle_anims/sprites/small_ember.gbapal.lz',b'graphics/battle_anims/sprites/fire.gbapal.lz'),('include/battle.h',b'u16 *multiUseBuffer;',b'u8 *multiUseBuffer;'),('src/battle_anim_mons.c',b'selectedPalettes = 0xe;',b'selectedPalettes = 0xc;'),('src/battle_anim_utility_funcs.c',b'AllocZeroed(0x2000)',b'AllocZeroed(0x1000)'),('src/battle_anim_utility_funcs.c',b'multiUseBuffer[gBattleAnimArgs[1] * 16]',b'multiUseBuffer[gBattleAnimArgs[1] * 8]'),('src/battle_anim_dragon.c',b'(gBattleAnimArgs[2] * 3) / 5',b'(gBattleAnimArgs[2] * 2) / 5'),('src/battle_anim_dragon.c',b'sprite->data[4] / 10',b'sprite->data[4] / 8'),('src/battle_anim_dragon.c',b'++sprite->data[0] > sprite->data[3]',b'++sprite->data[0] >= sprite->data[3]'),('src/battle_anim_dragon.c',b'.callback = AnimOverheatFlame,',b'.callback = SpriteCallbackDummy,')]
  for path,before,after in changes:
   self.assertIn(before,original(REFERENCE,path),path)
   def changed(reference,p):
    data=original(reference,p);return data.replace(before,after) if p==path else data
   with patch.object(imp,'read',side_effect=changed),self.assertRaises(ValueError,msg=path):imp.build(REFERENCE)
 def test_unknown_flame_dependency_omits_only_the_incomplete_source_root(self):
  original=imp.read;expected={r['moveId'] for r in self.data['scripts']}
  def changed(reference,path):
   data=original(reference,path);return data.replace(b'createsprite gOverheatFlameSpriteTemplate,',b'createsprite gUnreviewedHeatSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=changed):data,_=imp.build(REFERENCE)
  self.assertEqual(expected-{self.row['moveId']},{r['moveId'] for r in json.loads(data)['scripts']})
if __name__=='__main__':unittest.main()
