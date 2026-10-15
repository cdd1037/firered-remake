#!/usr/bin/env python3
"""Import exact Five Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path
from machine_catalog import admission_view, compatibility_additions

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('TEAM_ROCKET_ADMIN','TEAM_ROCKET_ADMIN_2','SCIENTIST_GIDEON','TEAM_ROCKET_GRUNT_42','TEAM_ROCKET_GRUNT_47','TEAM_ROCKET_GRUNT_48','TEAM_ROCKET_GRUNT_49','TEAM_ROCKET_GRUNT_50','TEAM_ROCKET_GRUNT_51')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            inputs[path] = dict(path=path)
            raw[path] = data
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
    def encode(name):
        return [int(one("charmap.txt", "^'" + re.escape(c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def identity(symbol):
        return dict(value="pret:" + symbol)
    def ai_flags(symbols):
        return sum(1 << int(one("include/constants/battle_ai.h", r"^#define " + s + r"\s+\(1 << (\d+)\)[^\n]*")[1]) for s in symbols)
    def natural_moves(species, level):
        pointer = one("src/data/pokemon/level_up_learnset_pointers.h", r"^    \[" + species + r"\] = (\w+),$")[1]
        body = one("src/data/pokemon/level_up_learnsets.h", r"^static const u16 " + pointer + r"\[\] = \{\n(.*?)^\};")[1]
        moves = []
        for lv, move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)", body):
            if int(lv) <= level and move not in moves:
                moves = (moves + [move])[-4:]
        need(1 <= len(moves) <= 4, "Expected source natural moves")
        return moves

    trainers, resolved = [], []
    for short in TRAINERS:
        symbol, path = "TRAINER_" + short, "src/data/trainers.h"
        match = one(path, r"^    \[" + symbol + r"\] = \{\n(.*?)^    \},")
        fields = dict(re.findall(r"^        \.(\w+) = (.*),$", match[1], re.M))
        shape, party_symbol = re.fullmatch(r"((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)", fields["party"]).groups()
        custom, held = "CUSTOM" in shape, shape.startswith("ITEM_")
        flags = int(custom) | (int(held) << 1)
        party_path = "src/data/trainer_parties.h"
        party = one(party_path, r"^static const struct TrainerMon" + ("Item" if held else "NoItem") + ("Custom" if custom else "Default") + "Moves " + party_symbol + r"\[\] = \{\n(.*?)^\};")
        rows = list(re.finditer(r"    \{\n(.*?)    \},\n", party[1], re.S))
        need("".join(r[0] for r in rows) == party[1], "Unhandled trainer party syntax")
        members, movesets = [], []
        for row in rows:
            mf = dict(re.findall(r"^        \.(\w+) = (.*),$", row[1], re.M))
            need(set(mf) == {"iv", "lvl", "species"} | ({"heldItem"} if held else set()) | ({"moves"} if custom else set()), "Unexpected member fields")
            species, level = mf["species"], int(mf["lvl"])
            names_path = "src/data/text/species_names.h"
            name = one(names_path, r'^    \[' + species + r'\] = _\("([^"]+)"\),$')
            moves = mf["moves"].strip("{}").split(", ") if custom else natural_moves(species, level)
            members.append(dict(species=identity(species), sourceSpeciesId=number("include/constants/species.h", species),
                speciesName=name[1], encodedSpeciesName=encode(name[1]), level=level, sourceIv=int(mf["iv"]),
                heldItem=mf.get("heldItem", "ITEM_NONE"), customMoves=[identity(m) for m in moves] if custom else [],
                source=ref(party_path, party_symbol, party), speciesNameSource=ref(names_path, species, name)))
            movesets.append(dict(species=identity(species), level=level, moves=moves))
        name = re.fullmatch(r'_\("([^"]+)"\)', fields["trainerName"])[1]
        items = fields["items"].strip("{}").split(", ") if fields["items"] != "{}" else []
        items += ["ITEM_NONE"] * (4 - len(items))
        money = one("src/battle_main.c", r"^\s*\{" + fields["trainerClass"] + r", (\d+)\},$")
        ai = fields["aiFlags"].split(" | ")
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number("include/constants/opponents.h", symbol),
            trainerClass=fields["trainerClass"], sourceTrainerClass=number("include/constants/trainers.h", fields["trainerClass"]),
            trainerPic=fields["trainerPic"], sourceTrainerPic=number("include/constants/trainers.h", fields["trainerPic"]),
            sourceTrainerName=name, encodedTrainerName=encode(name),
            encounterMusicGender=sum(number("include/constants/trainers.h", v) if not v.isdigit() else int(v) for v in fields["encounterMusic_gender"].split(" | ")),
            items=items, doubleBattle=fields["doubleBattle"] == "TRUE", partyFlags=flags,
            aiFlags=ai_flags(ai), aiFlagSymbols=ai, baseMoney=int(money[1]), party=members,
            source=ref(path, symbol, match), moneySource=ref("src/battle_main.c", fields["trainerClass"], money)))
        resolved.append(dict(trainer=symbol, party=movesets))
    canonical_path = 'content/generated/canonical-pokemon/catalog.json'
    data = (dependency / canonical_path).read_bytes()
    canonical = json.loads(data)
    additions = ['SPECIES_HOPPIP','SPECIES_SKIPLOOM','SPECIES_JUMPLUFF']
    opponent_additions = ['SPECIES_HOUNDOUR','SPECIES_HOUNDOOM','SPECIES_PORYGON']
    species = {s['id']['value'][5:]:s for s in canonical['species']}
    metadata = {s['id']['value'][5:]:s for s in canonical['metadata']}
    edges = []
    for name in additions+opponent_additions:
        original = species[name]
        for sr in [original['source'],original['learnsetSource'],metadata[name]['learnsetPointerSource'],metadata[name]['dexSource']]+[e['source'] for e in metadata[name]['sourceEvolutions']]:
            read(sr['path'])
    for name,method,parameter,target in [('SPECIES_HOPPIP','EVO_LEVEL',18,'SPECIES_SKIPLOOM'),('SPECIES_SKIPLOOM','EVO_LEVEL',27,'SPECIES_JUMPLUFF')]:
        matches=[e for e in metadata[name]['sourceEvolutions'] if e['method']==method and e['parameterValue']==parameter and e['target']['value']=='pret:'+target]
        need(len(matches)==1,'Evolution edge: '+name)
        edges.append(dict(species=identity(name),sourceEvolution=matches[0]))
    battles=[]; actors=[]
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
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),
                rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',
                postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
                callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=args[3] if len(args)>3 else None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle)
            actors.append(dict(map=mapping['id'],localId=local_id,x=actor['x'],y=actor['y'],movementType=actor['movement_type'],sight=actor['trainer_sight_or_berry_tree_id'],script=label,trainer=identity(args[0])))
    need(len(trainers)==9 and len(battles)==9 and len(actors)==9 and sum(len(t['party']) for t in trainers)==26,'9 trainers/26 members/9 actors')
    need(not any(t['doubleBattle'] for t in trainers),'Nine original singles')
    identities=[]
    for trainer,sets in zip(trainers,resolved):
        h=0;members=[]
        for member,moves in zip(trainer['party'],sets['party']):
            h=(h+sum(trainer['encodedTrainerName'])+sum(member['encodedSpeciesName']))&0xffffffff
            pid=((0x80 if trainer['doubleBattle'] else 0x78 if trainer['encounterMusicGender']&128 else 0x88)+(h<<8))&0xffffffff
            members.append(dict(species=member['species'],level=member['level'],fixedIv=member['sourceIv']*31//255,personality=pid,heldItem=member['heldItem'],moves=[identity(m) for m in moves['moves']]))
        identities.append(dict(trainer=trainer['id'],members=members,basePrize=(8 if trainer['doubleBattle'] else 4)*trainer['baseMoney']*trainer['party'][-1]['level']))
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h']:read(path)
    # Counts are recomputed from an actual authenticated predecessor snapshot, never inferred from scope prose.
    baseline_path=Path(__file__).with_name('five-island-predecessor.json')
    baseline=json.loads(baseline_path.read_text())
    owned=set(baseline['ownedSpecies']); opponents=set(baseline['opponentSpecies'])
    need(len(owned)==132 and len(opponents)==153 and owned<=opponents,'Authenticated predecessor scope')
    new_owned=owned|{'pret:'+s for s in additions};new_opponents=opponents|new_owned|{'pret:'+s for s in opponent_additions}
    need(len(new_owned)==135 and len(new_opponents)==159,'Deduplicated exact successor scope')
    wild=json.loads(read('src/data/wild_encounters.json'))
    entries=[e for group in wild['wild_encounter_groups'] for e in group.get('encounters',[]) if e.get('map') in ['MAP_FIVE_ISLAND','MAP_FIVE_ISLAND_MEADOW'] and e.get('base_label','').endswith('_FireRed')]
    need(len(entries)==2,'Two original FireRed encounter entries')
    encounter_doc=dict(sourceCommit=COMMIT,profile='firered-five-island-encounters-v1',sourcePath='src/data/wild_encounters.json',slotTables=wild['wild_encounter_groups'][0]['fields'],encounters=entries)
    docs={
      'encounters.json':encounter_doc,
      'trainers.json':dict(sourceCommit=COMMIT,profile='firered-five-island-trainers-v1',completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-five-island-owned-pokemon-v1',ownedSpeciesAdded=additions,opponentSpeciesAdded=opponent_additions,evolutionEdges=edges),
      'rules.json':dict(sourceCommit=COMMIT,profile='firered-five-island-rules-v1',ownedSpeciesCount=len(new_owned),previousOwnedSpeciesCount=len(owned),opponentSpeciesCount=len(new_opponents),previousOpponentSpeciesCount=len(opponents),ownedSpeciesAdded=additions,opponentSpeciesAdded=opponent_additions,trainerSpecies=sorted({p['species']['value'] for t in trainers for p in t['party']}),trainerMoves=sorted({m for r in resolved for p in r['party'] for m in p['moves']}),identities=identities,trainerActors=actors,naturalLearnsets={s:species[s]['levelUpMoves'] for s in additions},limitations=['Source catalogs alone do not grant runtime route/capture/save/battle admission.','Vs Seeker/rematch teams are not admitted.','Qwilfish is retained only in source Super Rod declarations; no new rod or ownership admission.','Up-Grade held-item trade evolution remains explicitly unsupported.','Sapphire is the bespoke Gideon quest grant, not a ninth field item-ball event.']),
      'source-input-hashes.json':dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=[dict(path=canonical_path)])}
    return docs

def generate_items(reference,root):
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
 symbols=['ITEM_MAX_POTION','ITEM_PP_UP','ITEM_BIG_PEARL','ITEM_TM36','ITEM_PEARL','ITEM_UP_GRADE','ITEM_NEST_BALL','ITEM_NET_BALL','ITEM_SAPPHIRE']
 items=[metadata(s) for s in symbols]
 new=[]
 for m in [metadata('ITEM_BIG_PEARL'),metadata('ITEM_UP_GRADE'),metadata('ITEM_SAPPHIRE')]:
  i=m['item']
  new.append({k:i[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=m['description'],healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=i))
 additions=compatibility_additions(ROOT, 'content/generated/five-island-rules/items.json', 'machineAdditions')
 need(len(additions)==38, 'Exact admitted machine scope')
 tm36=admission_view(ROOT, 'content/generated/five-island-rules/items.json', '/tm36')
 need(len(tm36['species'])==135, 'Exact admitted TM36 species scope')
 dependencies.extend(dict(path=p) for p in ['scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json'])
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
 doc=dict(sourceCommit=COMMIT,profile='firered-five-island-items-v1',items=items,additions=new,machineAdditions=additions,tm36=tm36,pickups=pickups,questGrant=dict(script='FiveIsland_RocketWarehouse_EventScript_DefeatedGideon',flag='FLAG_RECOVERED_SAPPHIRE',item='ITEM_SAPPHIRE',quantity=1),unsupportedUses=[dict(item='ITEM_UP_GRADE',boundary='Held-item trade evolution is not implemented. No alternate trigger is invented.')])
 evidence=dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
 return doc,evidence

def generate_encounters(reference):
    inputs = {}
    def read(path):
        data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
        inputs[path] = dict(path=path)
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
            symbol = f',"{e["map"]}"' if field == "water_mons" else ""
            tables += f'        new(new("{map_id(e["map"])}"){symbol},EarlyLandEncounterRules.SourceCommit,EarlyLandEncounterRules.SourcePath,"{e["base_label"]}",{table["encounter_rate"]},[{slots}]),\n'
        tables += "    ];\n"
    tables += "    public static ImmutableArray<OldRodEncounterTable> OldRod { get; } = FishingSource.Select(t => t with { Slots = t.Slots.Take(2).ToImmutableArray() }).ToImmutableArray();\n}\n"
    combinations = sorted({(m["species"],lv) for e in encounters for field in ["land_mons","water_mons","fishing_mons"] for m in e.get(field,{}).get("mons",[]) for lv in range(m["min_level"],m["max_level"]+1)})
    pointers,learnsets,info,ids = [read(p) for p in ["src/data/pokemon/level_up_learnset_pointers.h","src/data/pokemon/level_up_learnsets.h","src/data/pokemon/species_info.h","include/constants/species.h"]]
    move_rows = []
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
        move_rows.append(dict(species=species,level=level,moves=moves))
        slots=",".join('new("pret:'+move+'")' for move in moves)
        source += f'        new(new("pret:{species}"),{level},[{slots}]),\n'
    source += "    ];\n    public static ImmutableArray<FiveIslandWildDependency> Species { get; } = [\n"
    dependencies=[]
    for species in sorted({s for s,l in combinations}):
        body=re.findall(r"^    \["+species+r"\] =\n    \{\n(.*?)^    \},",info,re.M|re.S)
        assert len(body)==1
        a,b=re.search(r"\.abilities = \{(ABILITY_\w+), (ABILITY_\w+)\}",body[0]).groups()
        common=re.search(r"\.itemCommon = (ITEM_\w+),",body[0])[1]
        rare=re.search(r"\.itemRare = (ITEM_\w+),",body[0])[1]
        number=int(re.search(r"^#define "+species+r"\s+(\d+)$",ids,re.M)[1])
        source += f'        new(new("pret:{species}"),{number},"{a}","{b}","{common}","{rare}"),\n'
        dependencies.append(dict(species=species,sourceSpeciesId=number,ability1=a,ability2=b,itemCommon=common,itemRare=rare))
    source += "    ];\n}\n"
    witness=dict(sourceCommit=COMMIT,encounters=encounters,initialMoves=move_rows,species=dependencies,inputs=[inputs[k] for k in sorted(inputs)])
    return {"src/Game.Domain/Encounters/FiveIslandEncounterTables.cs":tables,"src/Game.Domain/Encounters/FiveIslandWildSource.cs":source,"content/generated/five-island-rules/wild-source-witness.json":json.dumps(witness,indent=2)+"\n"}
def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency)
    items,evidence=generate_items(a.reference,a.dependency)
    docs['items.json']=items;docs['item-source-input-hashes.json']=evidence
    files={'content/generated/five-island-rules/'+n:(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode() for n,d in docs.items()}
    files.update({name:value.encode() for name,value in generate_encounters(a.reference).items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-five-island-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=9,battles=9,owned=docs['rules.json']['ownedSpeciesCount'],opponents=docs['rules.json']['opponentSpeciesCount']),indent=2))
if __name__=='__main__':main()
