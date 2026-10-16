#!/usr/bin/env python3
"""Compile Tower item metadata, pickup data and icons from retained source files."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from generated_output_compare import check_item_outputs
from item_icon_exports import output_materials
from image_io import indexed_png, png
from item_source import ItemSource
from ui_tiles import palette, rgba
import argparse,json,pathlib,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-pokemon-tower-items-v1'
ITEMS=('ELIXIR','CLEANSE_TAG','X_ACCURACY','SOOTHE_BELL','POKE_FLUTE')
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def unique(rows):
 result={}
 for k,v in rows:
  if k in result:raise ValueError('Duplicate JSON key '+k)
  result[k]=v
 return result
def parse(b):return json.loads(b,object_pairs_hook=unique)
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe relative path')
 f=pathlib.Path(root).absolute()/p
 if any(x.is_symlink() for x in (f,*f.parents)):raise ValueError('Symlink rejected')
 return f
def read(root, p):
 f=safe(root,p)
 if f.stat().st_size>4*1024*1024:raise ValueError('Bounded file exceeded')
 b=f.read_bytes()
 return b

def outputs(data):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}
 def match(p,pattern):
  found=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(found)!=1:raise ValueError('Nonunique source match '+p+' '+pattern)
  return found[0]
 def ref(p,s,m=None):return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def number(p,s):
  m=match(p,r'^#define '+re.escape(s)+r'[ \t]+(0x[0-9A-Fa-f]+|\d+|\(FLAG_HIDDEN_ITEMS_START \+\s+\d+\))[ \t]*(?://[^\n]*)?$')
  return number(p,'FLAG_HIDDEN_ITEMS_START')+int(re.search(r'\+\s+(\d+)',m[1])[1]) if m[1].startswith('(') else int(m[1],0)
 icon_source=ItemSource(data)
 declarations=parse(data['src/data/items.json'])['items'];items=[];icons=[];result={}
 for short in ITEMS:
  symbol='ITEM_'+short;selected=[row for row in declarations if row['itemId']==symbol]
  if len(selected)!=1:raise ValueError('Expected one item declaration: '+symbol)
  i=selected[0]
  # This document emits identities only; do not load definitions from another raw root.
  description=i['description_english'].replace('\\n','\n');wrapped=dict(item='pret:'+i['itemId'],description=description)
  effects=[]
  if short in ('ELIXIR','X_ACCURACY'):
   p='src/data/pokemon/item_effects.h';camel='Elixir' if short=='ELIXIR' else 'XAccuracy';m=match(p,r'^static const u8 sItemEffect_'+camel+r'\[(\d+)\] = \{\n(.*?)^\};');actual=' '.join(re.sub(r'//[^\n]*','',m[2]).split())
   if short=='ELIXIR':
    assert actual=='[4] = ITEM4_HEAL_PP_ALL, [6] = 10,';effects=[0,0,0,0,number('include/constants/item_effects.h','ITEM4_HEAL_PP_ALL'),0,10]
   else:
    assert actual=='[2] = 1 << 4, [5] = ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID, STAT_BOOST_FRIENDSHIP_CHANGE,'
    macro=match(p,r'^#define STAT_BOOST_FRIENDSHIP_CHANGE\s+.*?mid \*/');assert re.findall('= (\\d+)', macro[0]) == ['1', '1']
    effects=[0,0,16,0,0,number('include/constants/item_effects.h','ITEM5_FRIENDSHIP_LOW')|number('include/constants/item_effects.h','ITEM5_FRIENDSHIP_MID'),1,1]
   assert len(effects)==int(m[1])
  items.append(dict(metadata=wrapped, effectBytes=effects))
  image,palette_path=icon_source.icon_sources(symbol);w,h,indices,_=indexed_png(data[image]);pixels=rgba(indices,palette(data[palette_path]));assert(w,h)==(24,24)
  pngpath=symbol.lower()+'.png';result[pngpath]=png(w,h,pixels);icons.append(dict(item=ident(symbol), width=w, height=h, pngPath=pngpath))
 pickups=[]
 for floor in range(1,8):
  p=f'data/maps/PokemonTower_{floor}F/map.json';mapdata=parse(data[p]);mapsource=ref(p,mapdata['id'])
  for index,a in enumerate(mapdata['object_events']):
   if a['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   s=a['script'];sp='data/scripts/item_ball_scripts.inc';m=match(sp,r'^'+s+r'::\s*\n(.*?)(?=^\w+::|\Z)');award=re.fullmatch(r'\s*finditem (ITEM_\w+)\s+end\s*',m[1]);assert award,s
   pickups.append(dict(map=mapdata['id'],kind='visible',localId=index+1,eventIndex=None,x=a['x'],y=a['y'],elevation=a['elevation'],flag=a['flag'],sourceFlagId=number('include/constants/flags.h',a['flag']),initiallyHidden=False,underfoot=False,requiresItemfinder=False,allowsOrdinaryInteraction=True,item=ident(award[1]),quantity=1,script=s,scriptCommand='finditem',mapSource=mapsource,scriptSource=ref(sp,s,m)))
  for index,e in enumerate(mapdata['bg_events']):
   if e['type']!='hidden_item':continue
   pickups.append(dict(map=mapdata['id'],kind='hidden',localId=None,eventIndex=index,x=e['x'],y=e['y'],elevation=e['elevation'],flag=e['flag'],sourceFlagId=number('include/constants/flags.h',e['flag']),initiallyHidden=False,underfoot=e['underfoot'],requiresItemfinder=e['underfoot'],allowsOrdinaryInteraction=not e['underfoot'],item=ident(e['item']),quantity=e['quantity'],script=None,scriptCommand=None,mapSource=mapsource,scriptSource=None))
 assert len(pickups)==10 and sum(p['kind']=='visible' for p in pickups)==8 and sum(p['allowsOrdinaryInteraction'] for p in pickups)==9
 assert all(p['quantity']==1 for p in pickups)
 result['pokemon-tower-items.json']=runtime_json_bytes(dict(schemaVersion=1,items=items,icons=icons), sort_keys=True)
 result['pokemon-tower-pickups.json']=canonical(dict(schemaVersion=1,pickups=pickups))
 return result

def directory(path):
 path=pathlib.Path(path).absolute()
 for part in (path,*path.parents):
  if part.is_symlink():raise ValueError('Symlink rejected')
  if part.exists() and not part.is_dir():raise ValueError('Non-directory path')
 return path

def run(raw,out,reference=None,lock_path=None,check=False):
 raw,out=directory(raw),directory(out)
 if raw==out or raw in out.parents or out in raw.parents:raise ValueError('Raw/output overlap')
 if reference is not None:
  reference=directory(reference)
  if any(path==reference or path in reference.parents or reference in path.parents for path in (raw,out)):
   raise ValueError('Reference/output overlap')
 lock_path=pathlib.Path(lock_path or ROOT/'scripts/recipes/items/pokemon-tower-items.lock.json')
 lock=parse(read(lock_path.parent,lock_path.name))
 if (lock['commit'],lock['profile'])!=(COMMIT,PROFILE):raise ValueError('Tower source identity differs')
 paths=[entry['path'] for entry in lock['files']]
 if not paths or len(paths)!=len(set(paths)):raise ValueError('Empty or duplicate source selection')
 data={}
 for path in paths:
  safe(raw,path)
  data[path]=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+path]) if reference else read(raw,path)
 result=outputs(data)
 manifest=dict(schemaVersion=1,inputCount=len(data),outputs=[dict(path=p) for p in sorted(result)])
 materials=[(safe(base,path),content) for (base,path),content in output_materials(out,result).items()]
 if check:
  generated=next(path for path in (out,*out.parents) if path.name=='generated')
  check_item_outputs(generated,{path.relative_to(generated).as_posix():content for path,content in materials})
  # Packing rewrites standalone icon paths and deduplicates their manifest rows.
  expected=parse(result['pokemon-tower-items.json'])['icons']
  actual=parse(read(out,'pokemon-tower-items.json'))['icons']
  paths={before['pngPath']:after['pngPath'] for before,after in zip(expected,actual)}
  manifest['outputs']=[dict(path=path) for path in dict.fromkeys(paths.get(row['path'],row['path']) for row in manifest['outputs'])]
  if parse(read(out,'manifest.json'))!=manifest:raise ValueError('Tower output manifest differs')
 else:
  materials.append((safe(out,'manifest.json'),canonical(manifest)))
  if reference:materials.extend((safe(raw,path),content) for path,content in data.items())
  for target,_ in materials:
   directory(target.parent)
   for candidate in (target,safe(target.parent,target.name+'.tmp')):
    if candidate.exists() and not candidate.is_file():raise ValueError('Non-file output path')
  for target,content in materials:
   target.parent.mkdir(parents=True,exist_ok=True)
   temporary=safe(target.parent,target.name+'.tmp');temporary.write_bytes(content);temporary.replace(target)
 return dict(sourceFiles=len(data),outputs=len(result)+1,checked=check)

def main():
 ap=argparse.ArgumentParser(description=__doc__)
 ap.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
 ap.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/items/pokemon-tower')
 ap.add_argument('--reference-root',type=pathlib.Path)
 ap.add_argument('--lock',type=pathlib.Path)
 ap.add_argument('--check',action='store_true')
 args=ap.parse_args()
 print(json.dumps(run(args.raw,args.out,args.reference_root,args.lock,args.check)))
if __name__=='__main__':main()
