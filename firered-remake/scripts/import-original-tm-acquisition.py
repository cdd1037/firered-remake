#!/usr/bin/env python3
"""Import three original TM acquisition maps; retain Route12/15 and adjacent maps.

Only SHA-pinned local converters execute. Original Git blobs remain inert.
The resource does not admit its events or trainers to the running game.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'original-tm-acquisition'
PROFILE = 'firered-original-tm-acquisition-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
UTILITIES_SHA = '00c8e1105f255e0895eebc5b52a18b5d199ba36a5aedb06ea3961e861b8440c8'
MANIFEST_SHA256 = 'a9ca71a8ae872b6ac5f673f090042396e7c7931ee1c118d13a94bb37bf135884'
LOCK_SHA256 = 'c58a3f9050e87685c7bd69553d4fb5acded2a7bc8defeea2314e6d7b88d1f815'
MAPS = ['Route12_NorthEntrance_2F', 'SaffronCity_MrPsychicsHouse', 'FiveIsland_MemorialPillar']
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_GenericBuilding1':'secondary/generic_building_1',
    'gTileset_GenericBuilding2':'secondary/generic_building_2', 'gTileset_General':'primary/general',
    'gTileset_SeviiIslands45':'secondary/sevii_islands_45'}
ANCHORS = {
    'Route12': 'snorlax-region/route-12/world.json',
    'Route12_NorthEntrance_1F': 'snorlax-region/route-12-north-entrance-1f/world.json',
    'Route15': 'fuchsia-route-region/route-15/runtime-map.json',
    'SaffronCity': 'silph-region/saffron-city/runtime-map.json',
    'FiveIsland_Meadow': 'five-island-region/five-island-meadow/runtime-map.json'}
NPCS = {'OBJ_EVENT_GFX_LASS', 'OBJ_EVENT_GFX_BALDING_MAN', 'OBJ_EVENT_GFX_MAN',
    'OBJ_EVENT_GFX_ROCKER', 'OBJ_EVENT_GFX_ITEM_BALL', 'OBJ_EVENT_GFX_RED_NORMAL'}

def sha(b): return hashlib.sha256(b).hexdigest()
def canonical(v): return (json.dumps(v, sort_keys=True, indent=2)+'\n').encode()
def need(ok, message):
    if not ok: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: '+str(path))
def module(path, digest, name):
    safe(path); need(sha(path.read_bytes()) == digest, 'Reviewed local converter differs: '+str(path))
    spec = importlib.util.spec_from_file_location(name, path); obj = importlib.util.module_from_spec(spec); spec.loader.exec_module(obj); return obj

def retained_maps(src, reader, current):
    result = []
    for name, relative in ANCHORS.items():
        path = current/'content/generated'/relative; safe(path); data = path.read_bytes(); doc = json.loads(data)
        original = src.json('data/maps/'+name+'/map.json')
        need(doc['events'] == original, 'Retained map events differ: '+name)
        manifest_path = path.parent.parent/'manifest.json'; manifest = json.loads(manifest_path.read_bytes())
        row = next(e for e in manifest['outputs'] if e['path'] == '/'.join(relative.split('/')[1:]))
        need(row['sha256'] == sha(data) and row['byteLength'] == len(data), 'Retained source manifest differs: '+name)
        result.append(dict(sourceName=name, mapId='map.'+reader.directory(name),
            path='content/generated/'+relative, sha256=sha(data), byteLength=len(data),
            sourceMapSymbol=original['id'], sourceRef=src.ref('data/maps/'+name+'/map.json', original['id']),
            operation='reuse-existing-source-map', eventChanges=[], geometryChanges=[]))
    return result

def service_contracts(worlds, scripts):
    blocks = {b['label']: b for b in scripts['blocks']}
    def code(label): return '\n'.join(r['text'] for r in blocks[label]['lines'])
    result = []
    for name, item, actor, flag in [
        (MAPS[0], 'ITEM_TM27', 'Lass', 'FLAG_GOT_TM27'),
        (MAPS[1], 'ITEM_TM29', 'MrPsychic', 'FLAG_GOT_TM29_FROM_MR_PSYCHIC')]:
        world = next(w for w in worlds if w['sourceName'] == name); label = name+'_EventScript_'+actor
        rows = [(i,e) for i,e in enumerate(world['events']['object_events']) if e.get('script') == label]
        need(len(rows) == 1, 'Gift actor is ambiguous')
        text = code(label); check = text.index('checkitemspace '+item); give = text.index('giveitem_msg '); setflag = text.index('setflag '+flag)
        need(check < give < setflag and text.index('goto_if_set '+flag) < check, 'Gift capacity/duplicate order differs')
        result.append(dict(mapId=world['stableId'], entry=label, item='pret:'+item, quantity=1,
            eventGroup='object_events', eventIndex=rows[0][0], sourceEvent=rows[0][1], flag=flag,
            sourceRef=blocks[label]['sourceRef'], capacityBeforeGrant=True, flagAfterReceipt=True,
            genderDialogue=(item == 'ITEM_TM27')))
    world = worlds[2]; prefix = MAPS[2]+'_EventScript_'
    text = code(prefix+'PlaceLemonade')
    need(text.index('removeitem ITEM_LEMONADE') < text.index('giveitem ITEM_TM42') < text.index('goto_if_eq VAR_RESULT, FALSE'),
        'Memorial must spend Lemonade before the source reward/full-bag check')
    need('FLAG_NO_ROOM_FOR_TM42_AT_MEMORIAL_PILLAR' in code(prefix+'NoRoomForTM42') and
        'removeitem' not in code(prefix+'ReturnedForTM42') and 'giveitem ITEM_TM42' in code(prefix+'ReturnedForTM42'),
        'Memorial pending reward/retry differs')
    need('checkitem ITEM_LEMONADE' in code(prefix+'Memorial') and 'MSGBOX_YESNO' in code(prefix+'AskPlaceLemonade') and
        'goto_if_eq VAR_RESULT, YES' in code(prefix+'AskPlaceLemonade'), 'Memorial prerequisite/choice differs')
    result.append(dict(mapId=world['stableId'], entry=prefix+'Memorial', retryEntry=prefix+'MemorialMan',
        item='pret:ITEM_TM42', quantity=1, eventGroup='bg_events', eventIndex=0,
        sourceEvent=world['events']['bg_events'][0], sourceRef=blocks[prefix+'Memorial']['sourceRef'],
        prerequisiteItem='pret:ITEM_LEMONADE', choice='MSGBOX_YESNO', acceptedChoice='YES',
        consumeBeforeCapacity=True, pendingFlag='FLAG_NO_ROOM_FOR_TM42_AT_MEMORIAL_PILLAR',
        receivedFlag='FLAG_GOT_TM42_AT_MEMORIAL_PILLAR', retryDoesNotConsumeLemonade=True))
    return result

def service_bindings(worlds, inventory):
    blocks={b['label']:b for b in inventory['blocks']}
    aliases=inventory['aliases']
    implicit={'giveitem':'Std_ObtainItem','giveitem_msg':'Std_ReceivedItem',
        'msgreceiveditem':'Std_ReceivedItem','putitemaway':'Std_PutItemAway','finditem':'Std_FindItem'}
    message={'MSGBOX_DEFAULT':'Std_MsgboxDefault','MSGBOX_YESNO':'Std_MsgboxYesNo',
        'MSGBOX_SIGN':'Std_MsgboxSign','MSGBOX_NPC':'Std_MsgboxNPC','MSGBOX_AUTOCLOSE':'Std_MsgboxAutoclose'}
    def closure(root):
        pending=[root]; selected=set()
        while pending:
            label=aliases.get(pending[-1],pending[-1]);pending.pop()
            if label in selected:continue
            need(label in blocks, 'Missing service call target '+label);selected.add(label)
            for row in blocks[label]['lines']:
                code=row['text'].split('@')[0].strip()
                if not code:continue
                op=code.split()[0]
                if op in implicit:pending.append(implicit[op])
                if op=='msgbox':pending.append(message[code.split(',')[-1].strip()] if ',' in code else 'Std_MsgboxDefault')
                pending += [aliases.get(t,t) for t in re.findall(r'\b\w+\b',re.sub(r'"[^"\n]*"','',code))
                            if aliases.get(t,t) in blocks and aliases.get(t,t) not in selected]
        programs=[b for b in selected if '_Movement_' not in b and not any(
            r['text'].strip().startswith('.string') for r in blocks[b]['lines'])]
        return sorted(programs),sorted(selected-set(programs))
    rows=[]
    for map_name,group,entry,items in [(MAPS[0],'object_events','Lass',['ITEM_TM27']),
        (MAPS[1],'object_events','MrPsychic',['ITEM_TM29']),
        (MAPS[2],'object_events','MemorialMan',['ITEM_TM42']),
        (MAPS[2],'bg_events','Memorial',['ITEM_TM42','ITEM_LEMONADE'])]:
        world=next(w for w in worlds if w['sourceName']==map_name);label=map_name+'_EventScript_'+entry
        matches=[(i,e) for i,e in enumerate(world['events'][group]) if e.get('script')==label]
        need(len(matches)==1,'Source service event binding differs');index,event=matches[0]
        programs,other=closure(label)
        rows.append(dict(id='original-tm-acquisition:'+label,mapId=world['stableId'],scriptId=label,
            sourceLocalId=index+1 if group=='object_events' else None,
            backgroundX=event['x'] if group=='bg_events' else None,backgroundY=event['y'] if group=='bg_events' else None,
            eventGroup=group,eventIndex=index,sourceEvent=event,sourceRef=world['sourceRef'],
            programIds=programs,itemSymbols=items,otherReachableLabels=other))
    return rows

def memorial_trainers(src):
    """Project unchanged ordinary trainer record shapes, without admitting new species or battles."""
    def one(path,pattern):
        matches=list(re.finditer(pattern,src.text(path),re.M|re.S));need(len(matches)==1,'Ambiguous source declaration '+pattern);return matches[0]
    def ref(path,symbol,m):
        return dict(path=path,symbol=symbol,startLine=src.text(path).count('\n',0,m.start())+1,
            endLine=src.text(path).count('\n',0,m.end())+1,sha256=src.entries[path]['sha256'])
    def number(path,symbol):
        text=one(path,r'^#define '+re.escape(symbol)+r'\s+([^\n]+)')[1].split('//')[0].strip()
        return int(text,0) if re.fullmatch(r'(?:0x[0-9a-fA-F]+|\d+)',text) else 1 << int(re.fullmatch(r'\(1 << (\d+)\)',text)[1])
    def identity(s):return dict(value='pret:'+s)
    def encode(name):return [int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name]
    def natural(species,level):
        pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+species+r'\] = (\w+),$')[1]
        body=one('src/data/pokemon/level_up_learnsets.h',r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')[1]
        moves=[]
        for lv,move in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)',body):
            if int(lv)<=level and move not in moves:moves=(moves+[move])[-4:]
        return moves
    trainers=[];resolved=[];battles=[]
    for name in ['Milo','Chaz','Harold']:
        symbol='TRAINER_BIRD_KEEPER_'+name.upper();path='src/data/trainers.h'
        m=one(path,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},');fields=dict(re.findall(r'^        \.(\w+) = (.*),$',m[1],re.M))
        party_symbol=re.fullmatch(r'NO_ITEM_DEFAULT_MOVES\((\w+)\)',fields['party'])[1]
        pp='src/data/trainer_parties.h';party=one(pp,r'^static const struct TrainerMonNoItemDefaultMoves '+party_symbol+r'\[\] = \{\n(.*?)^\};')
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n    \},\n',party[1]))
        need(''.join(r[0] for r in rows)==party[1], 'Unexpected memorial source party shape')
        members=[];movesets=[]
        for row in rows:
            iv,level,species=int(row[1]),int(row[2]),row[3]
            np='src/data/text/species_names.h';nm=one(np,r'^    \['+species+r'\] = _\("([^"]+)"\),$')
            members.append(dict(species=identity(species),sourceSpeciesId=number('include/constants/species.h',species),
                speciesName=nm[1],encodedSpeciesName=encode(nm[1]),level=level,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[],
                source=ref(pp,party_symbol,party),speciesNameSource=ref(np,species,nm)))
            movesets.append(dict(species=identity(species),level=level,moves=natural(species,level)))
        original_name=re.fullmatch(r'_\("([^"]+)"\)',fields['trainerName'])[1]
        ai=fields['aiFlags'].split(' | ');items=[] if fields['items']=='{}' else fields['items'].strip('{}').split(', ');items+=['ITEM_NONE']*(4-len(items))
        money=one('src/battle_main.c',r'^\s*\{'+fields['trainerClass']+r', (\d+)\},$')
        trainers.append(dict(id=identity(symbol),sourceTrainerId=number('include/constants/opponents.h',symbol),
            trainerClass=fields['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),
            trainerPic=fields['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),
            sourceTrainerName=original_name,encodedTrainerName=encode(original_name),
            encounterMusicGender=sum(int(v) if v.isdigit() else number('include/constants/trainers.h',v) for v in fields['encounterMusic_gender'].split(' | ')),
            items=items,doubleBattle=fields['doubleBattle']=='TRUE',partyFlags=0,
            aiFlags=sum(number('include/constants/battle_ai.h',a) for a in ai),aiFlagSymbols=ai,baseMoney=int(money[1]),party=members,
            source=ref(path,symbol,m),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
        resolved.append(dict(trainer=symbol,party=movesets))
        label=MAPS[2]+'_EventScript_'+name;sp='data/scripts/trainers.inc';block=one(sp,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
        command=re.search(r'^\ttrainerbattle_single ([^\n]+)$',block[1],re.M);need(command is not None,'Memorial must retain source single battle')
        args=command[1].split(', ');need(len(args)==3 and args[0]==symbol,'Memorial trainer source binding differs')
        post=re.search(r'^\tmsgbox (\w+)',block[1],re.M)
        battles.append(dict(trainer=identity(symbol),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE'),
            rivalFlags=0,battleTypeFlags=8,scriptEntry=label,introText=args[1],defeatText=args[2],victoryText='',
            postBattleText=post[1],entryScript='EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',
            source=ref(sp,label,block),postBattleScript=None,musicOption=None))
    need(len(trainers)==3 and sum(len(t['party']) for t in trainers)==6 and not any(t['doubleBattle'] for t in trainers),'Original memorial trainer scope differs')
    wild=src.json('src/data/wild_encounters.json');group=next(g for g in wild['wild_encounter_groups'] if g.get('label')=='gWildMonHeaders')
    encounters=[e for e in group['encounters'] if e['map']=='MAP_FIVE_ISLAND_MEMORIAL_PILLAR' and e['base_label'].endswith('_FireRed')]
    need(len(encounters)==1,'Original Memorial wild table is missing')
    return dict(sourceCommit=COMMIT,profile=PROFILE+'-trainers',completeSourceRoster=False,
        trainers=trainers,battles=battles,resolvedMoves=resolved),dict(sourceCommit=COMMIT,profile=PROFILE+'-encounters',
        sourcePath='src/data/wild_encounters.json',sourceSha256=src.entries['src/data/wild_encounters.json']['sha256'],
        slotTables=group['fields'],encounters=encounters)

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference-root',type=Path,required=True); p.add_argument('--dependency-root',type=Path,default=ROOT)
    p.add_argument('--project-root',type=Path,default=ROOT); p.add_argument('--check',action='store_true'); a=p.parse_args()
    target, dep, ref = [Path(os.path.abspath(x)) for x in [a.project_root,a.dependency_root,a.reference_root]]
    for path in [target,dep,ref]: safe(path)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader=module(dep/'scripts/import-cinnabar-source-preparation.py',READER_SHA,'original_tm_reader')
    reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,PROFILE
    util=module(dep/'scripts/import-fossil-lab-region.py',UTILITIES_SHA,'original_tm_utilities')
    util.MAPS=MAPS+['Route12','Route15']
    src=reader.Source(ref); worlds=[reader.decode(src,n) for n in MAPS]
    anchors=retained_maps(src,reader,dep)
    source_only=[dict(sourceName=n,events=src.json('data/maps/'+n+'/map.json')) for n in ['Route12','Route15']]
    implicit=['Std_FindItem','Std_ObtainItem','Std_ReceivedItem','Std_PutItemAway']
    # Shared macro expansion references these original roots implicitly.
    implicit_events=dict(events=dict(object_events=[dict(script=s) for s in implicit],coord_events=[],bg_events=[]))
    scripts=util.script_inventory(src,reader,worlds+source_only+[implicit_events])
    scripts['implicitStandardRoots']=sorted(set(scripts['implicitStandardRoots']+implicit))
    constants=util.constants_inventory(src,scripts,worlds+source_only)
    for n in MAPS:
        for leaf in ['scripts.inc','text.inc']: src.read('data/maps/'+n+'/'+leaf,'live-map-script')
    topology=reader.topology(src,worlds)
    need(all(c['reciprocal'] for c in topology['connections']), 'Original map connection is nonreciprocal')
    # Retain existing entrance event rows as well as the newly imported exit rows.
    for anchor in anchors:
        events=src.json(anchor['sourceRef']['path'])
        for i,e in enumerate(events['warp_events']):
            matches=[w for w in worlds if w['events']['id']==e['dest_map']]
            if matches:
                end=matches[0]['events']['warp_events'][int(e['dest_warp_id'])]
                need(end['dest_map']==events['id'], 'Incoming original warp is nonreciprocal')
                topology['warps'].append(dict(sourceMap=events['id'],sourceIndex=i,source=e,destinationMap=e['dest_map'],
                    destinationIndex=int(e['dest_warp_id']),destination=end,dynamic=False,reciprocalIndex=int(end['dest_warp_id'])==i))
    trainers,encounters=memorial_trainers(src)
    contracts=dict(schemaVersion=1,resourceId=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,
        retainedMaps=anchors,mapTopology=topology,services=service_contracts(worlds,scripts),
        serviceBindings=service_bindings(worlds,scripts),
        sourceOnlyInteractions=[e['script'] for e in worlds[2]['events']['object_events'] if e['trainer_type']=='TRAINER_TYPE_NORMAL'],
        acquisitionCatalogOwner='original-machine-catalog/acquisitions.json',
        limitations=['Current-game mount and shared event executor admission are separate.',
            'Memorial Pillar trainers, encounters and non-TM pickups retain original declarations; this package does not implement their battle/catalog admission.'])
    shared=reader.load_helper('_regional_service_shared.py'); shared.PROFILE,shared.TILESETS=PROFILE,TILESETS
    converter=reader.load_helper('_reviewed_runtime_converter.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.original-tm-acquisition-',dir=target) as temporary:
        stage=Path(temporary)
        for world in worlds: shared.import_map(src,world['sourceName'],world['stableId'],stage/reader.directory(world['sourceName']))
        npcs=shared.npc_inventory(src,worlds,stage/'npc')
        need({r['graphicsId'] for r in npcs['graphics']}==NPCS, 'Original actor inventory differs')
        for row in npcs['graphics']:
            generated=stage/row['texturePath']; relative='content/generated/regional-service-maps/'+row['texturePath']; existing=dep/relative
            safe(existing); need(generated.read_bytes()==existing.read_bytes() and sha(existing.read_bytes())==row['sha256'], 'Reused source sprite differs')
            row.update(texturePath=relative,byteLength=existing.stat().st_size,reuseExisting=True); generated.unlink()
        (stage/'npc').rmdir()
        source_hash=sha(''.join(p+'\0'+e['sha256']+'\n' for p,e in sorted(src.entries.items())).encode())
        entries=[]
        for world in worlds:
            directory=reader.directory(world['sourceName']); path=stage/directory/'world.json'; rich=path.read_bytes()
            compact=converter.convert(rich,sha(rich),source_hash); util.compact_check(compact,world,src)
            data=converter.encode(compact); (stage/directory/'runtime-map.json').write_bytes(data); path.unlink()
            entries.append(dict(sourceName=world['sourceName'],stableId=world['stableId'],directory=directory,
                sourceMapSymbol=world['events']['id'],path=directory+'/runtime-map.json',sha256=sha(data),byteLength=len(data),
                sourceWorldSha256=sha(rich),sourceWorldByteLength=len(rich),fullSourceHash=source_hash))
        lock=src.lock(); lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False); lock_bytes=canonical(lock)
        docs={'catalog.json':dict(schemaVersion=1,resourceId=PROFILE,sourceCommit=COMMIT,sourceTree=TREE,
                  fullSourceHash=source_hash,maps=entries,inputFiles=lock['files'],tilesets=TILESETS),
              'scripts.json':scripts,'constants.json':constants,'contracts.json':contracts,'npc-source.json':npcs,
              'memorial-trainers.json':trainers,'memorial-encounters.json':encounters}
        for name,value in docs.items(): (stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=sha(p.read_bytes()),byteLength=p.stat().st_size)
                 for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,sourceTree=TREE,fullSourceHash=source_hash,
            lockSha256=sha(lock_bytes),readerSha256=READER_SHA,sourceUtilitiesSha256=UTILITIES_SHA,
            converterSha256=reader.HELPERS['_reviewed_runtime_converter.py'],rendererSha256=reader.HELPERS['_regional_service_shared.py'],
            inputCount=len(src.entries),mapCount=3,reusedMapCount=5,cellCount=sum(len(w['cells']) for w in worlds),
            scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_bytes=canonical(manifest); (stage/'manifest.json').write_bytes(manifest_bytes)
        if MANIFEST_SHA256: need(sha(manifest_bytes)==MANIFEST_SHA256, 'Pinned manifest differs: '+sha(manifest_bytes))
        if LOCK_SHA256: need(sha(lock_bytes)==LOCK_SHA256, 'Pinned lock differs: '+sha(lock_bytes))
        writes=[(target/'scripts/original-tm-acquisition.lock.json',lock_bytes)]
        writes += [(target/'content/raw'/PACKAGE/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if a.check: need(path.is_file() and path.read_bytes()==data, 'Regeneration differs: '+str(path))
        if not a.check:
            for path,data in writes: path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(manifestSha256=sha(manifest_bytes),manifestByteLength=len(manifest_bytes),lockSha256=sha(lock_bytes),
            lockByteLength=len(lock_bytes),fullSourceHash=source_hash,inputCount=len(src.entries),outputCount=len(outputs),
            scriptBlockCount=len(scripts['blocks']),check=a.check),indent=2))

if __name__=='__main__': main()
