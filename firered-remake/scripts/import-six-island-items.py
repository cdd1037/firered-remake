#!/usr/bin/env python3
"""Import Six Island items, source shop/pickups and exact 38x132 teaching. No upstream execution."""
import argparse,  json, re, subprocess
from pathlib import Path
from machine_catalog import compatibility_additions
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
  return dict(item=value,description=i['description_english'].replace('\\n','\n'),declarationDescription=i['description_english'],descriptionSource=r)
 symbols=['ITEM_SUN_STONE','ITEM_DRAGON_SCALE','ITEM_DREAM_MAIL']
 items=[metadata(s) for s in symbols]
 new=[]
 for m in items:
  i=m['item']
  new.append({k:i[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=m['description'],healHp=0,cureStatus=0,effectBytes=[],useSupport=9 if i['sourceItemSymbol']=='ITEM_SUN_STONE' else 10,effectSource=None,sourceMetadata=i))
 need(items[0]['item']['sourceItemId']==93 and items[0]['item']['fieldUseFunction']=='FieldUseFunc_EvoItem','Sun Stone source evolution use')
 need(items[1]['item']['sourceItemId']==201 and items[1]['item']['holdEffect']=='HOLD_EFFECT_DRAGON_SCALE' and items[1]['item']['holdEffectParam']==10,'Dragon Scale is not Dragon Fang')
 need(items[2]['item']['sourceItemId']==130 and items[2]['item']['fieldUseFunction']=='FieldUseFunc_Mail','Dream Mail source identity')
 additions=compatibility_additions(ROOT, 'content/generated/six-island-items/items.json', 'machineAdditions')
 need(len(additions)==38, 'Exact admitted machine scope')
 dependencies.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
 stock_path='data/maps/SixIsland_Mart/scripts.inc';stock_text=source(stock_path);stock=re.search(r'^SixIsland_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)',stock_text,re.M)
 symbols=re.findall(r'\t\.2byte (ITEM_\w+)',stock[1]);need(symbols.pop()=='ITEM_NONE' and len(symbols)==8,'Mart stock')
 shop=dict(id='SixIsland_Mart_Items',items=[dict(value='pret:'+s) for s in symbols],source=ref(stock_path,'SixIsland_Mart_Items',stock_text[:stock.start()].count('\n')+1,stock_text[:stock.end()].count('\n')+1))
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
  pickups.append(dict(id='six-island:'+flag,mapSymbol=map_symbol,hidden=hidden,sourceLocalId=local_id,x=event['x'],y=event['y'],elevation=event['elevation'],script='EventScript_HiddenItemScript' if hidden else event['script'],itemSymbol=item,sourceItemId=info['sourceItemId'],item=info['id'],itemName=info['name'],pocket=info['pocket'],quantity=quantity,flag=flag,sourceFlagId=flag_value(flag)))
 for name in ['SixIsland','SixIsland_WaterPath','SixIsland_RuinValley']:
  mapping=json.loads(source('data/maps/'+name+'/map.json'));scripts=source('data/scripts/item_ball_scripts.inc')
  for local_id,event in enumerate(mapping['object_events'],1):
   if event['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   m=re.search(r'^'+event['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',scripts,re.M);need(m,'Pickup recipe')
   pickup(mapping['id'],event,False,local_id,m[1],int(m[2] or 1))
  for event in mapping['bg_events']:
   if event['type']=='hidden_item':pickup(mapping['id'],event,True,None,event['item'],event['quantity'])
 need(len(pickups)==9 and sum(p['hidden'] for p in pickups)==4,'Five visible and four hidden pickups')
 for path in ['data/scripts/obtain_item.inc','src/pokemon.c','src/battle_util.c','src/battle_script_commands.c','include/constants/hold_effects.h']:source(path)
 doc=dict(sourceCommit=COMMIT,profile='firered-six-island-items-v1',items=items,additions=new,machineAdditions=additions,shop=shop,pickups=pickups,unsupportedUses=[dict(item='ITEM_DRAGON_SCALE',boundary='Held-item trade evolution trigger is not implemented. No NPC, fee or alternate trigger is invented.'),dict(item='ITEM_DREAM_MAIL',boundary='Mail reading, writing, attaching and delivery are not implemented; purchase, inventory and source sale remain ordinary supported transactions.')])
 evidence=dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
 return doc,evidence

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
 p.add_argument('--check',action='store_true');a=p.parse_args();doc,evidence=generate(a.reference,a.dependency)
 files={'content/generated/six-island-items/'+name:(json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode() for name,value in [('items.json',doc),('source-input-hashes.json',evidence)]}
 lock=dict(sourceCommit=COMMIT,profile='firered-six-island-items-v1',files=[dict(path=k) for k,v in sorted(files.items())])
 for name,data in files.items():
  dest=a.output/name
  if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
  else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 print(json.dumps(dict(checked=a.check),indent=2))
if __name__=='__main__':main()
