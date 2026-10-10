#!/usr/bin/env python3
"""Authenticated Route11, Digletts Cave and east Route2 source maps; no gameplay activation."""
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
LOCK_SHA256 = '6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304'
MANIFEST_SHA256 = '69bcb11e2647356e22c6a0e295b4a605d1468d1d81d430ad805b3359334eaded'
PROFILE = 'firered-route11-region-import-v1'
MAPS = [('Route11', 'map.route-11', 'route-11'), ('DiglettsCave_SouthEntrance', 'map.digletts-cave-south-entrance', 'digletts-cave-south-entrance'), ('DiglettsCave_B1F', 'map.digletts-cave-b1f', 'digletts-cave-b1f'), ('DiglettsCave_NorthEntrance', 'map.digletts-cave-north-entrance', 'digletts-cave-north-entrance'), ('Route2_EastBuilding', 'map.route-2-east-building', 'route-2-east-building'), ('Route2_House', 'map.route-2-house', 'route-2-house'), ('Route11_EastEntrance_1F', 'map.route-11-east-entrance-1f', 'route-11-east-entrance-1f'), ('Route11_EastEntrance_2F', 'map.route-11-east-entrance-2f', 'route-11-east-entrance-2f')]
SERVICE_MAPS = [name for name, _, _ in MAPS] + ['Route2']
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_VermilionCity': 'secondary/vermilion_city', 'gTileset_DiglettsCave': 'secondary/digletts_cave', 'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding2': 'secondary/generic_building_2'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
if __import__('hashlib').sha256((ROOT / 'scripts/import-route22-region.py').read_bytes()).hexdigest() != '9871702d1061d563568444586faf5275722aa76fc67f791551caa4eb9ea8843f':
    raise ValueError('Reviewed shared region importer differs')
