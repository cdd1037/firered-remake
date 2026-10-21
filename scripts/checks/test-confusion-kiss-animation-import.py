#!/usr/bin/env python3
"""Confusion/kiss import closure and material regression checks; no upstream code execution."""
import sys
sys.dont_write_bytecode=True
import argparse,importlib.util,io,json,re,struct,unittest
from pathlib import Path
from unittest.mock import patch
from indexed_material_expectations import assert_preserved_materials, assert_source_pixels, dependent_roots
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(add_help=False);p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--generated',type=Path,default=ROOT/'content/generated/battle/animations');opts,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
REFERENCE=opts.reference

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
imp=module('confusion_kiss_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py')
bg=module('confusion_kiss_backgrounds',ROOT/'scripts/imports/battle/import-source-animation-backgrounds.py')
NAMES={'gConfuseRayBallBounceSpriteTemplate','gConfuseRayBallSpiralSpriteTemplate','gDevilSpriteTemplate','gPinkHeartSpriteTemplate','gAngelSpriteTemplate','gRedHeartBurstSpriteTemplate','gForesightMagnifyingGlassSpriteTemplate'}
NEW_ROOTS={109,142,186,193}

def png(stem):
 with Image.open(io.BytesIO(imp.read(REFERENCE,'graphics/battle_anims/sprites/'+stem+'.png'))) as im:
  return im.size,im.tobytes(),im.getpalette()[:48],bytes(v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes())
def pal(stem):
 rows=imp.read(REFERENCE,'graphics/battle_anims/sprites/'+stem+'.pal').decode().splitlines();assert rows[:3]==['JASC-PAL','0100','16'];return [int(c) for line in rows[3:] for c in line.split()]
