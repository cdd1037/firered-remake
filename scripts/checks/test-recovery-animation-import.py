#!/usr/bin/env python3
"""Four complete recovery/Aurora roots and exact indexed material dependencies."""
import importlib.util,json,hashlib,struct,unittest
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
REFERENCE=ROOT/'content/raw/pokefirered/037335f4'
def module(file,name):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('import-source-elemental-animations.py','recovery_elemental');bg=module('import-source-animation-backgrounds.py','recovery_background')
class RecoveryImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(data);cls.roots={s['moveId']:s for s in cls.pack['scripts']};cls.recipes={s['template']:s for s in cls.pack['spriteRecipes']};cls.bgfiles=bg.build(REFERENCE);cls.background=json.loads(cls.bgfiles['manifest.json']);cls.material=json.loads((ROOT/'tests/Game.Presentation.Tests/Fixtures/recovery-material-source-vectors.json').read_text())
 def block(self,row,key):
  start,length=row[key];return self.bgfiles['atlas.bin'][start:start+length]
 def test_complete_four_roots_and_callback_resources(self):
  for move,name in [(62,'AURORA_BEAM'),(156,'REST'),(234,'MORNING_SUN'),(236,'MOONLIGHT')]:self.assertEqual('Move_'+name,self.roots[move]['label']);self.assertEqual('end',self.roots[move]['commands'][-1]['op'])
  sun=self.roots[234];self.assertEqual(['morning-sun'],sun['overlayDependencies']);self.assertEqual(['gGreenStarSpriteTemplate'],sun['spriteDependencies']);self.assertIn('SE_M_MORNING_SUN',sun['soundEffects']);self.assertEqual(15,sum(c['op']=='call' and c['args']==['MorningSunStar'] for c in sun['commands']))
  self.assertEqual(10,sum(c['op']=='call' and c['args']==['AuroraBeamCreateRings'] for c in self.roots[62]['commands']));self.assertEqual(20,self.pack['backgroundSymbols']['BG_AURORA']);self.assertIn(dict(op='setarg',args=['7','0xFFFF'],line=5509),self.roots[62]['commands'])
  self.assertEqual(3,sum(c['op']=='createsprite' for c in self.roots[156]['commands']));self.assertEqual(5,sum(c['op']=='createsprite' and c['args'][0]=='gMoonlightSparkleSpriteTemplate' for c in self.roots[236]['commands']))
 def test_source_frame_affine_tables_and_shapes(self):
  green=self.recipes['gGreenStarSpriteTemplate'];self.assertEqual([[0,4],[8],[12]],[[f['tile'] for f in a] for a in green['animations']]);self.assertEqual([0,-1,-1],green['loopStarts']);self.assertEqual([],green['affineAnimations'])
  sleep=self.recipes['gSleepLetterZSpriteTemplate'];self.assertEqual([226,30],[a[0]['rotation'] for a in sleep['affineAnimations']]);self.assertEqual([1,255],[a[1]['rotation'] for a in sleep['affineAnimations']]);self.assertEqual([24,24],[a[1]['duration'] for a in sleep['affineAnimations']])
  ring=self.recipes['gAuroraBeamRingSpriteTemplate'];self.assertTrue(ring['affineDoubleSize']);self.assertEqual((8,16),(ring['width'],ring['height']));self.assertEqual([96,96],[ring['affineAnimations'][0][1]['x'],ring['affineAnimations'][0][1]['y']]);self.assertEqual([-1,-1],ring['loopStarts'])
  self.assertTrue(self.recipes['gMoonSpriteTemplate']['blend']);self.assertFalse(self.recipes['gMoonlightSparkleSpriteTemplate']['blend'])
 def test_all_five_indexed_materials_match_independent_source_bytes(self):
  indexed={r['id']:r for r in self.pack['indexedAssets']}
  for row in self.material['sprites']:
   name=row['id'];self.assertEqual(row['size'],[indexed[name]['width'],indexed[name]['height']]);self.assertEqual(row['packedSha256'],hashlib.sha256(self.pixels[name+'.4bpp']).hexdigest());self.assertEqual(row['palette'],list(struct.unpack('<16H',self.pixels[name+'.pal16'])));self.assertEqual('ANIM_TAG_'+name.replace('-','_').upper(),indexed[name]['paletteTag'])
 def test_morning_sun_and_aurora_keep_source_map_palette_and_padding(self):
  for vector in self.material['backgrounds']:
   row=next(r for r in self.background['overlays' if vector['id']=='morning-sun' else 'moves'] if r['id']==('morning-sun' if vector['id']=='morning-sun' else 20))
   for key,hashkey in [('tiles','packedSha256'),('tilemap','tilemapSha256')]:self.assertEqual(vector[hashkey],hashlib.sha256(self.block(row,key)).hexdigest())
   self.assertEqual(vector['palette'],list(struct.unpack('<16H',self.block(row,'palette'))))
   if vector['id']=='morning-sun':self.assertEqual(8,row['paletteBank']);self.assertEqual(8192,len(self.block(row,'tiles')));self.assertEqual(bytes(8192-640),self.block(row,'tiles')[640:])
 def test_adapter_removal_closes_exactly_four_roots_and_preserves_prior_rows(self):
  names={'gSleepLetterZSpriteTemplate','gGreenStarSpriteTemplate','gMoonSpriteTemplate','gMoonlightSparkleSpriteTemplate','gAuroraBeamRingSpriteTemplate'}
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in names}),patch.object(imp,'TASKS',imp.TASKS-{'AnimTask_MorningSunLightBeam','AnimTask_AlphaFadeIn','AnimTask_MoonlightEndFade','AnimTask_RotateAuroraRingColors'}):data,pixels=imp.build(REFERENCE)
  old={r['moveId']:r for r in json.loads(data)['scripts']};self.assertEqual({62,156,234,236},set(self.roots)-set(old))
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for name,data in pixels.items():self.assertEqual(data,self.pixels[name])
 def test_wrong_raw_child_and_callback_sound_fail_closed(self):
  original=imp.read
  for old,new,reason in [(b'CreateSprite(&gGreenStarSpriteTemplate,',b'CreateSprite(&gOtherStarSpriteTemplate,','raw child dependency'),(b'PlaySE12WithPanning(SE_M_MORNING_SUN,',b'PlaySE12WithPanning(SE_M_SNORE,','callback sounds')]:
   def changed(ref,path):
    data=original(ref,path);return data.replace(old,new) if path=='src/battle_anim_effects_3.c' else data
   with patch.object(imp,'read',side_effect=changed):
    with self.assertRaisesRegex(ValueError,reason):imp.build(REFERENCE)
 def test_wrong_registered_palette_and_overlay_source_load_fail_closed(self):
  original=imp.read
  def changed(ref,path):
   data=original(ref,path);return data.replace(b'{gBattleAnimSpritePal_GreenStar, ANIM_TAG_GREEN_STAR}',b'{gBattleAnimSpritePal_Moon, ANIM_TAG_GREEN_STAR}') if path=='src/data/battle_anim.h' else data
  with patch.object(imp,'read',side_effect=changed):
   with self.assertRaisesRegex(ValueError,'Source indexed registration'):imp.build(REFERENCE)
  original=bg.read
  def changed_bg(ref,path):
   data=original(ref,path);return data.replace(b'LoadCompressedPalette(gBattleAnim_MorningSunPal,',b'LoadCompressedPalette(gBattleAnim_ScaryFacePal,') if path=='src/battle_anim_effects_3.c' else data
  with patch.object(bg,'read',side_effect=changed_bg):
   with self.assertRaisesRegex(ValueError,'Morning Sun BG1'):bg.build(REFERENCE)
if __name__=='__main__':unittest.main()
