#!/usr/bin/env python3
"""Import exact Seafoam/Route19/20 catalogs from pinned raw source, without running upstream code.
Use --raw-root RAW --out PROJECT [--check]. Only live final resources are emitted.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))

from shared_trainers import trainer_json
from trainer_import import selected_trainers
from world_source import WorldSource
import argparse
import json
import re
from pathlib import Path


TRAINERS = ('SWIMMER_MALE_REECE', 'SWIMMER_MALE_RICHARD', 'SWIMMER_MALE_TONY', 'SWIMMER_MALE_MATTHEW', 'SWIMMER_MALE_DOUGLAS', 'SWIMMER_MALE_DAVID', 'SWIMMER_MALE_AXLE', 'SWIMMER_FEMALE_ALICE', 'SWIMMER_FEMALE_CONNIE', 'SWIMMER_FEMALE_ANYA', 'SIS_AND_BRO_LIA_LUC', 'SWIMMER_FEMALE_MELISSA', 'PICNICKER_MISSY', 'SWIMMER_FEMALE_NORA', 'BIRD_KEEPER_ROGER', 'SWIMMER_MALE_DEAN', 'PICNICKER_IRENE', 'SWIMMER_MALE_BARRY', 'SWIMMER_MALE_DARRIN', 'SWIMMER_FEMALE_SHIRLEY', 'SWIMMER_FEMALE_TIFFANY')
MAPS = {"Route19": "map.route-19", "Route20": "map.route-20", **{"SeafoamIslands_" + f: "map.seafoam-islands-" + f.lower() for f in ("1F", "B1F", "B2F", "B3F", "B4F")}}
def need(value, message):
    if not value:
        raise ValueError(message)

def generate(raw_root):
    source = WorldSource(Path(raw_root))
    read = source.text
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
    held=[]
    for symbol in wild_species:
        match=one('src/data/pokemon/species_info.h', r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},')
        fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$', match[1], re.M))
        held.append(dict(species=identity(symbol), common=fields['itemCommon'], rare=fields['itemRare']))
    docs = {'trainers.json': dict(trainers=trainers, battles=battles)}

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
    return docs,files


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw-root',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True,help='Project output root for final JSON and C# resources')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    docs,files=generate(args.raw_root)
    files.update({'content/generated/data/regions/seafoam/'+name:
                  trainer_json(args.out/'content/generated', document, check=args.check)
                  for name,document in docs.items()})
    for name,data in files.items():
        target=args.out/name
        if args.check:
            need(target.read_bytes()==data,'Regeneration differs: '+name)
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(checked=args.check,trainers=len(docs['trainers.json']['trainers']),
                          battles=len(docs['trainers.json']['battles']))))

if __name__=='__main__':main()