SPEC = importlib.util.spec_from_file_location('_route11_region_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
digest, canonical, json_bytes = SHARED.digest, SHARED.canonical, SHARED.json_bytes
require_safe_target, verify_bytes, source_hash = SHARED.require_safe_target, SHARED.verify_bytes, SHARED.source_hash
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    # Preserve existing profiles when the caller overrides --raw.
    raw=pathlib.Path(raw)
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind=='raw' and protected.name=='route11-region':continue
            if raw.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(raw.resolve()):
                raise ValueError('Existing profile overlap')
    SHARED.materialize(raw,reference_repo)

def read_lock(path=None):
    target = path or ROOT / 'content/sources/route11-region.lock.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or
            lock.get('repository') != 'pret/pokefirered' or lock.get('commit') != COMMIT or
            lock.get('tree') != TREE or lock.get('profile') != PROFILE or
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
        if (set(entry) != {'path', 'byteLength', 'gitBlobSha1', 'sha256', 'roles'} or
                type(entry['byteLength']) is not int or entry['byteLength'] <= 0 or
                not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1']) or
                not re.fullmatch('[0-9a-f]{64}', entry['sha256']) or not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    if digest(data) != LOCK_SHA256:
        raise ValueError('Reviewed source lock hash differs')
    return lock

for key in ('ROOT', 'COMMIT', 'TREE', 'LOCK_SHA256', 'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
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
    roots.update(['EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'EventScript_CutTree', 'EventScript_FldEffCut'])
    required = {'Route2_EastBuilding_EventScript_Aide', 'Route11_EastEntrance_2F_EventScript_Aide', 'DiglettsCave_B1F_OnTransition'}
    if not required <= roots: raise ValueError('Critical Route11/Cave entry labels are missing')
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
    result = SHARED.constants_inventory(src, worlds, scripts)
    for name, target in scripts['aliases'].items():
        if target.isdecimal():
            matches = [p for p in scripts['filePreambles'] if re.search(r'\.(?:equ|set)\s+' + name + r',\s*' + target + r'\b', src.text(p))]
            if len(matches) != 1: raise ValueError('Ambiguous regional alias: ' + name)
            result['values'][name] = dict(value=int(target), sourceRef=src.ref(matches[0], name))
    return result

def declaration(src, path, symbol, pattern=None):
    pattern = pattern or r'^(?:static )?[^\n;]+\b' + re.escape(symbol) + r'\([^;\n]*\)\n\{.*?^\}'
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1: raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))

def reused_maps(src):
    directory = ROOT/'content/generated/forest-region'
    require_safe_target(directory,directory)
    manifest_path=directory/'manifest.json';require_safe_target(directory,manifest_path)
    manifest=manifest_path.read_bytes()
    if digest(manifest)!='d65059293915511e54171f0bfac9045267725c9a549741a2b258980ee60bb873':raise ValueError('Reused Forest manifest differs')
    entries={e['path']:e for e in json_bytes(manifest)['outputs']}
    outputs=[e for e in entries.values() if e['path'].startswith('route-2/')]
    for e in outputs:
        p=directory/e['path'];require_safe_target(directory,p);raw=p.read_bytes()
        if digest(raw)!=e['sha256'] or len(raw)!=e['byteLength']:raise ValueError('Reused Route2 artifact differs')
    world=json_bytes((directory/'route-2/world.json').read_bytes())
    if world['events']!=src.json('data/maps/Route2/map.json'):raise ValueError('Reused Route2 source events differ')
    return [dict(sourceName='Route2',stableId='map.route-2',sourceMapSymbol='MAP_ROUTE2',profile='firered-forest-region-import-v1',
        fullSourceHash='6233feb1f3f5ee0807bde325c687b09766b7aa6826a00bfa21ef72bf33c9b1c1',directory='forest-region/route-2',
        manifestSha256=digest(manifest),outputs=outputs)]


def dependency_inventory(src, worlds, scripts, constants, connections, warps):
    rows={b['label']:b for b in scripts['blocks']}
    def block(label,*tokens):
        b=rows[label];text='\n'.join(r['text'] for r in b['lines'])
        if any(t not in text for t in tokens):raise ValueError('Regional script contract differs: '+label)
        return dict(label=label,sourceRef=b['sourceRef'],sourceText=text)
    route2=src.json('data/maps/Route2/map.json')
    events=[w['events'] for w in worlds]+[route2]
    trainers=[];pickups=[];cut=[]
    for m in events:
        for i,e in enumerate(m['object_events'],1):
            label=e.get('script','');b=rows.get(label)
            if e['graphics_id']=='OBJ_EVENT_GFX_CUT_TREE':
                cut.append(dict(map=m['id'],localId=i,declaration=e,sourceRef=src.ref('data/maps/'+m['name']+'/map.json',m['id'])))
            if b:
                text='\n'.join(r['text'] for r in b['lines'])
                found=re.search(r'finditem (ITEM_\w+)(?:, (\d+))?',text)
                if found:pickups.append(dict(map=m['id'],localId=i,kind='visible',item=found[1],quantity=int(found[2] or '1'),declaration=e,sourceRef=b['sourceRef']))
                trainer=re.search(r'trainerbattle_single (TRAINER_\w+)',text)
                if trainer:
                    symbol=trainer[1];decl=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\]\s*=\s*\{.*?^    \},')
                    party=re.search(r'\.party = (\w+)\((\w+)\)',decl['sourceText'])
                    if party is None:raise ValueError('Trainer party format missing')
                    trainers.append(dict(map=m['id'],localId=i,trainer=symbol,trainerId=constants['values'][symbol]['value'],actor=e,script=block(label),declaration=decl,
                        party=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{.*?^\};'),partyFormat=party[1],runtimeStatus='sourceOnly'))
        for i,e in enumerate(m['bg_events']):
            if e.get('type')=='hidden_item':pickups.append(dict(map=m['id'],eventIndex=i,kind='hidden',item=e['item'],quantity=1,declaration=e,sourceRef=src.ref('data/maps/'+m['name']+'/map.json',m['id'])))
    def aide(map_name,item,flag,count,alias):
        m=src.json('data/maps/'+map_name+'/map.json');label=map_name+'_EventScript_Aide'
        i,e=next((i,e) for i,e in enumerate(m['object_events'],1) if e.get('script')==label)
        return dict(map=m['id'],localId=i,actor=e,item=item,flag=flag,requiredCaughtKanto=count,countVariable='VAR_0x8006',countMode=0,
            sourceAlias=alias,script=block(label,'setvar VAR_0x8004, 0','specialvar VAR_RESULT, GetPokedexCount','goto_if_lt VAR_0x8006, '+alias,
                                         'checkitemspace '+item,'giveitem_msg','setflag '+flag),
            countSource=declaration(src,'src/prof_pc.c','GetPokedexCount'),decline=block('Aide_EventScript_DeclineCheckMons'),
            insufficient=block('Aide_EventScript_HaventCaughtEnough'),noRoom=block('Aide_EventScript_NoRoomForItem'),
            oneTime=True,capacityBeforeGrant=True,runtimeStatus='requiresApplicationIntegration')
    aides=[aide('Route2_EastBuilding','ITEM_HM05','FLAG_GOT_HM05',10,'REQUIRED_SEEN_MONS'),
           aide('Route11_EastEntrance_2F','ITEM_ITEMFINDER','FLAG_GOT_ITEMFINDER',30,'REQUIRED_CAUGHT_MONS')]
    selected_items=sorted({p['item'] for p in pickups}|{'ITEM_HM05','ITEM_ITEMFINDER'})
    items=[dict(symbol=s,declaration=next(i for i in src.json('src/data/items.json')['items'] if i['itemId']==s),sourceRef=src.ref('src/data/items.json',s)) for s in selected_items]
    table=src.json('src/data/wild_encounters.json')
    encounter_rows=[e for g in table['wild_encounter_groups'] for e in g['encounters'] if e['map'] in ('MAP_ROUTE11','MAP_DIGLETTS_CAVE_B1F') and e['base_label'].endswith('_FireRed')]
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,trainers=trainers,pickups=pickups,items=items,aides=aides,
        cutTrees=cut,cutScript=block('EventScript_CutTree'),reusedRoute2Events=route2,
        encounters=dict(sourceRef=src.ref('src/data/wild_encounters.json','gWildMonHeaders'),gameVersion='FIRERED',fields=table['wild_encounter_groups'][0]['fields'],tables=encounter_rows),
        caveVisit=block('DiglettsCave_B1F_OnTransition','setworldmapflag FLAG_WORLD_MAP_DIGLETTS_CAVE_B1F'),
        trades=[dict(map=map_name,script=block(label),runtimeStatus='unsupportedPendingTradeIntegration') for map_name,label in [('MAP_ROUTE2_HOUSE','Route2_House_EventScript_Reyley'),('MAP_ROUTE11_EAST_ENTRANCE_2F','Route11_EastEntrance_2F_EventScript_Turner')]],
        tradeSource=declaration(src,'src/data/ingame_trades.h','sInGameTrades',r'^static const struct InGameTrade sInGameTrades\[\] =\s*\{.*?^\};'),
        externalConnections=[c for c in connections if c['runtimeStatus']=='externalBoundary'],externalWarps=[w for w in warps if w['runtimeStatus']=='externalBoundary'],
        blockedMaps=['MAP_ROUTE12'],sourceAudio=sorted({m['music'] for m in events}),
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False,
                         notes=['Source closure includes trainer rematches and in-game trades; importing their labels does not enable them.',
                                'HM05 gift and teaching are distinct from Flash field presentation. Itemfinder acquisition is distinct from item use.',
                                'Route11 east connection to Route12 remains an explicit unavailable boundary; all original coordinates are retained.']))


def compact_derivatives(stage,src):
    converter_path=ROOT/'scripts/export-runtime-maps.py'
    if digest(converter_path.read_bytes())!='9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8':raise ValueError('Reviewed compact converter differs')
    spec=importlib.util.spec_from_file_location('_route11_compact_converter',converter_path)
    converter=importlib.util.module_from_spec(spec);spec.loader.exec_module(converter)
    for _,_,directory in MAPS:
        raw=(stage/directory/'world.json').read_bytes()
        (stage/directory/'runtime-map.json').write_bytes(converter.encode(converter.convert(raw,digest(raw),source_hash(src.lock))))


def import_catalog(raw,out,lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if protected.name=='route11-region':continue
            if any(q.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(q.resolve()) for q in [raw,out]):raise ValueError('Existing profile overlap')
    src=Source(raw,lock_path);reused=reused_maps(src);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.route11-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        overlap=dict(events=src.json('data/maps/Route2/map.json'))
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds+[overlap]);constants=constants_inventory(src,worlds+[overlap],scripts)
        npcs=npc_inventory(src,worlds+[overlap],stage/'npc');dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,tree=TREE,fullSourceHash=source_hash(src.lock),runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            reusedMaps=reused,connections=connections,warps=warps,inputFiles=src.lock['files'])
        for filename,doc in {'route11-catalog.json':catalog,'scripts.json':scripts,'constants.json':constants,'npc-source.json':npcs,'dependencies.json':dependencies}.items():
            (stage/filename).write_bytes(canonical(doc).encode())
        compact_derivatives(stage,src)
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=digest(p.read_bytes()),byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,fullSourceHash=source_hash(src.lock),lockSha256=LOCK_SHA256,
            inputCount=len(src.entries),mapCount=len(worlds),reusedMapCount=len(reused),cellCount=sum(len(w['cells']) for w in worlds),
            borderCellCount=sum(len(w['borderCells']) for w in worlds),warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
        if digest(data)!=MANIFEST_SHA256:raise ValueError('Reviewed generated output hashes differ: '+digest(data))
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest


from pathlib import Path
import hashlib
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
PREVIOUS_MANIFEST = 'bc889d0bfa494a142902951dedff141e43441517059152b5ea47f0da19f4c92e'
REGION_MANIFEST = MANIFEST_SHA256
REGION_CATALOG = 'ef5887516f7103da508a5ffbd9b28d2ebe104af6ae1ebb180d44774564edc693'
REGION_LOCK = LOCK_SHA256
SOURCE = 'e85f9c8575479f09000b712a953e86203f62e64dfd844725c2fb349b3347c77f'
MAXIMUM_MAP_BYTES = 2 * 1024 * 1024

def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


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
    require(type(entry['byteLength']) is int and 0 < entry['byteLength'] <= maximum, 'Invalid runtime input length')
    raw = bounded(root, entry['path'], maximum)
    require(len(raw) == entry['byteLength'] and sha(raw) == entry['sha256'], 'Pinned runtime input differs: ' + entry['path'])
    return raw


def export_runtime(generated, output, check=False):
    generated, output = Path(generated), Path(output)
    for kind in ['raw', 'generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind == 'generated' and protected.name == 'route11-runtime-maps': continue
            require(not output.resolve().is_relative_to(protected.resolve()) and
                    not protected.resolve().is_relative_to(output.resolve()), 'Existing profile overlap')
    source_locks = ROOT/'content/sources'
    require(not output.resolve().is_relative_to(source_locks.resolve()) and
            not source_locks.resolve().is_relative_to(output.resolve()), 'Source lock/output overlap')
    converter = ROOT / 'scripts/export-runtime-maps.py'
    require(sha(bounded(ROOT, 'scripts/export-runtime-maps.py', 64 * 1024)) == BASE_HASH, 'Reviewed base converter differs')
    spec = importlib.util.spec_from_file_location('_route11_region_runtime_base', converter)
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    previous, region = generated / 'runtime-city-services-maps', generated / 'route11-region'
    for source in (previous, region, ROOT / 'scripts'):
        require(not source.resolve().is_relative_to(output.resolve()) and
                not output.resolve().is_relative_to(source.resolve()), 'Source/output overlap')
    old_bytes = bounded(previous, 'manifest.json', 256 * 1024)
    require(sha(old_bytes) == PREVIOUS_MANIFEST, 'Prior 83-map manifest differs')
    old = json.loads(old_bytes)
    require(len(old['maps']) == 83 and old['converterVersion'] == base.VERSION and
            old['sourceCommit'] == base.COMMIT, 'Prior runtime collection differs')
    manifest_bytes = bounded(region, 'manifest.json', 128 * 1024)
    require(sha(manifest_bytes) == REGION_MANIFEST, 'Route11/Cave source manifest differs')
    source_manifest = json.loads(manifest_bytes)
    require(source_manifest['mapCount'] == 8 and source_manifest['fullSourceHash'] == SOURCE and
            source_manifest['sourceCommit'] == base.COMMIT, 'Route11/Cave source profile differs')
    inventory = {entry['path']: entry for entry in source_manifest['outputs']}
    catalog_bytes = authenticated(region, inventory['route11-catalog.json'], 256 * 1024)
    require(sha(catalog_bytes) == REGION_CATALOG, 'Route11/Cave source catalog differs')
    catalog = json.loads(catalog_bytes)
    lock = bounded(ROOT, 'content/sources/route11-region.lock.json', 256 * 1024)
    require(sha(lock) == REGION_LOCK and json.loads(lock)['files'] == catalog['inputFiles'], 'Route11/Cave source lock differs')
    payloads, entries = {}, []
    for entry in old['maps']:
        require(entry['path'] not in payloads, 'Duplicate prior runtime path')
        payloads[entry['path']] = authenticated(previous, entry, MAXIMUM_MAP_BYTES)
        entries.append(entry)
    registry = old['sourceRegistry']
    require(registry['path'] == 'source-registry.json', 'Prior source registry path differs')
    payloads[registry['path']] = authenticated(previous, registry, 256 * 1024)
    ship_lock = bounded(previous, 'ssanne-source-lock.json', 256 * 1024)
    require(sha(ship_lock) == '72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1', 'Prior ship source lock differs')
    payloads['ssanne-source-lock.json'] = ship_lock
    gym_lock = bounded(previous, 'vermilion-gym-source-lock.json', 256 * 1024)
    require(sha(gym_lock) == '569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8', 'Prior Gym source lock differs')
    payloads['vermilion-gym-source-lock.json'] = gym_lock
    city_lock = bounded(previous, 'city-services-source-lock.json', 256 * 1024)
    require(sha(city_lock) == '4b71a3820a2d50ff80c32589c60979cb519dd7aeecec9bec13696bf1ad317eb5', 'Prior city source lock differs')
    payloads['city-services-source-lock.json'] = city_lock
    payloads['route11-region-source-lock.json'] = lock
    new_cells = new_borders = new_warps = 0
    require(len(catalog['maps']) == 8, 'Route11/Cave map collection incomplete')
    for map_spec in catalog['maps']:
        directory = map_spec['directory']
        rich_entry, compact_entry = inventory[directory + '/world.json'], inventory[directory + '/runtime-map.json']
        raw = authenticated(region, rich_entry, 16 * 1024 * 1024)
        encoded = authenticated(region, compact_entry, MAXIMUM_MAP_BYTES)
        compact = base.convert(raw, rich_entry['sha256'], SOURCE)
        require(encoded == base.encode(compact), 'Route11/Cave compact derivative differs: ' + directory)
        require(compact['stableId'] == map_spec['stableId'] and compact['sourceRef']['symbol'] == map_spec['sourceMapSymbol'],
                'Route11/Cave map identity differs')
        path = 'route11-region/' + directory + '/runtime-map.json'
        require(path not in payloads, 'Duplicate new runtime path')
        payloads[path] = encoded
        entries.append(dict(stableId=compact['stableId'], path=path, byteLength=len(encoded), sha256=sha(encoded),
                            sourcePath='route11-region/' + directory + '/world.json', sourceByteLength=len(raw),
                            sourceWorldSha256=rich_entry['sha256'], fullSourceHash=SOURCE))
        new_cells += compact['width'] * compact['height']
        new_borders += compact['borderWidth'] * compact['borderHeight']
        new_warps += len(compact['events']['warp_events'])
    require((new_cells, new_borders, new_warps) == (8978, 32, 22), 'Route11/Cave source geometry differs')
    entries.sort(key=lambda entry: entry['path'])
    require(len(entries) == 91 and len({entry['stableId'] for entry in entries}) == 91, 'Runtime map collection incomplete')
    manifest = dict(schemaVersion=1, converterVersion=base.VERSION, sourceCommit=base.COMMIT,
                    converterSha256=sha(Path(__file__).read_bytes()), maps=entries, sourceRegistry=registry)
    payloads['manifest.json'] = base.encode(manifest)
    # Validate every input and every final/staging destination before any writes. Manifest is published last.
    for relative in payloads:
        target = safe(output, relative)
        safe(output, target.with_suffix('.tmp').relative_to(output).as_posix())
    for relative, encoded in payloads.items():
        target = safe(output, relative)
        if check:
            require(target.is_file() and bounded(output, relative, max(len(encoded), 1)) == encoded, 'Stale output: ' + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            stage = target.with_suffix('.tmp')
            stage.write_bytes(encoded)
            stage.replace(target)
    return dict(maps=91, preservedMaps=83, newCells=new_cells, newBorderCells=new_borders, newWarps=new_warps,
                runtimeBytes=sum(map(len, payloads.values())), manifestSha256=sha(payloads['manifest.json']),
                converterSha256=manifest['converterSha256'], sourceLockSha256=sha(lock))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/route11-region')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/route11-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or upstream execution.')
    parser.add_argument('--runtime-only',action='store_true')
    parser.add_argument('--runtime-output',type=pathlib.Path,default=ROOT/'content/generated/route11-runtime-maps')
    parser.add_argument('--check-runtime',action='store_true')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    if not args.runtime_only and not args.check_runtime:import_catalog(args.raw,args.out)
    print(json.dumps(export_runtime(ROOT/'content/generated',args.runtime_output,args.check_runtime),sort_keys=True))
