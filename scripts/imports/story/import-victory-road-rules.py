#!/usr/bin/env python3
"""Import exact Victory Road rules from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY REGION OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers, resolved_parties, natural_moves
from item_catalog import item_definition as shared_item, inventory_item, item_json
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
            data = read_source(reference, path, COMMIT)
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
    def identity(symbol):
        return dict(value="pret:" + symbol)
    trainers = selected_trainers(read, ["TRAINER_" + short for short in TRAINERS])
    resolved = resolved_parties(read, trainers)
    region_inputs = {}
    def dependent(root, path,  bucket):
        data = (root / path).read_bytes()
        bucket[path] = dict(path=path)
        return json.loads(data)
    contracts = dependent(region, 'content/generated/maps/regions/victory-road/contracts.json',
         region_inputs)
    dependencies = {}
    canonical = dependent(dependency, 'content/generated/data/pokemon/canonical/catalog.json',
         dependencies)
    baseline = dependent(dependency, 'scripts/recipes/story/fuchsia-gym-baseline.lock.json',  dependencies)
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
    trainer_doc = dict(  completeSourceRoster=False,
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
            table=dict(map=dict(value=map_ids[e['map']]),rawEncounterRate=v['encounter_rate'],slots=rows)
            if field == 'fishing_mons': table['rodGroups'] = next(f['groups'] for f in headers['fields'] if f['type']==field)
            dest.append(table)
    initial=[]
    for symbol,lv in sorted(pairs):
        mon=species[symbol]; moves=natural_moves(read,symbol,lv)
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
            itemCommon=held[1],itemRare=held[2],source=ref(source_path,symbol,source)))
    wild_doc=dict(land=land,water=water,fishing=fishing,
        initialMoves=initial,species=wild_dependencies,ownedSpeciesAdded=[])

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
        definition=shared_item(symbol)
        description=item['description_english'].replace('\\n','\n')
        if 'moveId' in item:
            path='src/move_descriptions.c'; desc_symbol='gMoveDescription_'+item['moveId']
            m=one(path,r'^const u8 '+desc_symbol+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
            description=json.loads(m[1])
        metadata[symbol]=dict(item=definition,description=description)
    need(len(placements)==18 and len(item_symbols)==16 and sum(p['hidden'] for p in placements)==10,'Pickup scope')
    item_doc=dict(items=list(metadata.values()),pickups=placements,
        renewable=False)
    machines = shared_machines.machine_definitions(dependency, 'content/generated/data/regions/victory-road/machines.json', 'machines')
    machine_doc = dict(  machines=machines)
    # Authenticated auxiliary mechanics inputs are data-only evidence, never executed.
    for path in ['src/pokemon.c','src/battle_setup.c','src/battle_main.c','src/wild_encounter.c',
                 'data/scripts/move_tutors.inc','src/renewable_hidden_items.c','include/constants/flags.h','src/data/pokemon/item_effects.h']:
        read(path)
    rules=dict(ownedSpeciesCount=115,newOwnedSpecies=[],
        trainerSpecies=sorted({m['species']['value'] for t in trainers for m in t['party']}),
        trainerMoves=sorted({m for t in resolved for p in t['party'] for m in p['moves']}),
        wildMoves=sorted({m['value'] for p in initial for m in p['moves']}),
        basePayouts={t['id']['value']: (8 if t['doubleBattle'] else 4)*t['baseMoney']*t['party'][-1]['level'] for t in trainers},
        ordinaryWildAdmissionAdditions=['ARBOK','GOLBAT','GOLDEEN','MACHOKE','MAROWAK','POLIWAG','POLIWHIRL','PRIMEAPE'],
        molTResImported=False,goodAndSuperRodAccess=False,doubleEdgeTutorAdmitted=False)
    need(len(rules['trainerSpecies'])==40 and len(rules['trainerMoves'])==93 and len(rules['wildMoves'])==54,'Dependency closure')
    outputs={'trainers.json':trainer_doc,'wild.json':wild_doc,'items.json':item_doc,'machines.json':machine_doc,'rules.json':rules}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','region','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    outputs,raw=generate(a.reference,a.dependency,a.region)
    files={f'content/generated/data/regions/victory-road/{name}':(trainer_json(a.output/'content/generated', value, check=a.check) if name == 'trainers.json' else (json.dumps(value,indent=2,ensure_ascii=False, default=item_json)+'\n').encode()) for name,value in outputs.items()}
    files.update({'content/raw/pokefirered/037335f4/'+name:data for name,data in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-victory-road-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=12,members=45,battleOccurrences=13,wildSpecies=19,wildSpeciesLevelPairs=122,
        ownedSpeciesAdded=0,pickups=18,itemTypes=16,newMachines=4,machineCompatibilityCells=460,
        sourceInputs=len(raw),files=len(files),bytes=sum(map(len,files.values()))),indent=2, default=item_json))
if __name__=='__main__':main()
