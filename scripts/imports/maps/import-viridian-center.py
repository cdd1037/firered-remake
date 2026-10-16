#!/usr/bin/env python3
"""Authenticated, local Git-blob-only Viridian Pokemon Center source increment."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure
from map_source import LockedSource, render_map, connections_and_warps, object_pairs, json_bytes, require_safe_target
from map_runtime import movement
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
from overworld_sprites import graphics_records
from map_runtime import compact_world as _compact_world, encode as _encode_runtime

import argparse
import base64
import collections
import io
import json
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile
from PIL import Image
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-viridian-center-import-v1'
MAPS = [('ViridianCity_PokemonCenter_1F', 'map.viridian-pokemon-center-1f', 'viridian-pokemon-center-1f')]
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_PokemonCenter': 'secondary/pokemon_center'}


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')) + '\n'


def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/story/viridian-center.lock.json', maps=MAPS, profile=PROFILE)


class Source(LockedSource):
    def __init__(self, raw, lock_path=None):
        super().__init__(raw, read_lock(lock_path))


def import_map(src, name, stable_id, out):
    return render_map(src, name, stable_id, out, tilesets=TILESETS, render_border=False)


def script_inventory(src, worlds):
    parsed = parse_scripts(src, capture_macros=False)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'EventScript_AfterWhiteOutHeal'])
    required = {'ViridianCity_PokemonCenter_1F_OnTransition',
                'ViridianCity_PokemonCenter_1F_EventScript_Nurse'}
    if not required <= roots:
        raise ValueError('Critical center entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='notCertifiedForCenter')
    result.pop("localMacros")
    return result


def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


def materialize(raw, reference_repo=None, lock_path=None):
    """Read only immutable Git blobs, or verify an already materialized local source set."""
    raw = pathlib.Path(raw)
    lock = read_lock(lock_path)
    tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        tree = {}
        for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
            if row:
                fields, name = row.split(b'\t')
                tree[name.decode()] = tuple(fields.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = raw / path
        require_safe_target(raw, target)
        if tree is not None:
            data = git('cat-file', 'blob', COMMIT + ':' + entry['path'])
        else:
            data = target.read_bytes()
        verified[path] = data
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=COMMIT,  inputCount=len(verified))
    return manifest


def constants_inventory(src, world):
    values = {}
    for path in sorted(src.data):
        if not path.startswith('include/constants/'):
            continue
        for match in re.finditer(r'^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$', src.text(path), re.M):
            name, token = match.groups()
            value = int(token, 16 if token.startswith('0x') else 10)
            if name in values and values[name]['value'] != value:
                raise ValueError('Conflicting source constant: ' + name)
            values[name] = dict(value=value, sourceRef=src.ref(path, name, src.text(path)[:match.start()].count('\n') + 1))
    path = 'src/data/heal_locations.json'
    table = src.json(path)['heal_locations']
    template = src.text('src/data/heal_locations.constants.json.txt')
    if 'HEAL_LOCATION_NONE,' not in template or '{{ heal_location.id }},' not in template:
        raise ValueError('Unsupported heal-location enumeration template')
    for i, row in enumerate(table, 1):
        values[row['id']] = dict(value=i, sourceRef=src.ref(path, row['id']))
    generator = src.text('tools/mapjson/mapjson.cpp')
    if 'json_to_string(obj_event, "local_id") << " " << i + 1' not in generator:
        raise ValueError('Unsupported local object ID generation')
    for i, obj in enumerate(world['events']['object_events'], 1):
        if 'local_id' in obj:
            values[obj['local_id']] = dict(value=i, sourceRef=src.ref(world['sourceRef']['path'], obj['local_id']))
    return dict(schemaVersion=1, values=values)


def service_inventory(src, scripts, world, constants):
    blocks = {b['label']: b for b in scripts['blocks']}
    def instructions(label):
        return [x['text'].strip() for x in blocks[label]['lines'] if x['text'].strip() and not x['text'].strip().startswith('@')]
    expected = ['applymovement VAR_LAST_TALKED, Common_Movement_WalkInPlaceFasterLeft', 'waitmovement 0',
                'dofieldeffect FLDEFF_POKECENTER_HEAL', 'waitfieldeffect FLDEFF_POKECENTER_HEAL',
                'applymovement VAR_LAST_TALKED, Common_Movement_WalkInPlaceFasterDown', 'waitmovement 0',
                'special HealPlayerParty', 'return']
    if instructions('EventScript_PkmnCenterNurse_TakeAndHealPkmn') != expected:
        raise ValueError('Original nurse healing order differs')
    if instructions('ViridianCity_PokemonCenter_1F_OnTransition') != ['setrespawn HEAL_LOCATION_VIRIDIAN_CITY', 'end']:
        raise ValueError('Original entry respawn script differs')
    if instructions('CableClub_OnResume') != ['special InitUnionRoom', 'end']:
        raise ValueError('Original resume script differs')
    if 'case 1 EventScript_PkmnCenterNurse_Goodbye' not in instructions('EventScript_PkmnCenterNurse'):
        raise ValueError('Original optional-comma case syntax differs')
    heal = next(x for x in src.json('src/data/heal_locations.json')['heal_locations'] if x['id'] == 'HEAL_LOCATION_VIRIDIAN_CITY')
    if (constants['values'][heal['id']]['value'], heal['x'], heal['y'], heal['respawn_npc']) != (2, 26, 27, 'LOCALID_VIRIDIAN_NURSE'):
        raise ValueError('Viridian heal destination differs')
    return dict(schemaVersion=1,
                mapScripts='ViridianCity_PokemonCenter_1F_MapScripts',
                onTransition='ViridianCity_PokemonCenter_1F_OnTransition', onResume='CableClub_OnResume',
                nurseEntry='ViridianCity_PokemonCenter_1F_EventScript_Nurse',
                whiteoutEntry='EventScript_AfterWhiteOutHeal', healLocation=heal,
                healLocationSourceRef=src.ref('src/data/heal_locations.json', heal['id']),
                orderedHealingInstructions=expected,
                specials=scripts['specials'],
                fieldEffect=dict(symbol='FLDEFF_POKECENTER_HEAL', value=25, native='FldEff_PokecenterHeal',
                                 sourceRef=src.ref('src/field_effect.c', 'FldEff_PokecenterHeal')),
                boundaries=[dict(id='secondFloor', status='unsupported', sourceWarpId=3,
                                 destinationMap='MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F', destinationWarpId=0),
                            dict(id='linkPlay', status='unsupported', sourceEntry='CableClub_OnResume'),
                            dict(id='pokemonStorage', status='unsupported', behavior='MB_PC')])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.viridian-center-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        name, stable, directory = MAPS[0]
        world = import_map(src, name, stable, stage / directory)
        connections, warps = connections_and_warps(src, [world])
        for warp in warps:
            warp['runtimeStatus'] = 'existingCityDestination' if warp['destinationMap'] == 'MAP_VIRIDIAN_CITY' else 'unsupportedSecondFloor'
        scripts = script_inventory(src, [world])
        constants = constants_inventory(src, world)
        npcs = npc_inventory(src, [world], stage / 'npc')
        service = service_inventory(src, scripts, world, constants)
        catalog = dict(schemaVersion=1,  commit=COMMIT,

                       map=dict(sourceName=name, stableId=stable, sourceMapSymbol=world['events']['id'], directory=directory),
                       connections=connections, warps=warps)
        for filename, document in package_documents(generated_root(out), constants, {'center-catalog.json': catalog, 'scripts.json': scripts,
                                    'npc-source.json': npcs, 'service.json': service}).items():
            (stage / filename).write_text(canonical(document), encoding='utf-8')
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,


                         outputs=outputs)
        (stage / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
        for path in sorted(stage.rglob('*')):
            require_safe_target(out, out / path.relative_to(stage))
        for path in sorted(stage.rglob('*')):
            destination = out / path.relative_to(stage)
            if path.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, destination)
    print(json.dumps({k: v for k, v in manifest.items() if k != 'outputs'}, sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/maps/regions/viridian-center')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        help='Materialize locked Git blobs from this local repository; never read its worktree or run its scripts.')
    args = parser.parse_args()
    if args.reference_repo is not None:
        materialize(args.raw, args.reference_repo)
    import_catalog(args.raw, args.out)
