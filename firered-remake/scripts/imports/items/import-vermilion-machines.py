#!/usr/bin/env python3
"""Incremental Meowth/Persian compatibility for the unchanged eight Bill machines. No upstream scripts executed."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, pathlib, subprocess, sys, re
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
import sys
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from machine_catalog import load_catalog, machine_reference, compatibility_reference
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-vermilion-machines-v1'
PATHS=('src/data/pokemon/tmhm_learnsets.h', 'include/constants/species.h')
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,p):
 if pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Symlink rejected')
 return q
MACHINES=('TM39_ROCK_TOMB','TM05_ROAR','TM09_BULLET_SEED','TM46_THIEF','TM03_WATER_PULSE','TM45_ATTRACT','TM43_SECRET_POWER','TM28_DIG')
def convert(sources):
 shared = load_catalog(ROOT)
 rows = [dict(item=dict(value='pret:ITEM_' + item[:4]), compatibility=[
  compatibility_reference(shared, 'pret:ITEM_' + item[:4], 'pret:SPECIES_' + species)
  for species in ('MEOWTH', 'PERSIAN')]) for item in MACHINES]
 return dict(schemaVersion=1,rows=rows)
def run(repo,raw,out):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 def overlap(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlap(raw,out):raise ValueError('Raw/generated overlap')
 if repo:
  repo=pathlib.Path(repo).absolute()
  if overlap(repo,raw) or overlap(repo,out):raise ValueError('Reference output overlap')
 lb=safe(ROOT,'scripts/recipes/items/vermilion-machines.lock.json').read_bytes()
 lock=json.loads(lb)
 if (lock['commit'], lock['profile']) != (COMMIT, PROFILE) or set((x['path'] for x in lock['files'])) != set(PATHS):raise ValueError('Source identity differs')
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
 print(json.dumps(dict(machines=8,addedSpecies=2)))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/machine-learning/vermilion.json');a=p.parse_args();run(a.reference_repo,a.raw,a.out)
