#!/usr/bin/env python3
"""Reconstruct the six remaining FireRed TMs and their original acquisition references."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
from machine_catalog import machine_definitions

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/original-machine-catalog/'
PROFILE = 'firered-original-machine-catalog-v1'
MACHINES = ['TM10', 'TM18', 'TM27', 'TM29', 'TM42', 'TM48']
MAPS = ['Route12', 'Route15', 'Route12_NorthEntrance_2F', 'SaffronCity_MrPsychicsHouse', 'FiveIsland_MemorialPillar']
DEPENDENCIES = (
    'scripts/import-config/machine-admissions.json',
    'scripts/import-config/machine-catalog.json',
)

def encode(value): return (json.dumps(value, separators=(',', ':'), ensure_ascii=False) + '\n').encode()
def need(ok, message):
 if not ok: raise ValueError(message)
def safe(root, path):
 need(not Path(path).is_absolute() and not any(p in ('', '.', '..') for p in path.split('/')) and '\\' not in path and ':' not in path, 'Unsafe relative path')
 target = root / path
 need(not any(p.is_symlink() for p in [target, *target.parents]), 'Symlink rejected')
 return target

def generate(reference, dependency):
 env = {k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
 env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
 def git(*args): return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks', '-C', str(reference), *args], env=env)
 raw = {}; inputs = {}; deps = []
 def read(path):
  if path not in raw:
   data = git('show', COMMIT + ':' + path)
   blob = git('rev-parse', COMMIT + ':' + path).decode().strip()
   inputs[path] = dict(path=path); raw[path] = data.decode()
  return raw[path]
 def ref(path, symbol, start, end):
  return dict(path=path, symbol=symbol, startLine=start, endLine=end)
 def matchref(path, symbol, match):
  text=read(path); return ref(path, symbol, text.count('\n',0,match.start())+1, text.count('\n',0,match.end())+1)
 deps.extend(dict(path=path) for path in DEPENDENCIES)
 added=machine_definitions(dependency, BASE+'machines.json', 'additions')
 need([row['item'] for row in added]==['pret:ITEM_'+name for name in MACHINES], 'Exact admitted remaining machines')
 need(all(len(row['species'])==186 for row in added), 'Exact owned roster')
 # Parse ordinary map script blocks and follow their source label edges. No coordinates or synthetic rewards.
 blocks={}
 def parse_blocks(path):
  text=read(path);labels=list(re.finditer(r'^(\w+)::\s*$',text,re.M));result={}
  for i,label in enumerate(labels):
   end=labels[i+1].start() if i+1<len(labels) else len(text)
   lines=[dict(line=text.count('\n',0,label.end())+n+1,text=line.strip()) for n,line in enumerate(text[label.end():end].splitlines()) if line.strip()]
   result[label[1]]=dict(label=label[1],source=ref(path,label[1],text.count('\n',0,label.start())+1,text.count('\n',0,end)),commands=lines)
  blocks.update(result);return result
 parse_blocks('data/scripts/item_ball_scripts.inc')
 map_docs=[]
 for map_name in MAPS:
  path='data/maps/'+map_name+'/map.json';doc=json.loads(read(path));map_docs.append((map_name,path,doc))
  parse_blocks('data/maps/'+map_name+'/scripts.inc')
 def reachable(label):
  found=set();todo=[label]
  while todo:
   current=todo.pop()
   if current in found or current not in blocks:continue
   found.add(current)
   todo.extend(token for c in blocks[current]['commands'] for token in re.findall(r'\b\w+\b',c['text']) if token in blocks and token not in found)
  return found
 acquisitions=[];used=set()
 for map_name,path,doc in map_docs:
  for group in ['object_events','bg_events']:
   for index,event in enumerate(doc[group]):
    root=event.get('script');closure=reachable(root)
    grants=[]
    for label in sorted(closure):
     for command in blocks[label]['commands']:
      m=re.match(r'(finditem|giveitem|giveitem_msg)\s+(.+)$',command['text'])
      if m:
       args=[x.strip() for x in m[2].split(',')];offset=1 if m[1]=='giveitem_msg' else 0
       if args[offset] in ['ITEM_'+x for x in MACHINES]:
        quantity=args[offset+1] if len(args)>offset+1 else '1';need(quantity.isdigit(),'Static source quantity')
        grants.append(dict(item=dict(value='pret:'+args[offset]),grantScript=label,operation=m[1],quantity=int(quantity),source=ref(blocks[label]['source']['path'],label,command['line'],command['line'])))
    if grants:
     used.update(closure)
     acquisitions.append(dict(kind='itemBall' if all(g['operation']=='finditem' for g in grants) else 'npcScript',mapId=doc['id'],mapSourcePath=path,eventGroup=group,eventIndex=index,eventScript=root,sourceEvent=event,grants=grants,reachableScripts=sorted(closure)))
 pickup_path='src/battle_script_commands.c';text=read(pickup_path)
 table=re.search(r'static const struct PickupItem sPickupItems\[\] =\s*\{(.*?)\};',text,re.S);need(table is not None,'Pickup table')
 rows=[(m[1],int(m[2]),m.start()) for m in re.finditer(r'\{ (ITEM_\w+), (\d+) \}',table[1])]
 index=next(i for i,r in enumerate(rows) if r[0]=='ITEM_TM10');need(index>0,'Pickup predecessor threshold')
 pickup_function=re.search(r'static void Cmd_pickup\(void\)\n\{.*?^\}',text,re.M|re.S);need(pickup_function is not None,'Pickup function')
 pickup=dict(item=dict(value='pret:ITEM_TM10'),kind='pickup',tableSource=matchref(pickup_path,'sPickupItems',table),functionSource=matchref(pickup_path,'Cmd_pickup',pickup_function),lowerRollInclusive=rows[index-1][1],upperRollExclusive=rows[index][1],tableIndex=index,eligibilitySource=pickup_function[0])
 transfer=[]
 for path,symbols in [('src/tm_case.c',['Action_Give','Task_SelectedTMHM_GiveParty','Task_SelectedTMHM_GivePC']),('src/party_menu.c',['CursorCB_TakeItem','TryTakeMonItem'])]:
  text=read(path)
  for symbol in symbols:
   function=re.search(r'static (?:void|u8) '+symbol+r'\((?:u8 taskId|struct Pokemon \*mon)\)\n\{.*?^\}',text,re.M|re.S);need(function is not None,'Original held transfer '+symbol)
   transfer.append(dict(source=matchref(path,symbol,function),text=function[0]))
 policy=re.search(r'^#define IS_HM\(itemId\) \(ItemId_GetImportance\(itemId\) != 0\)',read('src/tm_case.c'),re.M);need(policy is not None,'Source important machine Give restriction')
 transfer.append(dict(source=matchref('src/tm_case.c','IS_HM',policy),text=policy[0]))
 mapped={g['item']['value'][10:] for a in acquisitions for g in a['grants']}|{'TM10'};need(mapped==set(MACHINES),'Exact acquisition coverage')
 return {
  'machines.json':dict(sourceCommit=COMMIT,profile=PROFILE,previousMachineCount=51,machineCount=57,speciesCount=186,additions=added),
  'acquisitions.json':dict(sourceCommit=COMMIT,profile=PROFILE,acquisitions=acquisitions,pickup=pickup,heldTransferSource=transfer,scriptBlocks=[blocks[label] for label in sorted(used)]),
  'source-input-hashes.json':dict(sourceCommit=COMMIT,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=deps)}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--project-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
 docs=generate(a.reference_root,a.dependency_root);files={BASE+name:encode(doc) for name,doc in docs.items()}
 lock=dict(sourceCommit=COMMIT,profile=PROFILE,files=[dict(path=name) for name,data in sorted(files.items())])
 for name,data in files.items():
  path=safe(a.project_root,name)
  if a.check:need(path.read_bytes()==data,'Regeneration differs: '+name)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 print(json.dumps(dict(checked=a.check,machines=57,species=186),indent=2))
if __name__=='__main__': main()
