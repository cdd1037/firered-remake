#!/usr/bin/env python3
"""Authenticated local Route5/6, Underground Path and Vermilion source import; no gameplay activation."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import collections
import io
import struct
from PIL import Image
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
PROFILE = 'firered-vermilion-region-import-v1'
MAPS = [('Route5', 'map.route-5', 'route-5'),
        ('Route5_SouthEntrance', 'map.route-5-south-entrance', 'route-5-south-entrance'),
        ('UndergroundPath_NorthEntrance', 'map.underground-path-north-entrance', 'underground-path-north-entrance'),
        ('UndergroundPath_NorthSouthTunnel', 'map.underground-path-north-south-tunnel', 'underground-path-north-south-tunnel'),
        ('UndergroundPath_SouthEntrance', 'map.underground-path-south-entrance', 'underground-path-south-entrance'),
        ('Route6_NorthEntrance', 'map.route-6-north-entrance', 'route-6-north-entrance'),
        ('Route6', 'map.route-6', 'route-6'),
        ('VermilionCity', 'map.vermilion-city', 'vermilion-city'),
        ('VermilionCity_PokemonCenter_1F', 'map.vermilion-city-pokemon-center-1f', 'vermilion-city-pokemon-center-1f'),
        ('VermilionCity_Mart', 'map.vermilion-city-mart', 'vermilion-city-mart')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_CeruleanCity': 'secondary/cerulean_city', 'gTileset_VermilionCity': 'secondary/vermilion_city',
            'gTileset_GenericBuilding1': 'secondary/generic_building_1', 'gTileset_GenericBuilding2': 'secondary/generic_building_2',
            'gTileset_UndergroundPath': 'secondary/underground_path', 'gTileset_PokemonCenter': 'secondary/pokemon_center',
            'gTileset_Mart': 'secondary/mart'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_vermilion_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='vermilion-region')

def read_lock(path=None):
    target = path or ROOT / 'scripts/vermilion-region.lock.json'
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
    required = {'Route5_SouthEntrance_EventScript_Guard', 'Route6_NorthEntrance_EventScript_Guard',
                'VermilionCity_PokemonCenter_1F_EventScript_Nurse', 'VermilionCity_Mart_EventScript_Clerk'}
    if not required <= roots:
        raise ValueError('Critical Vermilion entry labels are missing')
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

def declaration(src, path, symbol, pattern):
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))


def encounter_inventory(src):
    path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    rows = [e for e in group['encounters'] if e['base_label'] in ('sRoute5_FireRed', 'sRoute6_FireRed')]
    if len(rows) != 2 or any(len(e['land_mons']['mons']) != 12 for e in rows):
        raise ValueError('Original FireRed route encounters differ')
    return dict(schemaVersion=1, gameVersion='FIRERED', revision=0, runtimeIntegrated=False,
                fields=group['fields'], maps=[dict(declaration=e, sourceRef=src.ref(path, e['base_label'])) for e in rows],
                landSpecies=sorted({m['species'] for e in rows for m in e['land_mons']['mons']}),
                deferredWaterAndFishingSpecies=sorted({m['species'] for e in rows for k in ('water_mons', 'fishing_mons') for m in e.get(k,{}).get('mons',[])}),
                waterAndFishingStatus='Original tables retained; Surf and fishing require separate runtime support')


def regional_connections_and_warps(src, worlds):
    # Original Route6 connects to Saffron's connection-only view with non-reciprocal
    # offsets (0 versus +12). Retain the source quirk as an unadmitted boundary.
    normal, exceptional = [], []
    for world in worlds:
        copy = dict(world, events=dict(world['events']))
        if world['events']['id'] == 'MAP_ROUTE6':
            original = next(c for c in world['events']['connections'] if c['map'] == 'MAP_SAFFRON_CITY_CONNECTION')
            path = 'data/maps/SaffronCity_Connection/map.json'
            reverse = next(c for c in src.json(path)['connections'] if c['map'] == 'MAP_ROUTE6')
            if original != dict(map='MAP_SAFFRON_CITY_CONNECTION',offset=0,direction='up') or reverse != dict(map='MAP_ROUTE6',offset=12,direction='down'):
                raise ValueError('Original Saffron connection-view offset exception differs')
            copy['events']['connections'] = [c for c in world['events']['connections'] if c != original]
            exceptional.append(dict(sourceMap=world['events']['id'],sourceConnectionIndex=0,**original,
                runtimeStatus='externalBoundary',coordinateRule='destination transverse coordinate = source transverse coordinate - offset',
                sourceRef=world['sourceRef'],reciprocalSource=reverse,reciprocalSourceRef=src.ref(path,'MAP_SAFFRON_CITY_CONNECTION'),
                sourceOffsetException='Original connection-only Saffron view uses non-reciprocal Route6 offsets; boundary remains unadmitted'))
        normal.append(copy)
    connections, warps = connections_and_warps(src, normal)
    for c in connections:
        if c['sourceMap'] == 'MAP_ROUTE6': c['sourceConnectionIndex'] = 1
    return sorted(connections+exceptional,key=lambda c:(c['sourceMap'],c['sourceConnectionIndex'])), warps


def regional_npcs(src, worlds, out):
    normal, invisible = [], []
    for world in worlds:
        copy = dict(world, events=dict(world['events'])); copy['events']['object_events'] = []
        for index, event in enumerate(world['events']['object_events'], 1):
            if event['graphics_id'] == '0':
                if event['movement_type'] != 'MOVEMENT_TYPE_INVISIBLE':
                    raise ValueError('Unknown zero-graphics actor')
                invisible.append(dict(map=world['events']['id'], localId=index, declaration=event,
                    sourceRef=world['sourceRef'], runtimeStatus='sourceInvisibleActor'))
            else:
                copy['events']['object_events'].append(event)
        normal.append(copy)
    result = npc_inventory(src, normal, out); result['invisibleActors'] = invisible
    return result


def dependency_inventory(src, worlds, scripts, constants, connections, warps, encounters):
    trainer_symbols = sorted({token for block in scripts['blocks'] if block['label'].startswith('Route6_')
        for line in block['lines'] for token in re.findall(r'\bTRAINER_[A-Z0-9_]+\b', line['text'])})
    trainers=[]
    for symbol in trainer_symbols:
        record=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\] = \{\n.*?^    \},')
        party=re.search(r'\.party = (\w+)\((\w+)\)',record['sourceText'])
        if not party: raise ValueError('Unknown trainer party type')
        record.update(sourceTrainerId=constants['values'][symbol]['value'],partyFormat=party[1],runtimeStatus='sourceOnly')
        record['party']=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{\n.*?^\};')
        record['species']=sorted(set(re.findall(r'\bSPECIES_[A-Z0-9_]+\b',record['party']['sourceText'])))
        trainers.append(record)
    if len(trainers) != 6: raise ValueError('Original Route6 initial trainer inventory differs')
    city=src.json('data/maps/CeruleanCity/map.json');route5=next(w for w in worlds if w['events']['id']=='MAP_ROUTE5')
    south=next(c for c in city['connections'] if c['map']=='MAP_ROUTE5');north=next(c for c in route5['events']['connections'] if c['map']=='MAP_CERULEAN_CITY')
    if south != dict(map='MAP_ROUTE5',offset=0,direction='down') or north != dict(map='MAP_CERULEAN_CITY',offset=0,direction='up'):
        raise ValueError('Cerulean/Route5 original offsets differ')
    heal = next(l for l in src.json('src/data/heal_locations.json')['heal_locations'] if l['id']=='HEAL_LOCATION_VERMILION_CITY')
    mart = next(b for b in scripts['blocks'] if b['label']=='VermilionCity_Mart_Items')
    stock = [re.search(r'ITEM_[A-Z0-9_]+',l['text'])[0] for l in mart['lines'] if '.2byte ITEM_' in l['text']]
    if stock != ['ITEM_POKE_BALL','ITEM_SUPER_POTION','ITEM_ANTIDOTE','ITEM_PARALYZE_HEAL','ITEM_AWAKENING','ITEM_ICE_HEAL','ITEM_REPEL','ITEM_NONE']:
        raise ValueError('Vermilion stock order differs')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,trainers=trainers,
        trainerSpecies=sorted({s for t in trainers for s in t['species']}),encounterSpecies=encounters['landSpecies'],
        ceruleanSouthConnection=dict(sourceEdgeY=39,destinationEdgeY=0,sourceXToDestinationX=0,
            sourceConnection=south,reverseConnection=north,sourceRef=src.ref('data/maps/CeruleanCity/map.json','MAP_CERULEAN_CITY'),enabled=False),
        healLocation=dict(declaration=heal,sourceId=constants['values']['HEAL_LOCATION_VERMILION_CITY']['value'],sourceRef=src.ref('src/data/heal_locations.json','HEAL_LOCATION_VERMILION_CITY')),
        mart=dict(items=stock,sourceRef=mart['sourceRef'],runtimeIntegrated=False),
        coordinateEvents=[dict(map=w['events']['id'],index=i,declaration=e,sourceRef=w['sourceRef'],enabled=False) for w in worlds for i,e in enumerate(w['events']['coord_events'])],
        exteriorBoundaries=[c for c in connections if c['runtimeStatus']=='externalBoundary'],externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        deferredFeatures=[
            dict(feature='North entrance NPC trade',sourceScript='UndergroundPath_NorthEntrance_EventScript_Saige',reason='Source trade retained; no substitute reward or unsupported trade execution'),
            dict(feature='VS Seeker gift/rematches',sourceScript='VermilionCity_PokemonCenter_1F_EventScript_VSSeekerWoman',reason='Source reward retained; no silent gift before feature implementation'),
            dict(feature='Daycare, SS Anne, Route11, city interiors and Saffron',reason='Source-visible external boundaries; unadmitted maps remain inaccessible'),
            dict(feature='Cut, Surf, fishing, ferries and multiplayer',reason='Source declarations retained without enabling unimplemented mechanics')],
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False),
        limitations=['Authenticated source/art only; no executable handlers, campaign registration, GUI or audio certification.',
                    'Source-visible later branches do not grant rewards or activate external travel.'])




def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.vermilion-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=regional_connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=regional_npcs(src,worlds,stage/'npc');encounters=encounter_inventory(src)
        dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps,encounters)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps,inputFiles=src.lock['files'])
        for filename,doc in package_documents(out.parent, constants, {'vermilion-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies,'encounters.json':encounters}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),
            warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),
            trainerCount=len(dependencies['trainers']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/vermilion-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
