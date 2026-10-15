#!/usr/bin/env python3
"""Exact offline FireRed Route11 and Digletts Cave B1F land and held-item source import. No upstream scripts executed."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, pathlib, subprocess, sys, re
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-route11-encounters-v1'
PATHS=('src/data/wild_encounters.json', 'src/wild_encounter.c', 'src/data/pokemon/species_info.h', 'src/pokemon.c')
WEIGHTS=(20,20,10,10,10,10,5,5,4,4,1,1)
def unique_pairs(pairs):
 result={}
 for key,value in pairs:
  if key in result:raise ValueError('Duplicate JSON field: '+key)
  result[key]=value
 return result
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,p):
 if pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Symlink rejected')
 return q
def convert(sources):
 species=sources['src/data/pokemon/species_info.h'].decode(); held=[]
 for symbol in ('SPECIES_EKANS','SPECIES_SPEAROW','SPECIES_DROWZEE','SPECIES_DIGLETT','SPECIES_DUGTRIO'):
  block=re.search(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',species,re.M|re.S)
  if block is None or '.itemCommon = ITEM_NONE,' not in block[1] or '.itemRare = ITEM_NONE,' not in block[1]:raise ValueError('Source held item table differs: '+symbol)
  held.append(dict(species='pret:'+symbol,common='ITEM_NONE',rare='ITEM_NONE',sourcePath='src/data/pokemon/species_info.h'))
 method=re.search(r'^void SetWildMonHeldItem\(void\)\n\{(.*?)^\}',sources['src/pokemon.c'].decode(),re.M|re.S)
 if method is None or 'u16 rnd = Random() % 100;' not in method[1] or method[1].find('u16 rnd = Random() % 100;') > method[1].find('itemCommon'):raise ValueError('Source unconditional held-item draw differs')
 doc=json.loads(sources['src/data/wild_encounters.json'],object_pairs_hook=unique_pairs);tables=[];excluded=[]
 for name,mapid in (('Route11','map.route-11'),('DiglettsCaveB1F','map.digletts-cave-b1f')):
  rows=[e for group in doc['wild_encounter_groups'] for e in group.get('encounters',[]) if e['base_label']=='s'+name+'_FireRed']
  if len(rows)!=1 or len(rows[0]['land_mons']['mons'])!=12:raise ValueError('Expected exact FireRed land table')
  land=rows[0]['land_mons'];tables.append(dict(map=dict(value=mapid),rawEncounterRate=land['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=WEIGHTS[i]) for i,m in enumerate(land['mons'])]))
  for method in ('water_mons','fishing_mons'):
   if method in rows[0]:excluded.append(dict(map=mapid,sourceSymbol=rows[0]['base_label'],method=method,sourceTable=rows[0][method],enabled=False))
 return dict(schemaVersion=1,tables=tables,heldItemDrawBound=100,heldItems=held,excludedTables=excluded)

def run(repo,raw,out):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 def overlap(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlap(raw,out):raise ValueError('Raw/generated overlap')
 if repo:
  repo=pathlib.Path(repo).absolute()
  if overlap(repo,raw) or overlap(repo,out):raise ValueError('Reference output overlap')
  if subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD']).decode().strip() != COMMIT:raise ValueError('Pinned commit/tree differs')
 lb=safe(ROOT,'scripts/recipes/story/route11-encounters.lock.json').read_bytes()
 lock=json.loads(lb,object_pairs_hook=unique_pairs)
 if (lock['commit'], lock['profile'], lock.get('schemaVersion'), lock.get('repository'), lock.get('gameEdition'), lock.get('romRevision')) != (COMMIT, PROFILE, 1, 'pret/pokefirered', 'FIRERED', 0) or len(lock['files']) != len(PATHS) or set((x['path'] for x in lock['files'])) != set(PATHS):raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']]) if repo else safe(raw,e['path']).read_bytes()
  sources[e['path']]=b
 data=canonical(convert(sources))
 material=[(out,data)]+([(safe(raw,p),b) for p,b in sources.items()] if repo else [])
 for f,_ in material:
  if f.exists() and not f.is_file():raise ValueError('Nonregular output')
  if any(x.exists() and not x.is_dir() for x in f.parents):raise ValueError('Non-directory parent')
 for f,b in material:f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,tables=2,slots=24)))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/encounters/route11.json');a=p.parse_args();run(a.reference_repo,a.raw,a.out)
