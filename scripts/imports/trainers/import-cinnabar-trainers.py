#!/usr/bin/env python3
"""Compile Cinnabar trainer definitions and script battle bindings from raw source."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'lib'))
import argparse
import json
import re
from project_paths import PROJECT_ROOT
from world_source import WorldSource
from world_selection import load_selection
from source_features import script_blocks,feature_constants
from trainer_import import selected_trainers
from shared_trainers import trainer_json


def compile_trainers(raw):
    src=WorldSource(raw)
    names=[r['sourceName'] for r in load_selection(PROJECT_ROOT/'data/world-maps.json') if r['path'].startswith('maps/regions/cinnabar/')]
    scripts=script_blocks(src,names)
    constants=feature_constants(src,[],scripts)['values']
    occurrences=[]
    for block in scripts['blocks']:
        for row in block['lines']:
            code=row['text'].split('@')[0].strip()
            if not code.startswith('trainerbattle_'):continue
            op,args=code.split(None,1)
            if 'rematch' in op:continue
            occurrences.append((block,op,[a.strip() for a in args.split(',')]))
    trainers=selected_trainers(src.text,sorted({args[0] for _,_,args in occurrences}))
    battles=[]
    for block,op,operands in occurrences:
        symbol=operands[0];args=operands[1:];no_intro=op=='trainerbattle_no_intro';double=op=='trainerbattle_double'
        mode='TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if no_intro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'
        path=block['sourceRef']['path'];text=src.text(path)
        match=re.search(r'^'+block['label']+r'::\n((?:(?!^\w+::).)*)',text,re.M|re.S)
        if match is None:raise ValueError('Missing trainer script')
        post=re.search(r'^\tmsgbox (\w+)',match[1],re.M)
        start=text.count('\n',0,match.start())+1;end=text.count('\n',0,match.end())+1
        battle=dict(trainer=dict(value='pret:'+symbol),mode=constants[mode]['value'],rivalFlags=0,battleTypeFlags=9 if double else 8,
                    scriptEntry=block['label'],introText='' if no_intro else args[0],defeatText=args[0] if no_intro else args[1],victoryText='',
                    postBattleText=post[1] if post else '',entryScript='EventScript_DoNoIntroTrainerBattle' if no_intro else 'EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',
                    source=dict(path=path,symbol=block['label'],startLine=start,endLine=end),
                    postBattleScript=args[2] if not double and len(args)>2 else None,musicOption=args[3] if not double and len(args)>3 else None)
        if double:battle['notEnoughMonsText']=args[2]
        battles.append(battle)
    if len(trainers)!=24 or len(battles)!=31:raise ValueError('Cinnabar battle scope changed')
    return dict(completeSourceRoster=False,trainers=trainers,battles=battles)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--raw',type=Path,default=PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--out',type=Path,default=PROJECT_ROOT/'content/generated')
    p.add_argument('--check',action='store_true');args=p.parse_args()
    data=trainer_json(args.out,compile_trainers(args.raw),check=args.check)
    path=args.out/'data/regions/cinnabar/trainers.json'
    if args.check:
        if path.read_bytes()!=data:raise ValueError('Cinnabar trainers differ')
    else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    print(json.dumps(dict(trainers=24,battles=31,checked=args.check)))

if __name__=='__main__':main()
