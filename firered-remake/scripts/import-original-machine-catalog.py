#!/usr/bin/env python3
"""Reconstruct the six remaining FireRed TMs and their original acquisition references."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/original-machine-catalog/'
PROFILE = 'firered-original-machine-catalog-v1'
MACHINES = ['TM10', 'TM18', 'TM27', 'TM29', 'TM42', 'TM48']
MAPS = ['Route12', 'Route15', 'Route12_NorthEntrance_2F', 'SaffronCity_MrPsychicsHouse', 'FiveIsland_MemorialPillar']
DEPENDENCIES = {
 'content/generated/department-machine-stock/machines.json': '4b6d7d5d11c453a081c7fafef86e296938a12fe4b8df77e1513479be5701d48a',
 'content/generated/stone-evolution-species/machines.json': '333dc352f5e9fc491c05a333250142cb5c5e24ed4c525240fc864629621fb2ab',
 'content/generated/game-corner-prizes/machines.json': 'c30c6f9ef42bef171aa4b618438c4646c32a99816ccd2893f4d12fcde118e2bf',
 'content/generated/breeding-species/machines.json': '15a696696cb67b3faf8957617e71f2890b65833145474f13e5946d38ce6dcd11',
 'content/generated/ordinary-evolution-species/machines.json': 'f64f126985c190ccf739fa508b14d0f755bd179beba4426c168ce9f40ce89c88'}

def sha(data): return hashlib.sha256(data).hexdigest()
def fingerprint(value): return sha(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode())
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
 need(git('show', '-s', '--format=%T', COMMIT).decode().strip() == TREE, 'Pinned source tree')
 raw = {}; inputs = {}; deps = []
 def read(path):
  if path not in raw:
   data = git('show', COMMIT + ':' + path)
   blob = git('rev-parse', COMMIT + ':' + path).decode().strip()
   need(hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest() == blob, 'Git blob integrity')
   inputs[path] = dict(path=path, byteLength=len(data), gitBlobSha1=blob, sha256=sha(data)); raw[path] = data.decode()
  return raw[path]
 def ref(path, symbol, start, end):
  return dict(path=path, symbol=symbol, startLine=start, endLine=end, sha256=inputs[path]['sha256'])
 def matchref(path, symbol, match):
  text=read(path); return ref(path, symbol, text.count('\n',0,match.start())+1, text.count('\n',0,match.end())+1)
 documents=[]
 for path, expected in DEPENDENCIES.items():
  data=safe(dependency,path).read_bytes();need(sha(data)==expected, 'Dependency changed: '+path)
  deps.append(dict(path=path, byteLength=len(data), sha256=expected));documents.append(json.loads(data))
 department, stone, prizes, breeding, ordinary=documents
 roster=department['tm15']['compatibility']+stone['additions'][0]['compatibility']+prizes['inherited'][0]['compatibility']+breeding['additions'][0]['compatibility']+ordinary['additions'][0]['compatibility']
 roster=sorted([(row['species']['value'],row['sourceSpeciesId']) for row in roster],key=lambda row:row[1])
 need(len(roster)==186 and len(set(roster))==186 and len({r[0] for r in roster})==186, 'Exact owned roster')
 old_ids=[row['item']['value'][10:] for row in ordinary['additions']]
 need(len(old_ids)==51 and set(old_ids).isdisjoint(MACHINES) and set(old_ids+MACHINES)=={f'TM{i:02d}' for i in range(1,51)}|{f'HM{i:02d}' for i in range(1,8)}, 'Original 50 TMs and seven used HMs')
 ip='src/data/items.json'; items_text=read(ip); items=json.loads(items_text)['items']
 item_ids=dict(re.findall(r'^#define (ITEM_TM\d+)\s+(\d+)\b',read('include/constants/items.h'),re.M))
 move_ids=dict(re.findall(r'^#define (MOVE_\w+)\s+(\d+)\b',read('include/constants/moves.h'),re.M))
 tm_path='src/data/pokemon/tmhm_learnsets.h'; tm=read(tm_path)
 tm_moves=dict(re.findall(r'TMHM\((TM\d+)_(\w+)\)',tm))
 battle_path='src/data/battle_moves.h';battle=read(battle_path)
 desc_path='src/move_descriptions.c';descriptions=read(desc_path)
 added=[]
 for machine_id in MACHINES:
  symbol='ITEM_'+machine_id; item=next(i for i in items if i['itemId']==symbol)
  pos=items_text.index('"itemId": "'+symbol+'"');start=items_text.rfind('{',0,pos);end=items_text.index('}',pos)
  item_source=ref(ip,symbol,items_text.count('\n',0,items_text.index('"english"',start))+1,items_text.count('\n',0,end))
  value=dict(id=dict(value='pret:'+symbol),sourceItemSymbol=symbol,sourceItemId=int(item_ids[symbol]),name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=item['type'],holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=item['importance']==0,canSell=item['importance']==0,canGive=item['importance']==0,source=item_source)
  move=tm_moves[machine_id]; move_symbol='MOVE_'+move
  dm=re.search(r'const u8 (gMoveDescription_'+''.join(word.title() for word in move.split('_'))+r')\[\] = _\("(.*?)"\);', descriptions)
  need(dm is not None,'Move description '+move)
  machine=dict(item=value,description=json.loads('"'+dm[2]+'"'),declarationDescription=item['description_english'].replace('\\n','\n'),descriptionSource=matchref(desc_path,dm[1],dm))
  bm=re.search(r'\['+move_symbol+r'\]\s*=\s*\{(.*?)\},',battle,re.S);need(bm is not None,'Battle move')
  pp=int(re.search(r'\.pp\s*=\s*(\d+)',bm[1])[1]);rows=[]
  for species,number in roster:
   symbol=species[5:];m=re.search(r'^    \['+symbol+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),',tm,re.M|re.S);need(m is not None,'Compatibility row '+symbol)
   rows.append(dict(species=dict(value=species),sourceSpeciesId=number,canLearn='TMHM('+machine_id+'_'+move+')' in m[1],source=matchref(tm_path,symbol,m)))
  added.append(dict(machine=machine,move=dict(value='pret:'+move_symbol),sourceMoveId=int(move_ids[move_symbol]),movePp=pp,compatibility=rows))
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
  'machines.json':dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,previousMachineCount=51,machineCount=57,speciesCount=186,predecessorMachinesSha256=DEPENDENCIES['content/generated/ordinary-evolution-species/machines.json'],additions=added,additionsSemanticSha256=fingerprint(added)),
  'acquisitions.json':dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,acquisitions=acquisitions,pickup=pickup,heldTransferSource=transfer,scriptBlocks=[blocks[label] for label in sorted(used)]),
  'source-input-hashes.json':dict(sourceCommit=COMMIT,sourceTree=TREE,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=deps)}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--project-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
 docs=generate(a.reference_root,a.dependency_root);files={BASE+name:encode(doc) for name,doc in docs.items()}
 lock=dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,importerSha256=sha(Path(__file__).read_bytes()),files=[dict(path=name,byteLength=len(data),sha256=sha(data)) for name,data in sorted(files.items())]);files['scripts/original-machine-catalog.lock.json']=encode(lock)
 for name,data in files.items():
  path=safe(a.project_root,name)
  if a.check:need(path.read_bytes()==data,'Regeneration differs: '+name)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 print(json.dumps(dict(checked=a.check,machines=57,species=186,additionsFingerprint=docs['machines.json']['additionsSemanticSha256'],hashes={p:sha(b) for p,b in files.items()}),indent=2))
if __name__=='__main__': main()
