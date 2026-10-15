#!/usr/bin/env python3
"""Extract only the upgraded League rematch teams from pinned Git blobs; never execute upstream code."""
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
    'content/generated/celio-pokemon/pokemon.json',
    'content/generated/five-island-rules/pokemon.json',
    'scripts/five-island-predecessor.json',
    'content/generated/cerulean-cave-pokemon/pokemon.json',
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
    canonical=dependent('content/generated/canonical-pokemon/catalog.json')
    deps={path:dependent(path) for path in DEPENDENCIES}
    celio=deps['content/generated/celio-pokemon/pokemon.json']
    five_pokemon=deps['content/generated/five-island-rules/pokemon.json']
    baseline=deps['scripts/five-island-predecessor.json']
    cave=deps['content/generated/cerulean-cave-pokemon/pokemon.json']
    species={s['id']['value'][5:]:s for s in canonical['species']}
    owned=({s[5:] for s in baseline['ownedSpecies']} | set(five_pokemon['ownedSpeciesAdded'])) | set(celio['speciesAdded']) | set(cave['speciesAdded'])
    previous={s[5:] for s in baseline['opponentSpecies']} | owned | set(five_pokemon['opponentSpeciesAdded'])
    need(len(owned)==139 and len(previous)==163 and owned<=previous,'Exact current Cave predecessor scope')
    trainers,battles,identities,move_defs=[],[],[],{}
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
        members,derived=[],[];name_hash=0
        for row in rows:
            mf=dict(re.findall(r'^        \.(\w+) = (.*),$',row[1],re.M))
            need(set(mf)=={'iv','lvl','species','heldItem','moves'},'Unknown member field')
            sp=mf['species'];p=species[sp]
            names='src/data/text/species_names.h';nm=one(names,r'^    \['+sp+r'\] = _\("([^"]+)"\),$')
            moves=mf['moves'].strip('{}').split(', ');need(len(moves)==4 and len(set(moves))==4,'Custom moves shape')
            level,iv=int(mf['lvl']),int(mf['iv']);spname=encode(nm[1]);name_hash+=sum(encoded)+sum(spname)
            pid=((0x78 if gender&128 else 0x88)+(name_hash<<8))&0xffffffff
            ratio=p['genderRatio'];actual_gender='genderless' if ratio==255 else 'female' if ratio==254 or ratio!=0 and (pid&255)<ratio else 'male'
            derived.append(dict(species=identity(sp),level=level,sourceIv=iv,fixedIv=iv*31//255,personality=pid,nature=pid%25,
                abilitySlot=pid&1,ability=p['ability2'] if pid&1 and p['ability2']!='ABILITY_NONE' else p['ability1'],gender=actual_gender,
                heldItem=mf['heldItem'],customMoves=[identity(m) for m in moves]))
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
        identities.append(dict(trainer=identity(symbol),members=derived,basePrize=4*int(money[1])*members[-1]['level']))
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
    mechanics=[]
    for path,symbol,pattern in [
        ('src/battle_main.c','CreateNPCTrainerParty',r'^static u8 CreateNPCTrainerParty\([^\n]*\)\n\{.*?^\}'),
        ('src/battle_setup.c','BattleSetup_GetTerrainId',r'^u8 BattleSetup_GetTerrainId\(void\)\n\{.*?^\}'),
        ('src/battle_setup.c','StartTrainerBattle',r'^void StartTrainerBattle\(void\)\n\{.*?^\}'),
        ('src/battle_setup.c','CB2_EndTrainerBattle',r'^static void CB2_EndTrainerBattle\(void\)\n\{.*?^\}'),
        ('src/battle_script_commands.c','Cmd_getmoneyreward',r'^static void Cmd_getmoneyreward\(void\)\n\{.*?^\}'),
        ('data/scripts/trainer_battle.inc','EventScript_DoNoIntroTrainerBattle',r'^EventScript_DoNoIntroTrainerBattle::\n((?:(?!^\w+::).)*)')]:
        match=one(path,pattern);mechanics.append(ref(path,symbol,match))
    for path in ['src/pokemon.c','src/battle_ai_switch_items.c','src/data/pokemon/item_effects.h','src/data/items.json',
                 'src/data/pokemon/species_info.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/level_up_learnset_pointers.h',
                 'src/field_specials.c','include/constants/battle.h','asm/macros/event.inc', 'src/random.c','include/random.h','include/constants/pokemon.h']:
        read(path)
    starter_source=one('src/field_specials.c',r'^static const u16 sStarterSpecies\[\] = \{(.*?)^\};')
    need(re.findall(r'SPECIES_\w+',starter_source[1])==['SPECIES_BULBASAUR','SPECIES_SQUIRTLE','SPECIES_CHARMANDER'],'Player starter order differs')
    for value,suffix in [(0,'Charmander'),(1,'Bulbasaur'),(2,'Squirtle')]:
        need('call_if_eq VAR_STARTER_MON, '+str(value)+', PokemonLeague_ChampionsRoom_EventScript_Rematch'+suffix in read('data/maps/PokemonLeague_ChampionsRoom/scripts.inc'),'Champion starter mapping differs')
    champion_map=[dict(playerStarter=identity('SPECIES_BULBASAUR'),sourceStarterValue=0,trainer=identity('TRAINER_CHAMPION_REMATCH_CHARMANDER')),
                 dict(playerStarter=identity('SPECIES_SQUIRTLE'),sourceStarterValue=1,trainer=identity('TRAINER_CHAMPION_REMATCH_BULBASAUR')),
                 dict(playerStarter=identity('SPECIES_CHARMANDER'),sourceStarterValue=2,trainer=identity('TRAINER_CHAMPION_REMATCH_SQUIRTLE'))]
    # These independent construction vectors consume the original low/high Random32
    # draws and retry the whole OT ID if CreateBoxMon's shiny test is below eight.
    need('#define Random32() (Random() | (Random() << 16))' in read('include/random.h'), 'Random32 order')
    need('#define RAND_MULT 1103515245' in read('include/random.h') and
         '#define ISO_RANDOMIZE1(val)(RAND_MULT * (val) + 24691)' in read('include/random.h'), 'Source RNG')
    need('} while (shinyValue < SHINY_ODDS);' in read('src/pokemon.c') and
         re.search(r'^#define SHINY_ODDS\s+8(?:\s|$)',read('include/constants/pokemon.h'),re.M), 'Source no-shiny rejection')
    def draw(seed):
        seed=(seed*1103515245+24691)&0xffffffff
        return seed,seed>>16
    def first_shiny_seed(pid):
        for seed in range(1000000):
            s,lo=draw(seed);s,hi=draw(s)
            if hi^lo^(pid>>16)^(pid&0xffff)<8:return seed
        raise ValueError('No finite source rejection fixture')
    generation_cases=[]
    for team in identities:
        for initial in [42,first_shiny_seed(team['members'][0]['personality'])]:
            seed=initial;draws=0;ots=[];raw_draws=[]
            for member in team['members']:
                while True:
                    seed,lo=draw(seed);seed,hi=draw(seed);draws+=2;raw_draws.extend([lo,hi])
                    pid=member['personality']
                    if hi^lo^(pid>>16)^(pid&0xffff)>=8:break
                ots.append(lo|(hi<<16))
            generation_cases.append(dict(trainer=team['trainer'],initialSeed=initial,finalSeed=seed,
                draws=draws,rawDraws=raw_draws,originalTrainerIds=ots))
    rules=dict(sourceCommit=COMMIT,profile='firered-league-rematch-rules-v1',ownedSpeciesCount=139,previousOpponentSpeciesCount=163,
        opponentSpeciesCount=168,predecessorProfile='firered-cerulean-cave-opponents-v1',ownedSpeciesAdded=[],trainerOnlySpeciesAdded=missing,trainerSpecies=sorted(required),
        trainerMoves=sorted(move_defs),identities=identities,generationCases=generation_cases,championForPlayerStarter=champion_map,
        mode=3,battleTypeFlags=8,mechanicsTerrain='BATTLE_TERRAIN_BUILDING',rematchSwitchFlag='FLAG_SYS_CAN_LINK_WITH_RS',requiredFlagValue=True,
        moneyRule='4 * finalPartyMemberLevel * trainerClassMoney * moneyMultiplier',mechanicsSources=mechanics,
        limitations=['Only upgraded League rematch teams; first-clear and all other trainer IDs are rejected.',
          'Opponent-only species never grant capture, ownership, evolution or wild encounter admission.',
          'Mode 3 reuses ordinary trainer AI, medicine, EXP and money. Story flags, doors, Hall of Fame and whiteout routing belong to Application.',
          'Source trainer names (TERRY for champion) determine personality; display rival name does not replace this source construction.'])
    outputs={'trainers.json':dict(sourceCommit=COMMIT,profile=PROFILE,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=move_defs),
        'pokemon.json':dict(sourceCommit=COMMIT,profile='firered-league-rematch-opponents-v1',
            ownedSpeciesAdded=[],opponentSpecies=[dict(id=species[s]['id'],evolutions=species[s]['evolutions']) for s in missing]),'rules.json':rules,
        'source-input-hashes.json':dict(sourceCommit=COMMIT,files=[inputs[k] for k in sorted(inputs)],predecessorInputs=dependencies)}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    for n in ['reference','dependency','output']:p.add_argument(n,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw=generate(a.reference,a.dependency)
    files={'content/generated/league-rematch-rules/'+n:(json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode() for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,profile='firered-league-rematch-rules-v1',
        files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=7,members=38,trainerSpecies=25,newOpponentSpecies=5,newOwnedSpecies=0,
        moves=len(docs['rules.json']['trainerMoves']),sourceInputs=len(raw),files=len(files)),indent=2))
if __name__=='__main__':main()
