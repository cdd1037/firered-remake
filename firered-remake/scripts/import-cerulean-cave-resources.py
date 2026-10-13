#!/usr/bin/env python3
"""Import pinned Cerulean Cave resources, retaining the existing city's entrance.

Only reviewed local Python converters execute. Upstream Git blobs are read as
inert data. This package adds no runtime profile, campaign loader or game rules.
"""
import argparse
import ast
import base64
import collections
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'cerulean-cave'
RESOURCE_ID = 'firered-cerulean-cave-resources-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
MANIFEST_SHA256 = '7861f71f2f1dae78cef47248f13999f12bc862a1ff086378a66826311d487016'
LOCK_SHA256 = 'a58256d497d92e1c97e48f9f5ae63c64566609ffc88c16337d086757efae8e59'
MAPS = ['CeruleanCave_1F', 'CeruleanCave_2F', 'CeruleanCave_B1F']
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_CeruleanCave': 'secondary/cerulean_cave',
            'gTileset_CeruleanCity': 'secondary/cerulean_city'}
CITY_PACKAGE = 'content/generated/rocket-hideout-runtime-maps'
CITY_MANIFEST_SHA = 'ee99ed635f7355c82f26373274caf3d1bc2706e7329f4d17383d3fc6b614c56b'
CITY_PATH = 'cerulean-region/cerulean-city/runtime-map.json'
CITY_SHA = 'abb0fedb1fd1d3e61807c2700535c7ab8a758ae132faa0e91121e1c05b18bead'


def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path):
    need(sha(path.read_bytes()) == READER_SHA, 'Reviewed source reader differs')
    spec = importlib.util.spec_from_file_location('cerulean_cave_reader', path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_inventory(src, reader, worlds):
    """Label closure, rooted only in cave events and the existing entrance guard."""
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p.endswith('/text.inc') or
              any(p == f'data/maps/{n}/scripts.inc' for n in MAPS + ['CeruleanCity']))]
    paths += ['data/event_scripts.s']
    for path in sorted(paths):
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
    roots = {label for label, b in blocks.items() if b['path'] in [f'data/maps/{n}/scripts.inc' for n in MAPS]}
    roots.update(e['script'] for w in worlds for e in w['events']['object_events'])
    # The source engine selects hidden items directly; event macros call these
    # standard scripts indirectly through gStdScripts rather than by label.
    implicit = ['EventScript_HiddenItemScript', 'Std_FindItem', 'Std_MsgboxNPC',
                'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_PutItemAway']
    roots.update(implicit + ['CeruleanCity_EventScript_CeruleanCaveGuard'])
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
        b = blocks[label]
        src.read(b['path'], 'script-label-closure')
        result.append(dict(label=label, sourceRef=src.ref(b['path'], label, b['line']), lines=b['lines']))
        for row in b['lines']:
            code = row['text'].split('@')[0].strip()
            if not code or code.startswith(('.', '#')): continue
            op, _, operands = code.partition(' ')
            if '\t' in op: op, operands = code.split(None, 1)
            opcodes[op] += 1
            if op in ('special', 'specialvar'): specials[operands.split(',')[-1].strip()] += 1
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'): target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement', 'case') or op.startswith(('goto_if_', 'call_if_')): target = operands.split(',')[-1].strip()
            elif op == 'msgbox': target = operands.split(',')[0].strip()
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    used_aliases = {a: b for a, b in aliases.items() if b in selected}
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}),
                implicitSpecials=[dict(symbol='GetQuestLogState', macro='goto_if_questlog', sourceRef=src.ref('asm/macros/event.inc', 'goto_if_questlog'))],
                blocks=result, aliases=used_aliases, opcodeCounts=dict(sorted(opcodes.items())),
                specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def constants_inventory(src, scripts, worlds, city):
    definitions, values = {}, {}
    paths = ['include/constants/' + name + '.h' for name in
             ['flags', 'vars', 'event_objects', 'event_object_movement', 'map_scripts', 'species',
              'moves', 'items', 'pokemon', 'battle', 'battle_setup', 'field_effects', 'songs',
              'sound', 'global', 'opponents', 'trainers', 'menu', 'game_stat', 'metatile_behaviors', 'field_weather']]
    for path in paths:
        for line, text in enumerate(src.text(path).splitlines(), 1):
            m = re.match(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)', text)
            if m: definitions[m[1]] = (m[2].split('//')[0].strip(), path, line)
    for path in ['asm/macros/event.inc', 'data/event_scripts.s']:
        for line, text in enumerate(src.text(path).splitlines(), 1):
            m = re.match(r'\s*([A-Z][A-Z0-9_]+)\s*=\s*(\d+)\s*$', text)
            n = re.match(r'\s*\.set\s+(\w+),\s*(\d+)\s*$', text)
            if m or n:
                m = m or n; definitions[m[1]] = (m[2], path, line)
    operands = '\n'.join(re.sub(r'"[^"\n]*"', '', r['text'].split('@')[0]) for b in scripts['blocks'] for r in b['lines'])
    tokens = set(re.findall(r'\b[A-Z][A-Z0-9_x]+\b', operands + json.dumps([w['events'] for w in worlds]) + json.dumps(city['guard'])))
    active = set()
    def resolve(symbol):
        if symbol in values: return values[symbol]['value']
        need(symbol not in active and symbol in definitions, 'Unresolved constant: ' + symbol)
        active.add(symbol)
        expression, path, line = definitions[symbol]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int: return node.value
            if isinstance(node, ast.Name): return resolve(node.id)
            if isinstance(node, ast.BinOp):
                left, right = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
                if isinstance(node.op, ast.BitOr): return left | right
                if isinstance(node.op, ast.LShift) and 0 <= right < 32: return left << right
            raise ValueError('Unsupported constant: ' + symbol)
        value = number(ast.parse(expression, mode='eval').body)
        values[symbol] = dict(value=value, sourceRef=src.ref(path, symbol, line)); active.remove(symbol)
        return value
    for token in sorted(tokens & definitions.keys()): resolve(token)
    values['LOCALID_CERULEAN_CAVE_GUARD'] = dict(value=12, sourceRef=src.ref('data/maps/CeruleanCity/map.json', 'LOCALID_CERULEAN_CAVE_GUARD'))
    return dict(schemaVersion=1, values=values, nonNumericTokens=sorted(tokens - values.keys()))


