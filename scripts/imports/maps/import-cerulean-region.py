#!/usr/bin/env python3
"""Authenticated local Cerulean City and ten city interiors import; no gameplay activation."""
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
PROFILE = 'firered-cerulean-region-import-v1'
MAPS = [(name, 'map.' + slug, slug) for name, slug in [
    ('CeruleanCity', 'cerulean-city'), ('CeruleanCity_PokemonCenter_1F', 'cerulean-city-pokemon-center-1f'),
    ('CeruleanCity_PokemonCenter_2F', 'cerulean-city-pokemon-center-2f'), ('CeruleanCity_Mart', 'cerulean-city-mart'),
    ('CeruleanCity_Gym', 'cerulean-city-gym'), ('CeruleanCity_BikeShop', 'cerulean-city-bike-shop'),
    *[(f'CeruleanCity_House{i}', f'cerulean-city-house-{i}') for i in range(1, 6)]]]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
    'gTileset_CeruleanCity': 'secondary/cerulean_city', 'gTileset_CeruleanGym': 'secondary/cerulean_gym',
    'gTileset_PokemonCenter': 'secondary/pokemon_center', 'gTileset_Mart': 'secondary/mart',
    'gTileset_BikeShop': 'secondary/bike_shop', 'gTileset_BurgledHouse': 'secondary/burgled_house',
    'gTileset_GenericBuilding1': 'secondary/generic_building_1', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_cerulean_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='cerulean-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/cerulean-region.lock.json', maps=MAPS, profile=PROFILE)

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
    required = {'CeruleanCity_OnTransition', 'CeruleanCity_Gym_EventScript_MistyDefeated', 'CeruleanCity_PokemonCenter_1F_OnTransition'}
    if not required <= roots:
        raise ValueError('Critical Cerulean entry labels are missing')
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

def regional_warps(src, worlds):
    # Multiplayer rooms return through a saved dynamic warp, not a fabricated reciprocal warp.
    normal = []
    for world in worlds:
        copy = dict(world, events=dict(world['events']))
        copy['events']['warp_events'] = [e for e in world['events']['warp_events'] if e['dest_map'] not in ('MAP_UNION_ROOM', 'MAP_TRADE_CENTER')]
        normal.append(copy)
    connections, warps = connections_and_warps(src, normal)
    for world in worlds:
        for index, event in enumerate(world['events']['warp_events']):
            if event['dest_map'] not in ('MAP_UNION_ROOM', 'MAP_TRADE_CENTER'): continue
            name = {'MAP_UNION_ROOM': 'UnionRoom', 'MAP_TRADE_CENTER': 'TradeCenter'}[event['dest_map']]
            path = f'data/maps/{name}/map.json'; target = src.json(path)
            destination_index = int(event['dest_warp_id']); destination = target['warp_events'][destination_index]
            if destination['dest_map'] != 'MAP_DYNAMIC' or destination['dest_warp_id'] != 'WARP_ID_DYNAMIC':
                raise ValueError('Expected original multiplayer dynamic return')
            cell = world['cells'][event['y'] * world['width'] + event['x']]
            warps.append(dict(sourceMap=world['events']['id'], sourceWarpId=index, source=event,
                destinationMap=target['id'], destinationWarpId=destination_index, destinationX=destination['x'], destinationY=destination['y'],
                behaviorName=cell['behaviorName'], behaviorId=cell['behaviorId'], cellCollision=cell['collision'], cellElevation=cell['elevation'],
                runtimeStatus='externalDynamicBoundary', ordinaryWarpCandidate=False, dynamicReturn=destination,
                destinationSourceRef=src.ref(path, target['id'])))
    return connections, sorted(warps, key=lambda w: (w['sourceMap'], w['sourceWarpId']))

def regional_npcs(src, worlds, out):
    normal, dynamic, clones = [], [], []
    for world in worlds:
        copy = dict(world, events=dict(world['events'])); copy['events']['object_events'] = []
        for index, event in enumerate(world['events']['object_events'], 1):
            if event['graphics_id'] == '0':
                dynamic.append(dict(map=world['events']['id'], localId=index, declaration=event,
                    sourceRef=world['sourceRef'], runtimeStatus='requiresDynamicGossiperGraphicsHandler'))
            else: copy['events']['object_events'].append(event)
            if event.get('type') == 'clone':
                target_path = 'data/maps/Route9/map.json'
                matches = [e for e in src.json(target_path)['object_events'] if e.get('local_id') == event['target_local_id']]
                if len(matches) != 1: raise ValueError('Missing cloned Route9 object')
                clones.append(dict(map=world['events']['id'], localId=index, declaration=event, target=matches[0],
                    sourceRef=src.ref(target_path, event['target_local_id']), runtimeStatus='externalBoundary'))
        normal.append(copy)
    result = npc_inventory(src, normal, out); result['dynamicGraphics'] = dynamic; result['clones'] = clones
    return result

