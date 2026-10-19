#!/usr/bin/env python3
"""Source closure, shared ownership and material/ABI boundaries for Sky Attack/Spit Up."""
import importlib.util,io,json,re,struct,unittest
from PIL import Image
from pathlib import Path
from indexed_material_expectations import decode_indexed
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
s=importlib.util.spec_from_file_location('charged_root_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py');imp=importlib.util.module_from_spec(s);s.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ChargedRootAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=imp.read_sources(REFERENCE)
 def test_two_complete_source_roots_and_all_reachable_commands(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in (143,255):
   root=self.rows[move];commands=root['commands']+[c for row in root['commands'] if row['op']=='call' for c in self.pack['subroutines'][row['args'][0]]]
   for c in commands:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op in {'jumpretfalse','jumprettrue'}:args=['7','0' if op=='jumpretfalse' else '1',args[0]];op='jumpargeq'
    self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(1,sum(c['op']=='choosetwoturnanim' for c in self.rows[143]['commands']));self.assertEqual(2,sum(c['op']=='jumpifmoveturn' for c in self.rows[255]['commands']))
  self.assertEqual({'SetSkyBg','UnsetSkyBg'},{c['args'][0] for c in self.rows[143]['commands'] if c['op']=='call'});self.assertEqual(18,self.pack['backgroundSymbols']['BG_SKY'])
  self.assertIn(76,self.rows);self.assertNotIn(177,self.rows)
 def test_spit_up_retained_duration_is_kept_in_original_branch_text(self):
  orbs=[c for c in self.rows[255]['commands'] if c['op']=='createsprite' and c['args'][0]=='gSpitUpOrbSpriteTemplate']
  self.assertEqual(20,len(orbs));self.assertEqual(8,sum(len(c['args'])==5 for c in orbs));self.assertEqual(12,sum(len(c['args'])==4 for c in orbs));self.assertEqual({'12'},{c['args'][4] for c in orbs if len(c['args'])==5})
 def test_materials_preserve_gray_orb_alias_and_original_bird_shape(self):
  indexed,pixels=imp.build_indexed_assets(REFERENCE)
  for name,size,tilebytes in [('bird',(64,64),2048),('red-orb-2',(8,24),96)]:
   row=next(r for r in indexed if r['id']==name);self.assertEqual(size,(row['width'],row['height']));self.assertEqual(tilebytes,len(pixels[name+'.4bpp']));self.assertEqual(32,len(pixels[name+'.pal16']));self.assertEqual(1,sum(r['id']==name for r in indexed))
   stem='gray_orb' if name=='red-orb-2' else name
   with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png'])) as image:palette=image.getpalette()[:48];raster=list(image.tobytes())
   if name=='red-orb-2':palette=[int(c) for line in self.raw['graphics/battle_anims/sprites/red_orb_2.pal'].decode().splitlines()[3:] for c in line.split()]
   colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
   self.assertEqual(struct.pack('<16H',*colors),pixels[name+'.pal16']);self.assertEqual(tuple((colors[i]|32768) if i else 0 for i in raster),decode_indexed(*size,pixels[name+'.4bpp'],pixels[name+'.pal16']))
  self.assertEqual(pixels['gray-orb.4bpp'],pixels['red-orb-2.4bpp']);self.assertNotEqual(pixels['gray-orb.pal16'],pixels['red-orb-2.pal16'])
  recipes={r['template']:r for r in self.pack['spriteRecipes']};bird=recipes['gSkyAttackBirdSpriteTemplate'];orb=recipes['gSpitUpOrbSpriteTemplate']
  self.assertEqual(('bird','AnimSkyAttackBird',64,64,True,[]),(bird['asset'],bird['callback'],bird['width'],bird['height'],bird['affineDoubleSize'],bird['affineAnimations']))
  self.assertEqual((8,8,'red-orb-2'),(orb['width'],orb['height'],orb['asset']));self.assertEqual([128,8,0],[c['x'] for c in orb['affineAnimations'][0]])
 def test_missing_callback_or_task_blocks_entire_root(self):
  for move,name,collection in [(143,'gSkyAttackBirdSpriteTemplate','SPRITES'),(255,'gSpitUpOrbSpriteTemplate','SPRITES'),(143,'AnimTask_GetTargetIsAttackerPartner','TASKS'),(143,'AnimTask_AttackerFadeFromInvisible','TASKS'),(255,'AnimTask_SpitUpDeformMon','TASKS')]:
   with patch.object(imp,collection,getattr(imp,collection)-{name}):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('callback:'+name,next(r['missing'] for r in blocked if r['moveId']==move))
 def test_changed_callback_and_registered_material_fail_closed(self):
  for file,old,new in [('src/battle_anim_effects_2.c',b'if (sprite->data[3]++ >= sprite->data[2])',b'if (++sprite->data[3] >= sprite->data[2])'),('src/battle_anim_flying.c',b'sprite->x > DISPLAY_WIDTH + 45',b'sprite->x > DISPLAY_WIDTH + 44'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_Bird, 0x0800,',b'{gBattleAnimSpriteGfx_Bird, 0x0400,'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_RedOrb2, ANIM_TAG_RED_ORB_2}',b'{gBattleAnimSpritePal_GrayOrb, ANIM_TAG_RED_ORB_2}')]:
   raw=dict(self.raw);self.assertIn(old,raw[file]);raw[file]=raw[file].replace(old,new)
   with patch.object(imp,'read_sources',return_value=raw):
    with self.assertRaises(ValueError):imp.build(REFERENCE)
 def test_bad_effect_and_abi_never_admit_a_partial_root(self):
  for move,symbol,effect in [(143,'SKY_ATTACK','EFFECT_SKY_ATTACK'),(255,'SPIT_UP','EFFECT_SPIT_UP')]:
   raw=dict(self.raw);text=raw['src/data/battle_moves.h'].decode();block=re.search(r'\[MOVE_'+symbol+r'\].*?\n    \},',text,re.S)[0];raw['src/data/battle_moves.h']=text.replace(block,block.replace(effect,'EFFECT_HIT')).encode()
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertIn('invocation:move-turn',next(r['missing'] for r in blocked if r['moveId']==move))
  for move,name in [(143,'gSkyAttackBirdSpriteTemplate'),(255,'gSpitUpOrbSpriteTemplate')]:
   raw=dict(self.raw);text=raw['data/battle_anim_scripts.s'].decode();line=re.search(r'createsprite '+name+r',[^\n]+',text)[0];raw['data/battle_anim_scripts.s']=text.replace(line,line+', 99',1).encode()
   with patch.object(imp,'read_sources',return_value=raw):
    data,blocked=imp.build(REFERENCE);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_regeneration_is_exact(self):self.assertEqual(self.data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
