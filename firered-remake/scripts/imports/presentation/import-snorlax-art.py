#!/usr/bin/env python3
"""Rebuild authenticated Snorlax normal/shiny battle art, party icon and footprint."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, importlib.util, io, json, pathlib, re, sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('snorlax_metadata',ROOT/'scripts/imports/pokemon/import-snorlax-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
COMMIT=meta.COMMIT
from image_io import png as encode_rgba_png

png = encode_rgba_png
def build(root):
 lock,s=meta.authenticated(root);inventory={r['path']:r for r in lock['files']};out={};sprites=[];icons=[];footprints=[]
 def need(v,m):
  if not v:raise ValueError(m)
 def palette(path):
  lines=s[path].decode().splitlines();need(lines[:2]==['JASC-PAL','0100'] and len(lines)==int(lines[2])+3,'Palette framing differs');rgb=[tuple(map(int,x.split())) for x in lines[3:]];need(all(len(x)==3 and all(0<=c<=255 for c in x) for x in rgb),'Palette values differ');return [tuple((c>>3)*255//31 for c in x) for x in rgb],[(r>>3)|((g>>3)<<5)|((b>>3)<<10) for r,g,b in rgb]
 def image(path,shape):
  im=Image.open(io.BytesIO(s[path]));need(im.mode=='P' and im.size==shape,'Indexed art shape differs: '+path);return bytes(x%16 for x in im.get_flattened_data())
 def rgba(indices,pal):return bytes(c for i in indices for c in (*pal[i],0 if i==0 else 255))
 def source(path,symbol,match=None,kind='pret'):
  r=dict(kind=kind,path=path,symbol=symbol,repository='pret/pokefirered',commit=COMMIT,url=f'https://github.com/pret/pokefirered/blob/{COMMIT}/{path}')
  if match:
   text=s[path].decode();r.update(startLine=text.count('\n',0,match.start())+1,endLine=text.count('\n',0,match.end())+1);r['url']+=f"#L{r['startLine']}"
  return r
 def small(path,symbol,match=None):return {k:v for k,v in source(path,symbol,match).items() if k in ('path','symbol','startLine','endLine')}
 def add(stem,w,h,pixels):
  out[stem+'.png']=png(w,h,pixels);return dict(texturePath=stem + '.png', width=w, height=h)
 elevations=s['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode();em=re.search(r'\[SPECIES_SNORLAX\]\s*=\s*(\d+)',elevations);elevation=int(em[1]) if em else 0;need(elevation==0,'Snorlax ground elevation differs')
 graphics=s['src/data/graphics/pokemon.h'].decode()
 for side in ('front','back'):
  path=f'src/data/pokemon_graphics/{side}_pic_coordinates.h';cm=re.search(r'\[SPECIES_SNORLAX\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',s[path].decode());need(cm is not None,'Sprite coordinates missing');w,h,yoffset=map(int,cm.groups());need((w,h,yoffset)==((64,56,5) if side=='front' else (64,48,11)),'Sprite coordinates differ')
  image_path=f'graphics/pokemon/snorlax/{side}.png';indices=image(image_path,(64,64));need(f'graphics/pokemon/snorlax/{side}.4bpp.lz' in graphics,'Sprite binding differs')
  alpha=[(i%64,i//64) for i,v in enumerate(indices) if v];xs,ys=zip(*alpha);bounds=dict(x=min(xs),y=min(ys),width=max(xs)-min(xs)+1,height=max(ys)-min(ys)+1)
  x,y=(144,8+yoffset-elevation) if side=='front' else (40,48+yoffset)
  for variant in ('normal','shiny'):
   palpath=f'graphics/pokemon/snorlax/{variant}.pal';pal,pal555=palette(palpath);asset=add(f'snorlax-{side}-{variant}',64,64,rgba(indices,pal))
   sprites.append(dict(speciesId='pret:SPECIES_SNORLAX',sourceSpeciesId=143,side=side,paletteVariant=variant,sourceImagePath=image_path,sourcePalettePath=palpath,frameCount=1,sourceFrameIndex=0,frameWidth=64,frameHeight=64,sourceWidth=w,sourceHeight=h,sourceYOffset=yoffset,sourceEnemyElevation=elevation,singleBattleBaseCenter=dict(x=176 if side=='front' else 72,y=40 if side=='front' else 80),singleBattleCenter=dict(x=x+32,y=y+32),singleBattleTopLeft=dict(x=x,y=y),alphaBounds=bounds,transparentIndex=0,paletteRgb555=pal555,paletteExpansion='floor(rgb5 * 255 / 31)',flipH=False,**asset))
 icontext=s['src/pokemon_icon.c'].decode();pm=re.search(r'\[SPECIES_SNORLAX\]\s*=\s*(\d+),',icontext);need(pm and pm[1]=='1','Icon palette differs');im=re.search(r'\[SPECIES_SNORLAX\]\s*=\s*gMonIcon_Snorlax,',icontext);need(im is not None,'Icon table differs')
 path='graphics/pokemon/snorlax/icon.png';indices=image(path,(32,64))[:1024];palpath='graphics/pokemon/icon_palettes/icon_palette_1.pal';pal,pal555=palette(palpath);asset=add('icons/snorlax',32,32,rgba(indices,pal));icons.append(dict(speciesId='pret:SPECIES_SNORLAX',sourceSpeciesId=143,sourceFrameIndex=0,sourceFrameCount=2,paletteIndex=1,paletteVariant='shared-normal-shiny',transparentIndex=0,paletteRgb555=pal555,**asset))
 path='graphics/pokemon/snorlax/footprint.png';need('[SPECIES_SNORLAX] = gMonFootprint_Snorlax,' in s['src/data/pokemon_graphics/footprint_table.h'].decode(),'Footprint binding differs');im=Image.open(io.BytesIO(s[path]));need(im.mode=='1' and im.size==(16,16),'Footprint shape differs');indices=bytes(1 if x==0 else 0 for x in im.get_flattened_data());pal,_=palette('graphics/pokedex/kanto_dex_bgpals.pal');asset=add('snorlax-footprint',16,16,rgba(indices,pal[:16]));footprints.append(dict(sourceSpeciesId=143, **asset))
 path='src/data/pokemon/pokedex_entries.h';dm=re.search(r'\[NATIONAL_DEX_SNORLAX\]\s*=\s*\{(.*?)\n    \}',s[path].decode(),re.S);need(dm is not None,'Dex entry missing');f=dict(re.findall(r'\.(\w+) = (.*?),\n',dm[1]));need(f['categoryName']=='_("SLEEPING")' and f['description']=='gSnorlaxPokedexText','Dex identities differ')
 dp='src/data/pokemon/pokedex_text_fr.h';dt=re.search(r'const u8 gSnorlaxPokedexText\[\] = _\(\n(.*?)\);',s[dp].decode(),re.S);description=''.join(json.loads(x) for x in re.findall(r'"(?:[^"\\]|\\.)*"',dt[1]));dex=dict(sourceSpeciesId=143,name='SNORLAX',category='SLEEPING',heightDecimeters=int(f['height']),weightHectograms=int(f['weight']),description=description)
 manifest=dict(schemaVersion=1, sprites=sprites, icons=icons, footprints=footprints, dex=dex, outputs=[dict(path=p) for p, b in sorted(out.items())]);out['manifest.json']=meta.canonical(manifest);return out

def run(root=ROOT,check=False):
 out=build(root)
 for rel,b in out.items():
  path=root/'content/generated/sprites/pokemon/snorlax'/rel
  if check:
   if meta.read(root,'content/generated/sprites/pokemon/snorlax/'+rel)!=b:raise ValueError('Snorlax art bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print(json.dumps(dict(passed=True,sourceBlobs=31,battleSprites=4,icons=1,footprints=1,outputCount=len(out),manifestBytes=len(out['manifest.json'])),indent=2))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();run(a.project_root,a.check)
