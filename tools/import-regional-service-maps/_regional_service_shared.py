#!/usr/bin/env python3
"""Authenticated Route22, League gate, and Route23 source increment; no gameplay or audio activation."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parents[2] / 'scripts/lib')
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from map_scripts import parse_scripts, script_closure
from map_source import render_map, connections_and_warps, object_pairs, json_bytes, require_safe_target
from map_runtime import movement
from overworld_sprites import graphics_records
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parents[2] / 'scripts/lib')
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
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
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-route22-region-import-v1'
MAPS = [('Route22', 'map.route-22', 'route-22'), ('Route22_NorthEntrance', 'map.route-22-north-entrance', 'route-22-north-entrance'), ('Route23', 'map.route-23', 'route-23')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_ViridianCity': 'secondary/viridian_city', 'gTileset_IndigoPlateau': 'secondary/indigo_plateau', 'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':')) + '\n'


def read_lock(path=None):
    target = path or ROOT / 'scripts/recipes/maps/route22-region.lock.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or
            lock.get('repository') != 'pret/pokefirered' or lock.get('commit') != COMMIT or
            lock.get('profile') != PROFILE or
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
        if (not entry['roles']):
            raise ValueError('Malformed source entry')
    return lock


class Source:
    def __init__(self, raw, lock_path=None):
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.data = {}
        for path, entry in self.entries.items():
            require_safe_target(raw, raw / path)
            data = (raw / path).read_bytes()
            self.data[path] = data

    def text(self, path):
        return self.data[path].decode('utf-8')

    def json(self, path):
        return json_bytes(self.data[path])

    def units(self, path, code):
        data = self.data[path]
        if len(data) % struct.calcsize(code):
            raise ValueError('Misaligned source binary: ' + path)
        return list(struct.unpack('<' + str(len(data) // struct.calcsize(code)) + code, data))

    def ref(self, path, symbol, line=None):
        result = dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path,
                      symbol=symbol)
        if line is not None:
            result['line'] = line
        return result


def unloaded_school_tile(name, tile_id, palette, emitted):
    # ResetScreenForMapLoad clears VRAM; the source RoofRoom references two
    # unloaded School slots behind the bookshelf. Keep this source contract
    # explicit, rather than allowing arbitrary out-of-bounds graphics.
    if name == 'CeladonCity_Condominiums_RoofRoom' and tile_id == 0x2c0 and palette == 7 and emitted == 50:
        return Image.new('RGBA', (8, 8))
    return None


def import_map(src, name, stable_id, out):
    world = render_map(src, name, stable_id, out, tilesets=TILESETS, unloaded_tile=unloaded_school_tile)
    world.update(profile=PROFILE, runtimeIntegrated=False)
    return world


def script_inventory(src, worlds):
    parsed = parse_scripts(src)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    required = {'Route22_EventScript_EarlyRival', 'Route23_OnTransition', 'Route22_NorthEntrance_EventScript_BoulderBadgeGuardTrigger'}
    if not required <= roots: raise ValueError('Critical Route22 script entries missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    return script_closure(parsed, roots, runtime_status="unsupportedInRoute22Region")


def npc_inventory(src, worlds, out, *, render=False):
    root = 'src/data/object_events/'
    graphics = {e['graphics_id'] for w in worlds for e in w['events']['object_events']}
    graphics.add('OBJ_EVENT_GFX_RED_NORMAL')
    records = graphics_records(src, graphics, out, render=render)
    return dict(schemaVersion=1, graphics=records, dynamicGraphics=[])


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
    wanted = {'AI_SCRIPT_CHECK_BAD_MOVE', 'AI_SCRIPT_TRY_TO_FAINT', 'AI_SCRIPT_CHECK_VIABILITY'}
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


def materialize(raw, reference_repo):
    """Copy the declared paths from the selected source commit."""
    raw, reference = pathlib.Path(raw), pathlib.Path(reference_repo)
    require_safe_target(raw, raw)
    if raw.resolve().is_relative_to(reference.resolve()) or reference.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Reference and destination must be disjoint')
    lock = read_lock()
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull,
               GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    def git(*args):
        return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks', '-C', str(reference), *args], env=env)
    verified = {}
    for entry in lock['files']:
        path = entry['path']; require_safe_target(raw, raw / path)
        data = git('show', COMMIT + ':' + path); verified[path] = data
    for path, data in verified.items():
        target = raw / path; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)


def first_badge_boundary(worlds):
    """Recompute the exact source collision/guard/trigger stop set, independently of the planning manifest."""
    w = next(w for w in worlds if w['stableId'] == 'map.route-23')
    cells = {(c['x'], c['y']): c for c in w['cells']}
    events = w['events']
    triggers = {(e['x'], e['y']) for e in events['coord_events'] if e['var_value'] == '1'}
    objects = {(e['x'], e['y']) for e in events['object_events']}
    entry = (events['warp_events'][2]['x'], events['warp_events'][2]['y'])
    visited, attempted, queue = {entry}, set(), collections.deque([entry])
    while queue:
        x, y = queue.popleft()
        for p in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            c = cells.get(p)
            if c is None or c['collision'] or p in objects: continue
            if c['elevation'] != 3 or c['behaviorName'] not in ('MB_NORMAL','MB_SOUTH_ARROW_WARP'):
                raise ValueError('South corridor needs another movement rule')
            if p in triggers: attempted.add(p)
            elif p not in visited: visited.add(p); queue.append(p)
    if len(visited) != 51 or attempted != {(13,149),(14,149)} or any(cells[p]['encounterType'] for p in visited | attempted):
        raise ValueError('First-badge source boundary changed')
    return dict(stableReachableCellCount=len(visited), entry=list(entry),
                stableRows=[dict(y=y, x=sorted(x for x,row in visited if row == y)) for y in sorted({y for x,y in visited})],
                attemptedCascadeTriggerCells=sorted(map(list,attempted)), allCascadeTriggerCells=sorted(map(list,triggers)),
                blockingNpc=[15,149], reachableEncounterCells=0, requiredScene=1,
                requiredMissingBadge='FLAG_BADGE02_GET', runtimeVerified=False)


def dependency_inventory(src, worlds, scripts):
    # The reviewed audit is a bounded declarative source extractor. Its immutable output is
    # authenticated here, and every reused field points back to retained pinned raw sources.
    data = (ROOT / 'tests/Game.Route22Planning.Tests/manifest.json').read_bytes()
    plan = json_bytes(data)
    for w,p in zip(worlds, plan['maps']):
        if w['events'] != p['events'] or w['layout'] != p['layout']:
            raise ValueError('Map differs from reviewed first-badge boundary')
    boundary = first_badge_boundary(worlds)
    for k in ('stableReachableCellCount','stableRows','entry','attemptedCascadeTriggerCells','allCascadeTriggerCells'):
        if boundary[k] != plan['route23FirstBadgeBoundary'][k]: raise ValueError('First-badge proof differs')
    route22, route23 = worlds[0], worlds[2]
    if any(c['collision'] == 0 for c in route22['cells'] if c['y'] == 0 and c['x'] < 24) or any(c['collision'] == 0 for c in route23['cells'] if c['y'] == 159):
        raise ValueError('Direct north connection bypass opened')
    trigger_status = []
    for w in worlds:
        for i,e in enumerate(w['events']['coord_events']):
            candidate = (w['stableId'] == 'map.route-22' and e['var_value'] == '1') or (w['stableId'] == 'map.route-22-north-entrance' and e['var_value'] == '0') or (w['stableId'] == 'map.route-23' and e['var_value'] == '1')
            trigger_status.append(dict(map=w['events']['id'], sourceCoordEventIndex=i, declaration=e,
                                       scope='firstBadgeCandidate' if candidate else 'deferredLaterStory', enabled=False))
    return dict(schemaVersion=1, profile=PROFILE, runtimeIntegrated=False, completeGame=False,
                verificationKind='authenticated-content-and-static-boundary',
                earlyRivalTrainers=plan['earlyRivalTrainers'], encounters=plan['encounters'],
                requiredBadgeStrings=plan['requiredBadgeStrings'], route22SceneWrites=plan['route22SceneWrites'],
                firstBadgeBoundary=boundary, northConnectionBoundary=plan['route22Route23ConnectionBoundary'],
                coordinateEvents=trigger_status,
                audio=[dict(symbol=symbol, sourceRef=src.ref(path,symbol), status='missingPlayableAudio', converted=False)
                       for symbol,path in [('MUS_ROUTE3','sound/songs/midi/mus_route3.mid'),('MUS_VICTORY_ROAD','sound/songs/midi/mus_victory_road.mid')]],
                deferred=['Route22 scene 3/4 and late rival parties','Route23 beyond the Cascade checkpoint',
                          'Route23 high-level land encounters and hidden items','Surf and all fishing encounters',
                          'Victory Road and Indigo Plateau access','Species/moves/AI and battle integration','Audio synthesis and real listening'],
                unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']], specials=[x['symbol'] for x in scripts['specials']],
                                 runtimeIntegration=False),
                limitations=['Full source declarations are preserved, including late-game scripts; none are registered as executable by this increment.',
                             'The 51-cell boundary is a static source proof, not application or GUI acceptance.',
                             'Raw trainer tables retain their complete original file; only the three early-rival parties are exposed as metadata.',
                             'NPC and map frame-zero renders do not certify original animation timing.'])


def import_catalog(raw, out):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    require_safe_target(out, out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw); out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.route22-region-stage-', dir=out.parent) as temp:
        stage = pathlib.Path(temp)
        worlds = [import_map(src, name, stable, stage / directory) for name, stable, directory in MAPS]
        connections, warps = connections_and_warps(src, worlds)
        scripts = script_inventory(src, worlds)
        constants = constants_inventory(src, worlds, scripts)
        npcs = npc_inventory(src, worlds, stage / 'npc')
        dependencies = dependency_inventory(src, worlds, scripts)
        catalog = dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, runtimeIntegrated=False,
                       maps=[dict(sourceName=name, stableId=stable, sourceMapSymbol=w['events']['id'], directory=d)
                             for (name,stable,d),w in zip(MAPS,worlds)],
                       connections=connections, warps=warps)
        for filename, document in package_documents(out.parent, constants, {'route22-catalog.json':catalog,'scripts.json':scripts,
                                   'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage / filename).write_bytes(canonical(document).encode())
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputCount=len(src.entries), mapCount=len(worlds), cellCount=sum(len(w['cells']) for w in worlds),
                        unresolvedLabelCount=len(scripts['unresolvedLabelReferences']), outputs=outputs)
        data = canonical(manifest).encode()
        (stage / 'manifest.json').write_bytes(data)
        for path in sorted(stage.rglob('*')): require_safe_target(out, out / path.relative_to(stage))
        for path in sorted(stage.rglob('*')):
            dest = out / path.relative_to(stage)
            if path.is_dir(): dest.mkdir(parents=True, exist_ok=True)
            else: dest.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(path,dest)
    print(json.dumps(dict(profile=PROFILE, inputCount=len(src.entries), outputCount=len(outputs),
                         runtimeIntegrated=False),sort_keys=True))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/route22')
    parser.add_argument('--reference-root',type=pathlib.Path,help='Pinned local reference; no network or upstream execution')
    parser.add_argument('--materialize',action='store_true',help='Copy locked Git blobs from --reference-root or .tools/reference-root')
    args=parser.parse_args()
    if args.materialize or args.reference_root:
        reference=args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip())
        materialize(args.raw,reference)
    import_catalog(args.raw,args.out)
