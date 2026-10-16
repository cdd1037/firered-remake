#!/usr/bin/env python3
"""Compile Sevii Ruby battle rules directly from pinned raw source.
Use --raw-root RAW --out FINAL_RULE_DIRECTORY [--check].
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import selected_trainers
from map_runtime import generated_root
from sevii_rule_source import region_context, battle_occurrences
import argparse
import json
import re
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('BLACK_BELT_HUGH', 'BLACK_BELT_SHEA', 'CAMPER_BRYCE', 'CRUSH_GIRL_JOCELYN', 'CRUSH_GIRL_SHARON', 'CRUSH_GIRL_TANYA', 'CRUSH_KIN_MIK_KIA', 'FISHERMAN_TOMMY', 'PICNICKER_CLAIRE', 'PKMN_RANGER_BETH', 'PKMN_RANGER_LOGAN', 'SWIMMER_FEMALE_ABIGAIL', 'SWIMMER_FEMALE_MARIA', 'SWIMMER_MALE_FINN', 'SWIMMER_MALE_GARRETT', 'TEAM_ROCKET_GRUNT_43', 'TEAM_ROCKET_GRUNT_44')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(raw_root):
    source, worlds, scripts = region_context(raw_root, 'sevii-ruby')
    texts = {}
    def read(path):
        if path not in texts:
            texts[path] = source.text(path)
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
    occurrences = battle_occurrences(source, scripts, ruby=True)
    battles = []
    for occurrence in occurrences:
        if occurrence['rematch']: continue
        path, label = occurrence['sourceRef']['path'], occurrence['script']
        block = one(path, r'^' + label + r'::\n((?:(?!^\w+::).)*)')
        args = occurrence['arguments']; opcode = occurrence['opcode']
        one(path, r'^\t' + opcode + ' ' + re.escape(', '.join(args)) + '$')
        double = opcode == 'trainerbattle_double'; no_intro = opcode == 'trainerbattle_no_intro'
        need(opcode in ['trainerbattle_single', 'trainerbattle_double', 'trainerbattle_no_intro'], 'Unexpected battle opcode')
        post = re.search(r'^\tmsgbox (\w+)', block[1], re.M)
        mode = 'TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if no_intro else 'TRAINER_BATTLE_SINGLE'
        battle = dict(trainer=identity(args[0]), mode=number('include/constants/battle_setup.h', mode),
            rivalFlags=0, battleTypeFlags=9 if double else 8, scriptEntry=label,
            introText='' if no_intro else args[1], defeatText=args[1] if no_intro else args[2], victoryText='',
            postBattleText=post[1] if post else '', entryScript='EventScript_TryDoDoubleTrainerBattle' if double else
            'EventScript_DoNoIntroTrainerBattle' if no_intro else 'EventScript_TryDoNormalTrainerBattle',
            callback='CB2_EndTrainerBattle', source=ref(path,label,block), postBattleScript=None, musicOption=None)
        if double: battle['notEnoughMonsText'] = args[3]
        battles.append(battle)
    need(len(trainers) == 17 and len(battles) == 18, 'Expected 17 trainers and 18 first-battle scripts')
    need(sum(t['doubleBattle'] for t in trainers)==1 and sum(b['mode']==4 for b in battles)==2, 'Mik/Kia double scope')
    held = [t for t in trainers if any(m['heldItem'] != 'ITEM_NONE' for m in t['party'])]
    need(len(held)==6 and all(t['partyFlags']==2 and all(m['heldItem']=='ITEM_BLACK_BELT' for m in t['party']) for t in held), 'Exact Black Belt teams')
    return {'trainers.json': dict(trainers=trainers, battles=battles)}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw-root',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    docs=generate(args.raw_root)
    for name,document in docs.items():
        data=(trainer_json(generated_root(args.out), document, check=args.check) if name == 'trainers.json'
              else (json.dumps(document,indent=2,ensure_ascii=False)+'\n').encode())
        target=args.out/name
        if args.check:
            need(target.read_bytes()==data,'Regeneration differs: '+name)
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(checked=args.check,trainers=17,battles=18),indent=2))

if __name__=='__main__':main()
