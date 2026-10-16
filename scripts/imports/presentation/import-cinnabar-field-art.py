#!/usr/bin/env python3
"""Decode exact Mansion statue/barrier and Cinnabar Gym metatiles in three source layers."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,importlib.util,io,json,pathlib,re,struct,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('presentation_metadata',ROOT/'scripts/imports/presentation/import-cinnabar-presentation-source.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
from image_io import png as encode_rgba_png

def build(root):
 _,source=meta.authenticated(root);text=lambda p:source[p].decode();unit=lambda p,f:list(struct.unpack('<'+f*(len(source[p])//struct.calcsize(f)),source[p]));labels={k:int(v,16) for k,v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)',text('include/constants/metatile_labels.h'),re.M)}
 out={};tiles=[];assets=[]
 for family,secondary,maps in [('mansion','pokemon_mansion',['PokemonMansion_1F','PokemonMansion_2F','PokemonMansion_3F','PokemonMansion_B1F']),('gym','cinnabar_gym',['CinnabarIsland_Gym'])]:
  scripts=text('data/scripts/pokemon_mansion.inc') if family=='mansion' else text('data/maps/CinnabarIsland_Gym/scripts.inc')
  symbols=sorted(set(re.findall(r'^\s*setmetatile [^,\n]+, [^,\n]+, (METATILE_\w+), [01]',scripts,re.M)));meta.require(len(symbols)==(29 if family=='mansion' else 8),'Cinnabar metatile scope differs')
  bases=['data/tilesets/primary/building','data/tilesets/secondary/'+secondary];images=[Image.open(io.BytesIO(source[b+'/tiles.png'])) for b in bases]
  meta.require(all(i.mode=='P' and i.width%8==0 and i.height%8==0 and i.getextrema()[1]<16 for i in images),'Source tile format differs');emitted=[]
  for base,img in zip(bases,images):
   rule=re.search(re.escape('$(TILESETGFXDIR)/'+base.removeprefix('data/tilesets/')+'/tiles.4bpp:')+r'.*?\n\t[^\n]*?-num_tiles (\d+)',text('tileset_rules.mk'));emitted.append(int(rule[1]) if rule else img.width*img.height//64)
  palettes=[]
  for n in range(13):
   lines=text(bases[0 if n<7 else 1]+f'/palettes/{n:02d}.pal').splitlines();meta.require(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Map palette differs');palettes.append([tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in row.split()) for row in lines[3:]])
  palettes[0][0]=(0,0,0);attrs=[unit(b+'/metatile_attributes.bin','I') for b in bases];metatiles=[unit(b+'/metatiles.bin','H') for b in bases];cache={}
  def tile(value):
   if value not in cache:
    tid,pal=value&1023,value>>12;bi,local=(0,tid) if tid<640 else (1,tid-640);img=images[bi];meta.require(pal<13 and local<emitted[bi],'Uninitialized tile/palette');pixels=[]
    for y in range(8):
     for x in range(8):
      ix,iy=(7-x if value&0x400 else x),(7-y if value&0x800 else y);color=img.getpixel((local%(img.width//8)*8+ix,local//(img.width//8)*8+iy));pixels.append((*palettes[pal][color],0 if color==0 else 255))
    cache[value]=Image.new('RGBA',(8,8));cache[value].putdata(pixels)
   return cache[value]
  layers=[Image.new('RGBA',(len(symbols)*16,16)) for _ in range(3)]
  for column,symbol in enumerate(symbols):
   mid=labels[symbol];bi,local=(0,mid) if mid<640 else (1,mid-640);attribute=attrs[bi][local];values=metatiles[bi][local*8:local*8+8];groups={0:[[0x3014]*4,values[:4],values[4:]],1:[values[:4],values[4:],[0]*4],2:[values[:4],[0]*4,values[4:]]}.get((attribute>>29)&3);meta.require(groups is not None and len(values)==8,'Metatile layers differ')
   for layer,group in zip(layers,groups):
    for n,v in enumerate(group):layer.paste(tile(v),(column*16+n%2*8,n//2*8))
   tiles.append(dict(family=family,symbol=symbol,metatileId=mid,rawAttributes=attribute,x=column*16,y=0,width=16,height=16))
  for name,layer in zip(('bottom','middle','top'),layers):
   rgba=layer.tobytes();stem=family+'-'+name;out[stem+'.png']=encode_rgba_png(layer.width,layer.height,rgba);assets.append(dict(family=family, layer=name, texturePath=stem + '.png', width=layer.width, height=layer.height))
 out['manifest.json']=meta.canonical(dict(schemaVersion=1,tiles=tiles,layers=assets,limits=['Source statue/barrier and quiz doors replace exact metatiles after the source script commands.','Three source BG layers retain the map-family identity; gameplay owns collision and flags.'],outputs=[dict(path=p) for p,b in sorted(out.items())]));return out
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();out=build(a.project_root)
 for relative,b in out.items():
  target=meta.safe(a.project_root,'content/generated/maps/objects/cinnabar-field-art/'+relative)
  if a.check:meta.require(target.read_bytes()==b,'Cinnabar field art differs: '+relative)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(passed=True,metatiles=37,layers=6,manifestBytes=len(out['manifest.json']))))
