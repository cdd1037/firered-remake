#!/usr/bin/env python3
"""Source-authenticated layer atlases and elevator/spin presentation constants."""
import argparse,io,json,pathlib,re,struct
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe presentation path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Presentation symlink rejected')
 return q
def authenticated(root=ROOT):
 b=safe(root,'scripts/rocket-hideout-presentation.lock.json').read_bytes()
 lock=json.loads(b)
 if (lock['schemaVersion'], lock['commit'], len(lock['files']), len(lock['inputs'])) != (1, COMMIT, 35, 3):raise ValueError('Presentation inventory differs')
 source={};inputs={}
 for entries,base,result in [(lock['files'],root/'content/raw/pokefirered/037335f4',source),(lock['inputs'],root,inputs)]:
  for row in entries:
   b=safe(base,row['path']).read_bytes()
   result[row['path']]=b
 return lock,source,inputs
def build(source,inputs):
 text=lambda p:source[p].decode()
 unit=lambda p,f:list(struct.unpack('<'+f*(len(source[p])//struct.calcsize(f)),source[p]))
 labels={k:int(v,16) for k,v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)',text('include/constants/metatile_labels.h'),re.M)}
 contract=json.loads(inputs['content/generated/rocket-hideout-region/rocket-hideout.json']);result={};metadata=[]
 for prefix,secondary in [('GameCorner','game_corner'),('SilphCo','silph_co')]:
  symbols=sorted({c['metatile'] for p in contract['metatilePatches'] for c in p['cells'] if c['metatile'].startswith('METATILE_'+prefix+'_')})
  if secondary=='silph_co':symbols+=['METATILE_SilphCo_ElevatorWindow_'+row+str(n) for row in ['Top','Mid','Bottom'] for n in range(3)]
  bases=['data/tilesets/primary/building','data/tilesets/secondary/'+secondary];graphics=[bases[0],'data/tilesets/secondary/'+('condominiums' if secondary=='silph_co' else secondary)]
  images=[Image.open(io.BytesIO(source[b+'/tiles.png'])) for b in graphics]
  if any(i.mode!='P' or i.width%8 or i.height%8 or i.getextrema()[1]>15 for i in images):raise ValueError('Source tiles are not indexed4bpp')
  emitted=[]
  for base,img in zip(graphics,images):
   rule=re.search(re.escape('$(TILESETGFXDIR)/'+base.removeprefix('data/tilesets/')+'/tiles.4bpp:')+r'.*?\n\t[^\n]*?-num_tiles (\d+)',text('tileset_rules.mk'))
   emitted.append(int(rule[1]) if rule else img.width*img.height//64)
  palettes=[]
  for n in range(13):
   lines=text(graphics[0 if n<7 else 1]+f'/palettes/{n:02d}.pal').splitlines()
   if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19:raise ValueError('Palette format differs')
   palettes.append([tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in row.split()) for row in lines[3:]])
  palettes[0][0]=(0,0,0)
  attrs=[unit(b+'/metatile_attributes.bin','I') for b in bases];metatiles=[unit(b+'/metatiles.bin','H') for b in bases];cache={}
  def tile(value):
   if value not in cache:
    tid,pal=value&1023,value>>12;bi,local=(0,tid) if tid<640 else (1,tid-640);img=images[bi]
    if pal>=13 or local>=emitted[bi]:raise ValueError('Uninitialized source tile/palette')
    pixels=[]
    for y in range(8):
     for x in range(8):
      ix,iy=(7-x if value&0x400 else x),(7-y if value&0x800 else y);color=img.getpixel((local%(img.width//8)*8+ix,local//(img.width//8)*8+iy));pixels.append((*palettes[pal][color],0 if color==0 else 255))
    cache[value]=Image.new('RGBA',(8,8));cache[value].putdata(pixels)
   return cache[value]
  layers=[Image.new('RGBA',(len(symbols)*16,16)) for _ in range(3)]
  for column,symbol in enumerate(symbols):
   mid=labels[symbol];bi,local=(0,mid) if mid<640 else (1,mid-640);attribute=attrs[bi][local];values=metatiles[bi][local*8:local*8+8]
   groups={0:[[0x3014]*4,values[:4],values[4:]],1:[values[:4],values[4:],[0]*4],2:[values[:4],[0]*4,values[4:]]}.get((attribute>>29)&3)
   if groups is None or len(values)!=8:raise ValueError('Source layer type/metatile differs')
   for layer,group in zip(layers,groups):
    for n,v in enumerate(group):layer.paste(tile(v),(column*16+n%2*8,n//2*8))
   metadata.append(dict(tileset=secondary,symbol=symbol,metatileId=mid,rawAttributes=attribute,x=column*16,y=0,width=16,height=16))
  # Independent prepared flattened atlas must equal these three decoded layers.
  composite=Image.new('RGBA',layers[0].size,(0,0,0,255))
  for layer in layers:composite=Image.alpha_composite(composite,layer)
  original=Image.open(io.BytesIO(inputs['content/generated/rocket-hideout-region/patches/'+secondary+'.png'])).convert('RGBA')
  oldrows=[row for row in contract['patchAtlas'] if row['atlas'].endswith('/'+secondary+'.png')]
  for row in oldrows:
   col=symbols.index(row['symbol'])*16
   if composite.crop((col,0,col+16,16)).tobytes()!=original.crop((row['x'],row['y'],row['x']+16,row['y']+16)).tobytes():raise ValueError('Prepared source patch differs: '+row['symbol'])
  for name,layer in zip(['bottom','middle','top'],layers):
   out=io.BytesIO();layer.save(out,format='PNG',compress_level=9);result[secondary+'-'+name+'.png']=out.getvalue()
 special=text('src/field_specials.c')
 def source_array(name):
  match=re.search(r'static const u8 '+name+r'\[\] = \{(.*?)\};',special,re.S)
  if not match:raise ValueError('Source timing array missing')
  return [int(n) for n in re.findall(r'\d+',match[1])]
 shakes=source_array('sElevatorAnimationDuration');windows=source_array('sElevatorWindowAnimDuration')
 if shakes!=[8,16,24,32,38,46,53,56,57] or windows!=[3,6,9,12,15,18,21,24,27]:raise ValueError('Source elevator timing differs')
 if len(metadata)!=22:raise ValueError('Presentation atlas scope differs')
 return result,metadata,dict(shakeEveryTicks=3,shakeCounts=shakes,windowFirstTick=7,windowEveryTicks=6,windowCounts=windows,windowX=1,windowY=0,windowWidth=3,windowHeight=3,currentFloorWindow=dict(x=176,y=8,width=56,height=32))
def run(root=ROOT,check=False):
 lock,source,inputs=authenticated(root);output,tiles,elevator=build(source,inputs)
 manifest=encode(dict(schemaVersion=1,profile='firered-rocket-hideout-presentation-v1',sourceCommit=COMMIT,tiles=tiles,elevator=elevator,outputs=[dict(path=p) for p,b in sorted(output.items())]))
 output['manifest.json']=manifest
 for relative,b in output.items():
  p=safe(root,'content/generated/rocket-hideout-presentation/'+relative)
  if check:
   if p.read_bytes()!=b:raise ValueError('Generated presentation differs: '+relative)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,sourceBlobs=len(source),metatiles=len(tiles),layers=6,manifestBytes=len(manifest))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.check),indent=2))
