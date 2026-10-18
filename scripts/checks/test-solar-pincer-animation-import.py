#!/usr/bin/env python3
"""Solar/Pincer source closure and shared background-set ownership."""
import importlib.util,json,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
imp=module('solar_physical',ROOT/'scripts/imports/battle/import-source-physical-animations.py')
bg=module('solar_background',ROOT/'scripts/imports/battle/import-source-animation-backgrounds.py')
RAW=ROOT/'content/raw/pokefirered/037335f4'
class SolarPincerImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,_=imp.build(RAW);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']}
 def test_source_roots_and_raw_child_dependencies(self):
  self.assertIn(12,self.rows);self.assertIn(76,self.rows)
  self.assertEqual(['gSolarBeamSmallOrbSpriteTemplate'],self.rows[76]['spriteDependencies'])
  commands=self.rows[76]['commands']+[x for c in self.rows[76]['commands'] if c['op']=='call' for x in self.pack['subroutines'][c['args'][0]]];self.assertEqual(21,sum(c['op']=='createsprite' and c['args'][0]=='gSolarBeamBigOrbSpriteTemplate' for c in commands))
  command=next(c for c in self.rows[12]['commands'] if c['op']=='fadetobgfromset');self.assertEqual(['BG_GUILLOTINE_OPPONENT','BG_GUILLOTINE_PLAYER','BG_GUILLOTINE_CONTESTS'],command['args'])
  self.assertEqual([12,13,14],[self.pack['backgroundSymbols'][n] for n in command['args']])
 def test_missing_primary_or_task_callback_blocks_complete_root(self):
  for move,name,collection in [(12,'gGuillotineSpriteTemplate','SPRITES'),(76,'gSolarBeamBigOrbSpriteTemplate','SPRITES'),(76,'AnimTask_CreateSmallSolarBeamOrbs','TASKS')]:
   with patch.object(imp,collection,getattr(imp,collection)-{name}):
    data,blocked=imp.build(RAW);self.assertNotIn(move,{r['moveId'] for r in json.loads(data)['scripts']});self.assertTrue(any(r['moveId']==move for r in blocked))
 def test_background_set_has_only_source_battle_materials(self):
  files=bg.build(RAW);manifest=json.loads(files['manifest.json']);moves={r['id']:r for r in manifest['moves']};atlas=files['atlas.bin']
  for id,name in [(12,'guillotine_opponent'),(13,'guillotine_player')]:
   offset,length=moves[id]['tilemap'];self.assertEqual((RAW/f'graphics/battle_anims/backgrounds/{name}.bin').read_bytes(),atlas[offset:offset+length])
  self.assertNotIn(14,moves);self.assertNotIn(26,moves)
  self.assertEqual(moves[24]['tiles'],moves[25]['tiles']);self.assertNotEqual(moves[24]['tilemap'],moves[25]['tilemap'])
 def test_preexisting_background_pixels_and_maps_are_unchanged(self):
  current=bg.build(RAW);now=json.loads(current['manifest.json'])
  added={'BG_SOLAR_BEAM_OPPONENT','BG_SOLAR_BEAM_PLAYER','BG_GUILLOTINE_OPPONENT','BG_GUILLOTINE_PLAYER'}
  with patch.object(bg,'MOVE_BACKGROUNDS',tuple(n for n in bg.MOVE_BACKGROUNDS if n not in added)):old=bg.build(RAW)
  before=json.loads(old['manifest.json'])
  for group in ['terrains','moves','overlays']:
   rows={r['id']:r for r in now[group]}
   for row in before[group]:
    for name,value in row.items():
     if name=='id':continue
     if isinstance(value,list) and len(value)==2 and all(isinstance(x,int) for x in value):
      offset,length=value;n,l=rows[row['id']][name];self.assertEqual(old['atlas.bin'][offset:offset+length],current['atlas.bin'][n:n+l])
     else:self.assertEqual(value,rows[row['id']][name])
 def test_changed_raw_lifetime_and_pincer_handoff_fail_closed(self):
  raw=imp.read_sources(RAW)
  for file,old,new in [('src/battle_anim_effects_1.c',b'gTasks[taskId].data[1] == 15',b'gTasks[taskId].data[1] == 14'),('src/battle_anim_effects_2.c',b'++sprite->data[4] == 51',b'++sprite->data[4] == 50')]:
   changed=dict(raw);self.assertIn(old,changed[file]);changed[file]=changed[file].replace(old,new)
   with patch.object(imp,'read_sources',return_value=changed):
    with self.assertRaises(ValueError):imp.build(RAW)
 def test_regeneration(self):self.assertEqual(self.data,(ROOT/'content/generated/battle/animations/physical.json').read_bytes())
if __name__=='__main__':unittest.main()
