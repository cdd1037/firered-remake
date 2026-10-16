import importlib.util,json,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('background_scripts', ROOT/'scripts/imports/battle/import-source-background-scripts.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=imp.ROOT/'content/raw/pokefirered/037335f4'
class BackgroundScriptImportTests(unittest.TestCase):
 def test_dependency_discovery_and_source_symbols(self):
  pack=json.loads(imp.build(REFERENCE));self.assertEqual({60,93,94,95,149,168,242},{r['moveId'] for r in pack['scripts']});self.assertEqual({'BG_DARK':1,'BG_PSYCHIC':3},pack['backgroundSymbols']);self.assertNotIn('spriteRecipes',pack);self.assertNotIn('assets',pack);self.assertEqual(imp.build(REFERENCE),(ROOT/'content/generated/battle/animations/background-scripts.json').read_bytes())
  for script in pack['scripts']:
   commands=script['commands']+[c for name in dict.fromkeys(c['args'][0] for c in script['commands'] if c['op']=='call') for c in pack['subroutines'][name]];ops=[c['op'] for c in commands];self.assertEqual(2,ops.count('waitbgfadein'));self.assertEqual(1,ops.count('restorebg'))
 def test_uncovered_background_and_sprite_fail_closed(self):
  original=imp.elemental.read
  for old,new,missing in [(b'fadetobg BG_DARK',b'fadetobg BG_UNKNOWN',{168,242}),(b'createsprite gSharpTeethSpriteTemplate',b'createsprite UnknownSprite',{242})]:
   def read(reference,path):
    data=original(reference,path);return data.replace(old,new) if path=='data/battle_anim_scripts.s' else data
   with patch.object(imp.elemental,'read',side_effect=read):
    self.assertFalse(missing&{r['moveId'] for r in json.loads(imp.build(REFERENCE))['scripts']})
 def test_sound_dependencies_are_reachable_numeric_source_operands(self):
  scripts={s['moveId']:s for s in json.loads(imp.build(REFERENCE))['scripts']}
  self.assertEqual({'SE_M_PSYBEAM':182,'SE_M_PSYBEAM2':193},scripts[60]['soundEffects'])
  self.assertEqual({'SE_M_SUPERSONIC':177},scripts[95]['soundEffects'])
  self.assertEqual({'SE_M_PSYBEAM':182,'SE_M_TELEPORT':196},scripts[149]['soundEffects'])
  with self.assertRaises(ValueError):imp.elemental.sound_effects(REFERENCE,[{'op':'playsewithpan','args':['SE_UNKNOWN','0']}])
if __name__=='__main__':unittest.main()
