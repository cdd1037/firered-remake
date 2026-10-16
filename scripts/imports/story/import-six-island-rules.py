#!/usr/bin/env python3
"""Import exact Six Island battle catalogs from pinned source inputs, without running upstream code.
Use REFERENCE_OR_RAW DEPENDENCY OUTPUT [--check].
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers
import argparse
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
MAPS = ('SixIsland_WaterPath', 'SixIsland_RuinValley', 'SixIsland_GreenPath', 'SixIsland_PatternBush', 'SixIsland_OutcastIsland')


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = (reference / path).read_bytes() if not (reference / ".git").exists() and (reference / path).is_file() else read_source(reference, path, COMMIT)
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
    actor_scripts = []
    for name in MAPS:
        mapping = json.loads(read('data/maps/' + name + '/map.json'))
        for local_id, actor in enumerate(mapping['object_events'], 1):
            if actor['trainer_type'] != 'TRAINER_TYPE_NORMAL': continue
            label = actor['script']
            paths = ['data/maps/' + name + '/scripts.inc', 'data/scripts/trainers.inc']
            found = [(path, match) for path in paths for match in re.finditer(r'^' + label + r'::\n((?:(?!^\w+::).)*)', read(path), re.M | re.S)]
            need(len(found) == 1, 'Unique actor battle script: ' + label)
            path, block = found[0]
            command = re.search(r'^\t(trainerbattle_single|trainerbattle_double) ([^\n]+)$', block[1], re.M)
            need(command is not None, 'First battle command: ' + label)
            actor_scripts.append((mapping, local_id, actor, path, block, command))
    trainer_symbols = sorted({entry[-1][2].split(', ')[0] for entry in actor_scripts})
    trainers = selected_trainers(read, trainer_symbols)
    battles=[]; actors=[]
    for mapping, local_id, actor, path, block, command in actor_scripts:
            label = actor['script']
            args=command[2].split(', ');double=command[1]=='trainerbattle_double'
            need(args[0] in trainer_symbols,'Source actor trainer scope')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),
                rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',
                postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',
                callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle)
            actors.append(dict(map=mapping['id'],localId=local_id,x=actor['x'],y=actor['y'],movementType=actor['movement_type'],sight=actor['trainer_sight_or_berry_tree_id'],script=label,trainer=identity(args[0])))
    need(trainers and len(battles)==len(actor_scripts), 'Every trainer actor must bind a battle')
    docs={
      'trainers.json':dict(trainers=trainers,battles=battles),
      'rules.json':dict(trainerActors=actors)}
    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency)
    files={'content/generated/data/regions/six-island/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()) for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,profile='firered-six-island-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=len(docs['trainers.json']['trainers']),battles=len(docs['trainers.json']['battles'])),indent=2))
if __name__=='__main__':main()
