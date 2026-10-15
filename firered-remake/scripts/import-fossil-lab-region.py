#!/usr/bin/env python3
"""Import the original Cinnabar fossil revival rooms and authenticate its retained city boundary.

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
PACKAGE = 'fossil-lab-region'
RESOURCE_ID = 'firered-fossil-lab-region-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['CinnabarIsland_PokemonLab_Entrance', 'CinnabarIsland_PokemonLab_ExperimentRoom']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_Lab': 'secondary/lab',
            'gTileset_General': 'primary/general', 'gTileset_CinnabarIsland': 'secondary/cinnabar_island'}
CITY_PACKAGE = 'content/generated/cinnabar-source-preparation'
CITY_PATH = 'cinnabar-island/runtime-map.json'
NPC_REUSE = {'OBJ_EVENT_GFX_BOY', 'OBJ_EVENT_GFX_RED_NORMAL', 'OBJ_EVENT_GFX_SCIENTIST'}


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path):
    spec = importlib.util.spec_from_file_location('fossil_lab_reader', path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def script_inventory(src, reader, worlds):
    """Bounded map/event/standard-script closure, with original source references."""
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or
              any(p in (f'data/maps/{n}/scripts.inc', f'data/maps/{n}/text.inc') for n in MAPS))]
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
                implicitSpecials=[],
                blocks=result, aliases=used_aliases, opcodeCounts=dict(sorted(opcodes.items())),
                specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def constants_inventory(src, scripts, worlds):
    definitions, values = {}, {}
    paths = ['include/constants/' + name + '.h' for name in
             ['flags', 'vars', 'event_objects', 'event_object_movement', 'map_scripts', 'species',
              'moves', 'items', 'pokemon', 'battle', 'battle_setup', 'field_effects', 'songs',
              'sound', 'global', 'opponents', 'trainers', 'menu', 'game_stat', 'metatile_behaviors', 'field_weather', 'trade']]
    for path in paths:
        for line, text in enumerate(src.text(path).splitlines(), 1):
            m = re.match(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)', text)
            if m: definitions[m[1]] = (m[2].split('//')[0].strip(), path, line)
    for path in ['asm/macros/event.inc', 'data/event_scripts.s'] + [f'data/maps/{n}/scripts.inc' for n in MAPS]:
        for line, text in enumerate(src.text(path).splitlines(), 1):
            m = re.match(r'\s*([A-Z][A-Z0-9_]+)\s*=\s*(\d+)\s*$', text)
            n = re.match(r'\s*\.(?:set|equ)\s+(\w+),\s*(\d+)\s*$', text)
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
    world = reader.decode(src, 'CinnabarIsland')
    compact_check(json.loads(raw), world, src)
    entrances = [dict(index=i, event=e) for i, e in enumerate(world['events']['warp_events']) if e['dest_map'] == 'MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE']
    need(len(entrances) == 1 and entrances[0]['index'] == 2 and entrances[0]['event'] ==
         dict(x=8, y=9, elevation=0, dest_map='MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE', dest_warp_id='1'), 'Original city entrance differs')
    return dict(map='map.cinnabar-island', operation='reuse-existing-source-map', geometryChanges=[], eventChanges=[],
                currentPackage=CITY_PACKAGE,  currentResource=entry,
                entrances=entrances, requiredIntegration='Register the two fossil lab rooms; retain Cinnabar geometry, events and original warp indices.'), world


def event_contract(src, worlds, scripts, retained, links):
    entrance, experiment = worlds
    bylabel = {b['label']: b for b in scripts['blocks']}
    prefix = 'CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_'
    state = 'VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_REVIVE_STATE'
    which = 'VAR_MAP_SCENE_CINNABAR_ISLAND_POKEMON_LAB_EXPERIMENT_ROOM_WHICH_FOSSIL'
    body = lambda label: '\n'.join(r['text'] for r in bylabel[label]['lines'])
    need([(w['width'], w['height'], len(w['cells']), len(w['borderBlocks'])) for w in worlds] == [(28, 11, 308, 4), (15, 11, 165, 4)], 'Fossil lab dimensions differ')
    need([(len(w['events']['object_events']), len(w['events']['warp_events']), len(w['events']['coord_events']), len(w['events']['bg_events'])) for w in worlds] == [(1, 6, 0, 4), (2, 1, 0, 0)], 'Fossil lab event cohort differs')
    transition = 'CinnabarIsland_PokemonLab_Entrance_OnTransition'
    ready = 'CinnabarIsland_PokemonLab_Entrance_EventScript_ReadyFossil'
    need(f'call_if_eq {state}, 1, {ready}' in body(transition) and f'setvar {state}, 2' in body(ready), 'Source transition readiness differs')
    objects = [dict(map=w['events']['id'], localId=i, event=e, sourceRef=bylabel[e['script']]['sourceRef'])
               for w in worlds for i,e in enumerate(w['events']['object_events'], 1)]
    gifts = []
    for token, species, fossil, revived in [('Helix', 'OMANYTE', 'HELIX_FOSSIL', 'HELIX'), ('Dome', 'KABUTO', 'DOME_FOSSIL', 'DOME'), ('Amber', 'AERODACTYL', 'OLD_AMBER', 'AMBER')]:
        label = prefix + 'Give' + species.capitalize()
        give = body(label)
        need(f'givemon SPECIES_{species}, 5' in give and give.index('EventScript_NoMoreRoomForPokemon') < give.index(f'setflag FLAG_REVIVED_{revived}'), 'Source delivery guard/order differs')
        gifts.append(dict(species='SPECIES_' + species, level=5, heldItem='ITEM_NONE', item='ITEM_' + fossil,
                          selection=fossil, acquiredFlag='FLAG_GOT_' + fossil, revivedFlag='FLAG_REVIVED_' + revived,
                          command='givemon', sourceRef=bylabel[label]['sourceRef']))
    menus = []
    menu_source = src.text('src/script_menu.c')
    for name,symbol in [('Helix','HELIX'), ('Dome','DOME'), ('Amber','AMBER'), ('HelixAmber','HELIX_AMBER'), ('DomeAmber','DOME_AMBER')]:
        label = 'sMultichoiceList_' + name
        match = re.search(r'static const struct MenuAction ' + label + r'\[\] = \{\n(.*?)\n\};', menu_source, re.S)
        need(match is not None, 'Missing fossil menu ' + label)
        entries = re.findall(r'\{ (\w+) \}', match[1])
        need(len(entries) == (3 if name.endswith('Amber') and name != 'Amber' else 2), 'Fossil menu size differs')
        menus.append(dict(symbol='MULTICHOICE_' + symbol, entries=entries, cancelResult=127,
                          sourceRef=src.ref('src/script_menu.c', label, menu_source[:match.start()].count('\n')+1)))
    unsupported_maps = [dict(sourceMap=entrance['events']['id'], sourceWarpIndex=i, event=e, status='unimplemented-destination-preserved')
                        for i,e in enumerate(entrance['events']['warp_events']) if i in (3, 4)]
    unsupported = dict(map=experiment['events']['id'], localId=1, script=prefix + 'Garett',
                       reason='NPC trade entry is preserved as source evidence but not admitted; no trade or successful result may be synthesized.',
                       sourceRef=bylabel[prefix + 'Garett']['sourceRef'])
    return dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, runtimeIntegrated=False, maps=MAPS,
                retainedCity=retained, mapTopology=links, objects=objects, giftChoices=gifts, menus=menus,
                backgroundEvents=entrance['events']['bg_events'], unsupportedWarpDestinations=unsupported_maps,
                unsupportedNpcEntries=[unsupported],
                mapHeaders=[dict(map=w['events']['id'], **{k:w['events'][k] for k in ['music', 'map_type', 'battle_scene', 'region_map_section', 'allow_cycling', 'allow_escaping', 'allow_running', 'requires_flash']}) for w in worlds],
                entryConditions=dict(storyFlags=[], sourceMapWarpIndex=2, destinationMapWarpIndex=1),
                revivalState=dict(variable=state, selectedFossilVariable=which, idle=0, pending=1, ready=2,
                                  readinessTrigger='MAP_SCRIPT_ON_TRANSITION', readinessMap=entrance['events']['id'],
                                  readinessSource=bylabel[transition]['sourceRef'], timerBased=False,
                                  successResetsState=True, successPreservesSelectedFossil=True,
                                  fullStoragePreservesReadyState=True, results=dict(party=0, pc=1, full=2)),
                limitations=['Source/resource contract only; gift command admission belongs to Application.',
                             'Only Entrance and ExperimentRoom are added. Original Lounge and ResearchRoom warps remain unimplemented and unchanged.',
                             'Garett and common trade source are retained but NPC trade is explicitly outside this increment.',
                             'Revival menus use acquisition and revived flags, not current bag item presence.',
                             'Revival completes on the original Entrance transition; no timer or required city exit is introduced.',
                             'Full party uses PC; exhausted storage retains ready state and does not set the revived flag.'])


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
    src = reader.Source(ref); worlds = [reader.decode(src, name) for name in MAPS]
    retained, city = retained_city(src, reader, current)
    scripts = script_inventory(src, reader, worlds); constants = constants_inventory(src, scripts, worlds)
    for name in MAPS:
        for leaf in ['scripts.inc', 'text.inc']: src.read(f'data/maps/{name}/' + leaf, 'live-map-script')
    src.read('data/maps/map_groups.json', 'source-map-number-projection')
    links = reader.topology(src, worlds)
    entrance = retained['entrances'][0]; event = entrance['event']; index = entrance['index']; target_index = int(event['dest_warp_id'])
    end = worlds[0]['events']['warp_events'][target_index]
    links['warps'].append(dict(sourceMap='MAP_CINNABAR_ISLAND', sourceIndex=index, source=event,
                              destinationMap='MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE', destinationIndex=target_index, destination=end,
                              dynamic=False, reciprocalIndex=int(end['dest_warp_id']) == index))
    need(len(links['warps']) == 8 and sum(w['reciprocalIndex'] for w in links['warps']) == 6, 'Fossil lab boundary topology differs')
    contracts = event_contract(src, worlds, scripts, retained, links)
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='fossil-lab-', dir=target) as temp:
        stage = Path(temp)
        for world in worlds:
            relative = reader.directory(world['sourceName'])
            shared.import_map(src, world['sourceName'], world['stableId'], stage / relative)
        npcs = reuse_npcs(src, shared, worlds, stage, current)
        entries = []
        for world in worlds:
            relative = reader.directory(world['sourceName'])
            path = stage / relative / 'runtime-map.json'; rich = path.read_bytes()
            compact = converter.normalize_runtime(json.loads(rich)); compact_check(compact, world, src)
            data = converter.encode(compact); (stage / relative / 'runtime-map.json').write_bytes(data)
            entries.append(dict(sourceName=world['sourceName'], stableId=world['stableId'], directory=relative, sourceMapSymbol=world['events']['id'],
                        path=relative + '/runtime-map.json'))
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False); lock_bytes = canonical(lock)
        docs = package_documents(target / 'content/generated', constants, {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
                                     maps=entries, tilesets=TILESETS,
                                    mapSymbols={w['events']['id']: w['stableId'] for w in worlds + [city]}),
                'scripts.json': scripts,  'contracts.json': contracts, 'npc-source.json': npcs,
                'geometry.json': dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,

                        inputCount=len(src.entries), mapCount=2, cellCount=473, borderCellCount=8, directedWarpCount=8,
                        objectCount=3, giftChoiceCount=3, coordinateEventCount=0, backgroundEventCount=4,
                        unsupportedWarpCount=2, unsupportedNpcEntryCount=1,
                        reusedNpcGraphicsCount=len(npcs['graphics']), scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        writes = [(target / 'scripts/fossil-lab-region.contract.json', canonical(contracts))]
        writes += [(target / 'content/raw/pokefirered/037335f4' / p, data) for p, data in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
                              mapCount=2, cellRoundtripCount=473, borderCellRoundtripCount=8, unchangedCityCells=len(city['cells']),
                              inputCount=len(src.entries), outputCount=len(outputs), scriptBlockCount=len(scripts['blocks']), check=args.check), indent=2))


if __name__ == '__main__': main()
