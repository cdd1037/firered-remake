#!/usr/bin/env python3
"""Recovery candidate for authenticated Celadon approach maps. Original output pins remain mandatory."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse,collections,importlib.util,json,pathlib,re,shutil,tempfile
ROOT=pathlib.Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-celadon-approach-region-import-v1'
MAPS=[('LavenderTown_PokemonCenter_1F','map.lavender-town-pokemon-center-1f','lavender-town-pokemon-center-1f'),('LavenderTown_PokemonCenter_2F','map.lavender-town-pokemon-center-2f','lavender-town-pokemon-center-2f'),('LavenderTown_Mart','map.lavender-town-mart','lavender-town-mart'),('LavenderTown_House1','map.lavender-town-house-1','lavender-town-house-1'),('LavenderTown_VolunteerPokemonHouse','map.lavender-town-volunteer-pokemon-house','lavender-town-volunteer-pokemon-house'),('Route8','map.route-8','route-8'),('Route8_WestEntrance','map.route-8-west-entrance','route-8-west-entrance'),('UndergroundPath_EastEntrance','map.underground-path-east-entrance','underground-path-east-entrance'),('UndergroundPath_EastWestTunnel','map.underground-path-east-west-tunnel','underground-path-east-west-tunnel'),('UndergroundPath_WestEntrance','map.underground-path-west-entrance','underground-path-west-entrance'),('Route7','map.route-7','route-7'),('Route7_EastEntrance','map.route-7-east-entrance','route-7-east-entrance'),('CeladonCity','map.celadon-city','celadon-city'),('CeladonCity_PokemonCenter_1F','map.celadon-city-pokemon-center-1f','celadon-city-pokemon-center-1f'),('CeladonCity_PokemonCenter_2F','map.celadon-city-pokemon-center-2f','celadon-city-pokemon-center-2f')]
SERVICE_MAPS=[n for n,_,_ in MAPS]
TILESETS={'gTileset_General':'primary/general','gTileset_Building':'primary/building','gTileset_PokemonCenter':'secondary/pokemon_center','gTileset_GenericBuilding1':'secondary/generic_building_1','gTileset_GenericBuilding2':'secondary/generic_building_2','gTileset_FanClubDaycare':'secondary/fan_club_daycare','gTileset_Mart':'secondary/mart','gTileset_LavenderTown':'secondary/lavender_town','gTileset_CeladonCity':'secondary/celadon_city','gTileset_UndergroundPath':'secondary/underground_path'}
SPEC=importlib.util.spec_from_file_location('_celadon_recovery_shared',ROOT/'scripts/import-route22-region.py');SHARED=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(SHARED)
canonical,json_bytes=SHARED.canonical,SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source=SHARED.Source
import_map,connections_and_warps,npc_inventory=SHARED.import_map,SHARED.connections_and_warps,SHARED.npc_inventory

def protect_project_paths(*targets):
    for protected in [ROOT/'scripts/import-config',ROOT/'scripts',ROOT/'src',ROOT/'tests',ROOT/'.git']:
        for target in targets:
            path=pathlib.Path(target).resolve()
            if path.is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(path):raise ValueError('Protected project path overlap')

def materialize(raw,reference_repo):
    raw=pathlib.Path(raw);protect_project_paths(raw)
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind=='raw' and (protected.name=='celadon-approach-region' or (protected.name=='pokefirered' and raw.resolve()==(ROOT/'content/raw/pokefirered/037335f4').resolve())):continue
            if raw.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(raw.resolve()):raise ValueError('Existing profile overlap')
    SHARED.materialize(raw,reference_repo,source_package='celadon-approach-region')

def read_lock(path=None):
    target=path or ROOT/'scripts/import-config/celadon-approach-region.json';require_safe_target(target.parent,target)
    data=target.read_bytes();lock=json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != PROFILE) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in MAPS]) or (not lock.get('files'))):raise ValueError('Invalid source identity or scope')
    seen=set()
    for entry in lock['files']:
        p=pathlib.PurePosixPath(entry['path'])
        if p.is_absolute() or '..' in p.parts or str(p)!=entry['path'] or entry['path'] in seen:raise ValueError('Unsafe or duplicate source path')
        seen.add(entry['path'])
        if (not entry['roles']):raise ValueError('Malformed source entry')
    return lock
for key in ('ROOT','COMMIT','TREE','PROFILE','MAPS','TILESETS','read_lock'):setattr(SHARED,key,globals()[key])

def script_inventory(src,worlds):
    blocks,aliases,preambles,local_macros={},{},{},[]
    script_paths=[p for p,e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc','.s'))]
    for path in sorted(script_paths):
        current=None;macro=None;preambles[path]=[]
        for number,line in enumerate(src.text(path).splitlines(),1):
            definition=re.fullmatch(r'\s*\.macro\s+(\w+)(.*)',line)
            if definition:
                if macro is not None:raise ValueError('Nested regional macro')
                macro=dict(name=definition[1],arguments=definition[2].strip(),lines=[],sourceRef=src.ref(path,definition[1],number));local_macros.append(macro);current=None;continue
            if macro is not None:
                if line.strip()=='.endm':macro=None
                else:macro['lines'].append(dict(line=number,text=line))
                continue
            label=re.fullmatch(r'(\w+)::?',line.split('@')[0].strip())
            if label:
                name=label[1]
                if name in blocks:raise ValueError('Duplicate script label: '+name)
                current=dict(label=name,sourceRef=src.ref(path,name,number),lines=[]);blocks[name]=current
            elif current is not None:current['lines'].append(dict(line=number,text=line))
            else:preambles[path].append(dict(line=number,text=line))
            alias=re.fullmatch(r'\s*\.(?:equ|set)\s+(\w+),\s*(\w+)\s*',line)
            if alias:aliases[alias[1]]=alias[2]
    selected_map_prefixes=tuple(f'data/maps/{name}/' for name in SERVICE_MAPS)
    roots={label for label,block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events','coord_events','bg_events'):roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0','0x0','NULL'))
    roots.update(['Std_MsgboxNPC','Std_MsgboxDefault','Std_MsgboxSign','Std_MsgboxYesNo','Std_ReceivedItem','Std_PutItemAway'])
    roots.update(['EventScript_TryDoNormalTrainerBattle','EventScript_DoTrainerBattleFromApproach','EventScript_CutTree','EventScript_FldEffCut'])
    required={'LavenderTown_PokemonCenter_1F_OnTransition','CeladonCity_PokemonCenter_1F_OnTransition','Route8_WestEntrance_EventScript_GuardTrigger','Route7_EastEntrance_EventScript_GuardTrigger','UndergroundPath_EastWestTunnel_OnTransition','CeladonCity_OnTransition'}
    if not required<=roots:raise ValueError('Critical regional entry labels missing')
    if roots-blocks.keys():raise ValueError('Missing map event labels: '+', '.join(sorted(roots-blocks.keys())))
    selected,pending=set(),sorted(roots);references,unresolved=[],[]
    commands,specials,movements=collections.defaultdict(list),collections.defaultdict(list),collections.defaultdict(list)
    while pending:
        label=pending.pop()
        if label in selected:continue
        selected.add(label);block=blocks[label]
        for line in block['lines']:
            text=line['text'].split('@')[0].strip()
            if not text or text.startswith(('.string','.include','#')):continue
            op=text.split()[0];location=dict(label=label,path=block['sourceRef']['path'],line=line['line']);commands[op].append(location)
            if op in ('special','specialvar'):specials[text.split(',')[-1].strip().split()[-1]].append(location)
            if label.endswith('Movement') or 'Movement_' in label:movements[op].append(location)
            operands=text.split(None,1)[1] if ' ' in text else '';target=None
            if op in ('call','goto','goto_if_questlog','message'):target=operands.strip()
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):target=operands.split(',')[-1].strip()
            elif op=='case':target=re.split(r'[,\s]+',operands.strip())[-1]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):target=operands.split(',')[0].strip()
            if op in ('trainerbattle_single','trainerbattle_double','trainerbattle_rematch','trainerbattle_rematch_double','trainerbattle_no_intro'):
                for extra in operands.split(',')[1:4] if op!='trainerbattle_no_intro' else operands.split(',')[1:2]:
                    if extra.strip() not in ('0','NULL') and aliases.get(extra.strip(),extra.strip()) not in blocks:unresolved.append(dict(symbol=extra.strip(),**location))
            if target and target not in ('0','0x0','NULL') and aliases.get(target,target) not in blocks:unresolved.append(dict(symbol=target,**location))
            for token in re.findall(r'\b[A-Za-z_]\w*\b',text):
                token=aliases.get(token,token)
                if token in blocks:
                    references.append(dict(source=label,target=token,line=line['line']))
                    if token not in selected:pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Movement_|Text_|.*_(?:EventScript|Movement|Text)_|CableClub_On).*',token):unresolved.append(dict(symbol=token,**location))
    if unresolved:raise ValueError('Unresolved source labels: '+', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1,profile=PROFILE,executable=False,roots=sorted(roots),blocks=[blocks[x] for x in sorted(selected)],aliases=aliases,filePreambles=preambles,localMacros=local_macros,references=references,unresolvedLabelReferences=unresolved,
        commands=[dict(opcode=k,occurrences=v,runtimeStatus='sourceOnly') for k,v in sorted(commands.items())],
        specials=[dict(symbol=k,occurrences=v,runtimeStatus='requiresNamedHandlerAudit') for k,v in sorted(specials.items())],
        movements=[dict(opcode=k,occurrences=v,runtimeStatus='requiresMovementAudit') for k,v in sorted(movements.items())])

def constants_inventory(src,worlds,scripts):
    result=SHARED.constants_inventory(src,worlds,scripts)
    for name,target in scripts['aliases'].items():
        if target.isdecimal():
            matches=[p for p in scripts['filePreambles'] if re.search(r'\.(?:equ|set)\s+'+name+r',\s*'+target+r'\b',src.text(p))]
            if len(matches)!=1:raise ValueError('Ambiguous regional alias: '+name)
            result['values'][name]=dict(value=int(target),sourceRef=src.ref(matches[0],name))
    for index,location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'],1):result['values'][location['id']]=dict(value=index,sourceRef=src.ref('src/data/heal_locations.json',location['id']))
    return result

def declaration(src,path,symbol,pattern=None):
    pattern=pattern or r'^(?:static )?[^\n;]+\b'+re.escape(symbol)+r'\([^;\n]*\)\n\{.*?^\}'
    matches=list(re.finditer(pattern,src.text(path),re.M|re.S))
    if len(matches)!=1:raise ValueError('Missing/ambiguous declaration: '+symbol)
    match=matches[0];return dict(symbol=symbol,sourceText=match[0],sourceRef=src.ref(path,symbol,src.text(path)[:match.start()].count('\n')+1))
def reused_maps(src):
    directory = ROOT/'content/generated/rocktunnel-region'
    require_safe_target(directory,directory)
    manifest_path=directory/'manifest.json';require_safe_target(directory,manifest_path)
    manifest=manifest_path.read_bytes()
    entries={e['path']:e for e in json_bytes(manifest)['outputs']}
    outputs=[e for e in entries.values() if e['path'].startswith('lavender-town/')]
    for e in outputs:
        p=directory/e['path'];require_safe_target(directory,p)
    world=json_bytes((directory/'lavender-town/runtime-map.json').read_bytes())
    if world['events']!=src.json('data/maps/LavenderTown/map.json'):raise ValueError('Reused Lavender source events differ')
    return [dict(sourceName='LavenderTown',stableId='map.lavender-town',sourceMapSymbol='MAP_LAVENDER_TOWN',profile='firered-rocktunnel-region-import-v1',
        directory='rocktunnel-region/lavender-town',
        outputs=outputs)]


def dependency_inventory(src, worlds, scripts, constants, connections, warps):
    rows={b['label']:b for b in scripts['blocks']}
    def block(label,*tokens):
        b=rows[label];text='\n'.join(r['text'] for r in b['lines'])
        if any(t not in text for t in tokens):raise ValueError('Regional script contract differs: '+label)
        return dict(label=label,sourceRef=b['sourceRef'],sourceText=text)
    events=[w['events'] for w in worlds]
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
                trainer=re.search(r'(trainerbattle_single|trainerbattle_double) (TRAINER_\w+)',text)
                if trainer:
                    symbol=trainer[2];decl=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\]\s*=\s*\{.*?^    \},')
                    party=re.search(r'\.party = (\w+)\((\w+)\)',decl['sourceText'])
                    if party is None:raise ValueError('Trainer party format missing')
                    trainers.append(dict(map=m['id'],localId=i,trainer=symbol,trainerId=constants['values'][symbol]['value'],actor=e,script=block(label),declaration=decl,
                        party=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{.*?^\};'),partyFormat=party[1],
                        battleCommand=trainer[1],availability='regionalFootRoute',runtimeStatus='requiresDoubleBattleIntegration' if trainer[1]=='trainerbattle_double' else 'sourceOnly'))
        for i,e in enumerate(m['bg_events']):
            if e.get('type')=='hidden_item':pickups.append(dict(map=m['id'],eventIndex=i,kind='hidden',item=e['item'],quantity=e['quantity'],declaration=e,sourceRef=src.ref('data/maps/'+m['name']+'/map.json',m['id'])))
    mart_block=block('LavenderTown_Mart_Items')
    stock=re.findall(r'\.2byte (ITEM_\w+)',mart_block['sourceText'])
    if stock[-1]!='ITEM_NONE' or len(stock)!=10:raise ValueError('Lavender Mart stock differs')
    stock=stock[:-1]
    items=[dict(symbol=s,declaration=next(i for i in src.json('src/data/items.json')['items'] if i['itemId']==s),sourceRef=src.ref('src/data/items.json',s)) for s in sorted({p['item'] for p in pickups}|set(stock))]
    table=src.json('src/data/wild_encounters.json')
    encounter_rows=[e for g in table['wild_encounter_groups'] for e in g['encounters'] if e['map'] in ('MAP_ROUTE8','MAP_ROUTE7','MAP_CELADON_CITY') and e['base_label'].endswith('_FireRed')]
    centers=[]
    for name,heal_id in [('LavenderTown','HEAL_LOCATION_LAVENDER_TOWN'),('CeladonCity','HEAL_LOCATION_CELADON_CITY')]:
        heal=next(h for h in src.json('src/data/heal_locations.json')['heal_locations'] if h['id']==heal_id)
        centers.append(dict(map=name+'_PokemonCenter_1F',transition=block(name+'_PokemonCenter_1F_OnTransition','setrespawn '+heal_id),
            nurse=block(name+'_PokemonCenter_1F_EventScript_Nurse','call EventScript_PkmnCenterNurse'),healLocation=heal,sourceRef=src.ref('src/data/heal_locations.json',heal_id)))
    guards=[]
    for name,direction in [('Route8_WestEntrance','right'),('Route7_EastEntrance','left')]:
        m=src.json('data/maps/'+name+'/map.json')
        guards.append(dict(map=m['id'],triggers=m['coord_events'],requiredFlag='FLAG_GOT_TEA',sceneVariable='VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES',
            script=block(name+'_EventScript_GuardTrigger','goto_if_set FLAG_GOT_TEA',name+'_Text_ThirstyOnGuardDuty'),
            deniedMovement=block(name+'_Movement_BlockPlayerEntry','walk_'+direction),teaBranch=block(name+'_EventScript_GiveTea','removeitem ITEM_TEA'),
            opened=block(name+'_EventScript_GuardDrinkTea','setvar VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES, 1'),runtimeStatus='denyUntilTeaAndSaffronIntegration'))
    fuji=src.json('data/maps/LavenderTown_VolunteerPokemonHouse/map.json')
    fuji_index,fuji_actor=next((i,e) for i,e in enumerate(fuji['object_events'],1) if e['script']=='LavenderTown_VolunteerPokemonHouse_EventScript_MrFuji')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,trainers=trainers,pickups=pickups,items=items,cutTrees=cut,
        cutScript=block('EventScript_CutTree'),reusedLavenderEvents=src.json('data/maps/LavenderTown/map.json'),
        encounters=dict(sourceRef=src.ref('src/data/wild_encounters.json','gWildMonHeaders'),gameVersion='FIRERED',fields=table['wild_encounter_groups'][0]['fields'],tables=encounter_rows),
        centers=centers,mart=dict(map='MAP_LAVENDER_TOWN_MART',clerk=block('LavenderTown_Mart_EventScript_Clerk'),stock=stock,stockSource=mart_block),guards=guards,
        volunteerHouse=dict(mrFujiLocalId=fuji_index,mrFujiActor=fuji_actor,rescueFlag='FLAG_RESCUED_MR_FUJI',giftFlag='FLAG_GOT_POKE_FLUTE',
            flute=block('LavenderTown_VolunteerPokemonHouse_EventScript_MrFuji'),runtimeStatus='preRescueDialogueOnly'),
        celadonArrival=block('CeladonCity_OnTransition','setworldmapflag FLAG_WORLD_MAP_CELADON_CITY'),
        undergroundVisit=block('UndergroundPath_EastWestTunnel_OnTransition','setworldmapflag FLAG_WORLD_MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL'),
        externalConnections=[c for c in connections if c['runtimeStatus']=='externalBoundary'],externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        blockedMaps=sorted(({w['destinationMap'] for w in warps if w['runtimeStatus'].startswith('external')}|{c['map'] for c in connections if c['runtimeStatus']=='externalBoundary'})-{'MAP_LAVENDER_TOWN'}),sourceAudio=sorted({m['music'] for m in events}),
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False,
            notes=['Source closure does not enable rematches, double battles, multiplayer services or Move Tutors.',
                   'Volunteer house keeps pre-rescue dialogue and hidden Mr Fuji; the Poke Flute gift needs Tower rescue integration.',
                   'Name Rater, Pokemon Tower, Saffron, Route16 and Celadon non-Center interiors remain external boundaries.']))

def regional_warps(src,worlds):
    normal=[]
    for world in worlds:
        copy=dict(world,events=dict(world['events']));copy['events']['warp_events']=[e for e in world['events']['warp_events'] if e['dest_map'] not in ('MAP_UNION_ROOM','MAP_TRADE_CENTER')];normal.append(copy)
    connections,warps=connections_and_warps(src,normal)
    for world in worlds:
        for index,event in enumerate(world['events']['warp_events']):
            if event['dest_map'] not in ('MAP_UNION_ROOM','MAP_TRADE_CENTER'):continue
            name={'MAP_UNION_ROOM':'UnionRoom','MAP_TRADE_CENTER':'TradeCenter'}[event['dest_map']]
            path=f'data/maps/{name}/map.json';target=src.json(path);destination_index=int(event['dest_warp_id']);destination=target['warp_events'][destination_index]
            if destination['dest_map']!='MAP_DYNAMIC' or destination['dest_warp_id']!='WARP_ID_DYNAMIC':raise ValueError('Expected original multiplayer dynamic return')
            cell=world['cells'][event['y']*world['width']+event['x']]
            warps.append(dict(sourceMap=world['events']['id'],sourceWarpId=index,source=event,destinationMap=target['id'],destinationWarpId=destination_index,destinationX=destination['x'],destinationY=destination['y'],
                behaviorName=cell['behaviorName'],behaviorId=cell['behaviorId'],cellCollision=cell['collision'],cellElevation=cell['elevation'],runtimeStatus='externalDynamicBoundary',ordinaryWarpCandidate=False,dynamicReturn=destination,destinationSourceRef=src.ref(path,target['id'])))
    return connections,sorted(warps,key=lambda w:(w['sourceMap'],w['sourceWarpId']))


def import_catalog(raw,out,lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out);protect_project_paths(raw,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            for role,target in [('raw',raw),('generated',out)]:
                if kind==role and protected.name=='celadon-approach-region':continue
                if target.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(target.resolve()):raise ValueError('Existing profile overlap')
    src=Source(raw,lock_path);reused=reused_maps(src);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.celadon-approach-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        overlap=dict(events=src.json('data/maps/LavenderTown/map.json'))
        connections,warps=regional_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds+[overlap],scripts)
        npcs=npc_inventory(src,worlds,stage/'npc');dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],reusedMaps=reused,connections=connections,warps=warps,inputFiles=src.lock['files'])
        for filename,doc in package_documents(out.parent, constants, {'celadon-approach-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies}).items():(stage/filename).write_bytes(canonical(doc).encode())

        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=len(worlds),reusedMapCount=len(reused),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/celadon-approach-region');parser.add_argument('--reference-root',type=pathlib.Path,required=True);parser.add_argument('--materialize',action='store_true');args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root)
    import_catalog(args.raw,args.out)
