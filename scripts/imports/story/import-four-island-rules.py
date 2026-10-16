#!/usr/bin/env python3
"""Import exact Four Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers, resolved_parties
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TRAINERS = ('TEAM_ROCKET_GRUNT_45',)


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
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
    path='data/maps/FourIsland_IcefallCave_Back/scripts.inc'
    label='FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene'
    block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
    battle_line=one(path,r'^\ttrainerbattle_no_intro (TRAINER_\w+), (\w+)$')
    mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT')
    tail=block[1].split(battle_line[0],1)[1]
    post=re.search(r'^\tmsgbox (\w+)',tail,re.M)[1]
    battle=dict(trainer=identity(battle_line[1]),mode=mode,rivalFlags=0,battleTypeFlags=8,scriptEntry=label,
        introText='',defeatText=battle_line[2],victoryText='',postBattleText=post,entryScript='EventScript_DoNoIntroTrainerBattle',
        callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
    trainer=trainers[0]
    need(mode==3 and trainer['sourceTrainerId']==539 and trainer['partyFlags']==0 and not trainer['doubleBattle'] and trainer['aiFlags']==1 and trainer['items']==['ITEM_NONE']*4, 'Exact Grunt45 declaration')
    need([(p['species']['value'],p['level'],p['sourceIv'],p['heldItem']) for p in trainer['party']]==[('pret:SPECIES_ZUBAT',38,0,'ITEM_NONE')]*2+[('pret:SPECIES_GOLBAT',38,0,'ITEM_NONE')], 'Grunt45 party')
    docs={'trainers.json':dict(completeSourceRoster=False,
            trainers=trainers,battles=[battle],resolvedMoves=resolved)}
    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']: p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs=generate(a.reference,a.dependency)
    files={'content/generated/data/regions/four-island/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()) for n,d in docs.items()}
    for name,data in files.items():
        dest=a.output/name
        if a.check: need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else: dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=1,battles=1),indent=2))
if __name__=='__main__': main()
