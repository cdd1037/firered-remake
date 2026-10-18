#!/usr/bin/env python3
"""Direct indexed-PNG/map/OAM observations; no importer or product code."""
import argparse,hashlib,json,struct,sys
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
from source_extract import COMMIT
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('supplement',type=Path);a=p.parse_args();out=Path(__file__).resolve().parent;sources={};pins=json.loads((out/'pinned-inputs.json').read_text())
def sha(b):return hashlib.sha256(b).hexdigest()
def read(p):
 candidate=a.reference/p
 if not candidate.exists():candidate=a.supplement/p
 b=candidate.read_bytes();assert sha(b)==pins[p],'Pinned source mismatch: '+p;sources[p]={'sha256':sha(b),'bytes':len(b),'url':f'https://github.com/pret/pokefirered/blob/{COMMIT}/{p}'};return candidate
for p in ['src/graphics.c','Makefile','graphics_file_rules.mk']:read(p)
def image(p):
 im=Image.open(read(p));im.load();assert im.mode=='P';assert im.width%8==im.height%8==0;return im

def pal(im):
 p=im.getpalette();return [(p[i]>>3)|((p[i+1]>>3)<<5)|((p[i+2]>>3)<<10) for i in range(0,48,3)]
def tilebytes(im):
 return bytes(im.getpixel((tx+x,ty+y)) for ty in range(0,im.height,8) for tx in range(0,im.width,8) for y in range(8) for x in range(8))
def blend(color,coeff,target):return sum((((color>>shift)&31)+(((((target>>shift)&31)-((color>>shift)&31))*coeff)>>4))<<shift for shift in [0,5,10])
ims={n:image('graphics/battle_anims/sprites/'+n+'.png') for n in ['flying_dirt','sunlight','rain_drops']};bg=image('graphics/battle_anims/backgrounds/sandstorm_brew.png');dirtpal=pal(ims['flying_dirt']);mapbytes=read('graphics/battle_anims/backgrounds/sandstorm_brew.bin').read_bytes();assert len(mapbytes)==2048;tilemap=struct.unpack('<1024H',mapbytes);bgtiles=tilebytes(bg)
banks=[[((b+i)&31)|(((b*3+i*2)&31)<<5)|(((b*5+i*3)&31)<<10) for i in range(16)] for b in range(16)];banks[8]=dirtpal

