#!/usr/bin/env python3
"""Compile the seven Cerulean trainer parties and battle entries into domain data."""
import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-cerulean-trainers-v1'


def compile_catalog(root):
    generated = root / 'content/generated'
    raw = root / 'content/raw/pokefirered/037335f4'
    def read(path):
        return json.loads((generated / path).read_text())
    rows = read('cerulean-region/dependencies.json')['trainers']
    trainer_text = (raw / 'src/data/trainers.h').read_text()
    party_text = (raw / 'src/data/trainer_parties.h').read_text()
    constants = read('shared-constants/values.json')
    constants['F_TRAINER_FEMALE'] = 128
    species = {s['id']['value'][5:]: s for s in read('canonical-pokemon/catalog.json')['species']}
    money_text = (raw / 'src/battle_main.c').read_text()
    setup = (raw / 'src/battle_setup.c').read_text()
    for statement in ('gMain.savedCallback = CB2_EndTrainerBattle;', 'return EventScript_DoNoIntroTrainerBattle;', 'return EventScript_TryDoNormalTrainerBattle;'):
        if statement not in setup: raise ValueError('Cerulean callback source differs')
    def encode(name):
        if not re.fullmatch('[A-Z]+', name): raise ValueError('Trainer/species name glyph')
        return [0xbb + ord(c) - ord('A') for c in name]
    def expression(text):
        result = 0
        for symbol in text.split('|'): result |= constants[symbol.strip()]
        return result
    trainers, battles = [], []
    if len(rows) != 7: raise ValueError('Cerulean party closure')
    for row in rows:
        symbol = row['symbol']
        record = re.search(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', trainer_text, re.M | re.S)
        if record is None: raise ValueError('Missing Cerulean trainer declaration')
        decl = dict(re.findall(r'^\s*\.(\w+) = (.+),$', record[1], re.M))
        party_spec = re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', decl['party'])
        if party_spec is None or len(decl) != 8 or decl['doubleBattle'] != 'FALSE':
            raise ValueError('Cerulean trainer declaration')
        format, party_symbol = party_spec.groups(); custom = format == 'NO_ITEM_CUSTOM_MOVES'
        shape = 'TrainerMonNoItemCustomMoves' if custom else 'TrainerMonNoItemDefaultMoves'
        party_match = re.search(r'^static const struct ' + shape + ' ' + party_symbol + r'\[\] = \{\n(.*?)^\};', party_text, re.M | re.S)
        if party_match is None: raise ValueError('Missing Cerulean trainer party')
        party_ref = dict(path='src/data/trainer_parties.h', symbol=party_symbol,
            startLine=party_text.count('\n', 0, party_match.start()) + 1,
            endLine=party_text.count('\n', 0, party_match.end()) + 1)
        original = dict(path='src/data/trainers.h', symbol=symbol,
            startLine=trainer_text.count('\n', 0, record.start()) + 1,
            endLine=trainer_text.count('\n', 0, record.end()) + 1)
        members = list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{(MOVE_\w+(?:, MOVE_\w+){3})\},\n)?    \},\n', party_match[1]))
        if not members or ''.join(m[0] for m in members) != party_match[1]:
            raise ValueError('Unsupported Cerulean trainer party declaration')
        name = re.fullmatch(r'_\("([A-Z]+)"\)', decl['trainerName'])[1]
        items = [v.strip() for v in decl['items'].strip('{}').split(',') if v.strip()]
        if len(items) > 4 or any(v != 'ITEM_SUPER_POTION' for v in items): raise ValueError('Cerulean trainer items')
        party = []
        for member in members:
            source_iv, level, symbol_species = int(member[1]), int(member[2]), member[3]
            if bool(member[4]) != custom: raise ValueError('Cerulean trainer move format')
            slots = member[4].split(', ') if custom else []
            nonempty = [move for move in slots if move != 'MOVE_NONE']
            if custom and slots != nonempty + ['MOVE_NONE'] * (4 - len(nonempty)):
                raise ValueError('Nontrailing empty custom move slot')
            definition = species[symbol_species]
            species_id = definition['sourceSpeciesId']; species_name = symbol_species.removeprefix('SPECIES_')
            party.append(dict(species=dict(value='pret:' + symbol_species), sourceSpeciesId=species_id,
                speciesName=species_name, encodedSpeciesName=encode(species_name), level=level, sourceIv=source_iv,
                heldItem='ITEM_NONE', customMoves=[dict(value='pret:' + m) for m in slots],
                source=party_ref, speciesNameSource=dict(path='src/data/text/species_names.h', symbol=symbol_species, startLine=species_id + 2, endLine=species_id + 2)))
        trainer_class = decl['trainerClass']; money = re.search(r'^\s*\{' + trainer_class + r', (\d+)\},$', money_text, re.M)
        if not money: raise ValueError('Cerulean trainer money')
        money_line = money_text[:money.start()].count('\n') + 1
        trainer_id = dict(value='pret:' + symbol)
        trainers.append(dict(id=trainer_id, sourceTrainerId=constants[symbol], trainerClass=trainer_class,
            sourceTrainerClass=constants[trainer_class], trainerPic=decl['trainerPic'], sourceTrainerPic=constants[decl['trainerPic']],
            sourceTrainerName=name, encodedTrainerName=encode(name), encounterMusicGender=expression(decl['encounterMusic_gender']),
            items=items + ['ITEM_NONE'] * (4 - len(items)), doubleBattle=False, partyFlags=1 if custom else 0,
            aiFlags=expression(decl['aiFlags']), aiFlagSymbols=[v.strip() for v in decl['aiFlags'].split('|')],
            baseMoney=int(money[1]), party=party, source=original,
            moneySource=dict(path='src/battle_main.c', symbol=trainer_class, startLine=money_line, endLine=money_line)))
        rival = symbol.startswith('TRAINER_RIVAL_'); grunt = symbol == 'TRAINER_TEAM_ROCKET_GRUNT_5'; misty = symbol == 'TRAINER_LEADER_MISTY'
        map_name = 'CeruleanCity' if rival or grunt else 'CeruleanCity_Gym'
        person = 'Rival' if rival else 'Grunt' if grunt else name.title()
        suffix = symbol.split('_')[-1].title() if rival else ''
        entry = map_name + '_EventScript_' + person + suffix
        intro = map_name + '_Text_' + person + 'Intro'; defeat = map_name + '_Text_' + person + 'Defeat'
        script_path = 'data/maps/' + map_name + '/scripts.inc'; script = (raw / script_path).read_text()
        expected = 'trainerbattle_no_intro ' + symbol + ', ' + defeat if rival or grunt else 'trainerbattle_single ' + symbol + ', ' + intro + ', ' + defeat + (', CeruleanCity_Gym_EventScript_MistyDefeated, NO_MUSIC' if misty else '')
        match = re.search('^' + entry + r'::\n(?:(?!^\w+::).)*?\t' + re.escape(expected) + '$', script, re.M | re.S)
        if not match: raise ValueError('Cerulean battle operands: ' + symbol)
        line = script[:match.start()].count('\n') + 1
        battles.append(dict(trainer=trainer_id, mode=3 if rival or grunt else 1 if misty else 0,
            rivalFlags=0, battleTypeFlags=8, scriptEntry=entry, introText=intro, defeatText=defeat, victoryText='',
            postBattleText='CeruleanCity_Gym_Text_ExplainCascadeBadge' if misty else 'CeruleanCity_Text_RecoveredTM28FromGrunt' if grunt else map_name + '_Text_' + person + 'PostBattle',
            entryScript='EventScript_DoNoIntroTrainerBattle' if rival or grunt else 'EventScript_TryDoNormalTrainerBattle', callback='CB2_EndTrainerBattle',
            source=dict(path=script_path, symbol=entry, startLine=line, endLine=line + match[0].count('\n')),
            postBattleScript='CeruleanCity_Gym_EventScript_MistyDefeated' if misty else None, musicOption='NO_MUSIC' if misty else None))
    if sum(len(t['party']) for t in trainers) != 19: raise ValueError('Cerulean party member closure')
    return dict(schemaVersion=1, sourceCommit=COMMIT, profile=PROFILE, completeSourceRoster=False, trainers=trainers, battles=battles)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = (json.dumps(compile_catalog(ROOT), sort_keys=True, indent=2) + '\n').encode()
    path = ROOT / 'content/generated/cerulean-trainers/trainers.json'
    if args.check:
        if path.read_bytes() != data: raise ValueError('Cerulean trainer output differs')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print(json.dumps(dict(trainers=7, checked=args.check)))

if __name__ == '__main__': main()
