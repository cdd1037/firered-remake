#!/usr/bin/env python3
"""Exact offline FireRed Route5/6 land and held-item source import. No upstream scripts executed."""
import argparse, hashlib, json, pathlib, subprocess, sys, re
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-vermilion-encounters-v1'
LOCK_SHA256='059ffca76a2fe880b362ee0babe44d342b78a44624636e5251f58dabf01f1428'
GENERATED_SHA256='d80ed8568b1f975b1ce168bd08a32e347fb9d9efaecc0f9b32cab84b090d5177'
PATHS=('src/data/wild_encounters.json', 'src/wild_encounter.c', 'src/data/pokemon/species_info.h', 'src/pokemon.c')
WEIGHTS=(20,20,10,10,10,10,5,5,4,4,1,1)
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,p):
 if pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Symlink rejected')
 return q
def convert(lock,sources):
 species=sources['src/data/pokemon/species_info.h'].decode()
 block=re.search(r'^    \[SPECIES_MEOWTH\] =\s*\n    \{\n(.*?)^    \},',species,re.M|re.S)
 if block is None or '.itemCommon = ITEM_NONE,' not in block[1] or '.itemRare = ITEM_NUGGET,' not in block[1]:raise ValueError('Meowth source held table differs')
 method=re.search(r'^void SetWildMonHeldItem\(void\)\n\{(.*?)^\}',sources['src/pokemon.c'].decode(),re.M|re.S)
 if method is None or 'u16 rnd = Random() % 100;' not in method[1] or 'if (rnd <= 94)' not in method[1]:raise ValueError('Source held-item draw boundary differs')
 doc=json.loads(sources['src/data/wild_encounters.json']);tables=[]
 for name in ('Route5','Route6'):
  rows=[e for group in doc['wild_encounter_groups'] for e in group.get('encounters',[]) if e['base_label']=='s'+name+'_FireRed']
  if len(rows)!=1 or len(rows[0]['land_mons']['mons'])!=12:raise ValueError('Expected exact FireRed land table')
  land=rows[0]['land_mons'];tables.append(dict(map=dict(value='map.route-'+name[5:]),sourceCommit=COMMIT,sourcePath='src/data/wild_encounters.json',sourceSymbol=rows[0]['base_label'],sourceSha256=sha(sources['src/data/wild_encounters.json']),rawEncounterRate=land['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=WEIGHTS[i]) for i,m in enumerate(land['mons'])]))
 return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tables=tables,heldItemDrawBound=100,meowthRareItem='ITEM_NUGGET',meowthRareMinimumRoll=95,limitations=['Land only; Route6 surfing and fishing remain unavailable.', 'Meowth rare Nugget consumes the original held-item draw; Pickup is an independent post-victory step.'])
def run(repo,raw,out):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 def overlap(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlap(raw,out):raise ValueError('Raw/generated overlap')
 if repo:
  repo=pathlib.Path(repo).absolute()
  if overlap(repo,raw) or overlap(repo,out):raise ValueError('Reference output overlap')
  if subprocess.check_output(['git','-C',str(repo),'rev-parse',COMMIT+'^{tree}']).decode().strip()!=TREE:raise ValueError('Pinned tree differs')
 for kind in ('raw','generated'):
  for d in (ROOT/'content'/kind).iterdir():
   if d.is_dir() and d.name!='vermilion-encounters' and (overlap(d.absolute(),raw) or overlap(d.absolute(),out)):raise ValueError('Historical output protected')
 lb=safe(ROOT,'scripts/vermilion-encounters.lock.json').read_bytes()
 if sha(lb)!=LOCK_SHA256:raise ValueError('Pinned source lock differs')
 lock=json.loads(lb)
 if (lock['commit'],lock['tree'],lock['profile'])!=(COMMIT,TREE,PROFILE) or set(x['path'] for x in lock['files'])!=set(PATHS):raise ValueError('Source identity differs')
 sources={}
 for e in lock['files']:
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']]) if repo else safe(raw,e['path']).read_bytes()
  if len(b)!=e['byteLength'] or sha(b)!=e['sha256'] or hashlib.sha1(('blob '+str(len(b))+'\0').encode()+b).hexdigest()!=e['gitBlobSha1']:raise ValueError('Pinned input differs')
  sources[e['path']]=b
 data=canonical(convert(lock,sources))
 if sha(data)!=GENERATED_SHA256:raise ValueError('Generated source differs')
 material=[(safe(out,'encounters.json'),data)]+([(safe(raw,p),b) for p,b in sources.items()] if repo else [])
 for f,_ in material:
  if f.exists() and not f.is_file():raise ValueError('Nonregular output')
  if any(x.exists() and not x.is_dir() for x in f.parents):raise ValueError('Non-directory parent')
 for f,b in material:f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,tables=2,slots=24,sha256=sha(data))))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/vermilion-encounters');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/vermilion-encounters');a=p.parse_args();run(a.reference_repo,a.raw,a.out)
