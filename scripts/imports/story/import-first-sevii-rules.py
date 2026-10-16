"""Test fixture conversion from source preparation; does not execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from item_catalog import item_definition as shared_item, inventory_item, item_json
from shared_constants import read_values
from field_item_services import service_script_graph
from map_runtime import generated_root
import argparse,json,re,sys,importlib.util
from pathlib import Path
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('paths',nargs='*',type=Path,help='Legacy Git mode: reference preparation output')
parser.add_argument('--raw-root',type=Path,help='Offline pinned raw cache')
parser.add_argument('--preparation',type=Path,help='Generated first-Sevii map package')
parser.add_argument('--out',type=Path,help='Generated first-Sevii rule directory')
parser.add_argument('--check',action='store_true')
args=parser.parse_args();check=args.check
if args.raw_root is not None:
 if args.paths or args.preparation is None or args.out is None:parser.error('Raw mode requires --preparation and --out, with no positional paths')
 reference=None;preparation=args.preparation;out=args.out
else:
 if len(args.paths)!=3 or args.preparation is not None or args.out is not None:parser.error('Provide reference preparation output, or --raw-root --preparation --out')
 reference,preparation,out=args.paths
generated=generated_root(out)
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def importer(name, path):
 spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
reader=importer('first_sevii_rule_source',(_PROJECT_ROOT / 'scripts/imports/source/import-cinnabar-source-preparation.py'))
prep=importer('first_sevii_rule_declarations',(_PROJECT_ROOT / 'scripts/lib/prepare_first_sevii.py'))
source=reader.Source(reference,raw_root=args.raw_root)
worlds=[dict(events=source.json('data/maps/'+name+'/map.json')) for name in prep.CORE+prep.SUPPORT]
script_index=prep.script_index(source,reader)
canonical=json.loads((_PROJECT_ROOT/'content/generated/data/pokemon/canonical/catalog.json').read_text())
decl=prep.declarations(source,script_index,worlds,canonical)
prep.item_declarations(source,script_index,worlds,decl)
# Preserve the deterministic field ordering of the previous importer handoff.
decl=json.loads(json.dumps(decl,sort_keys=True, default=item_json))
def write(name,content):
 target=out/name
 if check:assert target.read_text()==content,'Regeneration differs: '+str(target)
 else:target.write_text(content)

text={}
def read(p):
 if p not in text:
  text[p]=source.text(p)
 return text[p]
def one(p,pattern):
 matches=list(re.finditer(pattern,read(p),re.M|re.S))
 if len(matches)!=1:raise ValueError((p,pattern,len(matches)))
 return matches[0]
def ref(p,s,m):return dict(path=p,symbol=s,startLine=read(p).count('\n',0,m.start())+1,endLine=read(p).count('\n',0,m.end())+1)
def num(p,s):return int(one(p,r'^#define '+s+r'\s+(\d+)\b[^\n]*')[1])
def encode(s):return [int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in s]
def expr(s):
 n=0
 for v in s.split(' | '):
  p='include/constants/battle_ai.h' if v.startswith('AI_SCRIPT_') else 'include/constants/trainers.h'
  if v.startswith('AI_SCRIPT_') or v=='F_TRAINER_FEMALE':n|=1<<int(one(p,r'^#define '+v+r'\s+\(1 << (\d+)\)[^\n]*')[1])
  else:n|=num(p,v)
 return n
def identity(s):return dict(value='pret:'+s)
trainers=[];battles=[];resolved=[]
for d in decl['trainers']:
 for r in (d['sourceRef'],d['partySource']):
  read(r['path'])
 symbol=d['trainer'];p='src/data/trainers.h';m=one(p,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},');f=d['fields'];name=re.fullmatch(r'_\("([A-Z.& ]+)"\)',f['trainerName'])[1]
 shape,partySymbol=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',f['party']).groups();custom=shape=='NO_ITEM_CUSTOM_MOVES'
 pp='src/data/trainer_parties.h';party=one(pp,r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+partySymbol+r'\[\] = \{\n(.*?)^\};')
 rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]));assert ''.join(r[0] for r in rows)==party[1]
 mons=[]
 for row in rows:
  iv,lv,sp=int(row[1]),int(row[2]),row[3];np='src/data/text/species_names.h';nm=one(np,r'^    \['+sp+r'\] = _\("([^"]+)"\),$');sn=nm[1]
  mons.append(dict(species=identity(sp),sourceSpeciesId=num('include/constants/species.h',sp),speciesName=sn,encodedSpeciesName=encode(sn),level=lv,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[identity(x) for x in row[4].split(', ')] if custom else [],source=ref(pp,partySymbol,party),speciesNameSource=ref(np,sp,nm)))
 items=[x for x in f['items'].strip('{}').split(', ') if x];items+=['ITEM_NONE']*(4-len(items));money=one('src/battle_main.c',r'^\s*\{'+f['trainerClass']+r', (\d+)\},$')
 trainers.append(dict(id=identity(symbol),sourceTrainerId=num('include/constants/opponents.h',symbol),trainerClass=f['trainerClass'],sourceTrainerClass=num('include/constants/trainers.h',f['trainerClass']),trainerPic=f['trainerPic'],sourceTrainerPic=num('include/constants/trainers.h',f['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=expr(f['encounterMusic_gender']),items=items,doubleBattle=f['doubleBattle']=='TRUE',partyFlags=1 if custom else 0,aiFlags=expr(f['aiFlags']),aiFlagSymbols=f['aiFlags'].split(' | '),baseMoney=int(money[1]),party=mons,source=ref(p,symbol,m),moneySource=ref('src/battle_main.c',f['trainerClass'],money)))
 resolved.append(dict(trainer=symbol,party=[dict(species=x['species'],level=x['level'],moves=x['resolvedMoves']) for x in d['party']]))
for d in decl['battleOccurrences']:
 if d['rematch']:continue
 p=d['sourceRef']['path'];read(p);assert read(p).splitlines()[d['sourceRef']['line']-1].strip()==d['opcode']+' '+', '.join(d['arguments']);label=one(p,r'^'+d['script']+r'::\n((?:(?!^\w+::).)*)');args=d['arguments'][1:];noIntro=d['opcode']=='trainerbattle_no_intro';double=d['opcode']=='trainerbattle_double'
 mode='TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'
 post=re.search(r'^\tmsgbox (\w+)',label[1],re.M)
 row=dict(trainer=identity(d['trainer']),mode=num('include/constants/battle_setup.h',mode),rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=d['script'],introText='' if noIntro else args[0],defeatText=args[0] if noIntro else args[1],victoryText='',postBattleText='' if noIntro else post[1] if post else '',entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=dict(path=p,symbol=d['script'],startLine=d['sourceRef']['line'],endLine=d['sourceRef']['line']),postBattleScript=args[2] if not double and len(args)>2 else None,musicOption=args[3] if not double and len(args)>3 else None)
 if double:row['notEnoughMonsText']=args[2]
 battles.append(row)
assert len(trainers)==10 and sum(len(t['party']) for t in trainers)==21 and len(battles)==11
out.mkdir(parents=True,exist_ok=True)
document=dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved)
write('trainers.json',trainer_json(generated, document, check=check).decode())
print('Converted10 trainers/21 members/11 admitted source occurrences')
contracts=json.loads((preparation/'contracts.json').read_text()); constants=read_values(_PROJECT_ROOT / 'content/generated')
items=[]; additions=[]; stocks=[];pickups=[]
for item in decl['itemDeclarations']:
 symbol=item['itemId'];p='src/data/items.json';m=one(p,r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}')
 assert json.loads(m[0])==item
 r=ref(p,symbol,m)
 metadata=shared_item(symbol)
 description=item['description_english'].replace('\\n','\n')
 items.append(dict(item=metadata,description=description))
 if symbol in ['ITEM_ORAN_BERRY','ITEM_BLUK_BERRY','ITEM_FRESH_WATER','ITEM_SODA_POP','ITEM_MAX_ELIXIR','ITEM_RAZZ_BERRY','ITEM_PINAP_BERRY','ITEM_METEORITE','ITEM_TRI_PASS','ITEM_IAPAPA_BERRY']:
  effect_bytes=[]; effect_source=None; heal_hp=0; use_support=10
  if symbol=='ITEM_MAX_ELIXIR':
   ep='src/data/pokemon/item_effects.h'; em=one(ep,r'^static const u8 sItemEffect_MaxElixir\[7\] = \{\n(.*?)^\};')
   assert em[1]=='    [4] = ITEM4_HEAL_PP_ALL,\n    [6] = ITEM6_HEAL_PP_FULL,\n'
   cp='include/constants/item_effects.h'
   effect_bytes=[0,0,0,0,int(one(cp,r'^#define ITEM4_HEAL_PP_ALL\s+(0x[0-9A-Fa-f]+)\b[^\n]*')[1],16),0,int(one(cp,r'^#define ITEM6_HEAL_PP_FULL\s+(0x[0-9A-Fa-f]+)\b[^\n]*')[1],16)]
   effect_source=ref(ep,'sItemEffect_MaxElixir',em);use_support=12
   assert item['fieldUseFunc']=='FieldUseFunc_Ether' and item['battleUsage']==1
  elif symbol in ['ITEM_FRESH_WATER','ITEM_SODA_POP','ITEM_ORAN_BERRY']:
   effect_symbol={'ITEM_FRESH_WATER':'sItemEffect_FreshWater','ITEM_SODA_POP':'sItemEffect_SodaPop','ITEM_ORAN_BERRY':'sItemEffect_OranBerry'}[symbol]
   ep='src/data/pokemon/item_effects.h'; em=one(ep,r'^static const u8 '+effect_symbol+r'\[7\] = \{\n(.*?)^\};')
   amount=re.fullmatch(r'    \[4\] = ITEM4_HEAL_HP,\n    \[6\] = (\d+), // Amount of HP to recover\n',em[1]);assert amount
   heal_hp=int(amount[1]);use_support=1
   cp='include/constants/item_effects.h'; hp_flag=int(one(cp,r'^#define ITEM4_HEAL_HP\s+(0x[0-9A-Fa-f]+)\b[^\n]*')[1],16)
   effect_bytes=[0,0,0,0,hp_flag,0,heal_hp];effect_source=ref(ep,effect_symbol,em)
   assert item['fieldUseFunc']=='FieldUseFunc_Medicine' and item['holdEffectParam']==heal_hp and item['battleUsage']==1
  else:assert item['fieldUseFunc']=='FieldUseFunc_OakStopsYou' and item['battleUsage']==0
  additions.append(inventory_item(shared_item(symbol), description, heal_hp, 0, effect_bytes, use_support))
for stock in decl['shopStocks']:
 p=stock['sourceRef']['path']; m=one(p,r'^'+stock['label']+r'::\s*\n((?:\s*\.2byte ITEM_\w+\n)+)');ss=re.findall(r'\.2byte (ITEM_\w+)',m[1]);assert ss==stock['items']+['ITEM_NONE']
 stocks.append(dict(id=stock['label'],items=[identity(s) for s in stock['items']]))
lookup={i['item']['sourceItemSymbol']:i['item'] for i in items}
for hidden,key in [(False,'itemPickups'),(True,'hiddenItems')]:
 for i in contracts[key]:
  e=i['event'];symbol=e['item'] if hidden else re.fullmatch(r'\s*finditem (ITEM_\w+)',i['script']['lines'][0]['text'])[1];item=lookup[symbol]
  pickups.append(dict(id=e['flag'],mapSymbol=i['map'],hidden=hidden,sourceLocalId=None if hidden else i['localId'],x=e['x'],y=e['y'],elevation=e['elevation'],script='' if hidden else e['script'],itemSymbol=symbol,sourceItemId=item['sourceItemId'],item=item['id'],itemName=item['name'],pocket=item['pocket'],quantity=e['quantity'] if hidden else 1,flag=e['flag'],sourceFlagId=constants[e['flag']]))
p='src/renewable_hidden_items.c';renew=[]
for map in ['MAP_THREE_ISLAND_BOND_BRIDGE','MAP_THREE_ISLAND_BERRY_FOREST']:
 m=one(p,r'    \{\n        \.mapGroup = MAP_GROUP\('+map+r'\),\n(.*?)^    \},')
 groups={}
 for k in ['rare','uncommon','common']:
  group=re.search(r'\.'+k+r' = \{(.*?)\}',m[1],re.S);tokens=re.findall(r'HIDDEN_ID\((FLAG_\w+)\)|\b(NO_ITEM)\b',group[1]);assert len(tokens)==8;groups[k]=[a if a else None for a,b in tokens]
 renew.append(dict(mapSymbol=map,**groups,source=ref(p,'sRenewableHiddenItems',m)))
service_bindings=[]
catalog=json.loads((preparation/'catalog.json').read_text())
for name,root in [('ThreeIsland_DunsparceTunnel','ThreeIsland_DunsparceTunnel_EventScript_Prospector')]:
 map_entry=next(m for m in catalog['maps'] if m['sourceName']==name)
 events=source.json('data/maps/'+name+'/map.json')['object_events']
 matches=[(index,event) for index,event in enumerate(events) if event['script']==root]
 assert len(matches)==1,'Expected one source actor for '+root
 index,event=matches[0];graph=service_script_graph(script_index,root)
 assert graph['itemSymbols'] and all(symbol in lookup for symbol in graph['itemSymbols'])
 service_bindings.append(dict(id='first-sevii:'+root,mapId=map_entry['stableId'],scriptId=root,
  sourceLocalId=index+1,backgroundX=None,backgroundY=None,eventGroup='object_events',eventIndex=index,
  sourceEvent=event,programIds=graph['programIds'],itemSymbols=graph['itemSymbols']))
write('services.json',json.dumps(dict(serviceBindings=service_bindings),indent=2)+'\n')
itemsdoc=dict(items=items,additions=additions,shops=stocks,pickups=pickups,renewableGroups=renew,renewableStepThreshold=1500)
write('items.json',json.dumps(itemsdoc,indent=2,ensure_ascii=False, default=item_json)+'\n')
print(f'Converted {len(items)} items/{len(additions)} additions/{len(stocks)} stocks/{sum(not p["hidden"] for p in pickups)} visible/{sum(p["hidden"] for p in pickups)} hidden; renewable source metadata retained')

for source_file in ['src/data/wild_encounters.json','src/data/pokemon/species_info.h','src/data/pokemon/level_up_learnsets.h','src/battle_setup.c']:
 read(source_file)
encounters=dict(
 admitted=decl['wildEncounters'],scriptedHypno=decl['scriptedHypno'])
write('encounters.json',json.dumps(encounters,indent=2, default=item_json)+'\n')