def colors(rgb):return [sum((rgb[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
def block(pack,atlas,value):return atlas[value[0]:sum(value)]
def content_row(row,atlas):return {k:(block(None,atlas,v) if isinstance(v,list) and len(v)==2 else v) for k,v in row.items()}
class ConfusionKissImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(REFERENCE);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']};cls.backgrounds=bg.build(REFERENCE)
  with patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k not in NAMES}):old,cls.previous_pixels=imp.build(REFERENCE)
  cls.previous=json.loads(old)
  with patch.object(bg,'MOVE_BACKGROUNDS',tuple(x for x in bg.MOVE_BACKGROUNDS if x!='BG_GHOST')):cls.previous_backgrounds=bg.build(REFERENCE)
 def changed_build(self,path,before,after,importer=imp):
  original=importer.read;self.assertIn(before,original(REFERENCE,path))
  def changed(ref,name):
   data=original(ref,name);return data.replace(before,after) if name==path else data
  with patch.object(importer,'read',side_effect=changed):return importer.build(REFERENCE)
 def test_dependency_closure_preserves_all_unaffected_scripts_and_recipes(self):
  old={r['moveId']:r for r in self.previous['scripts']};affected=dependent_roots(self.pack,templates=NAMES);self.assertTrue(NEW_ROOTS<=affected);self.assertEqual(affected,set(self.roots)-set(old));self.assertEqual(set(old),set(self.roots)-affected)
  for move,row in old.items():self.assertEqual(row,self.roots[move])
  for row in self.previous['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  self.assertEqual(NAMES,set(self.recipes)-{r['template'] for r in self.previous['spriteRecipes']})
 def test_generated_scripts_pixels_and_background_blocks_match(self):
  prior=json.loads((opts.generated/'elemental.json').read_bytes())
  for row in prior['scripts']:self.assertEqual(row,self.roots[row['moveId']])
  for row in prior['spriteRecipes']:self.assertEqual(row,self.recipes[row['template']])
  assert_preserved_materials(self,prior,self.pack,opts.generated,self.pixels,'elemental-')
  self.assert_background_preserved({'manifest.json':(opts.generated/'backgrounds/manifest.json').read_bytes(),'atlas.bin':(opts.generated/'backgrounds/atlas.bin').read_bytes()})
 def test_four_roots_keep_complete_source_timing_tasks_and_sounds(self):
  expected={109:('CONFUSE_RAY',2,{'SE_M_STRING_SHOT2':123,'SE_M_CONFUSE_RAY':189}),142:('LOVELY_KISS',5,{'SE_M_ATTRACT':219,'SE_M_PSYBEAM2':193}),186:('SWEET_KISS',7,{'SE_M_ATTRACT':219,'SE_M_HEAL_BELL':188}),193:('FORESIGHT',1,{'SE_M_DETECT':202,'SE_M_SKETCH':198})}
  for move,(label,count,sounds) in expected.items():
   row=self.roots[move];self.assertEqual('Move_'+label,row['label']);self.assertEqual('end',row['commands'][-1]['op']);self.assertEqual(count,sum(c['op']=='createsprite' for c in row['commands']));self.assertEqual(sounds,row['soundEffects'])
   for c in row['commands']:
    if c['op']=='createsprite':self.assertIn(c['args'][0],self.recipes)
    if c['op']=='createvisualtask':self.assertIn(c['args'][0],imp.TASKS)
  ray=self.roots[109]['commands'];self.assertEqual([['SoundTask_AdjustPanningVar','2','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','2','0'],['AnimTask_BlendColorCycleByTag','2','ANIM_TAG_YELLOW_BALL','0','6','0','14','RGB(31, 10, 0)']],[c['args'] for c in ray if c['op']=='createvisualtask'])
  self.assertEqual(['BG_GHOST'],next(c['args'] for c in ray if c['op']=='fadetobg'));self.assertEqual(2,self.pack['backgroundSymbols']['BG_GHOST'])
  self.assertEqual([['23'],['23']],[c['args'] for c in self.roots[186]['commands'] if c['op']=='delay'])
  self.assertEqual([['17'],['48'],['24'],['10']],[c['args'] for c in self.roots[193]['commands'] if c['op']=='delay'])
 def test_every_missing_callback_or_task_adapter_closes_only_dependent_roots(self):
  for name,root in [('gConfuseRayBallBounceSpriteTemplate',109),('gConfuseRayBallSpiralSpriteTemplate',109),('gDevilSpriteTemplate',142),('gPinkHeartSpriteTemplate',142),('gAngelSpriteTemplate',186),('gRedHeartBurstSpriteTemplate',186),('gForesightMagnifyingGlassSpriteTemplate',193)]:
   with self.subTest(name=name),patch.object(imp,'TEMPLATES',{k:v for k,v in imp.TEMPLATES.items() if k!=name}):data,_=imp.build(REFERENCE)
   affected=dependent_roots(self.pack,templates={name});self.assertIn(root,affected);self.assertEqual(set(self.roots)-affected,{r['moveId'] for r in json.loads(data)['scripts']})
  for name in ['SoundTask_AdjustPanningVar','AnimTask_BlendColorCycleByTag']:
   with self.subTest(name=name),patch.object(imp,'TASKS',imp.TASKS-{name}):data,_=imp.build(REFERENCE)
   affected=dependent_roots(self.pack,tasks={name});self.assertIn(109,affected);self.assertEqual(set(self.roots)-affected,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_six_materials_match_source_indices_rgb555_and_distinct_heart_palettes(self):
  for asset,stem,size,palette in [('yellow-ball','yellow_ball',(16,16),None),('devil','devil',(32,64),None),('angel','angel',(32,32),None),('magnifying-glass','magnifying_glass',(32,32),None),('pink-heart','magenta_heart',(16,16),'pink_heart'),('red-heart','magenta_heart',(16,16),'red_heart')]:
   shape,linear,rgb,tiles=png(stem);self.assertEqual(size,shape);cs=colors(pal(palette) if palette else rgb);assert_source_pixels(self,self.pack,self.pixels,asset,size,linear,cs,'ANIM_TAG_'+asset.upper().replace('-','_'))
  self.assertNotEqual(self.pixels['pink-heart'],self.pixels['red-heart'])
  shape,linear,rgb,tiles=png('yellow_ball');self.assertEqual(bytes(tiles[i]|tiles[i+1]<<4 for i in range(0,len(tiles),2)),self.pixels['yellow-ball.4bpp']);self.assertEqual(struct.pack('<16H',*colors(rgb)),self.pixels['yellow-ball.pal16']);self.assertEqual({'id':'yellow-ball','width':16,'height':16,'indicesPath':'elemental-yellow-ball.4bpp','palettePath':'elemental-yellow-ball.pal16','paletteTag':'ANIM_TAG_YELLOW_BALL'},next(r for r in self.pack['indexedAssets'] if r['id']=='yellow-ball'))
 def test_exact_seven_recipe_frame_affine_programs(self):
  expected={'gConfuseRayBallBounceSpriteTemplate':('yellow-ball','AnimConfuseRayBallBounce',16,False,True),'gConfuseRayBallSpiralSpriteTemplate':('yellow-ball','AnimConfuseRayBallSpiral',16,True,False),'gDevilSpriteTemplate':('devil','AnimDevil',32,False,False),'gPinkHeartSpriteTemplate':('pink-heart','AnimPinkHeart',16,False,False),'gAngelSpriteTemplate':('angel','AnimAngel',32,False,False),'gRedHeartBurstSpriteTemplate':('red-heart','AnimParticleBurst',16,False,False),'gForesightMagnifyingGlassSpriteTemplate':('magnifying-glass','AnimForesightMagnifyingGlass',32,True,False)}
  for name,fields in expected.items():
   r=self.recipes[name];self.assertEqual(fields,tuple(r[k] for k in ['asset','callback','width','blend','affineDoubleSize']));self.assertEqual(r['width'],r['height']);self.assertTrue(r['animations'])
  self.assertEqual([[0],[16]],[[f['tile'] for f in a] for a in self.recipes['gDevilSpriteTemplate']['animations']]);self.assertEqual([0,0],self.recipes['gDevilSpriteTemplate']['loopStarts']);self.assertEqual([24],[f['duration'] for f in self.recipes['gAngelSpriteTemplate']['animations'][0]]);self.assertEqual([-1],self.recipes['gAngelSpriteTemplate']['loopStarts'])
  self.assertEqual([('frame',30,30,10,5),('frame',-30,-30,10,5),('jump',0,0,0,0)],[(c['op'],c['x'],c['y'],c['rotation'],c['duration']) for c in self.recipes['gConfuseRayBallBounceSpriteTemplate']['affineAnimations'][0]])
 def test_finite_frame_and_affine_pointer_tables_need_no_trailing_comma(self):
  for path,name,callback,table in [('src/battle_anim_effects_2.c','gAngelSpriteTemplate','AnimAngel','sAngelSpriteAnimTable'),('src/battle_anim_ghost.c','gConfuseRayBallBounceSpriteTemplate','AnimConfuseRayBallBounce','sAffineAnims_ConfuseRayBallBounce')]:
   source=imp.read(REFERENCE,path).decode();m=re.search(r'(const union (?:AffineAnimCmd|AnimCmd) \*const '+table+r'\[\] =\s*\{)(.*?)(\};)',source,re.S);self.assertIsNotNone(m)
   no_comma=re.sub(r',\s*$', '\n',m[2]);changed=source[:m.start(2)]+no_comma+source[m.end(2):];self.assertEqual(self.recipes[name],imp.recipe(changed,name,callback));self.assertGreater(len(imp.recipe(changed,name,callback)['affineAnimations' if 'Affine' in m[1] else 'animations']),0)
 def test_malformed_template_callback_registration_and_palette_path_fail_closed(self):
  changes=[('src/battle_anim_ghost.c',b'.callback = AnimConfuseRayBallBounce,',b'.callback = MissingRay,'),('src/battle_anim_effects_2.c',b'.callback = AnimPinkHeart,',b'.callback = MissingHeart,'),('src/battle_anim_effects_3.c',b'.callback = AnimForesightMagnifyingGlass,',b'.callback = MissingGlass,'),('src/data/battle_anim.h',b'{gBattleAnimSpriteGfx_YellowBall, 0x0080, ANIM_TAG_YELLOW_BALL}',b'{gBattleAnimSpriteGfx_YellowBall, 0x0040, ANIM_TAG_YELLOW_BALL}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_PinkHeart, ANIM_TAG_PINK_HEART}',b'{gBattleAnimSpritePal_RedHeart, ANIM_TAG_PINK_HEART}'),('src/data/battle_anim.h',b'{gBattleAnimSpritePal_RedHeart, ANIM_TAG_RED_HEART}',b'{gBattleAnimSpritePal_PinkHeart, ANIM_TAG_RED_HEART}'),('src/graphics.c',b'graphics/battle_anims/sprites/pink_heart.gbapal.lz',b'graphics/battle_anims/sprites/red_heart.gbapal.lz'),('src/graphics.c',b'graphics/battle_anims/sprites/magnifying_glass.4bpp.lz',b'graphics/battle_anims/sprites/angel.4bpp.lz')]
  for path,before,after in changes:
   with self.subTest(path=path,before=before),self.assertRaises(ValueError):self.changed_build(path,before,after)
 def test_malformed_implicit_sound_and_missing_sound_symbol_fail_closed(self):
  for before,after in [(b'PlaySE(SE_M_CONFUSE_RAY);',b'PlaySE(SE_M_ATTRACT);'),(b'PlaySE12WithPanning(SE_M_CONFUSE_RAY, gAnimCustomPanning);',b'PlaySE12WithPanning(SE_M_CONFUSE_RAY, 0);')]:
   with self.subTest(before=before),self.assertRaises(ValueError):self.changed_build('src/battle_anim_ghost.c',before,after)
  with self.assertRaises(ValueError):self.changed_build('include/constants/songs.h',b'#define SE_M_CONFUSE_RAY ',b'#define SE_REMOVED_CONFUSE_RAY ')
 def test_implicit_sound_api_and_callback_handoff_mutations_fail_closed(self):
  for before,after in [(b'PlaySE(SE_M_CONFUSE_RAY);',b'PlaySE12WithPanning(SE_M_CONFUSE_RAY, gAnimCustomPanning);'),(b'sprite->callback = AnimConfuseRayBallBounce_Step1;',b'sprite->callback = MissingConfuseRayStep;')]:
   with self.subTest(before=before),self.assertRaises(ValueError):self.changed_build('src/battle_anim_ghost.c',before,after)
 def assert_background_preserved(self,old):
  previous=json.loads(old['manifest.json']);current=json.loads(self.backgrounds['manifest.json']);oa=old['atlas.bin'];ca=self.backgrounds['atlas.bin']
  self.assertEqual(block(None,oa,previous['basePalette']),block(None,ca,current['basePalette']))
  for key in ['terrains','moves','overlays']:
   new={r['id']:r for r in current[key]};prior={r['id']:r for r in previous[key]}
   self.assertEqual({2} if key=='moves' and 2 not in prior else set(),set(new)-set(prior))
   for identity,row in prior.items():self.assertEqual(content_row(row,oa),content_row(new[identity],ca),(key,identity))
 def test_ghost_background_matches_exact_source_and_preserves_all_previous_blocks(self):
  self.assert_background_preserved(self.previous_backgrounds);m=json.loads(self.backgrounds['manifest.json']);a=self.backgrounds['atlas.bin'];ghost=next(r for r in m['moves'] if r['id']==2)
  tiles,palette=bg.indexed_tiles(bg.read(REFERENCE,'graphics/battle_anims/backgrounds/ghost.png'));tilemap=bg.read(REFERENCE,'graphics/battle_anims/backgrounds/ghost.bin');self.assertEqual([8192,2048,32],[len(tiles),len(tilemap),len(palette)])
  self.assertEqual(tiles,block(m,a,ghost['tiles']));self.assertEqual(tilemap,block(m,a,ghost['tilemap']));self.assertEqual(palette,block(m,a,ghost['palette']))
 def test_ghost_missing_table_and_invalid_tilemap_fail_closed(self):
  with self.assertRaises(ValueError):self.changed_build('src/data/battle_anim.h',b'[BG_GHOST] ',b'[BG_UNKNOWN_GHOST] ',bg)
  raw=bg.read(REFERENCE,'graphics/battle_anims/backgrounds/ghost.bin')
  with self.assertRaises(ValueError):self.changed_build('graphics/battle_anims/backgrounds/ghost.bin',raw,struct.pack('<H',1023)+raw[2:],bg)
 def test_two_builds_and_staged_generated_files_are_byte_identical(self):
  data,pixels=imp.build(REFERENCE);self.assertEqual(self.data,data);self.assertEqual(self.pixels,pixels);self.assertEqual(self.backgrounds,bg.build(REFERENCE))
  if opts.generated:
   self.assertEqual(data,(opts.generated/'elemental.json').read_bytes())
   for name,blob in pixels.items():self.assertEqual(blob,(opts.generated/('elemental-'+name if name.endswith(('.4bpp','.pal16')) else 'elemental-'+name+'.rgb555')).read_bytes())
   for name,blob in self.backgrounds.items():self.assertEqual(blob,(opts.generated/'backgrounds'/name).read_bytes())
if __name__=='__main__':unittest.main()
