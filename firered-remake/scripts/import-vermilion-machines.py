#!/usr/bin/env python3
"""Incremental Meowth/Persian compatibility for the unchanged eight Bill machines. No upstream scripts executed."""
import argparse,  json, pathlib, subprocess, sys, re
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
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
 p='src/data/pokemon/tmhm_learnsets.h';text=sources[p].decode();constants=sources['include/constants/species.h'].decode();rows=[]
 for full in MACHINES:
  compatibility=[]
  for species in ('MEOWTH','PERSIAN'):
   symbol='SPECIES_'+species;matches=list(re.finditer(r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),',text,re.M|re.S))
   if len(matches)!=1:raise ValueError('Nonunique compatibility source')
   m=matches[0];tokens=re.findall(r'TMHM\((\w+)\)',m[1])
   if re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1]) or len(tokens)!=len(set(tokens)):raise ValueError('Unsupported compatibility syntax')
   ids=re.findall(r'^#define '+symbol+r'\s+(\d+)\b',constants,re.M)
   if len(ids)!=1:raise ValueError('Nonunique species identity')
   compatibility.append(dict(species=dict(value='pret:'+symbol),sourceSpeciesId=int(ids[0]),canLearn=full in tokens,source=dict(path=p,symbol=symbol,startLine=text.count('\n',0,m.start())+1,endLine=text.count('\n',0,m.end())+1)))
  rows.append(dict(item=dict(value='pret:ITEM_'+full[:4]),compatibility=compatibility))
 return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,rows=rows)
def run(repo,raw,out):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 def overlap(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlap(raw,out):raise ValueError('Raw/generated overlap')
 if repo:
  repo=pathlib.Path(repo).absolute()
  if overlap(repo,raw) or overlap(repo,out):raise ValueError('Reference output overlap')
 for kind in ('raw','generated'):
  for d in (ROOT/'content'/kind).iterdir():
   if d.is_dir() and d.name!='vermilion-machines' and (overlap(d.absolute(),raw) or overlap(d.absolute(),out)):raise ValueError('Historical output protected')
 lb=safe(ROOT,'scripts/vermilion-machines.lock.json').read_bytes()
 lock=json.loads(lb)
 if (lock['commit'], lock['profile']) != (COMMIT, PROFILE) or set((x['path'] for x in lock['files'])) != set(PATHS):raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']]) if repo else safe(raw,e['path']).read_bytes()
  sources[e['path']]=b
 data=canonical(convert(sources))
 material=[(safe(out,'machines.json'),data)]+([(safe(raw,p),b) for p,b in sources.items()] if repo else [])
 for f,_ in material:
  if f.exists() and not f.is_file():raise ValueError('Nonregular output')
  if any(x.exists() and not x.is_dir() for x in f.parents):raise ValueError('Non-directory parent')
 for f,b in material:f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,machines=8,addedSpecies=2)))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/vermilion-machines');a=p.parse_args();run(a.reference_repo,a.raw,a.out)
