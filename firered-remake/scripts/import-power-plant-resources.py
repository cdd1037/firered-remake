#!/usr/bin/env python3
"""Import the original Power Plant and authenticate its retained Route10 boundary.

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
PACKAGE = 'power-plant-resources'
RESOURCE_ID = 'firered-power-plant-resources-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['PowerPlant']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_PowerPlant': 'secondary/power_plant',
            'gTileset_General': 'primary/general', 'gTileset_LavenderTown': 'secondary/lavender_town'}
ROUTE_PACKAGE = 'content/generated/rocket-hideout-runtime-maps'
ROUTE_PATH = 'rocktunnel-region/route-10/runtime-map.json'


def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
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
    roots.update(e['script'] for w in worlds for e in w['events']['object_events'])
    # The source engine selects hidden items directly; event macros call these
    # standard scripts indirectly through gStdScripts rather than by label.
    implicit = ['EventScript_HiddenItemScript', 'Std_FindItem', 'Std_MsgboxNPC',
                'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo', 'Std_PutItemAway']
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


def retained_route(src, reader, current):
    manifest = (current / ROUTE_PACKAGE / 'manifest.json').read_bytes()
    entry = next(e for e in json.loads(manifest)['maps'] if e['stableId'] == 'map.route-10')
    raw = (current / 'content/generated' / ROUTE_PATH).read_bytes()
    need(entry['path'] == ROUTE_PATH, 'Retained Route10 differs')
    world = reader.decode(src, 'Route10')
    compact_check(json.loads(raw), world, src)
    return dict(map='map.route-10', operation='reuse-existing-source-map', geometryChanges=[], eventChanges=[],
                currentPackage='content/generated', currentResource=dict(path=entry['path']),
                entrances=[dict(index=i, event=e) for i,e in enumerate(world['events']['warp_events']) if e['dest_map'] == 'MAP_POWER_PLANT'],
                requiredIntegration='Register map.power-plant; retain Route10 geometry, events and original warp indices.'), world


def route_witness(src, worlds):
    """Conservative source BFS; all objects remain, no encounters are suppressed."""
    byname = {w['sourceName']: w for w in worlds}
    symbols = {w['events']['id']: w['sourceName'] for w in worlds}
    def cell(name, x, y):
        w = byname[name]
        return w['cells'][y*w['width']+x] if 0 <= x < w['width'] and 0 <= y < w['height'] else None
    def search(label, start, goal):
        seen, queue, finish = {start: None}, collections.deque([start]), None
        while queue:
            state = queue.popleft(); name,x,y,elevation,surfing = state
            if goal(state): finish = state; break
            for dx,dy,direction in [(0,-1,'north'),(0,1,'south'),(-1,0,'west'),(1,0,'east')]:
                world=byname[name]
                # Source TryArrowWarp examines the player's current arrow cell.
                # Entering that cell is one ordinary step; pressing outward is
                # a separate transition, even when the outward cell is blocked.
                arrow={0x62:'east',0x63:'west',0x65:'south'}.get(cell(name,x,y)['behaviorId'])
                if direction == arrow:
                    warp=next(e for e in world['events']['warp_events'] if e['x']==x and e['y']==y)
                    next_name=symbols[warp['dest_map']]
                    end=byname[next_name]['events']['warp_events'][int(warp['dest_warp_id'])]
                    nxt=(next_name,end['x'],end['y'],cell(next_name,end['x'],end['y'])['elevation'],False)
                    if nxt not in seen:seen[nxt]=(state,direction,'warp');queue.append(nxt)
                    continue
                nx,ny = x+dx,y+dy; target = cell(name,nx,ny)
                if target is None or target['collision'] != 0: continue
                if any(o['x'] == nx and o['y'] == ny and o['elevation'] in (0,target['elevation']) for o in world['events']['object_events']): continue
                behavior,ne,ns = target['behaviorId'],target['elevation'],surfing
                if behavior not in (0x00,0x02,0x08,0x15,0x60,0x62,0x63,0x65): continue
                action = 'walk'
                if behavior == 0x15:
                    if ne != 1 or ((target['rawAttributes'] >> 9) & 31) != 2 or (not surfing and elevation != 3): continue
                    ns=True; action='surf-step' if surfing else 'start-surf'
                elif surfing:
                    if ne != 3: continue
                    ns=False; action='stop-surf'
                elif elevation and ne not in (0,15,elevation): continue
                if ne == 15: ne=elevation
                next_name=name
                warp=next((e for e in world['events']['warp_events'] if e['x']==nx and e['y']==ny),None)
                if warp and behavior == 0x60:
                    if warp['dest_map'] not in symbols: continue
                    if direction != 'north': continue
                    next_name=symbols[warp['dest_map']]
                    end=byname[next_name]['events']['warp_events'][int(warp['dest_warp_id'])]
                    nx,ny=end['x'],end['y']; ne=cell(next_name,nx,ny)['elevation'];ns=False;action='warp'
                nxt=(next_name,nx,ny,ne,ns)
                if nxt not in seen: seen[nxt]=(state,direction,action);queue.append(nxt)
        need(finish is not None,'No conservative source route: '+label)
        rows=[];cursor=finish
        while seen[cursor] is not None:
            previous,direction,action=seen[cursor]
            rows.append(dict(direction=direction,action=action,map=cursor[0],x=cursor[1],y=cursor[2],elevation=cursor[3],surfing=cursor[4]));cursor=previous
        rows.reverse()
        return dict(label=label,start=dict(map=start[0],x=start[1],y=start[2],elevation=start[3],surfing=start[4]),
                    finish=dict(map=finish[0],x=finish[1],y=finish[2],elevation=finish[3],surfing=finish[4]),stepCount=len(rows),
                    surfStarts=sum(r['action']=='start-surf' for r in rows),mapTransitions=[r for r in rows if r['action']=='warp'],steps=rows)
    routes=[search('Route10 connection to Zapdos',('Route10',0,8,3,False),lambda s:s[:3]==('PowerPlant',5,12)),
            search('Zapdos to west exit ledge',('PowerPlant',5,12,3,False),lambda s:s[:3]==('Route10',1,37)),
            search('West ledge re-entry to main entrance shore',('Route10',1,37,3,False),lambda s:s[:3]==('Route10',7,41))]
    return dict(schemaVersion=1,sourceOnly=True,runtimeValidated=False,
                preconditions=['Route10 north is reachable from its existing Route9 connection.','A legal Surf-capable party and Soul Badge are available.'],
                storyGate='No story flag blocks the PowerPlant entry scripts.',
                policy='Every source object remains blocking. No collision patches, pickups, ledge shortcuts or encounter suppression.',routes=routes,
                ruleSources=[src.ref('src/event_object_movement.c','IsElevationMismatchAt'),src.ref('src/field_player_avatar.c','CanStopSurfing'),
                             src.ref('src/field_control_avatar.c','TryArrowWarp'),src.ref('src/metatile_behavior.c','MetatileBehavior_IsSurfable')])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root',type=Path,required=True)
    parser.add_argument('--dependency-root',type=Path,default=ROOT)
    parser.add_argument('--current-project-root',type=Path)
    parser.add_argument('--project-root',type=Path,default=ROOT)
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args();target,dep,ref=args.project_root.absolute(),args.dependency_root.absolute(),args.reference_root.absolute()
    current=(args.current_project_root or dep).absolute()
    for p in (target,dep,ref,current):safe(p)
    # Validate the lexical route first, then normalize '..' without following
    # symlinks. Otherwise a legitimate sibling reference appears nested.
    target,dep,ref,current=(Path(os.path.abspath(p)) for p in (target,dep,ref,current))
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap')
    reader=module(dep/'scripts/import-cinnabar-source-preparation.py');reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,RESOURCE_ID
    src=reader.Source(ref);world=reader.decode(src,'PowerPlant');worlds=[world]
    retained,route=retained_route(src,reader,current)
    scripts=script_inventory(src,reader,worlds);constants=constants_inventory(src,scripts,worlds)
    witness=route_witness(src,worlds+[route])
    for leaf in ['scripts.inc','text.inc']:
        path='data/maps/PowerPlant/'+leaf
        if path in src.tree:src.read(path,'live-map-script')
    links=reader.topology(src,worlds)
    for entrance in retained['entrances']:
        e=entrance['event'];i=entrance['index'];j=int(e['dest_warp_id']);end=world['events']['warp_events'][j]
        links['warps'].append(dict(sourceMap='MAP_ROUTE10',sourceIndex=i,source=e,destinationMap='MAP_POWER_PLANT',destinationIndex=j,destination=end,dynamic=False,reciprocalIndex=int(end['dest_warp_id'])==i))
    need(len(links['warps'])==7 and sum(w['reciprocalIndex'] for w in links['warps'])==4,'Boundary topology differs')
    bysymbol={w['events']['id']:w for w in worlds+[route]}
    for edge in links['warps']:
        for key,point in [('sourceMap','source'),('destinationMap','destination')]:
            w=bysymbol[edge[key]];e=edge[point];need(0<=e['x']<w['width'] and 0<=e['y']<w['height'],'Warp out of bounds')
    bylabel={b['label']:b for b in scripts['blocks']};pickups=[];statics=[];objects=[]
    for i,e in enumerate(world['events']['object_events'],1):
        b=bylabel[e['script']];row=dict(map='MAP_POWER_PLANT',localId=i,event=e,sourceRef=b['sourceRef']);objects.append(row)
        text='\n'.join(r['text'] for r in b['lines'])
        item=re.search(r'\bfinditem (ITEM_\w+)(?:, (\d+))?',text)
        mon=re.search(r'\bsetwildbattle (SPECIES_\w+), (\d+)',text)
        if item:pickups.append(dict(**row,item=item[1],quantity=int(item[2] or 1)))
        elif mon:statics.append(dict(**row,species=mon[1],level=int(mon[2]),fakeItemBall=e['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL'))
        else:raise ValueError('Unknown Power Plant object')
    hidden=[dict(map='MAP_POWER_PLANT',event=e) for e in world['events']['bg_events'] if e['type']=='hidden_item']
    need((len(objects),len(pickups),len(statics),len(hidden))==(8,5,3,2),'Original event cohort differs')
    symbols={r['item'] for r in pickups}|{r['event']['item'] for r in hidden}
    items=[i for i in src.json('src/data/items.json')['items'] if i['itemId'] in symbols]
    need({i['itemId'] for i in items}==symbols,'Item declaration missing')
    ep='src/data/wild_encounters.json';groups=[]
    for group in src.json(ep)['wild_encounter_groups']:
        chosen=[e for e in group['encounters'] if e['map']=='MAP_POWER_PLANT' and e['base_label'].endswith('_FireRed')]
        if chosen:groups.append(dict(**{k:v for k,v in group.items() if k!='encounters'},encounters=chosen))
    need(len(groups)==1 and len(groups[0]['encounters'])==1,'FireRed encounter cohort differs')
    contracts=dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,runtimeIntegrated=False,maps=MAPS,
        retainedRoute=retained,mapTopology=links,objects=objects,itemPickups=pickups,hiddenItems=hidden,staticEncounters=statics,
        mapHeaders=[dict(map='MAP_POWER_PLANT',**{k:world['events'][k] for k in ['allow_cycling','allow_escaping','allow_running','requires_flash']})],
        entryConditions=dict(storyFlags=[],fieldMove='Surf',badge='Soul Badge',sourceRef=src.ref('data/maps/PowerPlant/scripts.inc','PowerPlant_OnTransition')),
        escapeRopeAnchors=[dict(map='MAP_ROUTE10',x=7,y=41,entryWarpIndex=2),dict(map='MAP_ROUTE10',x=2,y=38,entryWarpIndex=4)],
        escapeRopeSource=src.ref('src/overworld.c','UpdateEscapeWarp'),
        inactiveSourceWarps=[dict(index=i,event=world['events']['warp_events'][i],cell=world['cells'][world['events']['warp_events'][i]['y']*world['width']+world['events']['warp_events'][i]['x']]) for i in [0,2,4]],
        limitations=['Resource/source contract only; runtime command and battle admission belongs to Application.','Eight total objects comprise five pickups, two fake balls and Zapdos.','Original three blocked warp records remain unchanged.'])
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='power-plant-',dir=target) as temp:
        stage=Path(temp);relative='power-plant'
        shared.import_map(src,'PowerPlant','map.power-plant',stage/relative)
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        p=stage/relative/'runtime-map.json';rich=p.read_bytes();compact=converter.normalize_runtime(json.loads(rich));compact_check(compact,world,src)
        data=converter.encode(compact);(stage/relative/'runtime-map.json').write_bytes(data)
        entries=[dict(sourceName='PowerPlant',stableId='map.power-plant',directory=relative,sourceMapSymbol='MAP_POWER_PLANT',path=relative+'/runtime-map.json')]
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lock_bytes=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,
                                  maps=entries,inputFiles=lock['files'],tilesets=TILESETS,mapSymbols={s:w['stableId'] for s,w in bysymbol.items()}),
              'scripts.json':scripts,'contracts.json':contracts,'npc-source.json':npcs,'reachability.json':witness,
              'items.json':dict(schemaVersion=1,declarations=items),'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS),
              'encounters.json':dict(schemaVersion=1,sourceRef=src.ref(ep,'gWildMonHeaders'),groups=groups,runtimeIntegrated=False)}, check=args.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=RESOURCE_ID,resourceId=RESOURCE_ID,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=1,cellCount=1960,borderCellCount=4,directedWarpCount=7,objectCount=8,pickupCount=5,hiddenItemCount=2,
            fakeItemBallCount=2,staticEncounterCount=3,npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        writes=[(target / 'scripts/power-plant-resources.contract.json', canonical(contracts))]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,data) for p,data in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if args.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(
            mapCount=1,cellRoundtripCount=1960,borderCellRoundtripCount=4,unchangedRoute10Cells=1920,inputCount=len(src.entries),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),check=args.check),indent=2))


if __name__=='__main__':main()
