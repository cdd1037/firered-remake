#!/usr/bin/env python3
"""Named controller roots retain exact source and canonical material ownership."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
from unittest.mock import patch
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--output',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest]
ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4';relative='scripts/imports/battle/import-source-general-animations.py'
spec=importlib.util.spec_from_file_location('named_general_import',ROOT/relative);module=importlib.util.module_from_spec(spec)
if a.candidate:exec(compile((a.candidate/relative).read_text(),str(ROOT/relative),'exec'),module.__dict__)
else:spec.loader.exec_module(module)
class NamedGeneralImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blobs=module.build(RAW);cls.pack=json.loads(cls.data);cls.rows={r['label']:r for r in cls.pack['generalScripts']};cls.source=(RAW/'data/battle_anim_scripts.s').read_text()
  if a.output:
   a.output.mkdir(parents=True,exist_ok=True);(a.output/'general.json').write_bytes(cls.data)
   for name,blob in cls.blobs.items():(a.output/name).write_bytes(blob)
 def test_existing_three_roots_and_materials_stay_exactly_unchanged(self):
  baseline=json.loads((ROOT/'content/generated/battle/animations/general.json').read_bytes())
  for row in baseline['generalScripts'][:3]:self.assertEqual(row,self.rows[row['label']])
  for key in ['assets','indexedAssets','spriteRecipes']:self.assertEqual(baseline[key],self.pack[key])
  for name,data in self.blobs.items():self.assertEqual(data,(ROOT/'content/generated/battle/animations'/name).read_bytes())
 def test_new_roots_and_helpers_match_every_original_instruction_line(self):
  for rows in [r['commands'] for r in self.pack['generalScripts'][3:]]+list(self.pack['subroutines'].values()):
   for c in rows:
    text=self.source.splitlines()[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
 def test_source_gotos_are_single_canonical_references_without_flattening(self):
  for label,target in [('General_Sun','Move_SUNNY_DAY'),('General_Sandstorm','Move_SANDSTORM'),('General_Hail','Move_HAIL')]:
   row=self.rows[label];self.assertEqual(1,len(row['commands']));self.assertEqual('goto',row['commands'][0]['op']);self.assertEqual([target],row['commands'][0]['args']);self.assertEqual([target],row['programReferences']);self.assertEqual({},row['soundEffects']);self.assertNotIn('moveId',row)
 def test_residual_rain_keeps_both_sixty_frame_emitters_and_source_palette_mask(self):
  row=self.rows['General_Rain'];self.assertEqual(['gRainDropSpriteTemplate'],row['spriteDependencies']);self.assertEqual({'ANIM_TAG_RAIN_DROPS':10115},row['spriteTags'])
  self.assertEqual(2,sum(c['args']==['AnimTask_CreateRaindrops','2','0','3','60'] for c in row['commands']))
  self.assertIn(['AnimTask_BlendBattleAnimPal','10','F_PAL_BG | F_PAL_BATTLERS_2','2','0','4','RGB_BLACK'],[c['args'] for c in row['commands']])
 def test_delayed_target_root_uses_psychic_helpers_and_focus_setup_keeps_endure(self):
  row=self.rows['General_FutureSightHit'];self.assertEqual(['AnimTask_SetAnimTargetToBattlerTarget','2'],row['commands'][0]['args']);self.assertEqual(['SetPsychicBackground','UnsetPsychicBackground'],[c['args'][0] for c in row['commands'] if c['op']=='call']);self.assertEqual({'BG_PSYCHIC':3},self.pack['backgroundSymbols'])
  focus=self.rows['General_FocusPunchSetUp'];self.assertEqual(['EndureEffect']*3,[c['args'][0] for c in focus['commands'] if c['op']=='call']);self.assertNotIn('gFocusPunchFistSpriteTemplate',str(focus));self.assertEqual({'ANIM_TAG_FOCUS_ENERGY':10184},focus['spriteTags'])
 def test_doom_retains_explosion_schedule_and_shared_beam_material(self):
  row=self.rows['General_DoomDesireHit'];self.assertEqual(['morning-sun'],row['overlayDependencies']);self.assertEqual(['AnimTask_SetAnimTargetToBattlerTarget','2'],row['commands'][0]['args'])
  self.assertEqual([['0','0','1','1'],['24','-24','1','1'],['-16','16','1','1'],['-24','-12','1','1'],['16','16','1','1']],[c['args'][3:] for c in row['commands'] if c['op']=='createsprite' and c['args'][0]=='gExplosionSpriteTemplate'])
  self.assertEqual({'SE_M_CONFUSE_RAY','SE_M_SELF_DESTRUCT'},set(row['soundEffects']));self.assertEqual({'ANIM_TAG_EXPLOSION':10198},row['spriteTags'])
 def test_target_assignment_semantic_change_fails_closed(self):
  original=module.shared.read
  def changed(reference,path):
   data=original(reference,path)
   if path=='src/battle_anim_utility_funcs.c':data=data.replace(b'gBattleAnimTarget = gBattlerTarget;',b'gBattleAnimTarget = gBattlerAttacker;')
   return data
  with patch.object(module.shared,'read',side_effect=changed),self.assertRaisesRegex(ValueError,'Delayed target callback'):module.build(RAW)
if __name__=='__main__':unittest.main()
