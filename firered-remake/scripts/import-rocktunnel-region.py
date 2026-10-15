#!/usr/bin/env python3
"""Authenticated Route9, Route10, RockTunnel and LavenderTown source maps; no gameplay activation."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parent)
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
from runtime_map_manifest import compose, compose_region
import argparse
import collections
import importlib.util
import json
import pathlib
import re
import shutil
import tempfile
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-rocktunnel-region-import-v1'
MAPS = [('Route9', 'map.route-9', 'route-9'), ('Route10', 'map.route-10', 'route-10'), ('RockTunnel_1F', 'map.rock-tunnel-1f', 'rock-tunnel-1f'), ('RockTunnel_B1F', 'map.rock-tunnel-b1f', 'rock-tunnel-b1f'), ('Route10_PokemonCenter_1F', 'map.route-10-pokemon-center-1f', 'route-10-pokemon-center-1f'), ('Route10_PokemonCenter_2F', 'map.route-10-pokemon-center-2f', 'route-10-pokemon-center-2f'), ('LavenderTown', 'map.lavender-town', 'lavender-town')]
SERVICE_MAPS = [name for name, _, _ in MAPS]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_CeruleanCity': 'secondary/cerulean_city', 'gTileset_LavenderTown': 'secondary/lavender_town', 'gTileset_RockTunnel': 'secondary/rock_tunnel', 'gTileset_Building': 'primary/building', 'gTileset_PokemonCenter': 'secondary/pokemon_center'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_rocktunnel_region_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def protect_project_paths(*targets):
    for protected in [ROOT/'scripts/import-config',ROOT/'scripts',ROOT/'src',ROOT/'tests',ROOT/'.git']:
        for target in targets:
            path=pathlib.Path(target).resolve()
            if path.is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(path):
                raise ValueError('Protected project path overlap')

def materialize(raw, reference_repo):
    # Preserve existing profiles when the caller overrides --raw.
    raw=pathlib.Path(raw)
    protect_project_paths(raw)
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind=='raw' and (protected.name=='rocktunnel-region' or (protected.name=='pokefirered' and raw.resolve()==(ROOT/'content/raw/pokefirered/037335f4').resolve())):continue
            if raw.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(raw.resolve()):
                raise ValueError('Existing profile overlap')
    SHARED.materialize(raw,reference_repo,source_package='rocktunnel-region')

def read_lock(path=None):
    target = path or ROOT / 'scripts/import-config/rocktunnel-region.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != PROFILE) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in MAPS]) or (not lock.get('files'))):
        raise ValueError('Invalid locked source identity or scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if entry['path'] in seen:
            raise ValueError('Duplicate source path')
        seen.add(entry['path'])
        if (not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    return lock

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
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
            alias = re.fullmatch(r'\s*\.(?:equ|set)\s+(\w+),\s*(\w+)\s*', line)
            if alias:
                aliases[alias[1]] = alias[2]
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name in SERVICE_MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_ReceivedItem', 'Std_PutItemAway'])
    roots.update(['EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'EventScript_CutTree', 'EventScript_FldEffCut', 'EventScript_FldEffFlash'])
    required = {'Route10_PokemonCenter_1F_EventScript_Aide', 'RockTunnel_1F_OnTransition', 'LavenderTown_OnTransition'}
    if not required <= roots: raise ValueError('Critical RockTunnel entry labels are missing')
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
            elif op in ('msgbox', 'giveitem_msg', 'msgreceiveditem', 'pokemart'):
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
    references = dict(scripts, blocks=scripts['blocks'] + [dict(lines=[dict(text=src.text('src/fldeff_flash.c')), dict(text=declaration(src,'src/party_menu.c','CursorCB_FieldMove')['sourceText'])])])
    result = SHARED.constants_inventory(src, worlds, references)
    for name, target in scripts['aliases'].items():
        if target.isdecimal():
            matches = [p for p in scripts['filePreambles'] if re.search(r'\.(?:equ|set)\s+' + name + r',\s*' + target + r'\b', src.text(p))]
            if len(matches) != 1: raise ValueError('Ambiguous regional alias: ' + name)
            result['values'][name] = dict(value=int(target), sourceRef=src.ref(matches[0], name))
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
    return result

def declaration(src, path, symbol, pattern=None):
    pattern = pattern or r'^(?:static )?[^\n;]+\b' + re.escape(symbol) + r'\([^;\n]*\)\n\{.*?^\}'
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1: raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))

def reused_maps(src):
    directory = ROOT/'content/generated/cerulean-region'
    require_safe_target(directory,directory)
    manifest_path=directory/'manifest.json';require_safe_target(directory,manifest_path)
    manifest=manifest_path.read_bytes()
    entries={e['path']:e for e in json_bytes(manifest)['outputs']}
    outputs=[e for e in entries.values() if e['path'].startswith('cerulean-city/')]
    for e in outputs:
        p=directory/e['path'];require_safe_target(directory,p)
    world=json_bytes((directory/'cerulean-city/runtime-map.json').read_bytes())
    if world['events']!=src.json('data/maps/CeruleanCity/map.json'):raise ValueError('Reused Cerulean source events differ')
    return [dict(sourceName='CeruleanCity',stableId='map.cerulean-city',sourceMapSymbol='MAP_CERULEAN_CITY',profile='firered-cerulean-region-import-v1',
        directory='cerulean-region/cerulean-city',
        outputs=outputs)]


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


def import_catalog(raw,out,lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    protect_project_paths(raw,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if protected.name=='rocktunnel-region':continue
            if any(q.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(q.resolve()) for q in [raw,out]):raise ValueError('Existing profile overlap')
    src=Source(raw,lock_path);reused=reused_maps(src);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.rocktunnel-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        overlap=dict(events=src.json('data/maps/CeruleanCity/map.json'))
        connections,warps=regional_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds+[overlap],scripts)
        npcs=npc_inventory(src,worlds,stage/'npc')
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            reusedMaps=reused,connections=connections,warps=warps)
        for filename,doc in package_documents(out.parent, constants, {'rocktunnel-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs}).items():
            (stage/filename).write_bytes(canonical(doc).encode())

        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),reusedMapCount=len(reused),cellCount=sum(len(w['cells']) for w in worlds),
            borderCellCount=sum(len(w['borderCells']) for w in worlds),warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
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


from pathlib import Path
MAXIMUM_MAP_BYTES = 2 * 1024 * 1024

def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe(root, relative):
    path = Path(relative)
    require(bool(relative) and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in relative.split('/'))
            and path.as_posix() == relative, 'Unsafe runtime path')
    target = Path(root) / path
    require(not any(p.is_symlink() for p in [target, *target.parents]), 'Symlink source/output rejected')
    return target


def bounded(root, relative, maximum):
    with safe(root, relative).open('rb') as stream:
        raw = stream.read(maximum + 1)
    require(0 < len(raw) <= maximum, 'Runtime input size out of bounds: ' + relative)
    return raw


def authenticated(root, entry, maximum):
    raw = bounded(root, entry['path'], maximum)
    return raw


def export_runtime(generated, output, check=False):
    return compose_region(generated, output, 'rocktunnel-region', 'rocktunnel-catalog.json', 'route11-runtime-maps', 98, check)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/rocktunnel-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or upstream execution.')
    parser.add_argument('--runtime-only',action='store_true')
    parser.add_argument('--runtime-output',type=pathlib.Path,default=ROOT/'content/generated/rocktunnel-runtime-maps')
    parser.add_argument('--check-runtime',action='store_true')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    if not args.runtime_only and not args.check_runtime:import_catalog(args.raw,args.out)
    print(json.dumps(export_runtime(ROOT/'content/generated',args.runtime_output,args.check_runtime),sort_keys=True))
