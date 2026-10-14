#!/usr/bin/env python3
"""Authenticate four original ordinary evolution targets without duplicating canonical mechanics."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/ordinary-evolution-species/'
IDENTITIES=[('RHYDON',112,'RHYHORN','EVO_LEVEL',42),('SLOWBRO',80,'SLOWPOKE','EVO_LEVEL',37),('CROBAT',169,'GOLBAT','EVO_FRIENDSHIP',0),('BLISSEY',242,'CHANSEY','EVO_FRIENDSHIP',0)]
DEPENDENCIES={
 'content/generated/canonical-pokemon/catalog.json':'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc',
 'content/generated/breeding-species/pokemon.json':'4f7c0b85bda98cd0980205f5ab96875a57852efeca1ac8ea1fbd63e5e472dc58',
 'content/generated/breeding-species/machines.json':'15a696696cb67b3faf8957617e71f2890b65833145474f13e5946d38ce6dcd11',
 'content/generated/breeding-species/metadata.json':'9b079771b481f49f7cb1d9030fe9bb450a02b19d9b5d24d7ad0e47228ed8f200'}
SHARED_INPUTS={p:'content/raw/bill-pokemon/'+p for p in ['include/constants/pokemon.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/pokedex_entries.h','src/data/pokemon/species_info.h']}
SHARED_INPUTS.update({'src/data/pokemon/tmhm_learnsets.h':'content/raw/bill-machines/src/data/pokemon/tmhm_learnsets.h','src/data/pokemon/egg_moves.h':'content/raw/breeding-species/src/data/pokemon/egg_moves.h','src/data/pokemon/tutor_learnsets.h':'content/raw/move-tutors/src/data/pokemon/tutor_learnsets.h','src/pokemon.c':'content/raw/bill-items/src/pokemon.c'})

def sha(data):return hashlib.sha256(data).hexdigest()
def fingerprint(value):return sha(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode())
def encode(value):return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
def need(ok,message):
 if not ok:raise ValueError(message)
def safe(root,path):
 need(not Path(path).is_absolute() and not any(p in ('','.','..') for p in path.split('/')) and '\\' not in path and ':' not in path,'Unsafe relative path')
 target=root/path;need(not any(p.is_symlink() for p in [target,*target.parents]),'Symlink rejected');return target

def generate(reference,dependency):
 env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')};env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
 def git(*args):return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args],env=env)
 if reference:need(git('show','-s','--format=%T',COMMIT).decode().strip()==TREE,'Pinned source tree')
 deps=[]
 def dependent(p):
  b=safe(dependency,p).read_bytes();need(sha(b)==DEPENDENCIES[p],'Dependency changed: '+p);deps.append(dict(path=p,byteLength=len(b),sha256=sha(b)));return json.loads(b)
 canonical,previous,machines,metadata=[dependent(p) for p in DEPENDENCIES]
 need(previous['ownedSpeciesCount']==182 and previous['opponentSpeciesCount']==203 and machines['machineCount']==51,'Exact predecessor')
 definitions={s['id']['value']:s for s in canonical['species']};metas={s['id']['value']:s for s in canonical['metadata']};inputs={};raw={}
 def read(p):
  if p not in raw:
   local=SHARED_INPUTS[p];data=safe(dependency,local).read_bytes()
   if reference:need(data==git('show',COMMIT+':'+p),'Shared raw differs from pinned source: '+p)
   inputs[p]=dict(path=p,localPath=local,shared=True,byteLength=len(data),gitBlobSha1=hashlib.sha1(f'blob {len(data)}\0'.encode()+data).hexdigest(),sha256=sha(data));raw[p]=data
  return raw[p].decode()
 source_definitions=[];evolutions=[]
 for name,number,parent,method,parameter in IDENTITIES:
  sid='pret:SPECIES_'+name;pid='pret:SPECIES_'+parent;definition=definitions[sid];meta=metas[sid]
  need(definition['sourceSpeciesId']==meta['nationalDexNumber']==number and definition['evolutions']==[] and meta['sourceEvolutions']==[],'Canonical target')
  source_definitions.append(dict(definition=definition,metadata=meta))
  edges=[e for e in metas[pid]['sourceEvolutions'] if e['target']['value']==sid]
  need(len(edges)==1 and edges[0]['method']==method and edges[0]['parameterValue']==parameter,'Exact original parent edge')
  evolutions.append(dict(species=dict(value=pid),**edges[0]))
  for ident in [sid,pid]:
   d,m=definitions[ident],metas[ident]
   refs=[d['source'],d['learnsetSource'],m['learnsetPointerSource'],m['dexSource']]+[r['source'] for r in d['levelUpMoves']]+[e['source'] for e in m['sourceEvolutions']]
   for sr in refs:read(sr['path']);need(inputs[sr['path']]['sha256']==sr['sha256'],'Canonical source identity')
 tm='src/data/pokemon/tmhm_learnsets.h';text=read(tm);machine_ids=[r['item']['value'][5:] for r in machines['additions']];need(len(set(machine_ids))==51,'Exact machine ids');cells=[]
 for item in machine_ids:
  rows=[]
  for name,number,*_ in IDENTITIES:
   symbol='SPECIES_'+name;matches=list(re.finditer(r'^    \['+symbol+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),',text,re.M|re.S));need(len(matches)==1,'Machine row');r=matches[0]
   sr=dict(path=tm,symbol=symbol,startLine=text.count('\n',0,r.start())+1,endLine=text.count('\n',0,r.end())+1,sha256=inputs[tm]['sha256'])
   rows.append(dict(species=dict(value='pret:'+symbol),sourceSpeciesId=number,canLearn=bool(re.search(r'TMHM\('+item[5:]+r'_',r[1])),source=sr))
  cells.append(dict(item=dict(value='pret:'+item),compatibility=rows))
 groups=dict((k,int(v)) for k,v in re.findall(r'^#define (EGG_GROUP_\w+)\s+(\d+)',read('include/constants/pokemon.h'),re.M));eggs=read('src/data/pokemon/egg_moves.h');egg_moves={n:re.findall(r'MOVE_\w+',body) for n,body in re.findall(r'egg_moves\(([A-Z0-9_]+),\s*(.*?)\)',eggs,re.S)}
 all_moves=metadata['allMachineMoves'];rows=[];sorted_species=sorted(canonical['species'],key=lambda r:r['sourceSpeciesId'])
 def base_species(species):
  for _ in range(5):
   parent=next((r['id']['value'] for r in sorted_species if any(e['target']['value']==species for e in metas[r['id']['value']]['sourceEvolutions'])),None)
   if parent is None:break
   species=parent
  return species
 for name,*_ in IDENTITIES:
  sid='pret:SPECIES_'+name;original=metas[sid]['originalFields'];expr=re.search(r'^    \[SPECIES_'+name+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),',text,re.M|re.S)[1];compatible=[]
  for kind,num,move in re.findall(r'TMHM\((TM|HM)(\d+)_(\w+)\)',expr):
   i=int(num)-1+(50 if kind=='HM' else 0);need(all_moves[i]['value']=='pret:MOVE_'+move,'Original machine index');compatible.append(dict(value='pret:MOVE_'+move))
  group_ids=[groups[g.strip('{} ')] for g in original['eggGroups'].split(',')]
  rows.append(dict(breeding=dict(species=dict(value=sid),eggGroup1=group_ids[0],eggGroup2=group_ids[1],eggCycles=int(original['eggCycles'])),baseEggSpecies=dict(value=base_species(sid)),eggMoves=[dict(value='pret:'+m) for m in egg_moves.get(name,[])],machineMoves=compatible))
 read('src/data/pokemon/tutor_learnsets.h');read('src/pokemon.c')
 return {
 'pokemon.json':dict(sourceCommit=COMMIT,sourceTree=TREE,profile='firered-ordinary-evolution-owned-pokemon-v1',canonicalCatalogSha256=deps[0]['sha256'],predecessorCatalogSha256=deps[1]['sha256'],previousOwnedSpeciesCount=182,ownedSpeciesCount=186,previousOpponentSpeciesCount=203,opponentSpeciesCount=204,ownedSpeciesAdded=['SPECIES_'+r[0] for r in IDENTITIES],opponentSpeciesAdded=['SPECIES_BLISSEY'],parentsUpdated=['SPECIES_'+r[2] for r in IDENTITIES],evolutions=evolutions,movesAdded=[],canonicalSpeciesFingerprint=fingerprint(source_definitions)),
 'machines.json':dict(sourceCommit=COMMIT,profile='firered-ordinary-evolution-teaching-v1',previousSpeciesCount=182,speciesCount=186,machineCount=51,additions=cells,additionsSemanticSha256=fingerprint(cells),itemsAdded=[]),
 'metadata.json':dict(sourceCommit=COMMIT,profile='firered-ordinary-evolution-metadata-v1',previousSpeciesCount=182,speciesCount=186,additions=rows,semanticSha256=fingerprint(rows)),
 'source-input-hashes.json':dict(sourceCommit=COMMIT,sourceTree=TREE,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=deps)}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--project-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference_root,a.dependency_root)
 files={BASE+name:encode(doc) for name,doc in docs.items()};lock=dict(sourceCommit=COMMIT,sourceTree=TREE,profile='firered-ordinary-evolution-species-v1',importerSha256=sha(Path(__file__).read_bytes()),files=[dict(path=name,byteLength=len(data),sha256=sha(data)) for name,data in sorted(files.items())]);files['scripts/ordinary-evolution-species.lock.json']=encode(lock)
 for name,data in files.items():
  path=safe(a.project_root,name)
  if a.check:need(path.read_bytes()==data,'Regeneration differs: '+name)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 print(json.dumps(dict(checked=a.check,owned=186,opponents=204,newMachineCells=204,machineFingerprint=docs['machines.json']['additionsSemanticSha256'],metadataFingerprint=docs['metadata.json']['semanticSha256'],hashes={p:sha(b) for p,b in files.items()}),indent=2))
if __name__=='__main__':main()
