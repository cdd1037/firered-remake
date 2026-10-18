"""Focus Punch's original hit root, separate setup, and one shared fist recipe."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
def module(name,relative,candidate=False):
 spec=importlib.util.spec_from_file_location(name,ROOT/relative);m=importlib.util.module_from_spec(spec)
 if candidate and a.candidate:exec(compile((a.candidate/relative).read_text(),str(ROOT/relative),'exec'),m.__dict__)
 else:spec.loader.exec_module(m)
 return m
physical=module('focus_physical','scripts/imports/battle/import-source-physical-animations.py',True)
background=module('focus_background','scripts/imports/battle/import-source-background-scripts.py');background.physical=physical
TEMPLATE='gFocusPunchFistSpriteTemplate'
class FocusPunchImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.physical_data,_=physical.build(RAW);cls.pack=json.loads(cls.physical_data);cls.background_data=background.build(RAW);cls.bg=json.loads(cls.background_data)
  cls.row=next(s for s in cls.bg['scripts'] if s['moveId']==264);cls.raw=physical.read_sources(RAW);cls.source=cls.raw['src/battle_anim_fight.c'].decode()
 def test_only_callback_capability_admits_complete_root_and_no_existing_root_changes(self):
  with patch.object(physical,'SPRITES',physical.SPRITES-{TEMPLATE}):prior=json.loads(background.build(RAW))
  old={s['moveId']:s for s in prior['scripts']};new={s['moveId']:s for s in self.bg['scripts']};self.assertEqual({264},new.keys()-old.keys())
  for key in old:self.assertEqual(old[key],new[key])
  self.assertEqual({k:v for k,v in prior.items() if k!='scripts'},{k:v for k,v in self.bg.items() if k!='scripts'})
  self.assertNotIn(264,{s['moveId'] for s in self.pack['scripts']});self.assertIn(158,old);self.assertEqual(1,sum(s['moveId']==158 for s in self.bg['scripts']))
 def test_root_closure_preserves_original_lines_and_only_folds_dead_contest_comparison(self):
  row=self.row;self.assertEqual('Move_FOCUS_PUNCH',row['label']);self.assertEqual(37,len(row['commands']))
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for c in row['commands']:
   text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op in {'jumprettrue','jumpretfalse'}:args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
   self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual({'FocusPunch','FocusPunchContinue','FocusPunchEnd','FocusPunchOnOpponent','FocusPunchOnPlayer'},set(row['labels']))
  self.assertNotIn('FocusPunchInContest',row['labels']);self.assertNotIn(9033,[c['line'] for c in row['commands']]);self.assertIn(['AnimTask_IsContest','2'],[c['args'] for c in row['commands']]);self.assertIn(['AnimTask_IsTargetPlayerSide','2'],[c['args'] for c in row['commands']])
  self.assertEqual({'BG_IMPACT_OPPONENT','BG_IMPACT_PLAYER'},{c['args'][0] for c in row['commands'] if c['op']=='fadetobg'})
  self.assertEqual(4,sum(c['op']=='createsprite' and c['args'][0]=='gBasicHitSplatSpriteTemplate' for c in row['commands']))
  self.assertEqual({'ANIM_TAG_HANDS_AND_FEET':10143,'ANIM_TAG_IMPACT':10135},row['spriteTags'])
 def test_existing_materials_and_original_hand_frame_table_are_reused(self):
  recipe=next(r for r in self.pack['spriteRecipes'] if r['template']==TEMPLATE);self.assertEqual('hands-and-feet',recipe['asset']);self.assertEqual([0,16,32,48,48],[a[0]['tile'] for a in recipe['animations']]);self.assertEqual([False,False,False,False,True],[a[0]['hFlip'] for a in recipe['animations']]);self.assertTrue(recipe['affineDoubleSize'])
  self.assertEqual([512,-32,0],[r['x'] for r in recipe['affineAnimations'][0]]);self.assertEqual([0,8,0],[r['duration'] for r in recipe['affineAnimations'][0]])
  generated=ROOT/'content/generated/battle/animations'
  for key,blob in {**physical.build_assets(RAW),**physical.build_indexed_assets(RAW)[1]}.items():
   name='physical-'+key if key.endswith(('.4bpp','.pal16')) else 'physical-'+key+'.rgb555'
   self.assertEqual(blob,(generated/name).read_bytes(),name)
 def test_callback_and_affine_source_contract_rejects_wrong_semantics(self):
  for old,new in [('sprite->data[1] + 40','sprite->data[1] + 41'),('sprite->data[0] > 40','sprite->data[0] > 39'),('Sin(sprite->data[1], 2)','Sin(sprite->data[1], 3)'),('AFFINEANIMCMD_FRAME(-0x20, -0x20, 0, 8)','AFFINEANIMCMD_FRAME(-0x10, -0x20, 0, 8)'),('.callback = AnimFocusPunchFist,','.callback = AnimFang,')]:
   self.assertIn(old,self.source)
   with self.assertRaises(ValueError):physical.focus_punch_recipe(self.source.replace(old,new))
 def test_setup_animation_is_separate_from_successful_focus_punch_hit(self):
  scripts=self.raw['data/battle_anim_scripts.s'].decode();battle=self.raw['data/battle_scripts_1.s'].decode()
  self.assertEqual('Move_FOCUS_PUNCH',re.findall(r'^\s*\.4byte (Move_\w+)$',scripts,re.M)[264]);self.assertRegex(scripts,r'\.4byte General_FocusPunchSetUp\s+@ B_ANIM_FOCUS_PUNCH_SETUP')
  self.assertNotIn('General_FocusPunchSetUp',self.row['labels']);self.assertFalse(any('FOCUS_PUNCH_SETUP' in str(c) for c in self.row['commands']))
  body=battle.split('BattleScript_EffectFocusPunch::',1)[1].split('\nBattleScript_',1)[0];self.assertIn('jumpifnodamage BattleScript_HitFromAccCheck',body);self.assertIn('printstring STRINGID_PKMNLOSTFOCUS',body);self.assertNotIn('attackanimation',body)
 def test_generated_outputs_match_the_maintained_producers(self):
  folder=a.generated or ROOT/'content/generated/battle/animations'
  self.assertEqual(self.physical_data,(folder/'physical.json').read_bytes());self.assertEqual(self.background_data,(folder/'background-scripts.json').read_bytes())
if __name__=='__main__':unittest.main()
