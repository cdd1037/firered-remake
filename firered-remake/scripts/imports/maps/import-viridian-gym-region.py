#!/usr/bin/env python3
"""Import one pinned Viridian Gym map and its source gate. No upstream code or ROM build."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
import argparse, ast, collections,  importlib.util, json, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PROFILE = 'firered-viridian-gym-region-v1'
PACKAGE = 'maps/regions/viridian-gym'
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_ViridianGym':'secondary/viridian_gym'}
def canonical(v): return (json.dumps(v, sort_keys=True, separators=(',', ':'))+'\n').encode()
def need(v, m):
    if not v: raise ValueError(m)
def module(path, name):
    spec=importlib.util.spec_from_file_location(name,path); m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink in output: '+str(path))

def script_inventory(src, reader):
    # Index common labels read-only; retain only files in the explicit gym/gate closure.
    paths=sorted(p for p in src.tree if p.endswith('.inc') and (p.startswith(('data/scripts/','data/text/','data/maps/ViridianCity/','data/maps/ViridianCity_Gym/'))))
    paths+=['data/event_scripts.s']
    blocks={};aliases={}
    for path in paths:
        text=reader.git(src.reference,'cat-file','blob',src.tree[path][2]).decode();current=None;macro=False
        for line_no,line in enumerate(text.splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'):macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1];need(current not in blocks,'Duplicate source label '+current)
                blocks[current]=dict(label=current,path=path,line=line_no,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=line_no,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if b['path'].startswith('data/maps/ViridianCity_Gym/')}
    roots.update(['ViridianCity_MapScripts','ViridianCity_EventScript_GymDoorLocked','ViridianCity_EventScript_GymDoor','ViridianCity_EventScript_OldMan',
        'EventScript_SetViridianGymTrainers','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo','Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    selected=set();pending=sorted(roots);references=[];unresolved=[];opcodes=collections.Counter()
    while pending:
        label=pending.pop()
        if label in selected:continue
        need(label in blocks,'Missing source label '+label);selected.add(label)
        for row in blocks[label]['lines']:
            code=row['text'].split('@')[0].strip()
            if not code or code.startswith(('.','#')):continue
            op=code.split()[0];opcodes[op]+=1
            args=code.split(None,1)[1] if len(code.split(None,1))==2 else '';targets=[]
            if op in ('call','goto','goto_if_questlog','message'):targets=[args]
            elif op in ('map_script','map_script_2','applymovement','case') or op.startswith(('goto_if_','call_if_')):targets=[args.split(',')[-1].strip()]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):targets=[args.split(',')[0].strip()]
            elif op.startswith('trainerbattle_'):targets=[v.strip() for v in args.split(',')[1:]]
            for target in targets:
                if target not in ('0','0x0','NULL','NO_MUSIC') and aliases.get(target,target) not in blocks:unresolved.append(dict(label=label,line=row['line'],target=target))
            for token in re.findall(r'\b[A-Za-z_]\w*\b',code):
                token=aliases.get(token,token)
                if token in blocks:
                    references.append(dict(source=label,target=token,line=row['line']))
                    if token not in selected:pending.append(token)
    need(not unresolved,'Unresolved script references: '+repr(unresolved))
    result=[]
    for label in sorted(selected):
        b=blocks[label];src.read(b['path'],'gym-and-city-gate-script-closure')
        result.append(dict(label=label,sourceRef=src.ref(b['path'],label,b['line']),lines=b['lines']))
    return dict(schemaVersion=1,executable=False,roots=sorted(roots),blocks=result,
        aliases={k:v for k,v in aliases.items() if v in selected},references=references,unresolvedLabelReferences=unresolved,
        opcodeCounts=dict(sorted(opcodes.items())))

def constants_inventory(src, shared, world, scripts):
    constants=shared.constants_inventory(src,[world],scripts);definitions={}
    for path in sorted(src.data):
        if not path.startswith('include/'):continue
        for m in re.finditer(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)',src.text(path),re.M):
            definitions[m[1]]=(m[2].split('//')[0].strip(),path,src.text(path).count('\n',0,m.start())+1)
    for m in re.finditer(r'^\s*([A-Z][A-Z0-9_]+)\s*=\s*([0-9]+)\s*$',src.text('asm/macros/event.inc'),re.M):
        definitions[m[1]]=(m[2],'asm/macros/event.inc',src.text('asm/macros/event.inc').count('\n',0,m.start())+1)
    pending=set()
    def resolve(name):
        if name in constants['values']:return constants['values'][name]['value']
        need(name in definitions and name not in pending,'Unresolved/cyclic constant '+name);pending.add(name)
        expression,path,line=definitions[name]
        def number(n):
            if isinstance(n,ast.Constant) and type(n.value) is int:return n.value
            if isinstance(n,ast.Name):return resolve(n.id)
            if isinstance(n,ast.BinOp):
                x,y=number(n.left),number(n.right)
                if isinstance(n.op,ast.Add):return x+y
                if isinstance(n.op,ast.Sub):return x-y
                if isinstance(n.op,ast.LShift) and 0<=y<32:return x<<y
                if isinstance(n.op,ast.BitOr):return x|y
            raise ValueError('Unsupported source expression '+name+': '+expression)
        value=number(ast.parse(expression,mode='eval').body);need(0<=value<=0x7fffffff,'Constant out of range')
        constants['values'][name]=dict(value=value,sourceRef=src.ref(path,name,line));pending.remove(name);return value
    operands=set(re.findall(r'\b[A-Z][A-Z0-9_]+\b','\n'.join(r['text'].split('@')[0] for b in scripts['blocks'] for r in b['lines'] if not r['text'].strip().startswith('.string'))))
    for symbol in sorted(operands & definitions.keys()):resolve(symbol)
    constants['nonNumericOperandTokens']=sorted(operands-constants['values'].keys())
    need(set(constants['nonNumericOperandTokens'])<= {'STR_VAR_1','STR_VAR_2','STR_VAR_3','NO_MUSIC'},'Unexpected unresolved numeric operand: '+repr(constants['nonNumericOperandTokens']))
    return constants

def contracts(src, reader, world, scripts, constants):
    blocks={b['label']:b for b in scripts['blocks']};events=world['events'];city=src.json('data/maps/ViridianCity/map.json')
    def block(label):return blocks[label]
    def lines(label):return [r['text'].split('@')[0].strip() for r in block(label)['lines'] if r['text'].split('@')[0].strip()]
    unlock=lines('ViridianCity_EventScript_TryUnlockGym');badges=[re.fullmatch(r'goto_if_unset (FLAG_BADGE\d\d_GET), EventScript_Return',l)[1] for l in unlock if l.startswith('goto_if_unset ')]
    need(badges==['FLAG_BADGE%02d_GET'%i for i in range(2,8)],'Original badge prerequisite differs')
    need(unlock[-2:]==['setvar VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR, 1','return'],'Original latched door differs')
    door=next(e for e in city['coord_events'] if e['script']=='ViridianCity_EventScript_GymDoorLocked')
    hidden=[e for e in events['bg_events'] if e['type']=='hidden_item'];need(len(hidden)==1 and hidden[0]['underfoot'] is True,'Underfoot source reward differs')
    occurrences=[]
    for b in scripts['blocks']:
        if b['sourceRef']['path']!='data/maps/ViridianCity_Gym/scripts.inc':continue
        for row in b['lines']:
            text=row['text'].strip()
            if not text.startswith('trainerbattle_single '):continue
            args=[a.strip() for a in text.split(None,1)[1].split(',')]
            event=next((i,e) for i,e in enumerate(events['object_events'],1) if e['script']==b['label'])
            occurrences.append(dict(trainer=args[0],sourceTrainerId=constants['values'][args[0]]['value'],script=b['label'],localId=event[0],event=event[1],
                opcode='trainerbattle_single',arguments=args,mode=1 if len(args)>3 else 0,noMusic=args[-1]=='NO_MUSIC',
                callback=args[3] if len(args)>3 else None,sourceRef=src.ref(b['sourceRef']['path'],b['label'],row['line']),admitted=False))
    need(len(occurrences)==9 and sum(x['mode']==1 for x in occurrences)==1,'Gym trainer occurrence scope differs')
    auto=[l.split()[1] for l in lines('EventScript_SetViridianGymTrainers') if l.startswith('settrainerflag ')]
    need(set(auto)=={r['trainer'] for r in occurrences if not r['noMusic']},'Skipped trainer flags differ')
    items={i['itemId']:i for i in src.json('src/data/items.json')['items']}
    item_refs=[dict(item=i,itemId=constants['values'][i]['value'],record=items[i],sourceRef=src.ref('src/data/items.json',i)) for i in ['ITEM_TM26','ITEM_MACHO_BRACE']]
    return dict(schemaVersion=1,
        entrance=dict(cityMap='MAP_VIRIDIAN_CITY',cityWarpIndex=2,cityWarp=city['warp_events'][2],gymWarpIndex=1,gymWarp=events['warp_events'][1],
            lockedTrigger=door,doorScene='VAR_MAP_SCENE_VIRIDIAN_CITY_GYM_DOOR',requiredBadges=badges,latched=True,
            transition=block('ViridianCity_OnTransition'),unlock=block('ViridianCity_EventScript_TryUnlockGym'),locked=block('ViridianCity_EventScript_GymDoorLocked'),jump=block('ViridianCity_Movement_JumpDownLedge')),
        trainerOccurrences=occurrences,autoDefeatedTrainers=auto,setGymTrainers=block('EventScript_SetViridianGymTrainers'),
        giovanni=dict(localId=8,event=events['object_events'][7],entry=block('ViridianCity_Gym_EventScript_Giovanni'),victory=block('ViridianCity_Gym_EventScript_DefeatedGiovanni'),
            reward=block('ViridianCity_Gym_EventScript_GiveTM26'),bagFull=block('ViridianCity_Gym_EventScript_NoRoomForTM26')),
        itemReferences=item_refs,itemPickups=[],hiddenItems=[dict(event=e,acquisition='ItemfinderUnderfootOnly',ordinaryInteractionAllowed=False,runtimeSupported=False,
            sourceRef=src.ref('data/maps/ViridianCity_Gym/map.json',e['flag']),
            behaviorSources=[src.ref('src/field_control_avatar.c','GetInteractedBackgroundEventScript'),src.ref('src/itemfinder.c','HiddenItemIsWithinRangeOfPlayer')]) for e in hidden],
        mapTopology=reader.topology(src,[world]))

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--dependency-root',type=Path,default=ROOT);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    target=a.project_root.absolute();dep=a.dependency_root.absolute();ref=a.reference_root.absolute();safe(target)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Reference/output overlap')
    reader=module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'viridian_gym_reader');reader.PRODUCT=dep;reader.PROFILE=PROFILE;reader.TILESETS=TILESETS
    src=reader.Source(ref)
    for p in ['data/maps/ViridianCity_Gym/map.json','data/maps/ViridianCity_Gym/scripts.inc','data/maps/ViridianCity_Gym/text.inc',
        'data/maps/ViridianCity/map.json','data/maps/ViridianCity/scripts.inc','data/maps/ViridianCity/text.inc',
        'src/data/tilesets/metatiles.h','src/data/items.json','src/field_control_avatar.c','src/itemfinder.c','src/metatile_behavior.c','src/field_player_avatar.c',
        'src/data/trainers.h','src/data/trainer_parties.h','asm/macros/event.inc','include/gba/defines.h']:
        src.read(p,'gym-source-contract')
    for n in ['field_weather','map_scripts','menu','battle_ai','flags','vars','items','opponents','trainers','event_objects','event_object_movement','metatile_behaviors','map_types','maps','songs','fame_checker','global','script_menu','items','pokemon']:
        path='include/constants/'+n+'.h'
        if path in src.tree:src.read(path,'script-and-event-constant')
    # Preserve both complete attribute/metatile tables and all original secondary palettes.
    for base in TILESETS.values():
        for part in ['tiles.png','metatiles.bin','metatile_attributes.bin']+[f'palettes/{i:02d}.pal' for i in range(16)]:src.read('data/tilesets/'+base+'/'+part,'source-tileset')
    world=reader.decode(src,'ViridianCity_Gym');scripts=script_inventory(src,reader)
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='viridian-gym-region-') as temp:
        stage=Path(temp);name='viridian-city-gym';shared.import_map(src,'ViridianCity_Gym','map.'+name,stage/name)
        npcs=shared.npc_inventory(src,[world],stage/'npc')
        constants=constants_inventory(src,shared,world,scripts);contract=contracts(src,reader,world,scripts,constants)
        rich=(stage/name/'runtime-map.json').read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/name/'runtime-map.json').write_bytes(compact)
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=['ViridianCity_Gym']);lockdata=canonical(lock)
        mapentry=dict(sourceName='ViridianCity_Gym',stableId='map.'+name,directory=name,sourceMapSymbol=world['events']['id'],path=name+'/runtime-map.json')
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,maps=[mapentry],tilesets=TILESETS,mapSymbols={'MAP_VIRIDIAN_CITY_GYM':'map.viridian-city-gym','MAP_VIRIDIAN_CITY':'map.viridian-city'}),
            'scripts.json':scripts,'contracts.json':contract,'npc-source.json':npcs,'geometry.json':reader.geometry_inventory([world])}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,trainerOccurrenceCount=9,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,b in writes:
            safe(path)
            if a.check:need(path.is_file() and path.read_bytes()==b,'Regeneration differs: '+str(path))
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))
if __name__=='__main__':main()
