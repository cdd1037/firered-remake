#!/usr/bin/env python3
"""Authenticated local S.S. Anne source maps and progression contract; no gameplay activation."""
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
import pathlib
import re
import shutil
import tempfile
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-ssanne-region-import-v1'
MAP_NAMES = tuple([f'SSAnne_1F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,8)])] +
    [f'SSAnne_2F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,7)])] +
    ['SSAnne_3F_Corridor'] + [f'SSAnne_B1F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,6)])] +
    ['SSAnne_CaptainsOffice','SSAnne_Deck','SSAnne_Exterior','SSAnne_Kitchen'])
def map_directory(name):
    suffix = name.removeprefix('SSAnne_').replace('CaptainsOffice','Captains_Office')
    suffix = re.sub(r'Room(\d+)',r'Room_\1',suffix)
    return 'ssanne-' + suffix.replace('_','-').lower()
MAPS = [(n,'map.'+map_directory(n),map_directory(n)) for n in MAP_NAMES]
TILESETS = {'gTileset_General':'primary/general', 'gTileset_VermilionCity':'secondary/vermilion_city', 'gTileset_SSAnne':'secondary/ss_anne'}
EXTERNAL_ROOTS = ['VermilionCity_MapScripts', 'VermilionCity_EventScript_CheckTicketLeft',
    'VermilionCity_EventScript_CheckTicketRight','VermilionCity_EventScript_ExitedTicketCheck']
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_ssanne_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='ssanne-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/ssanne-region.lock.json', maps=MAPS, profile=PROFILE)

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
    roots.update(EXTERNAL_ROOTS)
    roots.add('EventScript_CutTree')
    required = {'SSAnne_CaptainsOffice_EventScript_Captain','SSAnne_Exterior_ExitSSAnne',
                'SSAnne_2F_Corridor_EventScript_RivalTrigger','SSAnne_1F_Room6_EventScript_Woman'}
    if not required <= roots: raise ValueError('Critical S.S. Anne entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='sourceOnly')
    return result


def constants_inventory(src, worlds, scripts):
    result = SHARED.constants_inventory(src, worlds, scripts)
    # The post-departure pier retry writes the original port value even before Sevii travel exists.
    # Keep its numeric dependencies source-backed; importing constants does not admit ferry routes.
    expected = {'SEAGALLOP_VERMILION_CITY': 0, 'VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F': 0x4076,
                'VAR_MAP_SCENE_VERMILION_CITY': 0x407e, 'VAR_VERMILION_CITY_TICKET_CHECK_TRIGGER': 0x4053,
                'NPC_TEXT_COLOR_MALE': 0, 'LOCALID_PLAYER': 255, 'LOCALID_VERMILION_FERRY_SAILOR': 6}
    for name, value in expected.items():
        if result['values'].get(name, {}).get('value') != value:
            raise ValueError('Post-departure pier constant differs: ' + name)
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
    return result

def declaration(src, path, symbol, pattern):
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))


def regional_npcs(src, worlds, out):
    # Every source local ID is retained; no event filtering or renumbering.
    return npc_inventory(src, worlds, out)


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.ssanne-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=regional_npcs(src,worlds,stage/'npc')
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'ssanne-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs}).items():
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
    retired=out/'dependencies.json'
    require_safe_target(out,retired)
    retired.unlink(missing_ok=True)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/ssanne')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
