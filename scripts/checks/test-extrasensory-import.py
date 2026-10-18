#!/usr/bin/env python3
"""Focused source ownership and dependency checks for the Extrasensory closure."""
import argparse,importlib.util,json,re,sys,unittest
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
class ExtrasensoryImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'background-scripts.json').read_text());cls.root=next(r for r in cls.pack['scripts'] if r['moveId']==326);cls.physical=module('extrasensory_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py')
 def test_complete_source_root_and_helpers_keep_line_and_command_parity(self):
  source=(REFERENCE/'data/battle_anim_scripts.s').read_text().splitlines();rows=self.root['commands']+[r for c in self.root['commands'] if c['op']=='call' for r in self.pack['subroutines'][c['args'][0]]]
  for row in rows:
   op,*tail=source[row['line']-1].split('@',1)[0].strip().split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(row['op'],row['args']))
  self.assertEqual(['SetPsychicBackground','UnsetPsychicBackground'],[c['args'][0] for c in self.root['commands'] if c['op']=='call']);self.assertEqual(['0','1','2'],[c['args'][2] for c in self.root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_ExtrasensoryDistortion'])
  self.assertEqual([['AnimTask_TransparentCloneGrowAndShrink','5','ANIM_ATTACKER']],[c['args'] for c in self.root['commands'] if c['op']=='createvisualtask' and c['args'][0]=='AnimTask_TransparentCloneGrowAndShrink'])
 def test_existing_shared_background_and_sounds_are_complete_without_new_effect_art(self):
  self.assertEqual({'SE_M_BIND','SE_M_LEER'},set(self.root['soundEffects']));self.assertFalse(any(c['op'] in {'loadspritegfx','createsprite'} for c in self.root['commands']));self.assertEqual({},self.root['spriteTags']);self.assertIn('BG_PSYCHIC',self.pack['backgroundSymbols']);self.assertTrue({'AnimTask_ExtrasensoryDistortion','AnimTask_TransparentCloneGrowAndShrink'}<=self.physical.TASKS)
  manifest=json.loads((GENERATED/'backgrounds/manifest.json').read_text());self.assertTrue(any(r['id']==self.pack['backgroundSymbols']['BG_PSYCHIC'] for r in manifest['moves']))
 def test_canonical_root_ownership_remains_disjoint(self):
  for name in ['physical.json','elemental.json']:
   pack=json.loads((GENERATED/name).read_text());self.assertNotIn(326,{r['moveId'] for r in pack['scripts']})
  self.assertEqual(1,sum(r['moveId']==326 for r in self.pack['scripts']))
if __name__=='__main__':unittest.main()
