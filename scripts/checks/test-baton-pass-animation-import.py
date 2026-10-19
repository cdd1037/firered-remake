#!/usr/bin/env python3
"""Baton Pass source root, canonical indexed ball and shared palette checks."""
import argparse,io,json,re,struct,sys,types,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
from indexed_material_expectations import decode_indexed
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);p.add_argument('--importer',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';canonical=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
imp=types.ModuleType('baton_pass_import');imp.__file__=str(canonical);exec(compile((a.importer or canonical).read_text(),str(canonical),'exec'),imp.__dict__)
TEMPLATE='gBatonPassPokeballSpriteTemplate'
class BatonPassAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.blobs=imp.build(REFERENCE);cls.pack=json.loads(data);cls.rows={r['moveId']:r for r in cls.pack['scripts']}
 def test_exact_root_keeps_pre_party_selection_script_and_owned_sound(self):
  row=self.rows[226];self.assertEqual('Move_BATON_PASS',row['label']);self.assertEqual({'ANIM_TAG_POKEBALL':10226},row['spriteTags']);self.assertEqual({'SE_M_BATON_PASS':217},row['soundEffects'])
  self.assertEqual(['loadspritegfx','playsewithpan','createvisualtask','createsprite','end'],[c['op'] for c in row['commands']]);self.assertEqual(['AnimTask_BlendColorCycle','2','F_PAL_BG | F_PAL_BATTLERS','1','2','0','11','RGB(31, 22, 30)'],row['commands'][2]['args'])
  lines=imp.read(REFERENCE,'data/battle_anim_scripts.s').decode().splitlines()
  for c in row['commands']:
   op,*tail=lines[c['line']-1].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  source=imp.read(REFERENCE,'data/battle_scripts_1.s').decode();body=source.split('BattleScript_EffectBatonPass::',1)[1].split('\n\n',1)[0];self.assertLess(body.index('attackanimation'),body.index('openpartyscreen'))
 def test_recipe_and_indexed_pixels_match_original_palette_indices(self):
  r=next(r for r in self.pack['spriteRecipes'] if r['template']==TEMPLATE)
  self.assertEqual(('pokeball','AnimBatonPassPokeball',16,16,False,False),(r['asset'],r['callback'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([0],r['loopStarts']);self.assertEqual([],r['affineAnimations'])
  index=next(r for r in self.pack['indexedAssets'] if r['id']=='pokeball');self.assertEqual((16,16,'ANIM_TAG_POKEBALL'),(index['width'],index['height'],index['paletteTag']))
  with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/pokeball.png'))) as im:
   tiled=[v for y in range(0,16,8) for x in range(0,16,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];linear=list(im.tobytes());palette=im.getpalette()[:48]
  colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];packed=self.blobs['pokeball.4bpp'];self.assertEqual(tiled,[(packed[i//2]>>((i&1)*4))&15 for i in range(256)])
  self.assertEqual(colors,list(struct.unpack('<16H',self.blobs['pokeball.pal16'])));self.assertEqual(self.blobs['spotlight.pal16'],self.blobs['pokeball.pal16'])
  self.assertEqual(tuple(colors[i]|32768 if i else 0 for i in linear),decode_indexed(16,16,packed,self.blobs['pokeball.pal16']))
 def test_missing_callback_graphics_or_sound_omits_only_baton_pass(self):
  for key,value in [('TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=TEMPLATE}),('GFX',imp.GFX-{'ANIM_TAG_POKEBALL'}),('SOUNDS',imp.SOUNDS-{'SE_M_BATON_PASS'})]:
   with patch.object(imp,key,value):rows,_=imp.discover_scripts(REFERENCE)
   remaining={r['moveId']:r for r in rows};self.assertEqual({226},self.rows.keys()-remaining.keys());self.assertEqual({k:v for k,v in self.rows.items() if k!=226},remaining)
 def test_malformed_width_callback_and_material_registration_fail_closed(self):
  original=imp.read
  for extra in [True,False]:
   before=(TEMPLATE+', ANIM_ATTACKER, 2').encode();after=(TEMPLATE+', ANIM_ATTACKER'+(', 2, 0' if extra else '')).encode()
   def read(ref,path):
    data=original(ref,path);return data.replace(before,after) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp,'read',side_effect=read):rows,_=imp.discover_scripts(REFERENCE)
   self.assertNotIn(226,{r['moveId'] for r in rows})
  for path,before,after in [('src/battle_anim_effects_3.c',b'.callback = AnimBatonPassPokeball,',b'.callback = UnknownBatonPass,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Pokeball, 0x0080, ANIM_TAG_POKEBALL}',b'{gBattleAnimSpriteGfx_Pokeball, 0x0100, ANIM_TAG_POKEBALL}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_Pokeball, ANIM_TAG_POKEBALL}',b'{gBattleAnimSpritePal_Spotlight, ANIM_TAG_POKEBALL}'),('src/graphics.c',b'graphics/battle_anims/sprites/pokeball.4bpp.lz',b'graphics/battle_anims/sprites/spotlight.4bpp.lz')]:
   self.assertIn(before,original(REFERENCE,path))
   def read(ref,p):
    data=original(ref,p);return data.replace(before,after) if p==path else data
   with patch.object(imp,'read',side_effect=read),self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_generated_root_recipe_and_indexed_owner_equal_current_source(self):
  generated=json.loads((a.generated or ROOT/'content/generated/battle/animations/elemental.json').read_text())
  self.assertEqual(self.rows[226],next(r for r in generated['scripts'] if r['moveId']==226))
  for key,id in [('spriteRecipes','template'),('indexedAssets','id')]:
   name=TEMPLATE if id=='template' else 'pokeball';self.assertEqual(next(r for r in self.pack[key] if r[id]==name),next(r for r in generated[key] if r[id]==name))
if __name__=='__main__':unittest.main()
