#!/usr/bin/env python3
"""Import Four Island items, source shop/pickups and exact 38x125 teaching. No upstream execution."""
import argparse,  json, re, subprocess
from pathlib import Path
from machine_catalog import admission_view, compatibility_additions
ROOT=Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def need(v,m):
 if not v:raise ValueError(m)
def generate(reference,root):
 inputs={};texts={};dependencies=[]
 def source(path):
  if path not in texts:
   b=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
   inputs[path]=dict(path=path);texts[path]=b.decode()
  return texts[path]
 def ref(path,symbol,start,end):return dict(path=path,symbol=symbol,startLine=start,endLine=end)
 items_text=source('src/data/items.json');source_items=json.loads(items_text)['items']
 constants=source('include/constants/items.h');constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
 def metadata(symbol):
  i=next(i for i in source_items if i['itemId']==symbol)
  match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
  r=ref('src/data/items.json',symbol,line,line)
  value=dict(id={'value':'pret:'+symbol},sourceItemSymbol=symbol,sourceItemId=constant[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
  return dict(item=value,description=i['description_english'].replace('\\n','\n'))
 items=[metadata(s) for s in ['ITEM_NEVER_MELT_ICE','ITEM_HM07']]
 i=items[0]['item'];need(i['holdEffect']=='HOLD_EFFECT_ICE_POWER' and i['holdEffectParam']==10 and i['price']==100 and i['sourceItemId']==212,'NeverMeltIce original source')
 new=[{k:i[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=items[0]['description'],healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=i)]
 need(items[1]['item']['sourceItemId']==345 and items[1]['item']['pocket']=='POCKET_TM_CASE' and items[1]['item']['importance']==1,'Reusable HM07')
 need(next(i for i in source_items if i['itemId']=='ITEM_HM07')['moveId']=='Waterfall','HM07 move')
 move_constants=source('include/constants/moves.h');need(re.search(r'^#define\s+MOVE_WATERFALL\s+127\b',move_constants,re.M),'Waterfall move id')
 moves=source('src/data/battle_moves.h');waterfall=re.search(r'\[MOVE_WATERFALL\]\s*=\s*\{(.*?)\n\s*\}',moves,re.S)
 need(waterfall and all(re.search(r'\.'+k+r'\s*=\s*'+v+r'\b',waterfall[1]) for k,v in [('effect','EFFECT_HIT'),('power','80'),('type','TYPE_WATER'),('accuracy','100'),('pp','15')]),'Gen3 Waterfall source')
 additions=compatibility_additions(ROOT, 'content/generated/four-island-items/items.json', 'machineAdditions')
 need(len(additions)==37, 'Exact admitted machine scope')
 dependencies.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
 hm=admission_view(ROOT, 'content/generated/four-island-items/items.json', '/hm07')
 need(len(hm['species'])==125, 'Exact admitted HM07 species scope')
 stock_path='data/maps/FourIsland_Mart/scripts.inc';stock_text=source(stock_path);stock=re.search(r'^FourIsland_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)',stock_text,re.M)
 symbols=re.findall(r'\t\.2byte (ITEM_\w+)',stock[1]);need(symbols.pop()=='ITEM_NONE' and len(symbols)==8,'Mart stock')
 shop=dict(id='FourIsland_Mart_Items',items=[dict(value='pret:'+s) for s in symbols],source=ref(stock_path,'FourIsland_Mart_Items',stock_text[:stock.start()].count('\n')+1,stock_text[:stock.end()].count('\n')+1))
 pickups=[];flags=source('include/constants/flags.h')
 def pickup(map_symbol,event,hidden,local_id,item,quantity):
  flag=event['flag']
  def flag_value(symbol):
   m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Pickup flag')
   expr=m[1].split('//')[0].strip()
   if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
   add=re.fullmatch(r'\((FLAG_\w+) \+ (\d+)\)',expr);need(add,'Unsupported flag syntax')
   return flag_value(add[1])+int(add[2])
  info=metadata(item)['item'];need(quantity==1 and (not hidden or event['underfoot']==False),'Pickup bounds')
  pickups.append(dict(id='four-island:'+flag,mapSymbol=map_symbol,hidden=hidden,sourceLocalId=local_id,x=event['x'],y=event['y'],elevation=event['elevation'],script='EventScript_HiddenItemScript' if hidden else event['script'],itemSymbol=item,sourceItemId=info['sourceItemId'],item=info['id'],itemName=info['name'],pocket=info['pocket'],quantity=quantity,flag=flag,sourceFlagId=flag_value(flag)))
 for name in ['FourIsland','FourIsland_IcefallCave_1F','FourIsland_IcefallCave_B1F']:
  mapping=json.loads(source('data/maps/'+name+'/map.json'));scripts=source('data/scripts/item_ball_scripts.inc')
  for local_id,event in enumerate(mapping['object_events'],1):
   if event['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   m=re.search(r'^'+event['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',scripts,re.M);need(m,'Pickup recipe')
   pickup(mapping['id'],event,False,local_id,m[1],int(m[2] or 1))
  for event in mapping['bg_events']:
   if event['type']=='hidden_item':pickup(mapping['id'],event,True,None,event['item'],event['quantity'])
 need(len(pickups)==8 and sum(p['hidden'] for p in pickups)==2,'Six visible and two hidden pickups')
 for path in ['data/scripts/obtain_item.inc','src/pokemon.c','src/battle_util.c','src/battle_script_commands.c','include/constants/hold_effects.h']:source(path)
 doc=dict(sourceCommit=COMMIT,profile='firered-four-island-items-v1',items=items,additions=new,machineAdditions=additions,hm07=hm,shop=shop,pickups=pickups)
 evidence=dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
 return doc,evidence

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
 p.add_argument('--check',action='store_true');a=p.parse_args();doc,evidence=generate(a.reference,a.dependency)
 files={'content/generated/four-island-items/'+name:(json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode() for name,value in [('items.json',doc),('source-input-hashes.json',evidence)]}
 lock=dict(sourceCommit=COMMIT,profile='firered-four-island-items-v1',files=[dict(path=k) for k,v in sorted(files.items())])
 for name,data in files.items():
  dest=a.output/name
  if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
  else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 print(json.dumps(dict(checked=a.check),indent=2))
if __name__=='__main__':main()
