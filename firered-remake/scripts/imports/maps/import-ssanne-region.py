#!/usr/bin/env python3
"""Authenticated local S.S. Anne source maps and progression contract; no gameplay activation."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

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
    target = path or ROOT / 'scripts/recipes/maps/ssanne-region.lock.json'
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
    roots.update(EXTERNAL_ROOTS)
    roots.add('EventScript_CutTree')
    required = {'SSAnne_CaptainsOffice_EventScript_Captain','SSAnne_Exterior_ExitSSAnne',
                'SSAnne_2F_Corridor_EventScript_RivalTrigger','SSAnne_1F_Room6_EventScript_Woman'}
    if not required <= roots: raise ValueError('Critical S.S. Anne entry labels are missing')
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
    return dict(schemaVersion=1,  executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, localMacros=local_macros, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='sourceOnly') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])


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
