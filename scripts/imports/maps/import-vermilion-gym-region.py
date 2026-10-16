#!/usr/bin/env python3
"""Authenticated local Vermilion Gym, trash switches, field Cut and source barrier metatiles; no gameplay activation."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure

from shared_constants import package_documents
from map_runtime import generated_root, publish_map_package
import argparse
import ast
import collections
import io
import importlib.util
import json
import pathlib
import re
import shutil
import struct
import tempfile
from PIL import Image
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vermilion-gym-region-import-v1'
MAPS = [('VermilionCity_Gym', 'map.vermilion-city-gym', 'vermilion-city-gym')]
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_VermilionGym':'secondary/vermilion_gym'}
EXTERNAL_ROOTS = ['EventScript_CutTree', 'EventScript_FldEffCut', 'EventScript_SetGymTrainers']
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_vermilion-gym_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='vermilion-gym-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/vermilion-gym-region.lock.json', maps=MAPS, profile=PROFILE)

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    parsed = parse_scripts(src)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'EventScript_TryDoNormalTrainerBattle',
                  'EventScript_DoTrainerBattleFromApproach', 'Std_ReceivedItem'])
    roots.update(EXTERNAL_ROOTS)
    roots.add('EventScript_CutTree')
    required = {'VermilionCity_Gym_EventScript_LtSurge','VermilionCity_Gym_EventScript_InitTrashCans',
                'VermilionCity_Gym_EventScript_TrashCan','EventScript_CutTree','EventScript_FldEffCut'}
    if not required <= roots: raise ValueError('Critical Vermilion Gym entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='sourceOnly')
    return result


def constants_inventory(src, worlds, scripts):
    # Include aliases and temporary state expressions omitted by the historical
    # numeric-only decoder. Every value still comes from an original declaration.
    result = SHARED.constants_inventory(src, worlds, scripts)
    values, declarations = result['values'], {}
    for path in sorted(src.data):
        if path.startswith('include/constants/'):
            for match in re.finditer(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)', src.text(path), re.M):
                declarations[match[1]] = (match[2].split('//')[0].strip(), src.ref(path, match[1], src.text(path)[:match.start()].count('\n') + 1))
    visiting = set()
    def resolve(name):
        if name in values: return values[name]['value']
        if name in visiting or name not in declarations: raise ValueError('Missing/cyclic constant: ' + name)
        visiting.add(name)
        expression, reference = declarations[name]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int: return node.value
            if isinstance(node, ast.Name): return resolve(node.id)
            if isinstance(node, ast.BinOp):
                left, right = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
            raise ValueError('Unsupported constant expression: ' + name)
        value = number(ast.parse(expression, mode='eval').body)
        values[name] = dict(value=value, sourceRef=reference)
        visiting.remove(name)
        return value
    for name in ['FLAG_TEMP_1', 'FLAG_TEMP_12', 'VAR_TEMP_0', 'VAR_TEMP_1', 'TEMP_FLAGS_END', 'TEMP_VARS_END']:
        resolve(name)
    for name in declarations:
        if name.startswith('METATILE_VermilionGym_'): resolve(name)
    for name, target in scripts['aliases'].items():
        values[name] = dict(value=resolve(target), sourceRef=src.ref('data/maps/VermilionCity_Gym/scripts.inc', name))
    return result

def declaration(src, path, symbol, pattern=None):
    pattern = pattern or r'^(?:static )?[^\n;]+\b' + re.escape(symbol) + r'\([^;\n]*\)\n\{.*?^\}'
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1: raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))

def puzzle_neighbors(source_text):
    """Decode the source switch/case table, preserving its RNG residue order."""
    rows = []
    for match in re.finditer(r'((?:    case \d+:\n)+)(.*?)        break;', source_text, re.S):
        cases = [int(v) for v in re.findall(r'case (\d+):', match[1])]
        modulus = int(re.search(r'idx = Random\(\) % (\d+);', match[2])[1])
        offsets = [int(n) * (1 if sign == '+' else -1) for sign, n in re.findall(r'gSpecialVar_0x8005 ([+-])= (\d+);', match[2])]
        if len(offsets) != modulus: raise ValueError('Puzzle RNG cases differ')
        rows.extend(dict(firstSwitchId=first, secondDrawModulus=modulus, secondSwitchIds=[first + off for off in offsets]) for first in cases)
    if [r['firstSwitchId'] for r in rows] != list(range(1, 16)): raise ValueError('Puzzle source cases incomplete')
    for row in rows:
        first = row['firstSwitchId']
        for second in row['secondSwitchIds']:
            if not 1 <= second <= 15 or abs((first-1)//5-(second-1)//5) + abs((first-1)%5-(second-1)%5) != 1:
                raise ValueError('Puzzle neighbor is not orthogonally adjacent')
    return rows

def dependency_inventory(src, worlds, scripts, constants, connections, warps):
    by_label = {b['label']: b for b in scripts['blocks']}
    def block(label):
        b = by_label[label]
        return dict(label=label, sourceRef=b['sourceRef'], sourceText='\n'.join(row['text'] for row in b['lines']))
    def verify(label, *tokens):
        b = block(label)
        if any(token not in b['sourceText'] for token in tokens): raise ValueError('Source script contract differs: ' + label)
        return b
    city = src.json('data/maps/VermilionCity/map.json')
    trees = [(i, e) for i, e in enumerate(city['object_events'], 1) if e['graphics_id'] == 'OBJ_EVENT_GFX_CUT_TREE']
    if len(trees) != 1 or (trees[0][0], trees[0][1]['x'], trees[0][1]['y'], trees[0][1]['flag']) != (7, 19, 24, 'FLAG_TEMP_12'):
        raise ValueError('External Cut-tree source differs')
    special = declaration(src, 'src/field_specials.c', 'SetVermilionTrashCans')
    if 'u16 idx = (Random() % 15) + 1;' not in special['sourceText']: raise ValueError('First-switch RNG differs')
    neighbors = puzzle_neighbors(special['sourceText'])
    puzzle = dict(special=special, random=declaration(src, 'src/random.c', 'Random'),
        rngMacro=declaration(src, 'include/random.h', 'ISO_RANDOMIZE1', r'^#define RAND_MULT[^\n]+\n#define ISO_RANDOMIZE1[^\n]+'),
        rngDrawCount=2, firstDrawModulus=15, firstDrawAdd=1, neighbors=neighbors,
        firstSwitchVariable='VAR_TEMP_0', secondSwitchVariable='VAR_TEMP_1',
        firstFoundFlag='FLAG_TEMP_1', completedFlag='FLAG_FOUND_BOTH_VERMILION_GYM_SWITCHES',
        firstFoundResetsOnMapTransition=True, completedPersistsOnMapTransition=True,
        init=verify('VermilionCity_Gym_EventScript_InitTrashCans', 'goto_if_set FLAG_FOUND_BOTH_VERMILION_GYM_SWITCHES', 'special SetVermilionTrashCans'),
        first=verify('VermilionCity_Gym_EventScript_FoundSwitchOne', 'setflag FOUND_FIRST_SWITCH', 'playse SE_PIN'),
        reset=verify('VermilionCity_Gym_EventScript_TrySwitchTwo', 'clearflag FOUND_FIRST_SWITCH', 'special SetVermilionTrashCans'),
        complete=verify('VermilionCity_Gym_EventScript_FoundSwitchTwo', 'setflag FLAG_FOUND_BOTH_VERMILION_GYM_SWITCHES', 'playse SE_UNLOCK'),
        cans=[dict(id=int(e['script'].removeprefix('VermilionCity_Gym_EventScript_TrashCan')), declaration=e, sourceRef=worlds[0]['sourceRef'])
              for e in worlds[0]['events']['bg_events'] if 'EventScript_TrashCan' in e['script']])
    cut = dict(map='MAP_VERMILION_CITY', localId=trees[0][0], declaration=trees[0][1], sourceRef=src.ref('data/maps/VermilionCity/map.json', 'object_event_7'),
        requiredBadge='FLAG_BADGE02_GET', requiredMove='MOVE_CUT', removalFlag='FLAG_TEMP_12', requiresHmInBag=False,
        skipsEggs=True, requiresPositiveHp=False, requiresPositivePp=False, consumesPp=False, firstEligiblePartySlot=True,
        treeReturnsOnMapTransition=True, fieldMoveCounter='GAME_STAT_USED_CUT', sound='SE_M_CUT',
        interaction=verify('EventScript_CutTree', 'goto_if_unset FLAG_BADGE02_GET', 'checkpartymove MOVE_CUT', 'MSGBOX_YESNO'),
        partyMenuScript=block('EventScript_FldEffCut'), removal=block('EventScript_CutTreeDown'), movement=block('Movement_CutTreeDown'),
        functions=[declaration(src, p, s) for p, s in [
            ('src/scrcmd.c','ScrCmd_checkpartymove'), ('src/fldeff_cut.c','SetUpFieldMove_Cut'),
            ('src/fldeff_cut.c','FieldCallback_CutTree'), ('src/fldeff_cut.c','FldEff_UseCutOnTree'),
            ('src/fldeff_cut.c','FieldMoveCallback_CutTree'), ('src/fldeff_rocksmash.c','CheckObjectGraphicsInFrontOfPlayer'),
            ('src/scrcmd.c','ScrCmd_removeobject'), ('src/event_object_movement.c','RemoveObjectEventByLocalIdAndMap'),
            ('src/event_object_movement.c','MovementAction_CutTree_Step0'), ('src/event_object_movement.c','MovementAction_CutTree_Step1'),
            ('src/event_object_movement.c','MovementAction_CutTree_Step2'), ('src/event_data.c','ClearTempFieldEventData'),
            ('src/overworld.c','LoadMapFromCameraTransition'), ('src/overworld.c','LoadMapFromWarp'),
            ('src/party_menu.c','CursorCB_FieldMove'), ('src/party_menu.c','MonKnowsMove')]])
    surge = dict(trainer='TRAINER_LEADER_LT_SURGE', badgeFlag='FLAG_BADGE03_GET', defeatedFlag='FLAG_DEFEATED_LT_SURGE',
        rewardItem='ITEM_TM34', rewardQuantity=1, rewardFlag='FLAG_GOT_TM34_FROM_SURGE', rewardRetriesOnFullBag=True,
        entry=block('VermilionCity_Gym_EventScript_LtSurge'), victory=block('VermilionCity_Gym_EventScript_DefeatedLtSurge'),
        reward=block('VermilionCity_Gym_EventScript_GiveTM34'), fullBag=block('VermilionCity_Gym_EventScript_NoRoomForTM34'),
        setGymTrainers=block('EventScript_SetVermilionGymTrainers'), aide=block('VermilionCity_Gym_EventScript_ShowOaksAide'))
    symbols = sorted(set(re.findall(r'\bTRAINER_\w+', src.text('data/maps/VermilionCity_Gym/scripts.inc'))))
    trainers = [dict(symbol=symbol, sourceTrainerId=constants['values'][symbol]['value'],
        declaration=declaration(src, 'src/data/trainers.h', symbol, r'^    \[' + symbol + r'\] = \{\n.*?^    \},')) for symbol in symbols]
    if len(trainers) != 4 or len(puzzle['cans']) != 15: raise ValueError('Gym actor/puzzle scope differs')
    return dict(schemaVersion=1,   puzzle=puzzle, cut=cut, surge=surge, trainers=trainers,
        externalWarps=warps, externalConnections=connections, fieldMetatileCommand=declaration(src, 'src/scrcmd.c', 'ScrCmd_setmetatile'),
        fieldMetatileSetter=declaration(src, 'src/fieldmap.c', 'MapGridSetMetatileIdAt'),
        fieldMetatileMasks=declaration(src, 'include/global.fieldmap.h', 'MAPGRID_MASKS', r'^#define MAPGRID_METATILE_ID_MASK[^\n]+\n#define MAPGRID_COLLISION_MASK[^\n]+\n#define MAPGRID_ELEVATION_MASK[^\n]+'),
        sourceAudio=['MUS_GYM', 'SE_PIN', 'SE_UNLOCK', 'SE_M_CUT'],
        unsupported=['No gameplay handlers registered by this content loader.', 'Cut grass, Dotted Hole, other Cut trees and later field moves are not admitted by this profile.'])

def beam_metatiles(src, scripts, constants, output):
    """Render all source beam states using original indexed tiles and palettes."""
    bases = ['data/tilesets/primary/building', 'data/tilesets/secondary/vermilion_gym']
    attrs = [src.units(b + '/metatile_attributes.bin', 'I') for b in bases]
    tiles = [src.units(b + '/metatiles.bin', 'H') for b in bases]
    images = [Image.open(io.BytesIO(src.data[b + '/tiles.png'])) for b in bases]
    palettes = []
    for i in range(13):
        lines = src.text(bases[0 if i < 7 else 1] + f'/palettes/{i:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19: raise ValueError('Invalid beam palette')
        palettes.append([tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in row.split()) for row in lines[3:]])
    palettes[0][0] = (0, 0, 0)
    def tile(entry):
        tid, pal = entry & 1023, entry >> 12
        index, local = (0, tid) if tid < 640 else (1, tid - 640)
        img = images[index]
        if img.mode != 'P' or pal >= 13 or local * 64 >= img.width * img.height: raise ValueError('Invalid beam tile')
        pixels = []
        for y in range(8):
            for x in range(8):
                ix, iy = (7-x if entry & 0x400 else x), (7-y if entry & 0x800 else y)
                color = img.getpixel(((local % (img.width//8))*8+ix, (local//(img.width//8))*8+iy))
                pixels.append((*palettes[pal][color], 0 if color == 0 else 255))
        result = Image.new('RGBA', (8,8)); result.putdata(pixels); return result
    rows = {b['label']: b for b in scripts['blocks']}
    blocks = src.units('data/layouts/VermilionCity_Gym/map.bin', 'H')
    definitions, states = {}, []
    output.mkdir()
    for state, suffix in [('on','SetBeamsOn'), ('half','SetOneBeamOff'), ('off','SetBeamsOff')]:
        label = 'VermilionCity_Gym_EventScript_' + suffix
        patches = []
        for row in rows[label]['lines']:
            match = re.fullmatch(r'\s*setmetatile (\d+), (\d+), (\w+), ([01])', row['text'])
            if not match: continue
            x,y,symbol,impassable = int(match[1]),int(match[2]),match[3],bool(int(match[4]))
            mid = constants['values'][symbol]['value']; local = mid - 640
            if not 0 <= local < len(attrs[1]): raise ValueError('Beam metatile range differs')
            attr, entries = attrs[1][local], tiles[1][local*8:local*8+8]
            layer_type = (attr >> 29) & 3
            groups = {0:[[0x3014]*4,entries[:4],entries[4:]],1:[entries[:4],entries[4:],[0]*4],2:[entries[:4],[0]*4,entries[4:]]}.get(layer_type)
            if groups is None: raise ValueError('Reserved beam layer type')
            if mid not in definitions:
                textures = {}
                for layer, group in zip(('bottom','middle','top'), groups):
                    img = Image.new('RGBA',(16,16))
                    for k, entry in enumerate(group): img.paste(tile(entry), ((k%2)*8,(k//2)*8))
                    filename = f'{mid:03x}-{layer}.png'; img.save(output/filename,optimize=False,compress_level=9)
                    textures[layer+'TexturePath'] = 'beam-metatiles/' + filename
                definitions[mid] = dict(rawMetatileId=mid,symbol=symbol,rawAttributes=attr,behavior=attr&511,
                    terrain=(attr>>9)&31,encounterType=(attr>>24)&7,layerType=layer_type,tileEntries=entries,
                    sourceRef=src.ref(bases[1]+'/metatile_attributes.bin',f'metatile_{local:03x}'),**textures)
            raw = blocks[y*11+x]; collision = 3 if impassable else 0
            patches.append(dict(x=x,y=y,rawMetatileId=mid,isImpassable=impassable,collision=collision,
                sourceRawBlock=raw,sourceElevation=raw>>12,resultingRawBlock=(raw&0xf000)|mid|(collision<<10)))
        if len(patches) != 10: raise ValueError('Beam state must contain all ten source patches')
        states.append(dict(state=state,label=label,sourceRef=rows[label]['sourceRef'],patches=patches))
    return dict(schemaVersion=1,mapId=MAPS[0][1],preserveSourceElevation=True,
        definitions=[definitions[k] for k in sorted(definitions)],states=states)


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()): raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.vermilion-gym-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        tree = [e for e in src.json('data/maps/VermilionCity/map.json')['object_events'] if e['graphics_id']=='OBJ_EVENT_GFX_CUT_TREE']
        npcs=npc_inventory(src,worlds+[dict(events=dict(object_events=tree))],stage/'npc')
        dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps)
        beams=beam_metatiles(src,scripts,constants,stage/'beam-metatiles')
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'vermilion-gym-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies,'beam-metatiles.json':beams}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            beamMetatileCount=len(beams['definitions']),outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')): require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir(): dest.mkdir(parents=True,exist_ok=True)
            else: dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/vermilion-gym')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize: materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)


