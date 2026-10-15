#!/usr/bin/env python3
"""Deterministic source-pixel Surf sprites/icons/footprints, without source execution."""
import argparse, importlib.util, io, json, pathlib, re, sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('surf_meta',ROOT/'scripts/import-surf-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
from image_io import png as encode_rgba_png

png = encode_rgba_png
def build(root):
 lock,s=meta.authenticated(root);inv={r['path']:r for r in lock['files']};out={};sprites=[];icons=[];footprints=[];dex=[]
 def need(v,m):
  if not v:raise ValueError(m)
 def palette(path):
  lines=s[path].decode().splitlines();need(lines[:2]==['JASC-PAL','0100'] and len(lines)==int(lines[2])+3,'Palette framing differs');rgb=[tuple(map(int,x.split())) for x in lines[3:]];return [tuple((c>>3)*255//31 for c in x) for x in rgb],[(r>>3)|((g>>3)<<5)|((b>>3)<<10) for r,g,b in rgb]
 def indices(path,shape):
  im=Image.open(io.BytesIO(s[path]));need(im.mode=='P' and im.size==shape,'Source indexed pixels differ:'+path);pixels=bytes(im.get_flattened_data());need(all(i<16 for i in pixels),'4bpp palette overflow');return pixels
 def rgba(ix,pal):return bytes(c for i in ix for c in (*pal[i],0 if i==0 else 255))
 def source(path,symbol,m=None,kind='pret'):
  r=dict(kind=kind,path=path,symbol=symbol,repository='pret/pokefirered',commit=meta.COMMIT,url=f'https://github.com/pret/pokefirered/blob/{meta.COMMIT}/{path}')
  if m:r.update(startLine=s[path].decode().count('\n',0,m.start())+1,endLine=s[path].decode().count('\n',0,m.end())+1);r['url']+=f"#L{r['startLine']}"
  return r
 def small(path,symbol,m=None):return {k:v for k,v in source(path,symbol,m).items() if k in ('path','symbol','startLine','endLine')}
 def add(stem,w,h,px):
  out[stem+'.png']=png(w,h,px);return dict(texturePath=stem + '.png', width=w, height=h)
 for name,id in [('Psyduck',54),('Golduck',55)]:
  symbol='SPECIES_'+name.upper();stem=name.lower();elevationText=s['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode();em=re.search(r'\['+symbol+r'\]\s*=\s*(\d+)',elevationText);elevation=int(em[1]) if em else 0
  for side in ('front','back'):
   path=f'src/data/pokemon_graphics/{side}_pic_coordinates.h';cm=re.search(r'\['+symbol+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',s[path].decode());need(cm,'Source coordinates missing');w,h,yoff=map(int,cm.groups());imagepath=f'graphics/pokemon/{stem}/{side}.png';ix=indices(imagepath,(64,64));need(f'graphics/pokemon/{stem}/{side}.4bpp.lz' in s['src/data/graphics/pokemon.h'].decode(),'Sprite binding missing');xs,ys=zip(*[(i%64,i//64) for i,v in enumerate(ix) if v]);x,y=(144,8+yoff-elevation) if side=='front' else (40,48+yoff)
   for variant in ('normal','shiny'):
    pp=f'graphics/pokemon/{stem}/{variant}.pal';pal,pal555=palette(pp);asset=add(f'{stem}-{side}-{variant}',64,64,rgba(ix,pal));sprites.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=id,side=side,paletteVariant=variant,sourceImagePath=imagepath,sourcePalettePath=pp,frameCount=1,sourceFrameIndex=0,frameWidth=64,frameHeight=64,sourceWidth=w,sourceHeight=h,sourceYOffset=yoff,sourceEnemyElevation=elevation,singleBattleBaseCenter=dict(x=176 if side=='front' else 72,y=40 if side=='front' else 80),singleBattleCenter=dict(x=x+32,y=y+32),singleBattleTopLeft=dict(x=x,y=y),alphaBounds=dict(x=min(xs),y=min(ys),width=max(xs)-min(xs)+1,height=max(ys)-min(ys)+1),transparentIndex=0,paletteRgb555=pal555,paletteExpansion='floor(rgb5 * 255 / 31)',flipH=False,**asset))
  ip='src/pokemon_icon.c';it=s[ip].decode();pm=re.search(r'\['+symbol+r'\]\s*=\s*(\d+),',it);im=re.search(r'\['+symbol+r'\]\s*=\s*gMonIcon_'+name+r',',it);need(pm and im,'Source icon binding missing');pi=int(pm[1]);imagepath=f'graphics/pokemon/{stem}/icon.png';ix=indices(imagepath,(32,64))[:1024];pp=f'graphics/pokemon/icon_palettes/icon_palette_{pi}.pal';pal,pal555=palette(pp);asset=add('icons/'+stem,32,32,rgba(ix,pal));icons.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=id,sourceFrameIndex=0,sourceFrameCount=2,paletteIndex=pi,paletteVariant='shared-normal-shiny',transparentIndex=0,paletteRgb555=pal555,**asset))
  imagepath=f'graphics/pokemon/{stem}/footprint.png';need('['+symbol+'] = gMonFootprint_'+name+',' in s['src/data/pokemon_graphics/footprint_table.h'].decode(),'Source footprint binding missing');im=Image.open(io.BytesIO(s[imagepath]));need(im.mode=='1' and im.size==(16,16),'Footprint pixels differ');ix=bytes(1 if v==0 else 0 for v in im.get_flattened_data());pal,_=palette('graphics/pokedex/kanto_dex_bgpals.pal');asset=add(stem+'-footprint',16,16,rgba(ix,pal[:16]));footprints.append(dict(sourceSpeciesId=id,source=small(imagepath,'gMonFootprint_'+name),**asset))
  dp='src/data/pokemon/pokedex_entries.h';dm=re.search(r'\[NATIONAL_DEX_'+name.upper()+r'\]\s*=\s*\{(.*?)\n    \}',s[dp].decode(),re.S);need(dm,'Dex entry missing');f=dict(re.findall(r'\.(\w+) = (.*?),\n',dm[1]));tp='src/data/pokemon/pokedex_text_fr.h';tm=re.search(r'const u8 g'+name+r'PokedexText\[\] = _\(\n(.*?)\);',s[tp].decode(),re.S);need(tm,'Dex text missing');description=''.join(json.loads(x) for x in re.findall(r'"(?:[^"\\]|\\.)*"',tm[1]));speciesname=re.search(r'\['+symbol+r'\] = _\("([^"]+)"\)',s['src/data/text/species_names.h'].decode())[1];dex.append(dict(sourceSpeciesId=id,name=speciesname,category=re.fullmatch(r'_\("([^"]+)"\)',f['categoryName'])[1],heightDecimeters=int(f['height']),weightHectograms=int(f['weight']),description=description))
 manifest=dict(schemaVersion=1,profile='firered-surf-art-v1',sourceCommit=meta.COMMIT,sprites=sprites,icons=icons,footprints=footprints,dex=dex,limits=['Party icon is source frame0 and shared by normal/shiny.','No battle animation, overworld sprites, cries or sound are enabled by this art package.','Pixel expansion follows existing catalog floor(rgb5 *255/31).'],outputs=[dict(path=p) for p,b in sorted(out.items())]);out['manifest.json']=meta.canonical(manifest);return out
if __name__=='__main__':
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--project-root',type=pathlib.Path,default=ROOT);ap.add_argument('--check',action='store_true');args=ap.parse_args();out=build(args.project_root)
 for rel,b in out.items():
  p=meta.safe(args.project_root,'content/generated/surf-art/'+rel)
  if args.check:
   if p.read_bytes()!=b:raise ValueError('Generated art differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 print(json.dumps(dict(passed=True,files=len(out),manifestBytes=len(out['manifest.json'])),indent=2))
