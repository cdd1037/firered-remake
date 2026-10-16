#!/usr/bin/env python3
"""Authenticate Route18 and both original Haden gate floors.

Pinned upstream Git blobs are inert data. Reviewed local readers/renderers retain
all terrain, trainers, triggers, warps and source scripts. This resource package
does not open Cycling Road or alter any of the nine trade definitions.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
import argparse
from collections import deque
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PACKAGE = 'maps/regions/npc-trade-haden'
RESOURCE_ID = 'firered-npc-trade-haden-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['Route18', 'Route18_EastEntrance_1F', 'Route18_EastEntrance_2F']
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
    'gTileset_CeladonCity': 'secondary/celadon_city', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}
CITY_PATH = 'content/generated/maps/regions/fuchsia-route/fuchsia-city.json'
NPC_REUSE = {'OBJ_EVENT_GFX_BOY', 'OBJ_EVENT_GFX_POLICEMAN', 'OBJ_EVENT_GFX_RED_NORMAL', 'OBJ_EVENT_GFX_ROCKER'}


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def walk_path(world, start, end, minimum_x):
    """Source-only foot geometry proof; does not claim real-command traversal."""
    cells = {(c['x'], c['y']): c for c in world['cells']}
    actors = {(a['x'], a['y']) for a in world['events']['object_events']}
    paths = {start: [start]}; queue = deque([start])
    while queue:
        here = queue.popleft()
        if here == end: return [list(p) for p in paths[here]]
        for dx, dy in ((-1, 0), (0, -1), (1, 0), (0, 1)):
            point = (here[0] + dx, here[1] + dy); cell = cells.get(point)
            if point in paths or point in actors or not cell or point[0] < minimum_x: continue
            if cell['collision'] != 0 or cell['elevation'] != 3: continue
            if cell['behaviorName'] != 'MB_NORMAL': continue
            paths[point] = paths[here] + [point]; queue.append(point)
    raise ValueError('Original bounded approach is disconnected: ' + world['sourceName'])


def contracts(src, worlds, scripts, city_bytes, links):
    route, lower, upper = worlds
    need([(w['width'], w['height']) for w in worlds] == [(60, 20), (13, 12), (13, 11)], 'Original map dimensions differ')
    need([(len(w['events']['object_events']), len(w['events']['warp_events']), len(w['events']['coord_events']),
        len(w['events']['bg_events'])) for w in worlds] == [(3, 2, 0, 2), (1, 3, 11, 0), (1, 1, 0, 2)], 'Original event cohort differs')
    actor = upper['events']['object_events'][0]
    need((actor['graphics_id'], actor['x'], actor['y'], actor['elevation'], actor['movement_type'], actor['script']) ==
        ('OBJ_EVENT_GFX_BOY', 5, 3, 3, 'MOVEMENT_TYPE_FACE_LEFT', 'Route18_EastEntrance_2F_EventScript_Haden'), 'Original Haden actor differs')
    expected_warps = [
        [dict(x=41, y=9, elevation=3, dest_map='MAP_ROUTE18_EAST_ENTRANCE_1F', dest_warp_id='0'),
         dict(x=48, y=9, elevation=3, dest_map='MAP_ROUTE18_EAST_ENTRANCE_1F', dest_warp_id='1')],
        [dict(x=1, y=6, elevation=3, dest_map='MAP_ROUTE18', dest_warp_id='0'),
         dict(x=11, y=6, elevation=3, dest_map='MAP_ROUTE18', dest_warp_id='1'),
         dict(x=9, y=10, elevation=3, dest_map='MAP_ROUTE18_EAST_ENTRANCE_2F', dest_warp_id='0')],
        [dict(x=10, y=9, elevation=3, dest_map='MAP_ROUTE18_EAST_ENTRANCE_1F', dest_warp_id='2')]]
    need([w['events']['warp_events'] for w in worlds] == expected_warps, 'Original door/stair cohort differs')
    need(route['events']['connections'] == [dict(map='MAP_ROUTE17', offset=0, direction='up'),
        dict(map='MAP_FUCHSIA_CITY', offset=-10, direction='right')], 'Original Route18 connections differ')
    need(lower['events']['connections'] is None and upper['events']['connections'] is None, 'Gate indoor connections differ')
    city = src.json('data/maps/FuchsiaCity/map.json')
    connection = dict(direction='left', map='MAP_ROUTE18', offset=10)
    need(city['connections'][1] == connection and json.loads(city_bytes)['events'] == city, 'Retained Fuchsia connection differs')
    by_label = {b['label']: b for b in scripts['blocks']}
    body = lambda label: '\n'.join(r['text'] for r in by_label[label]['lines'])
    need('call_if_eq VAR_MAP_SCENE_ROUTE16, 1, Route18_OnTransitionCyclingRoad' in body('Route18_OnTransition') and
        'special ForcePlayerOntoBike' in body('Route18_OnWarpCyclingRoad'), 'Original Route18 conditional cycling entry differs')
    need('clearflag FLAG_SYS_ON_CYCLING_ROAD' in body('Route18_EastEntrance_1F_OnTransition') and
        'call_if_set FLAG_GOT_BICYCLE, Route18_EastEntrance_1F_EventScript_DisableNeedBikeTrigger' in body('Route18_EastEntrance_1F_OnTransition'),
        'Original gate transition differs')
    need('setvar VAR_MAP_SCENE_ROUTE16, 1' in body('EventScript_SetEnteringCyclingRoad') and
        'setvar VAR_MAP_SCENE_ROUTE16, 0' in body('EventScript_SetExitingCyclingRoad'), 'Original cycling state dependencies differ')
    need('setvar VAR_0x8008, INGAME_TRADE_LICKITUNG' in body(actor['script']) and
        'setflag FLAG_DID_MARC_TRADE' in body(actor['script']), 'Original Haden trade binding differs')
    def cell(world, x, y): return world['cells'][y * world['width'] + x]
    trigger_cells = [('Route18', 48, 9, 'MB_WEST_ARROW_WARP'), ('Route18_EastEntrance_1F', 11, 6, 'MB_EAST_ARROW_WARP'),
        ('Route18_EastEntrance_1F', 9, 10, 'MB_UP_RIGHT_STAIR_WARP'), ('Route18_EastEntrance_2F', 10, 9, 'MB_DOWN_LEFT_STAIR_WARP')]
    for name, x, y, behavior in trigger_cells:
        value = cell(next(w for w in worlds if w['sourceName'] == name), x, y)
        need((value['behaviorName'], value['collision'], value['elevation']) == (behavior, 0, 3), 'Source door/stair tile differs')
    need(all(c['behaviorName'] == 'MB_NORMAL' for c in route['cells'] if c['x'] >= 47 and c['collision'] == 0 and c['elevation'] == 3 and
        (c['x'], c['y']) != (48, 9)), 'New eastern grass or unsupported tile behavior')
    paths = [dict(map=route['stableId'], start=[59, 9], end=[49, 9], cells=walk_path(route, (59, 9), (49, 9), 47)),
        dict(map=lower['stableId'], start=[10, 6], end=[8, 10], cells=walk_path(lower, (10, 6), (8, 10), 7)),
        dict(map=upper['stableId'], start=[11, 9], end=[4, 3], cells=walk_path(upper, (11, 9), (4, 3), 0))]
    dependencies = [dict(label=label, sourceRef=by_label[label]['sourceRef']) for label in [
        'Route18_OnTransition', 'Route18_OnWarp', 'Route18_OnWarpCyclingRoad', 'Route18_EastEntrance_1F_OnTransition',
        'EventScript_SetEnteringCyclingRoad', 'EventScript_SetExitingCyclingRoad', actor['script']]]
    return dict(schemaVersion=1, resourceId=RESOURCE_ID,  tradeId=5, stableMapId=upper['stableId'],
        actorLocalId=1, actor=actor, mapTopology=links,
        retainedCity=dict(path=CITY_PATH,  map='map.fuchsia-city',
            connectionIndex=1, connection=connection, geometryChanges=[], eventChanges=[]),
        expectedMapCounts=dict(predecessor=316, successor=319), scriptsOwner='npc-trades', scriptDependencies=dependencies,
        sourceCyclingState=dict(variable='VAR_MAP_SCENE_ROUTE16', foot=0, cyclingRoad=1,
            flag='FLAG_SYS_ON_CYCLING_ROAD', special='ForcePlayerOntoBike', specialAdmitted=False),
        footAccess=dict(applicationAdmissionRequired=True, sceneValue=0,
            maps=[dict(map=route['stableId'], minimumX=47, width=60, height=20),
                  dict(map=lower['stableId'], minimumX=7, width=13, height=12),
                  dict(map=upper['stableId'], minimumX=0, width=13, height=11)],
            fuchsiaEdge=dict(source=[0, 19], destination=[59, 9]),
            geometryOnlyPaths=paths, doorStairCells=[dict(sourceMap=n, x=x, y=y, behavior=b) for n,x,y,b in trigger_cells],
            originalWestDoorPreserved=True, terrainChanges=[]),
        preservedDeferredTrainers=[dict(localId=i, actor=a) for i,a in enumerate(route['events']['object_events'], 1)],
        changesToTradeDefinitions=[], speciesAdded=[], itemsAdded=[])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref = [Path(os.path.abspath(p)) for p in (args.project_root, args.dependency_root, args.reference_root)]
    for path in (target, dep, ref): safe(path)
    need(not(target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'haden_reader')
    helper = module(dep / 'scripts/imports/maps/import-fossil-lab-region.py', 'haden_helper')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    helper.MAPS, helper.NPC_REUSE = MAPS, NPC_REUSE
    src = reader.Source(ref); worlds = [reader.decode(src, name) for name in MAPS]
    scripts = helper.script_inventory(src, reader, worlds)
    constants = helper.constants_inventory(src, scripts, worlds)
    for name in MAPS:
        for leaf in ('scripts.inc', 'text.inc'): src.read(f'data/maps/{name}/{leaf}', 'embedded-map-source')
    src.read('data/maps/map_groups.json', 'source-map-number-projection')
    links = reader.topology(src, worlds)
    need(len(links['warps']) == 6 and len(links['connections']) == 2 and all(c['reciprocal'] for c in links['connections']), 'Original map topology differs')
    retained = dep/CITY_PATH; safe(retained); city_bytes = retained.read_bytes()
    contract = contracts(src, worlds, scripts, city_bytes, links)
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='haden-', dir=target) as folder:
        stage = Path(folder)
        for world in worlds:
            shared.import_map(src, world['sourceName'], world['stableId'], stage/reader.directory(world['sourceName']))
        npcs = helper.reuse_npcs(src, shared, worlds, stage, dep)
        entries = []
        for world in worlds:
            relative = reader.directory(world['sourceName']); rich_path = stage/relative/'runtime-map.json'; rich = rich_path.read_bytes()
            compact = converter.normalize_runtime(json.loads(rich)); helper.compact_check(compact, world, src)
            encoded = converter.encode(compact); (stage/relative/'runtime-map.json').write_bytes(encoded)
            entries.append(dict(sourceName=world['sourceName'], stableId=world['stableId'], directory=relative,
                sourceMapSymbol=world['events']['id'], path=relative+'/runtime-map.json'))
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False); lock_bytes = canonical(lock)
        docs = package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1, resourceId=RESOURCE_ID,
             maps=entries, tilesets=TILESETS),
            'contracts.json':contract, 'npc-source.json':npcs, 'scripts.json':scripts,
            'geometry.json':dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage/name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
            for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,  resourceId=RESOURCE_ID,

               directedWarpCount=6, coordinateEventCount=11,
            backgroundEventCount=4, reusedNpcGraphicsCount=4,
             outputs=outputs)
        manifest_bytes = canonical(manifest); (stage/'manifest.json').write_bytes(manifest_bytes)
        writes = []
        writes += [(target/'content/raw/pokefirered/037335f4'/p, b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: '+str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict( inputCount=len(src.entries), outputCount=len(outputs),
            scriptBlockCount=len(scripts['blocks']), mapCount=3, cellRoundtripCount=1499, borderCellRoundtripCount=12,
            reusedNpcGraphicsCount=4, check=args.check), indent=2))


if __name__ == '__main__': main()
