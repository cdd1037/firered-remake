#!/usr/bin/env python3
"""Authenticated, local Git-blob-only Route 2 and Viridian Forest source increment."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure
from map_source import LockedSource, render_map, connections_and_warps, object_pairs, json_bytes, require_safe_target
from map_runtime import movement
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
from overworld_sprites import graphics_records
from map_runtime import compact_world as _compact_world, encode as _encode_runtime

import argparse
import ast
import base64
import collections
import io
import json
import os
import pathlib
import re
import shutil
import struct
import subprocess
import tempfile
from PIL import Image
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-forest-region-import-v1'
MAPS = [('Route2', 'map.route-2', 'route-2'),
        ('Route2_ViridianForest_SouthEntrance', 'map.route-2-forest-south-gate', 'forest-south-gate'),
        ('ViridianForest', 'map.viridian-forest', 'viridian-forest'),
        ('Route2_ViridianForest_NorthEntrance', 'map.route-2-forest-north-gate', 'forest-north-gate')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_ViridianCity': 'secondary/viridian_city',
            'gTileset_ViridianForest': 'secondary/viridian_forest', 'gTileset_Building': 'primary/building',
            'gTileset_GenericBuilding2': 'secondary/generic_building_2'}


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')) + '\n'


def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/forest-region.lock.json', maps=MAPS, profile=PROFILE)


class Source(LockedSource):
    def __init__(self, raw, lock_path=None):
        super().__init__(raw, read_lock(lock_path))


def import_map(src, name, stable_id, out):
    return render_map(src, name, stable_id, out, tilesets=TILESETS)


def script_inventory(src, worlds):
    parsed = parse_scripts(src, capture_macros=False)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'Std_FindItem', 'EventScript_HiddenItemScript',
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach'])
    required = {'ViridianForest_OnTransition', 'ViridianForest_EventScript_Rick', 'EventScript_CutTree'}
    if not required <= roots:
        raise ValueError('Critical forest entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='unsupportedInForestRegion')
    result.pop("localMacros")
    return result


def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


def materialize(raw, reference_repo=None, lock_path=None):
    """Read only immutable Git blobs, or verify an already materialized local source set."""
    raw = pathlib.Path(raw)
    lock = read_lock(lock_path)
    if reference_repo is not None:
        reference = pathlib.Path(reference_repo).resolve()
        if raw.resolve() == reference or raw.resolve().is_relative_to(reference) or reference.is_relative_to(raw.resolve()):
            raise ValueError('Raw materialization and reference repository must be disjoint')
    tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        tree = {}
        for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
            if row:
                fields, name = row.split(b'\t')
                tree[name.decode()] = tuple(fields.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = raw / path
        require_safe_target(raw, target)
        if tree is not None:
            data = git('cat-file', 'blob', COMMIT + ':' + entry['path'])
        else:
            data = target.read_bytes()
        verified[path] = data
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=COMMIT,  inputCount=len(verified))
    return manifest


def constants_inventory(src, worlds, scripts):
    values = {}
    for path in sorted(src.data):
        if not path.startswith('include/constants/'):
            continue
        for match in re.finditer(r'^#define\s+([A-Z][A-Z0-9_]+)\s+(0x[0-9a-fA-F]+|[0-9]+)\s*(?://[^\n]*)?$', src.text(path), re.M):
            name, token = match.groups()
            value = int(token, 16 if token.startswith('0x') else 10)
            if name in values and values[name]['value'] != value:
                raise ValueError('Conflicting source constant: ' + name)
            values[name] = dict(value=value, sourceRef=src.ref(path, name, src.text(path)[:match.start()].count('\n') + 1))
    # Resolve only the arithmetic needed by regional flags and trainer AI. No eval,
    # C preprocessing, functions, or upstream execution is permitted.
    expressions = {}
    for path in sorted(src.data):
        if path.startswith('include/constants/'):
            for match in re.finditer(r'^#define[ \t]+([A-Za-z_][A-Za-z0-9_]+)[ \t]+([^\n]+)', src.text(path), re.M):
                token = match[2].split('//')[0].strip()
                expressions[match[1]] = (token, path, src.text(path)[:match.start()].count('\n') + 1)
    wanted = {'AI_SCRIPT_CHECK_BAD_MOVE'}
    for world in worlds:
        for kind in ('object_events','coord_events','bg_events'):
            for event in world['events'][kind]:
                if event.get('flag','').startswith('FLAG_'):
                    wanted.add(event['flag'])
    for block in scripts['blocks']:
        for row in block['lines']:
            wanted.update(re.findall(r'\bFLAG_[A-Za-z0-9_]+\b', row['text']))
    pending = set()
    def resolve(name):
        if name in values:
            return values[name]['value']
        if name in pending or name not in expressions:
            raise ValueError('Missing/cyclic regional constant: ' + name)
        pending.add(name)
        expression, path, line = expressions[name]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                return resolve(node.id)
            if isinstance(node, ast.BinOp):
                left, right = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
                if isinstance(node.op, ast.LShift) and 0 <= right < 32: return left << right
            raise ValueError('Unsupported regional constant expression: ' + expression)
        value = number(ast.parse(expression, mode='eval').body)
        if not 0 <= value <= 0x7fffffff:
            raise ValueError('Regional constant outside supported range')
        values[name] = dict(value=value, sourceRef=src.ref(path,name,line))
        pending.remove(name)
        return value
    for name in sorted(wanted):
        resolve(name)
    generator = src.text('tools/mapjson/mapjson.cpp')
    if 'json_to_string(obj_event, "local_id") << " " << i + 1' not in generator:
        raise ValueError('Unsupported local object ID generation')
    for path in sorted(src.data):
        if path.startswith('data/maps/') and path.endswith('/map.json'):
            for i, obj in enumerate(src.json(path)['object_events'], 1):
                if 'local_id' in obj:
                    values[obj['local_id']] = dict(value=i, sourceRef=src.ref(path, obj['local_id']))
    return dict(schemaVersion=1, values=values)


def dependency_inventory(src, worlds, scripts, constants):
    """Bounded declarative extraction. No species/move/special executor is implied."""
    def one(pattern, path):
        matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one source definition: ' + path + ':' + pattern)
        return matches[0]

    def reference(path, symbol, match):
        return src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1)

    def fields(body):
        values = {}
        for line in body.splitlines():
            if not line.strip():
                continue
            match = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if match is None or match[1] in values:
                raise ValueError('Unparsed/duplicate table field: ' + line)
            values[match[1]] = match[2]
        return values

    def constant(symbol):
        return constants['values'][symbol]['value']

    def table(path, symbol):
        m = one(r'^    \[' + re.escape(symbol) + r'\] =\s*\n?    ?\{\n(.*?)^    \},', path)
        return fields(m[1]), reference(path, symbol, m)

    encounter_path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(encounter_path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    template = src.text('src/data/wild_encounters.json.txt')
    if 'contains(encounter.base_label, "FireRed")' not in template or '#ifdef FIRERED' not in template:
        raise ValueError('Unknown wild encounter version-selection template')
    weights = next(f['encounter_rates'] for f in group['fields'] if f['type'] == 'land_mons')
    if weights != [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1]:
        raise ValueError('Unexpected source encounter slots')
    encounters = []
    species_symbols = set()
    for world in worlds:
        rows = [r for r in group['encounters'] if r['map'] == world['events']['id'] and 'FireRed' in r['base_label']]
        expected = 1 if world['events']['id'] in ('MAP_ROUTE2', 'MAP_VIRIDIAN_FOREST') else 0
        if len(rows) != expected:
            raise ValueError('Wild table scope differs')
        for row in rows:
            if set(row) != {'map', 'base_label', 'land_mons'} or len(row['land_mons']['mons']) != 12:
                raise ValueError('Unsupported encounter field/table shape')
            for mon in row['land_mons']['mons']:
                if set(mon) != {'min_level', 'max_level', 'species'} or not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                    raise ValueError('Invalid encounter slot')
                species_symbols.add(mon['species'])
            encounters.append(dict(table=row, slotWeights=weights, sourceRef=src.ref(encounter_path, row['base_label']),
                                   runtimeStatus='unsupportedInForestRegion'))

    trainers = []
    trainer_symbols = []
    for block in scripts['blocks']:
        for line in block['lines']:
            m = re.fullmatch(r'\s*trainerbattle_single (TRAINER_\w+), (\w+), (\w+)\s*', line['text'])
            if m:
                trainer_symbols.append((m[1], block['label'], m[2], m[3], block['sourceRef']))
    if {t[0] for t in trainer_symbols} != {'TRAINER_BUG_CATCHER_' + n for n in ('RICK','DOUG','SAMMY','ANTHONY','CHARLIE')} or len(trainer_symbols) != 5:
        raise ValueError('Forest trainer roster differs')
    charmap = {c: int(v, 16) for c, v in re.findall(r"^'([A-Z])'\s*=\s*([0-9A-F]{2})$", src.text('charmap.txt'), re.M)}
    for symbol, label, intro, defeat, script_ref in sorted(trainer_symbols):
        path = 'src/data/trainers.h'
        match = one(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', path)
        f = fields(match[1])
        if set(f) != {'trainerClass','encounterMusic_gender','trainerPic','trainerName','items','doubleBattle','aiFlags','party'}:
            raise ValueError('Trainer fields differ')
        if f['items'] != '{}' or f['doubleBattle'] != 'FALSE' or f['aiFlags'] != 'AI_SCRIPT_CHECK_BAD_MOVE':
            raise ValueError('Unsupported forest trainer layout')
        party_symbol = re.fullmatch(r'NO_ITEM_DEFAULT_MOVES\((\w+)\)', f['party'])[1]
        pp = 'src/data/trainer_parties.h'
        pm = one(r'^static const struct TrainerMonNoItemDefaultMoves ' + party_symbol + r'\[\] = \{\n(.*?)^\};', pp)
        entries = list(re.finditer(r'    \{\n(.*?)    \},\n', pm[1], re.S))
        if not entries or ''.join(e[0] for e in entries) != pm[1]:
            raise ValueError('Unparsed forest trainer party')
        party = []
        for entry in entries:
            pf = fields(entry[1])
            if set(pf) != {'iv','lvl','species'} or not pf['iv'].isdigit() or not pf['lvl'].isdigit():
                raise ValueError('Unsupported trainer member fields')
            species_symbols.add(pf['species'])
            party.append(dict(species=pf['species'], sourceSpeciesId=constant(pf['species']), level=int(pf['lvl']),
                              sourceIv=int(pf['iv']), heldItem='ITEM_NONE', customMoves=[],
                              speciesName=one(r'^    \[' + pf['species'] + r'\]\s*= _\("([A-Z]+)"\),$', 'src/data/text/species_names.h')[1]))
        name = re.fullmatch(r'_\("([A-Z]+)"\)', f['trainerName'])[1]
        money = one(r'^    \{' + f['trainerClass'] + r', (\d+)\},$', 'src/battle_main.c')
        trainers.append(dict(symbol=symbol, sourceTrainerId=constant(symbol), declaration=f,
                             name=name, encodedName=[charmap[c] for c in name], baseMoney=int(money[1]), party=party,
                             partySourceRef=reference(pp, party_symbol, pm), sourceRef=reference(path, symbol, match),
                             sourceTrainerClass=constant(f['trainerClass']), sourceTrainerPic=constant(f['trainerPic']),
                             encounterMusicGender=constant(f['encounterMusic_gender']), aiFlags=constant(f['aiFlags']), partyFlags=0,
                             scriptEntry=label, introText=intro, defeatText=defeat, scriptSourceRef=script_ref,
                             runtimeStatus='unsupportedInForestRegion'))
    expected_species = {'SPECIES_'+n for n in ('RATTATA','PIDGEY','CATERPIE','WEEDLE','METAPOD','KAKUNA','PIKACHU')}
    if species_symbols != expected_species:
        raise ValueError('Wild/trainer species scope differs')
    species = []
    move_symbols = {'MOVE_CUT'}
    for symbol in sorted(species_symbols):
        f, ref = table('src/data/pokemon/species_info.h', symbol)
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', 'src/data/pokemon/level_up_learnset_pointers.h')[1]
        path = 'src/data/pokemon/level_up_learnsets.h'
        m = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', path)
        lines = m[1].splitlines()
        if not lines or lines[-1].strip() != 'LEVEL_UP_END':
            raise ValueError('Learnset terminator absent')
        learns = []
        for line in lines[:-1]:
            row = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if row is None:
                raise ValueError('Unparsed level-up move')
            learns.append(dict(level=int(row[1]), move=row[2])); move_symbols.add(row[2])
        path = 'src/data/pokemon/evolution.h'
        evolution = one(r'^    \[' + symbol + r'\]\s*= ([^\n]*),$', path)
        species.append(dict(symbol=symbol, sourceSpeciesId=constant(symbol), declaration=f, sourceRef=ref,
                            levelUpMoves=learns, learnsetSourceRef=reference('src/data/pokemon/level_up_learnsets.h', pointer, m),
                            evolutionDeclaration=evolution[1], evolutionSourceRef=reference(path, symbol, evolution),
                            evolutionStatus='unsupported; targets outside direct regional species are not imported',
                            runtimeStatus='unsupportedInForestRegion'))
    moves = []
    for symbol in sorted(move_symbols):
        f, ref = table('src/data/battle_moves.h', symbol)
        if set(f) != {'effect','power','type','accuracy','pp','secondaryEffectChance','target','priority','flags'}:
            raise ValueError('Move fields differ')
        moves.append(dict(symbol=symbol, sourceMoveId=constant(symbol), declaration=f, sourceRef=ref,
                          runtimeStatus='unsupportedInForestRegion'))
    item_symbols = {e['item'] for w in worlds for e in w['events']['bg_events'] if e['type'] == 'hidden_item'}
    for b in scripts['blocks']:
        for row in b['lines']:
            m = re.fullmatch(r'\s*finditem (ITEM_\w+)\s*', row['text'])
            if m: item_symbols.add(m[1])
    item_table = src.json('src/data/items.json')['items']
    items = []
    for symbol in sorted(item_symbols):
        row = next(i for i in item_table if i.get('itemId') == symbol)
        items.append(dict(symbol=symbol, table=row, sourceRef=src.ref('src/data/items.json', symbol), runtimeStatus='unsupportedInForestRegion'))
    event_inventory = []
    references = {src.json(p)['id']: (p, src.json(p)) for p in src.data if p.startswith('data/maps/') and p.endswith('/map.json')}
    for world in worlds:
        events = world['events']
        for index, obj in enumerate(events['object_events']):
            record = dict(map=events['id'], sourceObjectIndex=index, localId=index + 1, object=obj,
                          runtimeStatus='unsupportedInForestRegion')
            if obj['type'] == 'clone':
                path, target = references[obj['target_map']]
                targets = [(i, o) for i, o in enumerate(target['object_events']) if o.get('local_id') == obj['target_local_id']]
                if len(targets) != 1:
                    raise ValueError('Unresolved clone object target')
                target_index, target_obj = targets[0]
                record.update(cloneTarget=dict(map=target['id'], sourceObjectIndex=target_index, localId=target_index+1,
                                              object=target_obj, sourceRef=src.ref(path,obj['target_local_id'])))
            elif not 0 <= obj['x'] < world['width'] or not 0 <= obj['y'] < world['height']:
                raise ValueError('Ordinary object outside map')
            event_inventory.append(record)
    unsupported = dict(opcodes=sorted(c['opcode'] for c in scripts['commands'] if not c['opcode'].startswith('.')),
                       movements=[m['opcode'] for m in scripts['movements']],
                       autonomousMovementTypes=sorted({o['movement_type'] for w in worlds for o in w['events']['object_events'] if 'movement_type' in o}),
                       nativeSpecials=[s['symbol'] for s in scripts['specials']],
                       encounters=[e['table']['base_label'] for e in encounters], trainers=[t['symbol'] for t in trainers],
                       fieldEffects=['FLDEFF_USE_CUT_ON_TREE'], eventUnions=['clone','hidden_item'],
                       other=['Evolution execution and evolution target species','Trainer sight/approach and persistent beaten flags',
                              'Item flags/bag-full branches','Original movement timing, audio and map animations',
                              'External boundaries: Pewter City, Digletts Cave, Route 2 House and East Building'])
    return dict(schemaVersion=1,   completeGame=False,
                encounters=encounters, noEncounterMaps=[w['events']['id'] for w in worlds if not any(e['table']['map']==w['events']['id'] for e in encounters)],
                trainers=trainers, species=species, moves=moves, items=items, objects=event_inventory,
                coordinateEvents=[dict(map=w['events']['id'], events=w['events']['coord_events']) for w in worlds],
                backgroundEvents=[dict(map=w['events']['id'], events=w['events']['bg_events']) for w in worlds],
                unsupported=unsupported)


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve() == raw.resolve() or out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.forest-region-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        constants = constants_inventory(src, worlds, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        dependencies = dependency_inventory(src, worlds, scripts, constants)
        catalog = dict(schemaVersion=1,  commit=COMMIT,

                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=world['events']['id'], directory=directory)
                             for (name, stable, directory), world in zip(MAPS, worlds)],
                       connections=connections, warps=warps)
        for filename, document in package_documents(generated_root(out), constants, {'forest-catalog.json': catalog, 'scripts.json': scripts,
                                    'npc-source.json': npcs, 'dependencies.json': dependencies}).items():
            (stage / filename).write_bytes(canonical(document).encode('utf-8'))
        publish_map_package(stage)
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1,


                         outputs=outputs)
        manifest_bytes = canonical(manifest).encode('utf-8')
        (stage / 'manifest.json').write_bytes(manifest_bytes)
        for path in sorted(stage.rglob('*')):
            require_safe_target(out, out / path.relative_to(stage))
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
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/maps/regions/forest')
    parser.add_argument('--reference-repo', type=pathlib.Path,
                        default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'),
                        help='Local immutable Git repository; also FIRERED_ORIGINAL_REFERENCE. No network or upstream execution.')
    args = parser.parse_args()
    if args.reference_repo is not None:
        materialize(args.raw, args.reference_repo)
    import_catalog(args.raw, args.out)
