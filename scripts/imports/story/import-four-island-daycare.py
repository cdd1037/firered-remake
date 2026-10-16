#!/usr/bin/env python3
"""Authenticate the original Four Island Pokemon Day Care and its existing town door.

Reads pinned Git blobs as inert data. Executes only the hash-pinned local map
renderer, compact converter and source utilities. Service rules live separately.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
import argparse
import collections
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PACKAGE = 'maps/regions/four-island-daycare'
RESOURCE_ID = 'firered-four-island-daycare-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAP = 'FourIsland_PokemonDayCare'
MAP_ID = 'map.four-island-pokemon-day-care'
DIRECTORY = 'four-island-pokemon-day-care'
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_FanClubDaycare': 'secondary/fan_club_daycare'}
TOWN_PACKAGE = 'content/generated/maps/regions/four-island'
TOWN_RESOURCE = 'four-island.json'
NPC_REUSE = {'OBJ_EVENT_GFX_OLD_MAN_1', 'OBJ_EVENT_GFX_OLD_WOMAN', 'OBJ_EVENT_GFX_RED_NORMAL'}


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def retained_town(src, current):
    manifest_path = current / TOWN_PACKAGE / 'manifest.json'
    safe(manifest_path)
    manifest = manifest_path.read_bytes()
    row = next(e for e in json.loads(manifest)['outputs'] if e['path'] == TOWN_RESOURCE)
    path = current / TOWN_PACKAGE / TOWN_RESOURCE
    safe(path)
    raw = path.read_bytes()
    original = src.json('data/maps/FourIsland/map.json')
    need(json.loads(raw)['events'] == original, 'Retained Four Island events differ')
    entrances = [dict(index=i, event=e) for i, e in enumerate(original['warp_events']) if e['dest_map'] == 'MAP_FOUR_ISLAND_POKEMON_DAY_CARE']
    need([(r['index'], r['event']['x'], r['event']['y'], r['event']['elevation'], r['event']['dest_warp_id']) for r in entrances] == [(1, 12, 13, 0, '0')], 'Original Four Island entrance differs')
    return dict(map='map.four-island', operation='reuse-existing-source-map', currentPackage=TOWN_PACKAGE,
                 currentResource=row,
                entrances=entrances, geometryChanges=[], eventChanges=[],
                dayCareMan=dict(localId=1, actor=original['object_events'][0],
                    pendingEggPosition=dict(x=16, y=14), defaultPosition=dict(x=16, y=13),
                    callback='FourIsland_EventScript_TrySetDayCareManPos', flag='FLAG_PENDING_DAYCARE_EGG'))


def script_inventory(src, reader, world):
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p in (f'data/maps/{MAP}/scripts.inc', f'data/maps/{MAP}/text.inc', 'data/maps/FourIsland/scripts.inc'))]
    for path in sorted(paths + ['data/event_scripts.s']):
        text = reader.git(src.reference, 'cat-file', 'blob', src.tree[path][2]).decode()
        current, macro = None, False
        for line, raw in enumerate(text.splitlines(), 1):
            code = raw.split('@')[0].strip()
            if code.startswith('.macro'): macro, current = True, None; continue
            if code == '.endm': macro = False; continue
            if macro: continue
            found = re.fullmatch(r'(\w+)::?', code)
            if found:
                current = found[1]
                need(current not in blocks, 'Duplicate script label: ' + current)
                blocks[current] = dict(label=current, path=path, line=line, lines=[])
            elif current: blocks[current]['lines'].append(dict(line=line, text=raw))
            found = re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)', code)
            if found: aliases[found[1]] = found[2]
    roots = {f'{MAP}_MapScripts', f'{MAP}_EventScript_DaycareWoman',
             'FourIsland_EventScript_DaycareMan', 'FourIsland_EventScript_TrySetDayCareManPos'}
    implicit = ['Std_MsgboxDefault', 'Std_MsgboxYesNo']
    roots.update(implicit)
    shared_path = 'data/scripts/day_care.inc'
    src.read(shared_path, 'shared-daycare-original-evidence')
    pending, selected = sorted(roots), set()
    while pending:
        label = aliases.get(pending[-1], pending[-1]); pending.pop()
        if label in selected: continue
        need(label in blocks, 'Missing script label: ' + label)
        selected.add(label)
        for row in blocks[label]['lines']:
            code = re.sub(r'"[^"\n]*"', '', row['text'].split('@')[0])
            for token in re.findall(r'\b[A-Za-z_]\w*\b', code):
                token = aliases.get(token, token)
                if token in blocks and token not in selected: pending.append(token)
    result, opcodes, specials, unresolved = [], collections.Counter(), collections.Counter(), []
    for label in sorted(selected):
        block = blocks[label]
        src.read(block['path'], 'script-label-closure')
        result.append(dict(label=label, sourceRef=src.ref(block['path'], label, block['line']), lines=block['lines']))
        for row in block['lines']:
            code = row['text'].split('@')[0].strip()
            if not code or code.startswith(('.', '#')): continue
            parts = code.split(None, 1); op = parts[0]; operands = parts[1] if len(parts) == 2 else ''
            opcodes[op] += 1
            if op in ('special', 'specialvar'): specials[operands.split(',')[-1].strip()] += 1
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'): target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement', 'case') or op.startswith(('goto_if_', 'call_if_')): target = operands.split(',')[-1].strip()
            elif op == 'msgbox': target = operands.split(',')[0].strip()
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                 blocks=result,
                fallthroughEdges=[dict(source='FourIsland_EventScript_TrySetDayCareManPos', destination='FourIsland_EventScript_EndSetDayCareManPos')],
                excludedRoots=['EventScript_EggHatch', 'Route5_PokemonDayCare_EventScript_DaycareMan',
                    'FourIsland_EventScript_RivalScene', 'FourIsland_PokemonDayCare_EventScript_UnusedEnd',
                    'FourIsland_PokemonDayCare_EventScript_UnusedRetrieveMon'],
                requiredApplicationSpecials=sorted(specials),
                futureUnsupportedRoots=['EventScript_EggHatch'], futureUnsupportedSpecials=['EggHatch'],
                aliases={a: b for a, b in aliases.items() if b in selected},
                opcodeCounts=dict(sorted(opcodes.items())), specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def service_contract(src):
    paths = ['src/daycare.c', 'src/pokemon.c', 'src/party_menu.c', 'src/mail_data.c',
             'src/field_control_avatar.c', 'src/pokemon_storage_system_menu.c',
             'include/global.h', 'include/daycare.h', 'include/pokemon.h',
             'include/constants/daycare.h', 'src/data/party_menu.h', 'src/strings.c']
    for path in paths: src.read(path, 'four-island-daycare-native-service')
    anchors = [('src/daycare.c', name) for name in [
        'void StoreSelectedPokemonInDaycare(void)', 'u16 TakePokemonFromDaycare(void)',
        'void GetDaycareCost(void)', 'u8 GetNumLevelsGainedFromDaycare(void)',
        'void RejectEggFromDayCare(void)', 'void GiveEggFromDaycare(void)',
        'bool8 ShouldEggHatch(void)', 'void GetDaycareMonNicknames(void)',
        'u8 GetDaycareState(void)', 'void SetDaycareCompatibilityString(void)',
        'void ShowDaycareLevelMenu(void)', 'void ChooseSendDaycareMon(void)']]
    for path, anchor in anchors:
        value = src.text(path); need(value.count(anchor) == 1, 'Native anchor differs: ' + anchor)
    text = src.text('src/daycare.c')
    need('cost = 100 + 100 * numLevelsGained;' in text, 'Original withdrawal price differs')
    match = re.search(r'sCompatibilityMessages\[\]\s*=\s*\{(.*?)\};', text, re.S)
    need(match, 'Original compatibility string table missing')
    symbols = re.findall(r'\b(gDaycareText_\w+)\b', match[1])
    strings = src.text('src/strings.c'); messages = []
    for score, symbol in zip([70, 50, 20, 0], symbols, strict=True):
        found = re.search(r'const u8 ' + symbol + r'\[\] = _\("(.*?)"\);', strings)
        need(found, 'Original compatibility text missing: ' + symbol)
        messages.append(dict(score=score, symbol=symbol, text=found[1],
            sourceRef=src.ref('src/strings.c', symbol, strings[:found.start()].count('\n') + 1)))
    return dict(slotCount=2, partyCancelValue=6, withdrawalMenuCancelValue=2,
        states=dict(empty=0, pendingEgg=1, oneMon=2, twoMons=3), compatibilityMessages=messages,
        eggsCannotBeSelectedForDeposit=True, minimumNonEggPartyCountBeforeDeposit=2,
        mustLeaveAliveNonEggPartyMember=True, basePrice=100, pricePerLevelGained=100,
        pendingEggFlag='FLAG_PENDING_DAYCARE_EGG', pendingEggBlocksWomanService=True,

        )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--current-project-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref, current = [Path(os.path.abspath(p)) for p in
                               [args.project_root, args.dependency_root, args.reference_root, args.current_project_root or args.dependency_root]]
    for path in [target, dep, ref, current]: safe(path)
    need(not (target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader = module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'four_island_daycare_reader')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    util = module(dep / 'scripts/imports/maps/import-fossil-lab-region.py', 'four_island_daycare_utils')
    util.MAPS = [MAP]
    src = reader.Source(ref)
    world = reader.decode(src, MAP); world['stableId'] = MAP_ID
    retained = retained_town(src, current)
    scripts = script_inventory(src, reader, world)
    constants = util.constants_inventory(src, scripts, [world, dict(sourceName='FourIsland', events=src.json('data/maps/FourIsland/map.json'))])
    daycare_constants = 'include/constants/daycare.h'
    for line, text in enumerate(src.text(daycare_constants).splitlines(), 1):
        match = re.match(r'^#define\s+(\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b', text)
        if match: constants['values'][match[1]] = dict(value=int(match[2], 0), sourceRef=src.ref(daycare_constants, match[1], line))
    constants['nonNumericTokens'] = sorted(set(constants['nonNumericTokens']) - constants['values'].keys())
    src.read(f'data/maps/{MAP}/scripts.inc', 'live-map-script')
    links = reader.topology(src, [world])
    entrance = retained['entrances'][0]; event = entrance['event']; end = world['events']['warp_events'][0]
    links['warps'].append(dict(sourceMap='MAP_FOUR_ISLAND', sourceIndex=entrance['index'], source=event,
        destinationMap='MAP_FOUR_ISLAND_POKEMON_DAY_CARE', destinationIndex=0, destination=end, dynamic=False, reciprocalIndex=True))
    actor = world['events']['object_events']
    need(len(actor) == 1 and actor[0]['graphics_id'] == 'OBJ_EVENT_GFX_OLD_WOMAN' and
         (actor[0]['x'], actor[0]['y'], actor[0]['elevation'], actor[0]['movement_type'], actor[0]['script']) ==
         (2, 2, 3, 'MOVEMENT_TYPE_FACE_DOWN', 'FourIsland_PokemonDayCare_EventScript_DaycareWoman'), 'Original house actor differs')
    need((world['width'], world['height'], len(world['cells']), len(world['borderBlocks']), len(world['events']['warp_events'])) ==
         (12, 9, 108, 4, 1) and world['events']['layout'] == 'LAYOUT_FOUR_ISLAND_POKEMON_DAY_CARE', 'Original house geometry differs')
    need(world['events']['warp_events'] == [dict(x=4, y=7, elevation=3, dest_map='MAP_FOUR_ISLAND', dest_warp_id='1')] and
         world['events']['connections'] is None and not world['events']['coord_events'] and not world['events']['bg_events'], 'Original house door/event boundaries differ')
    contract = dict(schemaVersion=1, resourceId=RESOURCE_ID,  retainedTown=retained,
        topology=links, house=dict(map=MAP_ID, sourceMap='MAP_FOUR_ISLAND_POKEMON_DAY_CARE', layout='LAYOUT_FOUR_ISLAND_POKEMON_DAY_CARE',
            width=12, height=9, localId=1, actor=actor[0], warps=world['events']['warp_events']),
        service=service_contract(src))
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='four-island-daycare-', dir=target) as temp:
        stage = Path(temp)
        shared.import_map(src, MAP, MAP_ID, stage / DIRECTORY)
        npc_world = dict(events=dict(object_events=[retained['dayCareMan']['actor']]))
        npcs = shared.npc_inventory(src, [world, npc_world], stage / 'npc')
        need({r['graphicsId'] for r in npcs['graphics']} == set(NPC_REUSE), 'House actor cohort differs')
        path = stage / DIRECTORY / 'runtime-map.json'; rich = path.read_bytes()
        compact = converter.normalize_runtime(json.loads(rich)); util.compact_check(compact, world, src)
        data = converter.encode(compact); (stage / DIRECTORY / 'runtime-map.json').write_bytes(data)
        entry = dict(sourceName=MAP, stableId=MAP_ID, directory=DIRECTORY, sourceMapSymbol=world['events']['id'],
            path=DIRECTORY + '/runtime-map.json')
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=[MAP], completeGame=False)
        lock_bytes = (json.dumps(lock, sort_keys=True, indent=2) + '\n').encode()
        docs = package_documents(target / 'content/generated', constants, {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID,
                     maps=[entry], tilesets=TILESETS,
                    mapSymbols={'MAP_FOUR_ISLAND_POKEMON_DAY_CARE': MAP_ID, 'MAP_FOUR_ISLAND': 'map.four-island'}),
                'scripts.json': scripts,  'contracts.json': contract,
                'npc-source.json': npcs, 'geometry.json': dict(maps=reader.geometry_inventory([world]), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,  resourceId=RESOURCE_ID,

              directedWarpCount=2,  retainedExteriorActorCount=1, reusedNpcGraphicsCount=3,
              outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        writes = [(target / 'scripts/recipes/story/four-island-daycare.lock.json', lock_bytes)]
        writes += [(target / 'content/raw/pokefirered/037335f4' / p, b) for p, b in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
        if not args.check:
            for path, data in writes: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
            inputCount=len(src.entries), outputCount=len(outputs), scriptBlockCount=len(scripts['blocks']), check=args.check), indent=2))


if __name__ == '__main__': main()
