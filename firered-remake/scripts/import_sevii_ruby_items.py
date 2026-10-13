#!/usr/bin/env python3
"""Derive Ruby keys and HM06/118-species teaching from pinned local source only."""
import hashlib, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
RAW=ROOT/'content/raw/sevii-ruby-region'
OUT=ROOT/'content/generated/sevii-ruby-items'
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK='a4db62be71d3a82b03bbdb3a4748c42a036415a98be0a349c54160aa565ca523'
lockpath=ROOT/'content/sources/sevii-ruby-region.lock.json'
assert hashlib.sha256(lockpath.read_bytes()).hexdigest()==LOCK
files={r['path']:r for r in json.loads(lockpath.read_text())['files']}
def source(path):
 b=(RAW/path).read_bytes();assert hashlib.sha256(b).hexdigest()==files[path]['sha256'];return b.decode()
def ref(path,symbol,start,end):return dict(path=path,symbol=symbol,startLine=start,endLine=end,sha256=files[path]['sha256'])
def digest(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()).hexdigest()
items_text=source('src/data/items.json');source_items=json.loads(items_text)['items']; constants=source('include/constants/items.h')
constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
def metadata(symbol):
 i=next(i for i in source_items if i['itemId']==symbol)
 match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
 r=ref('src/data/items.json',symbol,line,line)
 value=dict(id={'value':'pret:'+symbol},sourceItemSymbol=symbol,sourceItemId=constant[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
 return dict(item=value,description=i['description_english'].replace('\\n','\n'),declarationDescription=i['description_english'],descriptionSource=r)
move_constants=source('include/constants/moves.h')
assert re.search(r'^#define\s+MOVE_ROCK_SMASH\s+249\b',move_constants,re.M)
battle_moves=source('src/data/battle_moves.h')
rock=re.search(r'\[MOVE_ROCK_SMASH\]\s*=\s*\{(.*?)\n\s*\}',battle_moves,re.S)
assert rock and re.search(r'\.pp\s*=\s*15\b',rock[1])
assert next(i for i in source_items if i['itemId']=='ITEM_HM06')['moveId']=='RockSmash'
items=[metadata(s) for s in ['ITEM_RUBY','ITEM_RAINBOW_PASS','ITEM_HM06','ITEM_DIRE_HIT','ITEM_FIRE_STONE']]
new=[]
for m in [items[0],items[1],items[3],items[4]]:
 i=m['item'];new.append({k:i[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=m['description'],healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=i))
effect_path=ROOT/'content/raw/victory-road-rules/src/data/pokemon/item_effects.h'
effects=effect_path.read_bytes()
effects_hash='d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25'
assert hashlib.sha256(effects).hexdigest()==effects_hash
# Independently pinned source assigns Dire Hit at byte0 and the shared friendship suffix.
effect_text=effects.decode(); dire=re.search(r'static const u8 sItemEffect_DireHit\[8\] = \{(.*?)\n\};',effect_text,re.S)
assert dire and '[0] = 2 << 4' in dire[1] and '[5] = ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID' in dire[1] and 'STAT_BOOST_FRIENDSHIP_CHANGE' in dire[1]
new[2]['effectBytes']=[32,0,0,0,0,96,1,1]
new[2]['effectSource']=dict(path='src/data/pokemon/item_effects.h',symbol='sItemEffect_DireHit',startLine=effect_text[:dire.start()].count('\n')+1,endLine=effect_text[:dire.end()].count('\n')+1,sha256=effects_hash)
fire=re.search(r'static const u8 sItemEffect_FireStone\[6\] = \{(.*?)\n\};',effect_text,re.S)
assert fire and '[4] = ITEM4_EVO_STONE' in fire[1]
new[3]['effectBytes']=[0,0,0,0,128,0]
new[3]['useSupport']=9
new[3]['effectSource']=dict(path='src/data/pokemon/item_effects.h',symbol='sItemEffect_FireStone',startLine=effect_text[:fire.start()].count('\n')+1,endLine=effect_text[:fire.end()].count('\n')+1,sha256=effects_hash)
# The authenticated Cinnabar derivative enumerates the exact inherited 115 species.
previous_path=ROOT/'content/generated/cinnabar-items/machines.json'
previous_bytes=previous_path.read_bytes()
assert hashlib.sha256(previous_bytes).hexdigest()=='5ddf581260c05519aeb8f8e5fc45a556f973aee1b3e769915a6c8f17a6e17d72'
previous=json.loads(previous_bytes)
# Exactly the 36 machines in the National Dex predecessor.
old_ids=[i['value'][5:] for i in previous['inheritedItemIds']]+['ITEM_TM14','ITEM_TM22','ITEM_TM38','ITEM_TM26','ITEM_TM02','ITEM_TM07','ITEM_TM37','ITEM_TM50']
assert len(old_ids)==36 and len(set(old_ids))==36
owned=[(r['species']['value'][5:],r['sourceSpeciesId']) for r in previous['newMachines'][0]['compatibility']]+[('SPECIES_RAPIDASH',78),('SPECIES_SLUGMA',218),('SPECIES_MAGCARGO',219)]
assert len(owned)==118 and len(set(owned))==118
text=source('src/data/pokemon/tmhm_learnsets.h');matches={m[1]:m for m in re.finditer(r'\[(SPECIES_\w+)\]\s*=\s*TMHM_LEARNSET\((.*?)\),',text,re.S)}
def cell(species,source_id,item):
 m=matches[species];start=text[:m.start()].count('\n')+1;end=text[:m.end()].count('\n')+1
 return dict(species={'value':'pret:'+species},sourceSpeciesId=source_id,canLearn=bool(re.search(r'TMHM\('+item[5:]+r'_',m[2])),source=ref('src/data/pokemon/tmhm_learnsets.h',species,start,end))
additions=[dict(item={'value':'pret:'+item},compatibility=[cell(s,n,item) for s,n in owned[-3:]]) for item in sorted(old_ids)]
hm=dict(machine=items[2],move={'value':'pret:MOVE_ROCK_SMASH'},sourceMoveId=249,movePp=15,compatibility=[cell(s,n,'ITEM_HM06') for s,n in sorted(owned,key=lambda p:p[1])])
OUT.mkdir(parents=True,exist_ok=True)
document=dict(sourceCommit=COMMIT,profile='firered-sevii-ruby-items-v1',items=items,additions=new,machineAdditions=additions,hm06=hm)
output=(json.dumps(document,ensure_ascii=False,indent=2)+'\n').encode();(OUT/'items.json').write_bytes(output)
print(json.dumps(dict(itemsSha256=hashlib.sha256(output).hexdigest(),shopFingerprint=digest(new),inheritedMachineFingerprint=digest(additions),hm06Fingerprint=digest(hm)),indent=2))