def bgsample(x,y):
 x%=256;y%=256;e=tilemap[(y//8)*32+x//8];tile=e&1023;px=x%8;py=y%8
 if e&1024:px=7-px
 if e&2048:py=7-py
 assert tile*64+py*8+px<len(bgtiles)
 index=bgtiles[tile*64+py*8+px];return [e,tile,e>>12,px,py,index,banks[e>>12][index],int(index!=0)]
points=[(-1,-1),(0,0),(7,7),(8,0),(239,159),(255,255),(256,256),(511,-257)];seen=set()
for y in range(256):
 for x in range(256):
  s=bgsample(x,y);key=(s[2],(s[0]>>10)&3,s[5])
  if key not in seen:seen.add(key);points.append((x,y))
samples=[dict(point=list(p),value=bgsample(*p)) for p in dict.fromkeys(points)]
bgrenders=[];backdrop=31|(5<<5)|(27<<10)
for scroll in [(0,0),(65530,65535),(6,65535),(65500,65520),(257,256)]:
 for red in [False,True]:
  inds=bytearray();rgb=bytearray()
  for y in range(160):
   for x in range(240):
    s=bgsample(x+scroll[0],y+scroll[1]);inds.extend([s[2],s[5]]);color=(blend(s[6],6,31) if red else s[6]) if s[7] else backdrop;rgb.extend(struct.pack('<H',color))
  bgrenders.append(dict(scroll=list(scroll),heatRedBlend=red,bankIndexSha256=sha(inds),rgb555Sha256=sha(rgb)))
sprites=[]
for name,w,h,offsets in [('flying_dirt',32,16,[0,8]),('sunlight',32,32,[0]),('rain_drops',16,32,[0,8,16,24,32,40,48])]:
 im=ims[name];pixels=tilebytes(im);frames=[]
 for offset in offsets:
  for flip in [0,8,16,24]:
   indices=[]
   for y in range(h):
    for x in range(w):
     px=w-1-x if flip&8 else x;py=h-1-y if flip&16 else y;tile=offset+(py//8)*(w//8)+px//8;indices.append(pixels[tile*64+(py%8)*8+px%8])
   frames.append(dict(tileOffset=offset,flip=flip,indices=indices,indexSha256=sha(bytes(indices))))
 sprites.append(dict(name=name,pngSize=list(im.size),frameSize=[w,h],paletteRgb555=pal(im),tileIndicesSha256=sha(pixels),frames=frames))
abi=json.loads((out/'motion-vectors.json').read_text())['abi'];fragments=[];dirt=tilebytes(ims['flying_dirt']);dims=[[(8,8),(16,16),(32,32),(64,64)],[(16,8),(32,8),(32,16),(64,32)],[(8,16),(8,32),(16,32),(32,64)]]
for case in abi:
 if case['nullTable']:continue
 indices=bytearray();owners=bytearray();colors=bytearray();opaque=0
 for y in range(160):
  for x in range(240):
   hits=[]
   for i,piece in enumerate(case['row']['pieces']):
    ox,oy,shape,size,tile,priority,palette,flip,mode,mosaic=piece;w,h=dims[shape][size];px=(x-ox)&511;py=(y-oy)&255
    if px>=w or py>=h:continue
    if flip&8:px=w-1-px
    if flip&16:py=h-1-py
    localTile=((tile+(py//8)*(w//8)+px//8)-case['tile'])&1023;addr=localTile*64+(py%8)*8+px%8;index=dirt[addr] if addr<len(dirt) else 0
    if index:hits.append((priority,i,index))
   if hits:
    priority,owner,index=min(hits);opaque+=1;color=dirtpal[index]
   else:owner=255;index=0;color=backdrop
   indices.append(index);owners.append(owner);colors.extend(struct.pack('<H',color))
 fragments.append(dict(abiCaseIndex=abi.index(case),pixelIndexSha256=sha(indices),ownerIndexSha256=sha(owners),rgb555Sha256=sha(colors),rgb555FieldSha256=sha(colors[:240*112*2]),opaquePixels=opaque))
result=dict(schema='firered-weather-material-source-v1',commit=COMMIT,sources=sources,paletteAuthority='PNG indexed palettes by Makefile %.gbapal: %.png; no specific override in graphics_file_rules.mk. Sandstorm BG intentionally loads flying_dirt palette, not sandstorm_brew PNG palette.',background=dict(size=[256,256],tilemapBanks=sorted({e>>12 for e in tilemap}),tilemapFlipCombinations=sorted({(e>>10)&3 for e in tilemap}),tileIndicesSha256=sha(bgtiles),paletteRgb555=dirtpal,unusedPngPaletteRgb555=pal(bg),heatPaletteRgb555=[blend(c,6,31) for c in dirtpal],controlPaletteRgb555=banks,sampleFields=['entry','tile','bank','localXAfterFlip','localYAfterFlip','index','rgb555','opaque'],samples=samples,renders=bgrenders),sprites=sprites,fragmentRenders=fragments,fragmentRenderSemantics='Native source ABI pieces sampled with GBA 1D tile addressing, 9-bit X and8-bit Y wrap, object flip bits; lower priority value then earlier OAM piece wins. Paletteindex0 is transparent. Mosaic flag is metadata only; probes use unit mosaic cells. ObjBlend mode is metadata only here; hashes are source layer pixels before composition.',backdropRgb555=backdrop)
(out/'material-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('material',len(samples),len(fragments),sum(len(s['frames']) for s in sprites))
