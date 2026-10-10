#!/usr/bin/env python3
"""Authenticated local Cerulean City and ten city interiors import; no gameplay activation."""
import argparse
import collections
import importlib.util
import json
import os
import pathlib
import re
import shutil
import tempfile
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
LOCK_SHA256 = '74e8e6a8a75d4362d3055bef4bf26bfbf509ba8c9a1bb51d38eca35d033f4784'
MANIFEST_SHA256 = '6121d663f744e6e278722c6a5f9cd4527e4d87593b283eed81ba20250f6a7187'
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
SPEC = importlib.util.spec_from_file_location('_cerulean_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
digest, canonical, json_bytes = SHARED.digest, SHARED.canonical, SHARED.json_bytes
require_safe_target, verify_bytes, source_hash = SHARED.require_safe_target, SHARED.verify_bytes, SHARED.source_hash
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
materialize = SHARED.materialize

def read_lock(path=None):
    target = path or ROOT / 'scripts/cerulean-region.lock.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or
            lock.get('repository') != 'pret/pokefirered' or lock.get('commit') != COMMIT or
            lock.get('tree') != TREE or lock.get('profile') != PROFILE or
            lock.get('gameVersion') != 'FIRERED' or type(lock.get('revision')) is not int or
            lock['revision'] != 0 or lock.get('completeGame') is not False or
            lock.get('maps') != [x[0] for x in MAPS] or not lock.get('files')):
        raise ValueError('Invalid locked source identity or scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if entry['path'] in seen:
            raise ValueError('Duplicate source path')
        seen.add(entry['path'])
        if (set(entry) != {'path', 'byteLength', 'gitBlobSha1', 'sha256', 'roles'} or
                type(entry['byteLength']) is not int or entry['byteLength'] <= 0 or
                not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1']) or
                not re.fullmatch('[0-9a-f]{64}', entry['sha256']) or not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    if digest(data) != LOCK_SHA256:
        raise ValueError('Reviewed source lock hash differs')
    return lock

for key in ('ROOT', 'COMMIT', 'TREE', 'LOCK_SHA256', 'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    blocks, aliases, preambles, local_macros = {}, {}, {}, []
    script_paths = [p for p, e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc', '.s'))]
    for path in sorted(script_paths):
        current = None
        macro = None
        preambles[path] = []
        for number, line in enumerate(src.text(path).splitlines(), 1):
            definition = re.fullmatch(r'\s*\.macro\s+(\w+)(.*)', line)
            if definition:
                if macro is not None: raise ValueError('Nested regional macro')
                macro = dict(name=definition[1], arguments=definition[2].strip(), lines=[], sourceRef=src.ref(path, definition[1], number))
                local_macros.append(macro)
                current = None
                continue
            if macro is not None:
                if line.strip() == '.endm': macro = None
                else: macro['lines'].append(dict(line=number, text=line))
                continue
            label = re.fullmatch(r'(\w+)::?', line.split('@')[0].strip())
            if label:
                name = label[1]
                if name in blocks:
                    raise ValueError('Duplicate script label: ' + name)
                current = dict(label=name, sourceRef=src.ref(path, name, number), lines=[])
                blocks[name] = current
            elif current is not None:
                current['lines'].append(dict(line=number, text=line))
            else:
                preambles[path].append(dict(line=number, text=line))
            alias = re.fullmatch(r'\s*\.equ\s+(\w+),\s*(\w+)\s*', line)
            if alias:
                aliases[alias[1]] = alias[2]
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
    selected, pending = set(), sorted(roots)
    references, unresolved = [], []
    commands, specials, movements = collections.defaultdict(list), collections.defaultdict(list), collections.defaultdict(list)
    while pending:
        label = pending.pop()
        if label in selected:
            continue
        selected.add(label)
        block = blocks[label]
        for line in block['lines']:
            text = line['text'].split('@')[0].strip()
            if not text or text.startswith(('.string', '.include', '#')):
                continue
            op = text.split()[0]
            location = dict(label=label, path=block['sourceRef']['path'], line=line['line'])
            commands[op].append(location)
            if op in ('special', 'specialvar'):
                specials[text.split(',')[-1].strip().split()[-1]].append(location)
            if label.endswith('Movement') or 'Movement_' in label:
                movements[op].append(location)
            # Target operands are validated independently of naming conventions. A renamed
            # unknown label must not disappear just because it lacks an EventScript prefix.
            operands = text.split(None, 1)[1] if ' ' in text else ''
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'):
                target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement') or op.startswith(('goto_if_', 'call_if_')):
                target = operands.split(',')[-1].strip()
            elif op == 'case':
                target = re.split(r'[,\s]+', operands.strip())[-1]
            elif op in ('msgbox', 'giveitem_msg', 'pokemart'):
                target = operands.split(',')[0].strip()
            if op in ('trainerbattle_single', 'trainerbattle_no_intro'):
                for extra in operands.split(',')[1:4] if op == 'trainerbattle_single' else operands.split(',')[1:2]:
                    if extra.strip() not in ('0', 'NULL') and aliases.get(extra.strip(), extra.strip()) not in blocks:
                        unresolved.append(dict(symbol=extra.strip(), **location))
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(symbol=target, **location))
            for token in re.findall(r'\b[A-Za-z_]\w*\b', text):
                token = aliases.get(token, token)
                if token in blocks:
                    references.append(dict(source=label, target=token, line=line['line']))
                    if token not in selected:
                        pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Movement_|Text_|.*_(?:EventScript|Movement|Text)_|CableClub_On).*', token):
                    unresolved.append(dict(symbol=token, **location))
    if unresolved:
        raise ValueError('Unresolved source label dependencies: ' + ', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1, profile=PROFILE, executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, localMacros=local_macros, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='sourceOnly') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])


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
    return dict(schemaVersion=1, profile=PROFILE, runtimeIntegrated=False, completeGame=False,
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
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False),
        limitations=['Authenticated data/art only. No executable handlers, campaign registration, playable proof, audio or native acceptance.',
            'Dynamic postgame journal NPC graphics are inventoried without invented graphics.',
            'Routes 24/25/Bill, Route5/9, Cerulean Cave and multiplayer rooms are not imported as playable maps.',
            'Map and NPC art use original static source frames; original animation and timing remain unverified.'])

def compact_derivatives(stage, src):
    """Emit the existing compact wire format without changing its registered runtime profiles."""
    converter_path = ROOT / 'scripts/export-runtime-maps.py'
    if digest(converter_path.read_bytes()) != '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8':
        raise ValueError('Reviewed compact converter differs')
    spec = importlib.util.spec_from_file_location('_cerulean_compact_converter', converter_path)
    converter = importlib.util.module_from_spec(spec); spec.loader.exec_module(converter)
    for _, _, directory in MAPS:
        raw = (stage / directory / 'world.json').read_bytes()
        compact = converter.convert(raw, digest(raw), source_hash(src.lock))
        (stage / directory / 'runtime-map.json').write_bytes(converter.encode(compact))

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
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,tree=TREE,fullSourceHash=source_hash(src.lock),runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps,inputFiles=src.lock['files'])
        for filename,doc in {'cerulean-catalog.json':catalog,'scripts.json':scripts,'constants.json':constants,'npc-source.json':npcs,'dependencies.json':dependencies}.items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))
        compact_derivatives(stage, src)
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=digest(p.read_bytes()),byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,fullSourceHash=source_hash(src.lock),lockSha256=LOCK_SHA256,
            inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),
            warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),
            trainerCount=len(dependencies['trainers']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        if digest(data)!=MANIFEST_SHA256: raise ValueError('Reviewed generated output hashes differ: '+digest(data))
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
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/cerulean-region')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/cerulean-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize: materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
