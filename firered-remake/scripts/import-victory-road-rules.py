#!/usr/bin/env python3
"""Import exact Victory Road rules from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY REGION OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

import machine_catalog as shared_machines

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ("BLACK_BELT_DAISUKE", "COOLTRAINER_ALEXA", "COOLTRAINER_CAROLINE", "COOLTRAINER_COLBY",
    "COOLTRAINER_GEORGE", "COOLTRAINER_NAOMI", "COOLTRAINER_ROLANDO", "COOL_COUPLE_RAY_TYRA",
    "JUGGLER_GREGORY", "JUGGLER_NELSON", "POKEMANIAC_DAWSON", "TAMER_VINCENT")


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency, region):
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
    region_inputs = {}
    def dependent(root, path,  bucket):
        data = (root / path).read_bytes()
        bucket[path] = dict(path=path)
        return json.loads(data)
    contracts = dependent(region, 'content/generated/victory-road-region/contracts.json',
         region_inputs)
    dependencies = {}
    canonical = dependent(dependency, 'content/generated/canonical-pokemon/catalog.json',
         dependencies)
    baseline = dependent(dependency, 'scripts/fuchsia-gym-baseline.lock.json',  dependencies)
    owned = ['SPECIES_' + s for s in baseline['species'] +
        ['CHANSEY','SCYTHER','TAUROS','PSYDUCK','GOLDUCK','LAPRAS','KRABBY','KINGLER']]
    need(len(owned) == len(set(owned)) == 115, 'Expected unchanged owned115')
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    battles = []
    for occurrence in contracts['trainerOccurrences']:
        label = occurrence['event']['script']
        path = occurrence['sourceRef']['path']
        block = one(path, r'^' + label + r'::\n((?:(?!^\w+::).)*)')
        double = occurrence['opcode'] == 'trainerbattle_double'
        command = one(path, r'^\t' + occurrence['opcode'] + ' ' + re.escape(', '.join(occurrence['arguments'])) + '$')
        args = occurrence['arguments']
        post = re.search(r'^\tmsgbox (\w+)', block[1], re.M)
        mode = 'TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'
        battle = dict(trainer=identity(args[0]), mode=number('include/constants/battle_setup.h', mode),
            rivalFlags=0, battleTypeFlags=9 if double else 8, scriptEntry=label, introText=args[1],
            defeatText=args[2], victoryText='', postBattleText=post[1] if post else '',
            entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
            callback='CB2_EndTrainerBattle', source=ref(path,label,block), postBattleScript=None, musicOption=None)
        if double: battle['notEnoughMonsText'] = args[3]
        battles.append(battle)
    need(len(trainers) == 12 and len(battles) == 13 and sum(len(t['party']) for t in trainers) == 45, 'Trainer scope')
    need(sum(t['doubleBattle'] for t in trainers) == 1 and sum(b['mode'] == 4 for b in battles) == 2, 'Double scope')
    trainer_doc = dict(sourceCommit=COMMIT, profile='firered-victory-road-trainers-v1', completeSourceRoster=False,
        trainers=trainers, battles=battles, resolvedMoves=resolved)

    # Full original FireRed table declarations, including rods whose acquisition is not yet admitted.
    wild_path = 'src/data/wild_encounters.json'
    wild_source = json.loads(read(wild_path))
    headers = next(g for g in wild_source['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    source_encounters = [e for e in headers['encounters'] if e.get('base_label') in
        {'sVictoryRoad1F_FireRed','sVictoryRoad2F_FireRed','sVictoryRoad3F_FireRed','sRoute23_FireRed'}]
    weights = {f['type']: f['encounter_rates'] for f in headers['fields']}
    map_ids = {'MAP_ROUTE23': 'map.route-23', **{f'MAP_VICTORY_ROAD_{n}F': f'map.victory-road-{n}f' for n in range(1,4)}}
    land, water, fishing, pairs = [], [], [], set()
    for e in source_encounters:
        for field, dest in [('land_mons', land), ('water_mons', water), ('fishing_mons', fishing)]:
            if field not in e: continue
            v=e[field]; rows=[]
            for i,s in enumerate(v['mons']):
                rows.append(dict(index=i,species=identity(s['species']), minimumLevel=s['min_level'], maximumLevel=s['max_level'], weight=weights[field][i]))
                pairs.update((s['species'],lv) for lv in range(s['min_level'],s['max_level']+1))
            table=dict(map=dict(value=map_ids[e['map']]),sourceMapSymbol=e['map'],sourceCommit=COMMIT,sourcePath=wild_path,
                sourceSymbol=e['base_label'],rawEncounterRate=v['encounter_rate'],slots=rows)
            if field == 'land_mons': table.pop('sourceMapSymbol')
            if field == 'fishing_mons': table['rodGroups'] = next(f['groups'] for f in headers['fields'] if f['type']==field)
            dest.append(table)
    initial=[]
    for symbol,lv in sorted(pairs):
        mon=species[symbol]; moves=natural_moves(symbol,lv)
        canonical_moves=[]
        for entry in mon['levelUpMoves']:
            m=entry['move']['value'][5:]
            if entry['level']<=lv and m not in canonical_moves:canonical_moves=(canonical_moves+[m])[-4:]
        need(moves==canonical_moves,'Canonical initial moves differ')
        initial.append(dict(species=identity(symbol),level=lv,moves=[identity(m) for m in moves]))
    wild_species=sorted({symbol for symbol,lv in pairs})
    need(set(wild_species)<=set(owned) and len(wild_species)==19 and len(pairs)==122,'Wild ownership/pair scope')
    wild_dependencies=[]
    for symbol in wild_species:
        p=species[symbol]
        source_path='src/data/pokemon/species_info.h'
        source=one(source_path,r'^    \['+symbol+r'\] =\n    \{(.*?)^    \},')
        held=re.search(r'\.itemCommon = (ITEM_\w+),\n\s*\.itemRare = (ITEM_\w+),',source[1])
        need(held,'Missing held slots')
        wild_dependencies.append(dict(species=identity(symbol),sourceSpeciesId=p['sourceSpeciesId'],ability1=p['ability1'],ability2=p['ability2'],
            itemCommon=held[1],itemRare=held[2],source=ref(source_path,symbol,source),learnsetSource=p['learnsetSource']))
    wild_doc=dict(sourceCommit=COMMIT,profile='firered-victory-road-wild-v1',land=land,water=water,fishing=fishing,
        initialMoves=initial,species=wild_dependencies,ownedSpeciesAdded=[],
        limitations=['Good Rod and Super Rod acquisition/use are not provided by this region rules package. Their exact source slots are retained.',
          'Battle generation and source mechanics do not grant campaign map admission, rod possession or a save upgrade.'])

    item_path='src/data/items.json'; source_items=json.loads(read(item_path))['items']
    placements=[]
    for row in contracts['itemPickups']:
        event=row['event']; path=row['script']['sourceRef']['path']; label=event['script']
        block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        need(re.search(r'^\tfinditem '+row['item']+'$',block[1],re.M),'Item pickup command differs')
        placements.append(dict(id=event['flag'],map=dict(value=map_ids[row['map']]),localId=row['localId'],x=event['x'],y=event['y'],
            elevation=event['elevation'],item=identity(row['item']),quantity=row['quantity'],flag=event['flag'],
            hidden=False,underfoot=False,scriptEntry=label,source=ref(path,label,block)))
    for row in contracts['hiddenItems']:
        e=row['event']; path='data/maps/'+('Route23' if row['map']=='MAP_ROUTE23' else 'VictoryRoad_1F')+'/map.json'
        events=json.loads(read(path)); need(e in events['bg_events'],'Hidden event differs')
        m=one(path,r'^\s*"flag": "'+e['flag']+r'"[^\n]*$')
        placements.append(dict(id=e['flag'],map=dict(value=map_ids[row['map']]),localId=0,x=e['x'],y=e['y'],elevation=e['elevation'],
            item=identity(e['item']),quantity=e['quantity'],flag=e['flag'],hidden=True,underfoot=e['underfoot'],scriptEntry='',source=ref(path,e['flag'],m)))
    item_symbols=sorted({p['item']['value'][5:] for p in placements})
    metadata={}
    for symbol in item_symbols:
        item=next(i for i in source_items if i['itemId']==symbol)
        m=one(item_path,r'^    \{\n(?:(?!^    \}).)*?"itemId": "'+symbol+r'"(?:(?!^    \}).)*?^    \}')
        item_ref=ref(item_path,symbol,m)
        definition=dict(id=identity(symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),name=item['english'],
            price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=str(item['type']),
            holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],
            battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=item['importance']==0,canSell=item['price']!=0,
            canGive=item['importance']==0,source=item_ref)
        description=item['description_english'].replace('\\n','\n'); description_ref=item_ref
        if 'moveId' in item:
            path='src/move_descriptions.c'; desc_symbol='gMoveDescription_'+item['moveId']
            m=one(path,r'^const u8 '+desc_symbol+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
            description=json.loads(m[1]);description_ref=ref(path,desc_symbol,m)
        metadata[symbol]=dict(item=definition,description=description,declarationDescription=item['description_english'].replace('\\n','\n'),descriptionSource=description_ref)
    need(len(placements)==18 and len(item_symbols)==16 and sum(p['hidden'] for p in placements)==10,'Pickup scope')
    item_doc=dict(sourceCommit=COMMIT,profile='firered-victory-road-items-v1',items=list(metadata.values()),pickups=placements,
        renewable=False,limitations=['Eighteen unique pickup flags represent sixteen item types.',
          'Pickup/source metadata does not itself admit new bag item use; capability audit documents the exact current boundary.',
          'Double-Edge tutor and its one-time flag remain outside this rules admission.'])
    machines = shared_machines.machine_definitions(dependency, 'content/generated/victory-road-rules/machines.json', 'machines')
    machine_doc = dict(sourceCommit=COMMIT, profile='firered-victory-road-machines-v1', machines=machines)
    # Authenticated auxiliary mechanics inputs are data-only evidence, never executed.
    for path in ['src/pokemon.c','src/battle_setup.c','src/battle_main.c','src/wild_encounter.c',
                 'data/scripts/move_tutors.inc','src/renewable_hidden_items.c','include/constants/flags.h','src/data/pokemon/item_effects.h']:
        read(path)
    rules=dict(sourceCommit=COMMIT,profile='firered-victory-road-rules-v1',ownedSpeciesCount=115,newOwnedSpecies=[],
        trainerSpecies=sorted({m['species']['value'] for t in trainers for m in t['party']}),
        trainerMoves=sorted({m for t in resolved for p in t['party'] for m in p['moves']}),
        wildMoves=sorted({m['value'] for p in initial for m in p['moves']}),
        basePayouts={t['id']['value']: (8 if t['doubleBattle'] else 4)*t['baseMoney']*t['party'][-1]['level'] for t in trainers},
        ordinaryWildAdmissionAdditions=['ARBOK','GOLBAT','GOLDEEN','MACHOKE','MAROWAK','POLIWAG','POLIWHIRL','PRIMEAPE'],
        molTResImported=False,goodAndSuperRodAccess=False,doubleEdgeTutorAdmitted=False)
    need(len(rules['trainerSpecies'])==40 and len(rules['trainerMoves'])==93 and len(rules['wildMoves'])==54,'Dependency closure')
    outputs={'trainers.json':trainer_doc,'wild.json':wild_doc,'items.json':item_doc,'machines.json':machine_doc,'rules.json':rules,
        'source-input-hashes.json':dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],
            predecessorInputs=list(dependencies.values()),regionInputs=list(region_inputs.values()))}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','region','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    outputs,raw=generate(a.reference,a.dependency,a.region)
    files={f'content/generated/victory-road-rules/{name}':(json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode() for name,value in outputs.items()}
    files.update({'content/raw/pokefirered/037335f4/'+name:data for name,data in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-victory-road-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=12,members=45,battleOccurrences=13,wildSpecies=19,wildSpeciesLevelPairs=122,
        ownedSpeciesAdded=0,pickups=18,itemTypes=16,newMachines=4,machineCompatibilityCells=460,
        sourceInputs=len(raw),files=len(files),bytes=sum(map(len,files.values()))),indent=2))
if __name__=='__main__':main()
