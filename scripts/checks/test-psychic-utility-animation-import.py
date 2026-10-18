#!/usr/bin/env python3
"""Independently verify three psychic roots, finite commands and every material texel.

Reads published output only; does not regenerate evidence or run the full producer.
"""
import argparse,hashlib,importlib.util,json,re,struct,sys,unittest
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
p=argparse.ArgumentParser(add_help=False);p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);p.add_argument('--reference',type=Path);p.add_argument('--generated',type=Path);a,remaining=p.parse_known_args();sys.argv=[sys.argv[0],*remaining]
ROOT=a.root;REFERENCE=a.reference or ROOT/'content/raw/pokefirered/037335f4';GENERATED=a.generated or ROOT/'content/generated/battle/animations'
spec=importlib.util.spec_from_file_location('psychic_frames',ROOT/'scripts/lib/source_animation_frames.py');frames=importlib.util.module_from_spec(spec);spec.loader.exec_module(frames)
class PsychicUtilityImportTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.pack=json.loads((GENERATED/'elemental.json').read_bytes());cls.roots={r['moveId']:r for r in cls.pack['scripts']};cls.recipes={r['template']:r for r in cls.pack['spriteRecipes']}
 def test_each_selected_command_and_call_is_the_pinned_authored_source(self):
  source=(REFERENCE/'data/battle_anim_scripts.s').read_text().splitlines()
  for move in (134,244,295):
   root=self.roots[move];commands=root['commands']+[c for row in root['commands'] if row['op']=='call' for c in self.pack['subroutines'][row['args'][0]]]
   self.assertTrue(commands)
   for c in commands:
    text=source[c['line']-1].split('@',1)[0].strip();op,*tail=text.split(None,1);args=[v.strip() for v in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    self.assertEqual((op,args),(c['op'],c['args']))
  self.assertEqual(3,sum(c['op']=='createsprite' for c in self.roots[134]['commands']));self.assertEqual(1,sum(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_BlendColorCycleExclude' for c in self.roots[244]['commands']))
  self.assertEqual(6,sum(c['op']=='createsprite' and c['args'][0]=='gRandomPosHitSplatSpriteTemplate' for c in self.roots[295]['commands']))
 def test_every_source_pixel_index_palette_entry_and_rgb555_pixel(self):
  for asset,png,size in [('alert','alert',(32,112)),('bent-spoon','bent_spoon',(16,192)),('spiral','spiral',(64,64)),('white-circle-of-light','circle_of_light',(64,64))]:
   with self.subTest(asset=asset),Image.open(REFERENCE/('graphics/battle_anims/sprites/'+png+'.png')) as im:
    self.assertEqual('P',im.mode);self.assertEqual(size,im.size);indices=list(im.tobytes());palette=im.getpalette()[:48]
    if asset=='white-circle-of-light':
     rows=(REFERENCE/'graphics/battle_anims/sprites/white_circle_of_light.pal').read_text().splitlines();self.assertEqual(['JASC-PAL','0100','16'],rows[:3]);palette=[int(v) for line in rows[3:] for v in line.split()]
    self.assertEqual(48,len(palette));self.assertTrue(all(0<=i<16 for i in indices))
    colors=[(palette[i*3]>>3)|((palette[i*3+1]>>3)<<5)|((palette[i*3+2]>>3)<<10) for i in range(16)]
    tiled=[im.getpixel((x+dx,y+dy)) for y in range(0,im.height,8) for x in range(0,im.width,8) for dy in range(8) for dx in range(8)]
    packed=bytes(tiled[i]|(tiled[i+1]<<4) for i in range(0,len(tiled),2));row=next(r for r in self.pack['indexedAssets'] if r['id']==asset)
    self.assertEqual(size,(row['width'],row['height']));self.assertEqual('ANIM_TAG_'+asset.upper().replace('-','_'),row['paletteTag']);self.assertEqual(packed,(GENERATED/row['indicesPath']).read_bytes());self.assertEqual(struct.pack('<16H',*colors),(GENERATED/row['palettePath']).read_bytes())
    pixels=struct.pack('<HH',*size)+struct.pack('<'+'H'*len(indices),*[colors[i]|(0x8000 if i else 0) for i in indices]);self.assertEqual(pixels,(GENERATED/('elemental-'+asset+'.rgb555')).read_bytes())
 def test_native_finite_loops_retain_their_command_boundaries(self):
  for template,file,arrays in [('gBentSpoonSpriteTemplate','battle_anim_psychic.c',['sAnim_BentSpoon_0','sAnim_BentSpoon_1']),('gKinesisZapEnergySpriteTemplate','battle_anim_effects_2.c',['sKinesisZapEnergyAnimCmds'])]:
   source=(REFERENCE/('src/'+file)).read_text();r=self.recipes[template]
   for i,array in enumerate(arrays):
    body=re.search(r'const union AnimCmd '+array+r'\[\]\s*=\s*\{(.*?)\};',source,re.S)[1];f,j,c=frames.frame_program(body);self.assertEqual(f,r['animations'][i]);self.assertEqual(j,r['loopStarts'][i]);self.assertEqual(c,r['frameLoops'][i]);self.assertEqual(1,len(c));self.assertEqual(1,c[0]['count'])
  for body in ['ANIMCMD_FRAME(0, 1)\nANIMCMD_LOOP(1)\nANIMCMD_LOOP(1)\nANIMCMD_END','ANIMCMD_FRAME(0, 64)\nANIMCMD_END','ANIMCMD_FRAME(0, 1)\nANIMCMD_JUMP(1)','ANIMCMD_FRAME(0, 1)\nANIMCMD_END\nANIMCMD_FRAME(1, 1)']:
   with self.assertRaises(ValueError):frames.frame_program(body)
 def test_native_oracle_inputs_match_reviewed_source(self):
  pins=json.loads((ROOT/'tests/Reference/PsychicUtility/pinned-inputs.json').read_text());self.assertTrue(pins)
  for path,expected in pins.items():self.assertEqual(expected,hashlib.sha256((REFERENCE/path).read_bytes()).hexdigest(),path)
if __name__=='__main__':unittest.main()
