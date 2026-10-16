#!/usr/bin/env python3
"""Locked, local-only Viridian increment: inspectable data and renderings, never a playable-world claim."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure
from map_source import LockedSource, render_map, connections_and_warps, object_pairs, json_bytes, require_safe_target
from map_runtime import movement
from map_runtime import publish_map_package, generated_root

from map_runtime import compact_world as _compact_world, encode as _encode_runtime

from overworld_sprites import graphics_records
import argparse
import base64
import collections
import io
import json
import pathlib
import re
import shutil
import struct
import tempfile

from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-viridian-region-import-v1'
MAPS = [('PalletTown', 'map.pallet-town', 'pallet'),
        ('PalletTown_ProfessorOaksLab', 'map.professor-oaks-lab', 'professor-oaks-lab'),
        ('Route1', 'map.route-1', 'route-1'),
        ('ViridianCity', 'map.viridian-city', 'viridian-city'),
        ('ViridianCity_Mart', 'map.viridian-mart', 'viridian-mart')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_PalletTown': 'secondary/pallet_town', 'gTileset_ViridianCity': 'secondary/viridian_city',
            'gTileset_Lab': 'secondary/lab', 'gTileset_Mart': 'secondary/mart'}
NEW_MAPS = ['MAP_ROUTE1', 'MAP_VIRIDIAN_CITY', 'MAP_VIRIDIAN_CITY_MART']


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')) + '\n'


def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/viridian-region.lock.json', maps=MAPS, profile=PROFILE)


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
    roots.update(['Std_ObtainItem', 'Std_FindItem', 'Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo'])
    required = {'ViridianCity_Mart_EventScript_ParcelScene', 'ViridianCity_Mart_EventScript_Clerk',
                'ViridianCity_Mart_Items', 'PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene',
                'Route1_EventScript_MartClerk', 'ViridianCity_EventScript_SetOldManBlockingRoad'}
    if not required <= roots:
        raise ValueError('Critical parcel/encounter-route labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='notCertifiedForRegion')
    result.pop("localMacros")
    return result


def encounter_inventory(src, worlds):
    path = 'src/data/wild_encounters.json'
    groups = src.json(path)['wild_encounter_groups']
    group = next(x for x in groups if x['label'] == 'gWildMonHeaders' and x['for_maps'] is True)
    selected = {w['events']['id'] for w in worlds}
    records = [x for x in group['encounters'] if x['map'] in selected and x['base_label'].endswith('_FireRed')]
    if {x['map'] for x in records} != {'MAP_ROUTE1', 'MAP_PALLET_TOWN', 'MAP_VIRIDIAN_CITY'}:
        raise ValueError('FireRed region encounter selection differs')
    fields = {x['type']: x for x in group['fields']}
    for record in records:
        for kind, value in record.items():
            if kind in ('map', 'base_label'):
                continue
            if kind not in fields or len(value['mons']) != len(fields[kind]['encounter_rates']):
                raise ValueError('Encounter slot count differs from weighted source table')
            if type(value['encounter_rate']) is not int or not 0 < value['encounter_rate'] <= 255:
                raise ValueError('Invalid encounter rate')
            for mon in value['mons']:
                if any(type(mon[k]) is not int for k in ('min_level', 'max_level')) or not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                    raise ValueError('Invalid encounter level range')
    route = next(x for x in records if x['map'] == 'MAP_ROUTE1')['land_mons']
    if route['encounter_rate'] != 21 or fields['land_mons']['encounter_rates'] != [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1]:
        raise ValueError('Route 1 reviewed encounter table differs')
    return dict(schemaVersion=1, gameVersion='FIRERED', sourceRef=src.ref(path, 'gWildMonHeaders'),
                fields=group['fields'], records=records, isExecutable=False,
                semantics='encounter_rate is a raw engine rate, not a percentage per step',
                blockers=['Field encounter state and step rate/avoidance/repel/lead modifiers',
                          'Source RNG consumption and map-transition encounter continuity',
                          'Wild Pidgey/Rattata generation, moves, battle, escape, capture and field return',
                          'Surfing and all fishing profiles remain outside this playable increment'])


def quest_inventory(src, scripts):
    blocks = {x['label']: x for x in scripts['blocks']}
    parcel = blocks['ViridianCity_Mart_EventScript_ParcelScene']
    receipt = blocks['PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene']
    stock = blocks['ViridianCity_Mart_Items']
    stock_items = [re.fullmatch(r'\s*\.2byte (ITEM_\w+)\s*', x['text'])[1]
                   for x in stock['lines'] if re.fullmatch(r'\s*\.2byte (ITEM_\w+)\s*', x['text'])]
    if stock_items != ['ITEM_POKE_BALL', 'ITEM_POTION', 'ITEM_ANTIDOTE', 'ITEM_PARALYZE_HEAL', 'ITEM_NONE']:
        raise ValueError('Mart inventory differs')
    item_symbols = stock_items[:-1] + ['ITEM_OAKS_PARCEL']
    source_items = src.json('src/data/items.json')['items']
    items = []
    for symbol in item_symbols:
        matches = [x for x in source_items if x['itemId'] == symbol]
        if len(matches) != 1:
            raise ValueError('Expected unique source item: ' + symbol)
        numeric_id = re.search(r'^#define\s+' + symbol + r'\s+(\d+)\s*$', src.text('include/constants/items.h'), re.M)
        if numeric_id is None:
            raise ValueError('Item ID is not a reviewed numeric declaration')
        items.append(dict(sourceItemId=int(numeric_id[1]), source=matches[0],
                          sourceRef=src.ref('src/data/items.json', symbol)))
    required_lines = {
        parcel['label']: ['setvar VAR_MAP_SCENE_VIRIDIAN_CITY_MART, 1',
                          'giveitem_msg ViridianCity_Mart_Text_ReceivedOaksParcelFromClerk, ITEM_OAKS_PARCEL, 1, MUS_OBTAIN_KEY_ITEM',
                          'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 5'],
        receipt['label']: ['removeitem ITEM_OAKS_PARCEL', 'setflag FLAG_SYS_POKEDEX_GET',
                           'special SetUnlockedPokedexFlags',
                           'giveitem_msg PalletTown_ProfessorOaksLab_Text_ReceivedFivePokeBalls, ITEM_POKE_BALL, 5',
                           'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 6',
                           'setvar VAR_MAP_SCENE_VIRIDIAN_CITY_MART, 2',
                           'setvar VAR_MAP_SCENE_VIRIDIAN_CITY_OLD_MAN, 1',
                           'setvar VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE, 1',
                           'setvar VAR_MAP_SCENE_ROUTE22, 1']}
    transitions = []
    for label, required in required_lines.items():
        lines = [x['text'].strip() for x in blocks[label]['lines']]
        positions = [lines.index(x) for x in required]
        if positions != sorted(positions):
            raise ValueError('Parcel source effect ordering differs')
        transitions.append(dict(label=label, sourceRef=blocks[label]['sourceRef'],
                                orderedRequiredInstructions=required, originalBlock=blocks[label]['lines']))
    return dict(schemaVersion=1, executable=False, shopStock=stock_items, items=items,
                transitions=transitions,
                requiredEntryLabels=['ViridianCity_Mart_MapScripts', 'ViridianCity_Mart_OnLoad',
                                     'ViridianCity_Mart_OnFrame', parcel['label'],
                                     'ViridianCity_Mart_EventScript_Clerk',
                                     'PalletTown_ProfessorOaksLab_EventScript_ProfOak', receipt['label']],
                warnings=['Preserve original scene/giveitem ordering and source failure semantics; do not inject item/flags.',
                          'Importing complete original blocks is not script execution or whole-game reachability.'])


def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    dynamic = 'OBJ_EVENT_GFX_VAR_0' in graphics
    graphics.discard('OBJ_EVENT_GFX_VAR_0')
    graphics.update(['OBJ_EVENT_GFX_RED_NORMAL', 'OBJ_EVENT_GFX_OLD_MAN_1', 'OBJ_EVENT_GFX_OLD_MAN_LYING_DOWN'])
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records,
                dynamicGraphics=[dict(graphicsId='OBJ_EVENT_GFX_VAR_0', variable='VAR_OBJ_GFX_ID_0',
                                      candidates=['OBJ_EVENT_GFX_OLD_MAN_LYING_DOWN', 'OBJ_EVENT_GFX_OLD_MAN_1'],
                                      sourceScript='ViridianCity_OnTransition', runtimeStatus='requiresVariableBinding')] if dynamic else [])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    # Staging protects previous valid outputs from any source/parser/render failure.
    with tempfile.TemporaryDirectory(prefix='.viridian-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        encounters = encounter_inventory(src, worlds)
        quest = quest_inventory(src, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        catalog = dict(schemaVersion=1,  commit=COMMIT,
                        isPlayable=False,
                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=w['events']['id'], directory=directory,
                                  role='newMap' if w['events']['id'] in NEW_MAPS else 'existingPalletReference')
                             for (name, stable, directory), w in zip(MAPS, worlds)],
                       connections=connections, warps=warps,
                       scope='Independent source-data increment. The existing five-map application catalog is unchanged.')
        documents = {'viridian-catalog.json': catalog, 'encounters.json': encounters,
                     'parcel-quest.json': quest, 'npc-source.json': npcs}
        for filename, document in documents.items():
            (stage / filename).write_text(canonical(document), encoding='utf-8')
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,
                        newMapCount=3,


                        isPlayable=False, outputs=outputs)
        (stage / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
        # Only these owned paths are replaced, after the whole import has succeeded.
        for path in sorted(stage.rglob('*')):
            destination = out / path.relative_to(stage)
            require_safe_target(out, destination)
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
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/maps/regions/viridian')
    args = parser.parse_args()
    import_catalog(args.raw, args.out)
