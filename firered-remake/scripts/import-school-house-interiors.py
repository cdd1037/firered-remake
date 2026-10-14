#!/usr/bin/env python3
"""Authenticate ordinary School/House interiors and their unchanged adjacent entrances.

Pinned upstream Git blobs are inert input data. Only the reviewed local map
renderer, compact converter and constants reader execute. Generic grid and
wall-map runtime admission are separate from this source resource package.
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
PACKAGE = 'school-house-interiors'
RESOURCE_ID = 'firered-school-house-interiors-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MANIFEST_SHA256 = '11326c7a3fb8a93ed9f9d3454ec9659736738ba107093e4f84f999a0e9e4acfa'
LOCK_SHA256 = '9e395ff86732c25de3e098392334551b66884d4f76d89c01c7d634abaed6c933'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
RENDERER_SHA = '3d476350a7cd6dcbc3c929449187ddd450f5e7570d27691a834a97f097d55d3d'
UTILITIES_SHA = '00c8e1105f255e0895eebc5b52a18b5d199ba36a5aedb06ea3961e861b8440c8'
ROOMS = [
    dict(sourceName='ViridianCity_School', stableId='map.viridian-school', width=10, height=9,
         layout='LAYOUT_VIRIDIAN_CITY_SCHOOL', actors=2, backgrounds=5, exits=[(3,0),(4,0),(5,3)]),
    dict(sourceName='FuchsiaCity_House1', stableId='map.fuchsia-city-house-1', width=11, height=9,
         layout='LAYOUT_HOUSE1', actors=3, backgrounds=0, exits=[(3,3),(4,3),(5,3)]),
    dict(sourceName='ThreeIsland_House2', stableId='map.three-island-house2', width=11, height=9,
         layout='LAYOUT_HOUSE3', actors=2, backgrounds=0, exits=[(4,3)])]
MAPS = [room['sourceName'] for room in ROOMS]
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_School': 'secondary/school',
            'gTileset_GenericBuilding1': 'secondary/generic_building_1',
            'gTileset_GenericBuilding2': 'secondary/generic_building_2'}
ANCHORS = [
    ('ViridianCity', 'content/generated/rocket-hideout-runtime-maps', 'viridian-region/viridian-city/runtime-map.json',
     'ee99ed635f7355c82f26373274caf3d1bc2706e7329f4d17383d3fc6b614c56b',
     '2321bdf24ee587b5564e13660f7315ddea028b2d2477c51cdc59af9d24374b07', 3, 25, 18, 'MAP_VIRIDIAN_CITY_SCHOOL', 1),
    ('FuchsiaCity', 'content/generated/fuchsia-route-region', 'fuchsia-city/runtime-map.json',
     '0dfe16640308b507cfeb0ee4e9dd6082fcbc7ac2f2c07da2ab83614c0a6bd152',
     'e52978f1264280fee620b4323c11211ce66203922bafd7e3ed5ea775f8e41822', 5, 14, 31, 'MAP_FUCHSIA_CITY_HOUSE1', 1),
    ('ThreeIsland', 'content/generated/first-sevii-region', 'three-island/runtime-map.json',
     '8b772a69eb46f85430d35c65fdd2c00dc2c1011ef1e062fa63e82952f8a9b136',
     '13b8c7f89ac61f94d81d6d8e719ac3c8b86dd9598246b6111833bee25a45bdfc', 3, 4, 6, 'MAP_THREE_ISLAND_HOUSE2', 0)]
NPC_REUSE = {
    'OBJ_EVENT_GFX_ROCKER': ('content/generated/first-sevii-region/npc/rocker.png', '59e2bc1bd24f2bb6d763da22d0de3624a389f29e36bc755a69511a5b975555b1'),
    'OBJ_EVENT_GFX_MAN': ('content/generated/first-sevii-region/npc/man.png', '48802f9735acac9e011fa8b0f97ce086afb0b1e07e1fdd7953ce652020f39c5c'),
    'OBJ_EVENT_GFX_WOMAN_2': ('content/generated/pokemon-tower-region/npc/woman-2.png', '18f95efa875fb0e3dcd9d7e241205cc859d9d9588426dd84c4f93bdf7279c001'),
    'OBJ_EVENT_GFX_LASS': ('content/generated/regional-service-maps/npc/lass.png', '71dd9d13c652832d18cc20a41f95a0263a154ef7b732340db5d06e976e1930da'),
    'OBJ_EVENT_GFX_OLD_MAN_1': ('content/generated/celadon-approach-region/npc/old-man-1.png', '8a736f3d95c6cf7bdbf70cce3c54d88f82f8e5fa82fd0279d34a087ab819d709'),
    'OBJ_EVENT_GFX_LITTLE_BOY': ('content/generated/pokemon-tower-region/npc/little-boy.png', 'de1e0e8c7739bed2f6f3f43bd890e290bd1e91c9baa9a6ab59fa4d3db8d0b8f4'),
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
    for name, package, relative, manifest_hash, map_hash, index, x, y, destination, destination_index in ANCHORS:
        manifest_path = current / package / 'manifest.json'; safe(manifest_path)
        manifest = manifest_path.read_bytes()
        need(sha(manifest) == manifest_hash, 'Retained anchor manifest differs: ' + name)
        row = next(e for e in json.loads(manifest).get('outputs', json.loads(manifest).get('maps', [])) if e['path'] == relative)
        path = current / package / relative; safe(path); raw = path.read_bytes()
        need(row['sha256'] == map_hash and sha(raw) == map_hash and len(raw) == row['byteLength'], 'Retained anchor map differs: ' + name)
        original = src.json('data/maps/' + name + '/map.json')
        need(json.loads(raw)['events'] == original, 'Retained anchor events differ: ' + name)
        entrance = original['warp_events'][index]
        need(entrance == dict(x=x, y=y, elevation=0, dest_map=destination, dest_warp_id=str(destination_index)), 'Original room entrance differs: ' + name)
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
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo', 'EventScript_Bookshelf', 'EventScript_Cabinet', 'EventScript_Kitchen', 'EventScript_PlayerFacingTVScreen', 'EventScript_PolishedWindow', 'EventScript_BeautifulSkyWindow', 'EventScript_WallTownMap']
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
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', READER_SHA, 'school_house_interiors_reader')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    util = module(dep / 'scripts/import-fossil-lab-region.py', UTILITIES_SHA, 'school_house_interiors_utils')
    util.MAPS = MAPS
    src = reader.Source(ref)
    worlds = [reader.decode(src, name) for name in MAPS]
    for world, room in zip(worlds, ROOMS, strict=True): world['stableId'] = room['stableId']
    anchors = retained_anchors(src, reader, current)
    scripts = script_inventory(src, reader, worlds)
    constants = util.constants_inventory(src, scripts, worlds)
    fame_path = 'include/constants/fame_checker.h'
    for line, text in enumerate(src.text(fame_path).splitlines(), 1):
        match = re.fullmatch(r'#define\s+(FAMECHECKER_(?:OAK|DAISY|BILL|SABRINA))\s+(\d+)', text)
        if match:
            constants['values'][match[1]] = dict(value=int(match[2]), sourceRef=src.ref(fame_path, match[1], line))
    constants['nonNumericTokens'] = sorted(set(constants['nonNumericTokens']) - constants['values'].keys())
    for path in ['src/field_control_avatar.c', 'src/metatile_behavior.c', 'src/field_specials.c']:
        src.read(path, 'source-background-dispatch-reference')
    for name in MAPS:
        for leaf in ['scripts.inc', 'text.inc']: src.read(f'data/maps/{name}/' + leaf, 'live-map-script')
    links = reader.topology(src, worlds)
    for world, anchor, room in zip(worlds, anchors, ROOMS, strict=True):
        entrance = anchor['entrances'][0]; event = entrance['event']; destination_index = int(event['dest_warp_id'])
        end = world['events']['warp_events'][destination_index]
        links['warps'].append(dict(sourceMap=anchor['sourceMap'], sourceIndex=entrance['index'], source=event,
            destinationMap=world['events']['id'], destinationIndex=destination_index, destination=end, dynamic=False, reciprocalIndex=True))
        need(world['events']['connections'] is None and not world['events']['coord_events'], 'Room event scope differs')
        expected_exits = [dict(x=x, y=7, elevation=elevation, dest_map=anchor['sourceMap'],
                               dest_warp_id=str(entrance['index'])) for x, elevation in room['exits']]
        need(world['events']['warp_events'] == expected_exits, 'Original source exit geometry differs')
        need((world['width'], world['height'], len(world['cells']), len(world['borderBlocks']), world['events']['layout']) ==
             (room['width'], room['height'], room['width'] * room['height'], 4, room['layout']), 'Room geometry differs')
        need(len(world['events']['object_events']) == room['actors'] and
             len(world['events']['bg_events']) == room['backgrounds'], 'Room actor/background cohort differs')
    need(constants['values']['LOCALID_SCHOOL_WOMAN']['value'] == 1 and
         constants['values']['LOCALID_SCHOOL_LASS']['value'] == 2 and
         constants['values']['MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD']['value'] == 15, 'Source school constants differ')
    contract = dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, retainedAnchors=anchors, topology=links,
        rooms=[dict(map=w['stableId'], sourceMap=w['events']['id'], layout=w['events']['layout'], width=w['width'], height=w['height'],
                    actors=[dict(localId=i + 1, event=e) for i, e in enumerate(w['events']['object_events'])],
                    warps=w['events']['warp_events'], backgroundEvents=w['events']['bg_events']) for w in worlds],
        reachableFameFacts=[dict(map=worlds[0]['stableId'], entryScript='ViridianCity_School_EventScript_PokemonJournal',
                sourcePath='data/scripts/fame_checker.inc', person=person, personId=person_id, fact=3)
            for person, person_id in [('FAMECHECKER_OAK', 0), ('FAMECHECKER_DAISY', 1)]] +
            [dict(map=worlds[1]['stableId'], entryScript='FuchsiaCity_House1_EventScript_OldMan',
                  sourcePath='data/maps/FuchsiaCity_House1/scripts.inc', person='FAMECHECKER_BILL', personId=13, fact=2),
             dict(map=worlds[2]['stableId'], entryScript='ThreeIsland_House2_EventScript_Man',
                  sourcePath='data/maps/ThreeIsland_House2/scripts.inc', person='FAMECHECKER_SABRINA', personId=7, fact=1)],
        sourceMenu=dict(entryScript='ViridianCity_School_EventScript_ChooseBlackboardTopic', opcode='multichoicegrid',
            arguments=['7', '1', 'MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD', '3', 'FALSE'], topicResults=[0,1,2,3,4],
            exitResults=[5,127], runtimeAdmission='separate authenticated generic grid handler'),
        limitations=['Map and script source package; current-game mounting and generic grid runtime are separate.',
                     'No changes to exterior geometry, events, source headers or warp numbering.',
                     'Four exact reachable Fame facts are source candidates; importing shared fame scripts is not broader runtime coverage.',
                     'Source MB_REGION_MAP wall-map and MB_CABINET dispatch require separate generic runtime admission.', 'No combat, gifts or new species.', 'NPC and player art reuse existing authenticated resources.'])
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('_reviewed_runtime_converter.py')
    renderer = module(dep / 'scripts/school-house-map-renderer.py', RENDERER_SHA, 'school_house_renderer')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='school-house-interiors-', dir=target) as temp:
        stage = Path(temp)
        rendered_worlds = [renderer.import_map(src, world['sourceName'], world['stableId'], stage / reader.directory(world['sourceName']), shared) for world in worlds]
        contract['graphicsRule'] = dict(rule='map-entry-cleared-vram-compressed-callback-free-tilesets',
            rendererSha256=RENDERER_SHA, originalRendererSha256=reader.HELPERS['_regional_service_shared.py'],
            sourceReferences=[src.ref(p, symbol) for p, symbol in [
                ('src/overworld.c', 'ResetScreenForMapLoad'), ('src/fieldmap.c', 'CopySecondaryTilesetToVram'),
                ('src/new_menu_helpers.c', 'DecompressAndCopyTileDataToVram2'), ('tileset_rules.mk', 'secondary/school/tiles.4bpp')]],
            clearedTiles=[dict(map=w['stableId'], tiles=w['clearedVramTiles']) for w in rendered_worlds])
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
            rendererSha256=RENDERER_SHA, inputCount=len(src.entries), mapCount=len(worlds),
            cellCount=sum(len(w['cells']) for w in worlds), borderCellCount=sum(len(w['borderBlocks']) for w in worlds),
            directedWarpCount=len(links['warps']), objectCount=sum(len(w['events']['object_events']) for w in worlds),
            reusedNpcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        if MANIFEST_SHA256: need(sha(manifest_bytes) == MANIFEST_SHA256, 'Pinned generated manifest differs: ' + sha(manifest_bytes))
        if LOCK_SHA256: need(sha(lock_bytes) == LOCK_SHA256, 'Pinned source lock differs: ' + sha(lock_bytes))
        writes = [(target / 'scripts/school-house-interiors.lock.json', lock_bytes),
                  (target / 'scripts/school-house-interiors.contract.json', canonical(contract))]
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
