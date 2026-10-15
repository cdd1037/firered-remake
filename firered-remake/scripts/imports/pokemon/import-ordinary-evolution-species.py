#!/usr/bin/env python3
"""Authenticate four original ordinary evolution targets without duplicating canonical mechanics."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
from machine_catalog import compatibility_additions

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/data/pokemon/ordinary-evolution/'
IDENTITIES=[('RHYDON',112,'RHYHORN','EVO_LEVEL',42),('SLOWBRO',80,'SLOWPOKE','EVO_LEVEL',37),('CROBAT',169,'GOLBAT','EVO_FRIENDSHIP',0),('BLISSEY',242,'CHANSEY','EVO_FRIENDSHIP',0)]
DEPENDENCIES=(
    'content/generated/data/pokemon/canonical/catalog.json',
    'content/generated/data/pokemon/breeding/pokemon.json',
)
SHARED_INPUTS={p:'content/raw/pokefirered/037335f4/'+p for p in ['include/constants/pokemon.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/pokedex_entries.h','src/data/pokemon/species_info.h']}
SHARED_INPUTS.update({'src/data/pokemon/egg_moves.h':'content/raw/pokefirered/037335f4/src/data/pokemon/egg_moves.h','src/data/pokemon/tutor_learnsets.h':'content/raw/pokefirered/037335f4/src/data/pokemon/tutor_learnsets.h','src/pokemon.c':'content/raw/pokefirered/037335f4/src/pokemon.c'})

def encode(value, *, compact=False):
    return (json.dumps(value, ensure_ascii=False, indent=None if compact else 2,
                       separators=(',', ':') if compact else None) + '\n').encode()
def need(ok,message):
 if not ok:raise ValueError(message)
def safe(root,path):
 need(not Path(path).is_absolute() and not any(p in ('','.','..') for p in path.split('/')) and '\\' not in path and ':' not in path,'Unsafe relative path')
 target=root/path;need(not any(p.is_symlink() for p in [target,*target.parents]),'Symlink rejected');return target

def generate(reference,dependency):
 env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')};env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
 def git(*args):return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args],env=env)
 if reference:pass
 deps=[]
 def dependent(p):
  b=safe(dependency,p).read_bytes();deps.append(dict(path=p));return json.loads(b)
 canonical,previous=[dependent(p) for p in DEPENDENCIES]
 deps.extend(dict(path=p) for p in ['scripts/recipes/config/machine-admissions.json', 'scripts/recipes/config/machine-catalog.json'])
 need(previous['ownedSpeciesCount']==182 and previous['opponentSpeciesCount']==203,'Exact predecessor')
 definitions={s['id']['value']:s for s in canonical['species']};metas={s['id']['value']:s for s in canonical['metadata']};inputs={};raw={}
 def read(p):
  if p not in raw:
   local=SHARED_INPUTS[p];data=safe(dependency,local).read_bytes()
   if reference:need(data==git('show',COMMIT+':'+p),'Shared raw differs from pinned source: '+p)
   inputs[p]=dict(path=p,localPath=local,shared=True);raw[p]=data
  return raw[p].decode()
 source_definitions=[];evolutions=[]
 for name,number,parent,method,parameter in IDENTITIES:
  sid='pret:SPECIES_'+name;pid='pret:SPECIES_'+parent;definition=definitions[sid];meta=metas[sid]
  need(definition['sourceSpeciesId']==meta['nationalDexNumber']==number and definition['evolutions']==[] and meta['sourceEvolutions']==[],'Canonical target')
  source_definitions.append(dict(definition=definition,metadata=meta))
  edges=[e for e in metas[pid]['sourceEvolutions'] if e['target']['value']==sid]
  need(len(edges)==1 and edges[0]['method']==method and edges[0]['parameterValue']==parameter,'Exact original parent edge')
  evolutions.append(dict(species=dict(value=pid),**edges[0]))
 cells=compatibility_additions(dependency,BASE+'machines.json');need(len(cells)==51,'Exact admitted machines')
 groups=dict((k,int(v)) for k,v in re.findall(r'^#define (EGG_GROUP_\w+)\s+(\d+)',read('include/constants/pokemon.h'),re.M));eggs=read('src/data/pokemon/egg_moves.h');egg_moves={n:re.findall(r'MOVE_\w+',body) for n,body in re.findall(r'egg_moves\(([A-Z0-9_]+),\s*(.*?)\)',eggs,re.S)}
 rows=[];sorted_species=sorted(canonical['species'],key=lambda r:r['sourceSpeciesId'])
 def base_species(species):
  for _ in range(5):
   parent=next((r['id']['value'] for r in sorted_species if any(e['target']['value']==species for e in metas[r['id']['value']]['sourceEvolutions'])),None)
   if parent is None:break
   species=parent
  return species
 for name,*_ in IDENTITIES:
  sid='pret:SPECIES_'+name;original=metas[sid]
  group_ids=[groups[g] for g in original['eggGroups']]
  rows.append(dict(breeding=dict(species=dict(value=sid),eggGroup1=group_ids[0],eggGroup2=group_ids[1],eggCycles=int(original['eggCycles'])),baseEggSpecies=dict(value=base_species(sid)),eggMoves=[dict(value='pret:'+m) for m in egg_moves.get(name,[])]))
 read('src/data/pokemon/tutor_learnsets.h');read('src/pokemon.c')
 return {
 'pokemon.json':dict(previousOwnedSpeciesCount=182, ownedSpeciesCount=186, previousOpponentSpeciesCount=203, opponentSpeciesCount=204, ownedSpeciesAdded=['SPECIES_' + r[0] for r in IDENTITIES], opponentSpeciesAdded=['SPECIES_BLISSEY'], parentsUpdated=['SPECIES_' + r[2] for r in IDENTITIES], evolutions=evolutions, movesAdded=[]),
 'machines.json':dict(previousSpeciesCount=182, speciesCount=186, machineCount=51, additions=cells, itemsAdded=[]),
 'metadata.json':dict(previousSpeciesCount=182, speciesCount=186, additions=rows)}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--project-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference_root,a.dependency_root)
 files={BASE+name:encode(doc, compact=name in ('metadata.json', 'machines.json')) for name,doc in docs.items()};lock=dict(sourceCommit=COMMIT,profile='firered-ordinary-evolution-species-v1',files=[dict(path=name) for name,data in sorted(files.items())])
 for name,data in files.items():
  path=safe(a.project_root,name)
  if a.check:need(path.read_bytes()==data,'Regeneration differs: '+name)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 print(json.dumps(dict(checked=a.check,owned=186,opponents=204,newMachineCells=204),indent=2))
if __name__=='__main__':main()
