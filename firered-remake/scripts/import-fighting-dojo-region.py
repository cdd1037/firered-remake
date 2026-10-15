#!/usr/bin/env python3
"""Import the original Saffron Fighting Dojo and authenticate its retained city boundary.

Only pinned reviewed local converters execute; upstream Git blobs are inert data.
This resource package does not admit battles or field commands by itself.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import ast
import base64
import collections
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'fighting-dojo-region'
RESOURCE_ID = 'firered-fighting-dojo-region-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['SaffronCity_Dojo']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_PewterGym': 'secondary/pewter_gym',
            'gTileset_General': 'primary/general', 'gTileset_SaffronCity': 'secondary/saffron_city'}
CITY_PACKAGE = 'content/generated/silph-region'
CITY_PATH = 'saffron-city/runtime-map.json'
NPC_REUSE = {'OBJ_EVENT_GFX_BLACK_BELT', 'OBJ_EVENT_GFX_ITEM_BALL', 'OBJ_EVENT_GFX_RED_NORMAL'}


def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path):
    spec = importlib.util.spec_from_file_location('fighting_dojo_reader', path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_inventory(src, reader, worlds):
    """Bounded map/event/standard-script closure, with original source references."""
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p.endswith('/text.inc') or
              any(p == f'data/maps/{n}/scripts.inc' for n in MAPS))]
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
    roots.update(e['script'] for w in worlds for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in w['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo']
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
            if op == 'trainerbattle_single':
                for target in operands.split(',')[1:]:
                    target = target.strip()
                    if aliases.get(target, target) not in blocks:
                        unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    used_aliases = {a: b for a, b in aliases.items() if b in selected}
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}),
                implicitSpecials=[dict(symbol='GetQuestLogState', macro='goto_if_questlog', sourceRef=src.ref('asm/macros/event.inc', 'goto_if_questlog'))],
                blocks=result, aliases=used_aliases, opcodeCounts=dict(sorted(opcodes.items())),
                specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def constants_inventory(src, scripts, worlds):
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
    tokens = set(re.findall(r'\b[A-Z][A-Z0-9_x]+\b', operands + json.dumps([w['events'] for w in worlds])))
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
    generator = src.text('tools/mapjson/mapjson.cpp')
    need('json_to_string(obj_event, "local_id") << " " << i + 1' in generator, 'Local ID generator differs')
    for world in worlds:
        for index, event in enumerate(world['events']['object_events'], 1):
            if 'local_id' in event:
                values[event['local_id']] = dict(value=index, sourceRef=src.ref(
                    'data/maps/' + world['sourceName'] + '/map.json', event['local_id']))
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
    for path in document['scriptPaths']:
        src.read(path)


def retained_city(src, reader, current):
    manifest = (current / CITY_PACKAGE / 'manifest.json').read_bytes()
    entry = next(e for e in json.loads(manifest)['outputs'] if e['path'] == CITY_PATH)
    raw = (current / CITY_PACKAGE / CITY_PATH).read_bytes()
    world = reader.decode(src, 'SaffronCity')
    compact_check(json.loads(raw), world, src)
    entrances = [dict(index=i, event=e) for i, e in enumerate(world['events']['warp_events']) if e['dest_map'] == 'MAP_SAFFRON_CITY_DOJO']
    need(len(entrances) == 1 and entrances[0]['index'] == 2 and entrances[0]['event'] ==
         dict(x=40, y=12, elevation=0, dest_map='MAP_SAFFRON_CITY_DOJO', dest_warp_id='1'), 'Original city entrance differs')
    return dict(map='map.saffron-city', operation='reuse-existing-source-map', geometryChanges=[], eventChanges=[],
                currentPackage=CITY_PACKAGE,  currentResource=entry,
                entrances=entrances, requiredIntegration='Register map.saffron-city-dojo; retain Saffron geometry, events and original warp indices.'), world


def event_contract(src, world, scripts, retained, links):
    events = world['events']; bylabel = {b['label']: b for b in scripts['blocks']}
    objects, trainers, gifts = [], [], []
    for index, event in enumerate(events['object_events'], 1):
        block = bylabel[event['script']]
        row = dict(map=events['id'], localId=index, event=event, sourceRef=block['sourceRef'])
        objects.append(row)
        text = '\n'.join(r['text'] for r in block['lines'])
        battle = re.search(r'\btrainerbattle_single ([^\n]+)', text)
        picture = re.search(r'\bshowmonpic (SPECIES_\w+), (\d+), (\d+)', text)
        if battle:
            arguments = [a.strip() for a in battle[1].split(',')]
            trainers.append(dict(**row, trainer=arguments[0], battleMode=1 if len(arguments) == 4 else 0,
                                 opcode='trainerbattle_single', arguments=arguments))
        elif picture:
            gifts.append(dict(**row, species=picture[1], level=25, heldItem='ITEM_NONE',
                              picture=dict(x=int(picture[2]), y=int(picture[3])), sharedChoiceFlag='FLAG_GOT_HITMON_FROM_DOJO'))
        else:
            raise ValueError('Unexpected Dojo source object')
    need((world['width'], world['height'], len(world['cells']), len(world['borderBlocks'])) == (13, 16, 208, 4), 'Dojo dimensions differ')
    need((len(objects), len(trainers), len(gifts), len(events['warp_events']), len(events['coord_events']), len(events['bg_events'])) == (7, 5, 2, 3, 2, 4), 'Dojo event cohort differs')
    need([t['trainer'] for t in trainers] == ['TRAINER_BLACK_BELT_HITOSHI', 'TRAINER_BLACK_BELT_HIDEKI', 'TRAINER_BLACK_BELT_AARON', 'TRAINER_BLACK_BELT_MIKE', 'TRAINER_BLACK_BELT_KOICHI'], 'Trainer event order differs')
    need([t['battleMode'] for t in trainers] == [0, 0, 0, 0, 1] and trainers[-1]['arguments'][-1] == 'SaffronCity_Dojo_EventScript_DefeatedMasterKoichi', 'Master callback differs')
    need([(g['localId'], g['species']) for g in gifts] == [(6, 'SPECIES_HITMONLEE'), (7, 'SPECIES_HITMONCHAN')], 'Gift object order differs')
    for event in events['coord_events']:
        text = '\n'.join(r['text'] for r in bylabel[event['script']]['lines'])
        need(event['var'] == 'VAR_MAP_SCENE_SAFFRON_CITY_DOJO' and event['var_value'] == '0' and
             'setvar VAR_MAP_SCENE_SAFFRON_CITY_DOJO, 1' in text and 'trainerbattle' not in text, 'Coordinate trigger semantics differ')
    return dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, runtimeIntegrated=False, maps=MAPS,
                retainedCity=retained, mapTopology=links, objects=objects, trainers=trainers, giftChoices=gifts,
                coordinateEvents=events['coord_events'], backgroundEvents=events['bg_events'],
                mapHeaders=[dict(map=events['id'], **{k:events[k] for k in ['music', 'map_type', 'battle_scene', 'region_map_section', 'allow_cycling', 'allow_escaping', 'allow_running', 'requires_flash']})],
                entryConditions=dict(storyFlags=[], sourceMapWarpIndex=2, destinationMapWarpIndex=1),
                giftCommand=dict(opcode='givemon', speciesOperand='VAR_TEMP_1', level=25, results=dict(party=0, pc=1, full=2),
                                 sourceRef=bylabel['SaffronCity_Dojo_EventScript_GiveHitmon']['sourceRef']),
                limitations=['Source/resource contract only; battle and gift command admission belongs to Application.',
                             'Scene 1 is not victory evidence: either coordinate trigger sets it before normal trainer sight.',
                             'The source gift scripts contain no explicit defeated-master or defeated-four-trainers prerequisite.',
                             'Only the selected ball is removed; full party uses PC and fully exhausted storage retains the choice.'])


def reuse_npcs(src, shared, worlds, stage, current):
    # Region packages declare the IDs rendered by the shared overworld import.
    folder = stage / 'npc'
    inventory = shared.npc_inventory(src, worlds, folder)
    need({r['graphicsId'] for r in inventory['graphics']} == set(NPC_REUSE), 'NPC cohort differs')
    return inventory


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
    target, dep, ref, current = (Path(os.path.abspath(p)) for p in (target, dep, ref, current))
    need(not(target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    src = reader.Source(ref); world = reader.decode(src, MAPS[0]); worlds = [world]
    retained, city = retained_city(src, reader, current)
    scripts = script_inventory(src, reader, worlds); constants = constants_inventory(src, scripts, worlds)
    for leaf in ['scripts.inc', 'text.inc']:
        src.read('data/maps/SaffronCity_Dojo/' + leaf, 'live-map-script')
    src.read('data/maps/map_groups.json', 'source-map-number-projection')
    links = reader.topology(src, worlds)
    entrance = retained['entrances'][0]; event = entrance['event']; index = entrance['index']; target_index = int(event['dest_warp_id'])
    end = world['events']['warp_events'][target_index]
    links['warps'].append(dict(sourceMap='MAP_SAFFRON_CITY', sourceIndex=index, source=event,
                              destinationMap='MAP_SAFFRON_CITY_DOJO', destinationIndex=target_index, destination=end,
                              dynamic=False, reciprocalIndex=int(end['dest_warp_id']) == index))
    need(len(links['warps']) == 4 and sum(w['reciprocalIndex'] for w in links['warps']) == 2, 'Dojo boundary topology differs')
    contracts = event_contract(src, world, scripts, retained, links)
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='fighting-dojo-', dir=target) as temp:
        stage = Path(temp); relative = 'saffron-city-dojo'
        shared.import_map(src, MAPS[0], 'map.' + relative, stage / relative)
        npcs = reuse_npcs(src, shared, worlds, stage, current)
        path = stage / relative / 'runtime-map.json'; rich = path.read_bytes()
        compact = converter.normalize_runtime(json.loads(rich)); compact_check(compact, world, src)
        data = converter.encode(compact); (stage / relative / 'runtime-map.json').write_bytes(data)
        entries = [dict(sourceName=MAPS[0], stableId='map.' + relative, directory=relative, sourceMapSymbol=world['events']['id'],
                        path=relative + '/runtime-map.json')]
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False); lock_bytes = canonical(lock)
        docs = package_documents(target / 'content/generated', constants, {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
                                     maps=entries, inputFiles=lock['files'], tilesets=TILESETS,
                                    mapSymbols={w['events']['id']: w['stableId'] for w in worlds + [city]}),
                'scripts.json': scripts,  'contracts.json': contracts, 'npc-source.json': npcs,
                'geometry.json': dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,

                        inputCount=len(src.entries), mapCount=1, cellCount=208, borderCellCount=4, directedWarpCount=4,
                        objectCount=7, trainerCount=5, giftChoiceCount=2, coordinateEventCount=2, backgroundEventCount=4,
                        reusedNpcGraphicsCount=len(npcs['graphics']), scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        writes = [(target / 'scripts/fighting-dojo-region.contract.json', canonical(contracts))]
        writes += [(target / 'content/raw/pokefirered/037335f4' / p, data) for p, data in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
                              mapCount=1, cellRoundtripCount=208, borderCellRoundtripCount=4, unchangedCityCells=len(city['cells']),
                              inputCount=len(src.entries), outputCount=len(outputs), scriptBlockCount=len(scripts['blocks']), check=args.check), indent=2))


if __name__ == '__main__': main()
