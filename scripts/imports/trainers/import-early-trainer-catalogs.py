#!/usr/bin/env python3
"""Compile early battle bindings directly from inert source scripts and shared trainer definitions."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT as ROOT
from shared_trainers import trainer_json
from trainer_import import convert_battles, selected_trainers, TextSources
import argparse
import json
import re

SELECTIONS = {'forest': ['BUG_CATCHER_ANTHONY',
            'BUG_CATCHER_CHARLIE',
            'BUG_CATCHER_DOUG',
            'BUG_CATCHER_RICK',
            'BUG_CATCHER_SAMMY'],
 'pewter': ['CAMPER_LIAM', 'LEADER_BROCK'],
 'route22': ['RIVAL_ROUTE22_EARLY_SQUIRTLE',
             'RIVAL_ROUTE22_EARLY_BULBASAUR',
             'RIVAL_ROUTE22_EARLY_CHARMANDER'],
 'route3': ['YOUNGSTER_BEN',
            'YOUNGSTER_CALVIN',
            'BUG_CATCHER_COLTON',
            'BUG_CATCHER_GREG',
            'BUG_CATCHER_JAMES',
            'LASS_JANICE',
            'LASS_SALLY',
            'LASS_ROBIN'],
 'mtmoon': ['YOUNGSTER_JOSH',
            'BUG_CATCHER_KENT',
            'BUG_CATCHER_ROBBY',
            'LASS_MIRIAM',
            'LASS_IRIS',
            'SUPER_NERD_JOVAN',
            'SUPER_NERD_MIGUEL',
            'HIKER_MARCOS',
            'TEAM_ROCKET_GRUNT',
            'TEAM_ROCKET_GRUNT_2',
            'TEAM_ROCKET_GRUNT_3',
            'TEAM_ROCKET_GRUNT_4']}
SCRIPTS = {'forest': ['data/maps/ViridianForest/scripts.inc'],
 'pewter': ['data/maps/PewterCity_Gym/scripts.inc'],
 'route22': ['data/maps/Route22/scripts.inc'],
 'route3': ['data/scripts/trainers.inc'],
 'mtmoon': ['data/maps/MtMoon_1F/scripts.inc', 'data/maps/MtMoon_B2F/scripts.inc']}
COUNTS = {region: len(ids) for region, ids in SELECTIONS.items()}

def require(value, message):
    if not value:
        raise ValueError(message)


def compile_catalog(root, region):
    raw = Path(root) / 'content/raw/pokefirered/037335f4'
    def read(path): return (raw / path).read_text()
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        require(len(matches) == 1, 'Expected one source binding: ' + pattern)
        return matches[0]
    def number(path, symbol):
        return int(one(path, r'^#define ' + symbol + r'\s+(0x[0-9A-Fa-f]+|\d+)\b')[1], 0)
    if region != 'route22':
        result = convert_battles(dict(trainers=SELECTIONS[region], scripts=SCRIPTS[region]), TextSources(read))
        for battle in result['battles']:
            for field in ('rivalFlags', 'victoryText'):
                battle.pop(field)
            if region != 'pewter':
                battle.pop('postBattleScript')
                battle.pop('musicOption')
            battle['source']['endLine'] = battle['source']['startLine']
            if battle['trainer']['value'] == 'pret:TRAINER_SUPER_NERD_MIGUEL':
                battle['introText'] = one(battle['source']['path'],
                    r'^MtMoon_B2F_EventScript_BattleMiguel::\n.*?\tmessage (\w+)')[1]
                battle['postBattleText'] = 'MtMoon_B2F_Text_WellEachTakeAFossil'
                require(battle['postBattleText'] in read(battle['source']['path']), 'Missing fossil dialogue')
        return result

    path = 'data/maps/Route22/scripts.inc'
    trainers = selected_trainers(read, ['TRAINER_' + name for name in SELECTIONS[region]])
    modes = 'include/constants/battle_setup.h'
    flags = 'include/constants/flags.h'
    require('#define TRAINER_FLAGS_START      (FLAG_0x4FF + 1)' in read(flags), 'Trainer flag base')
    require('moneyReward = 4 * lastMonLevel * gBattleStruct->moneyMultiplier * (gBattleTypeFlags & BATTLE_TYPE_DOUBLE ? 2 : 1) * gTrainerMoneyTable[i].value;' in read('src/battle_script_commands.c'), 'Trainer prize formula')
    battles = []
    # Preserve the current application's player-starter selection contract.
    players = {'SQUIRTLE': 'BULBASAUR', 'BULBASAUR': 'CHARMANDER', 'CHARMANDER': 'SQUIRTLE'}
    for trainer in trainers:
        symbol = trainer['id']['value'][5:]
        suffix = symbol.rsplit('_', 1)[1]
        label = 'Route22_EventScript_EarlyRival' + suffix.title()
        match = one(path, r'^' + label + r'::\n\ttrainerbattle_earlyrival ' + symbol + r', (\d+), (\w+), (\w+)\n')
        starter = int(one(path, r'^\tcall_if_eq VAR_STARTER_MON, (\d+), ' + label + r'$')[1])
        line = read(path).count('\n', 0, match.start()) + 1
        battles.append(dict(trainer=trainer['id'], playerStarterVar=starter,
            playerStarter=dict(value='pret:SPECIES_' + players[suffix]),
            mode=number(modes, 'TRAINER_BATTLE_EARLY_RIVAL'), rivalFlags=int(match[1]),
            battleTypeFlags=8, wonTrainerFlag=number(flags, 'FLAG_0x4FF') + 1 + trainer['sourceTrainerId'],
            basePrizeMoney=4 * trainer['party'][-1]['level'] * trainer['baseMoney'],
            scriptEntry=label, defeatText=match[2], victoryText=match[3],
            entryScript='EventScript_DoNoIntroTrainerBattle', callback='CB2_EndTrainerBattle',
            source=dict(path=path, symbol=label, startLine=line, endLine=line)))
    return dict(schemaVersion=1, trainers=trainers, battles=battles)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for region in COUNTS:
        document = compile_catalog(ROOT, region)
        data = trainer_json(ROOT / 'content/generated', document, check=args.check)
        path = ROOT / 'content/generated/data/trainer-battles' / (region + '.json')
        if args.check:
            require(path.read_bytes() == data, region + ': Generated trainer output differs')
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(catalogs=len(COUNTS), trainers=sum(COUNTS.values()), checked=args.check)))


if __name__ == '__main__':
    main()
