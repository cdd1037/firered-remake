#!/usr/bin/env python3
"""Pinned static Bug Catcher/Red/Leaf trainer frames. Never executes upstream code."""
import argparse,  importlib.util, io, json, pathlib, re, sys, subprocess
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def canonical(d):return (json.dumps(d,sort_keys=True,indent=2)+'\n').encode()
def safe(root,p):
 q=root/p
 if not q.resolve().is_relative_to(root.resolve()):raise ValueError('Escaping path: '+p)
 return q

def run(repository, raw, output):
 lock_bytes=(ROOT/'scripts/pewter-trainer-art.lock.json').read_bytes();lock=json.loads(lock_bytes)
 project=json.loads((ROOT/'content/source-lock.json').read_bytes())
 if (project['source']['repository'],project['source']['sourceCommit'],project['referenceGame']['gameEdition'],project['referenceGame']['romRevision'])!=('pret/pokefirered',COMMIT,'FIRERED',0):raise ValueError('Project source profile differs')
 if (lock['repository'],lock['commit'])!=('pret/pokefirered',COMMIT) or len(lock['files'])!=9:raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  p=e['path'];b=subprocess.check_output(['git','-C',str(repository),'show',COMMIT+':'+p]) if repository else safe(raw,p).read_bytes()
  sources[p]=b
  if repository:
   target=safe(raw,p);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 def require(p,needle):
  if needle not in sources[p].decode():raise ValueError('Source contract missing: '+needle)
 require('src/data/graphics/trainers.h','gTrainerFrontPic_LeaderBrock[] = INCBIN_U32("graphics/trainers/front_pics/leader_brock_front_pic.4bpp.lz")')
 require('src/data/graphics/trainers.h','gTrainerPalette_LeaderBrock[] = INCBIN_U32("graphics/trainers/palettes/leader_brock.gbapal.lz")')
 require('src/data/graphics/trainers.h','gTrainerFrontPic_Camper[] = INCBIN_U32("graphics/trainers/front_pics/camper_front_pic.4bpp.lz")')
 require('src/battle_controller_opponent.c','(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
 require('tools/gbagfx/gfx.c','#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
 output.mkdir(parents=True,exist_ok=True);assets=[]
 for id,stem,side,palette,x,y in [('camper-front','camper_front_pic','front','camper',144,8),('brock-front','leader_brock_front_pic','front','leader_brock',144,8)]:
  path='graphics/trainers/'+side+'_pics/'+stem+'.png';pal='graphics/trainers/palettes/'+palette+'.pal'
  with Image.open(io.BytesIO(sources[path])) as im:
   if im.mode!='P' or im.width!=64 or im.height not in (64,320):raise ValueError('Trainer source geometry differs')
   indices=im.crop((0,0,64,64)).tobytes()
  lines=sources[pal].decode().splitlines()
  if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19:raise ValueError('Palette shape differs')
  colors=[tuple((v//8<<3)|(v//8>>2) for v in map(int,line.split())) for line in lines[3:]]
  if max(indices)>15:raise ValueError('Trainer palette index differs')
  rgba=bytes(v for i in indices for v in (*colors[i],255 if i else 0))
  Image.frombytes('RGBA',(64,64),rgba).save(output/(id+'.png'))
  assets.append(dict(id=id, texturePath=id + '.png', width=64, height=64, x=x, y=y, sourceImage=path, sourcePalette=pal, sourceFrame=0))
 document=dict(profile='firered-pewter-trainer-art-v1',sourceCommit=COMMIT,inputs=lock['files'],paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',assets=assets,limitations=['Static settled trainer frames only; original slide, throw, attack timing and audio remain unverified.'])
 data=canonical(document);(output/'pewter-trainer-art.json').write_bytes(data)
 print(json.dumps(dict(assets=assets),indent=2))
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--source-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/pewter-trainer-art');a=p.parse_args();run(a.source_repo,a.raw,a.output)
