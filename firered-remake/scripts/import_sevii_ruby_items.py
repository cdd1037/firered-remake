#!/usr/bin/env python3
"""Derive Ruby keys and HM06/118-species teaching from pinned local source only."""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import  json, re
from pathlib import Path
from machine_catalog import admission_view, compatibility_additions
ROOT=Path(__file__).resolve().parents[1]
RAW=ROOT/'content/raw/pokefirered/037335f4'
OUT=ROOT/'content/generated/sevii-ruby-items'
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def source(path):
 b=(RAW/path).read_bytes();return b.decode()
def ref(path,symbol,start,end):return dict(path=path,symbol=symbol,startLine=start,endLine=end)
items_text=source('src/data/items.json');source_items=json.loads(items_text)['items']; constants=source('include/constants/items.h')
constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
def metadata(symbol):
 i=next(i for i in source_items if i['itemId']==symbol)
 match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
 r=ref('src/data/items.json',symbol,line,line)
 value=shared_item(symbol)
 return dict(item=value,description=i['description_english'].replace('\\n','\n'))
move_constants=source('include/constants/moves.h')
assert re.search(r'^#define\s+MOVE_ROCK_SMASH\s+249\b',move_constants,re.M)
battle_moves=source('src/data/battle_moves.h')
rock=re.search(r'\[MOVE_ROCK_SMASH\]\s*=\s*\{(.*?)\n\s*\}',battle_moves,re.S)
assert rock and re.search(r'\.pp\s*=\s*15\b',rock[1])
assert next(i for i in source_items if i['itemId']=='ITEM_HM06')['moveId']=='RockSmash'
items=[metadata(s) for s in ['ITEM_RUBY','ITEM_RAINBOW_PASS','ITEM_HM06','ITEM_DIRE_HIT','ITEM_FIRE_STONE']]
new=[]
for m in [items[0],items[1],items[3],items[4]]:
 i=m['item'];new.append(dict(item=i)|dict(description=m['description'], healHp=0, cureStatus=0, effectBytes=[], useSupport=10))
effect_path=ROOT/'content/raw/pokefirered/037335f4/src/data/pokemon/item_effects.h'
effects=effect_path.read_bytes()
# Independently pinned source assigns Dire Hit at byte0 and the shared friendship suffix.
effect_text=effects.decode(); dire=re.search(r'static const u8 sItemEffect_DireHit\[8\] = \{(.*?)\n\};',effect_text,re.S)
assert dire and '[0] = 2 << 4' in dire[1] and '[5] = ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID' in dire[1] and 'STAT_BOOST_FRIENDSHIP_CHANGE' in dire[1]
new[2]['effectBytes']=[32,0,0,0,0,96,1,1]
new[2]['effectSource']=dict(path='src/data/pokemon/item_effects.h',symbol='sItemEffect_DireHit',startLine=effect_text[:dire.start()].count('\n')+1,endLine=effect_text[:dire.end()].count('\n')+1)
fire=re.search(r'static const u8 sItemEffect_FireStone\[6\] = \{(.*?)\n\};',effect_text,re.S)
assert fire and '[4] = ITEM4_EVO_STONE' in fire[1]
new[3]['effectBytes']=[0,0,0,0,128,0]
new[3]['useSupport']=9
new[3]['effectSource']=dict(path='src/data/pokemon/item_effects.h',symbol='sItemEffect_FireStone',startLine=effect_text[:fire.start()].count('\n')+1,endLine=effect_text[:fire.end()].count('\n')+1)
additions=compatibility_additions(ROOT, 'content/generated/sevii-ruby-items/items.json', 'machineAdditions')
assert len(additions)==36
hm=admission_view(ROOT, 'content/generated/sevii-ruby-items/items.json', '/hm06')
assert len(hm['species'])==118
OUT.mkdir(parents=True,exist_ok=True)
document=dict(sourceCommit=COMMIT,profile='firered-sevii-ruby-items-v1',items=items,additions=new,machineAdditions=additions,hm06=hm)
output=(json.dumps(document,ensure_ascii=False,indent=2, default=item_json)+'\n').encode();(OUT/'items.json').write_bytes(output)
print(json.dumps(dict(),indent=2, default=item_json))
