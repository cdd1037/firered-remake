#!/usr/bin/env python3
"""Compile Victory Road battles and pickups directly from pinned raw source."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import selected_trainers, resolved_parties
from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse
import json
import re
from pathlib import Path

import machine_catalog as shared_machines

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ("BLACK_BELT_DAISUKE", "COOLTRAINER_ALEXA", "COOLTRAINER_CAROLINE", "COOLTRAINER_COLBY",
    "COOLTRAINER_GEORGE", "COOLTRAINER_NAOMI", "COOLTRAINER_ROLANDO", "COOL_COUPLE_RAY_TYRA",
    "JUGGLER_GREGORY", "JUGGLER_NELSON", "POKEMANIAC_DAWSON", "TAMER_VINCENT")


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(raw_root, dependency):
    from world_source import WorldSource
    source = WorldSource(raw_root)
    shared_item = ItemSource(source.read).definition
    read = source.text
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
    from source_features import script_blocks
    names = ('Route23', 'VictoryRoad_1F', 'VictoryRoad_2F', 'VictoryRoad_3F', 'IndigoPlateau_Exterior')
    blocks = {b['label']: b for b in script_blocks(source, names)['blocks']}
    occurrences, pickups, hidden = [], [], []
    for name in names:
        events = source.json('data/maps/' + name + '/map.json')
        path = 'data/maps/' + name + '/scripts.inc'
        for local_id, event in enumerate(events['object_events'], 1):
            label = event['script']
            if label not in blocks: continue
            path = blocks[label]['sourceRef']['path']
            match = re.search(r'^' + re.escape(label) + r'::\n((?:(?!^\w+::).)*)', read(path), re.M | re.S)
            if match is None: continue
            for command in re.finditer(r'^\t(trainerbattle_\w+) (.+)$', match[1], re.M):
                occurrences.append((path, label, command[1], [a.strip() for a in command[2].split(',')]))
            if event['graphics_id'] == 'OBJ_EVENT_GFX_ITEM_BALL':
                command = re.search(r'^\tfinditem (ITEM_\w+)(?:, (\d+))?$', match[1], re.M)
                need(command is not None, 'Unknown item pickup: ' + label)
                pickups.append((events['id'], local_id, event, path, command[1], int(command[2] or 1)))
        hidden.extend((events['id'], event, 'data/maps/' + name + '/map.json') for event in events['bg_events'] if event['type'] == 'hidden_item')
    battles = []
    for path, label, opcode, args in occurrences:
        block = one(path, r'^' + label + r'::\n((?:(?!^\w+::).)*)')
        double = opcode == 'trainerbattle_double'
        command = one(path, r'^\t' + opcode + ' ' + re.escape(', '.join(args)) + '$')
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
    map_ids = {'MAP_ROUTE23': 'map.route-23', **{f'MAP_VICTORY_ROAD_{n}F': f'map.victory-road-{n}f' for n in range(1,4)}}

    item_path='src/data/items.json'; source_items=json.loads(read(item_path))['items']
    placements=[]
    for map_symbol, local_id, event, path, item, quantity in pickups:
        label=event['script']
        block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        placements.append(dict(id=event['flag'],map=dict(value=map_ids[map_symbol]),localId=local_id,x=event['x'],y=event['y'],
            elevation=event['elevation'],item=identity(item),quantity=quantity,flag=event['flag'],
            hidden=False,underfoot=False,scriptEntry=label,source=ref(path,label,block)))
    for map_symbol, e, path in hidden:
        m=one(path,r'^\s*"flag": "'+e['flag']+r'"[^\n]*$')
        placements.append(dict(id=e['flag'],map=dict(value=map_ids[map_symbol]),localId=0,x=e['x'],y=e['y'],elevation=e['elevation'],
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
    outputs={'trainers.json':trainer_doc,'items.json':item_doc,'machines.json':machine_doc}
    return outputs

def main():
    from generated_output_compare import generated_output_matches
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--raw', type=Path, default=_PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--dependency-root', type=Path, default=_PROJECT_ROOT)
    p.add_argument('--out', type=Path, default=_PROJECT_ROOT/'content/generated')
    p.add_argument('--check',action='store_true');a=p.parse_args()
    outputs=generate(a.raw,a.dependency_root)
    for name,value in outputs.items():
        data=trainer_json(a.out,value,check=a.check) if name=='trainers.json' else (json.dumps(value,ensure_ascii=False,default=item_json)+'\n').encode()
        target=a.out/'data/regions/victory-road'/name
        if a.check: need(generated_output_matches(target,data),'Regeneration differs: '+name)
        else: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(checked=a.check,outputs=len(outputs))))
if __name__=='__main__':main()
