#!/usr/bin/env python3
"""Source-pixel Silph portraits; exact lock and source bindings, no source execution."""
import argparse,importlib.util,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('art',ROOT/'scripts/import-silph-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
meta=art.meta
PROFILE='firered-silph-trainer-art-v1'
PORTRAITS=[('rocket-grunt-m-front','RocketGruntM','rocket_grunt_m',109),('rocket-grunt-f-front','RocketGruntF','rocket_grunt_f',137),('scientist-front','Scientist','scientist',107),('rival-late-front','RivalLate','rival_late',124),('giovanni-front','LeaderGiovanni','leader_giovanni',108)]
def build(root,reference=None):
 lock,raw=meta.authenticated(root,reference)
 def need(p,text):meta.require(text in raw[p].decode(),'Source portrait binding differs: '+text)
 need('src/battle_controller_opponent.c','(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
 table='src/data/trainer_graphics/front_pic_tables.h';coords=re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}',raw[table].decode().split('const struct CompressedSpriteSheet')[0]);out={};portraits=[]
 for identity,symbol,stem,picture in PORTRAITS:
  image=f'graphics/trainers/front_pics/{stem}_front_pic.png';palette=f'graphics/trainers/palettes/{stem}.pal'
  need('src/data/graphics/trainers.h',f'gTrainerFrontPic_{symbol}[] = INCBIN_U32("graphics/trainers/front_pics/{stem}_front_pic.4bpp.lz")')
  need('src/data/graphics/trainers.h',f'gTrainerPalette_{symbol}[] = INCBIN_U32("graphics/trainers/palettes/{stem}.gbapal.lz")')
  need(table,f'TRAINER_SPRITE({stem.upper()}, gTrainerFrontPic_{symbol}, 0x800)');need(table,f'TRAINER_PAL({stem.upper()}, gTrainerPalette_{symbol})')
  numeric=re.search(r'^#define TRAINER_PIC_'+stem.upper()+r'\s+(\d+)$',raw['include/constants/trainers.h'].decode(),re.M);meta.require(numeric and int(numeric[1])==picture and coords[picture]==('8','1'),'Trainer portrait coordinates differ')
  im=Image.open(io.BytesIO(raw[image]));meta.require(im.mode=='P' and im.size==(64,64),'Portrait source pixels differ');ix=im.tobytes();lines=raw[palette].decode().splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19 and max(ix)<16,'Source portrait palette differs');pal=[tuple((int(c)//8<<3)|(int(c)//8>>2) for c in line.split()) for line in lines[3:]]
  rgba=bytes(c for i in ix for c in (*pal[i],255 if i else 0));png=art.png(64,64,rgba);out[identity+'.png']=png
  portraits.append(dict(id=identity, sourceTrainerPicId=picture, trainerPic='TRAINER_PIC_' + stem.upper(), sourceImage=image, sourcePalette=palette, width=64, height=64, x=144, y=8, sourceFrame=0, texturePath=identity + '.png'))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=meta.COMMIT,inputs=lock['files'],portraits=portraits,paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',outputs=[dict(path=p) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();outputs=build(a.project_root,a.reference_root)
 for rel,b in outputs.items():
  target=meta.safe(a.project_root,'content/generated/silph-trainer-art/'+rel)
  if a.check:meta.require(target.read_bytes()==b,'Portrait output differs: '+rel)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(portraits=5,bytes=len(outputs['manifest.json']))))
