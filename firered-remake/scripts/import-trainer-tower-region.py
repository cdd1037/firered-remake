#!/usr/bin/env python3
"""Rebuild the 11 original Trainer Tower maps and all 27 layouts from inert pinned Git blobs."""
import argparse, ast, base64, collections, hashlib, importlib.util, json, re, struct, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-trainer-tower-region-v1'
PACKAGE = 'trainer-tower-region'
READER_SHA = '13a6129f2348d43716ad1c398bb7d797b9f6a22f23eb7bed93908fb0d18c407f'
MANIFEST_SHA = '025b657cc68b7771c52593583af4ebb35d1316ef9e6754e86b3de1331dca85ef'
LOCK_SHA = 'c5a2f22dbc2cbaf77b54d686f006b95d59d713eab622a634018f9481cc0ba8f1'
MAPS = ['TrainerTower_Lobby', *[f'TrainerTower_{i}F' for i in range(1,9)], 'TrainerTower_Roof', 'TrainerTower_Elevator']
TILESETS = {'gTileset_Building':'primary/building', 'gTileset_TrainerTower':'secondary/trainer_tower', 'gTileset_SilphCo':'secondary/silph_co'}
FORMATS = [('Single',''),('Double','_DOUBLES'),('Knockout','_KNOCKOUT')]
def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value,sort_keys=True,indent=2)+'\n').encode()
def need(value,message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink path rejected: '+str(path))
def module(path,name,expected):
    need(sha(path.read_bytes())==expected,'Reviewed author dependency differs: '+str(path))
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result

def script_index(src, reader):
    paths=[p for p in src.tree if p.endswith('.inc') and
      (p.startswith(('data/scripts/','data/text/')) or p.endswith('/text.inc') or
       any(p.startswith('data/maps/'+n+'/') for n in MAPS))]+['data/event_scripts.s']
    blocks={}; aliases={}
    for p in sorted(paths):
        b=reader.git(src.reference,'cat-file','blob',src.tree[p][2]); current=None; macro=False
        for ln,line in enumerate(b.decode().splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'): macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1];need(current not in blocks,'duplicate label '+current)
                blocks[current]=dict(label=current,path=p,line=ln,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=ln,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if b['path']=='data/scripts/trainer_tower.inc' or any(b['path'].startswith(f'data/maps/{m}/') for m in MAPS)}
    for name in MAPS:
        m=src.json(f'data/maps/{name}/map.json')
        roots.update(e['script'] for k in ('object_events','coord_events','bg_events') for e in m[k]
                     if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['EventScript_ResetAllMapFlags','EventScript_DoNoIntroTrainerBattle','EventScript_TryDoNormalTrainerBattle',
                  'EventScript_TryDoDoubleTrainerBattle','EventScript_PC','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo',
                  'Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    roots.add('EventScript_HiddenItemScript')
    pending=sorted(roots); selected=set()
    while pending:
        label=pending.pop();label=aliases.get(label,label)
        if label in selected:continue
        need(label in blocks,'missing label '+label);selected.add(label)
        for row in blocks[label]['lines']:
            for tok in re.findall(r'\b[A-Za-z_]\w*\b',row['text'].split('@')[0]):
                tok=aliases.get(tok,tok)
                if tok in blocks and tok not in selected:pending.append(tok)
    result=[]; opcodes=collections.Counter(); specials=collections.Counter();unresolved=[]
    for label in sorted(selected):
        b=blocks[label];src.read(b['path'],'script-label-closure')
        result.append(dict(label=label,sourceRef=src.ref(b['path'],label,b['line']),lines=b['lines']))
        for r in b['lines']:
            t=r['text'].split('@')[0].strip()
            if not t or t.startswith(('.','#')):continue
            op=t.split()[0];opcodes[op]+=1
            if op in ('special','specialvar'):specials[t.split(',')[-1].split()[-1]]+=1
            operands=t.split(None,1)[1] if len(t.split(None,1))==2 else '';target=None
            if op in ('call','goto','goto_if_questlog','message'):target=operands.strip()
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):target=operands.split(',')[-1].strip()
            elif op=='case':target=re.split(r'[,\s]+',operands.strip())[-1]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):target=operands.split(',')[0].strip()
            if target and target not in ('0','0x0','NULL','gStringVar4') and aliases.get(target,target) not in blocks:unresolved.append(dict(source=label,line=r['line'],target=target))
    need(not unresolved,'Unresolved labels '+repr(unresolved))
    return dict(executable=False,roots=sorted(roots),blocks=result,aliases=aliases,
      opcodeCounts=dict(sorted(opcodes.items())),specialCounts=dict(sorted(specials.items())),
      unresolvedLabelReferences=unresolved,dynamicTextBuffers=['gStringVar4'],
      warning='Full label dependency inventory includes guarded postgame and multiplayer branches, not runtime admission.')



def constants(src,scripts,worlds):
    definitions,values={},{}
    headers=['flags','vars','event_objects','event_object_movement','map_scripts','species','moves','items','pokemon','battle','battle_setup','field_effects','songs','sound','global','opponents','trainers','menu','game_stat','metatile_behaviors','metatile_labels','field_weather','trainer_tower','cable_club','fame_checker','weather','help_system','union_room','daycare','field_tasks','battle_ai']
    for path in ['include/constants/'+n+'.h' for n in headers]+['include/gba/defines.h']:
        for line,text in enumerate(src.text(path).splitlines(),1):
            m=re.match(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)',text)
            if m:definitions[m[1]]=(m[2].split('//')[0].strip(),path,line)
    for path in ['asm/macros/event.inc','data/event_scripts.s']:
        for line,text in enumerate(src.text(path).splitlines(),1):
            m=re.match(r'\s*([A-Z][A-Z0-9_]+)\s*=\s*(\d+)\s*$',text) or re.match(r'\s*\.set\s+(\w+),\s*(\d+)\s*$',text)
            if m:definitions[m[1]]=(m[2],path,line)
    for i,layout in enumerate(src.json('data/layouts/layouts.json')['layouts'],1):
        if 'id' in layout:values[layout['id']]=dict(value=i,sourceRef=src.ref('data/layouts/layouts.json',layout['id']))
    need('int i = 1;' in src.text('tools/mapjson/mapjson.cpp'),'Source layout enum numbering differs')
    for w in worlds:
        for i,event in enumerate(w['events']['object_events'],1):
            if 'local_id' in event:values[event['local_id']]=dict(value=i,sourceRef=w['sourceRef'])
    operands='\n'.join(re.sub(r'"[^"\n]*"','',r['text'].split('@')[0]) for b in scripts['blocks'] for r in b['lines'])
    tokens=set(re.findall(r'\b[A-Z][A-Za-z0-9_x]+\b',operands+json.dumps([w['events'] for w in worlds])))
    tokens.update(n for n in definitions if n.startswith(('CHALLENGE_','TRAINER_TOWER_','TTPRIZE_','METATILE_TrainerTower_','VAR_OBJ_GFX_ID_','FACILITY_CLASS_','TRAINER_CLASS_','TRAINER_ENCOUNTER_MUSIC_','MUS_ENCOUNTER_','OBJ_EVENT_GFX_')))
    active=set()
    def resolve(name):
        if name in values:return values[name]['value']
        need(name in definitions and name not in active,'Missing/cyclic constant '+name)
        active.add(name);expr,path,line=definitions[name]
        def number(n):
            if isinstance(n,ast.Constant) and type(n.value) is int:return n.value
            if isinstance(n,ast.Name):return resolve(n.id)
            if isinstance(n,ast.UnaryOp) and isinstance(n.op,ast.USub):return -number(n.operand)
            if isinstance(n,ast.BinOp):
                a,b=number(n.left),number(n.right)
                if isinstance(n.op,ast.Add):return a+b
                if isinstance(n.op,ast.Sub):return a-b
                if isinstance(n.op,ast.Mult):return a*b
                if isinstance(n.op,ast.BitOr):return a|b
                if isinstance(n.op,ast.LShift) and 0<=b<32:return a<<b
            raise ValueError('Unsupported source constant '+name+': '+expr)
        value=number(ast.parse(expr,mode='eval').body);values[name]=dict(value=value,sourceRef=src.ref(path,name,line));active.remove(name);return value
    for token in sorted(tokens & definitions.keys()):resolve(token)
    return dict(schemaVersion=1,values=values,nonNumericTokens=sorted(tokens-values.keys()))

class LayoutSource:
    """Only the renderer's layout lookup changes; the original map bytes and event header never change."""
    def __init__(self,src,name,layout):self.src,self.name,self.layout=src,name,layout
    def __getattr__(self,key):return getattr(self.src,key)
    def json(self,path):
        value=self.src.json(path)
        if path==f'data/maps/{self.name}/map.json':return dict(value,layout=self.layout['id'])
        return value

def compact(world,source_hash,src):
    layout=world['layout'];blocks=[];tables={};sources={};behaviors={}
    for field,path in [('cells',layout['blockdata_filepath']),('borderCells',layout['border_filepath'])]:
        data=struct.pack('<'+str(len(world[field]))+'H',*[c['rawBlock'] for c in world[field]])
        need(data==src.read(path),'Original layout cell mismatch '+path);blocks.append(base64.b64encode(data).decode())
        for cell in world[field]:
            r=cell['sourceRef'];sources[r['path']]=dict(path=r['path'],sha256=r['sha256'])
            tables[cell['metatileId']]=(cell['rawAttributes'],r['path']);behaviors[cell['behaviorId']]=cell['behaviorName']
    paths=sorted(sources);scripts=[]
    for leaf in ['scripts.inc','text.inc']:
        p=world['sourceRef']['path'].removesuffix('map.json')+leaf
        if p in src.data:scripts.append(dict(path=p,sha256=sha(src.data[p]),text=src.text(p)))
    return dict(schemaVersion=1,converterVersion='firered-runtime-map-v1',sourceCommit=COMMIT,fullSourceHash=source_hash,sourceWorldSha256=sha(canonical(world)),technicalSpawn=None,
        **{k:world[k] for k in ['stableId','sourceRef','width','height','tileSize','borderWidth','borderHeight','texturePath','artifacts','events']},
        blocks=blocks[0],borderBlocks=blocks[1],attributeSources=[sources[p] for p in paths],
        metatiles=[[k,a,paths.index(p)] for k,(a,p) in sorted(tables.items())],behaviors=[dict(id=k,name=v) for k,v in sorted(behaviors.items())],scripts=scripts)

def function(src,symbol):
    path='src/trainer_tower.c';text=src.text(path)
    m=re.search(r'^static void '+symbol+r'\([^;\n]*\)\n\{',text,re.M);need(m,'Missing tower function '+symbol)
    end=m.end();depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return dict(sourceRef=src.ref(path,symbol,text.count('\n',0,m.start())+1),text=text[m.start():end])

def build(target,dep,ref,check):
    reader=module(dep/'scripts/import-cinnabar-source-preparation.py','tower_reader',READER_SHA)
    reader.PRODUCT,reader.TILESETS,reader.PROFILE=dep,TILESETS,PROFILE
    src=reader.Source(ref);worlds=[reader.decode(src,n) for n in MAPS]
    scripts=script_index(src,reader);scripts['sourcePaths']=sorted({b['sourceRef']['path'] for b in scripts['blocks']})
    for p in ['asm/macros/trainer_tower.inc','src/trainer_tower.c','src/trainer_tower_sets.c','include/trainer_tower.h','data/maps/map_groups.json']:
        src.read(p,'tower-resource-contract')
    const=constants(src,scripts,worlds)
    tower=src.text('src/trainer_tower.c');layout_table=re.search(r'sFloorLayouts\[[^;]+?= \{(.*?)\n\};',tower,re.S)[1]
    rows=[re.findall(r'LAYOUT_\w+',r) for r in re.findall(r'\{([^{}]+)\}',layout_table)]
    need(rows==[[f'LAYOUT_TRAINER_TOWER_{i}F'+suffix for _,suffix in FORMATS] for i in range(1,9)],'Original layout selection table differs')
    layout_defs={l['id']:l for l in src.json('data/layouts/layouts.json')['layouts'] if 'id' in l}
    singles=re.search(r'sSingleBattleTrainerInfo\[\] = \{(.*?)\n\};',tower,re.S)[1]
    doubles=re.search(r'sDoubleBattleTrainerInfo\[\] = \{(.*?)\n\};',tower,re.S)[1]
    single_rows=[dict(graphicsId=g,facilityClass=c,gender=s) for g,c,s in re.findall(r'\{(OBJ_EVENT_GFX_\w+),\s*(FACILITY_CLASS_\w+),\s*(\w+)\}',singles)]
    double_rows=[dict(graphicsId1=a,graphicsId2=b,facilityClass=c,gender1=d,gender2=e) for a,b,c,d,e in re.findall(r'\{(OBJ_EVENT_GFX_\w+),\s*(OBJ_EVENT_GFX_\w+),\s*(FACILITY_CLASS_\w+),\s*(\w+),\s*(\w+)\}',doubles)]
    dynamic_graphics=set(re.findall(r'OBJ_EVENT_GFX_\w+',singles+doubles))
    lut=re.search(r'sTrainerEncounterMusicLUT\[105\] = \{(.*?)\n\};',tower,re.S)[1]
    music_lookup=[dict(facilityClass=c,comparisonValue=const['values'][c]['value'],musicId=m,musicIndex=const['values'][m]['value']) for c,m in re.findall(r'\{(FACILITY_CLASS_\w+),\s*(TRAINER_ENCOUNTER_MUSIC_\w+)\}',lut)]
    song_table=re.search(r'sTrainerTowerEncounterMusic\[\] = \{(.*?)\n\};',tower,re.S)[1]
    music_songs=[dict(musicId=m,musicIndex=const['values'][m]['value'],song=s,sourceSongId=const['values'][s]['value']) for m,s in re.findall(r'\[(TRAINER_ENCOUNTER_MUSIC_\w+)\]\s*=\s*(MUS_\w+)',song_table)]
    need(len(music_lookup)==105 and len(music_songs)==14,'Original encounter music table scope differs')
    exterior=src.json('data/maps/SevenIsland_TrainerTower/map.json')
    warps=[]
    allmaps={w['events']['id']:w['events'] for w in worlds}|{exterior['id']:exterior}
    for w in worlds:
        for i,e in enumerate(w['events']['warp_events']):
            row=dict(sourceMap=w['events']['id'],sourceIndex=i,source=e,dynamic=e['dest_map']=='MAP_DYNAMIC')
            if not row['dynamic']:
                dest=allmaps[e['dest_map']];j=int(e['dest_warp_id']);end=dest['warp_events'][j]
                row.update(destination=end,destinationMap=dest['id'],destinationIndex=j,reciprocalMap=end['dest_map']==w['events']['id'],reciprocalIndex=end['dest_map']==w['events']['id'] and end['dest_warp_id']==str(i))
            warps.append(row)
    boundary=[dict(sourceIndex=i,event=e) for i,e in enumerate(exterior['warp_events']) if e['dest_map']=='MAP_TRAINER_TOWER_LOBBY']
    need(len(boundary)==1,'Original outdoor entry scope differs')
    contracts=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,sourceMapCount=11,layoutCount=27,
        floorFormats=[dict(name=n,value=i,sourceSymbol='CHALLENGE_TYPE_'+n.upper()) for i,(n,_) in enumerate(FORMATS)],
        floorIndexSource='Original map header layout index minus LAYOUT_TRAINER_TOWER_1F; never the selected dynamic layout index.',
        mixed=dict(courseValue=3,hasOwnLayout=False,selection='Each floor uses CURR_FLOOR.challengeType, never the Mixed course index.'),
        layoutSelection=dict(rows=rows,sourceRef=src.ref('src/trainer_tower.c','sFloorLayouts'),function=function(src,'InitTrainerTowerFloor')),
        dynamicGraphics=dict(single=single_rows,double=double_rows,default='OBJ_EVENT_GFX_YOUNGSTER',function=function(src,'SetTrainerTowerNPCGraphics')),
        encounterMusic=dict(lookup=music_lookup,songs=music_songs,defaultMusicIndex=0,comparison='Compare the ordered facilityClass numeric column to the converted trainer class numeric value, as the original C does.',function=function(src,'PlayTrainerTowerEncounterMusic')),
        outdoorBoundary=dict(sourceRef=src.ref('data/maps/SevenIsland_TrainerTower/map.json',exterior['id']),entryWarps=boundary,unchanged=True),
        warps=warps,callbacks=[dict(map=w['events']['id'],root=w['sourceName']+'_MapScripts',sourceRef=src.ref('data/maps/'+w['sourceName']+'/scripts.inc',w['sourceName']+'_MapScripts')) for w in worlds],
        limitations=['Source resource preparation only; no current-game composition or command admission.', 'Elevator dynamic return is preserved; consumers must provide its original dynamic warp state.'])
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE,shared.TILESETS=PROFILE,TILESETS
    with tempfile.TemporaryDirectory(prefix='tower-region-') as temp:
        stage=Path(temp);rendered=[]
        for w in worlds:
            name=w['sourceName'];base=w['events']['layout'];formats=FORMATS if re.fullmatch(r'TrainerTower_[1-8]F',name) else [('Common','')]
            for fmt,suffix in formats:
                layout=layout_defs[base+suffix];relative=reader.directory(name)+'/'+fmt.lower()
                out=stage/relative;rw=shared.import_map(LayoutSource(src,name,layout),name,w['stableId'],out)
                rw['events']=w['events'];(out/'world.json').unlink()
                rendered.append((w,fmt,relative,rw))
        static_events=[e for w in worlds for e in w['events']['object_events'] if not e['graphics_id'].startswith('OBJ_EVENT_GFX_VAR_')]
        npc_worlds=[dict(events=dict(object_events=static_events+[dict(graphics_id=g) for g in sorted(dynamic_graphics)]))]
        npcs=shared.npc_inventory(src,npc_worlds,stage/'npc');npcs['dynamicGraphics']=contracts['dynamicGraphics']
        source_hash=sha(''.join(p+'\0'+e['sha256']+'\n' for p,e in sorted(src.entries.items())).encode());catalog=[]
        for w,fmt,relative,rw in rendered:
            doc=compact(rw,source_hash,src);data=canonical(doc);(stage/relative/'runtime-map.json').write_bytes(data)
            catalog.append(dict(sourceName=w['sourceName'],sourceMapSymbol=w['events']['id'],stableId=w['stableId'],format=fmt,layoutId=rw['layout']['id'],directory=relative,path=relative+'/runtime-map.json',sha256=sha(data),byteLength=len(data),sourceWorldSha256=doc['sourceWorldSha256'],sourceWorldByteLength=len(canonical(rw))))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lb=canonical(lock)
        docs={'catalog.json':dict(schemaVersion=1,profile=PROFILE,fullSourceHash=source_hash,maps=catalog,inputFiles=lock['files'],tilesets=TILESETS),
          'scripts.json':scripts,'constants.json':const,'contracts.json':contracts,'npc-source.json':npcs}
        for p,doc in docs.items():(stage/p).write_bytes(canonical(doc))
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=sha(p.read_bytes()),byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,sourceTree=reader.TREE,fullSourceHash=source_hash,lockSha256=sha(lb),inputCount=len(src.entries),outputCount=len(outputs),mapCount=11,layoutCount=27,objectCount=sum(len(w['events']['object_events']) for w in worlds),warpCount=len(warps),coordEventCount=sum(len(w['events']['coord_events']) for w in worlds),bgEventCount=sum(len(w['events']['bg_events']) for w in worlds),cellCount=sum(len(rw['cells']) for _,_,_,rw in rendered),borderCellCount=sum(len(rw['borderCells']) for _,_,_,rw in rendered),scriptBlockCount=len(scripts['blocks']),npcGraphicsCount=len(npcs['graphics']),authorDependencies=[dict(path='scripts/import-cinnabar-source-preparation.py',sha256=READER_SHA),dict(path='tools/import-regional-service-maps/_regional_service_shared.py',sha256=reader.HELPERS['_regional_service_shared.py'])],outputs=outputs)
        mb=canonical(manifest);(stage/'manifest.json').write_bytes(mb)
        if MANIFEST_SHA:need(sha(mb)==MANIFEST_SHA,'Pinned manifest differs')
        if LOCK_SHA:need(sha(lb)==LOCK_SHA,'Pinned source lock differs')
        writes=[(target/'scripts/trainer-tower-region.lock.json',lb),(target/'scripts/trainer-tower-resources.contract.json',canonical(contracts))]+[(target/'content/raw'/PACKAGE/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            safe(p)
            if check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k not in ['outputs','authorDependencies']}|dict(manifestSha256=sha(mb),manifestByteLength=len(mb),lockByteLength=len(lb),check=check),indent=2))

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',required=True,type=Path);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--dependency-root',type=Path,default=ROOT);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    for p in [a.reference_root,a.project_root,a.dependency_root]:safe(p.absolute())
    ref,target,dep=[p.resolve() for p in [a.reference_root,a.project_root,a.dependency_root]]
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap');build(target,dep,ref,a.check)
if __name__=='__main__':main()
