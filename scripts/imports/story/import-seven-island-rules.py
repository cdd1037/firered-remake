#!/usr/bin/env python3
"""Import exact Seven Island battle catalogs from pinned Git blobs, without running upstream code.
Use REFERENCE DEPENDENCY OUTPUT [--check]. All source inputs and artifacts are locked.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import read_source, selected_trainers, resolved_parties
from runtime_json import runtime_json_bytes
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse
import json
import re
import subprocess
from pathlib import Path
from machine_catalog import compatibility_additions

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
TRAINERS = ('CRUSH_GIRL_CYNDY', 'TAMER_EVAN', 'PKMN_RANGER_JACKSON', 'PKMN_RANGER_KATELYN', 'COOLTRAINER_LEROY', 'COOLTRAINER_MICHELLE', 'COOL_COUPLE_LEX_NYA', 'AROMA_LADY_MIAH', 'YOUNG_COUPLE_EVE_JON', 'PKMN_RANGER_NICOLAS', 'PKMN_RANGER_MADELINE', 'JUGGLER_MASON', 'RUIN_MANIAC_BRANDON', 'RUIN_MANIAC_BENJAMIN', 'PAINTER_EDNA', 'GENTLEMAN_CLIFFORD', 'PSYCHIC_DARIO', 'PSYCHIC_RODETTE')


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
    base=dependent('scripts/recipes/story/five-island-predecessor.json')
    five_p=dependent('content/generated/data/regions/five-island/pokemon.json')
    celio=dependent('content/generated/data/pokemon/celio/pokemon.json')
    cave=dependent('content/generated/data/pokemon/cerulean-cave/pokemon.json')
    rematch=dependent('content/generated/data/regions/league-rematch/pokemon.json')
    species={s['id']['value'][5:]:s for s in canonical['species']}
    metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
    for trainer in trainers:
        need(all(member['species']['value'][5:] in species for member in trainer['party']),
             'Unknown canonical trainer species')
    previous_owned={s[5:] for s in base['ownedSpecies']}|set(five_p['ownedSpeciesAdded'])|set(celio['speciesAdded'])|set(cave['speciesAdded'])
    previous_opponents={s[5:] for s in base['opponentSpecies']}|previous_owned|set(five_p['opponentSpeciesAdded'])|{s['id']['value'][5:] for s in rematch['opponentSpecies']}
    additions=['SPECIES_PHANPY','SPECIES_DONPHAN','SPECIES_LARVITAR','SPECIES_PUPITAR','SPECIES_TYRANITAR','SPECIES_SKARMORY','SPECIES_UNOWN']
    trainer_only=['SPECIES_GIRAFARIG','SPECIES_HITMONTOP','SPECIES_MILTANK','SPECIES_PINECO','SPECIES_SMEARGLE','SPECIES_URSARING']
    need(len(previous_owned)==139 and len(previous_opponents)==168,'Exact Cave/Rematch predecessor')
    need(len(previous_owned|set(additions))==146 and len(previous_opponents|set(additions)|set(trainer_only))==180,'Exact Seven closure')
    edges=[]
    for name,level,target in [('SPECIES_PHANPY',25,'SPECIES_DONPHAN'),('SPECIES_LARVITAR',30,'SPECIES_PUPITAR'),('SPECIES_PUPITAR',55,'SPECIES_TYRANITAR')]:
        e=metadata[name]['sourceEvolutions'];need(len(e)==1 and e[0]['method']=='EVO_LEVEL' and e[0]['parameterValue']==level and e[0]['target']==identity(target),'Source evolution')
        edges.append(dict(species=identity(name),sourceEvolution=e[0]))
    battles=[]
    names=['SevenIsland_SevaultCanyon','SevenIsland_SevaultCanyon_Entrance','SevenIsland_TanobyRuins','SevenIsland_TrainerTower']
    for name in names:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for actor in mapping['object_events']:
            if actor.get('trainer_type')!='TRAINER_TYPE_NORMAL':continue
            label=actor['script'];path='data/scripts/trainers.inc'
            block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            command=re.search(r'^\t(trainerbattle_single|trainerbattle_double) ([^\n]+)$',block[1],re.M)
            need(command is not None,'First trainer command')
            args=command[2].split(', ');double=command[1]=='trainerbattle_double'
            need(args[0][8:] in TRAINERS,'Source trainer scope')
            post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
            battle=dict(trainer=identity(args[0]),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None)
            if double:battle['notEnoughMonsText']=args[3]
            battles.append(battle)
    need(len(trainers)==18 and len(battles)==20 and sum(t['doubleBattle'] for t in trainers)==2,'18 identities, 20 actor scripts, two doubles')
    wild=json.loads(read('src/data/wild_encounters.json'))
    encounters=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map'].startswith('MAP_SEVEN_ISLAND_') and e['base_label'].endswith('_FireRed')]
    fields=next(g['fields'] for g in wild['wild_encounter_groups'] if 'fields' in g)
    need(len(encounters)==11,'Exactly eleven FireRed encounter maps')
    admitted={s['species'] for e in encounters for kind in ['land_mons','water_mons','rock_smash_mons','fishing_mons'] for s in e.get(kind,{}).get('mons',[])[:2 if kind=='fishing_mons' else None]}
    need(admitted-previous_owned=={'SPECIES_PHANPY','SPECIES_LARVITAR','SPECIES_SKARMORY','SPECIES_UNOWN'},'Minimal direct wild additions')
    all_wild={s['species'] for e in encounters for kind in ['land_mons','water_mons','rock_smash_mons','fishing_mons'] for s in e.get(kind,{}).get('mons',[])}
    held=[dict(species=identity(s),common=metadata[s]['itemCommon'],rare=metadata[s]['itemRare']) for s in sorted(all_wild)]
    cells=compatibility_additions(ROOT, 'content/generated/data/regions/seven-island/machines.json')
    need(len(cells)==39, 'Exact admitted machine scope')
    dependencies.extend(dict(path=p) for p in ['scripts/recipes/config/machine-admissions.json', 'scripts/recipes/config/machine-catalog.json'])
    for path in ['src/pokemon.c','src/battle_main.c','src/battle_setup.c','src/battle_script_commands.c','data/scripts/trainer_battle.inc','asm/macros/event.inc','include/constants/battle.h','src/data/pokemon/evolution.h','src/wild_encounter.c','include/random.h','include/constants/pokemon.h']:read(path)
    docs={
      'trainers.json':dict(completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),
      'pokemon.json':dict(previousOwnedSpeciesCount=139,ownedSpeciesCount=146,previousOpponentSpeciesCount=168,opponentSpeciesCount=180,ownedSpeciesAdded=additions,trainerOnlySpeciesAdded=trainer_only,evolutionEdges=edges,species=[dict(definition=dict(id=species[s]['id'],evolutions=species[s]['evolutions']),metadata=metadata[s]) for s in additions+trainer_only]),
      'encounters.json':dict(sourcePath='src/data/wild_encounters.json',encounters=encounters,fields=fields,heldItems=held,runtimeMethods=['land','surf','oldRod'],retainedMetadataOnlyMethods=['rockSmash','goodRod','superRod']),
      'machines.json':dict(previousSpeciesCount=139,speciesCount=146,machineCount=39,additions=cells,itemsAdded=[]),
    }
    items_text=read('src/data/items.json');source_items=json.loads(items_text)['items']
    constants=read('include/constants/items.h');constant={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',constants,re.M)}
    def item_metadata(symbol):
     i=next(i for i in source_items if i['itemId']==symbol)
     match=re.search(r'"itemId":\s*"'+symbol+r'"',items_text);line=items_text[:match.start()].count('\n')+1
     r=dict(path='src/data/items.json',symbol=symbol,startLine=line,endLine=line)
     value=shared_item(symbol)
     return dict(item=value,description=i['description_english'].replace('\\n','\n'))
    new_items=[]
    for m in [item_metadata(n) for n in ['ITEM_KINGS_ROCK','ITEM_LUCKY_PUNCH','ITEM_HEART_SCALE']]:
        i=m['item']
        new_items.append(dict(item=i)|dict(description=m['description'], healHp=0, cureStatus=0, effectBytes=[], useSupport=10))
    shop_path='data/maps/SevenIsland_Mart/scripts.inc'
    stock=one(shop_path,r'^SevenIsland_Mart_Items::\n((?:\t\.2byte ITEM_\w+\n)+)')
    stock_items=re.findall(r'\t\.2byte (ITEM_\w+)',stock[1]);need(stock_items.pop()=='ITEM_NONE' and len(stock_items)==9,'Source nine-item shop')
    shop=dict(id='SevenIsland_Mart_Items',items=[identity(s) for s in stock_items])
    flags=read('include/constants/flags.h')
    def flag_value(symbol):
        m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Pickup flag')
        expr=m[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
        add=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(0x[0-9A-Fa-f]+|\d+)\)',expr);need(add,'Flag syntax')
        return flag_value(add[1])+int(add[2],0)
    pickups=[]
    def pickup(mapping,event,hidden,local_id,item,quantity):
        flag=event['flag'];info=item_metadata(item)['item']
        need(quantity==1 and (not hidden or not event['underfoot']),'Pickup quantity/underfoot')
        pickups.append(dict(id='seven-island:'+flag,mapSymbol=mapping['id'],hidden=hidden,sourceLocalId=local_id,x=event['x'],y=event['y'],elevation=event['elevation'],script='EventScript_HiddenItemScript' if hidden else event['script'],itemSymbol=item,sourceItemId=info['sourceItemId'],item=info['id'],itemName=info['name'],pocket=info['pocket'],quantity=quantity,flag=flag,sourceFlagId=flag_value(flag)))
    for name in names+['SevenIsland_SevaultCanyon_House']:
        mapping=json.loads(read('data/maps/'+name+'/map.json'))
        for local_id,event in enumerate(mapping['object_events'],1):
            if event.get('graphics_id')!='OBJ_EVENT_GFX_ITEM_BALL':continue
            match=one('data/scripts/item_ball_scripts.inc',r'^'+event['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n')
            pickup(mapping,event,False,local_id,match[1],int(match[2] or 1))
        for event in mapping['bg_events']:
            if event['type']=='hidden_item':pickup(mapping,event,True,None,event['item'],event['quantity'])
    need(len(pickups)==13 and sum(p['hidden'] for p in pickups)==9,'13 source pickups')
    item_names=sorted({p['itemSymbol'] for p in pickups}|set(stock_items))
    docs['items.json']=dict(items=[item_metadata(n) for n in item_names],additions=new_items,shop=shop,pickups=pickups,renewableFlags=[p['flag'] for p in pickups if p['itemSymbol']=='ITEM_HEART_SCALE' or p['mapSymbol']=='MAP_SEVEN_ISLAND_TRAINER_TOWER' and p['itemSymbol'] in ['ITEM_BIG_PEARL','ITEM_PEARL']])
    need(len(docs['items.json']['renewableFlags'])==6,'Six inherited renewable flags')
    for path in ['data/scripts/obtain_item.inc','src/renewable_hidden_items.c','include/constants/hold_effects.h']:read(path)

    return docs

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('reference',type=Path);p.add_argument('output',type=Path);p.add_argument('--dependency-root',required=True,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();docs=generate(a.reference,a.dependency_root)
    files={'content/generated/data/regions/seven-island/'+n:(trainer_json(a.output/'content/generated', d, check=a.check) if n == 'trainers.json' else runtime_json_bytes(d, sort_keys=True, default=item_json)) for n,d in docs.items()}
    lock=dict(sourceCommit=COMMIT,profile='firered-seven-island-rules-v1',files=[dict(path=k) for k,v in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,trainers=18,battles=20,owned=146,opponents=180,machines=39),indent=2, default=item_json))
if __name__=='__main__':main()
