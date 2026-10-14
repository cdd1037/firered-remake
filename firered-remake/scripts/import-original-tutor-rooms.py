#!/usr/bin/env python3
"""Authenticate two original tutor rooms and their unchanged adjacent entrances.

Pinned upstream Git blobs are inert input data. Only the reviewed local map
renderer, compact converter and constants reader execute. Teaching business
rules and current-game admission are separate from this resource package.
"""
import argparse
import collections
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'original-tutor-rooms'
RESOURCE_ID = 'firered-original-tutor-rooms-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MANIFEST_SHA256 = 'e8722902286244706eaf6ab8a0566ff73eb8db09afe42615721bb3aa88ded62e'
LOCK_SHA256 = '78a61aef6f4b425b43f3389997fd47b85c80a0e89bf7942a93a7dc40d9d58c2c'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
UTILITIES_SHA = '00c8e1105f255e0895eebc5b52a18b5d199ba36a5aedb06ea3961e861b8440c8'
MAPS = ['FourIsland_House1', 'CinnabarIsland_PokemonLab_ResearchRoom']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding2': 'secondary/generic_building_2',
            'gTileset_Lab': 'secondary/lab'}
ANCHORS = [
    ('FourIsland', 'content/generated/four-island-region', 'four-island/runtime-map.json',
     '1757f9fb60a699a7866315501887940fb0fd96989278640d67374e0a6a6d1412',
     'b6405aa3dcdd27ee60598664c8fc3cb65b6912410516e335fc6e7075de49079c', 2, 25, 14, 'MAP_FOUR_ISLAND_HOUSE1'),
    ('CinnabarIsland_PokemonLab_Entrance', 'content/generated/fossil-lab-region', 'cinnabar-island-pokemon-lab-entrance/runtime-map.json',
     'f41fe3ffc06dd4ec3c16fe0d32a8671affe65c6d121d58ae2630b4ab9dea151c',
     '71cc01b7d1e0c1310ccfa97005cf5636dd07e5fffe4e8ed3209ad4c87845be39', 4, 19, 5, 'MAP_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM')]
NPC_REUSE = {
    'OBJ_EVENT_GFX_FAT_MAN': ('content/generated/four-island-region/npc/fat-man.png', '33d2717fa841e7213da284ee832207cc088e66f339c60b26266fe62557c1aa3f'),
    'OBJ_EVENT_GFX_POKE_MANIAC': ('content/generated/victory-road-region/npc/poke-maniac.png', 'f64d6539265b0787f8d2951d38eb2203343ccf4415144f08d6cd76c62150e00d'),
    'OBJ_EVENT_GFX_SCIENTIST': ('content/generated/silph-region/npc/scientist.png', '69a795f639fcebb3e039f751b86995993f2760bed525cf91d40a912f80167568'),
    'OBJ_EVENT_GFX_RED_NORMAL': ('content/generated/city-services/npc/red-normal.png', '51d410e3c642aea0682cd273737df10f41a46eaf88fb952552eabe6294c3410a')}


def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, expected, name):
    safe(path)
    need(sha(path.read_bytes()) == expected, 'Reviewed local codec differs: ' + str(path))
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def retained_anchors(src, reader, current):
    result = []
    for name, package, relative, manifest_hash, map_hash, index, x, y, destination in ANCHORS:
        manifest_path = current / package / 'manifest.json'; safe(manifest_path)
        manifest = manifest_path.read_bytes()
        need(sha(manifest) == manifest_hash, 'Retained anchor manifest differs: ' + name)
        row = next(e for e in json.loads(manifest)['outputs'] if e['path'] == relative)
        path = current / package / relative; safe(path); raw = path.read_bytes()
        need(row['sha256'] == map_hash and sha(raw) == map_hash and len(raw) == row['byteLength'], 'Retained anchor map differs: ' + name)
        original = src.json('data/maps/' + name + '/map.json')
        need(json.loads(raw)['events'] == original, 'Retained anchor events differ: ' + name)
        entrance = original['warp_events'][index]
        need(entrance == dict(x=x, y=y, elevation=0, dest_map=destination, dest_warp_id='0'), 'Original room entrance differs: ' + name)
        result.append(dict(sourceName=name, map='map.' + reader.directory(name), sourceMap=original['id'],
            operation='reuse-existing-source-map', currentPackage=package, currentManifestSha256=manifest_hash,
            currentManifestByteLength=len(manifest), currentResource=row, entrances=[dict(index=index, event=entrance)],
            geometryChanges=[], eventChanges=[]))
    return result


