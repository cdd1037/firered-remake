#!/usr/bin/env python3
"""Source-locked Lost Cave, Resort Gorgeous and Water Labyrinth candidate.

Reads pinned upstream blobs only. Reuses reviewed local converters; no upstream
code executes. This package does not admit any map or service into the game.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse,  importlib.util, json, os, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'lost-cave-region'
PROFILE = 'firered-lost-cave-region-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAPS = ['FiveIsland_WaterLabyrinth', 'FiveIsland_ResortGorgeous', 'FiveIsland_ResortGorgeous_House',
        'FiveIsland_LostCave_Entrance'] + ['FiveIsland_LostCave_Room' + str(i) for i in range(1,15)]
TILESETS = {'gTileset_General':'primary/general', 'gTileset_Building':'primary/building',
    'gTileset_SeviiIslands45':'secondary/sevii_islands_45', 'gTileset_Cave':'secondary/cave',
    'gTileset_Museum':'secondary/museum'}
def canonical(v): return (json.dumps(v, sort_keys=True, indent=2)+'\n').encode()
def need(v, message):
    if not v: raise ValueError(message)
def safe(p): need(not any(x.is_symlink() for x in [p,*p.parents]), 'Symlink rejected: '+str(p))
def module(path, name):
    safe(path)
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result); return result

def selphy_service(src, worlds, inventory):
    blocks={b['label']:b for b in inventory['blocks']};aliases=inventory['aliases']
    implicit={'giveitem':'Std_ObtainItem','giveitem_msg':'Std_ReceivedItem',
        'msgreceiveditem':'Std_ReceivedItem','putitemaway':'Std_PutItemAway','finditem':'Std_FindItem'}
    messages={'MSGBOX_DEFAULT':'Std_MsgboxDefault','MSGBOX_YESNO':'Std_MsgboxYesNo',
        'MSGBOX_SIGN':'Std_MsgboxSign','MSGBOX_NPC':'Std_MsgboxNPC','MSGBOX_AUTOCLOSE':'Std_MsgboxAutoclose'}
    root='FiveIsland_ResortGorgeous_House_EventScript_Selphy';pending=[root];selected=set()
    while pending:
        label=aliases.get(pending[-1],pending[-1]);pending.pop()
        if label in selected:continue
        need(label in blocks,'Missing Selphy service call target '+label);selected.add(label)
        for row in blocks[label]['lines']:
            code=row['text'].split('@')[0].strip()
            if not code:continue
            op=code.split()[0]
            if op in implicit:pending.append(implicit[op])
            if op=='msgbox':pending.append(messages[code.split(',')[-1].strip()] if ',' in code else 'Std_MsgboxDefault')
            pending += [aliases.get(t,t) for t in re.findall(r'\b\w+\b',re.sub(r'"[^"\n]*"','',code))
                        if aliases.get(t,t) in blocks and aliases.get(t,t) not in selected]
    programs=sorted(b for b in selected if '_Movement_' not in b and not any(r['text'].strip().startswith('.string') for r in blocks[b]['lines']))
    text=src.text('src/field_specials.c')
    reward_body=re.search(r'static const u16 sResortGorgeousDeluxeRewards\[\] = \{(.*?)\};',text,re.S)[1]
    items=['ITEM_LUXURY_BALL']+re.findall(r'ITEM_\w+',reward_body)
    need(items==['ITEM_LUXURY_BALL','ITEM_BIG_PEARL','ITEM_PEARL','ITEM_STARDUST','ITEM_STAR_PIECE','ITEM_NUGGET','ITEM_RARE_CANDY'], 'Original reward cohort differs')
    need('return ITEM_LUXURY_BALL;' in text,'Original standard reward differs')
    world=next(w for w in worlds if w['sourceName']=='FiveIsland_ResortGorgeous_House');event=world['events']['object_events'][0]
    need(event['script']==root and event['local_id']=='LOCALID_RESORT_GORGEOUS_HOUSE_SELPHY','Original Selphy actor binding differs')
    return dict(id='lost-cave-region:selphy-request-reward',mapId=world['stableId'],scriptId=root,
        sourceLocalId=1,backgroundX=None,backgroundY=None,eventGroup='object_events',eventIndex=0,sourceEvent=event,
        programIds=programs,itemSymbols=items,otherReachableLabels=sorted(selected-set(programs)),
        rewardSource=src.ref('src/field_specials.c','sResortGorgeousDeluxeRewards'),
        standardRewardSource=src.ref('src/field_specials.c','SampleResortGorgeousReward'))

def contract(src, worlds, scripts, links):
    blocks={b['label']:b for b in scripts['blocks']}
    def lines(label): return [r['text'].split('@')[0].strip() for r in blocks[label]['lines'] if r['text'].split('@')[0].strip()]
    found='FiveIsland_LostCave_Room10_EventScript_SetSelphyFound'
    need(lines(found)==['setflag FLAG_HIDE_LOST_CAVE_SELPHY','clearflag FLAG_HIDE_RESORT_GORGEOUS_SELPHY',
        'setvar VAR_MAP_SCENE_FIVE_ISLAND_LOST_CAVE_ROOM10, 1','setvar VAR_MAP_SCENE_FIVE_ISLAND_RESORT_GORGEOUS, 1','return'], 'Original rescue flags/order differs')
    home='FiveIsland_ResortGorgeous_EventScript_SelphyReturnHomeScene'
    need('clearflag FLAG_HIDE_RESORT_GORGEOUS_INSIDE_SELPHY' in lines(home) and
        'setvar VAR_MAP_SCENE_FIVE_ISLAND_RESORT_GORGEOUS, 2' in lines(home), 'Original return-home unlock differs')
    pickups=[dict(map=w['events']['id'],localId=i,event=e,script=blocks[e['script']])
             for w in worlds for i,e in enumerate(w['events']['object_events'],1) if e['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL']
    hidden=[dict(map=w['events']['id'],event=e) for w in worlds for e in w['events']['bg_events'] if 'item' in e]
    doors=[r for r in links['warps'] if 'LOST_CAVE_ROOM' in r['sourceMap']]
    reset=[r for r in doors if r['source']['dest_map']=='MAP_FIVE_ISLAND_LOST_CAVE_ROOM1']
    directions={(5,1):'up',(8,5):'right',(5,8):'down',(2,5):'left'}
    puzzle=[dict(sourceMap=r['sourceMap'],sourceIndex=r['sourceIndex'],
        direction=directions.get((r['source']['x'],r['source']['y']),'stairs-or-exit'),
        destinationMap=r['source']['dest_map'],destinationIndex=int(r['source']['dest_warp_id'])) for r in doors]
    anchor=src.json('data/maps/FiveIsland/map.json')
    north=next(c for c in anchor['connections'] if c['map']=='MAP_FIVE_ISLAND_WATER_LABYRINTH')
    need(north==dict(map='MAP_FIVE_ISLAND_WATER_LABYRINTH',offset=-48,direction='up'), 'Five Island original north connection differs')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,mapTopology=links,
        serviceBindings=[selphy_service(src,worlds,scripts)],
        itemPickups=pickups,hiddenItems=hidden,
        retainedAnchor=dict(map='map.five-island',sourceRef=src.ref('data/maps/FiveIsland/map.json','MAP_FIVE_ISLAND'),
            connection=north,geometryChanges=[],eventChanges=[]),
        puzzle=dict(routeType='original-static-directed-warps',dynamicWarpCount=0,rooms=puzzle,
            resetWarps=reset,roomCount=14,entranceCount=1,
            warning='One-way exits and resets retain source destination warp indexes; do not synthesize reciprocal links.'),
        rescue=dict(runtimeAdmitted=False,entryMap='MAP_FIVE_ISLAND_LOST_CAVE_ROOM10',
            scene=blocks['FiveIsland_LostCave_Room10_EventScript_FindSelphyScene'],found=blocks[found],
            returnHome=blocks[home],questLog=blocks['FiveIsland_LostCave_Room10_EventScript_SelphyQuestLog'],
            movements=[blocks[n] for n in sorted(blocks) if n.startswith(('FiveIsland_LostCave_Room10_Movement_', 'FiveIsland_ResortGorgeous_Movement_'))],
            houseHideFlag='FLAG_HIDE_RESORT_GORGEOUS_INSIDE_SELPHY',
            landing=dict(map='MAP_FIVE_ISLAND_RESORT_GORGEOUS',x=39,y=10)),
        services=dict(runtimeAdmitted=False,selphyEntry='FiveIsland_ResortGorgeous_House_EventScript_Selphy',
            selphySource='src/field_specials.c',stepSource='src/field_control_avatar.c',
            requestCounter='VAR_RESORT_GOREGEOUS_STEP_COUNTER',requestedMon='VAR_RESORT_GORGEOUS_REQUESTED_MON',
            reward='VAR_RESORT_GORGEOUS_REWARD',visitFlag='FLAG_TEMP_2',
            eggEntry='FiveIsland_WaterLabyrinth_EventScript_EggGentleman',eggSpecies='SPECIES_TOGEPI',
            eggFlags=['FLAG_GOT_TOGEPI_EGG','FLAG_NO_ROOM_FOR_TOGEPI_EGG'],
            eggRequirements=['GetLeadMonFriendship result6','party size below PARTY_SIZE'],
            giftAndPartyIdSpecials=['giveegg','PlayerPartyContainsSpeciesWithPlayerID']),
        mapHeaders=[dict(map=w['events']['id'],allowCycling=w['events']['allow_cycling'],allowEscaping=w['events']['allow_escaping'],
            allowRunning=w['events']['allow_running'],weather=w['events']['weather'],music=w['events']['music']) for w in worlds],
        limitations=['Content candidate only: no current composition, Application or save schema edits.',
            'Original Surf/fishing encounters, trainer parties and pickups require existing shared runtime wiring.',
            'Selphy rescue/home scene, request service and Togepi egg service require explicit Application integration.',
            'Source fog/weather, map preview and physical audio acceptance are not implied.'])

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--dependency-root',type=Path,default=ROOT)
    ap.add_argument('--project-root',type=Path,default=ROOT)
    ap.add_argument('--check',action='store_true'); a=ap.parse_args()
    target,dep,ref=[Path(os.path.abspath(x)) for x in (a.project_root,a.dependency_root,a.reference_root)]
    for p in (target,dep,ref):safe(p)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'lost_cave_reader')
    five=module(dep / 'scripts/import-five-island-region.py', 'lost_cave_five_utils')
    util=module(dep / 'scripts/import-fossil-lab-region.py', 'lost_cave_compact_check')
    reader.PRODUCT=dep;reader.TILESETS=TILESETS;reader.PROFILE=PROFILE
    five.CORE=MAPS;five.SUPPORT=[];five.ANCHOR=[];five.PROFILE=PROFILE
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in MAPS]
    scripts=five.script_index(src,reader);scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
    links=five.topology(src,worlds)
    proofbytes=(dep/'content/generated/canonical-pokemon/catalog.json').read_bytes()
    data=five.declarations(src,scripts,worlds,json.loads(proofbytes))
    data['itemDeclarations']=[i for i in data['itemDeclarations'] if i['itemId']!='ITEM_SAPPHIRE']
    data['limitations']=['Complete unchanged trainer parties and FireRed wild tables are source declarations; runtime integration remains separate.',
        'The original Togepi egg gift is preserved in scripts and service contracts; no substituted party/species.']
    for p in reader.META_PATHS+['src/event_data.c','src/new_game.c','src/field_tasks.c','src/fieldmap.c',
        'src/bike.c','src/party_menu.c','src/data/heal_locations.json','src/data/heal_locations.constants.json.txt',
        'tools/mapjson/mapjson.cpp','data/maps/map_groups.json','include/gba/defines.h',
        'src/data/tilesets/headers.h','src/data/tilesets/graphics.h']:
        src.read(p,'rule-reference')
    for n in ['flags','vars','maps','map_scripts','event_objects','event_object_movement','opponents','trainers',
        'species','moves','items','abilities','songs','battle','battle_ai','battle_setup','menu','seagallop',
        'metatile_labels','metatile_behaviors','cable_club','fame_checker','field_effects','field_weather',
        'game_stat','global','help_system','pokemon','sound','union_room','daycare','field_tasks']:
        src.read('include/constants/'+n+'.h','script-operand-constant')
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    const=five.constants(src,shared,worlds,scripts)
    contracts=contract(src,worlds,scripts,links)
    # Publish gameplay pickup operands, never the script transcript used to derive them.
    for pickup in contracts['itemPickups']:
        source_script = pickup.pop('script')
        matches = [re.fullmatch(r'\s*finditem\s+(ITEM_[A-Z0-9_]+)(?:\s*,\s*(\d+))?\s*', row['text']) for row in source_script['lines']]
        matches = [match for match in matches if match]
        need(len(matches) == 1, 'Lost Cave pickup instruction differs')
        pickup.update(item=matches[0][1], quantity=int(matches[0][2] or 1), sourceRef=source_script['sourceRef'])

    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='lost-cave-import-',dir=target) as temp:
        stage=Path(temp)
        for w in worlds:shared.import_map(src,w['sourceName'],w['stableId'],stage/reader.directory(w['sourceName']))
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        maps=[]
        for w in worlds:
            directory=reader.directory(w['sourceName']);p=stage/directory/'runtime-map.json';rich=p.read_bytes()
            compact=converter.normalize_runtime(json.loads(rich));util.compact_check(compact,w,src)
            raw=converter.encode(compact);(stage/directory/'runtime-map.json').write_bytes(raw)
            maps.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=directory,
                sourceMapSymbol=w['events']['id'],path=directory+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=MAPS);lb=canonical(lock)
        symbols={w['events']['id']:w['stableId'] for w in worlds};symbols['MAP_FIVE_ISLAND']='map.five-island'
        docs=package_documents(target / 'content/generated', const, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            runtimeIntegrated=False,maps=maps,tilesets=TILESETS,mapSymbols=symbols,inputFiles=lock['files']),
            'scripts.json':scripts,'contracts.json':contracts,'items.json':dict(declarations=data['itemDeclarations']),
            'npc-source.json':npcs,'geometry.json':dict(maps=reader.geometry_inventory(worlds),tilesets=TILESETS)}, check=a.check)
        for n,d in docs.items():(stage/n).write_bytes(canonical(d))
        outputs=[dict(path=p.relative_to(stage).as_posix())
                 for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(len(w['cells']) for w in worlds),
            warpCount=len(links['warps']),connectionCount=len(links['connections']),npcGraphicsCount=len(npcs['graphics']),
            trainerCount=len(data['trainers']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,
            outputs=outputs)
        mb=canonical(manifest);(stage/'manifest.json').write_bytes(mb)
        writes=[(target / 'scripts/lost-cave-region.contract.json', canonical(contracts))]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if p.suffix=='.json':json.loads(b)
            if a.check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
        if not a.check:
            for p,b in writes:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes),check=a.check),indent=2))
if __name__=='__main__':main()
