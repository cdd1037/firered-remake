#!/usr/bin/env python3
"""Independent native-map pixel vectors from pinned PNG indices and palette banks."""
from pathlib import Path
from PIL import Image
import argparse,json,struct
ROOT=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--output',type=Path,default=ROOT/'tests/Game.Presentation.Tests/Fixtures/sliding-background-pixels.json');a=p.parse_args()
stage=a.reference/'graphics/battle_anims/backgrounds'
rows=[]
base_rgb=[int(v) for row in (a.reference/'graphics/battle_interface/textbox1.pal').read_text().splitlines()[3:] for v in row.split()]
base_colors=[sum((base_rgb[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)]
for id,tiles,mapfile,palette in [(7,'drill','drill','drill'),(10,'highspeed','highspeed_player','highspeed'),(9,'highspeed','highspeed_opponent','highspeed'),(11,'thunder','thunder','thunder'),(18,'drill','drill','sky'),(16,'cosmic','cosmic','cosmic')]:
 image=Image.open(stage/(tiles+'.png'));tilemap=(stage/(mapfile+'.bin')).read_bytes()
 if palette=='sky':rgb=[int(v) for row in (stage/'sky.pal').read_text().splitlines()[3:] for v in row.split()]
 else:rgb=Image.open(stage/(palette+'.png')).getpalette()[:48]
 colors=[sum((rgb[i*3+c]//8)<<(c*5)for c in range(3))for i in range(16)]
 samples=[]
 for x,y in [(0,0),(7,7),(8,8),(128,64),(249,243),(255,255),(256,0),(-1,-13)]:
  xx=x&255;yy=y&255;entry=struct.unpack_from('<H',tilemap,((yy//8)*32+xx//8)*2)[0];tile=entry&1023;tx=xx%8;ty=yy%8
  if entry&1024:tx=7-tx
  if entry&2048:ty=7-ty
  index=image.getpixel(((tile%(image.width//8))*8+tx,(tile//(image.width//8))*8+ty));bank=entry>>12
  samples.append([x,y,bank if index else 0,index,(colors if bank==2 else base_colors)[index] if index else -1])
 rows.append(dict(id=id,samples=samples))
a.output.write_text(json.dumps(rows,separators=(',',':'))+'\n')
print('Independent source-map pixel samples:',sum(len(r['samples'])for r in rows))