def dependency_inventory(src, worlds, scripts, constants, connections, warps):
    def refmatch(path, symbol, match):
        return src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1)
    def declaration(path, symbol, pattern):
        matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
        if len(matches) != 1: raise ValueError('Missing/ambiguous declaration: ' + symbol)
        return dict(symbol=symbol, sourceText=matches[0][0], sourceRef=refmatch(path, symbol, matches[0]))
    trainer_symbols = sorted({token for block in scripts['blocks'] if block['sourceRef']['path'].startswith('data/maps/CeruleanCity')
        for line in block['lines'] for token in re.findall(r'\bTRAINER_(?:LEADER_MISTY|PICNICKER_DIANA|SWIMMER_MALE_LUIS|RIVAL_CERULEAN_\w+|TEAM_ROCKET_GRUNT_5)\b', line['text'])})
    trainers=[]
    for symbol in trainer_symbols:
        record = declaration('src/data/trainers.h', symbol, r'^    \[' + symbol + r'\] = \{\n.*?^    \},')
        party = re.search(r'\.party = (\w+)\((\w+)\)', record['sourceText'])
        if not party: raise ValueError('Unknown trainer party type')
        record['sourceTrainerId'] = constants['values'][symbol]['value']
        record['partyFormat'] = party[1]
        record['party'] = declaration('src/data/trainer_parties.h', party[2], r'^static const struct \w+ ' + party[2] + r'\[\] = \{\n.*?^\};')
        record['runtimeStatus'] = 'sourceOnly'; trainers.append(record)
    heal=next(l for l in src.json('src/data/heal_locations.json')['heal_locations'] if l['id']=='HEAL_LOCATION_CERULEAN_CITY')
    city=next(w for w in worlds if w['events']['id']=='MAP_CERULEAN_CITY')
    route4=src.json('data/maps/Route4/map.json')
    route4_layout=next(l for l in src.json('data/layouts/layouts.json')['layouts'] if l.get('id')==route4['layout'])
    west=next(c for c in city['events']['connections'] if c['map']=='MAP_ROUTE4')
    east=next(c for c in route4['connections'] if c['map']=='MAP_CERULEAN_CITY')
    if west != dict(map='MAP_ROUTE4',offset=10,direction='left') or east != dict(map='MAP_CERULEAN_CITY',offset=-10,direction='right'):
        raise ValueError('Route4/Cerulean original offsets differ')
    return dict(schemaVersion=1,   completeGame=False,
        trainers=trainers, healLocation=dict(table=heal, sourceRef=src.ref('src/data/heal_locations.json',heal['id'])),
        route4Connection=dict(sourceEdgeX=route4_layout['width']-1,destinationEdgeX=0,sourceYToDestinationY=10,
            sourceConnection=east, reverseConnection=west, sourceRef=src.ref('data/maps/Route4/map.json','MAP_ROUTE4'),enabled=False),
        coordinateEvents=[dict(map=w['events']['id'],index=i,declaration=e,sourceRef=w['sourceRef'],enabled=False) for w in worlds for i,e in enumerate(w['events']['coord_events'])],
        exteriorBoundaries=[c for c in connections if c['runtimeStatus']=='externalBoundary'],
        externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        progressionDependencies=[
            dict(feature='city exits',sourceScript='CeruleanCity_EventScript_BlockExits',requires='FLAG_GOT_SS_TICKET',scope='Bill on Route25 is deferred; preserve original police and Slowbro blockers'),
            dict(feature='Cascade Badge',sourceScript='CeruleanCity_Gym_EventScript_MistyDefeated',requires='Misty battle, badge/respawn flags, gym trainer completion and TM03 capacity retry'),
            dict(feature='north rival',sourceScript='CeruleanCity_EventScript_Rival',requires='starter-dependent battle, movements, fame checker reward and scene persistence'),
            dict(feature='robbery',sourceScript='CeruleanCity_EventScript_Grunt',requires='Team Rocket battle and TM28 capacity retry'),
            dict(feature='bike shop',sourceScript='CeruleanCity_BikeShop_EventScript_Clerk',requires='Bike Voucher from Vermilion and bicycle handling'),
            dict(feature='houses',requires='Dontae in-game trade, badge explanation, Berry Powder and Wonder News services'),
            dict(feature='Center 2F',requires='original cable/wireless/Mystery Gift handlers; multiplayer room exits disabled'),
            dict(feature='city edges',requires='Route24/25 and Bill next; Route5/9, Cut/Surf, Cerulean Cave are gated externally')],
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False))


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out); require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()): raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.cerulean-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp)
        worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=regional_warps(src,worlds)
        scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=regional_npcs(src,worlds,stage/'npc');dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps)
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'cerulean-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')): require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir(): dest.mkdir(parents=True,exist_ok=True)
            else: dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True))
    return manifest

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/cerulean')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize: materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
