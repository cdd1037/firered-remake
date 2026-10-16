#!/usr/bin/env python3
"""Import seven original Seafoam/Route19/Route20 maps and the two stopped-current layouts.

Only pinned reviewed local converters execute; upstream Git blobs are inert data.
This resource package does not admit battles or field commands by itself.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

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
ROOT = _PROJECT_ROOT
PACKAGE = 'maps/regions/seafoam'
RESOURCE_ID = 'firered-seafoam-resources-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['SeafoamIslands_1F', 'SeafoamIslands_B1F', 'SeafoamIslands_B2F', 'SeafoamIslands_B3F', 'SeafoamIslands_B4F', 'Route19', 'Route20']
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_SeafoamIslands': 'secondary/seafoam_islands',
            'gTileset_FuchsiaCity': 'secondary/fuchsia_city', 'gTileset_CinnabarIsland': 'secondary/cinnabar_island'}


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path):
    spec = importlib.util.spec_from_file_location('power_plant_reader', path)
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
    roots.update(e['script'] for w in worlds for kind in ('object_events', 'coord_events', 'bg_events') for e in w['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    # The source engine selects hidden items directly; event macros call these
    # standard scripts indirectly through gStdScripts rather than by label.
    implicit = ['EventScript_HiddenItemScript', 'Std_FindItem', 'Std_MsgboxNPC',
                'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_PutItemAway',
                'EventScript_DoFallWarp', 'EventScript_TryDoNormalTrainerBattle', 'EventScript_TryDoDoubleTrainerBattle']
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
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    used_aliases = {a: b for a, b in aliases.items() if b in selected or any(re.search(r'\b'+re.escape(a)+r'\b', row['text']) for block in result for row in block['lines'])}
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,

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
    tokens = set(re.findall(r'\b[A-Z][A-Z0-9_x]+\b', operands + json.dumps([w['events'] for w in worlds]) + json.dumps(scripts['aliases'])))
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

NPC_REUSE = {'camper', 'item-ball', 'picnicker', 'pushable-boulder', 'red-normal', 'rocker', 'swimmer-f-water', 'swimmer-m-land', 'swimmer-m-water', 'tuber-m-water'}


def extra_constants(src, constants, scripts):
    path = 'include/constants/metatile_labels.h'
    used = '\n'.join(row['text'] for block in scripts['blocks'] for row in block['lines'])
    for match in re.finditer(r'^#define\s+(METATILE_\w+)\s+(0x[0-9A-Fa-f]+)\s*$', src.text(path), re.M):
        if match[1] in used:
            constants['values'][match[1]] = dict(value=int(match[2], 16), sourceRef=src.ref(path, match[1], src.text(path)[:match.start()].count('\n')+1))
    for index, layout in enumerate(src.json('data/layouts/layouts.json')['layouts'], 1):
        symbol = layout.get('id', '')
        if symbol.startswith('LAYOUT_SEAFOAM_ISLANDS_'):
            constants['values'][symbol] = dict(value=index, sourceRef=src.ref('data/layouts/layouts.json', symbol))
    for alias, symbol in scripts['aliases'].items():
        if symbol not in constants['values']:
            path='include/constants/vars.h';text=src.text(path)
            match=re.search(r'^#define\s+'+re.escape(symbol)+r'\s+(0x[0-9A-Fa-f]+|[0-9]+)\b',text,re.M)
            if match:
                constants['values'][symbol]=dict(value=int(match[1],0),sourceRef=src.ref(path,symbol,text[:match.start()].count('\n')+1))
        if symbol in constants['values']:
            constants['values'][alias] = dict(value=constants['values'][symbol]['value'], aliasOf=symbol,
                sourceRef=src.ref('data/maps/SeafoamIslands_B3F/scripts.inc', alias, 1))


def source_declaration(src, path, symbol):
    text = src.text(path)
    matches = list(re.finditer(r'^(?:static )?[^\n;]+\b'+re.escape(symbol)+r'\([^;\n]*\)\n\{.*?^\}', text, re.M|re.S))
    need(len(matches) == 1, 'Missing source declaration: '+symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, text[:match.start()].count('\n')+1))


def alternate_world(src, reader, shared, base, stage):
    from layout_variants import import_layout_variant
    return import_layout_variant(src, reader, shared, base,
        base['events']['layout']+'_CURRENT_STOPPED', stage)


def compact_variant(base, rich, world, src):
    """Bounded original-layout adapter. The reviewed base codec remains unchanged."""
    doc = dict(base)
    doc['blocks'] = base64.b64encode(src.read(world['layout']['blockdata_filepath'])).decode()
    doc['borderBlocks'] = base64.b64encode(src.read(world['layout']['border_filepath'])).decode()
    doc['artifacts'] = json.loads(rich)['artifacts']
    attrs = ['data/tilesets/'+TILESETS[world['layout'][key]]+'/metatile_attributes.bin' for key in ('primary_tileset', 'secondary_tileset')]
    sources = sorted(attrs)
    doc['attributeSources'] = [dict(path=p) for p in sources]
    mids = sorted({v & 1023 for p in ('blockdata_filepath','border_filepath') for v in src.units(world['layout'][p], 'H')})
    doc['metatiles'] = [[mid, src.units(attrs[mid >= 640], 'I')[mid if mid < 640 else mid-640], sources.index(attrs[mid >= 640])] for mid in mids]
    names = {int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)', src.text('include/constants/metatile_behaviors.h'), re.M)}
    doc['behaviors'] = [dict(id=b, name=names[b]) for b in sorted({row[1] & 511 for row in doc['metatiles']})]
    compact_check(doc, world, src)
    return doc


def topology(src, worlds):
    headers={w['events']['id']:w['events'] for w in worlds}
    for name in ('FuchsiaCity','CinnabarIsland'):
        value=src.json('data/maps/'+name+'/map.json');headers[value['id']]=value
    warps=[];connections=[];opposite={'up':'down','down':'up','left':'right','right':'left'}
    for world in worlds:
        symbol=world['events']['id']
        for index,event in enumerate(world['events']['warp_events']):
            destination=headers[event['dest_map']];target=int(event['dest_warp_id']);end=destination['warp_events'][target]
            warps.append(dict(sourceMap=symbol,sourceIndex=index,source=event,destinationMap=destination['id'],destinationIndex=target,destination=end,dynamic=False,
                reciprocalIndex=end['dest_map']==symbol and int(end['dest_warp_id'])==index))
        for edge in world['events']['connections'] or []:
            dest=headers[edge['map']];back=[e for e in dest['connections'] or [] if e['map']==symbol and e['direction']==opposite[edge['direction']]]
            need(len(back)==1 and back[0]['offset']==-edge['offset'],'Nonreciprocal Seafoam route connection')
            connections.append(dict(sourceMap=symbol,reciprocal=True,destinationConnections=dest['connections'],**edge))
    return dict(warps=warps,connections=connections)


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
    for p in (target, dep, ref, current): safe(p)
    target, dep, ref, current = (Path(os.path.abspath(p)) for p in (target, dep, ref, current))
    need(not (target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep/'scripts/imports/source/import-cinnabar-source-preparation.py')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    src = reader.Source(ref); worlds = [reader.decode(src, name) for name in MAPS]
    scripts = script_inventory(src, reader, worlds)
    constants = constants_inventory(src, scripts, worlds); extra_constants(src, constants, scripts)
    links = topology(src, worlds)
    bylabel = {b['label']: b for b in scripts['blocks']}
    objects, pickups, hidden, boulders, trainers, statics = [], [], [], [], [], []
    for world in worlds:
        for index, event in enumerate(world['events']['object_events'], 1):
            row = dict(map=world['events']['id'], mapId=world['stableId'], localId=index, event=event,
                       sourceRef=world['sourceRef'])
            objects.append(row)
            body = '\n'.join(x['text'] for x in bylabel.get(event['script'], {}).get('lines', []))
            item = re.search(r'\bfinditem (ITEM_\w+)(?:, (\d+))?', body)
            mon = re.search(r'\bsetwildbattle (SPECIES_\w+), (\d+)', body)
            trainer = re.search(r'\b(trainerbattle_\w+) (TRAINER_\w+)', body)
            if item: pickups.append(dict(row, item=item[1], quantity=int(item[2] or 1), sourceRef=bylabel[event['script']]['sourceRef']))
            if mon: statics.append(dict(**row, species=mon[1], level=int(mon[2]), scriptSource=bylabel[event['script']]['sourceRef']))
            if trainer: trainers.append(dict(**row, trainer=trainer[2], opcode=trainer[1], scriptSource=bylabel[event['script']]['sourceRef'], ordinaryRequired=True))
            if event['graphics_id'] == 'OBJ_EVENT_GFX_PUSHABLE_BOULDER':
                reveal = event['trainer_type'] if event['trainer_type'].startswith('FLAG_') else None
                boulders.append(dict(**row, pushable=event['script']=='EventScript_StrengthBoulder', revealFlag=reveal))
        hidden.extend(dict(map=world['events']['id'], mapId=world['stableId'], event=e, sourceRef=world['sourceRef']) for e in world['events']['bg_events'] if e['type']=='hidden_item')
    need(len(trainers)==22 and len({t['trainer'] for t in trainers})==21, 'Original trainer cohort differs')
    need(len(statics)==1 and statics[0]['species']=='SPECIES_ARTICUNO' and statics[0]['localId']==3, 'Original Articuno differs')
    need(len(pickups)==5 and len(hidden)==3 and len(boulders)==14, 'Original object cohort differs')
    symbols = {r['item'] for r in pickups} | {r['event']['item'] for r in hidden}
    items = [i for i in src.json('src/data/items.json')['items'] if i['itemId'] in symbols]
    ep = 'src/data/wild_encounters.json'; groups = []
    for group in src.json(ep)['wild_encounter_groups']:
        chosen = [e for e in group['encounters'] if e['map'] in {w['events']['id'] for w in worlds} and e['base_label'].endswith('_FireRed')]
        if chosen: groups.append(dict(**{k:v for k,v in group.items() if k!='encounters'}, encounters=chosen))
    trainer_declarations = []
    for symbol in sorted({t['trainer'] for t in trainers}):
        path='src/data/trainers.h'; text=src.text(path); m=re.search(r'^    \['+symbol+r'\] = \{\n.*?^    \},',text,re.M|re.S); need(m is not None,'Missing trainer '+symbol)
        party=re.search(r'\.party = \w+\((\w+)\)',m[0])[1]; pp='src/data/trainer_parties.h'; pt=src.text(pp)
        pm=re.search(r'^static const struct \w+ '+party+r'\[\] = \{\n.*?^\};',pt,re.M|re.S);need(pm is not None,'Missing party '+party)
        trainer_declarations.append(dict(trainer=symbol, sourceText=m[0], sourceRef=src.ref(path,symbol,text[:m.start()].count('\n')+1),
            partyText=pm[0], partySourceRef=src.ref(pp,party,pt[:pm.start()].count('\n')+1)))
    engine_specs = {
        'src/field_control_avatar.c': ['HandleBoulderFallThroughHole', 'TryStartWarpEventScript'],
        'src/field_player_avatar.c': ['DoBoulderFinish', 'SeafoamIslandsB4F_CurrentDumpsPlayerOnLand'],
        'src/event_object_movement.c': ['GetBoulderRevealFlagByLocalIdAndMap', 'RemoveObjectEventByLocalIdAndMap'],
        'src/overworld.c': ['MetatileBehavior_IsSurfableInSeafoamIslands', 'UpdateEscapeWarp'],
        'src/scrcmd.c': ['ScrCmd_setmaplayoutindex'],
        'src/field_specials.c': ['ForcePlayerToStartSurfing']}
    declarations = [source_declaration(src,path,symbol) for path,names in engine_specs.items() for symbol in names]
    boundaries=[]
    for name, path in [('FuchsiaCity','content/generated/maps/regions/fuchsia-route/fuchsia-city.json'),
                       ('CinnabarIsland','content/generated/maps/regions/cinnabar/cinnabar-island.json')]:
        data=(current/path).read_bytes(); doc=json.loads(data); original=src.json('data/maps/'+name+'/map.json')
        need(doc['events']==original, 'Existing boundary events differ: '+name)
        boundaries.append(dict(sourceName=name,mapId=doc['stableId'],path=path,connections=original['connections'],operation='reuse-existing-unchanged'))
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='seafoam-',dir=target) as temp:
        stage=Path(temp); variants=[]; variant_worlds=[]
        for world in worlds:
            shared.import_map(src,world['sourceName'],world['stableId'],stage/reader.directory(world['sourceName']))
        for base in [w for w in worlds if w['sourceName'] in ('SeafoamIslands_B3F','SeafoamIslands_B4F')]:
            relative=reader.directory(base['sourceName'])+'-current-stopped'
            world, variant=alternate_world(src,reader,shared,base,stage/relative)
            variants.append(dict(**variant,directory=relative));variant_worlds.append(world)
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        need(any(r['graphicsId']=='OBJ_EVENT_GFX_ARTICUNO' for r in npcs['graphics']),'Articuno graphics missing')
        entries=[]; compact_by_id={}
        for world in worlds:
            relative=reader.directory(world['sourceName']);p=stage/relative/'runtime-map.json';rich=p.read_bytes()
            compact=converter.normalize_runtime(json.loads(rich));compact_check(compact,world,src);compact_by_id[world['stableId']]=compact
            data=converter.encode(compact);(stage/relative/'runtime-map.json').write_bytes(data)
            entries.append(dict(sourceName=world['sourceName'],stableId=world['stableId'],directory=relative,sourceMapSymbol=world['events']['id'],
                path=relative+'/runtime-map.json',
                width=world['width'],height=world['height'],layout=world['layout']))
        for variant,world in zip(variants,variant_worlds,strict=True):
            relative=variant['directory'];p=stage/relative/'runtime-map.json';rich=p.read_bytes()
            compact=compact_variant(compact_by_id[world['stableId']],rich,world,src);data=converter.encode(compact)
            (stage/relative/'runtime-map.json').write_bytes(data)
            variant.update(sourceName=world['sourceName'],stableId=world['stableId'],sourceMapSymbol=world['events']['id'],path=relative+'/runtime-map.json')
        contracts=dict(schemaVersion=1,resourceId=RESOURCE_ID,maps=MAPS,
            retainedBoundaries=boundaries,mapTopology=links,objects=objects,itemPickups=pickups,hiddenItems=hidden,boulders=boulders,staticEncounters=statics,
            ordinaryTrainerObjects=trainers,ordinaryTrainerCount=21,ordinaryTrainerObjectCount=22,layoutVariants=variants,
            currentCells=[dict(mapId=w['stableId'],x=c['x'],y=c['y'],behaviorId=c['behaviorId'],behaviorName=c['behaviorName']) for w in worlds for c in w['cells'] if 'CURRENT' in c['behaviorName']],
            fallHoles=[dict(mapId=w['stableId'],x=c['x'],y=c['y'],behaviorId=c['behaviorId'],warpIndex=next(i for i,e in enumerate(w['events']['warp_events']) if e['x']==c['x'] and e['y']==c['y'])) for w in worlds for c in w['cells'] if c['behaviorName']=='MB_FALL_WARP'],
            mapCallbacks=[b for b in scripts['blocks'] if b['label'].endswith(('_MapScripts','_OnTransition','_OnResume','_OnLoad','_OnWarp','_OnFrame')) and b['sourceRef']['path'].startswith('data/maps/')],
            b4fLoadPatches=[dict(x=x,y=14,metatile='METATILE_General_CalmWater',collision=0) for x in (12,13)],
            boulderFallRule='Clear the pushed object trainer_type reveal flag regardless of hole destination; RemoveObjectEventByLocalIdAndMap persists its hide flag.',
            resetRule='Route20 OnTransition resets each boulder chain only while that chain current-stopped flag is unset.',
            engineDeclarations=declarations)
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lock_bytes=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,
                maps=entries,layoutVariants=variants,tilesets=TILESETS,
                mapSymbols={**{w['events']['id']:w['stableId'] for w in worlds},'MAP_FUCHSIA_CITY':'map.fuchsia-city','MAP_CINNABAR_ISLAND':'map.cinnabar-island'}),
              'scripts.json':scripts,'contracts.json':contracts,'npc-source.json':npcs,
              'items.json':dict(schemaVersion=1,declarations=items),'trainers.json':dict(schemaVersion=1,ordinaryObjects=trainers,declarations=trainer_declarations),
              'geometry.json':dict(maps=reader.geometry_inventory(worlds),layoutVariants=variants,tilesets=TILESETS),
              'encounters.json':dict(schemaVersion=1,sourceRef=src.ref(ep,'gWildMonHeaders'),groups=groups)}, check=args.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,resourceId=RESOURCE_ID,
            layoutVariantCount=2,
            directedWarpCount=len(links['warps']),pickupCount=len(pickups),hiddenItemCount=len(hidden),
            boulderCount=len(boulders),staticEncounterCount=1,ordinaryTrainerCount=21,ordinaryTrainerObjectCount=22,
            reusedNpcGraphicsCount=len(npcs['graphics']),outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        writes=[]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,data) for p,data in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if args.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
        if not args.check:
            for path,data in writes:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(mapCount=7,layoutVariantCount=2,inputCount=len(src.entries),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),
            rawBytes=sum(len(b) for b in src.data.values()),generatedBytes=sum(len(b) for p,b in writes if '/generated/' in str(p)),check=args.check),indent=2))


if __name__=='__main__':main()

