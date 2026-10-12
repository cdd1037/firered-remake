#!/usr/bin/env python3
"""Source-pixel Saffron Gym portraits; exact lock and source bindings, no source execution."""
import argparse,hashlib,importlib.util,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('art',ROOT/'scripts/import-saffron-gym-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
meta=art.meta
PROFILE='firered-saffron-gym-trainer-art-v1'
LOCK_SHA='fa1f08584e533ca0de9d0b53f5929ffb42d0adf6e4ee005664ee8213ee3be9bb'
PORTRAITS=[('psychic-m-front','PsychicM','psychic_m',100),('sabrina-front','LeaderSabrina','leader_sabrina',122)]
def build(root,reference=None):
 lb=meta.safe(root,'scripts/saffron-gym-presentation.lock.json').read_bytes();meta.require(meta.sha(lb)==LOCK_SHA,'Saffron Gym portrait lock differs');lock=json.loads(lb)
 meta.require((lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(meta.COMMIT,meta.TREE,meta.PROFILE,31),'Saffron Gym portrait scope differs');raw={}
 for row in lock['files']:
  p=meta.safe(root,'content/raw/saffron-gym-presentation/'+row['path']);b=subprocess.check_output(['git','-C',str(reference),'show',meta.COMMIT+':'+row['path']]) if reference else p.read_bytes()
  meta.require((len(b),meta.sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Portrait source differs');raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
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
  rgba=bytes(c for i in ix for c in (*pal[i],255 if i else 0));png=art.png(64,64,rgba);out[identity+'.png']=png;out[identity+'.rgba']=rgba
  portraits.append(dict(id=identity,sourceTrainerPicId=picture,trainerPic='TRAINER_PIC_'+stem.upper(),sourceImage=image,sourcePalette=palette,width=64,height=64,x=144,y=8,sourceFrame=0,indexSha256=meta.sha(ix),texturePath=identity+'.png',rgbaPath=identity+'.rgba',sha256=meta.sha(png),rgbaSha256=meta.sha(rgba)))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=meta.COMMIT,lockSha256=LOCK_SHA,inputs=lock['files'],portraits=portraits,paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',outputs=[dict(path=p,sha256=meta.sha(b),byteLength=len(b)) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();outputs=build(a.project_root,a.reference_root)
 for rel,b in outputs.items():
  target=meta.safe(a.project_root,'content/generated/saffron-gym-trainer-art/'+rel)
  if a.check:meta.require(target.read_bytes()==b,'Portrait output differs: '+rel)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(portraits=2,sha256=meta.sha(outputs['manifest.json']),bytes=len(outputs['manifest.json']))))
