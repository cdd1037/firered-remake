"""Exact cure roots, source frames, palette-bank selection and indexed OBJ-window input."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);candidate=a.candidate/Path(path).name if a.candidate else ROOT/path
 exec(compile(candidate.read_text(),str(ROOT/path),'exec'),m.__dict__);return m
physical=module('cure_physical','scripts/imports/battle/import-source-physical-animations.py');background=module('cure_background','scripts/imports/battle/import-source-animation-backgrounds.py')
class CureImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=physical.build(RAW);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=physical.read_sources(RAW);cls.assets=physical.cure_materials(cls.raw);cls.bg=background.build(RAW)
 def test_capability_closure_admits_only_three_complete_source_roots(self):
  with patch.object(physical,'SPRITES',physical.SPRITES-{r[0] for r in physical.CURE_RECIPES}),patch.object(physical,'TASKS',physical.TASKS-physical.CURE_TASKS):prior=json.loads(physical.build(RAW)[0])
  old={r['moveId']:r for r in prior['scripts']};self.assertEqual({215,287,312},self.rows.keys()-old.keys())
  for i,row in old.items():self.assertEqual(row,self.rows[i],i)
  self.assertFalse({215,287,312}&{r['moveId'] for r in self.blocked})
 def test_literal_root_and_helper_commands_and_sound_dependencies_are_preserved(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in [215,287,312]:
   row=self.rows[move];commands=row['commands']+[c for call in row['commands'] if call['op']=='call' for c in self.pack['subroutines'][call['args'][0]]]
   for c in commands:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[s.strip() for s in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
   sounds={c['args'][0] for c in commands if c['op'] in ['playsewithpan','loopsewithpan','playse']};self.assertEqual(sounds,row['soundEffects'].keys())
  self.assertEqual(['cure-bubbles'],self.rows[287]['overlayDependencies']);self.assertEqual(['cure-bubbles'],self.rows[312]['overlayDependencies']);self.assertNotIn('overlayDependencies',self.rows[215]);self.assertEqual(4,sum(c['op']=='unloadspritegfx' for c in self.rows[215]['commands']))
 def test_original_palette_eighty_colors_selects_only_three_native_banks(self):
  lines=self.raw['graphics/battle_anims/sprites/music_notes_2.pal'].decode().splitlines();self.assertEqual(['JASC-PAL','0100','80'],lines[:3]);rgb=[list(map(int,line.split())) for line in lines[3:]]
  expected=struct.pack('<48H',*[sum((c//8)<<(5*i) for i,c in enumerate(row)) for row in rgb[:48]])
  files=self.assets[2];self.assertEqual(expected,files['music-notes-2.pal48']);self.assertEqual(expected[:32],files['music-notes-2.pal16']);self.assertEqual([dict(tag=10206,path='physical-music-notes-2.pal48',banks=3)],self.pack['paletteGroups'])
  source=self.raw['src/battle_anim_effects_2.c'].decode();self.assertRegex(source,r'#define NUM_MUSIC_NOTE_PAL_TAGS\s+3');self.assertRegex(source,r'gMusicNotePaletteTagsTable\[NUM_MUSIC_NOTE_PAL_TAGS\] =\s*\{\s*ANIM_TAG_MUSIC_NOTES_2,\s*ANIM_SPRITES_START - 1,\s*ANIM_SPRITES_START - 2,\s*\}')
  self.assertIn('LoadPalette(&gMonSpritesGfxPtr->multiUseBuffer[i * 32], OBJ_PLTT_ID(paletteNums[i]), PLTT_SIZE_4BPP);',source)
 def test_indexed_sheets_preserve_all_source_tiles_and_concatenation(self):
  for stem,parts in [('bell',['bell']),('music-notes-2',['music_notes_2']),('flower',['flower_0','flower_1'])]:
   indices=[]
   for part in parts:
    with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+part+'.png'])) as im:indices += [b for y in range(0,im.height,8) for x in range(0,im.width,8) for b in im.crop((x,y,x+8,y+8)).tobytes()]
   expected=bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2));self.assertEqual(expected,self.assets[2][stem+'.4bpp'])
  self.assertEqual(160,len(self.assets[2]['flower.4bpp']));self.assertEqual(2048,len(self.assets[2]['music-notes-2.4bpp']));self.assertEqual(1536,len(self.assets[2]['bell.4bpp']))
 def test_frame_and_affine_recipes_keep_original_termination_and_loops(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']};bell=recipes['gBellSpriteTemplate'];self.assertEqual([0,16,32,16,0,16,32,16,0,16,32,16,0],[f['tile'] for f in bell['animations'][0]]);self.assertEqual([6,6,15,6,6,6,15,6,6,6,15,6,6],[f['duration'] for f in bell['animations'][0]]);self.assertEqual([-1],bell['loopStarts'])
  flower=recipes['gAromatherapyBigFlowerSpriteTemplate'];self.assertEqual(['frame','frame','jump'],[f['op'] for f in flower['affineAnimations'][0]]);self.assertEqual(1,flower['affineAnimations'][0][-1]['target']);self.assertEqual(4,recipes['gAromatherapySmallFlowerSpriteTemplate']['animations'][0][0]['tile'])
  for wrapper,shared in [('gSparklingStarsSpriteTemplate','gGrantingStarsSpriteTemplate'),('gBlendThinRingExpandingSpriteTemplate','gThinRingExpandingSpriteTemplate')]:
   original=recipes.get(shared)
   if original is None:
    file='battle_anim_effects_1.c' if shared=='gGrantingStarsSpriteTemplate' else 'battle_anim_effects_2.c';callback='AnimGrantingStars' if shared=='gGrantingStarsSpriteTemplate' else 'AnimSpriteOnMonPos';original=physical.elemental.recipe(self.raw['src/'+file].decode(),shared,callback)
   for field in ['animations','loopStarts','affineAnimations']:self.assertEqual(original[field],recipes[wrapper][field])
 def test_bg1_mask_keeps_literal_indexed_map_and_palette_bank(self):
  manifest=json.loads(self.bg['manifest.json']);row=next(x for x in manifest['overlays'] if x['id']=='cure-bubbles');atlas=self.bg['atlas.bin'];read=lambda key:atlas[row[key][0]:sum(row[key])]
  self.assertEqual(8,row['paletteBank']);self.assertEqual((RAW/'graphics/battle_anims/masks/cure_bubbles.bin').read_bytes(),read('tilemap'));tiles,pal=background.indexed_tiles((RAW/'graphics/battle_anims/masks/cure_bubbles.png').read_bytes());self.assertEqual(tiles.ljust(8192,b'\0'),read('tiles'));self.assertEqual(pal,read('palette'))
 def test_malformed_palette_image_and_registration_fail_closed(self):
  for file,old,new in [('src/data/battle_anim.h','gBattleAnimSpriteGfx_Bell, 0x0600','gBattleAnimSpriteGfx_Bell, 0x0200'),('src/graphics.c','music_notes_2.4bpp.lz','music_notes.4bpp.lz'),('graphics/battle_anims/sprites/music_notes_2.pal','\n80\n','\n48\n')]:
   raw=dict(self.raw);self.assertIn(old,raw[file].decode());raw[file]=raw[file].decode().replace(old,new).encode()
   with self.assertRaises(ValueError):physical.cure_materials(raw)
 def test_generated_bytes_equal_maintained_producers(self):
  folder=a.generated or ROOT/'content/generated/battle/animations';self.assertEqual(self.data,(folder/'physical.json').read_bytes())
  for name,blob in {**physical.build_assets(RAW),**physical.build_indexed_assets(RAW)[1]}.items():
   path='physical-'+name if name.endswith(('.4bpp','.pal16','.pal48')) else 'physical-'+name+'.rgb555';self.assertEqual(blob,(folder/path).read_bytes(),path)
  for name,blob in self.bg.items():self.assertEqual(blob,(folder/'backgrounds'/name).read_bytes())
if __name__=='__main__':unittest.main()
