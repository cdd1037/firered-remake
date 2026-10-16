#!/usr/bin/env python3
"""Extract only the upgraded League rematch teams from pinned Git blobs; never execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from runtime_json import runtime_json_bytes
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-league-rematch-trainers-v1'
TRAINERS = ['ELITE_FOUR_LORELEI_2','ELITE_FOUR_BRUNO_2','ELITE_FOUR_AGATHA_2','ELITE_FOUR_LANCE_2',
            'CHAMPION_REMATCH_SQUIRTLE','CHAMPION_REMATCH_BULBASAUR','CHAMPION_REMATCH_CHARMANDER']
MISSING = ['CROBAT','MISDREAVUS','HERACROSS','KINGDRA','TYRANITAR']
DEPENDENCIES = (
    'content/generated/data/pokemon/celio/pokemon.json',
    'content/generated/data/regions/five-island/pokemon.json',
    'scripts/recipes/story/five-island-predecessor.json',
    'content/generated/data/pokemon/cerulean-cave/pokemon.json',
)

def need(value, message):
    if not value: raise ValueError(message)

def generate(reference, dependency):
    raw, inputs, texts, dependencies = {}, {}, {}, []
    def read(path):
        if path not in texts:
            data = subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
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
    deps={path:dependent(path) for path in DEPENDENCIES}
    celio=deps['content/generated/data/pokemon/celio/pokemon.json']
    five_pokemon=deps['content/generated/data/regions/five-island/pokemon.json']
    baseline=deps['scripts/recipes/story/five-island-predecessor.json']
    cave=deps['content/generated/data/pokemon/cerulean-cave/pokemon.json']
    species={s['id']['value'][5:]:s for s in canonical['species']}
    owned=({s[5:] for s in baseline['ownedSpecies']} | set(five_pokemon['ownedSpeciesAdded'])) | set(celio['speciesAdded']) | set(cave['speciesAdded'])
    previous={s[5:] for s in baseline['opponentSpecies']} | owned | set(five_pokemon['opponentSpeciesAdded'])
    need(len(owned)==139 and len(previous)==163 and owned<=previous,'Exact current Cave predecessor scope')
    trainers,battles,move_defs=[],[],{}
    for short in TRAINERS:
        symbol='TRAINER_'+short;path='src/data/trainers.h'
        block=one(path,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},')
        fields=dict(re.findall(r'^        \.(\w+) = (.*),$',block[1],re.M))
        party_symbol=re.fullmatch(r'ITEM_CUSTOM_MOVES\((\w+)\)',fields['party'])[1]
        party_path='src/data/trainer_parties.h'
        party=one(party_path,r'^static const struct TrainerMonItemCustomMoves '+party_symbol+r'\[\] = \{\n(.*?)^\};')
        rows=list(re.finditer(r'    \{\n(.*?)    \},\n',party[1],re.S))
        need(''.join(r[0] for r in rows)==party[1],'Unknown party syntax')
        name=re.fullmatch(r'_\("([^"]+)"\)',fields['trainerName'])[1]; encoded=encode(name)
        gender=sum(number('include/constants/trainers.h',v) if not v.isdigit() else int(v) for v in fields['encounterMusic_gender'].split(' | '))
        members=[]
        for row in rows:
            mf=dict(re.findall(r'^        \.(\w+) = (.*),$',row[1],re.M))
            need(set(mf)=={'iv','lvl','species','heldItem','moves'},'Unknown member field')
            sp=mf['species'];need(sp in species,'Unknown canonical trainer species: '+sp)
            names='src/data/text/species_names.h';nm=one(names,r'^    \['+sp+r'\] = _\("([^"]+)"\),$')
            moves=mf['moves'].strip('{}').split(', ');need(len(moves)==4 and len(set(moves))==4,'Custom moves shape')
            level,iv=int(mf['lvl']),int(mf['iv']);spname=encode(nm[1])
            members.append(dict(species=identity(sp),sourceSpeciesId=number('include/constants/species.h',sp),speciesName=nm[1],
                encodedSpeciesName=spname,level=level,sourceIv=iv,heldItem=mf['heldItem'],customMoves=[identity(m) for m in moves],
                source=ref(party_path,party_symbol,party),speciesNameSource=ref(names,sp,nm)))
            for move in moves:
                mp='src/data/battle_moves.h';m=one(mp,r'^    \['+move+r'\] =\n    \{(.*?)^    \},')
                effect=re.search(r'\.effect = (\w+),',m[1])[1];pp=int(re.search(r'\.pp = (\d+),',m[1])[1])
                move_defs[move]=dict(move=identity(move),sourceMoveId=number('include/constants/moves.h',move),pp=pp,effect=effect,source=ref(mp,move,m))
        money=one('src/battle_main.c',r'^    \{'+fields['trainerClass']+r', (\d+)\},$')
        ai=fields['aiFlags'].split(' | ');ai_flags=sum(number('include/constants/battle_ai.h',s) for s in ai)
        items=fields['items'].strip('{}').split(', ');items+=['ITEM_NONE']*(4-len(items))
        trainers.append(dict(id=identity(symbol),sourceTrainerId=number('include/constants/opponents.h',symbol),trainerClass=fields['trainerClass'],
            sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),trainerPic=fields['trainerPic'],
            sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),sourceTrainerName=name,encodedTrainerName=encoded,
            encounterMusicGender=gender,items=items,doubleBattle=fields['doubleBattle']=='TRUE',partyFlags=3,aiFlags=ai_flags,
            aiFlagSymbols=ai,baseMoney=int(money[1]),party=members,source=ref(path,symbol,block),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
        champion=short.startswith('CHAMPION');room='Champions' if champion else short.split('_')[-2].title()+'s'
        prefix='PokemonLeague_'+room+'Room';label=prefix+'_EventScript_Rematch'+(short.split('_')[-1].title() if champion else '')
        script_path='data/maps/'+prefix+'/scripts.inc';script=one(script_path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        need(script[1].strip()=='trainerbattle_no_intro '+symbol+', '+prefix+'_Text_Defeat\n\treturn','Trainer script differs')
        need('call_if_set FLAG_SYS_CAN_LINK_WITH_RS, '+prefix+'_EventScript_Rematch' in read(script_path),'First/rematch flag gate differs')
        battles.append(dict(trainer=identity(symbol),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT'),
            rivalFlags=0,battleTypeFlags=number('include/constants/battle.h','BATTLE_TYPE_TRAINER'),scriptEntry=label,introText='',defeatText=prefix+'_Text_Defeat',victoryText='',
            postBattleText='',entryScript='EventScript_DoNoIntroTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(script_path,label,script),
            postBattleScript=None,musicOption=None))
    required={m['species']['value'][5:] for t in trainers for m in t['party']}
    missing=sorted(required-previous,key=lambda s:species[s]['sourceSpeciesId'])
    need(missing==['SPECIES_'+s for s in MISSING],'New opponent closure differs')
    need(len(trainers)==7 and len(required)==25 and sum(len(t['party']) for t in trainers)==38,'League scope differs')
    need([t['sourceTrainerId'] for t in trainers]==[735,736,737,738,739,740,741],'Trainer IDs differ')
    for i,t in enumerate(trainers):
        need(t['aiFlags']==7 and not t['doubleBattle'] and t['baseMoney']==25,'Trainer parameters differ')
        need(t['items']==['ITEM_FULL_RESTORE']*(2 if i<4 else 4)+(['ITEM_NONE']*2 if i<4 else []),'Medicine differs')
        need(len(t['party'])==(5 if i<4 else 6) and all(m['sourceIv']==255 for m in t['party']),'Party parameters differ')
        berry=['ITEM_CHERI_BERRY','ITEM_PERSIM_BERRY','ITEM_CHESTO_BERRY','ITEM_PERSIM_BERRY'][i] if i<4 else 'ITEM_SITRUS_BERRY'
        need([m['heldItem'] for m in t['party']]==['ITEM_NONE']*(len(t['party'])-1)+[berry],'Held items differ')
        need(t['encounterMusicGender']==(10 if i<4 else 0),'Source gender/music byte differs')
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
                 'src/field_specials.c','include/constants/battle.h','asm/macros/event.inc', 'src/random.c','include/random.h','include/constants/pokemon.h']:
        read(path)
    starter_source=one('src/field_specials.c',r'^static const u16 sStarterSpecies\[\] = \{(.*?)^\};')
    need(re.findall(r'SPECIES_\w+',starter_source[1])==['SPECIES_BULBASAUR','SPECIES_SQUIRTLE','SPECIES_CHARMANDER'],'Player starter order differs')
    for value,suffix in [(0,'Charmander'),(1,'Bulbasaur'),(2,'Squirtle')]:
        need('call_if_eq VAR_STARTER_MON, '+str(value)+', PokemonLeague_ChampionsRoom_EventScript_Rematch'+suffix in read('data/maps/PokemonLeague_ChampionsRoom/scripts.inc'),'Champion starter mapping differs')
    # Validate the source RNG and no-shiny rules used by the shared trainer factory.
    need('#define Random32() (Random() | (Random() << 16))' in read('include/random.h'), 'Random32 order')
    need('#define RAND_MULT 1103515245' in read('include/random.h') and
         '#define ISO_RANDOMIZE1(val)(RAND_MULT * (val) + 24691)' in read('include/random.h'), 'Source RNG')
    need('} while (shinyValue < SHINY_ODDS);' in read('src/pokemon.c') and
         re.search(r'^#define SHINY_ODDS\s+8(?:\s|$)',read('include/constants/pokemon.h'),re.M), 'Source no-shiny rejection')
    outputs={'trainers.json':dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=move_defs),
        'pokemon.json':dict(
            ownedSpeciesAdded=[],opponentSpecies=[dict(id=species[s]['id'],evolutions=species[s]['evolutions']) for s in missing])}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for n in ['reference','dependency','output']:p.add_argument(n,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw=generate(a.reference,a.dependency)
    files={'content/generated/data/regions/league-rematch/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else runtime_json_bytes(d, sort_keys=True)) for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,profile='firered-league-rematch-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=7,members=38,trainerSpecies=25,newOpponentSpecies=5,newOwnedSpecies=0,
        moves=len(docs['trainers.json']['resolvedMoves']),sourceInputs=len(raw),files=len(files)),indent=2))
if __name__=='__main__':main()
