#!/usr/bin/env python3
"""Import exact Saffron Fighting Dojo catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers, resolved_parties, natural_moves
import argparse
import json
import re
import subprocess
from pathlib import Path

import sys
from machine_catalog import compatibility_additions, machine_definitions, admission_species

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
TRAINERS = ('BLACK_BELT_KOICHI', 'BLACK_BELT_MIKE', 'BLACK_BELT_HIDEKI', 'BLACK_BELT_AARON', 'BLACK_BELT_HITOSHI')


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
    dependencies=[]
    def dependent(path):
        data=(dependency/path).read_bytes()
        dependencies.append(dict(path=path));return json.loads(data)
    canonical=dependent('content/generated/data/pokemon/canonical/catalog.json')
    previous=dependent('content/generated/data/pokemon/power-plant.json')
    species={s['id']['value'][5:]:s for s in canonical['species']}
    metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
    additions=['SPECIES_HITMONLEE','SPECIES_HITMONCHAN']
    need(previous['ownedSpeciesCount']==152 and previous['opponentSpeciesCount']==186 and previous['machineCount']==41,'Exact Power Plant predecessor')
    for name in additions:
        original=species[name];meta=metadata[name]
        need(not original['evolutions'] and not meta['sourceEvolutions'],'Terminal source species')
    battles=[];actors=[];mapping=json.loads(read('data/maps/SaffronCity_Dojo/map.json'));path='data/maps/SaffronCity_Dojo/scripts.inc'
    for local_id,actor in enumerate(mapping['object_events'],1):
        if actor.get('trainer_type')!='TRAINER_TYPE_NORMAL':continue
        label=actor['script'];block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        command=re.search(r'^\ttrainerbattle_single ([^\n]+)$',block[1],re.M);need(command,'Source single battle')
        args=command[1].split(', ');need(args[0][8:] in TRAINERS and len(args)==(4 if args[0]=='TRAINER_BLACK_BELT_KOICHI' else 3),'Exact source trainer operands')
        post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
        battles.append(dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)==4 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=args[3] if len(args)==4 else None,musicOption=None))
        actors.append(dict(map=mapping['id'],localId=local_id,x=actor['x'],y=actor['y'],movementType=actor['movement_type'],sight=actor['trainer_sight_or_berry_tree_id'],script=label,trainer=identity(args[0])))
    need(len(trainers)==len(battles)==5 and sum(len(t['party']) for t in trainers)==11,'Five trainers and eleven members')
    need([t['sourceTrainerId'] for t in trainers]==[317,318,319,320,321] and all(t['partyFlags']==2 and t['aiFlags']==1 and t['baseMoney']==6 and t['sourceTrainerClass']==80 and t['sourceTrainerPic']==105 and t['encounterMusicGender']==4 and not t['doubleBattle'] and t['items']==['ITEM_NONE']*4 for t in trainers),'Exact Black Belt trainer metadata')
    need(all(m['sourceIv']==100 and m['heldItem']=='ITEM_BLACK_BELT' for t in trainers for m in t['party']),'Exact fixed IV and held Black Belt')
    identities=[]
    for trainer,sets in zip(trainers,resolved):
        h=0;members=[]
        for member,moves in zip(trainer['party'],sets['party']):
            h=(h+sum(trainer['encodedTrainerName'])+sum(member['encodedSpeciesName']))&0xffffffff
            pid=(0x88+(h<<8))&0xffffffff;p=species[member['species']['value'][5:]];ratio=p['genderRatio']
            members.append(dict(species=member['species'],level=member['level'],fixedIv=member['sourceIv']*31//255,personality=pid,nature=pid%25,abilitySlot=pid&1,ability=p['ability2'] if pid&1 and p['ability2']!='ABILITY_NONE' else p['ability1'],gender='genderless' if ratio==255 else 'female' if ratio==254 or ratio!=0 and (pid&255)<ratio else 'male',heldItem=member['heldItem'],moves=[identity(m) for m in moves['moves']]))
        identities.append(dict(trainer=trainer['id'],members=members,basePrize=4*trainer['baseMoney']*trainer['party'][-1]['level']))
    need([t['basePrize'] for t in identities]==[888,744,768,864,744],'Exact source rewards')
    cells=compatibility_additions(dependency, 'content/generated/data/regions/fighting-dojo/machines.json')
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h','include/random.h','include/constants/pokemon.h','include/constants/flags.h','include/constants/items.h','src/script_pokemon_util.c','data/maps/SaffronCity_Dojo/text.inc']:read(path)
    need('givemon VAR_TEMP_1, 25' in read('data/maps/SaffronCity_Dojo/scripts.inc'),'Level 25 source gift')
    need('TRAINER_BATTLE_CONTINUE_SCRIPT' in read('asm/macros/event.inc'),'Trainer continuation macro')
    def draw(seed):
        seed=(seed*1103515245+24691)&0xffffffff;return seed,seed>>16
    cases=[]
    for team in identities:
        pid=team['members'][0]['personality']
        shiny_seed=next(seed for seed in range(1000000) if (draw(draw(seed)[0])[1]^draw(seed)[1]^(pid>>16)^(pid&65535))<8)
        for initial in [42,shiny_seed]:
            seed=initial;raw_draws=[];ots=[]
            for member in team['members']:
                while True:
                    seed,lo=draw(seed);seed,hi=draw(seed);raw_draws.extend([lo,hi]);pid=member['personality']
                    if hi^lo^(pid>>16)^(pid&65535)>=8:break
                ots.append(lo|(hi<<16))
            cases.append(dict(trainer=team['trainer'],initialSeed=initial,finalSeed=seed,draws=len(raw_draws),rawDraws=raw_draws,originalTrainerIds=ots))
    docs={
      'trainers.json':dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(previousOwnedSpeciesCount=152,ownedSpeciesCount=154,previousOpponentSpeciesCount=186,opponentSpeciesCount=186,ownedSpeciesAdded=additions,trainerOnlySpeciesAdded=[],species=[dict(definition=dict(id=species[s]['id'],evolutions=species[s]['evolutions']),metadata=metadata[s]) for s in additions]),
      'rules.json':dict(identities=identities,generationCases=cases,trainerActors=actors,gifts=[dict(species=identity(s),sourceSpeciesId=species[s]['sourceSpeciesId'],level=25,moves=natural_moves(read,s,25),localId=6+i,hideFlag='FLAG_HIDE_DOJO_'+s[8:]+'_BALL',hideFlagId=0x60+i) for i,s in enumerate(additions)],choiceFlag='FLAG_GOT_HITMON_FROM_DOJO',choiceFlagId=0x278),
      'machines.json':dict(previousSpeciesCount=152,speciesCount=154,machineCount=41,additions=cells,itemsAdded=[]),
    }

    code='using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Trainers;\n\n// Generated by the bounded Fighting Dojo importer from authenticated source records.\npublic static partial class FightingDojoTrainerPartyFactory\n{\n'
    for name,rows in [('TrainerIds',[t['id']['value'] for t in trainers]),('BattleScripts',[b['scriptEntry'] for b in battles])]:
        trainer=name=='TrainerIds';typ='TrainerId' if trainer else 'string'
        code+=f'    public static readonly ImmutableHashSet<{typ}> {name} = new HashSet<{typ}> {{\n'
        for key in rows:code+=f'        {"new(" if trainer else ""}"{key}"{")" if trainer else ""},\n'
        code+='    }.ToImmutableHashSet('+('' if trainer else 'StringComparer.Ordinal')+');\n'
    code+='}\n'
    return docs,raw,{'src/Game.Domain/Trainers/FightingDojoTrainerSource.cs':code.encode()}

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('dependency',type=Path);p.add_argument('output',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw,files=generate(a.reference,a.dependency)
    files.update({'content/generated/data/regions/fighting-dojo/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()) for n,d in docs.items()})
    files.update({'content/raw/pokefirered/037335f4/'+n:data for n,data in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-fighting-dojo-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=5,battles=5,members=11,owned=154,opponents=186,machines=41,newCells=82),indent=2))
if __name__=='__main__':main()
