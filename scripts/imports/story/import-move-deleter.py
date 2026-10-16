#!/usr/bin/env python3
"""Authenticate the original Fuchsia City Move Deleter house and its existing town door.

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
PACKAGE = 'maps/regions/move-deleter'
RESOURCE_ID = 'firered-move-deleter-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAP = 'FuchsiaCity_House3'
MAP_ID = 'map.fuchsia-city-house-3'
DIRECTORY = 'fuchsia-city-house-3'
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding1': 'secondary/generic_building_1'}
TOWN_PACKAGE = 'content/generated/maps/regions/fuchsia-route'
TOWN_RESOURCE = 'fuchsia-city/runtime-map.json'
NPC_REUSE = {'OBJ_EVENT_GFX_BALDING_MAN', 'OBJ_EVENT_GFX_RED_NORMAL'}


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
    original = src.json('data/maps/FuchsiaCity/map.json')
    need(json.loads(raw)['events'] == original, 'Retained Fuchsia City events differ')
    entrances = [dict(index=i, event=e) for i, e in enumerate(original['warp_events']) if e['dest_map'] == 'MAP_FUCHSIA_CITY_HOUSE3']
    need([(r['index'], r['event']['x'], r['event']['y'], r['event']['elevation'], r['event']['dest_warp_id']) for r in entrances] == [(10, 19, 31, 0, '0')], 'Original Fuchsia City entrance differs')
    return dict(map='map.fuchsia-city', operation='reuse-existing-source-map', currentPackage=TOWN_PACKAGE,
                 currentResource=row,
                entrances=entrances, geometryChanges=[], eventChanges=[])


def script_inventory(src, reader, world):
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p in (f'data/maps/{MAP}/scripts.inc', f'data/maps/{MAP}/text.inc'))]
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
    roots = {label for label, b in blocks.items() if b['path'] == f'data/maps/{MAP}/scripts.inc'}
    roots.update(e['script'] for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in world['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxDefault', 'Std_MsgboxYesNo']
    roots.update(implicit)
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
                aliases={a: b for a, b in aliases.items() if b in selected},
                opcodeCounts=dict(sorted(opcodes.items())), specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def service_contract(src):
    paths = ['src/party_menu_specials.c', 'src/pokemon_summary_screen.c', 'src/party_menu.c',
             'include/pokemon_summary_screen.h', 'src/strings.c', 'src/data/party_menu.h']
    for path in paths: src.read(path, 'move-deleter-native-service')
    special = src.text(paths[0]); summary = src.text(paths[1]); party = src.text(paths[2])
    need('IsMoveHm(move) == TRUE && sMonSummaryScreen->mode != PSS_MODE_FORGET_MOVE' in summary,
         'Original Move Deleter HM exemption differs')
    need('SetMonMoveSlot(&gPlayerParty[gSpecialVar_0x8004], MOVE_NONE, gSpecialVar_0x8005);' in special and
         'RemoveMonPPBonus(&gPlayerParty[gSpecialVar_0x8004], gSpecialVar_0x8005);' in special and
         'ShiftMoveSlot(&gPlayerParty[gSpecialVar_0x8004], i, i + 1);' in special, 'Original move compaction differs')
    anchors = [(paths[0], 'void ChoosePartyMon(void)'), (paths[0], 'void SelectMoveDeleterMove(void)'),
               (paths[0], 'void GetNumMovesSelectedMonHas(void)'), (paths[0], 'void BufferMoveDeleterNicknameAndMove(void)'),
               (paths[0], 'static void ShiftMoveSlot('), (paths[0], 'void MoveDeleterForgetMove(void)'),
               (paths[0], 'void IsSelectedMonEgg(void)'), (paths[1], 'static u8 PokeSum_CanForgetSelectedMove(void)\n{'),
               (paths[1], 'static void Task_InputHandler_SelectOrForgetMove(u8 taskId)\n{'),
               (paths[2], 'void ChoosePartyMonByMenuType(u8 menuType)')]
    references=[]
    for path, anchor in anchors:
        text=src.text(path); need(text.count(anchor)==1, 'Native anchor differs: '+anchor)
        references.append(src.ref(path, anchor.split('(')[0].split()[-1], text[:text.index(anchor)].count('\n')+1))
    return dict(nativeSourceReferences=references, partyCancelValue=6, moveCancelValue=4,
        eggRefused=True, finalMoveRefused=True, hiddenMachineMovesAllowed=True, price=0,
        moveCancelDestination='FuchsiaCity_House3_EventScript_ChooseMonForMoveDeleter',
        partyCancelDestination='FuchsiaCity_House3_EventScript_CancelForgetMove',
        confirmationNoDestination='FuchsiaCity_House3_EventScript_CancelForgetMove',
        successFanfare='MUS_MOVE_DELETED', deletedMovePpUpsCleared=True,
        remainingMovesKeepOrderCurrentPpAndPpUps=True, surfLastHolderRestriction=False,
        externalPartyOrStorageScan=False, partyPrompt='gText_ChoosePokemon',
        summaryTitle='gText_PokeSum_PageName_KnownMoves', summaryControls='gText_PokeSum_Controls_PickDelete',
        cancelText='gFameCheckerText_Cancel')


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
    reader = module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'move_deleter_reader')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    util = module(dep / 'scripts/imports/maps/import-fossil-lab-region.py', 'move_deleter_utils')
    util.MAPS = [MAP]
    src = reader.Source(ref)
    world = reader.decode(src, MAP); world['stableId'] = MAP_ID
    retained = retained_town(src, current)
    scripts = script_inventory(src, reader, world)
    constants = util.constants_inventory(src, scripts, [world])
    for leaf in ['scripts.inc', 'text.inc']: src.read(f'data/maps/{MAP}/' + leaf, 'live-map-script')
    links = reader.topology(src, [world])
    entrance = retained['entrances'][0]; event = entrance['event']; end = world['events']['warp_events'][0]
    links['warps'].append(dict(sourceMap='MAP_FUCHSIA_CITY', sourceIndex=entrance['index'], source=event,
        destinationMap='MAP_FUCHSIA_CITY_HOUSE3', destinationIndex=0, destination=end, dynamic=False, reciprocalIndex=True))
    actor = world['events']['object_events']
    need(len(actor) == 1 and actor[0]['graphics_id'] == 'OBJ_EVENT_GFX_BALDING_MAN' and
         (actor[0]['x'], actor[0]['y'], actor[0]['elevation'], actor[0]['movement_type'], actor[0]['script']) ==
         (4, 4, 3, 'MOVEMENT_TYPE_FACE_RIGHT', 'FuchsiaCity_House3_EventScript_MoveDeleter'), 'Original house actor differs')
    need((world['width'], world['height'], len(world['cells']), len(world['borderBlocks']), len(world['events']['warp_events'])) ==
         (11, 9, 99, 4, 1) and world['events']['layout'] == 'LAYOUT_HOUSE1', 'Original house geometry differs')
    need(end == dict(x=4, y=7, elevation=3, dest_map='MAP_FUCHSIA_CITY', dest_warp_id='10') and
         world['events']['connections'] is None and not world['events']['coord_events'] and not world['events']['bg_events'], 'Original house door/event boundaries differ')
    contract = dict(schemaVersion=1, resourceId=RESOURCE_ID,  retainedTown=retained,
        topology=links, house=dict(map=MAP_ID, sourceMap='MAP_FUCHSIA_CITY_HOUSE3', layout='LAYOUT_HOUSE1',
            width=11, height=9, localId=1, actor=actor[0], warp=end),
        service=service_contract(src))
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='move-deleter-', dir=target) as temp:
        stage = Path(temp)
        shared.import_map(src, MAP, MAP_ID, stage / DIRECTORY)
        npcs = shared.npc_inventory(src, [world], stage / 'npc')
        need({r['graphicsId'] for r in npcs['graphics']} == set(NPC_REUSE), 'House actor cohort differs')
        path = stage / DIRECTORY / 'runtime-map.json'; rich = path.read_bytes()
        compact = converter.normalize_runtime(json.loads(rich)); util.compact_check(compact, world, src)
        data = converter.encode(compact); (stage / DIRECTORY / 'runtime-map.json').write_bytes(data)
        entry = dict(sourceName=MAP, stableId=MAP_ID, directory=DIRECTORY, sourceMapSymbol=world['events']['id'],
            path=DIRECTORY + '/runtime-map.json')
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=[MAP], completeGame=False)
        lock_bytes = canonical(lock)
        docs = package_documents(target / 'content/generated', constants, {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID,
                     maps=[entry], tilesets=TILESETS,
                    mapSymbols={'MAP_FUCHSIA_CITY_HOUSE3': MAP_ID, 'MAP_FUCHSIA_CITY': 'map.fuchsia-city'}),
                'scripts.json': scripts,  'contracts.json': contract,
                'npc-source.json': npcs, 'geometry.json': dict(maps=reader.geometry_inventory([world]), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,  resourceId=RESOURCE_ID,

              directedWarpCount=2,  reusedNpcGraphicsCount=2,
              outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        writes = []
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
