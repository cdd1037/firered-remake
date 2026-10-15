#!/usr/bin/env python3
"""Build one original Route16 Fly house and a bounded north-passage source contract.

Original blobs are inert. Reviewed existing regional-service render/compact tools
are reused; shared raw inputs and existing Route16/gate/NPC resources are referenced.
This source package does not admit a route, trainer, service or field move.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from map_runtime import normalize_runtime, runtime_cells
import argparse, base64, collections,  importlib.util, json, os, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'route16-fly-house'
RESOURCE_ID = 'firered-route16-fly-house-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAPS = ['Route16_House']
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_GenericBuilding1':'secondary/generic_building_1'}
ANCHORS = [('Route16','route-16/runtime-map.json'),
           ('Route16_NorthEntrance_1F','route-16-north-entrance-1f/runtime-map.json')]
NPC_REUSE = {'OBJ_EVENT_GFX_WOMAN_1':'content/generated/city-services/npc/woman-1.png',
 'OBJ_EVENT_GFX_RED_NORMAL':'content/generated/city-services/npc/red-normal.png'}
def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2)+'\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: '+str(path))
def module(path, name):
    safe(path)
    spec=importlib.util.spec_from_file_location(name,path); value=importlib.util.module_from_spec(spec); spec.loader.exec_module(value); return value

def script_inventory(src, reader, worlds):
    blocks, aliases = {}, {}
    paths = [p for p in src.tree if p.endswith('.inc') and
             (p.startswith(('data/scripts/', 'data/text/')) or p.endswith('/text.inc') or any(p == f'data/maps/{name}/scripts.inc' for name in MAPS))]
    for path in sorted(paths + ['data/event_scripts.s']):
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
    roots = {label for label, b in blocks.items() if b['path'] in {f'data/maps/{name}/scripts.inc' for name in MAPS}}
    roots.update(e['script'] for world in worlds for kind in ('object_events', 'coord_events', 'bg_events')
                 for e in world['events'][kind] if e.get('script') not in (None, '0', '0x0', 'NULL'))
    implicit = ['Std_MsgboxNPC', 'Std_MsgboxSign', 'Std_MsgboxDefault', 'Std_MsgboxYesNo', 'Std_ReceivedItem', 'Std_PutItemAway', 'Std_ObtainItem']
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
        block = blocks[label]
        src.read(block['path'], 'script-label-closure')
        result.append(dict(label=label, sourceRef=src.ref(block['path'], label, block['line']), lines=block['lines']))
        for row in block['lines']:
            code = row['text'].split('@')[0].strip()
            if not code or code.startswith(('.', '#')): continue
            parts = code.split(None, 1); op = parts[0]; operands = parts[1] if len(parts) == 2 else ''
            opcodes[op] += 1
            if op in ('special', 'specialvar'): specials[operands.split(',')[-1].strip()] += 1
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'): target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement', 'case') or op.startswith(('goto_if_', 'call_if_')): target = operands.split(',')[-1].strip()
            elif op in ('msgbox', 'giveitem_msg', 'msgreceiveditem'): target = operands.split(',')[0].strip()
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(label=label, line=row['line'], target=target))
    need(not unresolved, 'Unresolved script labels: ' + repr(unresolved))
    return dict(schemaVersion=1, executable=False, roots=sorted(roots), implicitStandardRoots=implicit,
                sourcePaths=sorted({b['sourceRef']['path'] for b in result}), blocks=result,
                aliases={a: b for a, b in aliases.items() if b in selected},
                opcodeCounts=dict(sorted(opcodes.items())), specialCounts=dict(sorted(specials.items())), unresolvedLabelReferences=unresolved)


def trainer_inventory(src):
    texts, inputs, raw = {}, {}, {}
    def read(path):
        if path not in texts:
            data = src.read(path, "north-passage-double-source")
            inputs[path] = dict(path=path)
            raw[path] = data
            texts[path] = data.decode()
        return texts[path]
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)
    def number(path, symbol):
        value = one(path, r"^#define " + symbol + r"\s+([^\n]+)")[1].split("//")[0].strip()
        return int(value, 0) if re.fullmatch(r"(?:0x[0-9a-fA-F]+|\d+)", value) else 1 << int(re.fullmatch(r"\(1 << (\d+)\)", value)[1])
    def encode(name):
        return [int(one("charmap.txt", "^'" + re.escape(c) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16) for c in name]
    def identity(symbol):
        return dict(value="pret:" + symbol)
    def ai_flags(symbols):
        return sum(1 << int(one("include/constants/battle_ai.h", r"^#define " + s + r"\s+\(1 << (\d+)\)[^\n]*")[1]) for s in symbols)
    def natural_moves(species, level):
        pointer = one("src/data/pokemon/level_up_learnset_pointers.h", r"^    \[" + species + r"\] = (\w+),$")[1]
        body = one("src/data/pokemon/level_up_learnsets.h", r"^static const u16 " + pointer + r"\[\] = \{\n(.*?)^\};")[1]
        moves = []
        for lv, move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)", body):
            if int(lv) <= level and move not in moves:
                moves = (moves + [move])[-4:]
        need(1 <= len(moves) <= 4, "Expected source natural moves")
        return moves

    trainers, resolved = [], []
    for short in ['YOUNG_COUPLE_LEA_JED']:
        symbol, path = "TRAINER_" + short, "src/data/trainers.h"
        match = one(path, r"^    \[" + symbol + r"\] = \{\n(.*?)^    \},")
        fields = dict(re.findall(r"^        \.(\w+) = (.*),$", match[1], re.M))
        shape, party_symbol = re.fullmatch(r"((?:NO_ITEM|ITEM)_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)", fields["party"]).groups()
        custom, held = "CUSTOM" in shape, shape.startswith("ITEM_")
        flags = int(custom) | (int(held) << 1)
        party_path = "src/data/trainer_parties.h"
        party = one(party_path, r"^static const struct TrainerMon" + ("Item" if held else "NoItem") + ("Custom" if custom else "Default") + "Moves " + party_symbol + r"\[\] = \{\n(.*?)^\};")
        rows = list(re.finditer(r"    \{\n(.*?)    \},\n", party[1], re.S))
        need("".join(r[0] for r in rows) == party[1], "Unhandled trainer party syntax")
        members, movesets = [], []
        for row in rows:
            mf = dict(re.findall(r"^        \.(\w+) = (.*),$", row[1], re.M))
            need(set(mf) == {"iv", "lvl", "species"} | ({"heldItem"} if held else set()) | ({"moves"} if custom else set()), "Unexpected member fields")
            species, level = mf["species"], int(mf["lvl"])
            names_path = "src/data/text/species_names.h"
            name = one(names_path, r'^    \[' + species + r'\] = _\("([^"]+)"\),$')
            moves = mf["moves"].strip("{}").split(", ") if custom else natural_moves(species, level)
            members.append(dict(species=identity(species), sourceSpeciesId=number("include/constants/species.h", species),
                speciesName=name[1], encodedSpeciesName=encode(name[1]), level=level, sourceIv=int(mf["iv"]),
                heldItem=mf.get("heldItem", "ITEM_NONE"), customMoves=[identity(m) for m in moves] if custom else [],
                source=ref(party_path, party_symbol, party), speciesNameSource=ref(names_path, species, name)))
            movesets.append(dict(species=identity(species), level=level, moves=moves))
        name = re.fullmatch(r'_\("([^"]+)"\)', fields["trainerName"])[1]
        items = fields["items"].strip("{}").split(", ") if fields["items"] != "{}" else []
        items += ["ITEM_NONE"] * (4 - len(items))
        money = one("src/battle_main.c", r"^\s*\{" + fields["trainerClass"] + r", (\d+)\},$")
        ai = fields["aiFlags"].split(" | ")
        trainers.append(dict(id=identity(symbol), sourceTrainerId=number("include/constants/opponents.h", symbol),
            trainerClass=fields["trainerClass"], sourceTrainerClass=number("include/constants/trainers.h", fields["trainerClass"]),
            trainerPic=fields["trainerPic"], sourceTrainerPic=number("include/constants/trainers.h", fields["trainerPic"]),
            sourceTrainerName=name, encodedTrainerName=encode(name),
            encounterMusicGender=sum(number("include/constants/trainers.h", v) if not v.isdigit() else int(v) for v in fields["encounterMusic_gender"].split(" | ")),
            items=items, doubleBattle=fields["doubleBattle"] == "TRUE", partyFlags=flags,
            aiFlags=ai_flags(ai), aiFlagSymbols=ai, baseMoney=int(money[1]), party=members,
            source=ref(path, symbol, match), moneySource=ref("src/battle_main.c", fields["trainerClass"], money)))
        resolved.append(dict(trainer=symbol, party=movesets))
    battles=[]
    for name in ['Lea','Jed']:
        label='Route16_EventScript_'+name; path='data/scripts/trainers.inc'
        block=one(path,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        command=re.search(r'^\ttrainerbattle_double ([^\n]+)$',block[1],re.M)
        need(command is not None, 'Original Lea/Jed double command absent')
        args=command[1].split(', '); post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
        need(args[0]=='TRAINER_YOUNG_COUPLE_LEA_JED', 'Original shared trainer ID differs')
        battles.append(dict(trainer=identity(args[0]),mode=4,rivalFlags=0,battleTypeFlags=9,scriptEntry=label,
            introText=args[1],defeatText=args[2],victoryText='',postBattleText=post[1],entryScript='EventScript_TryDoDoubleTrainerBattle',
            callback='CB2_EndTrainerBattle',source=ref(path,label,block),postBattleScript=None,musicOption=None,notEnoughMonsText=args[3]))
    need(len(trainers)==1 and trainers[0]['doubleBattle'] and trainers[0]['sourceTrainerId']==489 and trainers[0]['aiFlags']==1 and
         [(m['species']['value'],m['level'],m['sourceIv']) for m in trainers[0]['party']]==[('pret:SPECIES_RAPIDASH',29,0),('pret:SPECIES_NINETALES',29,0)], 'Original Lea/Jed party differs')
    return dict(sourceCommit=COMMIT,profile=RESOURCE_ID,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved)


def retained_maps(src, current):
    directory=current/'content/generated/snorlax-region'; safe(directory)
    manifest=(directory/'manifest.json').read_bytes()
    worlds, anchors=[],[]
    for name, relative in ANCHORS:
        path=directory/relative; safe(path); data=path.read_bytes()
        world=normalize_runtime(json.loads(data)); original=src.json('data/maps/'+name+'/map.json')
        need(world['events']==original, 'Retained map events differ: '+name)
        worlds.append(dict(world, cells=list(runtime_cells(world))))
        anchors.append(dict(sourceName=name,stableId=world['stableId'],sourceMap=original['id'],
          manifestPath='content/generated/snorlax-region/manifest.json',
          resourcePath='content/generated/snorlax-region/'+relative,
          operation='retain-existing-instance',geometryChanges=[],eventChanges=[]))
    return worlds, anchors


def north_contract(src, worlds, house, anchors):
    route, gate=worlds; by_symbol={w['events']['id']:w for w in [route,gate,house]}
    allowed={'MAP_ROUTE16':[0,1,2], 'MAP_ROUTE16_NORTH_ENTRANCE_1F':[0,1], 'MAP_ROUTE16_HOUSE':[0,1,2]}
    links=[]
    for symbol,indexes in allowed.items():
        for index in indexes:
            event=by_symbol[symbol]['events']['warp_events'][index]
            target=by_symbol[event['dest_map']]; destination=int(event['dest_warp_id']); back=target['events']['warp_events'][destination]
            need(back['dest_map']==symbol, 'Nonreciprocal map endpoint')
            # Three original house threshold tiles intentionally share Route16 warp 0;
            # its inbound destination is the middle threshold (house warp 1).
            need(int(back['dest_warp_id'])==index or (symbol=='MAP_ROUTE16_HOUSE' and destination==0 and int(back['dest_warp_id'])==1), 'Unexpected reciprocal warp index')
            links.append(dict(sourceMap=symbol,sourceIndex=index,source=event,destinationMap=event['dest_map'],destinationIndex=destination,destination=back))
    cells=[]
    for w in worlds:
        eligible=[c for c in w['cells'] if c['collision']==0 and c['elevation']==3 and
            ((5<=c['y']<=10 and 2<=c['x']<=41) or (c['x'],c['y'])==(41,11) if w is route else 2<=c['y']<=5) and
            c['behaviorName'] in ('MB_NORMAL','MB_TALL_GRASS','MB_WEST_ARROW_WARP','MB_EAST_ARROW_WARP')]
        cells.append(dict(map=w['stableId'],cells=[[c['x'],c['y']] for c in eligible],cellCount=len(eligible)))
    objects=route['events']['object_events']
    need([(objects[i]['x'],objects[i]['y'],objects[i]['script']) for i in [6,7,8]]==
         [(41,11,'EventScript_CutTree'),(30,5,'Route16_EventScript_Lea'),(31,5,'Route16_EventScript_Jed')], 'Original upper path actors differ')
    need(all(c['y']<6 for c in gate['cells'] if [c['x'],c['y']] in cells[1]['cells']), 'Upper gate scope leaked')
    # Collision-derived connectivity checks: no bypass around Cut, and the
    # upstairs/lower cycling passage remains disconnected from the upper room.
    route_cells={tuple(p) for p in cells[0]['cells']}
    def flood(points,start):
        reached={start}; pending=[start]
        while pending:
            x,y=pending.pop()
            for n in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if n in points and n not in reached:reached.add(n);pending.append(n)
        return reached
    east=flood(route_cells,(41,11)); west=flood(route_cells,(20,6))
    need((27,6) in east and (10,6) in west and east.isdisjoint(west), 'Original separated upper outdoor pockets differ')
    need((41,10) in east and [41,11] in cells[0]['cells'] and (41,12) not in route_cells, 'Cut threshold differs')
    return dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,runtimeIntegrated=False,
        retainedMaps=anchors,upperFootAdmissions=cells,allowedWarps=[dict(map=by_symbol[s]['stableId'],indexes=v) for s,v in allowed.items()],
        topology=links,cutTree=dict(map=route['stableId'],localId=7,event=objects[6],approach=[41,12]),
        presetDouble=dict(trainer='pret:TRAINER_YOUNG_COUPLE_LEA_JED',sourceTrainerId=489,localIds=[8,9],
            actors=[objects[7],objects[8]],sharedDefeatFlag=True,requiredUsableParty=2,rematchesAdmitted=False),
        gateActor=dict(map=gate['stableId'],localId=2,event=gate['events']['object_events'][1]),
        houseActors=[dict(localId=i+1,event=e) for i,e in enumerate(house['events']['object_events'])],
        excludedMaps=['MAP_ROUTE16_NORTH_ENTRANCE_2F','MAP_ROUTE17'],
        excludedWarps=[dict(map=route['stableId'],indexes=[3,4]),dict(map=gate['stableId'],indexes=[2,3,4])],
        requiredPreflight=['Retain original Cut tree until the ordinary Cut command removes local object 7 via FLAG_TEMP_12.',
          'Preserve Lea/Jed original sight, scripts and preset double entry; both actors share trainer 489 and one defeat state.',
          'Keep ordinary collision, elevation, actors, grass encounters and input transaction checks after this admission preflight.',
          'Route16 scene must remain 0 and FLAG_SYS_ON_CYCLING_ROAD clear; upper gate callback remains original.',
          'NorthEntrance1F lower cycling room and its stairs remain rejected before state changes.',
          'House gift and field Fly are separate application/domain integration; imported scripts alone authorize no operation.'])


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--dependency-root',type=Path,default=ROOT)
    p.add_argument('--project-root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
    target,dep,ref=[Path(os.path.abspath(x)) for x in [a.project_root,a.dependency_root,a.reference_root]]
    for path in [target,dep,ref]:safe(path)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'route16_fly_reader')
    reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,RESOURCE_ID
    util=module(dep / 'scripts/import-fossil-lab-region.py', 'route16_fly_util');util.MAPS=MAPS
    src=reader.Source(ref);house=reader.decode(src,MAPS[0]);worlds,anchors=retained_maps(src,dep)
    need((house['width'],house['height'],len(house['events']['warp_events']))==(11,9,3), 'Original house geometry differs')
    scripts=script_inventory(src,reader,[house]);constants=util.constants_inventory(src,scripts,[house])
    need(constants['values']['ITEM_HM02']['value']==340 and constants['values']['FLAG_GOT_HM02']['value']==0x238, 'Original HM02 grant constants differ')
    for leaf in ['scripts.inc','text.inc']:src.read('data/maps/Route16_House/'+leaf,'live-map-script')
    trainer=trainer_inventory(src);contract=north_contract(src,worlds,house,anchors)
    contract['gift']=dict(map=house['stableId'],localId=1,script='Route16_House_EventScript_Woman',item='ITEM_HM02',flag='FLAG_GOT_HM02',sourceRef=src.ref('data/maps/Route16_House/scripts.inc','Route16_House_EventScript_Woman',4))
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=RESOURCE_ID,TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='route16-fly-house-',dir=target) as temp:
        stage=Path(temp);shared.import_map(src,MAPS[0],house['stableId'],stage/'route-16-house')
        npcs=shared.npc_inventory(src,[house],stage/'npc')
        need({r['graphicsId'] for r in npcs['graphics']}==set(NPC_REUSE)|{'OBJ_EVENT_GFX_FEAROW'}, 'House actor resource cohort differs')
        for row in npcs['graphics']:
            if row['graphicsId'] in NPC_REUSE:
                path=NPC_REUSE[row['graphicsId']];existing=dep/path;safe(existing);rendered=stage/row['texturePath']
                need(rendered.read_bytes() == existing.read_bytes(), 'Reused actor differs')
                row.update(texturePath=path,reuseExisting=True);rendered.unlink()
            else:row.update(reuseExisting=False)
        path=stage/'route-16-house/runtime-map.json';rich=path.read_bytes();compact=converter.normalize_runtime(json.loads(rich))
        util.compact_check(compact,house,src);data=converter.encode(compact);(stage/'route-16-house/runtime-map.json').write_bytes(data)
        entry=dict(sourceName=MAPS[0],stableId=house['stableId'],directory='route-16-house',sourceMapSymbol='MAP_ROUTE16_HOUSE',
            path='route-16-house/runtime-map.json')
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False)
        own_raw=[]
        for row in lock['files']:
            raw=src.data[row['path']];storage=None
            candidate=dep/'content/raw/pokefirered/037335f4'/row['path'];safe(candidate)
            if candidate.is_file() and candidate.read_bytes()==raw:
                storage=candidate.relative_to(dep).as_posix()
            row['reuseExisting']=storage is not None
            row['storagePath']=storage or 'content/raw/pokefirered/037335f4/'+row['path']
            if storage is None:own_raw.append((target/row['storagePath'],raw))
        lock_bytes=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,resourceId=RESOURCE_ID,sourceCommit=COMMIT,
                maps=[entry],inputFiles=lock['files'],tilesets=TILESETS), 'scripts.json':scripts,
                'contracts.json':contract,'trainers.json':trainer,'npc-source.json':npcs}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=RESOURCE_ID,sourceCommit=COMMIT,inputCount=len(src.entries),ownedRawFileCount=len(own_raw),
            mapCount=1,retainedMapCount=2,cellCount=99,borderCellCount=4,directedWarpCount=8,objectCount=2,newNpcGraphicsCount=1,reusedNpcGraphicsCount=2,
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest);(stage/'manifest.json').write_bytes(manifest_bytes)
        writes=[(target/'scripts/route16-fly-house.lock.json',lock_bytes),(target/'scripts/route16-fly-house.contract.json',canonical(contract))]+own_raw
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if a.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
        if not a.check:
            for path,data in writes:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(inputCount=len(src.entries),ownedRawFileCount=len(own_raw),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),
            upperFootCounts=[r['cellCount'] for r in contract['upperFootAdmissions']],resolvedMoves=trainer['resolvedMoves'],check=a.check),indent=2))


if __name__=='__main__':main()
