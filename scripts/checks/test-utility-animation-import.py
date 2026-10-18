"""Complete utility source roots, raw-child dependencies and indexed material authority."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,rest=p.parse_known_args();sys.argv=[sys.argv[0],*rest];ROOT=a.repo;RAW=a.reference or ROOT/'content/raw/pokefirered/037335f4'
def module(name,path):
 spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);candidate=a.candidate/Path(path).name if a.candidate else ROOT/path;exec(compile(candidate.read_text(),str(ROOT/path),'exec'),m.__dict__);return m
physical=module('utility_physical','scripts/imports/battle/import-source-physical-animations.py')
class UtilityImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.blocked=physical.build(RAW);cls.pack=json.loads(cls.data);cls.rows={r['moveId']:r for r in cls.pack['scripts']};cls.raw=physical.read_sources(RAW);cls.assets=physical.utility_materials(cls.raw)
 def test_new_callbacks_close_exactly_four_source_roots(self):
  with patch.object(physical,'SPRITES',physical.SPRITES-{'gYawnCloudSpriteTemplate','gSpikesSpriteTemplate'}),patch.object(physical,'TASKS',physical.TASKS-physical.UTILITY_TASKS):prior=json.loads(physical.build(RAW)[0])
  old={r['moveId']:r for r in prior['scripts']};self.assertEqual({191,281,285,288},self.rows.keys()-old.keys());self.assertFalse({191,281,285,288}&{r['moveId'] for r in self.blocked})
  for i,row in old.items():self.assertEqual(row,self.rows[i])
 def test_literal_root_and_psychic_helper_commands_and_sounds(self):
  lines=self.raw['data/battle_anim_scripts.s'].decode().splitlines()
  for move in [191,281,285,288]:
   row=self.rows[move];commands=row['commands']+[c for call in row['commands'] if call['op']=='call' for c in self.pack['subroutines'][call['args'][0]]]
   for c in commands:
    text=lines[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else [];self.assertEqual((op,args),(c['op'],c['args']))
   sounds={c['args'][0] for c in commands if c['op'] in ['playsewithpan','waitplaysewithpan','loopsewithpan','playse']};self.assertEqual(sounds,row['soundEffects'].keys())
  self.assertEqual(['sSkillSwapOrbSpriteTemplate'],self.rows[285]['spriteDependencies']);self.assertEqual(['gGrudgeFlameSpriteTemplate'],self.rows[288]['spriteDependencies']);self.assertIn('sDeepInhaleAffineAnimCmds',self.pack['taskAffinePrograms']);self.assertIn('BG_PSYCHIC',self.pack['backgroundSymbols']);self.assertIn('BG_GHOST',self.pack['backgroundSymbols'])
 def test_indexed_material_preserves_every_source_pixel_palette_and_tile(self):
  for stem,symbol,size,shape in physical.UTILITY_MATERIALS:
   with Image.open(io.BytesIO(self.raw['graphics/battle_anims/sprites/'+stem+'.png'])) as im:
    indices=[v for y in range(0,im.height,8) for x in range(0,im.width,8) for v in im.crop((x,y,x+8,y+8)).tobytes()];pal=im.getpalette()[:48]
   name=stem.replace('_','-');self.assertEqual(size,len(self.assets[2][name+'.4bpp']));self.assertEqual(bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),self.assets[2][name+'.4bpp']);self.assertEqual(struct.pack('<16H',*[sum((pal[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]),self.assets[2][name+'.pal16'])
   row=next(x for x in self.assets[1] if x['id']==name);self.assertEqual('ANIM_TAG_'+stem.upper(),row['paletteTag']);self.assertEqual(shape,(row['width'],row['height']))
 def test_all_child_and_cloud_frame_affine_programs_match_source_parser(self):
  recipes={r['template']:r for r in self.pack['spriteRecipes']}
  for name,file,callback in physical.UTILITY_RECIPES:self.assertEqual(physical.elemental.recipe(self.raw['src/'+file].decode(),name,callback),recipes[name])
  self.assertEqual(3,len(recipes['gYawnCloudSpriteTemplate']['affineAnimations']));self.assertEqual(4,len(recipes['sSkillSwapOrbSpriteTemplate']['affineAnimations']));self.assertEqual([0,8,16,24],[f['tile'] for f in recipes['gGrudgeFlameSpriteTemplate']['animations'][0]])
 def test_malformed_material_linkage_allocation_shape_and_indices_fail(self):
  for file,old,new in [('src/data/battle_anim.h','gBattleAnimSpriteGfx_PinkCloud, 0x0200','gBattleAnimSpriteGfx_PinkCloud, 0x0080'),('src/graphics.c','bluegreen_orb.gbapal.lz','blue_orb.gbapal.lz')]:
   raw=dict(self.raw);self.assertIn(old,raw[file].decode());raw[file]=raw[file].decode().replace(old,new).encode()
   with self.assertRaises(ValueError):physical.utility_materials(raw)
  for mode,size in [('P',(8,8)),('RGB',(32,32))]:
   raw=dict(self.raw);buf=io.BytesIO();Image.new(mode,size).save(buf,format='PNG');raw['graphics/battle_anims/sprites/pink_cloud.png']=buf.getvalue()
   with self.assertRaises(ValueError):physical.utility_materials(raw)
  raw=dict(self.raw);buf=io.BytesIO();im=Image.new('P',(32,32),16);im.putpalette(list(range(256))*3);im.save(buf,format='PNG');raw['graphics/battle_anims/sprites/pink_cloud.png']=buf.getvalue()
  with self.assertRaises(ValueError):physical.utility_materials(raw)
 def test_generated_bytes_equal_the_maintained_producer(self):
  folder=a.generated or ROOT/'content/generated/battle/animations';self.assertEqual(self.data,(folder/'physical.json').read_bytes())
  for name,blob in {**self.assets[0],**self.assets[2]}.items():
   path='physical-'+name if name.endswith(('.4bpp','.pal16')) else 'physical-'+name+'.rgb555';self.assertEqual(blob,(folder/path).read_bytes())
if __name__=='__main__':unittest.main()