def script_inventory(src, reader, worlds):
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p.endswith('/text.inc') or any(p == f'data/maps/{name}/scripts.inc' for name in MAPS))]
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
    roots = {label for label, b in blocks.items() if b['path'] in {f'data/maps/{name}/scripts.inc' for name in MAPS}}
    roots.update(e['script'] for world in worlds for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in world['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo']
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
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}), blocks=result,
                aliases={a: b for a, b in aliases.items() if b in selected},
                opcodeCounts=dict(sorted(opcodes.items())), specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)



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
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', READER_SHA, 'original_tutor_rooms_reader')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    util = module(dep / 'scripts/import-fossil-lab-region.py', UTILITIES_SHA, 'original_tutor_rooms_utils')
    util.MAPS = MAPS
    src = reader.Source(ref)
    worlds = [reader.decode(src, name) for name in MAPS]
    anchors = retained_anchors(src, reader, current)
    scripts = script_inventory(src, reader, worlds)
    constants = util.constants_inventory(src, scripts, worlds)
    for name in MAPS:
        for leaf in ['scripts.inc', 'text.inc']: src.read(f'data/maps/{name}/' + leaf, 'live-map-script')
    links = reader.topology(src, worlds)
    for world, anchor in zip(worlds, anchors, strict=True):
        entrance = anchor['entrances'][0]; event = entrance['event']; end = world['events']['warp_events'][0]
        links['warps'].append(dict(sourceMap=anchor['sourceMap'], sourceIndex=entrance['index'], source=event,
            destinationMap=world['events']['id'], destinationIndex=0, destination=end, dynamic=False, reciprocalIndex=True))
        need(world['events']['connections'] is None and not world['events']['coord_events'] and
             len(world['events']['warp_events']) == 1 and len(world['events']['object_events']) == 2, 'Room event scope differs')
    need([(w['width'], w['height'], len(w['cells']), len(w['borderBlocks']), w['events']['layout']) for w in worlds] ==
         [(11, 9, 99, 4, 'LAYOUT_HOUSE3'), (15, 11, 165, 4, 'LAYOUT_CINNABAR_ISLAND_POKEMON_LAB_RESEARCH_ROOM')], 'Room geometry differs')
    expected = [
        [('OBJ_EVENT_GFX_FAT_MAN', 8, 3, 'MOVEMENT_TYPE_FACE_RIGHT', 'FourIsland_House1_EventScript_BodySlamTutor'),
         ('OBJ_EVENT_GFX_FAT_MAN', 9, 3, 'MOVEMENT_TYPE_FACE_LEFT', 'FourIsland_House1_EventScript_FatMan')],
        [('OBJ_EVENT_GFX_POKE_MANIAC', 5, 4, 'MOVEMENT_TYPE_WANDER_AROUND', 'CinnabarIsland_PokemonLab_ResearchRoom_EventScript_MetronomeTutor'),
         ('OBJ_EVENT_GFX_SCIENTIST', 10, 3, 'MOVEMENT_TYPE_WANDER_AROUND', 'CinnabarIsland_PokemonLab_ResearchRoom_EventScript_Scientist')]]
    for world, actors in zip(worlds, expected, strict=True):
        need([(e['graphics_id'], e['x'], e['y'], e['movement_type'], e['script']) for e in world['events']['object_events']] == actors and
             all(e['elevation'] == 3 for e in world['events']['object_events']), 'Original actors differ')
    need(worlds[0]['events']['warp_events'][0] == dict(x=4, y=7, elevation=3, dest_map='MAP_FOUR_ISLAND', dest_warp_id='2') and
         worlds[1]['events']['warp_events'][0] == dict(x=7, y=9, elevation=3, dest_map='MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE', dest_warp_id='4'), 'Room exits differ')
    need(not worlds[0]['events']['bg_events'] and len(worlds[1]['events']['bg_events']) == 2, 'Original background event cohort differs')
    need(constants['values']['MOVETUTOR_BODY_SLAM']['value'] == 3 and constants['values']['MOVETUTOR_METRONOME']['value'] == 8, 'Original tutor indexes differ')
    contract = dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, retainedAnchors=anchors, topology=links,
        rooms=[dict(map=w['stableId'], sourceMap=w['events']['id'], layout=w['events']['layout'], width=w['width'], height=w['height'],
                    actors=[dict(localId=i + 1, event=e) for i, e in enumerate(w['events']['object_events'])],
                    warps=w['events']['warp_events'], backgroundEvents=w['events']['bg_events']) for w in worlds],
        tutors=[dict(map=worlds[0]['stableId'], localId=1, tutorIndex=3, move='MOVE_BODY_SLAM', flag='FLAG_TUTOR_BODY_SLAM',
                     entryScript='FourIsland_House1_EventScript_BodySlamTutor'),
                dict(map=worlds[1]['stableId'], localId=1, tutorIndex=8, move='MOVE_METRONOME', flag='FLAG_TUTOR_METRONOME',
                     entryScript='CinnabarIsland_PokemonLab_ResearchRoom_EventScript_MetronomeTutor')],
        limitations=['Map and script source package only; teaching definitions and current-game admission are separate.',
                     'No changes to the adjacent maps, their geometry, events or warp numbering.',
                     'Ordinary NPC and background event scripts are retained; no additional side-quest business is introduced.',
                     'Copycat/Mimic room and Poke Doll prerequisite are outside this two-room package.',
                     'NPC and player art reuse existing authenticated resources.'])
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('_reviewed_runtime_converter.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='original-tutor-rooms-', dir=target) as temp:
        stage = Path(temp)
        for world in worlds: shared.import_map(src, world['sourceName'], world['stableId'], stage / reader.directory(world['sourceName']))
        npcs = shared.npc_inventory(src, worlds, stage / 'npc')
        need({r['graphicsId'] for r in npcs['graphics']} == set(NPC_REUSE), 'Room actor cohort differs')
        for row in npcs['graphics']:
            path, digest = NPC_REUSE[row['graphicsId']]; existing = current / path; safe(existing)
            rendered = stage / row['texturePath']
            need(sha(existing.read_bytes()) == digest == row['sha256'] and rendered.read_bytes() == existing.read_bytes(), 'Reused actor differs')
            row.update(texturePath=path, byteLength=existing.stat().st_size, reuseExisting=True); rendered.unlink()
        (stage / 'npc').rmdir()
        source_hash = sha(''.join(p + '\0' + e['sha256'] + '\n' for p, e in sorted(src.entries.items())).encode())
        entries = []
        for world in worlds:
            directory = reader.directory(world['sourceName']); path = stage / directory / 'world.json'; rich = path.read_bytes()
            compact = converter.convert(rich, sha(rich), source_hash); util.compact_check(compact, world, src)
            data = converter.encode(compact); (stage / directory / 'runtime-map.json').write_bytes(data); path.unlink()
            entries.append(dict(sourceName=world['sourceName'], stableId=world['stableId'], directory=directory,
                sourceMapSymbol=world['events']['id'], path=directory + '/runtime-map.json', sha256=sha(data), byteLength=len(data),
                sourceWorldSha256=sha(rich), sourceWorldByteLength=len(rich), fullSourceHash=source_hash))
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False)
        lock_bytes = canonical(lock)
        symbols = {w['events']['id']: w['stableId'] for w in worlds}
        symbols.update({a['sourceMap']: a['map'] for a in anchors})
        docs = {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, sourceTree=TREE,
                    fullSourceHash=source_hash, maps=entries, inputFiles=lock['files'], tilesets=TILESETS, mapSymbols=symbols),
                'scripts.json': scripts, 'constants.json': constants, 'contracts.json': contract,
                'npc-source.json': npcs, 'geometry.json': dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix(), sha256=sha(p.read_bytes()), byteLength=p.stat().st_size)
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
            sourceTree=TREE, fullSourceHash=source_hash, lockSha256=sha(lock_bytes), readerSha256=READER_SHA,
            sourceUtilitiesSha256=UTILITIES_SHA, converterSha256=reader.HELPERS['_reviewed_runtime_converter.py'],
            rendererSha256=reader.HELPERS['_regional_service_shared.py'], inputCount=len(src.entries), mapCount=2,
            cellCount=264, borderCellCount=8, directedWarpCount=4, objectCount=4, reusedNpcGraphicsCount=4,
            scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        if MANIFEST_SHA256: need(sha(manifest_bytes) == MANIFEST_SHA256, 'Pinned generated manifest differs: ' + sha(manifest_bytes))
        if LOCK_SHA256: need(sha(lock_bytes) == LOCK_SHA256, 'Pinned source lock differs: ' + sha(lock_bytes))
        writes = [(target / 'scripts/original-tutor-rooms.lock.json', lock_bytes),
                  (target / 'scripts/original-tutor-rooms.contract.json', canonical(contract))]
        writes += [(target / 'content/raw' / PACKAGE / p, b) for p, b in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
        if not args.check:
            for path, data in writes: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(manifestSha256=sha(manifest_bytes), manifestByteLength=len(manifest_bytes),
            lockSha256=sha(lock_bytes), lockByteLength=len(lock_bytes), fullSourceHash=source_hash,
            inputCount=len(src.entries), outputCount=len(outputs), scriptBlockCount=len(scripts['blocks']), check=args.check), indent=2))


if __name__ == '__main__': main()
