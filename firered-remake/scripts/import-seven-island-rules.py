#!/usr/bin/env python3
"""Import exact Seven Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
TRAINERS = ('CRUSH_GIRL_CYNDY', 'TAMER_EVAN', 'PKMN_RANGER_JACKSON', 'PKMN_RANGER_KATELYN', 'COOLTRAINER_LEROY', 'COOLTRAINER_MICHELLE', 'COOL_COUPLE_LEX_NYA', 'AROMA_LADY_MIAH', 'YOUNG_COUPLE_EVE_JON', 'PKMN_RANGER_NICOLAS', 'PKMN_RANGER_MADELINE', 'JUGGLER_MASON', 'RUIN_MANIAC_BRANDON', 'RUIN_MANIAC_BENJAMIN', 'PAINTER_EDNA', 'GENTLEMAN_CLIFFORD', 'PSYCHIC_DARIO', 'PSYCHIC_RODETTE')

def sha(data):
    return hashlib.sha256(data).hexdigest()

def fingerprint(value):
    return sha(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode())

def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs, raw = {}, {}, {}
    need(subprocess.check_output(["git", "rev-parse", COMMIT + "^{tree}"], cwd=reference).decode().strip() == TREE, "Pinned source tree")
    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            inputs[path] = dict(path=path, byteLength=len(data), gitBlobSha1=hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest(), sha256=sha(data))
            raw[path] = data
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1, sha256=inputs[path]["sha256"])
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
    dependencies=[]
    def dependent(path, expected):
        data=(dependency/path).read_bytes();need(sha(data)==expected,'Dependency changed: '+path)
        dependencies.append(dict(path=path,byteLength=len(data),sha256=sha(data)));return json.loads(data)
    canonical=dependent('content/generated/canonical-pokemon/catalog.json','c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc')
    five=dependent('content/generated/five-island-rules/items.json','c45233322a3b0bd5c2efe55063ec6066907a6e1a24ff0cfe3088a7b33d4d3968')
    base=dependent('scripts/five-island-predecessor.json','e2241d337388ef781209563eed0fa100c5d219841830ff3f400b71500e37278f')
    five_p=dependent('content/generated/five-island-rules/pokemon.json','9788f567d061f21e66d877718a542a3fb2068629f34b65da9dd604e387a0a909')
    celio=dependent('content/generated/celio-pokemon/pokemon.json','3f5b6aeeb5454af27c38ff837757fe9f50647a2ed95dc19ea94e83dc05c54e9f')
    cave=dependent('content/generated/cerulean-cave-pokemon/pokemon.json','55b3b1b8f593c72d71f92a43e7d599cf0bd278caad2e636b969b07b125330a95')
    old_machines=dependent('content/generated/cerulean-cave-pokemon/machines.json','a0b1fd8623e3a07d32b95746a2c2a2bb3340c0c8d70e62e6925e8cd0c071e172')
    rematch=dependent('content/generated/league-rematch-rules/pokemon.json','d9e9657f331699b88a9909d1bbb8dce05797ee0714e4c980730884aab4725b39')
    species={s['id']['value'][5:]:s for s in canonical['species']}
    metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
    previous_owned={r['species']['value'][5:] for r in five['tm36']['compatibility']}|set(celio['speciesAdded'])|set(cave['speciesAdded'])
    previous_opponents={s[5:] for s in base['opponentSpecies']}|previous_owned|set(five_p['opponentSpeciesAdded'])|{s['id']['value'][5:] for s in rematch['opponentSpecies']}
    additions=['SPECIES_PHANPY','SPECIES_DONPHAN','SPECIES_LARVITAR','SPECIES_PUPITAR','SPECIES_TYRANITAR','SPECIES_SKARMORY','SPECIES_UNOWN']
    trainer_only=['SPECIES_GIRAFARIG','SPECIES_HITMONTOP','SPECIES_MILTANK','SPECIES_PINECO','SPECIES_SMEARGLE','SPECIES_URSARING']
    need(len(previous_owned)==139 and len(previous_opponents)==168,'Exact Cave/Rematch predecessor')
    need(len(previous_owned|set(additions))==146 and len(previous_opponents|set(additions)|set(trainer_only))==180,'Exact Seven closure')
    for name in additions+trainer_only:
        original=species[name];meta=metadata[name]
        for sr in [original['source'],original['learnsetSource'],meta['learnsetPointerSource'],meta['dexSource']]+[m['source'] for m in original['levelUpMoves']]+[e['source'] for e in meta['sourceEvolutions']]:
            read(sr['path']);need(inputs[sr['path']]['sha256']==sr['sha256'],'Canonical source identity: '+name)
    edges=[]
    for name,level,target in [('SPECIES_PHANPY',25,'SPECIES_DONPHAN'),('SPECIES_LARVITAR',30,'SPECIES_PUPITAR'),('SPECIES_PUPITAR',55,'SPECIES_TYRANITAR')]:
        e=metadata[name]['sourceEvolutions'];need(len(e)==1 and e[0]['method']=='EVO_LEVEL' and e[0]['parameterValue']==level and e[0]['target']==identity(target),'Source evolution')
        edges.append(dict(species=identity(name),sourceEvolution=e[0]))
    battles=[];actors=[]
    names=['SevenIsland_SevaultCanyon','SevenIsland_SevaultCanyon_Entrance','SevenIsland_TanobyRuins','SevenIsland_TrainerTower']
    for name in names:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for local_id,actor in enumerate(mapping['object_events'],1):
            if actor.get('trainer_type')!='TRAINER_TYPE_NORMAL':continue
            label=actor['script'];path='data/scripts/trainers.inc'
            block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            command=re.search(r'^\t(trainerbattle_single|trainerbattle_double) ([^\n]+)$',block[1],re.M)
            need(command is not None,'First trainer command')
            args=command[2].split(', ');double=command[1]=='trainerbattle_double'
            need(args[0][8:] in TRAINERS,'Source trainer scope')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle);actors.append(dict(map=mapping['id'],localId=local_id,x=actor['x'],y=actor['y'],movementType=actor['movement_type'],sight=actor['trainer_sight_or_berry_tree_id'],script=label,trainer=identity(args[0])))
    need(len(trainers)==18 and len(battles)==20 and sum(t['doubleBattle'] for t in trainers)==2,'18 identities, 20 actor scripts, two doubles')
    identities=[]
    for trainer,sets in zip(trainers,resolved):
        h=0;members=[]
        for member,moves in zip(trainer['party'],sets['party']):
            h=(h+sum(trainer['encodedTrainerName'])+sum(member['encodedSpeciesName']))&0xffffffff
            pid=((0x80 if trainer['doubleBattle'] else 0x78 if trainer['encounterMusicGender']&128 else 0x88)+(h<<8))&0xffffffff
            p=species[member['species']['value'][5:]];ratio=p['genderRatio']
            members.append(dict(species=member['species'],level=member['level'],fixedIv=member['sourceIv']*31//255,personality=pid,nature=pid%25,abilitySlot=pid&1,ability=p['ability2'] if pid&1 and p['ability2']!='ABILITY_NONE' else p['ability1'],gender='genderless' if ratio==255 else 'female' if ratio==254 or ratio!=0 and (pid&255)<ratio else 'male',heldItem=member['heldItem'],moves=[identity(m) for m in moves['moves']]))
        identities.append(dict(trainer=trainer['id'],members=members,basePrize=(8 if trainer['doubleBattle'] else 4)*trainer['baseMoney']*trainer['party'][-1]['level']))
    wild=json.loads(read('src/data/wild_encounters.json'))
    encounters=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map'].startswith('MAP_SEVEN_ISLAND_') and e['base_label'].endswith('_FireRed')]
    fields=next(g['fields'] for g in wild['wild_encounter_groups'] if 'fields' in g)
    need(len(encounters)==11,'Exactly eleven FireRed encounter maps')
    admitted={s['species'] for e in encounters for kind in ['land_mons','water_mons','rock_smash_mons','fishing_mons'] for s in e.get(kind,{}).get('mons',[])[:2 if kind=='fishing_mons' else None]}
    need(admitted-previous_owned=={'SPECIES_PHANPY','SPECIES_LARVITAR','SPECIES_SKARMORY','SPECIES_UNOWN'},'Minimal direct wild additions')
    all_wild={s['species'] for e in encounters for kind in ['land_mons','water_mons','rock_smash_mons','fishing_mons'] for s in e.get(kind,{}).get('mons',[])}
    held=[dict(species=identity(s),common=metadata[s]['originalFields']['itemCommon'],rare=metadata[s]['originalFields']['itemRare']) for s in sorted(all_wild)]
    tm='src/data/pokemon/tmhm_learnsets.h';cells=[]
    for item in sorted(r['item']['value'][5:] for r in old_machines['additions']):
        rows=[]
        for name in additions:
            match=one(tm,r'^    \['+name+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),')
            rows.append(dict(species=identity(name),sourceSpeciesId=species[name]['sourceSpeciesId'],canLearn=bool(re.search(r'TMHM\('+item[5:]+r'_',match[1])),source=ref(tm,name,match)))
        cells.append(dict(item=identity(item),compatibility=rows))
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h','src/wild_encounter.c','include/random.h','include/constants/pokemon.h']:read(path)
    def draw(seed):
        seed=(seed*1103515245+24691)&0xffffffff;return seed,seed>>16
    cases=[]
    for team in identities:
        pid=team['members'][0]['personality']
        shiny_seed=next(seed for seed in range(1000000) if (draw(draw(seed)[0])[1]^draw(seed)[1]^(pid>>16)^(pid&65535))<8)
        for initial in [42,shiny_seed]:
            seed=initial;raw_draws=[];ots=[]
            for member in team['members']:
                while True:
                    seed,lo=draw(seed);seed,hi=draw(seed);raw_draws.extend([lo,hi]);pid=member['personality']
                    if hi^lo^(pid>>16)^(pid&65535)>=8:break
                ots.append(lo|(hi<<16))
            cases.append(dict(trainer=team['trainer'],initialSeed=initial,finalSeed=seed,draws=len(raw_draws),rawDraws=raw_draws,originalTrainerIds=ots))
    docs={
      'trainers.json':dict(sourceCommit=COMMIT,profile='firered-seven-island-trainers-v1',completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-seven-island-owned-pokemon-v1',canonicalCatalogSha256=dependencies[0]['sha256'],previousOwnedSpeciesCount=139,ownedSpeciesCount=146,previousOpponentSpeciesCount=168,opponentSpeciesCount=180,ownedSpeciesAdded=additions,trainerOnlySpeciesAdded=trainer_only,evolutionEdges=edges,species=[dict(definition=species[s],metadata=metadata[s]) for s in additions+trainer_only]),
      'rules.json':dict(sourceCommit=COMMIT,profile='firered-seven-island-rules-v1',identities=identities,generationCases=cases,trainerActors=actors,trainerHashes={t['id']['value']:fingerprint(t) for t in trainers},battleHashes={b['scriptEntry']:fingerprint(b) for b in battles}),
      'encounters.json':dict(sourceCommit=COMMIT,profile='firered-seven-island-encounters-v1',sourcePath='src/data/wild_encounters.json',sourceSha256=inputs['src/data/wild_encounters.json']['sha256'],encounters=encounters,fields=fields,heldItems=held,runtimeMethods=['land','surf','oldRod'],retainedMetadataOnlyMethods=['rockSmash','goodRod','superRod']),
      'machines.json':dict(sourceCommit=COMMIT,profile='firered-seven-island-teaching-v1',previousSpeciesCount=139,speciesCount=146,machineCount=39,additions=cells,additionsSemanticSha256=fingerprint(cells),itemsAdded=[]),
    }
    items_text=read('src/data/items.json');source_items=json.loads(items_text)['items']
    constants=read('include/constants/items.h');constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
    def item_metadata(symbol):
     i=next(i for i in source_items if i['itemId']==symbol)
     match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
     r=dict(path='src/data/items.json',symbol=symbol,startLine=line,endLine=line,sha256=inputs['src/data/items.json']['sha256'])
     value=dict(id={'value':'pret:'+symbol},sourceItemSymbol=symbol,sourceItemId=constant[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
     return dict(item=value,description=i['description_english'].replace('\\n','\n'),declarationDescription=i['description_english'],descriptionSource=r)
    new_items=[]
    for m in [item_metadata(n) for n in ['ITEM_KINGS_ROCK','ITEM_LUCKY_PUNCH','ITEM_HEART_SCALE']]:
        i=m['item']
        new_items.append({k:i[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=m['description'],healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=i))
    shop_path='data/maps/SevenIsland_Mart/scripts.inc'
    stock=one(shop_path,r'^SevenIsland_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)')
    stock_items=re.findall(r'\t\.2byte (ITEM_\w+)',stock[1]);need(stock_items.pop()=='ITEM_NONE' and len(stock_items)==9,'Source nine-item shop')
    shop=dict(id='SevenIsland_Mart_Items',items=[identity(s) for s in stock_items],source=ref(shop_path,'SevenIsland_Mart_Items',stock))
    flags=read('include/constants/flags.h')
    def flag_value(symbol):
        m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Pickup flag')
        expr=m[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
        add=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(0x[0-9A-Fa-f]+|\d+)\)',expr);need(add,'Flag syntax')
        return flag_value(add[1])+int(add[2],0)
    pickups=[]
    def pickup(mapping,event,hidden,local_id,item,quantity):
        flag=event['flag'];info=item_metadata(item)['item']
        need(quantity==1 and (not hidden or not event['underfoot']),'Pickup quantity/underfoot')
        pickups.append(dict(id='seven-island:'+flag,mapSymbol=mapping['id'],hidden=hidden,sourceLocalId=local_id,x=event['x'],y=event['y'],elevation=event['elevation'],script='EventScript_HiddenItemScript' if hidden else event['script'],itemSymbol=item,sourceItemId=info['sourceItemId'],item=info['id'],itemName=info['name'],pocket=info['pocket'],quantity=quantity,flag=flag,sourceFlagId=flag_value(flag)))
    for name in names+['SevenIsland_SevaultCanyon_House']:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for local_id,event in enumerate(mapping['object_events'],1):
            if event.get('graphics_id')!='OBJ_EVENT_GFX_ITEM_BALL':continue
            match=one('data/scripts/item_ball_scripts.inc',r'^'+event['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n')
            pickup(mapping,event,False,local_id,match[1],int(match[2] or 1))
        for event in mapping['bg_events']:
            if event['type']=='hidden_item':pickup(mapping,event,True,None,event['item'],event['quantity'])
    need(len(pickups)==13 and sum(p['hidden'] for p in pickups)==9,'13 source pickups')
    item_names=sorted({p['itemSymbol'] for p in pickups}|set(stock_items))
    docs['items.json']=dict(sourceCommit=COMMIT,profile='firered-seven-island-items-v1',items=[item_metadata(n) for n in item_names],additions=new_items,shop=shop,pickups=pickups,renewableFlags=[p['flag'] for p in pickups if p['itemSymbol']=='ITEM_HEART_SCALE' or p['mapSymbol']=='MAP_SEVEN_ISLAND_TRAINER_TOWER' and p['itemSymbol'] in ['ITEM_BIG_PEARL','ITEM_PEARL']])
    need(len(docs['items.json']['renewableFlags'])==6,'Six inherited renewable flags')
    for path in ['data/scripts/obtain_item.inc','src/renewable_hidden_items.c','include/constants/hold_effects.h']:read(path)
    docs['source-input-hashes.json']=dict(sourceCommit=COMMIT,sourceTree=TREE,files=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('reference',type=Path);p.add_argument('output',type=Path);p.add_argument('--dependency-root',required=True,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency_root)
    files={'content/generated/seven-island-rules/'+n:(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode() for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,sourceTree=TREE,profile='firered-seven-island-rules-v1',files=[dict(path=k,byteLength=len(v),sha256=sha(v)) for k,v in sorted(files.items())])
    files['scripts/seven-island-rules.lock.json']=(json.dumps(lock,indent=2)+'\n').encode()
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=18,battles=20,owned=146,opponents=180,machines=39,hashes={n:sha(v) for n,v in files.items()}),indent=2))
if __name__=='__main__':main()
