#!/usr/bin/env python3
"""Independent source indexed pixels and palette/map identity; imports no producer."""
import argparse,hashlib,json,struct,sys
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
from source_extract import COMMIT
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();out=Path(__file__).resolve().parent;sources={};pins=json.loads((out/'pinned-inputs.json').read_text())
def sha(b):return hashlib.sha256(b).hexdigest()
def read(path):
 b=(a.reference/path).read_bytes();assert sha(b)==pins[path],path;sources[path]={'sha256':sha(b),'bytes':len(b),'url':f'https://github.com/pret/pokefirered/blob/{COMMIT}/{path}'};return b
def img(path):
 import io
 im=Image.open(io.BytesIO(read(path)));im.load();assert im.mode=='P';return im
def pal(im):
 p=im.getpalette();return [(p[i]>>3)|((p[i+1]>>3)<<5)|((p[i+2]>>3)<<10) for i in range(0,48,3)]
def tiles(im):return bytes(im.getpixel((tx+x,ty+y)) for ty in range(0,im.height,8) for tx in range(0,im.width,8) for y in range(8) for x in range(8))
for path in ['src/graphics.c','src/data/battle_anim.h','Makefile','graphics_file_rules.mk']:read(path)
sprites=[]
for name,w,h,offsets in [('letter_z',32,32,[0]),('green_star',16,16,[0,4,8,12]),('moon',64,64,[0]),('green_sparkle',16,16,[0,4,8,12]),('rainbow_rings',8,16,[0,4])]:
 im=img('graphics/battle_anims/sprites/'+name+'.png');data=tiles(im);frames=[]
 for offset in offsets:
  for flip in range(4):
   inds=[]
   for y in range(h):
    for x in range(w):
     px=w-1-x if flip&1 else x;py=h-1-y if flip&2 else y;tile=offset+(py//8)*(w//8)+px//8;inds.append(data[tile*64+py%8*8+px%8])
   frames.append(dict(tile=offset,hFlip=bool(flip&1),vFlip=bool(flip&2),indices=inds))
 packed=bytes(data[i]|data[i+1]<<4 for i in range(0,len(data),2));sprites.append(dict(id=name.replace('_','-'),size=list(im.size),frameSize=[w,h],palette=pal(im),packedSha256=sha(packed),tileIndicesSha256=sha(data),frames=frames))
backgrounds=[]
for name,stem,bank,padded in [('morning-sun','masks/light_beam',8,8192),('aurora','backgrounds/aurora',2,0)]:
 im=img('graphics/battle_anims/'+stem+'.png');data=tiles(im);packed=bytes(data[i]|data[i+1]<<4 for i in range(0,len(data),2));packed+=bytes(max(0,padded-len(packed)));data=bytes(v for b in packed for v in [b&15,b>>4]);rawmap=read('graphics/battle_anims/'+stem+'.bin');tilemap=struct.unpack('<'+str(len(rawmap)//2)+'H',rawmap);palette=pal(im);samples=[];seen=set()
 for y in range(256):
  for x in range(256):
   e=tilemap[y//8*32+x//8];px=x%8;py=y%8
   if e&1024:px=7-px
   if e&2048:py=7-py
   i=data[(e&1023)*64+py*8+px];key=(e>>12,(e>>10)&3,i)
   if key not in seen:seen.add(key);samples.append(dict(point=[x,y],tile=e&1023,bank=e>>12,index=i))
 backgrounds.append(dict(id=name,pngSize=list(im.size),packedSha256=sha(packed),tilemapSha256=sha(rawmap),palette=palette,tilemapBanks=sorted({e>>12 for e in tilemap}),samples=samples,paletteBank=bank))
result=dict(schema='firered-recovery-material-source-v1',commit=COMMIT,sources=sources,paletteAuthority='PNG indexed colors under the pinned Makefile gbapal rule; source literal registration/load identities asserted independently by the focused producer check.',sprites=sprites,backgrounds=backgrounds)
(out/'material-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('material',len(sprites),sum(len(s['frames']) for s in sprites),[len(b['samples']) for b in backgrounds])
