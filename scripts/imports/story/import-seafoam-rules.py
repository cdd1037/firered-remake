#!/usr/bin/env python3
"""Import exact Seafoam/Route19/20 catalogs from pinned Git blobs, without running upstream code.
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
TRAINERS = ('SWIMMER_MALE_REECE', 'SWIMMER_MALE_RICHARD', 'SWIMMER_MALE_TONY', 'SWIMMER_MALE_MATTHEW', 'SWIMMER_MALE_DOUGLAS', 'SWIMMER_MALE_DAVID', 'SWIMMER_MALE_AXLE', 'SWIMMER_FEMALE_ALICE', 'SWIMMER_FEMALE_CONNIE', 'SWIMMER_FEMALE_ANYA', 'SIS_AND_BRO_LIA_LUC', 'SWIMMER_FEMALE_MELISSA', 'PICNICKER_MISSY', 'SWIMMER_FEMALE_NORA', 'BIRD_KEEPER_ROGER', 'SWIMMER_MALE_DEAN', 'PICNICKER_IRENE', 'SWIMMER_MALE_BARRY', 'SWIMMER_MALE_DARRIN', 'SWIMMER_FEMALE_SHIRLEY', 'SWIMMER_FEMALE_TIFFANY')
MAPS = {"Route19": "map.route-19", "Route20": "map.route-20", **{"SeafoamIslands_" + f: "map.seafoam-islands-" + f.lower() for f in ("1F", "B1F", "B2F", "B3F", "B4F")}}
BASE = "content/generated/data/regions/seafoam/"
RAW = "content/raw/pokefirered/037335f4/"
DEPENDENCIES = (
    'content/generated/data/pokemon/canonical/catalog.json',
    'content/generated/data/regions/npc-trade-owned/pokemon.json',
)


SHARED_INPUTS = {'asm/macros/event.inc': 'content/raw/pokefirered/037335f4/asm/macros/event.inc', 'charmap.txt': 'content/raw/pokefirered/037335f4/charmap.txt', 'data/maps/Route19/map.json': 'content/raw/pokefirered/037335f4/data/maps/Route19/map.json', 'data/maps/Route20/map.json': 'content/raw/pokefirered/037335f4/data/maps/Route20/map.json', 'data/scripts/trainer_battle.inc': 'content/raw/pokefirered/037335f4/data/scripts/trainer_battle.inc', 'data/scripts/trainers.inc': 'content/raw/pokefirered/037335f4/data/scripts/trainers.inc', 'include/constants/battle.h': 'content/raw/pokefirered/037335f4/include/constants/battle.h', 'include/constants/battle_ai.h': 'content/raw/pokefirered/037335f4/include/constants/battle_ai.h', 'include/constants/battle_setup.h': 'content/raw/pokefirered/037335f4/include/constants/battle_setup.h', 'include/constants/flags.h': 'content/raw/pokefirered/037335f4/include/constants/flags.h', 'include/constants/items.h': 'content/raw/pokefirered/037335f4/include/constants/items.h', 'include/constants/opponents.h': 'content/raw/pokefirered/037335f4/include/constants/opponents.h', 'include/constants/pokemon.h': 'content/raw/pokefirered/037335f4/include/constants/pokemon.h', 'include/constants/species.h': 'content/raw/pokefirered/037335f4/include/constants/species.h', 'include/constants/trainers.h': 'content/raw/pokefirered/037335f4/include/constants/trainers.h', 'include/random.h': 'content/raw/pokefirered/037335f4/include/random.h', 'src/battle_main.c': 'content/raw/pokefirered/037335f4/src/battle_main.c', 'src/battle_setup.c': 'content/raw/pokefirered/037335f4/src/battle_setup.c', 'src/data/pokemon/evolution.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/evolution.h', 'src/data/pokemon/level_up_learnset_pointers.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/level_up_learnsets.h', 'src/data/pokemon/pokedex_entries.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/species_info.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/species_info.h', 'src/data/pokemon/tmhm_learnsets.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/tmhm_learnsets.h', 'src/data/text/species_names.h': 'content/raw/pokefirered/037335f4/src/data/text/species_names.h', 'src/data/trainer_parties.h': 'content/raw/pokefirered/037335f4/src/data/trainer_parties.h', 'src/data/trainers.h': 'content/raw/pokefirered/037335f4/src/data/trainers.h', 'src/data/wild_encounters.json': 'content/raw/pokefirered/037335f4/src/data/wild_encounters.json', 'src/pokemon.c': 'content/raw/pokefirered/037335f4/src/pokemon.c', 'src/script_pokemon_util.c': 'content/raw/pokefirered/037335f4/src/script_pokemon_util.c', 'src/wild_encounter.c': 'content/raw/pokefirered/037335f4/src/wild_encounter.c'}


def need(value, message):
    if not value:
        raise ValueError(message)

def generate(reference, dependency):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = read_source(reference, path, COMMIT)
            reused = dependency / SHARED_INPUTS[path] if path in SHARED_INPUTS else None
            if reused is not None: need(reused.read_bytes() == data, "Shared source differs: " + path)
            local = reused.relative_to(dependency).as_posix() if reused else RAW + path
            inputs[path] = dict(path=path, localPath=local, shared=reused is not None)
            if reused is None: raw[path] = data
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
        dependencies.append(dict(path=path)); return json.loads(data)
    canonical,previous=[dependent(p) for p in DEPENDENCIES]
    need(previous['ownedSpeciesCount']==161 and previous['opponentSpeciesCount']==190,'Exact NPC trade ownership')
    species={s['id']['value'][5:]:s for s in canonical['species']}; metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
    additions=['SPECIES_ARTICUNO']
    need(len(species)==386,'Complete shared source mechanics')
    original=species[additions[0]]; meta=metadata[additions[0]]
    need(original['sourceSpeciesId']==144 and not original['evolutions'] and not meta['sourceEvolutions'],'Terminal Articuno source')
    battles=[];script_path='data/scripts/trainers.inc'
    for mapname in ['Route19','Route20']:
        mapping=json.loads(read('data/maps/'+mapname+'/map.json'))
        for local_id,actor in enumerate(mapping['object_events'],1):
            if actor.get('trainer_type')!='TRAINER_TYPE_NORMAL': continue
            label=actor['script']; block=one(script_path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            command=re.search(r'^\ttrainerbattle_(single|double) ([^\n]+)$',block[1],re.M); need(command,'Original ordinary battle')
            args=command[2].split(', '); double=command[1]=='double'
            need(args[0][8:] in TRAINERS and len(args)==(4 if double else 3),'Exact trainer operands')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            row=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(script_path,label,block),postBattleScript=None,musicOption=None)
            if double: row['notEnoughMonsText']=args[3]
            battles.append(row)
    need(len(trainers)==21 and len(battles)==22 and sum(t['doubleBattle'] for t in trainers)==1,'21 complete first-battle teams, 22 entries, one double')
    need(all(t['partyFlags']==0 and t['items']==['ITEM_NONE']*4 for t in trainers),'Default-move no-item original teams')
    need(all(m['heldItem']=='ITEM_NONE' for t in trainers for m in t['party']),'Original held items')
    need(all(member['species']['value'][5:] in species for trainer in trainers for member in trainer['party']),
         'Missing canonical trainer species')
    wildpath='src/data/wild_encounters.json';wild=json.loads(read(wildpath))
    by_symbol={json.loads(read('data/maps/'+n+'/map.json'))['id']:MAPS[n] for n in MAPS}
    entries=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map'] in by_symbol and e['base_label'].endswith('_FireRed')]
    need(len(entries)==7,'Seven original FireRed encounter map entries')
    fields={f['type']:f for g in wild['wild_encounter_groups'] for f in g.get('fields',[])}
    need(fields['land_mons']['encounter_rates']==[20,20,10,10,10,10,5,5,4,4,1,1] and fields['water_mons']['encounter_rates']==[60,30,5,4,1],'Original slot weights')
    tables={'land':[],'water':[],'oldRod':[],'goodRod':[],'superRodSource':[]}
    for entry in entries:
        for field in ['land_mons','water_mons','fishing_mons']:
            if field not in entry:continue
            source=entry[field];weights=fields[field]['encounter_rates']
            slots=[dict(index=i,species=identity(m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(source['mons'])]
            base=dict(map=dict(value=by_symbol[entry['map']]),rawEncounterRate=source['encounter_rate'])
            if field=='fishing_mons':
                need(len(slots)==10 and weights==[70,30,60,20,20,40,40,15,4,1],'Original fishing groups')
                for group,start,end in [('oldRod',0,2),('goodRod',2,5),('superRodSource',5,10)]:tables[group].append(dict(base,slots=slots[start:end]))
            else:tables['land' if field=='land_mons' else 'water'].append(dict(base,slots=slots))
    need([len(tables[k]) for k in tables]==[5,4,4,4,4],'Exact land/surf/rod scope')
    wild_species=sorted({s['species']['value'][5:] for group in tables.values() for t in group for s in t['slots']}|{'SPECIES_ARTICUNO'})
    held=[dict(species=identity(s),common=metadata[s]['itemCommon'],rare=metadata[s]['itemRare']) for s in wild_species]
    pairs={(s['species']['value'][5:],lv) for group in tables.values() for t in group for s in t['slots'] for lv in range(min(s['minimumLevel'],s['maximumLevel']),max(s['minimumLevel'],s['maximumLevel'])+1)}|{('SPECIES_ARTICUNO',50)}
    for s,level in sorted(pairs): natural_moves(read,s,level)
    need(natural_moves(read,'SPECIES_ARTICUNO',50)==['MOVE_MIST','MOVE_AGILITY','MOVE_MIND_READER','MOVE_ICE_BEAM'],'Original Articuno50 moves')
    art_path='data/maps/SeafoamIslands_B4F/scripts.inc';art=one(art_path,r'^SeafoamIslands_B4F_EventScript_Articuno::\n((?:(?!^\w+::).)*)')
    need('setwildbattle SPECIES_ARTICUNO, 50' in art[1] and 'special StartLegendaryBattle' in art[1],'Original static command')
    creation_path='src/script_pokemon_util.c';creation=one(creation_path,r'^void CreateScriptedWildMon\([^\n]+\)\n\{\n.*?^\}')
    need('CreateMon(&gEnemyParty[0], species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);' in creation[0],'Original static player OT random PID/IV')
    cells=compatibility_additions(dependency, 'content/generated/data/regions/seafoam/machines.json')
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/wild_encounter.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h','include/random.h','include/constants/pokemon.h','include/constants/flags.h','include/constants/items.h']:read(path)
    need('if (!(gBattleTypeFlags & (BATTLE_TYPE_POKEDUDE | BATTLE_TYPE_LEGENDARY | BATTLE_TYPE_TRAINER)))' in read('src/pokemon.c'),'Legendary held assignment bypass')
    docs={
      'trainers.json':dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(previousOwnedSpeciesCount=161,ownedSpeciesCount=162,previousOpponentSpeciesCount=190,opponentSpeciesCount=191,ownedSpeciesAdded=additions,opponentSpeciesAdded=additions,trainerOnlySpeciesAdded=[],evolutions=[]),
      'machines.json':dict(previousSpeciesCount=161,speciesCount=162,machineCount=41,additions=cells,itemsAdded=[]),
    }

    code='using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Trainers;\n\n// Generated from the 21 pinned Route19/20 first-battle teams and 22 source actor entries.\npublic static partial class SeafoamTrainerPartyFactory\n{\n'
    for name,rows in [('TrainerIds',[t['id']['value'] for t in trainers]),('BattleScripts',[b['scriptEntry'] for b in battles])]:
        trainer=name=='TrainerIds';typ='TrainerId' if trainer else 'string'
        code+=f'    public static readonly ImmutableHashSet<{typ}> {name} = '+(
            'ImmutableHashSet.CreateRange<TrainerId>([\n' if trainer else 'ImmutableHashSet.Create<string>(StringComparer.Ordinal,\n')
        for index,key in enumerate(rows):
            code+=f'        {"new(" if trainer else ""}"{key}"{")" if trainer else ""}'
            code+=(',\n' if index < len(rows)-1 else (']);\n' if trainer else ');\n'))
    code+='}\n'
    files={'src/Game.Domain/Trainers/SeafoamTrainerSource.cs':code.encode()}
    code='using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record SeafoamHeldItemSource(SpeciesId Species, string Common, string Rare);\npublic static class SeafoamEncounterTables\n{\n'
    for key,name,typ in [('land','Land','EarlyLandEncounterTable'),('water','Water','WaterEncounterTable'),('oldRod','OldRod','OldRodEncounterTable'),('superRodSource','SuperRodSource','OldRodEncounterTable')]:
        code+=f'    public static ImmutableArray<{typ}> {name} {{ get; }} = [\n'
        for row in tables[key]:
            args=[f'new("{row["map"]["value"]}")']
            args.append(str(row['rawEncounterRate']))
            slots=', '.join(f'new({s["index"]},new("{s["species"]["value"]}"),{s["minimumLevel"]},{s["maximumLevel"]},{s["weight"]})' for s in row['slots'])
            code+='        new('+', '.join(args)+', ['+slots+']),\n'
        code+='    ];\n'
    code+='    public static ImmutableArray<SeafoamHeldItemSource> HeldItems { get; } = [\n'+''.join(f'        new(new("{r["species"]["value"]}"),"{r["common"]}","{r["rare"]}"),\n' for r in held)+'    ];\n}\n'
    files['src/Game.Domain/Encounters/SeafoamEncounterTables.cs']=code.encode()
    return docs,raw,files


def main():
    p=argparse.ArgumentParser(description=__doc__)
    for arg in ['reference','dependency','output']:p.add_argument(arg,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw,files=generate(a.reference,a.dependency)
    files.update({BASE+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else (json.dumps(d,indent=2,ensure_ascii=False)+'\n').encode()) for n,d in docs.items()})
    files.update({RAW+n:data for n,data in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile='firered-seafoam-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=21,battles=22,members=sum(len(t['party']) for t in docs['trainers.json']['trainers']),owned=162,opponents=191,machines=41,newCells=41,newRawInputs=len(raw)),indent=2))
if __name__=='__main__':main()
