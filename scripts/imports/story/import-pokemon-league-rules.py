#!/usr/bin/env python3
"""Extract only the first League teams from pinned Git blobs; never execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers
from runtime_json import runtime_json_bytes
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-pokemon-league-trainers-v1'
TRAINERS = ['ELITE_FOUR_LORELEI','ELITE_FOUR_BRUNO','ELITE_FOUR_AGATHA','ELITE_FOUR_LANCE',
            'CHAMPION_FIRST_SQUIRTLE','CHAMPION_FIRST_BULBASAUR','CHAMPION_FIRST_CHARMANDER']
MISSING = ['MACHAMP','GENGAR','HITMONLEE','HITMONCHAN','RHYDON','JYNX','AERODACTYL','DRAGONAIR','DRAGONITE']

def need(value, message):
    if not value: raise ValueError(message)

def generate(reference, dependency):
    raw, inputs, texts, dependencies = {}, {}, {}, []
    def read(path):
        if path not in texts:
            data = read_source(reference, path, COMMIT)
            raw[path]=data; texts[path]=data.decode()
            inputs[path]=dict(path=path)
        return texts[path]
    def one(path, pattern):
        matches=list(re.finditer(pattern,read(path),re.M|re.S))
        need(len(matches)==1,'Expected one source match: '+path+' '+pattern)
        return matches[0]
    def ref(path,symbol,match):
        return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,match.start())+1,
                    endLine=read(path).count('\n',0,match.end())+1)
    def number(path,symbol):
        value=one(path,r'^#define '+symbol+r'\s+([^\n]+)')[1].split('//')[0].strip()
        return int(value,0) if re.fullmatch(r'(?:0x[0-9a-fA-F]+|\d+)',value) else 1<<int(re.fullmatch(r'\(1 << (\d+)\)',value)[1])
    def encode(name):
        return [int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name]
    def identity(symbol):return dict(value='pret:'+symbol)
    def dependent(path):
        data=(dependency/path).read_bytes()
        dependencies.append(dict(path=path));return json.loads(data)
    canonical=dependent('content/generated/data/pokemon/canonical/catalog.json')
    baseline=dependent('scripts/recipes/story/fuchsia-gym-baseline.lock.json')
    species={s['id']['value'][5:]:s for s in canonical['species']}
    owned={'SPECIES_'+s for s in baseline['species']+['CHANSEY','SCYTHER','TAUROS','PSYDUCK','GOLDUCK','LAPRAS','KRABBY','KINGLER']}
    previous=owned|{'SPECIES_'+s for s in ['ALAKAZAM','MR_MIME','SLOWBRO','NINETALES','ARCANINE','RAPIDASH','CLOYSTER','DEWGONG','EXEGGUTOR']}
    need(len(owned)==115 and len(previous)==124,'Predecessor scope')
    trainers = selected_trainers(read, ['TRAINER_' + short for short in TRAINERS])
    battles, move_defs = [], {}
    for short, trainer in zip(TRAINERS, trainers):
        symbol = 'TRAINER_' + short
        need(trainer['partyFlags'] == 3, 'League requires held-item custom-move parties')
        for member in trainer['party']:
            sp = member['species']['value'][5:]
            need(sp in species, 'Unknown canonical trainer species: ' + sp)
            moves = [slot['value'][5:] for slot in member['customMoves']]
            need(len(moves) == 4 and len(set(moves)) == 4, 'Custom moves shape')
            for move in moves:
                mp = 'src/data/battle_moves.h'
                m = one(mp, r'^    \[' + move + r'\] =\n    \{(.*?)^    \},')
                effect = re.search(r'\.effect = (\w+),', m[1])[1]
                pp = int(re.search(r'\.pp = (\d+),', m[1])[1])
                move_defs[move] = dict(move=identity(move), sourceMoveId=number('include/constants/moves.h', move),
                    pp=pp, effect=effect, source=ref(mp, move, m))
        champion=short.startswith('CHAMPION');room='Champions' if champion else short.split('_')[-1].title()+'s'
        prefix='PokemonLeague_'+room+'Room';label=prefix+'_EventScript_Battle'+(short.split('_')[-1].title() if champion else '')
        script_path='data/maps/'+prefix+'/scripts.inc';script=one(script_path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        need(script[1].strip()=='trainerbattle_no_intro '+symbol+', '+prefix+'_Text_Defeat\n\treturn','Trainer script differs')
        need('call_if_unset FLAG_SYS_CAN_LINK_WITH_RS, '+prefix+'_EventScript_Battle' in read(script_path),'First/rematch flag gate differs')
        battles.append(dict(trainer=identity(symbol),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT'),
            rivalFlags=0,battleTypeFlags=number('include/constants/battle.h','BATTLE_TYPE_TRAINER'),scriptEntry=label,introText='',defeatText=prefix+'_Text_Defeat',victoryText='',
            postBattleText='',entryScript='EventScript_DoNoIntroTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(script_path,label,script),
            postBattleScript=None,musicOption=None))
    required={m['species']['value'][5:] for t in trainers for m in t['party']}
    missing=sorted(required-previous,key=lambda s:species[s]['sourceSpeciesId'])
    need(missing==['SPECIES_'+s for s in MISSING],'New opponent closure differs')
    need(len(trainers)==7 and len(required)==25 and sum(len(t['party']) for t in trainers)==38,'League scope differs')
    need([t['sourceTrainerId'] for t in trainers]==[410,411,412,413,438,439,440],'Trainer IDs differ')
    for i,t in enumerate(trainers):
        need(t['aiFlags']==7 and not t['doubleBattle'] and t['baseMoney']==25,'Trainer parameters differ')
        need(t['items']==['ITEM_FULL_RESTORE']*(2 if i<4 else 4)+(['ITEM_NONE']*2 if i<4 else []),'Medicine differs')
        need(len(t['party'])==(5 if i<4 else 6) and all(m['sourceIv']==(250 if i<4 else 255) for m in t['party']),'Party parameters differ')
        need([m['heldItem'] for m in t['party']]==['ITEM_NONE']*(len(t['party'])-1)+['ITEM_SITRUS_BERRY'],'Held items differ')
    # Authenticate the exact mechanics and callback source which this admission reuses.
    for path,_,pattern in [
        ('src/battle_main.c','CreateNPCTrainerParty',r'^static u8 CreateNPCTrainerParty\([^\n]*\)\n\{.*?^\}'),
        ('src/battle_setup.c','BattleSetup_GetTerrainId',r'^u8 BattleSetup_GetTerrainId\(void\)\n\{.*?^\}'),
        ('src/battle_setup.c','StartTrainerBattle',r'^void StartTrainerBattle\(void\)\n\{.*?^\}'),
        ('src/battle_setup.c','CB2_EndTrainerBattle',r'^static void CB2_EndTrainerBattle\(void\)\n\{.*?^\}'),
        ('src/battle_script_commands.c','Cmd_getmoneyreward',r'^static void Cmd_getmoneyreward\(void\)\n\{.*?^\}'),
        ('data/scripts/trainer_battle.inc','EventScript_DoNoIntroTrainerBattle',r'^EventScript_DoNoIntroTrainerBattle::\n((?:(?!^\w+::).)*)')]:
        one(path,pattern)
    for path in ['src/pokemon.c','src/battle_ai_switch_items.c','src/data/pokemon/item_effects.h','src/data/items.json',
                 'src/data/pokemon/species_info.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/level_up_learnset_pointers.h',
                 'src/field_specials.c','include/constants/battle.h','asm/macros/event.inc']:
        read(path)
    starter_source=one('src/field_specials.c',r'^static const u16 sStarterSpecies\[\] = \{(.*?)^\};')
    need(re.findall(r'SPECIES_\w+',starter_source[1])==['SPECIES_BULBASAUR','SPECIES_SQUIRTLE','SPECIES_CHARMANDER'],'Player starter order differs')
    for value,suffix in [(0,'Charmander'),(1,'Bulbasaur'),(2,'Squirtle')]:
        need('call_if_eq VAR_STARTER_MON, '+str(value)+', PokemonLeague_ChampionsRoom_EventScript_Battle'+suffix in read('data/maps/PokemonLeague_ChampionsRoom/scripts.inc'),'Champion starter mapping differs')
    outputs={'trainers.json':dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=move_defs),
        'pokemon.json':dict(
            ownedSpeciesAdded=[],opponentSpecies=[dict(id=species[s]['id'],evolutions=species[s]['evolutions']) for s in missing])}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for n in ['reference','dependency','output']:p.add_argument(n,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw=generate(a.reference,a.dependency)
    files={'content/generated/data/regions/pokemon-league/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else runtime_json_bytes(d, sort_keys=True)) for n,d in docs.items()}
    files.update({'content/raw/pokefirered/037335f4/'+n:d for n,d in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-pokemon-league-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=7,members=38,trainerSpecies=25,newOpponentSpecies=9,newOwnedSpecies=0,
        moves=len(docs['trainers.json']['resolvedMoves']),sourceInputs=len(raw),files=len(files)),indent=2))
if __name__=='__main__':main()
