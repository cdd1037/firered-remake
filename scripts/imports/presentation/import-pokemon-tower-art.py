#!/usr/bin/env python3
"""Authenticated Gastly/Haunter ordinary presentation and Channeler portrait."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,importlib.util,io,json,pathlib,re,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT;COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe Tower art path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Tower art symlink rejected')
 return q
def resolve(roots,p):
 for root in roots:
  q=safe(root,p)
  if q.exists():return q
 raise FileNotFoundError(p)
def authenticated(roots):
 raw=safe(roots[0],'scripts/recipes/presentation/pokemon-tower-art.lock.json').read_bytes()
 lock=json.loads(raw)
 if (lock['schemaVersion'],lock['repository'],lock['commit'],len(lock['files']),len(lock['inputs']))!=(1,'pret/pokefirered',COMMIT,25,2):raise ValueError('Tower art inventory differs')
 sources={}
 for row in lock['files']+lock['inputs']:
  b=resolve(roots,row.get('localPath',row['path'])).read_bytes()
  sources[row['path']]=b
 return lock,sources

def build(roots):
 lock,s=authenticated(roots);p=resolve(roots,'scripts/imports/presentation/import-lab-battle-ui.py');spec=importlib.util.spec_from_file_location('tower_art_codec',p);ui=importlib.util.module_from_spec(spec);spec.loader.exec_module(ui)
 def need(v,m):
  if not v:raise ValueError(m)
 def palette(path):
  lines=s[path].decode().splitlines();need(lines[:2]==['JASC-PAL','0100'] and len(lines)==int(lines[2])+3,'Art palette differs');values=[tuple(map(int,x.split())) for x in lines[3:]];need(all(len(x)==3 and all(0<=c<=255 for c in x) for x in values),'Art palette values differ');return [tuple((c>>3)*255//31 for c in x) for x in values]
 def source_ref(path,symbol):
  row=next(x for x in lock['files'] if x['path']==path);return dict(kind='pret',path=path,symbol=symbol,repository='pret/pokefirered',commit=COMMIT)
 def add(stem,w,h,rgba):
  out[stem+'.png']=ui.png(w,h,rgba);return dict(texturePath=stem + '.png', width=w, height=h)
 out={};sprites=[];icons=[];footprints=[];portraits=[];graphics=s['src/data/graphics/pokemon.h'].decode();icon_source=s['src/pokemon_icon.c'].decode()
 coords={side:s['src/data/pokemon_graphics/'+side+'_pic_coordinates.h'].decode() for side in ['front','back']};elevations=s['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode()
 for name,sid in [('Gastly',92),('Haunter',93)]:
  stem=name.lower();symbol='SPECIES_'+name.upper();species='pret:'+symbol
  elevation=int(re.search(r'\['+symbol+r'\]\s*=\s*(\d+)',elevations)[1]);need(elevation==4,'Tower floating elevation differs')
  for side in ['front','back']:
   match=re.search(r'\['+symbol+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',coords[side]);need(match is not None,'Tower sprite coordinates missing');width,height,offset=map(int,match.groups());image_path=f'graphics/pokemon/{stem}/{side}.png';w,h,indices,_=ui.indexed_png(s[image_path]);need((w,h)==(64,64),'Tower sprite dimensions differ')
   need(f'graphics/pokemon/{stem}/{side}.4bpp.lz' in graphics,'Tower sprite binding differs')
   x,y=(144,8+offset-elevation) if side=='front' else (40,48+offset)
   for variant in ['normal','shiny']:
    palette_path=f'graphics/pokemon/{stem}/{variant}.pal';rgba=ui.rgba(indices,palette(palette_path));asset=add(f'{stem}-{side}-{variant}',64,64,rgba)
    sprites.append(dict(speciesId=species,sourceSpeciesId=sid,side=side,paletteVariant=variant,frameCount=1,sourceFrameIndex=0,frameWidth=64,frameHeight=64,sourceWidth=width,sourceHeight=height,sourceYOffset=offset,sourceEnemyElevation=elevation,singleBattleTopLeft=dict(x=x,y=y),transparentIndex=0,paletteExpansion='floor(rgb5 * 255 / 31)',**asset))
  palette_section=icon_source.split('const u8 gMonIconPaletteIndices[] = {',1)[1].split('};',1)[0];index=int(re.search(r'\['+symbol+r'\]\s*=\s*(\d+)',palette_section)[1]);need(index==2,'Tower icon palette differs')
  need(f'[{symbol}]' in icon_source and 'gMonIcon_'+name in icon_source,'Tower icon table differs')
  path=f'graphics/pokemon/{stem}/icon.png';w,h,indices,_=ui.indexed_png(s[path]);need((w,h)==(32,64),'Tower icon source shape differs');asset=add('icons/'+stem,32,32,ui.rgba(indices[:1024],palette('graphics/pokemon/icon_palettes/icon_palette_2.pal')))
  icons.append(dict(speciesId=species,sourceSpeciesId=sid,sourceFrameIndex=0,sourceFrameCount=2,paletteIndex=2,paletteVariant='shared-normal-shiny',source=source_ref(path,'gMonIcon_'+name),**asset))
  path=f'graphics/pokemon/{stem}/footprint.png';need(f'[{symbol}] = gMonFootprint_{name},' in s['src/data/pokemon_graphics/footprint_table.h'].decode(),'Tower footprint binding differs');im=Image.open(io.BytesIO(s[path]));need(im.mode=='1' and im.size==(16,16),'Tower footprint source shape differs');indices=bytes(1 if x==0 else 0 for x in im.get_flattened_data());asset=add(stem+'-footprint',16,16,ui.rgba(indices,palette('graphics/pokedex/kanto_dex_bgpals.pal')[:16]));footprints.append(dict(sourceSpeciesId=sid, **asset))
 path='graphics/trainers/front_pics/channeler_front_pic.png';pal='graphics/trainers/palettes/channeler.pal';need('TRAINER_SPRITE(CHANNELER, gTrainerFrontPic_Channeler, 0x800)' in s['src/data/trainer_graphics/front_pic_tables.h'].decode(),'Channeler portrait table differs');w,h,indices,_=ui.indexed_png(s[path]);need((w,h)==(64,64),'Channeler portrait shape differs');asset=add('channeler-front',64,64,ui.rgba(indices,ui.palette(s[pal])));portraits.append(dict(id='channeler-front',trainerPicture='CHANNELER',topLeft=[144,8],source=source_ref(path,'gTrainerFrontPic_Channeler'),**asset))
 meta=dict(schemaVersion=1,sprites=sprites,icons=icons,footprints=footprints,portraits=portraits,limits=['Party icon uses source frame 0, matching existing catalog convention.','No move animation timelines or cry recordings.'],outputs=[dict(path=p) for p,b in sorted(out.items())]);out['manifest.json']=encode(meta);return out

def run(root=ROOT,dependencies=(),check=False):
 out=build([root,*dependencies])
 for rel,b in out.items():
  p=safe(root,'content/generated/sprites/pokemon/pokemon-tower/'+rel)
  if check:
   if p.read_bytes()!=b:raise ValueError('Tower art output differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,sourceBlobs=25,battleSprites=8,icons=2,footprints=2,trainerPortraits=1,manifestBytes=len(out['manifest.json']))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',action='append',type=pathlib.Path,default=[]);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.dependency_root,a.check),indent=2))
