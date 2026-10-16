#!/usr/bin/env python3
"""Authenticated local Route5/6, Underground Path and Vermilion source import; no gameplay activation."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure

from shared_constants import package_documents
from map_runtime import generated_root, publish_map_package
import argparse
import collections
import io
import struct
from PIL import Image
import importlib.util
import json
import os
import pathlib
import re
import shutil
import tempfile
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vermilion-region-import-v1'
MAPS = [('Route5', 'map.route-5', 'route-5'),
        ('Route5_SouthEntrance', 'map.route-5-south-entrance', 'route-5-south-entrance'),
        ('UndergroundPath_NorthEntrance', 'map.underground-path-north-entrance', 'underground-path-north-entrance'),
        ('UndergroundPath_NorthSouthTunnel', 'map.underground-path-north-south-tunnel', 'underground-path-north-south-tunnel'),
        ('UndergroundPath_SouthEntrance', 'map.underground-path-south-entrance', 'underground-path-south-entrance'),
        ('Route6_NorthEntrance', 'map.route-6-north-entrance', 'route-6-north-entrance'),
        ('Route6', 'map.route-6', 'route-6'),
        ('VermilionCity', 'map.vermilion-city', 'vermilion-city'),
        ('VermilionCity_PokemonCenter_1F', 'map.vermilion-city-pokemon-center-1f', 'vermilion-city-pokemon-center-1f'),
        ('VermilionCity_Mart', 'map.vermilion-city-mart', 'vermilion-city-mart')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_CeruleanCity': 'secondary/cerulean_city', 'gTileset_VermilionCity': 'secondary/vermilion_city',
            'gTileset_GenericBuilding1': 'secondary/generic_building_1', 'gTileset_GenericBuilding2': 'secondary/generic_building_2',
            'gTileset_UndergroundPath': 'secondary/underground_path', 'gTileset_PokemonCenter': 'secondary/pokemon_center',
            'gTileset_Mart': 'secondary/mart'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_vermilion_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='vermilion-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/vermilion-region.lock.json', maps=MAPS, profile=PROFILE)

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    parsed = parse_scripts(src)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'Std_FindItem', 'EventScript_HiddenItemScript',
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'EventScript_PC', 'Std_ReceivedItem', 'EventScript_AfterWhiteOutHeal', 'EventScript_FieldWhiteOut', 'EventScript_FieldPoison'])
    required = {'Route5_SouthEntrance_EventScript_Guard', 'Route6_NorthEntrance_EventScript_Guard',
                'VermilionCity_PokemonCenter_1F_EventScript_Nurse', 'VermilionCity_Mart_EventScript_Clerk'}
    if not required <= roots:
        raise ValueError('Critical Vermilion entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='sourceOnly')
    return result


def constants_inventory(src, worlds, scripts):
    result = SHARED.constants_inventory(src, worlds, scripts)
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
    return result

def declaration(src, path, symbol, pattern):
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))


def encounter_inventory(src):
    path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    rows = [e for e in group['encounters'] if e['base_label'] in ('sRoute5_FireRed', 'sRoute6_FireRed')]
    if len(rows) != 2 or any(len(e['land_mons']['mons']) != 12 for e in rows):
        raise ValueError('Original FireRed route encounters differ')
    return dict(schemaVersion=1, gameVersion='FIRERED', revision=0,
                fields=group['fields'], maps=[dict(declaration=e, sourceRef=src.ref(path, e['base_label'])) for e in rows],
                landSpecies=sorted({m['species'] for e in rows for m in e['land_mons']['mons']}),
                deferredWaterAndFishingSpecies=sorted({m['species'] for e in rows for k in ('water_mons', 'fishing_mons') for m in e.get(k,{}).get('mons',[])}),
                waterAndFishingStatus='Original tables retained; Surf and fishing require separate runtime support')


def regional_connections_and_warps(src, worlds):
    # Original Route6 connects to Saffron's connection-only view with non-reciprocal
    # offsets (0 versus +12). Retain the source quirk as an unadmitted boundary.
    normal, exceptional = [], []
    for world in worlds:
        copy = dict(world, events=dict(world['events']))
        if world['events']['id'] == 'MAP_ROUTE6':
            original = next(c for c in world['events']['connections'] if c['map'] == 'MAP_SAFFRON_CITY_CONNECTION')
            path = 'data/maps/SaffronCity_Connection/map.json'
            reverse = next(c for c in src.json(path)['connections'] if c['map'] == 'MAP_ROUTE6')
            if original != dict(map='MAP_SAFFRON_CITY_CONNECTION',offset=0,direction='up') or reverse != dict(map='MAP_ROUTE6',offset=12,direction='down'):
                raise ValueError('Original Saffron connection-view offset exception differs')
            copy['events']['connections'] = [c for c in world['events']['connections'] if c != original]
            exceptional.append(dict(sourceMap=world['events']['id'],sourceConnectionIndex=0,**original,
                runtimeStatus='externalBoundary',coordinateRule='destination transverse coordinate = source transverse coordinate - offset',
                sourceRef=world['sourceRef'],reciprocalSource=reverse,reciprocalSourceRef=src.ref(path,'MAP_SAFFRON_CITY_CONNECTION'),
                sourceOffsetException='Original connection-only Saffron view uses non-reciprocal Route6 offsets; boundary remains unadmitted'))
        normal.append(copy)
    connections, warps = connections_and_warps(src, normal)
    for c in connections:
        if c['sourceMap'] == 'MAP_ROUTE6': c['sourceConnectionIndex'] = 1
    return sorted(connections+exceptional,key=lambda c:(c['sourceMap'],c['sourceConnectionIndex'])), warps


def regional_npcs(src, worlds, out):
    normal, invisible = [], []
    for world in worlds:
        copy = dict(world, events=dict(world['events'])); copy['events']['object_events'] = []
        for index, event in enumerate(world['events']['object_events'], 1):
            if event['graphics_id'] == '0':
                if event['movement_type'] != 'MOVEMENT_TYPE_INVISIBLE':
                    raise ValueError('Unknown zero-graphics actor')
                invisible.append(dict(map=world['events']['id'], localId=index, declaration=event,
                    sourceRef=world['sourceRef'], runtimeStatus='sourceInvisibleActor'))
            else:
                copy['events']['object_events'].append(event)
        normal.append(copy)
    result = npc_inventory(src, normal, out); result['invisibleActors'] = invisible
    return result


def dependency_inventory(src, worlds, scripts, constants, connections, warps, encounters):
    trainer_symbols = sorted({token for block in scripts['blocks'] if block['label'].startswith('Route6_')
        for line in block['lines'] for token in re.findall(r'\bTRAINER_[A-Z0-9_]+\b', line['text'])})
    trainers=[]
    for symbol in trainer_symbols:
        record=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\] = \{\n.*?^    \},')
        party=re.search(r'\.party = (\w+)\((\w+)\)',record['sourceText'])
        if not party: raise ValueError('Unknown trainer party type')
        record.update(sourceTrainerId=constants['values'][symbol]['value'],partyFormat=party[1],runtimeStatus='sourceOnly')
        record['party']=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{\n.*?^\};')
        record['species']=sorted(set(re.findall(r'\bSPECIES_[A-Z0-9_]+\b',record['party']['sourceText'])))
        trainers.append(record)
    if len(trainers) != 6: raise ValueError('Original Route6 initial trainer inventory differs')
    city=src.json('data/maps/CeruleanCity/map.json');route5=next(w for w in worlds if w['events']['id']=='MAP_ROUTE5')
    south=next(c for c in city['connections'] if c['map']=='MAP_ROUTE5');north=next(c for c in route5['events']['connections'] if c['map']=='MAP_CERULEAN_CITY')
    if south != dict(map='MAP_ROUTE5',offset=0,direction='down') or north != dict(map='MAP_CERULEAN_CITY',offset=0,direction='up'):
        raise ValueError('Cerulean/Route5 original offsets differ')
    heal = next(l for l in src.json('src/data/heal_locations.json')['heal_locations'] if l['id']=='HEAL_LOCATION_VERMILION_CITY')
    mart = next(b for b in scripts['blocks'] if b['label']=='VermilionCity_Mart_Items')
    stock = [re.search(r'ITEM_[A-Z0-9_]+',l['text'])[0] for l in mart['lines'] if '.2byte ITEM_' in l['text']]
    if stock != ['ITEM_POKE_BALL','ITEM_SUPER_POTION','ITEM_ANTIDOTE','ITEM_PARALYZE_HEAL','ITEM_AWAKENING','ITEM_ICE_HEAL','ITEM_REPEL','ITEM_NONE']:
        raise ValueError('Vermilion stock order differs')
    return dict(schemaVersion=1,completeGame=False,trainers=trainers,
        trainerSpecies=sorted({s for t in trainers for s in t['species']}),encounterSpecies=encounters['landSpecies'],
        ceruleanSouthConnection=dict(sourceEdgeY=39,destinationEdgeY=0,sourceXToDestinationX=0,
            sourceConnection=south,reverseConnection=north,sourceRef=src.ref('data/maps/CeruleanCity/map.json','MAP_CERULEAN_CITY'),enabled=False),
        healLocation=dict(declaration=heal,sourceId=constants['values']['HEAL_LOCATION_VERMILION_CITY']['value'],sourceRef=src.ref('src/data/heal_locations.json','HEAL_LOCATION_VERMILION_CITY')),
        mart=dict(items=stock,sourceRef=mart['sourceRef'],runtimeIntegrated=False),
        coordinateEvents=[dict(map=w['events']['id'],index=i,declaration=e,sourceRef=w['sourceRef'],enabled=False) for w in worlds for i,e in enumerate(w['events']['coord_events'])],
        exteriorBoundaries=[c for c in connections if c['runtimeStatus']=='externalBoundary'],externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        deferredFeatures=[
            dict(feature='North entrance NPC trade',sourceScript='UndergroundPath_NorthEntrance_EventScript_Saige',reason='Source trade retained; no substitute reward or unsupported trade execution'),
            dict(feature='VS Seeker gift/rematches',sourceScript='VermilionCity_PokemonCenter_1F_EventScript_VSSeekerWoman',reason='Source reward retained; no silent gift before feature implementation'),
            dict(feature='Daycare, SS Anne, Route11, city interiors and Saffron',reason='Source-visible external boundaries; unadmitted maps remain inaccessible'),
            dict(feature='Cut, Surf, fishing, ferries and multiplayer',reason='Source declarations retained without enabling unimplemented mechanics')],
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False))


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.vermilion-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=regional_connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=regional_npcs(src,worlds,stage/'npc');encounters=encounter_inventory(src)
        dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps,encounters)
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'vermilion-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies,'encounters.json':encounters}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/vermilion')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
