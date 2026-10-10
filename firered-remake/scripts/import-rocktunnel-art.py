#!/usr/bin/env python3
"""Restore fixed original PokeManiac and indoor Flash pixels; never run upstream code."""
import argparse,hashlib,importlib.util,json,pathlib,struct,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA='f3d680414ab57473344167d8ac168b9ec7a22af5b6d991a8a42df86027ec8c9a';PROFILE='firered-rocktunnel-art-v1'
SOURCES={'graphics/trainers/front_pics/pokemaniac_front_pic.png','graphics/trainers/palettes/pokemaniac.pal','src/data/graphics/trainers.h','src/data/trainer_graphics/front_pic_tables.h','include/constants/trainers.h','graphics/field_effects/pics/field_move_streaks_indoors.png','graphics/field_effects/pics/field_move_streaks_indoors.bin','src/field_effect.c'}
INPUTS={'scripts/import-lab-battle-ui.py','scripts/fetch-lab-battle-ui.py'}
def sha(b):return hashlib.sha256(b).hexdigest()
def encode(d):return(json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe art path')
 f=root/p
 if any(x.is_symlink() for x in (f,*f.parents)):raise ValueError('Art symlink rejected')
 return f
def build(data):
 text=data['src/data/graphics/trainers.h'].decode();table=data['src/data/trainer_graphics/front_pic_tables.h'].decode();field=data['src/field_effect.c'].decode()
 for token in ['gTrainerFrontPic_PokeManiac','graphics/trainers/front_pics/pokemaniac_front_pic.4bpp.lz','gTrainerPalette_PokeManiac','graphics/trainers/palettes/pokemaniac.gbapal.lz']:
  if token not in text:raise ValueError('PokeManiac binding differs')
 if 'TRAINER_SPRITE(POKEMANIAC, gTrainerFrontPic_PokeManiac, 0x800)' not in table or 'TRAINER_PAL(POKEMANIAC, gTrainerPalette_PokeManiac)' not in table:raise ValueError('PokeManiac source table differs')
 for token in ['CpuCopy16(sFieldMoveStreaksIndoors_Gfx, (void *)(VRAM + charbase), 0x80);','task->data[1] -= 16;','task->data[4] += 2;','if (task->data[4] >= 32)','WIN_RANGE(0x28, 0x78)']:
  if token not in field:raise ValueError('Indoor source contract differs')
 spec=importlib.util.spec_from_file_location('_rock_art_codec',ROOT/'scripts/import-lab-battle-ui.py');codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 output={};assets=[]
 def add(name,w,h,idx,colors,expected):
  rgba=codec.rgba(idx,colors);png=codec.png(w,h,rgba)
  if sha(rgba)!=expected:raise ValueError('Historical source pixels differ: '+name)
  output[name+'.rgba']=rgba;output[name+'.png']=png;assets.append(dict(id=name,width=w,height=h,rgbaSha256=sha(rgba),pngSha256=sha(png)))
 w,h,idx,_=codec.indexed_png(data['graphics/trainers/front_pics/pokemaniac_front_pic.png'])
 if(w,h)!=(64,64):raise ValueError('Portrait dimensions differ')
 add('pokemaniac-front',w,h,idx,codec.palette(data['graphics/trainers/palettes/pokemaniac.pal']),'983561ad992cadb598bbe1abaab59e80cdd8f7f93d3e5524692eb996a68e92b8')
 w,h,idx,pal=codec.indexed_png(data['graphics/field_effects/pics/field_move_streaks_indoors.png']);tiles=data['graphics/field_effects/pics/field_move_streaks_indoors.bin']
 if(w,h,len(tiles))!=(8,32,640):raise ValueError('Indoor source shape differs')
 pixels=bytearray(256*80)
 for n,entry in enumerate(struct.unpack('<320H',tiles)):
  tile=entry&1023
  if entry>>12 or tile>=4:raise ValueError('Indoor source tile differs')
  for y in range(8):
   for x in range(8):
    sx=7-x if entry&1024 else x;sy=7-y if entry&2048 else y;pixels[(n//32*8+y)*256+n%32*8+x]=idx[(tile*8+sy)*8+sx]
 add('indoor-streaks',256,80,pixels,codec.colors(pal[:16]),'7488609f54fff3199eec7055d243a59d7b238b395be026e23c1f0ac6847bcbb5');return assets,output

def run(reference=None,check=False):
 raw=safe(ROOT,'content/raw/rocktunnel-art');out=safe(ROOT,'content/generated/rocktunnel-art');locked=safe(ROOT,'scripts/rocktunnel-art.lock.json').read_bytes()
 if sha(locked)!=LOCK_SHA:raise ValueError('Art source lock differs')
 lock=json.loads(locked)
 if(lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'])!=(1,'pret/pokefirered',COMMIT,TREE) or {r['path'] for r in lock['files']}!=SOURCES or {r['path'] for r in lock['inputs']}!=INPUTS:raise ValueError('Art lock inventory differs')
 if reference and subprocess.check_output(['git','-C',str(reference),'rev-parse',COMMIT+'^{tree}']).decode().strip()!=TREE:raise ValueError('Pinned source tree differs')
 data={}
 for row in lock['files']:
  p=row['path'];b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+p]) if reference else safe(raw,p).read_bytes()
  if(len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Original blob differs: '+p)
  data[p]=b
 for row in lock['inputs']:
  b=safe(ROOT,row['path']).read_bytes()
  if(len(b),sha(b))!=(row['byteLength'],row['sha256']):raise ValueError('Local codec differs')
 assets,output=build(data);output['manifest.json']=encode(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,sourceTree=TREE,lockSha256=LOCK_SHA,inputs=lock['files'],assets=assets,transparentIndex=0,trainerPictureId=88,trainerTopLeft=[144,8],bannerTopLeft=[0,40],upstreamScriptsExecuted=False,outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(output.items())]))
 material={(out,p):b for p,b in output.items()}
 if reference:material.update({(raw,p):b for p,b in data.items()})
 for(parent,p),b in material.items():
  target=safe(parent,p)
  if check:
   if target.read_bytes()!=b:raise ValueError('Generated output differs: '+p)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 return dict(passed=True,assets=2,sourceBlobs=8,manifestSha256=sha(output['manifest.json']))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.reference_repo,a.check)))
