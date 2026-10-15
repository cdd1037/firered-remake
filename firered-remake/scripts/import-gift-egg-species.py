#!/usr/bin/env python3
"""Authenticate the original Togepi gift and Togetic evolution without duplicating canonical mechanics."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
from machine_catalog import compatibility_additions, load_catalog

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/gift-egg-species/'
IDENTITIES=[('TOGEPI',175),('TOGETIC',176)]
DEPENDENCIES=(
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/breeding-species/pokemon.json',
    'content/generated/breeding-species/metadata.json',
)
SHARED_INPUTS={p:'content/raw/pokefirered/037335f4/'+p for p in ['include/constants/pokemon.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/pokedex_entries.h','src/data/pokemon/species_info.h']}
SHARED_INPUTS.update({'src/data/pokemon/egg_moves.h':'content/raw/pokefirered/037335f4/src/data/pokemon/egg_moves.h','src/data/pokemon/tutor_learnsets.h':'content/raw/pokefirered/037335f4/src/data/pokemon/tutor_learnsets.h','src/pokemon.c':'content/raw/pokefirered/037335f4/src/pokemon.c'})

SHARED_INPUTS.update({'src/daycare.c':'content/raw/pokefirered/037335f4/src/daycare.c','src/field_specials.c':'content/raw/pokefirered/037335f4/src/field_specials.c','src/script_pokemon_util.c':'content/raw/pokefirered/037335f4/src/script_pokemon_util.c','src/data/region_map/region_map_sections.constants.json.txt':'content/raw/pokefirered/037335f4/src/data/region_map/region_map_sections.constants.json.txt'})

def encode(value):return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
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
 canonical,previous,metadata=[dependent(p) for p in DEPENDENCIES]
 deps.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
 catalog=load_catalog(dependency)
 need(previous['ownedSpeciesCount']==182 and previous['opponentSpeciesCount']==203,'Exact predecessor')
 definitions={s['id']['value']:s for s in canonical['species']};metas={s['id']['value']:s for s in canonical['metadata']};inputs={};raw={}
 def read(p):
  if p not in raw:
   local=SHARED_INPUTS[p];data=safe(dependency,local).read_bytes()
   if reference:need(data==git('show',COMMIT+':'+p),'Shared raw differs from pinned source: '+p)
   inputs[p]=dict(path=p,localPath=local,shared=True);raw[p]=data
  return raw[p].decode()
 source_definitions=[];evolutions=[]
 for name,number in IDENTITIES:
  sid='pret:SPECIES_'+name;definition=definitions[sid];meta=metas[sid]
  need(definition['sourceSpeciesId']==meta['nationalDexNumber']==number and definition['evolutions']==[],'Canonical target')
  source_definitions.append(dict(definition=definition,metadata=meta))
  refs=[definition['source'],definition['learnsetSource'],meta['learnsetPointerSource'],meta['dexSource']]+[r['source'] for r in definition['levelUpMoves']]+[e['source'] for e in meta['sourceEvolutions']]
  for sr in refs:read(sr['path'])
  for edge in meta['sourceEvolutions']:evolutions.append(dict(species=dict(value=sid),**edge))
 need(len(evolutions)==1 and evolutions[0]['method']=='EVO_FRIENDSHIP' and evolutions[0]['parameterValue']==0 and evolutions[0]['target']['value']=='pret:SPECIES_TOGETIC','Exact friendship edge')
 cells=compatibility_additions(dependency,BASE+'machines.json');need(len(cells)==57,'Exact admitted machines')
 groups=dict((k,int(v)) for k,v in re.findall(r'^#define (EGG_GROUP_\w+)\s+(\d+)',read('include/constants/pokemon.h'),re.M));eggs=read('src/data/pokemon/egg_moves.h');egg_moves={n:re.findall(r'MOVE_\w+',body) for n,body in re.findall(r'egg_moves\(([A-Z0-9_]+),\s*(.*?)\)',eggs,re.S)}
 all_moves=metadata['allMachineMoves'];rows=[];sorted_species=sorted(canonical['species'],key=lambda r:r['sourceSpeciesId'])
 def base_species(species):
  for _ in range(5):
   parent=next((r['id']['value'] for r in sorted_species if any(e['target']['value']==species for e in metas[r['id']['value']]['sourceEvolutions'])),None)
   if parent is None:break
   species=parent
  return species
 for name,*_ in IDENTITIES:
  sid='pret:SPECIES_'+name;original=metas[sid]['originalFields'];compatible=[]
  for item in catalog.species[sid]['compatibleItems']:
   machine=item['value'].removeprefix('pret:ITEM_');i=int(machine[2:])-1+(50 if machine.startswith('HM') else 0)
   compatible.append(dict(all_moves[i]))
  group_ids=[groups[g.strip('{} ')] for g in original['eggGroups'].split(',')]
  rows.append(dict(breeding=dict(species=dict(value=sid),eggGroup1=group_ids[0],eggGroup2=group_ids[1],eggCycles=int(original['eggCycles'])),baseEggSpecies=dict(value=base_species(sid)),eggMoves=[dict(value='pret:'+m) for m in egg_moves.get(name,[])],machineMoves=compatible))
 read('src/data/pokemon/tutor_learnsets.h');read('src/pokemon.c')
 for path in ['src/daycare.c','src/field_specials.c','src/script_pokemon_util.c','src/data/region_map/region_map_sections.constants.json.txt']:read(path)
 return {
 'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-gift-egg-owned-pokemon-v1',previousOwnedSpeciesCount=187,ownedSpeciesCount=189,previousOpponentSpeciesCount=210,opponentSpeciesCount=212,ownedSpeciesAdded=['SPECIES_'+r[0] for r in IDENTITIES],opponentSpeciesAdded=['SPECIES_'+r[0] for r in IDENTITIES],evolutions=evolutions,movesAdded=[]),
 'machines.json':dict(sourceCommit=COMMIT,profile='firered-gift-egg-teaching-v1',previousSpeciesCount=187,speciesCount=189,machineCount=57,additions=cells,itemsAdded=[]),
 'metadata.json':dict(sourceCommit=COMMIT,profile='firered-gift-egg-metadata-v1',previousSpeciesCount=187,speciesCount=189,additions=rows),
 'source-input-hashes.json':dict(sourceCommit=COMMIT,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=deps)}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--project-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference_root,a.dependency_root)
 files={BASE+name:encode(doc) for name,doc in docs.items()};lock=dict(sourceCommit=COMMIT,profile='firered-gift-egg-species-v1',files=[dict(path=name) for name,data in sorted(files.items())])
 for name,data in files.items():
  path=safe(a.project_root,name)
  if a.check:need(path.read_bytes()==data,'Regeneration differs: '+name)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 print(json.dumps(dict(checked=a.check,owned=189,opponents=212,newMachineCells=114),indent=2))
if __name__=='__main__':main()
