#!/usr/bin/env python3
"""Rebuild private Surf sprites from authenticated pinned blobs; never run upstream code."""
import argparse, hashlib, io, json, pathlib, subprocess, struct
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--reference-root',required=True,type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1];lock=json.loads((root/'scripts/field-surf-art.lock.json').read_text());commit=lock['sourceCommit']
if subprocess.check_output(['git','rev-parse',commit+'^{tree}'],cwd=a.reference_root,text=True).strip()!=lock['sourceTree']:raise SystemExit('Source tree differs')
blobs={}
for row in lock['files']:
 b=subprocess.check_output(['git','show',commit+':'+row['path']],cwd=a.reference_root)
 if len(b)!=row['byteLength'] or hashlib.sha256(b).hexdigest()!=row['sha256'] or hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()!=row['gitBlobSha1']:raise SystemExit('Source differs: '+row['path'])
 retained=root/'content/raw/field-surf'/row['path']
 if a.check:
  if not retained.is_file() or retained.read_bytes()!=b:raise SystemExit('Retained source differs: '+row['path'])
 else:retained.parent.mkdir(parents=True,exist_ok=True);retained.write_bytes(b)
 blobs[row['path']]=b
palette=[tuple(((v>>3)<<3)|((v>>3)>>2) for v in map(int,line.split())) for line in blobs['graphics/object_events/palettes/player.pal'].decode().splitlines()[3:]]
if len(palette)!=16:raise SystemExit('Player palette differs')
for row in lock['outputs']:
 im=Image.open(io.BytesIO(blobs[row['input']]))
 if row['id'] in ('water','pond'):
  base=row['input'][:-3]
  pal=[tuple(((v>>3)<<3)|((v>>3)>>2) for v in map(int,line.split())) for line in blobs[base+'pal'].decode().splitlines()[3:]]
  entries=struct.unpack('<2048H',blobs[base+'bin']);pixels=im.load();rgba=bytearray()
  if len(pal)!=48 or im.mode!='P':raise SystemExit('Terrain format differs')
  for y in range(112):
   for x in range(240):
    e=entries[y//8*32+x//8];tile=e&1023;bank=e>>12;xx=x%8;yy=y%8
    if bank not in (2,3,4):raise SystemExit('Terrain palette bank differs')
    if e&1024:xx=7-xx
    if e&2048:yy=7-yy
    index=pixels[tile%(im.width//8)*8+xx,tile//(im.width//8)*8+yy]%16
    rgba.extend((*pal[(bank-2)*16+index],255))
  rgba=bytes(rgba)
 else:
  if im.mode!='P' or im.size!=((192,32) if row['id']=='blob' else (224,32)):raise SystemExit('Surf sheet differs')
  # SurfRun indices0,1,2 also back the jump frames9,10,11 in the source pic tables.
  im=im.crop((0,0,row['width'],row['height']))
  rgba=bytes(c for i in im.getdata() for c in ((*palette[i],255) if i else (0,0,0,0)))
 if len(rgba)!=row['byteLength'] or hashlib.sha256(rgba).hexdigest()!=row['sha256']:raise SystemExit('Surf pixels differ')
 out=root/row['path']
 if a.check:
  if not out.is_file() or out.read_bytes()!=rgba:raise SystemExit('Stored output differs: '+row['path'])
 else:out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(rgba)
print('Verified',len(blobs),'pinned blobs and',sum(r['byteLength'] for r in lock['outputs']),'original Surf RGBA bytes')
