#!/usr/bin/env python3
"""Import exact Five Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. Only live trainer, item and encounter outputs are emitted.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers, resolved_parties
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse
import json
import re
from pathlib import Path
from machine_catalog import admission_view, compatibility_additions

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('TEAM_ROCKET_ADMIN','TEAM_ROCKET_ADMIN_2','SCIENTIST_GIDEON','TEAM_ROCKET_GRUNT_42','TEAM_ROCKET_GRUNT_47','TEAM_ROCKET_GRUNT_48','TEAM_ROCKET_GRUNT_49','TEAM_ROCKET_GRUNT_50','TEAM_ROCKET_GRUNT_51')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency, raw_root=None):
    texts = {}
    def read(path):
        if path not in texts:
            data = (Path(raw_root) / path).read_bytes() if raw_root is not None else read_source(reference, path, COMMIT)
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)
    def number(path, symbol):
        value = one(path, r"^#define " + symbol + r"\s+([^\n]+)")[1].split("//")[0].strip()
        return int(value, 0) if re.fullmatch(r"(?:0x[0-9a-fA-F]+|\d+)", value) else 1 << int(re.fullmatch(r"\(1 << (\d+)\)", value)[1])
    def identity(symbol):
        return dict(value="pret:" + symbol)
    trainers = selected_trainers(read, ["TRAINER_" + short for short in TRAINERS])
    resolved = resolved_parties(read, trainers)
    battles=[]
    for name in ['FiveIsland_Meadow','FiveIsland_RocketWarehouse']:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for local_id,actor in enumerate(mapping['object_events'],1):
            if actor['trainer_type']!='TRAINER_TYPE_NORMAL': continue
            label=actor['script'];path='data/maps/'+name+'/scripts.inc'
            block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            command=re.search(r'^\t(trainerbattle_single|trainerbattle_double) ([^\n]+)$',block[1],re.M)
            need(command is not None,'First-battle command: '+label)
            args=command[2].split(', ');double=command[1]=='trainerbattle_double'
            need(args[0][8:] in TRAINERS,'Source actor trainer scope')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>4 and args[4]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>3 else 'TRAINER_BATTLE_SINGLE'),
                rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',
                postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
                callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=args[3] if len(args)>3 else None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle)
    need(len(trainers)==9 and len(battles)==9 and sum(len(t['party']) for t in trainers)==26,'9 trainers/26 members/9 battles')
    need(not any(t['doubleBattle'] for t in trainers),'Nine original singles')
    return {'trainers.json': dict(completeSourceRoster=False, trainers=trainers,
                                 battles=battles, resolvedMoves=resolved)}

def generate_items(reference,root):
 texts={}
 def source(path):
  if path not in texts:
   b=read_source(reference, path, COMMIT)
   texts[path]=b.decode()
  return texts[path]
 def ref(path,symbol,start,end):return dict(path=path,symbol=symbol,startLine=start,endLine=end)
 items_text=source('src/data/items.json');source_items=json.loads(items_text)['items']
 constants=source('include/constants/items.h');constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
 def metadata(symbol):
  i=next(i for i in source_items if i['itemId']==symbol)
  match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
  r=ref('src/data/items.json',symbol,line,line)
  value=shared_item(symbol)
  return dict(item=value,description=i['description_english'].replace('\\n','\n'))
 symbols=['ITEM_MAX_POTION','ITEM_PP_UP','ITEM_BIG_PEARL','ITEM_TM36','ITEM_PEARL','ITEM_UP_GRADE','ITEM_NEST_BALL','ITEM_NET_BALL','ITEM_SAPPHIRE']
 items=[metadata(s) for s in symbols]
 new=[]
 for m in [metadata('ITEM_BIG_PEARL'),metadata('ITEM_UP_GRADE'),metadata('ITEM_SAPPHIRE')]:
  i=m['item']
  new.append(dict(item=i)|dict(description=m['description'], healHp=0, cureStatus=0, effectBytes=[], useSupport=10))
 additions=compatibility_additions(ROOT, 'content/generated/data/regions/five-island/items.json', 'machineAdditions')
 tm36=admission_view(ROOT, 'content/generated/data/regions/five-island/items.json', '/tm36')
 pickups=[];flags=source('include/constants/flags.h')
 def pickup(map_symbol,event,hidden,local_id,item,quantity):
  flag=event['flag']
  def flag_value(symbol):
   m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Pickup flag')
   expr=m[1].split('//')[0].strip()
   if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
   add=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(\d+)\)',expr);need(add,'Unsupported flag syntax')
   return flag_value(add[1])+int(add[2])
  info=metadata(item)['item'];need(quantity==1 and (not hidden or event['underfoot']==False),'Pickup bounds')
  pickups.append(dict(id='five-island:'+flag,mapSymbol=map_symbol,hidden=hidden,sourceLocalId=local_id,x=event['x'],y=event['y'],elevation=event['elevation'],script='EventScript_HiddenItemScript' if hidden else event['script'],itemSymbol=item,sourceItemId=info['sourceItemId'],item=info['id'],itemName=info['name'],pocket=info['pocket'],quantity=quantity,flag=flag,sourceFlagId=flag_value(flag)))
 for name in ['FiveIsland','FiveIsland_Meadow','FiveIsland_RocketWarehouse']:
  mapping=json.loads(source('data/maps/'+name+'/map.json'));scripts=source('data/scripts/item_ball_scripts.inc')
  for local_id,event in enumerate(mapping['object_events'],1):
   if event['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   m=re.search(r'^'+event['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',scripts,re.M);need(m,'Pickup recipe')
   pickup(mapping['id'],event,False,local_id,m[1],int(m[2] or 1))
  for event in mapping['bg_events']:
   if event['type']=='hidden_item':pickup(mapping['id'],event,True,None,event['item'],event['quantity'])
 need(len(pickups)==8 and sum(p['hidden'] for p in pickups)==2,'Six visible and two hidden pickups')
 for path in ['data/scripts/obtain_item.inc','src/pokemon.c','src/battle_util.c','src/battle_script_commands.c','include/constants/hold_effects.h']:source(path)
 doc=dict(items=items,additions=new,machineAdditions=additions,tm36=tm36,pickups=pickups,questGrant=dict(script='FiveIsland_RocketWarehouse_EventScript_DefeatedGideon',flag='FLAG_RECOVERED_SAPPHIRE',item='ITEM_SAPPHIRE',quantity=1),unsupportedUses=[dict(item='ITEM_UP_GRADE',boundary='Held-item trade evolution is not implemented. No alternate trigger is invented.')])
 return doc,None

def generate_encounters(reference):
    def read(path):
        data = read_source(reference, path, COMMIT)
        return data.decode()
    raw = read("src/data/wild_encounters.json")
    encounters = [e for group in json.loads(raw)["wild_encounter_groups"] for e in group["encounters"] if e["map"] in ["MAP_FIVE_ISLAND_MEADOW", "MAP_FIVE_ISLAND"] and e["base_label"].endswith("_FireRed")]
    assert len(encounters) == 2
    encounters.sort(key=lambda e: ["MAP_FIVE_ISLAND_MEADOW", "MAP_FIVE_ISLAND"].index(e["map"]))
    def map_id(symbol): return "map." + symbol[4:].lower().replace("_", "-")
    tables = "using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\n/// <summary>Exact pinned FireRed Five Island tables. Rod subdivisions retain their source indexes.</summary>\npublic static class FiveIslandEncounterTables\n{\n"
    for field, prop, typ, weights in [("land_mons","Land","EarlyLandEncounterTable",[20,20,10,10,10,10,5,5,4,4,1,1]),("water_mons","Water","WaterEncounterTable",[60,30,5,4,1]),("fishing_mons","FishingSource","OldRodEncounterTable",[70,30,60,20,20,40,40,15,4,1])]:
        tables += f"    public static ImmutableArray<{typ}> {prop} {{ get; }} = [\n"
        for e in encounters:
            if field not in e: continue
            table = e[field]
            slots = ",".join(f'new({i},new("pret:{m["species"]}"),{m["min_level"]},{m["max_level"]},{weights[i]})' for i,m in enumerate(table["mons"]))
            tables += f'        new(new("{map_id(e["map"])}"),{table["encounter_rate"]},[{slots}]),\n'
        tables += "    ];\n"
    tables += "    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = FishingSource.Select(t => t with { Slots = t.Slots.Take(2).ToImmutableArray() }).ToImmutableArray();\n}\n"
    combinations = sorted({(m["species"],lv) for e in encounters for field in ["land_mons","water_mons","fishing_mons"] for m in e.get(field,{}).get("mons",[]) for lv in range(m["min_level"],m["max_level"]+1)})
    pointers,learnsets,info,ids = [read(p) for p in ["src/data/pokemon/level_up_learnset_pointers.h","src/data/pokemon/level_up_learnsets.h","src/data/pokemon/species_info.h","include/constants/species.h"]]
    source = "using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record FiveIslandInitialMoves(SpeciesId Species, byte Level, ImmutableArray<MoveId> Moves);\npublic sealed record FiveIslandWildDependency(SpeciesId Species, int SourceSpeciesId, string Ability1, string Ability2, string ItemCommon, string ItemRare);\n\npublic static class FiveIslandWildSource\n{\n    public static ImmutableArray<FiveIslandInitialMoves> InitialMoves { get; } = [\n"
    for species,level in combinations:
        pointer = re.findall(r"^    \["+species+r"\] = (\w+),$",pointers,re.M)
        assert len(pointer)==1
        body = re.findall(r"^static const u16 "+pointer[0]+r"\[\] = \{\n(.*?)^\};",learnsets,re.M|re.S)
        assert len(body)==1
        moves=[]
        for lv,move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)",body[0]):
            if int(lv)<=level and move not in moves: moves=(moves+[move])[-4:]
        assert 1<=len(moves)<=4
        slots=",".join('new("pret:'+move+'")' for move in moves)
        source += f'        new(new("pret:{species}"),{level},[{slots}]),\n'
    source += "    ];\n    public static ImmutableArray<FiveIslandWildDependency> Species { get; } = [\n"
    for species in sorted({s for s,l in combinations}):
        body=re.findall(r"^    \["+species+r"\] =\n    \{\n(.*?)^    \},",info,re.M|re.S)
        assert len(body)==1
        a,b=re.search(r"\.abilities = \{(ABILITY_\w+), (ABILITY_\w+)\}",body[0]).groups()
        common=re.search(r"\.itemCommon = (ITEM_\w+),",body[0])[1]
        rare=re.search(r"\.itemRare = (ITEM_\w+),",body[0])[1]
        number=int(re.search(r"^#define "+species+r"\s+(\d+)$",ids,re.M)[1])
        source += f'        new(new("pret:{species}"),{number},"{a}","{b}","{common}","{rare}"),\n'
    source += "    ];\n}\n"
    return {"src/Game.Domain/Encounters/FiveIslandEncounterTables.cs":tables,"src/Game.Domain/Encounters/FiveIslandWildSource.cs":source}
def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency)
    items,_=generate_items(a.reference,a.dependency)
    docs['items.json']=items
    files={'content/generated/data/regions/five-island/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False, default=item_json)+'\n').encode()) for n,d in docs.items()}
    files.update({name:value.encode() for name,value in generate_encounters(a.reference).items()})
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=9,battles=9),indent=2, default=item_json))
if __name__=='__main__':main()
