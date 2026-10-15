#!/usr/bin/env python3
"""Strict local extraction of three Oak's lab trainers; never execute upstream source."""
import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-oaks-lab-trainers-v1'
SYMBOLS = ['TRAINER_RIVAL_OAKS_LAB_SQUIRTLE', 'TRAINER_RIVAL_OAKS_LAB_BULBASAUR',
           'TRAINER_RIVAL_OAKS_LAB_CHARMANDER']


def import_catalog(raw, out):
    lock = json.loads((ROOT / 'scripts/lab-trainers.lock.json').read_text(encoding='utf-8'))
    if (lock['schemaVersion'] != 1 or lock['commit'] != COMMIT or
            lock['repository'] != 'pret/pokefirered' or lock['profile'] != PROFILE or
            lock['completeSourceRoster'] is not False or lock['trainerSymbols'] != SYMBOLS):
        raise ValueError('Unknown source identity, profile or trainer scope')
    sources, inputs = {}, {}
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) in inputs:
            raise ValueError('Unsafe or duplicate source path')
        data = (raw / path).read_bytes()
        sources[str(path)] = data.decode('utf-8')
        inputs[str(path)] = entry
    if len(inputs) != 29:
        raise ValueError('Expected the complete bounded 29-input research manifest')

    def one(pattern, text):
        matches = list(re.finditer(pattern, text, re.M | re.S))
        if len(matches) != 1:
            raise ValueError(f'Expected one match for {pattern}; got {len(matches)}')
        return matches[0]

    def source(path, symbol, start, end):
        return dict(path=path, symbol=symbol, startLine=sources[path].count('\n', 0, start) + 1,
                    endLine=sources[path].count('\n', 0, end) + 1)

    def constant(path, symbol):
        expression = one(r'^#define ' + re.escape(symbol) + r'\s+([^\n]+)', sources[path])[1].split('//')[0].strip()
        if re.fullmatch(r'\d+', expression):
            return int(expression)
        shift = re.fullmatch(r'\(1 << (\d+)\)', expression)
        if shift:
            return 1 << int(shift[1])
        raise ValueError('Unsupported constant: ' + expression)

    def fields(body):
        result = {}
        for line in body.splitlines():
            match = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not match or match[1] in result:
                raise ValueError('Unknown or duplicate record field: ' + line)
            result[match[1]] = match[2]
        return result

    def function_ref(path, symbol):
        match = one(r'^(?:static )?(?:inline )?\w+[ *]+' + re.escape(symbol) + r'\([^;\n]*\)\n\{\n.*?^\}', sources[path])
        return source(path, symbol, match.start(), match.end())

    charmap = {}
    for match in re.finditer(r"^'([A-Z])'\s*=\s*([0-9A-F]{2})$", sources['charmap.txt'], re.M):
        if match[1] in charmap:
            raise ValueError('Duplicate character encoding')
        charmap[match[1]] = int(match[2], 16)
    if len(charmap) != 26:
        raise ValueError('Missing English uppercase character map')

    trainers, battles = [], []
    tp, pp = 'src/data/trainers.h', 'src/data/trainer_parties.h'
    names_path = 'src/data/text/species_names.h'
    script_path = 'data/maps/PalletTown_ProfessorOaksLab/scripts.inc'
    for symbol in SYMBOLS:
        match = one(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', sources[tp])
        f = fields(match[1])
        if set(f) != {'trainerClass', 'encounterMusic_gender', 'trainerPic', 'trainerName', 'items', 'doubleBattle', 'aiFlags', 'party'}:
            raise ValueError('Unknown trainer record layout')
        if f['items'] != '{}' or f['doubleBattle'] != 'FALSE':
            raise ValueError('Only empty-item single battles are supported')
        name = one(r'^_\("([A-Z]+)"\)$', f['trainerName'])[1]
        party_symbol = one(r'^NO_ITEM_DEFAULT_MOVES\((\w+)\)$', f['party'])[1]
        party_match = one(r'^static const struct TrainerMonNoItemDefaultMoves ' + party_symbol + r'\[\] = \{\n(.*?)^\};', sources[pp])
        party_body = party_match[1]
        entries = list(re.finditer(r'^    \{\n(.*?)^    \},\n', party_body, re.M | re.S))
        if len(entries) != 1 or ''.join(e[0] for e in entries) != party_body:
            raise ValueError('Exactly one fully parsed lab party member required')
        party = []
        for entry in entries:
            pf = fields(entry[1])
            if set(pf) != {'iv', 'lvl', 'species'} or not re.fullmatch(r'\d+', pf['iv']) or not re.fullmatch(r'\d+', pf['lvl']):
                raise ValueError('Unsupported party member layout')
            species_symbol = pf['species']
            species_match = one(r'^    \[' + species_symbol + r'\] = _\("([A-Z]+)"\),$', sources[names_path])
            species_name = species_match[1]
            party.append(dict(species={'value': 'pret:' + species_symbol},
                              sourceSpeciesId=constant('include/constants/species.h', species_symbol),
                              speciesName=species_name, encodedSpeciesName=[charmap[c] for c in species_name],
                              level=int(pf['lvl']), sourceIv=int(pf['iv']), heldItem='ITEM_NONE', customMoves=[],
                              source=source(pp, party_symbol, party_match.start(), party_match.end()),
                              speciesNameSource=source(names_path, species_symbol, species_match.start(), species_match.end())))
        ai_symbols = f['aiFlags'].split(' | ')
        if len(set(ai_symbols)) != len(ai_symbols):
            raise ValueError('Duplicate AI flags')
        ai_flags = 0
        for ai in ai_symbols:
            ai_flags |= constant('include/constants/battle_ai.h', ai)
        trainer_id = {'value': 'pret:' + symbol}
        money = one(r'^    \{' + f['trainerClass'] + r', (\d+)\},$', sources['src/battle_main.c'])
        trainers.append(dict(id=trainer_id, sourceTrainerId=constant('include/constants/opponents.h', symbol),
                             trainerClass=f['trainerClass'], sourceTrainerClass=constant('include/constants/trainers.h', f['trainerClass']),
                             trainerPic=f['trainerPic'], sourceTrainerPic=constant('include/constants/trainers.h', f['trainerPic']),
                             sourceTrainerName=name, encodedTrainerName=[charmap[c] for c in name],
                             encounterMusicGender=constant('include/constants/trainers.h', f['encounterMusic_gender']),
                             items=['ITEM_NONE'] * constant('include/battle.h', 'MAX_TRAINER_ITEMS'),
                             doubleBattle=False, partyFlags=0, aiFlags=ai_flags, aiFlagSymbols=ai_symbols,
                             baseMoney=int(money[1]), party=party, source=source(tp, symbol, match.start(), match.end()),
                             moneySource=source('src/battle_main.c', 'gTrainerMoneyTable', money.start(), money.end())))
        battle_match = one(r'^\ttrainerbattle_earlyrival ' + symbol + r', (\w+), (\w+), (\w+)\n\tgoto (\w+)\n\tend$', sources[script_path])
        flags = constant('include/constants/battle.h', battle_match[1])
        battle_flags = constant('include/constants/battle.h', 'BATTLE_TYPE_TRAINER')
        if flags & constant('include/constants/battle.h', 'RIVAL_BATTLE_TUTORIAL'):
            battle_flags |= constant('include/constants/battle.h', 'BATTLE_TYPE_FIRST_BATTLE')
        battles.append(dict(trainer=trainer_id, mode=constant('include/constants/battle_setup.h', 'TRAINER_BATTLE_EARLY_RIVAL'),
                            rivalFlags=flags, battleTypeFlags=battle_flags, defeatText=battle_match[2], victoryText=battle_match[3],
                            entryScript='EventScript_DoNoIntroTrainerBattle', callback='CB2_EndTrainerBattle',
                            postBattleScript=battle_match[4], source=source(script_path, symbol, battle_match.start(), battle_match.end())))
    rules_sources = [function_ref(path, symbol) for path, symbol in [
        ('src/battle_main.c', 'CreateNPCTrainerParty'), ('src/pokemon.c', 'CreateBoxMon'),
        ('src/pokemon.c', 'GiveBoxMonInitialMoveset'), ('src/battle_setup.c', 'StartTrainerBattle'),
        ('src/battle_setup.c', 'CB2_EndTrainerBattle'), ('src/battle_setup.c', 'IsPlayerDefeated'),
        ('src/battle_ai_script_commands.c', 'BattleAI_SetupAIData')]]
    document = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, completeSourceRoster=False,
                    inputs=lock['files'], limitations=[
                        'Exactly the three Oaks Lab rival trainers; not the complete source trainer roster.',
                        'Party creation is pure; battle effects, AI execution, money, experience and event mutation are outside this catalog.',
                        'Random32 is sequenced low-half then high-half; no source-ROM or frame-timing parity is claimed.',
                        'Source TERRY bytes determine personality; displayed rival names and player-derived OT name/met fields are separate.'
                    ], trainers=trainers, battles=battles)
    data = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    output = dict(path='lab-trainers.json')
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, trainerCount=len(trainers),
                    partyMemberCount=sum(len(t['party']) for t in trainers), completeSourceRoster=False,
                    romDownloaded=False, upstreamScriptsExecuted=False, outputs=[output])
    # All input/grammar checks finish before touching any output.
    out.mkdir(parents=True, exist_ok=True)
    for path, content in [('lab-trainers.json', data), ('manifest.json', (json.dumps(manifest, indent=2) + '\n').encode('utf-8'))]:
        temporary = out / (path + '.tmp')
        temporary.write_bytes(content)
        temporary.replace(out / path)
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--raw-root', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out-root', type=pathlib.Path, default=ROOT / 'content/generated/lab-trainers')
    args = parser.parse_args()
    import_catalog(args.raw_root, args.out_root)
