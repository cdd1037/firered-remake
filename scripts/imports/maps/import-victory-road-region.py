#!/usr/bin/env python3
"""Import pinned Route23, Victory Road and Indigo Plateau exterior source evidence. No upstream code or ROM build."""
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
PROFILE = 'firered-victory-road-region-v1'
PACKAGE = 'maps/regions/victory-road'
TILESETS = {'gTileset_General':'primary/general','gTileset_Cave':'secondary/cave','gTileset_IndigoPlateau':'secondary/indigo_plateau'}
MAPS = ['Route23','VictoryRoad_1F','VictoryRoad_2F','VictoryRoad_3F','IndigoPlateau_Exterior']
ANCHORS = ['Route22','Route22_NorthEntrance','IndigoPlateau_PokemonCenter_1F']
def canonical(v): return (json.dumps(v, sort_keys=True, separators=(',', ':'))+'\n').encode()
def need(v, m):
    if not v: raise ValueError(m)
def module(path, name):
    spec=importlib.util.spec_from_file_location(name,path); m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink in output: '+str(path))

def script_inventory(src, reader):
    # Index common labels read-only; retain only files in the explicit gym/gate closure.
    paths=sorted(p for p in src.tree if p.endswith('.inc') and ((p.startswith(('data/scripts/','data/text/')) or p.endswith('/text.inc') or any(p.startswith('data/maps/'+m+'/') for m in MAPS+['Route22_NorthEntrance']))))
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
    roots={n for n,b in blocks.items() if any(b['path'].startswith('data/maps/'+m+'/') for m in MAPS+['Route22_NorthEntrance'])}
    for name in MAPS:
        for kind in ['object_events','coord_events','bg_events']:
            roots.update(e['script'] for e in src.json('data/maps/'+name+'/map.json')[kind] if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['EventScript_UseSurf','EventScript_FldEffStrength','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo','Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
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
        b=blocks[label];src.read(b['path'],'league-approach-script-closure')
        result.append(dict(label=label,sourceRef=src.ref(b['path'],label,b['line']),lines=b['lines']))
    return dict(schemaVersion=1,executable=False,roots=sorted(roots),blocks=result,
        aliases={k:v for k,v in aliases.items() if v in selected},references=references,unresolvedLabelReferences=unresolved,
        opcodeCounts=dict(sorted(opcodes.items())))

def constants_inventory(src, shared, worlds, scripts):
    constants=shared.constants_inventory(src,worlds,scripts);definitions={}
    for path in sorted(src.data):
        if not (path.startswith('include/') or path=='src/data/tilesets/metatiles.h'):continue
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
    for symbol in sorted(set(re.findall(r'\bMETATILE_\w+\b','\n'.join(r['text'] for b in scripts['blocks'] for r in b['lines'])))):resolve(symbol)
    constants['nonNumericOperandTokens']=sorted(operands-constants['values'].keys())
    need(set(constants['nonNumericOperandTokens'])<= {'STR_VAR_1','STR_VAR_2','STR_VAR_3','NO_MUSIC'},'Unexpected unresolved numeric operand: '+repr(constants['nonNumericOperandTokens']))
    return constants

def contracts(src,reader,worlds,scripts,constants):
    blocks={b['label']:b for b in scripts['blocks']};by_name={w['sourceName']:w for w in worlds}
    def lines(label):return [r['text'].split('@')[0].strip() for r in blocks[label]['lines'] if r['text'].split('@')[0].strip()]
    gate=src.json('data/maps/Route22_NorthEntrance/map.json');route=by_name['Route23']['events']
    badges=['Boulder','Cascade','Thunder','Rainbow','Soul','Marsh','Volcano','Earth'];gates=[]
    for i,badge in enumerate(badges,1):
        events=gate if i==1 else route;actor=events['object_events'][0 if i==1 else i-2]
        prefix='Route22_NorthEntrance' if i==1 else 'Route23';script=prefix+'_EventScript_'+badge+'BadgeGuardTrigger'
        triggers=[e for e in events['coord_events'] if e['script']==script]
        check='Route23_EventScript_Check'+badge+'BadgeTrigger'
        need(all(e['var']=='VAR_MAP_SCENE_ROUTE23' and int(e['var_value'])==i-1 for e in triggers),'Badge gate order differs')
        need(lines(check)[0].startswith('goto_if_set FLAG_BADGE%02d_GET, '%i),'Badge check differs')
        need('setvar VAR_TEMP_1, '+str(i) in lines(script),'Badge index differs')
        gates.append(dict(order=i,badge=badge,flag='FLAG_BADGE%02d_GET'%i,map=events['id'],localId=1 if i==1 else i-1,event=actor,triggers=triggers,trigger=blocks[script],check=blocks[check]))
    switches=[];boulders=[];pickups=[];hidden=[];occurrences=[]
    for w in worlds:
        events=w['events'];name=w['sourceName'];mapref=src.ref('data/maps/'+name+'/map.json',events['id'])
        for i,e in enumerate(events['object_events'],1):
            if e['graphics_id']=='OBJ_EVENT_GFX_PUSHABLE_BOULDER':boulders.append(dict(map=events['id'],localId=i,event=e,sourceRef=mapref))
            if e['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL':
                cmds=lines(e['script']);m=re.fullmatch(r'finditem (ITEM_\w+)(?:, (\d+))?',cmds[0]);need(m is not None,'Unknown pickup '+e['script'])
                pickups.append(dict(map=events['id'],localId=i,event=e,item=m[1],quantity=int(m[2] or 1),script=blocks[e['script']],runtimeAdmitted=False))
            b=blocks.get(e.get('script'));rows=[] if b is None else [r for r in b['lines'] if r['text'].strip().startswith('trainerbattle_')]
            for row in rows:
                op,args=row['text'].strip().split(None,1);args=[a.strip() for a in args.split(',')]
                occurrences.append(dict(map=events['id'],localId=i,event=e,trainer=args[0],sourceTrainerId=constants['values'][args[0]]['value'],script=b['label'],opcode=op,arguments=args,sourceRef=src.ref(b['sourceRef']['path'],b['label'],row['line']),runtimeAdmitted=False))
        hidden.extend(dict(map=events['id'],event=e,sourceRef=mapref,runtimeAdmitted=False) for e in events['bg_events'] if e['type']=='hidden_item')
        for e in events['coord_events']:
            if int(e['var_value'])!=99:continue
            cell=w['cells'][e['y']*w['width']+e['x']];need(cell['behaviorName']=='MB_STRENGTH_BUTTON','Switch behavior differs')
            code=lines(e['script']);patches=[]
            for line in code:
                m=re.fullmatch(r'setmetatile (\d+), (\d+), (METATILE_\w+), ([01])',line)
                if m:patches.append(dict(x=int(m[1]),y=int(m[2]),metatile=m[3],metatileId=constants['values'][m[3]]['value'],collision=int(m[4])))
            copies=[l.split()[1] for l in code if l.startswith('copyobjectxytoperm ')]
            need('setvar '+e['var']+', 100' in code and len(patches)==2,'Switch completion differs')
            switches.append(dict(map=events['id'],event=e,behavior='MB_STRENGTH_BUTTON',dispatch='BoulderPushCompletionDirectCoordScript',playerTriggerValue=99,pressedValue=100,changes=patches,copyObjectIds=copies,script=blocks[e['script']],runtimeAdmitted=False))
    need(len(switches)==4 and len(boulders)==10 and len(pickups)==8 and len(hidden)==10,'Original field scope differs')
    need(len(occurrences)==13 and len({x['trainer'] for x in occurrences})==12,'Trainer scope differs')
    reset=lines('Route23_OnTransition')
    expected=['clearflag FLAG_HIDE_VICTORY_ROAD_3F_BOULDER','setflag FLAG_HIDE_VICTORY_ROAD_2F_BOULDER','setvar VAR_MAP_SCENE_VICTORY_ROAD_1F, 0','setvar VAR_MAP_SCENE_VICTORY_ROAD_2F_BOULDER1, 0','setvar VAR_MAP_SCENE_VICTORY_ROAD_2F_BOULDER2, 0','setvar VAR_MAP_SCENE_VICTORY_ROAD_3F, 0','end']
    need(reset==expected,'Route23 puzzle reset differs')
    falling=next(b for b in boulders if b['map']=='MAP_VICTORY_ROAD_3F' and b['localId']==8)
    need(falling['event']['trainer_type']=='FLAG_HIDE_VICTORY_ROAD_2F_BOULDER','Falling boulder reveal flag differs')
    hole=next(c for c in by_name['VictoryRoad_3F']['cells'] if c['x']==34 and c['y']==18);need(hole['behaviorName']=='MB_FALL_WARP','Boulder fall tile differs')
    item_names=sorted({p['item'] for p in pickups}|{p['event']['item'] for p in hidden});items={x['itemId']:x for x in src.json('src/data/items.json')['items']}
    return dict(schemaVersion=1,badgeGates=gates,trainerOccurrences=occurrences,boulders=boulders,switches=switches,
        route23Reset=blocks['Route23_OnTransition'],route23ResetCommands=expected,itemPickups=pickups,hiddenItems=hidden,
        itemReferences=[dict(item=n,record=items[n],sourceRef=src.ref('src/data/items.json',n)) for n in item_names],
        mapTopology=reader.topology(src,worlds),
        surf=dict(badge='FLAG_BADGE05_GET',move='MOVE_SURF',sourceRef=src.ref('data/scripts/field_moves.inc','EventScript_UseSurf'),requiredWaterGeometryPreserved=True),
        strength=dict(badge='FLAG_BADGE04_GET',move='MOVE_STRENGTH',activationFlag='FLAG_SYS_USE_STRENGTH',clearedOnMapEntry=True,
            sources=[src.ref('data/scripts/field_moves.inc','EventScript_StrengthBoulder'),src.ref('src/event_data.c','ClearTempFieldEventData')]),
        persistence=dict(templateCopyScope='Current saved-map object-event templates; not a permanent cross-map actor ledger',
            mapEntry='LoadMapFromWarp and LoadMapFromCameraTransition reload original templates, clear Strength, and run transition scripts',
            floorScene='Scene100 remains across cave floors; OnLoad inserts rock barriers only when scene !=100',
            route23Entry='Resets all four puzzle scenes and restores 3F falling boulder / hides its 2F counterpart',
            saveResume='Must preserve source saved current-map templates and active actors; do not treat Continue as a fresh warp entry',
            runtimeCertified=False,sources=[src.ref('src/overworld.c','LoadObjEventTemplatesFromHeader'),src.ref('src/overworld.c','LoadMapFromWarp'),src.ref('src/overworld.c','LoadMapFromCameraTransition'),src.ref('src/overworld.c','LoadSaveblockObjEventScripts'),src.ref('src/scrcmd.c','ScrCmd_copyobjectxytoperm'),src.ref('src/event_object_movement.c','TryOverrideObjectEventTemplateCoords')]),
        fallingBoulder=dict(source=falling,hole=dict(x=34,y=18,behavior='MB_FALL_WARP',warpIndex=4),destination=next(b for b in boulders if b['map']=='MAP_VICTORY_ROAD_2F' and b['localId']==12),
            removalFlag='FLAG_HIDE_VICTORY_ROAD_3F_BOULDER',revealFlag='FLAG_HIDE_VICTORY_ROAD_2F_BOULDER',
            trainerTypeIsRevealFlag=True,runtimeCertified=False,sources=[src.ref('src/field_control_avatar.c','HandleBoulderFallThroughHole'),src.ref('src/event_object_movement.c','GetBoulderRevealFlagByLocalIdAndMap'),src.ref('src/field_player_avatar.c','DoBoulderFinish')]),
        externalBoundaries=[dict(map='MAP_INDIGO_PLATEAU_EXTERIOR',warpIndex=0,destination='MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F',runtimeAdmitted=False,reason='League lobby, battles and Hall of Fame remain outside this candidate')],
        excludedEntryStates=[dict(map='MAP_INDIGO_PLATEAU_EXTERIOR',variable='VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR',value=1,reason='Credits source retained verbatim, never admitted here')],
        tutor=dict(map='MAP_VICTORY_ROAD_2F',localId=13,script=blocks['VictoryRoad_2F_EventScript_DoubleEdgeTutor'],runtimeAdmitted=False))

def declarations(src,dep,contract):
    # Reuse the existing authenticated canonical learnsets only to resolve required
    # default party moves. This creates dependency evidence, never an owned catalog.
    canonical_path=dep/'content/generated/data/pokemon/canonical/catalog.json';raw=canonical_path.read_bytes()
    canonical_doc=json.loads(raw);species={s['id']['value'][5:]:s for s in canonical_doc['species']};trainers=[]
    def match(pattern,path):
        ms=list(re.finditer(pattern,src.text(path),re.M|re.S));need(len(ms)==1,'Declaration ambiguous: '+pattern);return ms[0]
    for trainer in sorted({x['trainer'] for x in contract['trainerOccurrences']}):
        path='src/data/trainers.h';m=match(r'^    \['+trainer+r'\] = \{\n(.*?)^    \},',path)
        fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));pm=re.fullmatch(r'(NO_ITEM|ITEM)_(DEFAULT|CUSTOM)_MOVES\((\w+)\)',fields['party']);need(pm is not None,'Unsupported trainer format')
        item,custom,party=pm.groups();pp='src/data/trainer_parties.h';shape=('NoItem' if item=='NO_ITEM' else 'Item')+('Default' if custom=='DEFAULT' else 'Custom')+'Moves'
        p=match(r'^static const struct TrainerMon'+shape+' '+party+r'\[\] = \{\n(.*?)^\};',pp);members=[]
        for row in re.findall(r'    \{\n(.*?)    \},\n',p[1],re.S):
            f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',row,re.M));natural=[]
            for mv in species[f['species']]['levelUpMoves']:
                symbol=mv['move']['value'][5:]
                if mv['level']<=int(f['lvl']) and symbol not in natural:natural=(natural+[symbol])[-4:]
            moves=[x.strip() for x in f['moves'].strip('{}').split(',')] if 'moves' in f else natural
            members.append(dict(species=f['species'],level=int(f['lvl']),iv=int(f['iv']),heldItem=f.get('heldItem','ITEM_NONE'),resolvedMoves=moves,customMoves='moves' in f,rawFields=f))
        need(len(members)>0,'Empty trainer party')
        trainers.append(dict(trainer=trainer,fields=fields,party=members,battleAdmission=False,sourceRef=src.ref(path,trainer,src.text(path).count('\n',0,m.start())+1),partySource=src.ref(pp,party,src.text(pp).count('\n',0,p.start())+1)))
    encounters=[];slots=[];symbols={src.json('data/maps/'+n+'/map.json')['id'] for n in MAPS}
    for group in src.json('src/data/wild_encounters.json')['wild_encounter_groups']:
        selected=[x for x in group['encounters'] if x['map'] in symbols and x['base_label'].endswith('_FireRed')]
        if selected:encounters.extend(selected);slots.append({k:v for k,v in group.items() if k!='encounters'})
    need(len(encounters)==4,'Encounter map scope differs')
    wild=sorted({m['species'] for e in encounters for t in e.values() if isinstance(t,dict) and 'mons' in t for m in t['mons']})
    opponents=sorted({m['species'] for t in trainers for m in t['party']});moves=sorted({mv for t in trainers for m in t['party'] for mv in m['resolvedMoves']})
    return dict(schemaVersion=1,runtimeAdmitted=False,canonicalDependency=dict(path='content/generated/data/pokemon/canonical/catalog.json'),trainers=trainers,encounters=encounters,slotTables=slots,wildSpecies=wild,trainerSpecies=opponents,trainerMoves=moves,ownedCatalogModified=False,sourceRef=src.ref('src/data/wild_encounters.json','gWildMonHeaders'))

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    target=a.project_root.absolute();dep=a.dependency_root.absolute();ref=a.reference_root.absolute();safe(target)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Reference/output overlap')
    reader=module(dep / 'scripts/imports/source/import-cinnabar-source-preparation.py', 'victory_road_reader');reader.PRODUCT=dep;reader.PROFILE=PROFILE;reader.TILESETS=TILESETS
    src=reader.Source(ref)
    for name in MAPS+['Route22_NorthEntrance']:
        for part in ['map.json','scripts.inc','text.inc']:
            p='data/maps/'+name+'/'+part
            if p in src.tree:src.read(p,'league-approach-map')
    for name in ANCHORS:src.read('data/maps/'+name+'/map.json','reused-or-boundary-anchor')
    for p in ['src/data/tilesets/metatiles.h','src/data/items.json','src/data/trainers.h','src/data/trainer_parties.h','src/data/wild_encounters.json',
        'src/field_control_avatar.c','src/field_player_avatar.c','src/event_object_movement.c','src/overworld.c','src/event_data.c','src/scrcmd.c','src/metatile_behavior.c','src/itemfinder.c','src/renewable_hidden_items.c','asm/macros/event.inc','include/gba/defines.h']:
        if p in src.tree:src.read(p,'source-mechanism-or-dependency')
    for n in ['field_weather','map_scripts','menu','battle_ai','flags','vars','items','opponents','trainers','event_objects','event_object_movement','metatile_behaviors','metatile_labels','map_types','maps','songs','fame_checker','global','script_menu','pokemon','moves','field_effects','move_tutors','party_menu','battle','region_map_sections']:
        path='include/constants/'+n+'.h'
        if path in src.tree:src.read(path,'script-and-event-constant')
    for base in TILESETS.values():
        for part in ['tiles.png','metatiles.bin','metatile_attributes.bin']+[f'palettes/{i:02d}.pal' for i in range(16)]:src.read('data/tilesets/'+base+'/'+part,'source-tileset')
    worlds=[reader.decode(src,n) for n in MAPS];scripts=script_inventory(src,reader)
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS;converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='victory-road-region-',dir=target) as temp:
        stage=Path(temp)
        for w in worlds:shared.import_map(src,w['sourceName'],w['stableId'],stage/reader.directory(w['sourceName']))
        npcs=shared.npc_inventory(src,worlds,stage/'npc');constants=constants_inventory(src,shared,worlds,scripts);contract=contracts(src,reader,worlds,scripts,constants);deps=declarations(src,dep,contract)
        entries=[]
        for w in worlds:
            name=reader.directory(w['sourceName']);rich=(stage/name/'runtime-map.json').read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/name/'runtime-map.json').write_bytes(compact)
            entries.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=name,sourceMapSymbol=w['events']['id'],path=name+'/runtime-map.json',reused=w['sourceName']=='Route23'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=MAPS);lockdata=canonical(lock)
        map_symbols={w['events']['id']:w['stableId'] for w in worlds}
        for name in ANCHORS:map_symbols[src.json('data/maps/'+name+'/map.json')['id']]='map.'+reader.directory(name)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,maps=entries,tilesets=TILESETS,mapSymbols=map_symbols),
            'scripts.json':scripts,'contracts.json':contract,'npc-source.json':npcs,'geometry.json':reader.geometry_inventory(worlds)}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,newMapCount=4,trainerOccurrenceCount=13,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,b in writes:
            safe(path)
            if a.check:need(path.is_file() and path.read_bytes()==b,'Regeneration differs: '+str(path))
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))
if __name__=='__main__':main()