def compact_check(document, world, src):
    """Independent block/attribute roundtrip, including collision and elevation."""
    layout = world['layout']
    for field, path in [('blocks', layout['blockdata_filepath']), ('borderBlocks', layout['border_filepath'])]:
        need(base64.b64decode(document[field], validate=True) == src.read(path), 'Grid roundtrip differs: ' + path)
    need(document['events'] == world['events'], 'Original map events differ')
    rows = {row[0]: row[1] for row in document['metatiles']}
    raw = base64.b64decode(document['blocks'], validate=True)
    for cell, (block,) in zip(world['cells'], struct.iter_unpack('<H', raw), strict=True):
        need((block & 1023, (block >> 10) & 3, block >> 12, rows[block & 1023]) ==
             (cell['metatileId'], cell['collision'], cell['elevation'], cell['rawAttributes']), 'Cell roundtrip differs')
    for script in document['scripts']:
        original = src.read(script['path'])
        need(script['sha256'] == sha(original) and script['text'].encode() == original, 'Embedded script differs')


def city_overlay(src, reader, current):
    manifest = (current / CITY_PACKAGE / 'manifest.json').read_bytes()
    need(sha(manifest) == CITY_MANIFEST_SHA, 'Current compact package manifest differs')
    entry = next(e for e in json.loads(manifest)['maps'] if e['stableId'] == 'map.cerulean-city')
    need(entry['path'] == CITY_PATH and entry['sha256'] == CITY_SHA, 'Current city binding differs')
    raw = (current / CITY_PACKAGE / CITY_PATH).read_bytes()
    need(sha(raw) == CITY_SHA and len(raw) == entry['byteLength'], 'Current city resource differs')
    world = reader.decode(src, 'CeruleanCity')
    compact_check(json.loads(raw), world, src)
    guard = world['events']['object_events'][11]
    need(guard['local_id'] == 'LOCALID_CERULEAN_CAVE_GUARD' and guard['flag'] == 'FLAG_HIDE_CERULEAN_CAVE_GUARD', 'Entrance guard differs')
    need(world['events']['warp_events'][7] == dict(x=1, y=12, elevation=0, dest_map='MAP_CERULEAN_CAVE_1F', dest_warp_id='0'), 'Entrance warp differs')
    path = 'data/maps/OneIsland_PokemonCenter_1F/scripts.inc'
    text = src.text(path); command = 'setflag FLAG_HIDE_CERULEAN_CAVE_GUARD'
    need(text.count(command) == 1, 'Celio guard unlock differs')
    return dict(map='map.cerulean-city', operation='reuse-existing-source-map', geometryChanges=[], eventChanges=[],
                currentPackage=CITY_PACKAGE, currentManifestSha256=CITY_MANIFEST_SHA, currentResource=entry,
                guard=dict(localId=12, event=guard), entranceWarp=dict(index=7, event=world['events']['warp_events'][7]),
                unlockSource=src.ref(path, command, text[:text.index(command)].count('\n')+1),
                requiredIntegration='Register the three cave maps and preserve the existing guard hide-flag semantics; do not replace or patch CeruleanCity.'), world


