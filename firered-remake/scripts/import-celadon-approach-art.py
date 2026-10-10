#!/usr/bin/env python3
"""Pinned Biker portrait. Never executes upstream code."""
import argparse,hashlib,importlib.util,json,os,pathlib,struct,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-celadon-approach-art-v1'
LOCK_SHA='c77e3512b325d8126e34f2096dfb0a2ae56afff7c73f25f2edc599c1a4233e78'
SOURCES={'graphics/trainers/front_pics/biker_front_pic.png','graphics/trainers/palettes/biker.pal',
 'src/data/graphics/trainers.h','src/data/trainer_graphics/front_pic_tables.h','include/constants/trainers.h','src/data/trainers.h'}
INPUTS={'scripts/import-lab-battle-ui.py','scripts/fetch-lab-battle-ui.py'}
def sha(b):return hashlib.sha256(b).hexdigest()
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(base,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe art path')
 f=base/p
 if any(x.is_symlink() for x in (f,*f.parents)):raise ValueError('Art symlink rejected')
 return f
def build(data):
 s=data['src/data/graphics/trainers.h'].decode();t=data['src/data/trainer_graphics/front_pic_tables.h'].decode()
 for fragment in ['gTrainerFrontPic_Biker','graphics/trainers/front_pics/biker_front_pic.4bpp.lz','gTrainerPalette_Biker','graphics/trainers/palettes/biker.gbapal.lz']:
  if fragment not in s:raise ValueError('Trainer source binding differs')
 if 'TRAINER_SPRITE(BIKER, gTrainerFrontPic_Biker, 0x800)' not in t or 'TRAINER_PAL(BIKER, gTrainerPalette_Biker)' not in t:raise ValueError('Trainer source table differs')
 if 'TRAINER_PIC_BIKER                  91' not in data['include/constants/trainers.h'].decode():raise ValueError('Biker source picture differs')
 for symbol in ['TRAINER_BIKER_RICARDO','TRAINER_BIKER_JAREN']:
  match=__import__('re').search(r'\['+symbol+r'\] = \{(.*?)\n    \}',data['src/data/trainers.h'].decode(),__import__('re').S)
  if not match or '.trainerPic = TRAINER_PIC_BIKER' not in match[1]:raise ValueError('Biker source trainer binding differs')
 spec=importlib.util.spec_from_file_location('_celadon-approach_codec',ROOT/'scripts/import-lab-battle-ui.py');codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 output={};assets=[]
 def add(name,w,h,idx,colors):
  rgba=codec.rgba(idx,colors);png=codec.png(w,h,rgba);output[name+'.rgba']=rgba;output[name+'.png']=png
  assets.append(dict(id=name,width=w,height=h,rgbaSha256=sha(rgba),pngSha256=sha(png)))
 w,h,idx,_=codec.indexed_png(data['graphics/trainers/front_pics/biker_front_pic.png'])
 if (w,h)!=(64,64):raise ValueError('Trainer source dimensions differ')
 add('biker-front',w,h,idx,codec.palette(data['graphics/trainers/palettes/biker.pal']))
 return assets,output

def run(reference=None,check=False):
 raw=safe(ROOT,'content/raw/celadon-approach-art');out=safe(ROOT,'content/generated/celadon-approach-art')
 b=safe(ROOT,'scripts/celadon-approach-art.lock.json').read_bytes()
 if sha(b)!=LOCK_SHA:raise ValueError('Art source lock differs')
 lock=json.loads(b)
 if (lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'])!=(1,'pret/pokefirered',COMMIT,TREE) or {r['path'] for r in lock['files']}!=SOURCES or {r['path'] for r in lock['inputs']}!=INPUTS:raise ValueError('Art lock scope differs')
 if reference and subprocess.check_output(['git','-C',str(reference),'rev-parse',COMMIT+'^{tree}']).decode().strip()!=TREE:raise ValueError('Art source tree differs')
 data={}
 for row in lock['files']:
  p=row['path'];b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+p]) if reference else safe(raw,p).read_bytes()
  if (len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Art original blob differs: '+p)
  data[p]=b
 for row in lock['inputs']:
  b=safe(ROOT,row['path']).read_bytes()
  if (len(b),sha(b))!=(row['byteLength'],row['sha256']):raise ValueError('Art local codec differs')
 assets,output=build(data)
 output['manifest.json']=encode(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,sourceTree=TREE,lockSha256=LOCK_SHA,inputs=lock['files'],assets=assets,transparentIndex=0,trainerPictureId=91,trainerTopLeft=[144,8],upstreamScriptsExecuted=False,outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(output.items())]))
 materials={(out,p):b for p,b in output.items()}
 if reference:materials.update({(raw,p):b for p,b in data.items()})
 for (parent,p),b in materials.items():
  target=safe(parent,p)
  if check:
   if target.read_bytes()!=b:raise ValueError('Art output differs: '+p)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 return dict(passed=True,assets=len(assets),sourceBlobs=len(data),manifestSha256=sha(output['manifest.json']))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.reference_repo,a.check)))
