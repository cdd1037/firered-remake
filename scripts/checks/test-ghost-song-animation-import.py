"""Focused literal ghost/song recipes and partial-palette material contracts."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--raw-overlay',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=ROOT/'content/raw/pokefirered/037335f4'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);candidate=a.candidate/Path(path).name if a.candidate else ROOT/path;exec(compile(candidate.read_text(),str(ROOT/path),'exec'),m.__dict__);return m
physical=module('ghost_physical','scripts/imports/battle/import-source-physical-animations.py');background=module('ghost_background','scripts/imports/battle/import-source-animation-backgrounds.py')
def read(path):
 candidate=a.raw_overlay/path if a.raw_overlay else RAW/path;return (candidate if candidate.exists() else RAW/path).read_bytes()
class GhostSongImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  paths=['src/battle_anim_effects_1.c','src/battle_anim_effects_2.c','src/battle_anim_ghost.c','src/data/battle_anim.h','src/graphics.c']+['graphics/battle_anims/sprites/'+x[0]+'.png' for x in physical.GHOST_SONG_MATERIALS];cls.raw={p:read(p) for p in paths};cls.assets=physical.utility_materials(cls.raw,physical.GHOST_SONG_MATERIALS)
 def test_all_three_actual_generated_roots_keep_source_tags_raw_children_and_both_curse_branches(self):
  pack=json.loads((ROOT/'content/generated/battle/animations/physical.json').read_text());roots={r['moveId']:r for r in pack['scripts']}
  for move in [174,194,195]:self.assertIn(move,roots,'Actual canonical producer did not close this family root')
  self.assertEqual(['gDestinyBondWhiteShadowSpriteTemplate'],roots[194]['spriteDependencies']);self.assertIn('ANIM_TAG_WHITE_SHADOW',roots[194]['spriteTags']);self.assertIn('curse',roots[174]['overlayDependencies'])
  self.assertTrue({'ANIM_TAG_NAIL','ANIM_TAG_GHOSTLY_SPIRIT'}<=roots[174]['spriteTags'].keys());self.assertEqual('choosetwoturnanim',roots[174]['commands'][0]['op']);self.assertEqual({'CurseGhost','CurseStats'},set(roots[174]['commands'][0]['args']));self.assertEqual(['ANIM_TAG_MUSIC_NOTES_2'],list(roots[195]['spriteTags']))
 def test_all_five_literal_source_frame_affine_programs(self):
  rows=physical.ghost_song_recipes(self.raw);self.assertEqual(5,len(rows));self.assertEqual(['music-notes-2','music-notes-2','white-shadow','nail','ghostly-spirit'],[r['asset'] for r in rows]);self.assertEqual([(16,16),(16,16),(64,32),(32,16),(32,32)],[(r['width'],r['height']) for r in rows])
  for row in rows[:2]:
   self.assertEqual([-1]*8,row['loopStarts']);self.assertEqual([(i%6*4,41 if i==2 else 10,i>=6) for i in range(8)],[(a[0]['tile'],a[0]['duration'],a[0]['vFlip']) for a in row['animations']]);self.assertEqual([(0,5),(248,16),(8,16)],[(a[0]['rotation'],a[0]['duration']) for a in row['affineAnimations']])
  for row in rows[2:]:self.assertEqual([0],row['loopStarts']);self.assertEqual([],row['affineAnimations']);self.assertTrue(row['blend'])
  raw=dict(self.raw);raw['src/battle_anim_effects_1.c']=raw['src/battle_anim_effects_1.c'].replace(b'gMusicNotesAnimTable',b'missingTable')
  with self.assertRaises(ValueError):physical.ghost_song_recipes(raw)
 def test_new_assets_match_every_source_index_color_and_unchanged_shared_note_owner(self):
  for stem,symbol,size,shape in physical.GHOST_SONG_MATERIALS:
   png=self.raw['graphics/battle_anims/sprites/'+stem+'.png'];name=stem.replace('_','-')
   with Image.open(io.BytesIO(png)) as im:indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];pal=im.getpalette()[:48]
   self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.assets[2][name+'.4bpp']);self.assertEqual(struct.pack('<16H',*[sum((pal[3*i+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),self.assets[2][name+'.pal16']);self.assertEqual(physical.hit.rgb555(png,shape),self.assets[0][name]);self.assertEqual(size,len(self.assets[2][name+'.4bpp']))
  self.assertNotIn('music-notes-2',self.assets[0]);self.assertNotIn('gDestinyBondWhiteShadowSpriteTemplate',physical.SPRITES)
 def test_bad_registration_pixels_palette_link_and_shape_fail(self):
  for file,old,new in [('src/data/battle_anim.h','gBattleAnimSpriteGfx_Nail, 0x0400','gBattleAnimSpriteGfx_Nail, 0x0200'),('src/graphics.c','white_shadow.gbapal.lz','nail.gbapal.lz')]:
   raw=dict(self.raw);self.assertIn(old,raw[file].decode());raw[file]=raw[file].decode().replace(old,new).encode()
   with self.assertRaises(ValueError):physical.utility_materials(raw,physical.GHOST_SONG_MATERIALS)
  for mode,shape,color in [('P',(8,8),0),('RGB',(32,32),0),('P',(32,32),16)]:
   raw=dict(self.raw);buf=io.BytesIO();im=Image.new(mode,shape,color)
   if mode=='P':im.putpalette(list(range(256))*3)
   im.save(buf,format='PNG');raw['graphics/battle_anims/sprites/ghostly_spirit.png']=buf.getvalue()
   with self.assertRaises(ValueError):physical.utility_materials(raw,physical.GHOST_SONG_MATERIALS)
 def test_curse_overlay_uses_only_the_single_source_loaded_white_index(self):
  self.assertEqual(('graphics/battle_anims/masks/curse.png','graphics/battle_anims/masks/curse.bin'),background.curse_overlay_inputs(read));tiles,pal=background.indexed_tiles(read('graphics/battle_anims/masks/curse.png'));self.assertEqual(96,len(tiles));self.assertEqual(32767,struct.unpack('<16H',pal)[1]);self.assertEqual(2048,len(read('graphics/battle_anims/masks/curse.bin')))
  for path,old,new in [('src/battle_anim_utility_funcs.c',b'BG_PLTT_ID(animBgData.paletteId) + 1, PLTT_SIZEOF(1)',b'BG_PLTT_ID(animBgData.paletteId), PLTT_SIZE_4BPP'),('src/battle_anim_utility_funcs.c',b'{ RGB_WHITE }',b'{ RGB_BLACK }'),('src/graphics.c',b'masks/curse.4bpp.lz',b'masks/cure_bubbles.4bpp.lz')]:
   source=read(path);self.assertIn(old,source)
   with self.assertRaises(ValueError):background.curse_overlay_inputs(lambda p:source.replace(old,new) if p==path else read(p))
  buf=io.BytesIO();im=Image.new('P',(8,24),2);im.putpalette([0,0,0,255,255,255]+[0]*762);im.save(buf,format='PNG')
  with self.assertRaises(ValueError):background.curse_overlay_inputs(lambda p:buf.getvalue() if p.endswith('/curse.png') else read(p))
if __name__=='__main__':unittest.main()
