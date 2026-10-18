#!/usr/bin/env python3
"""Independent direct-PNG/map sampling. Does not import converter or product code."""
import argparse,hashlib,json,struct
from pathlib import Path
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('supplement',type=Path);a=p.parse_args();out=Path(__file__).resolve().parent
inputs={}
pins=json.loads((out.parent/'pinned-inputs.json').read_text())
def read(path):
 candidate=a.reference/path
 if not candidate.exists():candidate=a.supplement/path
 data=candidate.read_bytes();assert hashlib.sha256(data).hexdigest()==pins[path], 'Pinned material mismatch: '+path
 inputs[path]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'url':'https://github.com/pret/pokefirered/blob/037335f4c725d7c9aecdac87066f2002b4bd7e14/'+path}
 return candidate
def rgb555(r,g,b):return (r//8)|((g//8)<<5)|((b//8)<<10)
def image(path):
 im=Image.open(read(path));im.load();assert im.mode=='P';return im

def palette(im,pal=None):
 if pal:
  rows=read(pal).read_text().splitlines();assert rows[:3]==['JASC-PAL','0100','16'];return [rgb555(*map(int,x.split())) for x in rows[3:]]
 colors=im.getpalette()[:48];return [rgb555(*colors[i:i+3]) for i in range(0,48,3)]

def blend(a,b,eva,evb):
 return sum(min(31,((((a>>shift)&31)*min(16,eva))+(((b>>shift)&31)*min(16,evb)))>>4)<<shift for shift in (0,5,10))

materials=[]
for name,impath,mappath,palpath in [
 ('scary-face-player','graphics/battle_anims/backgrounds/scary_face.png','graphics/battle_anims/backgrounds/scary_face_player.bin',None),
 ('scary-face-opponent','graphics/battle_anims/backgrounds/scary_face.png','graphics/battle_anims/backgrounds/scary_face_opponent.bin',None),
 ('fog','graphics/weather/fog_horizontal.png','graphics/battle_anims/backgrounds/fog.bin','graphics/weather/default.pal')]:
 im=image(impath);pal=palette(im,palpath);tilemap=struct.unpack('<1024H',read(mappath).read_bytes());tileswide=im.width//8
 # Banks outside owned bank8 get explicit synthetic colors, never borrow the overlay palette.
 banks=[[((b+i)&31)|(((b*3+i*2)&31)<<5)|(((b*5+i*3)&31)<<10) for i in range(16)] for b in range(16)];banks[8]=pal
 def sample(x,y):
  px=x%256;py=y%256;entry=tilemap[(py//8)*32+px//8];tile=entry&1023;sx=px%8;sy=py%8
  if entry&1024:sx=7-sx
  if entry&2048:sy=7-sy
  tx=(tile%tileswide)*8+sx;ty=(tile//tileswide)*8+sy
  index=im.getpixel((tx,ty)) if ty<im.height else 0
  return [entry,tile,entry>>12,sx,sy,index,banks[entry>>12][index],int(index!=0)]
 points=[(-1,-1),(0,0),(7,7),(8,0),(239,0),(239,159),(0,159),(0,160),(239,160),(240,160),(255,255),(256,256),(511,-257),(110,47),(152,72),(120,80)]
 # Keep a representative for every combination of bank, flip flags, and pixel index.
 seen=set()
 for y in range(256):
  for x in range(256):
   s=sample(x,y);key=(s[2],(s[0]>>10)&3,s[5])
   if key not in seen:seen.add(key);points.append((x,y))
 points=list(dict.fromkeys(points));samples=[dict(point=[x,y],value=sample(x,y)) for x,y in points]
 renders=[]
 for sx,sy in [(0,0),(1,0),(65535,0),(65500,65520),(257,256)]:
  indexbytes=bytearray();rgb=bytearray();target=(31|(5<<5)|(27<<10))
  for y in range(160):
   for x in range(240):
    s=sample(x+sx,y+sy);indexbytes.extend((s[2],s[5]));rgb.extend(struct.pack('<H',s[6] if s[7] else target))
  renders.append(dict(scroll=[sx,sy],size=[240,160],bank_index_sha256=hashlib.sha256(indexbytes).hexdigest(),rgb555_sha256=hashlib.sha256(rgb).hexdigest()))
 blend_rows=[]
 for sample_row in samples:
  s=sample_row['value'];src=s[6]
  for eva,evb in [(0,16),(1,16),(5,12),(9,7),(14,2),(16,16),(31,31)]:
   target=31|(5<<5)|(27<<10);blend_rows.append([*sample_row['point'],eva,evb,src,target,blend(src,target,eva,evb) if s[7] else target])
 materials.append(dict(name=name,source_image=impath,source_map=mappath,palette_rgb555=pal,control_palette_rgb555=banks,samples=samples,renders=renders,blends=blend_rows))
sprites=[]
for name,path,w,h in [('glare-eye-dot','graphics/battle_anims/sprites/small_red_eye.png',8,8),('mist-ball-frame-zero','graphics/battle_anims/sprites/small_bubbles.png',16,16)]:
 im=image(path);pal=palette(im);rows=[]
 for flipx,flipy in [(0,0),(1,0),(0,1),(1,1)]:
  pixels=[im.getpixel((w-1-x if flipx else x,h-1-y if flipy else y)) for y in range(h) for x in range(w)]
  rows.append(dict(flip=[flipx,flipy],indices=pixels,rgb555=[pal[v] if v else None for v in pixels]))
 sprites.append(dict(name=name,source_image=path,size=[w,h],palette_rgb555=pal,frames=rows))
result=dict(schema='firered-bg1-material-source-v1',commit='037335f4c725d7c9aecdac87066f2002b4bd7e14',sources=inputs,sample_fields=['entry_u16','tile','bank','local_x_after_flip','local_y_after_flip','palette_index','rgb555','opaque'],blend_fields=['world_x','world_y','eva','evb','src_rgb555','second_target_rgb555','result_rgb555'],transparent_second_target_rgb555=31|(5<<5)|(27<<10),materials=materials,sprites=sprites)
(out/'vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
print(json.dumps({'materials':len(materials),'samples':sum(len(m['samples']) for m in materials),'blends':sum(len(m['blends']) for m in materials),'sprites':len(sprites),'path':str(out/'vectors.json')}))
