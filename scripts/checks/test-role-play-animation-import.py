"""Role Play root and locally converted source base-mon frame contracts."""
import argparse,importlib.util,io,json,re,struct,sys,unittest
from pathlib import Path
from unittest.mock import patch
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--repo',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--candidate',type=Path);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining];ROOT=a.repo;reference=a.reference or ROOT/'content/raw/pokefirered/037335f4';generated=a.generated or ROOT/'content/generated'
def load(name,relative):
 path=ROOT/relative;spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec)
 if a.candidate:exec(compile((a.candidate/relative).read_text(),str(path),'exec'),module.__dict__)
 else:spec.loader.exec_module(module)
 return module
imp=load('role_play_import','scripts/imports/battle/import-source-elemental-animations.py');hit=load('role_play_art','scripts/imports/battle/import-source-hit-animations.py')
if a.candidate:
 lock=json.loads((a.candidate/'scripts/recipes/battle/source-hit-animation.lock.json').read_bytes());hit.read_sources=lambda r:{x['path']:(r/x['path']).read_bytes() for x in lock['files']}
class RolePlayImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.data,cls.pixels=imp.build(reference);cls.pack=json.loads(cls.data);cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.hit=hit.build(reference)
 def test_only_closed_root_is_added_and_existing_materials_stay_identical(self):
  with patch.object(imp,'TASKS',imp.TASKS-imp.ADDITIONAL_MON_TASKS):old,pixels=imp.build(reference)
  old=json.loads(old);roots={r['moveId']:r for r in old['scripts']};self.assertEqual({272},set(self.roots)-set(roots));self.assertEqual(self.pixels,pixels)
  for move,row in roots.items():self.assertEqual(row,self.roots[move])
  for key in set(old)-{'scripts'}:self.assertEqual(old[key],self.pack[key])
 def test_root_preserves_source_order_lines_and_both_sounds(self):
  root=self.roots[272];self.assertEqual('Move_ROLE_PLAY',root['label']);self.assertEqual({},root['spriteTags']);self.assertEqual({'SE_M_DETECT':202,'SE_M_TRI_ATTACK':213},root['soundEffects'])
  self.assertEqual(['monbg','createvisualtask','createsprite','waitforvisualfinish','playsewithpan','waitplaysewithpan','createvisualtask','waitforvisualfinish','clearmonbg','createvisualtask','delay','createsprite','end'],[r['op'] for r in root['commands']]);self.assertEqual(['AnimTask_RolePlaySilhouette','2'],root['commands'][6]['args']);self.assertEqual(8603,root['commands'][6]['line']);self.assertNotIn('spriteDependencies',root)
 def test_missing_callback_or_sound_fails_closed(self):
  for attr,value in [('TASKS',imp.TASKS-imp.ADDITIONAL_MON_TASKS),('SOUNDS',imp.SOUNDS-{'SE_M_TRI_ATTACK'}),('SOUNDS',imp.SOUNDS-{'SE_M_DETECT'})]:
   with patch.object(imp,attr,value):data,_=imp.build(reference)
   self.assertNotIn(272,{r['moveId'] for r in json.loads(data)['scripts']})
 def test_additional_mon_frame_changes_no_existing_art_or_hit_scripts(self):
  with patch.object(hit,'additional_mon_assets',return_value=([],{})):old=hit.build(reference)
  self.assertEqual(old['source-hit-animations'],self.hit['source-hit-animations']);before=json.loads(old['campaign-hit-art']['manifest.json']);after=json.loads(self.hit['campaign-hit-art']['manifest.json']);self.assertEqual(before['geometry'],after['geometry']);self.assertEqual(before['formGeometry'],after['formGeometry']);self.assertEqual(before['assets'],[r for r in after['assets'] if 'frame' not in r])
  for name,blob in old['campaign-hit-art'].items():
   if name!='manifest.json':self.assertEqual(blob,self.hit['campaign-hit-art'][name])
 def test_deoxys_source_first_0x800_frame_and_palette_are_exact(self):
  art=self.hit['campaign-hit-art'];manifest=json.loads(art['manifest.json']);rows=[r for r in manifest['assets'] if 'frame' in r];self.assertEqual(4,len(rows))
  for row in rows:
   self.assertEqual(410,row['species']);self.assertEqual(0,row['frame']);self.assertNotIn('form',row)
   with Image.open(reference/f"graphics/pokemon/deoxys/{row['side']}.png") as image:
    self.assertEqual('P',image.mode);self.assertEqual((64,128),image.size);first=image.crop((0,0,64,64)).tobytes();second=image.crop((0,64,64,128)).tobytes();self.assertNotEqual(first,second)
   palette=[tuple(map(int,line.split())) for line in (reference/f"graphics/pokemon/deoxys/{row['paletteVariant']}.pal").read_text().splitlines()[3:]]
   expected=[(palette[i][0]>>3)|((palette[i][1]>>3)<<5)|((palette[i][2]>>3)<<10)|(0x8000 if i else 0) for i in first]
   actual=art[row['path']];self.assertEqual((64,64),struct.unpack('<HH',actual[:4]));self.assertEqual(expected,list(struct.unpack('<4096H',actual[4:])))
 def test_base_unown_geometry_differs_from_personality_selected_bitmap_and_spinda_preserves_alpha(self):
  geometry=json.loads(self.hit['campaign-hit-art']['manifest.json'])['geometry'];get=lambda species:next(r for r in geometry if r['species']==species and r['side']=='front');self.assertEqual(15,get(201)['yOffset']);self.assertEqual(16,get(415)['yOffset'])
  source=(reference/'src/pokemon.c').read_text()
  for symbol,value in [('FIRST_SPOT_COLOR',1),('LAST_SPOT_COLOR',3),('SPOT_COLOR_ADJUSTMENT',4)]:self.assertTrue(re.search(r'^#define '+symbol+r'\s+'+str(value)+r'\b',source,re.M),symbol)
  for i in range(16):self.assertEqual(i==0,(i+4 if 1<=i<=3 else i)==0)
 def test_generated_candidates_equal_rebuilt_bytes(self):
  self.assertEqual(self.data,(generated/'battle/animations/elemental.json').read_bytes())
  for folder,files in self.hit.items():
   path=generated/('battle/animations/hit' if folder=='source-hit-animations' else 'battle/art/campaign-hit-art')
   for name,blob in files.items():self.assertEqual(blob,(path/name).read_bytes())
if __name__=='__main__':unittest.main()
