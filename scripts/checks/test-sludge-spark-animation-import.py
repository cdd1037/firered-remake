#!/usr/bin/env python3
"""Keep authored scripts intact; safe callback policy is separate from source data."""
import importlib.util,json,os,re,types,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(os.environ.get('FIRERED_REPOSITORY_ROOT',Path(__file__).resolve().parents[2]))
PATH=ROOT/'scripts/imports/battle/import-source-elemental-animations.py'
if os.environ.get('FIRERED_ELEMENTAL_IMPORTER'):
 imp=types.ModuleType('sludge_spark_elemental');imp.__file__=str(PATH);exec(compile(Path(os.environ['FIRERED_ELEMENTAL_IMPORTER']).read_text(),str(PATH),'exec'),imp.__dict__)
else:
 spec=importlib.util.spec_from_file_location('sludge_spark_elemental',PATH);imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class SludgeSparkAnimationImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  rows,cls.subs=imp.discover_scripts(REFERENCE);cls.rows={r['moveId']:r for r in rows};cls.source=imp.read(REFERENCE,'data/battle_anim_scripts.s').decode();cls.poison=imp.read(REFERENCE,'src/battle_anim_poison.c').decode();cls.electric=imp.read(REFERENCE,'src/battle_anim_electric.c').decode()
 def flat(self,id):
  root=self.rows[id]['commands'];return root+[c for call in root if call['op']=='call' for c in self.subs[call['args'][0]]]
 def test_complete_roots_keep_every_authored_command_and_argument(self):
  lines=self.source.splitlines()
  for id,label in [(124,'SLUDGE'),(188,'SLUDGE_BOMB'),(209,'SPARK')]:
   row=self.rows[id];self.assertEqual('Move_'+label,row['label']);self.assertEqual('end',row['commands'][-1]['op'])
   for c in self.flat(id):
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[a.strip() for a in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(10,sum(c['op']=='call' and c['args']==['SludgeBombProjectile'] for c in self.rows[188]['commands']))
  self.assertEqual(12,sum(c['op']=='createsprite' and c['args'][0]=='gSludgeBombHitParticleSpriteTemplate' for c in self.rows[188]['commands']))
  self.assertEqual(6,sum(c['op']=='createsprite' and c['args'][0]=='gSparkElectricitySpriteTemplate' for c in self.rows[209]['commands']))
  self.assertEqual(8,sum(c['op']=='createsprite' and c['args'][0]=='gSparkElectricityFlashingSpriteTemplate' for c in self.rows[209]['commands']))
 def test_import_does_not_rewrite_the_undefined_authored_callback_operands(self):
  for id in [124,188]:
   projectiles=[c for c in self.flat(id) if c['op']=='createsprite' and c['args'][0]=='gSludgeProjectileSpriteTemplate'];self.assertTrue(projectiles)
   self.assertTrue(all(c['args'][-1]=='0' for c in projectiles))
  sparks=[c for c in self.flat(209) if c['op']=='createsprite' and c['args'][0]=='gSparkElectricityFlashingSpriteTemplate'];self.assertEqual(8,len(sparks));self.assertTrue(all(c['args'][-1]=='0' for c in sparks))
  self.assertIn('StartSpriteAnim(sprite, 2);',self.poison);self.assertIn('if (sprite->data[7] % sprite->data[4] == 0)',self.electric)
 def test_sludge_recipes_reuse_exact_existing_poison_sheet_and_shared_affine_programs(self):
  for name,callback,tile,scale,double in [('gSludgeProjectileSpriteTemplate','AnimSludgeProjectile',0,352,True),('gSludgeBombHitParticleSpriteTemplate','AnimSludgeBombHitParticle',8,236,False)]:
   r=imp.recipe(self.poison,name,callback);self.assertEqual('poison-bubble',r['asset']);self.assertEqual((16,16),(r['width'],r['height']));self.assertFalse(r['blend']);self.assertEqual(double,r['affineDoubleSize']);self.assertEqual([[dict(tile=tile,duration=1,hFlip=False,vFlip=False)]],r['animations']);self.assertEqual([-1],r['loopStarts']);self.assertEqual(scale,r['affineAnimations'][0][0]['x']);self.assertEqual(scale,r['affineAnimations'][0][0]['y'])
  self.assertTrue((ROOT/'content/generated/battle/animations/elemental-poison-bubble.rgb555').is_file())
 def test_stationary_spark_retains_dummy_program_and_direct_matrix_callback(self):
  r=imp.recipe(self.electric,'gSparkElectricitySpriteTemplate','AnimSparkElectricity');self.assertEqual('spark-2',r['asset']);self.assertEqual((16,16),(r['width'],r['height']));self.assertEqual([],r['affineAnimations']);self.assertEqual([0],r['loopStarts']);self.assertFalse(r['blend']);self.assertFalse(r['affineDoubleSize'])
  self.assertIn('gOamMatrices[matrixNum].b = sineVal;',self.electric);self.assertIn('sprite->callback = DestroyAnimSpriteAfterTimer;',self.electric)
  self.assertTrue((ROOT/'content/generated/battle/animations/elemental-spark-2.rgb555').is_file())
 def test_missing_callback_or_sound_keeps_entire_root_unadmitted(self):
  for name,ids in [('gSludgeProjectileSpriteTemplate',[124,188]),('gSludgeBombHitParticleSpriteTemplate',[188]),('gSparkElectricitySpriteTemplate',[209]),('gSparkElectricityFlashingSpriteTemplate',[209])]:
   with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=name}):rows,_=imp.discover_scripts(REFERENCE)
   for id in ids:self.assertNotIn(id,[r['moveId'] for r in rows])
  with patch.object(imp,'SOUNDS',imp.SOUNDS-{'SE_M_DIG'}):rows,_=imp.discover_scripts(REFERENCE)
  self.assertNotIn(188,[r['moveId'] for r in rows]);self.assertEqual(168,self.rows[188]['soundEffects']['SE_M_DIG'])
if __name__=='__main__':unittest.main()