def source_route(worlds, src):
    """Find a conservative source-geometry witness with every object still present.

    No Rock Smash, item collection, ledge shortcuts or collision changes. Surf
    availability is an explicit precondition, not a party/permission claim.
    """
    byname = {w['sourceName']: w for w in worlds}
    names = {w['events']['id']: w['sourceName'] for w in worlds}
    def cell(name, x, y):
        w = byname[name]
        return w['cells'][y*w['width']+x] if 0 <= x < w['width'] and 0 <= y < w['height'] else None
    start = ('CeruleanCave_1F', 33, 21, 3, False)
    seen, queue, finish = {start: None}, collections.deque([start]), None
    while queue:
        state = queue.popleft(); name, x, y, elevation, surfing = state
        if name == 'CeruleanCave_B1F' and abs(x-7)+abs(y-12) == 1 and elevation == 3:
            finish = state; break
        world = byname[name]
        for dx, dy, direction in [(0,-1,'north'), (0,1,'south'), (-1,0,'west'), (1,0,'east')]:
            nx, ny = x+dx, y+dy; target = cell(name, nx, ny)
            if target is None or target['collision'] != 0: continue
            # Conservatively block every original object, including pickups.
            if any(o['x'] == nx and o['y'] == ny and o['elevation'] in (0, target['elevation']) for o in world['events']['object_events']): continue
            behavior, next_elevation, next_surfing = target['behaviorId'], target['elevation'], surfing
            # Only normal/cave ground, rock stairs, ladder, ordinary surf water.
            # No directional ledge or impassable-edge shortcut is admitted.
            if behavior not in (0x00, 0x08, 0x10, 0x15, 0x2a, 0x61): continue
            action = 'walk'
            if behavior in (0x10, 0x15):
                if next_elevation != 1 or (target['rawAttributes'] >> 9) & 31 != 2: continue
                if not surfing and elevation != 3: continue
                next_surfing = True
                action = 'surf-step' if surfing else 'start-surf'
            elif surfing:
                if next_elevation != 3: continue
                next_surfing = False; action = 'stop-surf'
            elif elevation != 0 and next_elevation not in (0, 15, elevation): continue
            if next_elevation == 15: next_elevation = elevation
            next_name = name
            warp = next((e for e in world['events']['warp_events'] if e['x'] == nx and e['y'] == ny and e['dest_map'] in names), None)
            if warp:
                next_name = names[warp['dest_map']]
                destination = byname[next_name]['events']['warp_events'][int(warp['dest_warp_id'])]
                nx, ny = destination['x'], destination['y']
                next_elevation, next_surfing = cell(next_name, nx, ny)['elevation'], False
                action = 'warp'
            next_state = (next_name, nx, ny, next_elevation, next_surfing)
            if next_state not in seen:
                seen[next_state] = (state, direction, action); queue.append(next_state)
    need(finish is not None, 'No conservative no-Rock-Smash route to Mewtwo')
    rows, cursor = [], finish
    while seen[cursor] is not None:
        previous, direction, action = seen[cursor]
        rows.append(dict(direction=direction, action=action, map=cursor[0], x=cursor[1], y=cursor[2], elevation=cursor[3], surfing=cursor[4]))
        cursor = previous
    rows.reverse()
    return dict(schemaVersion=1, sourceOnly=True, runtimeValidated=False,
                preconditions=['Existing Cerulean City guard is hidden by its original flag.', 'A legal Surf-capable party and Soul Badge are already available.'],
                policy='All 34 original objects block the witness; no Rock Smash, pickups, ledge shortcuts, tile patches or encounter suppression is used.',
                start=dict(map=start[0], x=start[1], y=start[2], elevation=start[3]),
                finish=dict(map=finish[0], x=finish[1], y=finish[2], elevation=finish[3], mewtwoX=7, mewtwoY=12),
                stepCount=len(rows), rockSmashUses=0,
                surfStarts=sum(r['action'] == 'start-surf' for r in rows),
                mapTransitions=[r for r in rows if r['action'] == 'warp'], steps=rows,
                ruleSources=[src.ref('src/event_object_movement.c', 'IsElevationMismatchAt'),
                             src.ref('src/field_player_avatar.c', 'IsPlayerFacingSurfableFishableWater'),
                             src.ref('src/field_player_avatar.c', 'CanStopSurfing'),
                             src.ref('src/metatile_behavior.c', 'MetatileBehavior_IsSurfable')])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--current-project-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref = args.project_root.absolute(), args.dependency_root.absolute(), args.reference_root.absolute()
    current = (args.current_project_root or dep).absolute()
    for path in (target, dep, ref, current): safe(path)
    need(not(target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    src = reader.Source(ref)
    worlds = [reader.decode(src, name) for name in MAPS]
    city, city_world = city_overlay(src, reader, current)
    scripts = script_inventory(src, reader, worlds)
    route = source_route(worlds, src)
    # Loading the complete small cave script/text files before rendering ensures
    # the reviewed converter retains all live map callbacks in compact resources.
    for name in MAPS:
        for leaf in ['scripts.inc', 'text.inc']:
            path = f'data/maps/{name}/{leaf}'
            if path in src.tree: src.read(path, 'live-map-script')
    constants = constants_inventory(src, scripts, worlds, city)
    links = reader.topology(src, worlds)
    links['warps'].append(dict(sourceMap='MAP_CERULEAN_CITY', sourceIndex=7, source=city['entranceWarp']['event'],
                              destinationMap='MAP_CERULEAN_CAVE_1F', destinationIndex=0,
                              destination=worlds[0]['events']['warp_events'][0], dynamic=False, reciprocalIndex=True))
    need(len(links['warps']) == 16 and all(w['reciprocalIndex'] for w in links['warps']), 'Cave warp reciprocity differs')
    bysymbol = {w['events']['id']: w for w in worlds + [city_world]}
    for edge in links['warps']:
        for world, point in [(bysymbol[edge['sourceMap']], edge['source']), (bysymbol[edge['destinationMap']], edge['destination'])]:
            need(0 <= point['x'] < world['width'] and 0 <= point['y'] < world['height'], 'Warp out of bounds')
            cell = world['cells'][point['y'] * world['width'] + point['x']]
            need(cell['collision'] == 0 and point['elevation'] in (0, cell['elevation']), 'Warp collision/elevation differs')
    encounter_path = 'src/data/wild_encounters.json'
    groups = []
    for group in src.json(encounter_path)['wild_encounter_groups']:
        chosen = [e for e in group['encounters'] if e['map'] in bysymbol and 'CAVE' in e['map'] and e['base_label'].endswith('_FireRed')]
        if chosen: groups.append(dict(**{k:v for k,v in group.items() if k != 'encounters'}, encounters=chosen))
    need(len(groups) == 1 and len(groups[0]['encounters']) == 3, 'Original FireRed encounter cohort differs')
    encounters = dict(schemaVersion=1, sourceRef=src.ref(encounter_path, 'gWildMonHeaders'), groups=groups, runtimeIntegrated=False)
    bylabel = {b['label']: b for b in scripts['blocks']}
    pickups, hidden, objects = [], [], []
    for world in worlds:
        for index, event in enumerate(world['events']['object_events'], 1):
            row = dict(map=world['events']['id'], localId=index, event=event)
            objects.append(row)
            if event['graphics_id'] == 'OBJ_EVENT_GFX_ITEM_BALL':
                lines = '\n'.join(r['text'] for r in bylabel[event['script']]['lines'])
                match = re.search(r'\bfinditem (ITEM_\w+)(?:, (\d+))?', lines)
                need(match, 'Unsupported item pickup')
                pickups.append(dict(**row, item=match[1], quantity=int(match[2] or 1), sourceRef=bylabel[event['script']]['sourceRef']))
        hidden.extend(dict(map=world['events']['id'], event=e) for e in world['events']['bg_events'] if e['type'] == 'hidden_item')
    need(len(pickups) == 8 and len(hidden) == 1 and len(objects) == 34, 'Original cave object/item cohort differs')
    item_symbols = {p['item'] for p in pickups} | {p['event']['item'] for p in hidden}
    items = [i for i in src.json('src/data/items.json')['items'] if i['itemId'] in item_symbols]
    need({i['itemId'] for i in items} == item_symbols, 'Item declaration missing')
    contracts = dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, runtimeIntegrated=False,
                     maps=MAPS, cityEntrance=city, mapTopology=links, objects=objects, itemPickups=pickups, hiddenItems=hidden,
                     staticEncounter=dict(map='MAP_CERULEAN_CAVE_B1F', localId=3, species='SPECIES_MEWTWO', level=70,
                                          script='CeruleanCave_B1F_EventScript_Mewtwo', sourceRef=bylabel['CeruleanCave_B1F_EventScript_Mewtwo']['sourceRef']),
                     escapeRopeAnchor=dict(map='MAP_CERULEAN_CITY', x=1, y=13, entryWarpIndex=7,
                                          sourceRef=src.ref('src/overworld.c', 'UpdateEscapeWarp')),
                     mapHeaders=[dict(map=w['events']['id'], allowCycling=w['events']['allow_cycling'], allowEscaping=w['events']['allow_escaping'], allowRunning=w['events']['allow_running'], requiresFlash=w['events']['requires_flash']) for w in worlds],
                     limitations=['Resource/import contract only; no application, battle, encounter or field-move admission.',
                                  'Rock Smash objects and original land/water/rock/fishing tables are preserved; inclusion does not assert runtime support.',
                                  'Source geometry proves a no-Rock-Smash Surf route to Mewtwo; reachability.json is not an Application route test.'])
    shared = reader.load_helper('_regional_service_shared.py')
    shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('_reviewed_runtime_converter.py')
    with tempfile.TemporaryDirectory(prefix='cerulean-cave-', dir=target) as temp:
        stage = Path(temp)
        for world in worlds: shared.import_map(src, world['sourceName'], world['stableId'], stage / reader.directory(world['sourceName']))
        npcs = shared.npc_inventory(src, worlds, stage / 'npc')
        source_hash = sha(''.join(p + '\0' + e['sha256'] + '\n' for p, e in sorted(src.entries.items())).encode())
        entries = []
        for world in worlds:
            relative = reader.directory(world['sourceName']); path = stage / relative / 'world.json'; rich = path.read_bytes()
            compact = converter.convert(rich, sha(rich), source_hash)
            compact_check(compact, world, src)
            data = converter.encode(compact); (stage / relative / 'runtime-map.json').write_bytes(data); path.unlink()
            entries.append(dict(sourceName=world['sourceName'], stableId=world['stableId'], directory=relative,
                                sourceMapSymbol=world['events']['id'], path=relative + '/runtime-map.json', sha256=sha(data),
                                byteLength=len(data), sourceWorldSha256=sha(rich), sourceWorldByteLength=len(rich), fullSourceHash=source_hash))
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False)
        lock_bytes = canonical(lock)
        docs = {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, sourceTree=reader.TREE,
                                    fullSourceHash=source_hash, maps=entries, inputFiles=lock['files'], tilesets=TILESETS,
                                    mapSymbols={symbol: w['stableId'] for symbol,w in bysymbol.items()}),
                'reachability.json': route, 'scripts.json': scripts, 'constants.json': constants, 'npc-source.json': npcs, 'contracts.json': contracts,
                'encounters.json': encounters, 'items.json': dict(schemaVersion=1, declarations=items),
                'geometry.json': dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix(), sha256=sha(p.read_bytes()), byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT, sourceTree=reader.TREE,
                        fullSourceHash=source_hash, lockSha256=sha(lock_bytes), readerSha256=READER_SHA,
                        converterSha256=reader.HELPERS['_reviewed_runtime_converter.py'],
                        rendererSha256=reader.HELPERS['_regional_service_shared.py'],
                        inputCount=len(src.entries), mapCount=3, cellCount=2760, borderCellCount=12,
                        directedWarpCount=16, objectCount=34, pickupCount=8, hiddenItemCount=1,
                        npcGraphicsCount=len(npcs['graphics']), scriptBlockCount=len(scripts['blocks']),
                        unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        if MANIFEST_SHA256: need(sha(manifest_bytes) == MANIFEST_SHA256, 'Pinned manifest differs')
        if LOCK_SHA256: need(sha(lock_bytes) == LOCK_SHA256, 'Pinned source lock differs')
        index = dict(manifest, manifestSha256=sha(manifest_bytes), generatedDirectory='content/generated/' + PACKAGE,
                     currentCity=dict(path=CITY_PACKAGE + '/' + CITY_PATH, sha256=CITY_SHA, operation='reuse-unchanged'))
        writes = [(target / 'scripts/cerulean-cave-resources.lock.json', lock_bytes),
                  (target / 'scripts/cerulean-cave-resources.contract.json', canonical(contracts)),
                  (target / 'docs/cerulean-cave-resource-index.json', canonical(index))]
        writes += [(target / 'content/raw' / PACKAGE / p, data) for p, data in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(manifestSha256=sha(manifest_bytes), lockSha256=sha(lock_bytes), fullSourceHash=source_hash,
                              mapCount=3, cellRoundtripCount=2760, borderCellRoundtripCount=12, directedWarpCount=16,
                              unchangedCityCells=1920, inputCount=len(src.entries), outputCount=len(outputs),
                              scriptBlockCount=len(scripts['blocks']), totalBytes=sum(len(data) for _,data in writes), check=args.check), indent=2))


if __name__ == '__main__': main()
