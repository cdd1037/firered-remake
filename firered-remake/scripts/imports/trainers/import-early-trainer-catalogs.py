#!/usr/bin/env python3
"""Compile early-game trainer dependencies into executable, typed domain catalogs.

Only this build-time importer reads declaration/provenance inventories. Runtime
loaders consume the emitted TrainerDefinition and region battle-definition arrays.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
import argparse
import json
import pathlib

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
COUNTS = dict(forest=5, pewter=2, route22=3, route3=8, mtmoon=12)
ROUTE3 = {'TRAINER_' + s for s in ('YOUNGSTER_BEN', 'YOUNGSTER_CALVIN', 'BUG_CATCHER_COLTON',
    'BUG_CATCHER_GREG', 'BUG_CATCHER_JAMES', 'LASS_JANICE', 'LASS_SALLY', 'LASS_ROBIN')}
MONEY_LINES = dict(TRAINER_CLASS_LEADER=453, TRAINER_CLASS_RIVAL_EARLY=456,
    TRAINER_CLASS_YOUNGSTER=459, TRAINER_CLASS_BUG_CATCHER=460, TRAINER_CLASS_HIKER=461,
    TRAINER_CLASS_SUPER_NERD=464, TRAINER_CLASS_TEAM_ROCKET=466, TRAINER_CLASS_LASS=467,
    TRAINER_CLASS_CAMPER=482)
AI = 'AI_SCRIPT_CHECK_BAD_MOVE | AI_SCRIPT_TRY_TO_FAINT | AI_SCRIPT_CHECK_VIABILITY'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def identifier(symbol):
    return dict(value='pret:' + symbol)


def encode(name):
    result = []
    for c in name:
        require(c in 'ABCDEFGHIJKLMNOPQRSTUVWXYZ♀♂', 'Unsupported source-name glyph: ' + c)
        result.append(0xb6 if c == '♀' else 0xb5 if c == '♂' else 0xbb + ord(c) - ord('A'))
    return result


def reference(path, symbol, line):
    require(line > 0 and not pathlib.PurePosixPath(path).is_absolute() and
        all(p not in ('', '.', '..') for p in path.split('/')), 'Invalid trainer source reference')
    return dict(path=path, symbol=symbol, startLine=line, endLine=line)


def source_ref(value, symbol=None):
    require(value.get('kind', 'pret') == 'pret' and value.get('commit', COMMIT) == COMMIT,
        'Trainer provenance differs')
    return reference(value['path'], symbol or value['symbol'], value['line'])


def compile_standard(region, rows):
    if region == 'route3':
        rows = [r for r in rows if r['symbol'] in ROUTE3]
    trainers, battles = [], []
    for row in rows:
        d = row['declaration']
        brock = row['symbol'] == 'TRAINER_LEADER_BROCK'
        miguel = row['symbol'] == 'TRAINER_SUPER_NERD_MIGUEL'
        custom = region == 'pewter'
        ai = AI if brock else 'AI_SCRIPT_CHECK_BAD_MOVE'
        party_ref = source_ref(row['partySourceRef'])
        require(len(d) == 8 and d['doubleBattle'] == 'FALSE' and d['items'] == '{}' and
            d['aiFlags'] == ai and d['trainerName'] == '_("' + row['name'] + '")' and
            d['party'] == ('NO_ITEM_CUSTOM_MOVES' if custom else 'NO_ITEM_DEFAULT_MOVES') +
                '(' + party_ref['symbol'] + ')' and row['partyFlags'] == int(custom),
            region + ': Unsupported trainer declaration')
        require(encode(row['name']) == row['encodedName'], region + ': Trainer name encoding differs')
        if region == 'mtmoon':
            music = {'TRAINER_CLASS_YOUNGSTER': 'TRAINER_ENCOUNTER_MUSIC_MALE',
                'TRAINER_CLASS_BUG_CATCHER': 'TRAINER_ENCOUNTER_MUSIC_MALE',
                'TRAINER_CLASS_LASS': 'TRAINER_ENCOUNTER_MUSIC_FEMALE | F_TRAINER_FEMALE',
                'TRAINER_CLASS_SUPER_NERD': 'TRAINER_ENCOUNTER_MUSIC_SUSPICIOUS',
                'TRAINER_CLASS_HIKER': 'TRAINER_ENCOUNTER_MUSIC_HIKER',
                'TRAINER_CLASS_TEAM_ROCKET': 'TRAINER_ENCOUNTER_MUSIC_AQUA'}[d['trainerClass']]
            script_map = 'MtMoon_B2F' if miguel or 351 <= row['sourceTrainerId'] <= 354 else 'MtMoon_1F'
            require(d['encounterMusic_gender'] == music and row['postBattleScript'] is None and
                row['musicOption'] is None and row['scriptSourceRef']['path'] ==
                'data/maps/' + script_map + '/scripts.inc', 'MtMoon battle declaration differs')
        party = []
        for m in row['party']:
            party.append(dict(species=identifier(m['species']), sourceSpeciesId=m['sourceSpeciesId'],
                speciesName=m['speciesName'], encodedSpeciesName=encode(m['speciesName']),
                level=m['level'], sourceIv=m['sourceIv'], heldItem=m['heldItem'],
                customMoves=[identifier(move) for move in m['customMoves']], source=party_ref,
                speciesNameSource=reference('src/data/text/species_names.h', m['species'], m['sourceSpeciesId'] + 2)))
        trainer_id = identifier(row['symbol'])
        trainers.append(dict(id=trainer_id, sourceTrainerId=row['sourceTrainerId'],
            trainerClass=d['trainerClass'], sourceTrainerClass=row['sourceTrainerClass'],
            trainerPic=d['trainerPic'], sourceTrainerPic=row['sourceTrainerPic'],
            sourceTrainerName=row['name'], encodedTrainerName=row['encodedName'],
            encounterMusicGender=row['encounterMusicGender'], items=['ITEM_NONE'] * 4,
            doubleBattle=False, partyFlags=row['partyFlags'], aiFlags=row['aiFlags'],
            aiFlagSymbols=ai.split(' | '), baseMoney=row['baseMoney'], party=party,
            source=source_ref(row['sourceRef']),
            moneySource=reference('src/battle_main.c', d['trainerClass'], MONEY_LINES[d['trainerClass']])))
        post = 'MtMoon_B2F_Text_WellEachTakeAFossil' if miguel else row['scriptEntry'].replace('EventScript', 'Text') + 'PostBattle'
        battle = dict(trainer=trainer_id, mode=3 if miguel else 1 if brock else 0,
            battleTypeFlags=8, scriptEntry=row['scriptEntry'], introText=row['introText'],
            defeatText=row['defeatText'], postBattleText=post,
            entryScript='EventScript_DoNoIntroTrainerBattle' if miguel else 'EventScript_TryDoNormalTrainerBattle',
            callback='CB2_EndTrainerBattle', source=source_ref(row['scriptSourceRef']))
        if region == 'pewter':
            battle.update(postBattleScript=row['postBattleScript'], musicOption=row['musicOption'])
        battles.append(battle)
    return trainers, battles


def compile_route22(rows):
    trainers, battles = [], []
    for row in rows:
        symbol = row['trainer']; trainer_id = identifier(symbol)
        title = symbol.removeprefix('TRAINER_RIVAL_ROUTE22_EARLY_').title()
        party_symbol = 'sParty_RivalRoute22Early' + title
        d = row['trainerDefinition']['fields']
        require(d['trainerName'] == '_("TERRY")' and d['items'] == '{}' and d['doubleBattle'] == 'FALSE' and
            d['aiFlags'] == AI and d['encounterMusic_gender'] == 'TRAINER_ENCOUNTER_MUSIC_MALE' and
            d['party'] == 'NO_ITEM_CUSTOM_MOVES(' + party_symbol + ')' and
            row['partyEncoding'] == 'NO_ITEM_CUSTOM_MOVES', 'Unsupported Route22 trainer declaration')
        party = []
        for m in row['party']:
            sid = {'SPECIES_PIDGEY': 16, 'SPECIES_SQUIRTLE': 7, 'SPECIES_BULBASAUR': 1, 'SPECIES_CHARMANDER': 4}[m['species']]
            require(m['fixedIv'] == m['ivByte'] * 31 // 255, 'Route22 IV scaling differs')
            name = m['species'].removeprefix('SPECIES_')
            party.append(dict(species=identifier(m['species']), sourceSpeciesId=sid, speciesName=name,
                encodedSpeciesName=encode(name), level=m['level'], sourceIv=m['ivByte'], heldItem='ITEM_NONE',
                customMoves=[identifier(move) for move in m['moves']], source=source_ref(row['partySource'], party_symbol),
                speciesNameSource=reference('src/data/text/species_names.h', m['species'], sid + 2)))
        trainers.append(dict(id=trainer_id, sourceTrainerId=row['trainerId'], trainerClass=d['trainerClass'],
            sourceTrainerClass=81, trainerPic=d['trainerPic'], sourceTrainerPic=106, sourceTrainerName='TERRY',
            encodedTrainerName=encode('TERRY'), encounterMusicGender=0, items=['ITEM_NONE'] * 4,
            doubleBattle=False, partyFlags=row['partyFlags'], aiFlags=row['aiFlags'], aiFlagSymbols=AI.split(' | '),
            baseMoney=4, party=party, source=source_ref(row['trainerDefinition']['source'], symbol),
            moneySource=reference('src/battle_main.c', 'TRAINER_CLASS_RIVAL_EARLY', 456)))
        script = 'Route22_EventScript_EarlyRival' + title
        battles.append(dict(trainer=trainer_id, playerStarterVar=row['starterVar'], playerStarter=identifier(row['playerStarter']),
            mode=row['battleMode'], rivalFlags=row['rivalBattleFlags'], battleTypeFlags=8,
            wonTrainerFlag=row['wonTrainerFlag'], basePrizeMoney=row['basePrizeMoney'], scriptEntry=script,
            defeatText='Route22_Text_EarlyRivalDefeat', victoryText='Text_RivalVictory',
            entryScript='EventScript_DoNoIntroTrainerBattle', callback='CB2_EndTrainerBattle',
            source=reference('data/maps/Route22/scripts.inc', script, 60 + (row['trainerId'] - 329) * 4)))
    return trainers, battles


def compile_catalog(root, region):
    dependency = json.loads((root / 'content/generated' / (region + '-region') / 'dependencies.json').read_text())
    if region == 'route22':
        trainers, battles = compile_route22(dependency['earlyRivalTrainers'])
    else:
        trainers, battles = compile_standard(region, dependency['trainers'])
    require(len(trainers) == COUNTS[region] and len({t['id']['value'] for t in trainers}) == COUNTS[region],
        region + ': Trainer roster differs')
    profile = 'firered-' + ('route22-rival' if region == 'route22' else region) + '-trainers-v1'
    return dict(schemaVersion=1,   completeSourceRoster=False,
        trainers=trainers, battles=battles)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for region in COUNTS:
        document = compile_catalog(ROOT, region)
        data = trainer_json(ROOT / 'content/generated', document, check=args.check)
        path = ROOT / 'content/generated' / (region + '-trainers') / 'trainers.json'
        if args.check:
            require(path.read_bytes() == data, region + ': Generated trainer output differs')
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(catalogs=len(COUNTS), trainers=sum(COUNTS.values()), checked=args.check)))


if __name__ == '__main__':
    main()
