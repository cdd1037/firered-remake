#!/usr/bin/env python3
"""Source recipes, material preservation and sound ABI for projectile adapters."""
import importlib.util,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('projectile_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
class ProjectileImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  blob,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(blob);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def flat(self,move):
  root=self.roots[move];return root['commands']+[c for call in root['commands'] if call['op']=='call' for c in self.pack['subroutines'][call['args'][0]]]
 def test_complete_roots_keep_authored_projectile_multiplicity_and_source_songs(self):
  for move,label,template,count in [(49,'SONIC_BOOM','gSonicBoomSpriteTemplate',3),(63,'HYPER_BEAM','gHyperBeamOrbSpriteTemplate',52),(127,'WATERFALL','gSmallDriftingBubblesSpriteTemplate',18),(155,'BONEMERANG','gBonemerangSpriteTemplate',1)]:
   self.assertEqual('Move_'+label,self.roots[move]['label']);self.assertEqual(count,sum(c['op']=='createsprite' and c['args'][0]==template for c in self.flat(move)))
  self.assertEqual({'SE_M_HYPER_BEAM','SE_M_HYPER_BEAM2'},set(self.roots[63]['soundEffects']))
  self.assertEqual({'ANIM_TAG_ORBS'},set(self.roots[63]['spriteTags']))
 def test_exact_source_shapes_frames_and_bonemerang_affine(self):
  for template,callback,asset,width,height,blend,double in [('gSonicBoomSpriteTemplate','AnimSonicBoomProjectile','air-wave',32,16,True,True),('gSmallDriftingBubblesSpriteTemplate','AnimSmallDriftingBubbles','small-bubbles',8,8,False,False),('gBonemerangSpriteTemplate','AnimBonemerangProjectile','bone',32,32,False,False)]:
   r=self.recipes[template];self.assertEqual((callback,asset,width,height,blend,double),(r['callback'],r['asset'],r['width'],r['height'],r['blend'],r['affineDoubleSize']));self.assertEqual([0],r['loopStarts']);self.assertEqual([[dict(tile=0,duration=1,hFlip=False,vFlip=False)]],r['animations'])
  self.assertEqual([dict(op='frame',x=0,y=0,rotation=15,duration=1,target=0),dict(op='jump',x=0,y=0,rotation=0,duration=0,target=0)],self.recipes['gBonemerangSpriteTemplate']['affineAnimations'][0])
 def test_source_hyper_beam_table_stays_seven_and_unsafe_operand_is_not_forged(self):
  r=self.recipes['gHyperBeamOrbSpriteTemplate'];self.assertEqual(7,len(r['animations']));self.assertEqual(list(range(7)),[a[0]['tile'] for a in r['animations']]);self.assertEqual([-1]*7,r['loopStarts'])
  source=imp.read(REFERENCE,'src/battle_anim_effects_1.c').decode();self.assertIn('StartSpriteAnim(sprite, animNum % 8);',source)
 def test_sonic_boom_imports_exact_air_wave_not_air_wave_two(self):
  image=Image.open(REFERENCE/'graphics/battle_anims/sprites/air_wave.png');self.assertEqual(('P',(32,16)),(image.mode,image.size));palette=image.getpalette();expected=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3))|(32768 if i else 0) for i in image.tobytes()]
  self.assertEqual((32,16),struct.unpack_from('<HH',self.pixels['air-wave']));self.assertEqual(expected,list(struct.unpack_from('<512H',self.pixels['air-wave'],4)))
  self.assertNotEqual('air-wave-2',self.recipes['gSonicBoomSpriteTemplate']['asset'])
 def test_indexed_orbs_preserve_existing_fallback_and_palette_colors(self):
  row=next(r for r in self.pack['indexedAssets'] if r['id']=='orbs');self.assertEqual(('ANIM_TAG_ORBS',16,48),(row['paletteTag'],row['width'],row['height']))
  with Image.open(REFERENCE/'graphics/battle_anims/sprites/orbs.png') as image:
   palette=image.getpalette();indices=list(image.tobytes());tiled=[v for y in range(0,image.height,8) for x in range(0,image.width,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
  packed=bytes(tiled[i]|tiled[i+1]<<4 for i in range(0,len(tiled),2));self.assertEqual(packed,self.pixels['orbs.4bpp'])
  expected_colors=[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)];self.assertEqual(expected_colors,list(struct.unpack('<16H',self.pixels['orbs.pal16'])))
  self.assertEqual([expected_colors[i]|(32768 if i else 0) for i in indices],list(struct.unpack_from('<768H',self.pixels['orbs'],4)))
 def test_loop_panning_resolves_any_declared_song_and_rejects_invalid_abi(self):
  songs=set(re.findall(r'^#define (SE_\w+)\s+\d+\b',imp.read(REFERENCE,'include/constants/songs.h').decode(),re.M));valid=['SoundTask_LoopSEAdjustPanning','SE_M_HYPER_BEAM2','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','1','15','0','5']
  self.assertTrue(imp.supported_sound_task(valid,songs))
  for song in ['SE_M_BUBBLE_BEAM2','SE_M_PSYBEAM2','SE_M_TELEPORT','SE_M_MIST']:
   other=valid.copy();other[1]=song;self.assertTrue(imp.supported_sound_task(other,songs))
  for index,value in [(0,'UnknownSoundTask'),(1,'SE_UNKNOWN'),(2,'-65'),(3,'64'),(4,'128'),(5,'0'),(6,'256'),(7,'-1')]:
   invalid=valid.copy();invalid[index]=value;self.assertFalse(imp.supported_sound_task(invalid,songs))
  self.assertFalse(imp.supported_sound_task(valid[:-1],songs))
 def test_existing_bubble_beam_song_and_loop_panning_dependency_are_preserved(self):
  # Ice Beam owns the old loop-panning call using the Bubble Beam2 song.
  rows=[c for c in self.flat(58) if c['op']=='createsoundtask'];self.assertEqual(1,len(rows));self.assertEqual(['SoundTask_LoopSEAdjustPanning','SE_M_BUBBLE_BEAM2','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','4','4','0','10'],rows[0]['args'])
  self.assertIn('SE_M_BUBBLE_BEAM2',self.roots[58]['soundEffects']);self.assertEqual({'SE_M_BUBBLE':117,'SE_M_BUBBLE3':119},self.roots[61]['soundEffects']);self.assertEqual((16,40),struct.unpack_from('<HH',self.pixels['small-bubbles']))
 def test_missing_dependency_or_changed_callback_fails_closed(self):
  original=imp.read
  def read(reference,path):
   data=original(reference,path);return data.replace(b'gBonemerangSpriteTemplate,',b'gUnknownProjectileSpriteTemplate,') if path=='data/battle_anim_scripts.s' else data
  with patch.object(imp,'read',side_effect=read):blob,_=imp.build(REFERENCE)
  self.assertNotIn(155,{r['moveId'] for r in json.loads(blob)['scripts']})
  source=original(REFERENCE,'src/battle_anim_ground.c').decode()
  with self.assertRaises(ValueError):imp.recipe(source.replace('.callback = AnimBonemerangProjectile,','.callback = MissingProjectile,'),'gBonemerangSpriteTemplate','AnimBonemerangProjectile')
if __name__=='__main__':unittest.main()
