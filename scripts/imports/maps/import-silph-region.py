#!/usr/bin/env python3
"""Import SaffronCity and twelve Silph floors/elevator maps.

No upstream scripts are executed. Card doors, warp pads, pickups and elevator destinations retain exact source contracts.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import decode_geometry
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
from pathlib import Path
import argparse, base64, collections,  importlib.util, json, os, re, struct, subprocess, sys, tempfile

ROOT = _PROJECT_ROOT
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-silph-region-import-v1'
CORE = ['SaffronCity']+[f'SilphCo_{i}F' for i in range(1,12)]+['SilphCo_Elevator']
OPTIONAL = []
TILESETS = {'gTileset_Building':'primary/building','gTileset_SilphCo':'secondary/silph_co','gTileset_General':'primary/general','gTileset_SaffronCity':'secondary/saffron_city'}

def canonical(doc): return (json.dumps(doc, sort_keys=True, separators=(',', ':'))+'\n').encode()
def directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())
def git(repo, *args):
    env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(repo),*args],env=env)

class LazyBlobs(dict):
    def __init__(self, owner): super().__init__(); self.owner=owner
    def __missing__(self, path): return self.owner.read(path)

class Source:
    def __init__(self, reference):
        self.reference=reference
        if git(reference, 'rev-parse', 'HEAD').decode().strip() != COMMIT: raise ValueError('Source HEAD/tree differs')
        self.tree={}
        for row in git(reference,'ls-tree','-rz',COMMIT).split(b'\0'):
            if row:
                fields,path=row.split(b'\t'); self.tree[path.decode()]=tuple(fields.decode().split())
        self.data=LazyBlobs(self);self.entries={};self.roles=collections.defaultdict(set)
    def read(self,path,role='source-dependency'):
        if path not in self.tree or self.tree[path][:2]!=('100644','blob'): raise ValueError('Not a regular source blob: '+path)
        self.roles[path].add(role)
        if path not in self.data:
            b=git(self.reference,'cat-file','blob',self.tree[path][2])
            self.data[path]=b;self.entries[path]=dict(path=path)
        return self.data[path]
    def text(self,path):return self.read(path).decode()
    def json(self,path):return json.loads(self.read(path))
    def units(self,path,code):
        b=self.read(path);size=struct.calcsize(code)
        if len(b)%size:raise ValueError('Misaligned source binary')
        return list(struct.unpack('<'+str(len(b)//size)+code,b))
    def ref(self,path,symbol,line=None):
        self.read(path)
        v=dict(kind='pret',repository='pret/pokefirered',commit=COMMIT,path=path,symbol=symbol)
        if line is not None:v['line']=line
        return v
    def lock(self):
        return dict(schemaVersion=1,repository='pret/pokefirered',commit=COMMIT,profile=PROFILE,files=[dict(**self.entries[p],roles=sorted(self.roles[p])) for p in sorted(self.entries)])

def decode(src, name):
    return decode_geometry(src, name, tilesets=TILESETS)

def scripts(src,maps):
    # Full files are indexed to discover symbols; only selected closure files become
    # dependencies. This deliberately does not equate closure membership to admission.
    blocks={};aliases={}
    paths=[p for p in src.tree if p.endswith('.inc') and (p.startswith(('data/scripts/','data/text/')) or any(p.startswith('data/maps/'+n+'/') for n in CORE+OPTIONAL))]+['data/event_scripts.s']
    scanned={}
    for p in paths:
        b=git(src.reference,'cat-file','blob',src.tree[p][2]);scanned[p]=b;current=None;macro=False
        for ln,line in enumerate(b.decode().splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'):macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1]
                if current in blocks:raise ValueError('Duplicate source label '+current)
                blocks[current]=dict(label=current,path=p,line=ln,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=ln,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if any(b['path'].startswith('data/maps/'+m+'/') for m in CORE+OPTIONAL)}
    for m in maps:
        for k in ('object_events','coord_events','bg_events'):
            roots.update(e['script'] for e in m['events'][k] if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(n for n,b in blocks.items() if b['path']=='data/scripts/silphco_doors.inc')
    roots.update(['Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo','Std_FindItem','Std_ReceivedItem','Std_PutItemAway'])
    pending=sorted(roots);selected=set()
    while pending:
        label=pending.pop()
        if label in selected:continue
        if label not in blocks:raise ValueError('Missing root '+label)
        selected.add(label)
        for row in blocks[label]['lines']:
            for tok in re.findall(r'\b[A-Za-z_]\w*\b',row['text'].split('@')[0]):
                tok=aliases.get(tok,tok)
                if tok in blocks and tok not in selected:pending.append(tok)
    result=[];opcodes=collections.Counter();specials=collections.Counter();unresolved=[]
    for label in sorted(selected):
        b=blocks[label];p=b['path'];src.read(p,'script-label-closure')
        result.append(dict(label=label,sourceRef=src.ref(p,label,b['line']),lines=b['lines']))
        for row in b['lines']:
            t=row['text'].split('@')[0].strip()
            if not t or t.startswith(('.','#')):continue
            op=t.split()[0];opcodes[op]+=1
            if op in ('special','specialvar'):specials[t.split(',')[-1].split()[-1]]+=1
            operands=t.split(None,1)[1] if ' ' in t else '';target=None
            if op in ('call','goto','goto_if_questlog','message'):target=operands.strip()
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):target=operands.split(',')[-1].strip()
            elif op=='case':target=re.split(r'[,\s]+',operands.strip())[-1]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):target=operands.split(',')[0].strip()
            if target and target not in ('0','0x0','NULL') and aliases.get(target,target) not in blocks:unresolved.append(dict(source=label,line=row['line'],target=target))
    if unresolved:raise ValueError('Unresolved source label targets: '+repr(unresolved))
    return dict(executable=False,roots=sorted(roots),blocks=result,opcodeCounts=dict(sorted(opcodes.items())),specialCounts=dict(sorted(specials.items())),aliases=aliases,unresolvedLabelReferences=unresolved)

def topology(src,worlds):
    by_symbol={w['events']['id']:w['events'] for w in worlds};warps=[];connections=[]
    def target(symbol):
        if symbol in by_symbol:return by_symbol[symbol]
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                name=p.split('/')[2]
                if re.sub(r'([a-z])([A-Z])',r'\1_\2',name).upper()==symbol.removeprefix('MAP_'):
                    by_symbol[symbol]=src.json(p);return by_symbol[symbol]
        raise ValueError('Unknown external map '+symbol)
    opposite={'up':'down','down':'up','left':'right','right':'left'}
    for w in worlds:
        for c in w['events']['connections'] or []:
            d=target(c['map']);back=[x for x in d['connections'] or [] if x['map']==w['events']['id'] and x['direction']==opposite[c['direction']]]
            connections.append(dict(sourceMap=w['events']['id'],reciprocal=bool(len(back)==1 and back[0]['offset']==-c['offset']),destinationConnections=d['connections'],**c))
        for i,e in enumerate(w['events']['warp_events']):
            if e['dest_map']=='MAP_DYNAMIC':
                if w['sourceName']!='SilphCo_Elevator' or e['dest_warp_id']!='WARP_ID_DYNAMIC':raise ValueError('Unexpected dynamic warp')
                warps.append(dict(sourceMap=w['events']['id'],sourceIndex=i,source=e,dynamic=True));continue
            d=target(e['dest_map']);j=int(e['dest_warp_id']);end=d['warp_events'][j]
            if e['dest_map']!='MAP_SILPH_CO_ELEVATOR' and end['dest_map']!=w['events']['id']:raise ValueError('Nonreciprocal static warp')
            warps.append(dict(sourceMap=w['events']['id'],sourceIndex=i,source=e,destinationMap=d['id'],destinationIndex=j,destination=end,dynamic=False))
    return dict(connections=connections,warps=warps)

def geometry_inventory(worlds):
    out=[]
    for w in worlds:
        rows=collections.defaultdict(list)
        for c in w['cells']:rows[c['behaviorName']].append([c['x'],c['y']])
        out.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],width=w['width'],height=w['height'],layout=w['layout'],events=w['events'],sourceRef=w['sourceRef'],behaviorCounts={k:len(v) for k,v in sorted(rows.items())},specialBehaviorCells={k:v for k,v in sorted(rows.items()) if k!='MB_NORMAL'},originalGridBase64=base64.b64encode(struct.pack('<'+str(len(w['cells']))+'H',*[x['rawBlock'] for x in w['cells']])).decode(),attributes=sorted({(x['metatileId'],x['rawAttributes']) for x in w['cells']})))
    return out

def trainers(worlds,script_doc):
    blocks={b['label']:b for b in script_doc['blocks']};out=[]
    for w in worlds:
        for i,o in enumerate(w['events']['object_events'],1):
            if o.get('trainer_type')!='TRAINER_TYPE_NORMAL':continue
            b=blocks[o['script']];t=next(r['text'].strip() for r in b['lines'] if r['text'].strip().startswith('trainerbattle_'))
            op,args=t.split(None,1)
            out.append(dict(sourceMap=w['events']['id'],localId=i,declaration=o,sourceRef=b['sourceRef'],opcode=op,arguments=[x.strip() for x in args.split(',')],trainer=args.split(',')[0].strip(),isSouthOfSnorlax=w['sourceName']!='Route12' or o['y']>70))
    return out

def load_helper(name):
    p=PRODUCT/('scripts/imports/maps' if name == 'export-runtime-maps.py' else 'tools/import-regional-service-maps')/name
    spec=importlib.util.spec_from_file_location('_silph_'+p.stem,p);module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def contracts(src,worlds,doc):
    blocks={b['label']:b for b in doc['blocks']}
    def body(label):return '\n'.join(r['text'] for r in blocks[label]['lines'])
    def script(label):return dict(label=label,sourceRef=blocks[label]['sourceRef'],lines=blocks[label]['lines'])
    mtpath='include/constants/metatile_labels.h';mt=src.text(mtpath)
    values={n:int(v,16) for n,v in re.findall(r'^#define (METATILE_SilphCo_\w+)\s+(0x[0-9a-fA-F]+)',mt,re.M)}
    doors=[]
    for label in sorted(blocks):
        m=re.fullmatch(r'SilphCo_(\d+F)_EventScript_Door(\d*)',label)
        if not m:continue
        close='EventScript_Close'+m[1]+'Door'+m[2];opened='EventScript_Open'+m[1]+'Door'+m[2]
        def patches(key):
            rows=re.findall(r'^\s*setmetatile (\d+), (\d+), (METATILE_\w+), (\d+)$',body(key),re.M)
            if not rows:raise ValueError('Empty door patches')
            return [dict(x=int(x),y=int(y),metatile=s,metatileId=values[s],collision=int(c)) for x,y,s,c in rows]
        doors.append(dict(map='map.silph-co-'+m[1].lower(),script=label,doorIndex=int(re.search(r'setvar VAR_TEMP_1, (\d+)',body(label))[1]),flag=re.search(r'setvar VAR_0x8004, (FLAG_\w+)',body(label))[1],closedPatches=patches(close),openPatches=patches(opened),source=script(label),closedSource=script(close),openSource=script(opened)))
    if len(doors)!=20:raise ValueError('Expected20 exact source doors')
    pads=[]
    for w in worlds:
        for i,e in enumerate(w['events']['warp_events']):
            cell=w['cells'][e['y']*w['width']+e['x']]
            if cell['behaviorName']=='MB_REGULAR_WARP':pads.append(dict(map=w['stableId'],sourceMap=w['events']['id'],warpIndex=i,x=e['x'],y=e['y'],elevation=e['elevation'],destinationMap=e['dest_map'],destinationWarp=int(e['dest_warp_id']),behaviorId=cell['behaviorId'],sourceRef=w['sourceRef']))
    if len(pads)!=34:raise ValueError('Expected34 source pad endpoints: '+str(len(pads)))
    floors=[]
    for floor in range(1,12):
        label=f'SilphCo_Elevator_EventScript_To{floor}F';b=body(label);m=re.search(r'setdynamicwarp (MAP_\w+), (\d+), (\d+), (\d+)',b)
        floors.append(dict(floor=floor,menuIndex=11-floor,sourceFloorId=int(re.search(r'setvar VAR_0x8006, (\d+)',b)[1]),destinationMap=m[1],destinationWarp=int(m[2]),x=int(m[3]),y=int(m[4]),source=script(label)))
    pickups=[]
    for w in worlds:
        for i,o in enumerate(w['events']['object_events'],1):
            if o['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            label=o['script'];item=re.search(r'finditem (ITEM_\w+)',body(label))
            if not item:raise ValueError('Missing exact source pickup')
            pickups.append(dict(map=w['stableId'],localId=i,item=item[1],flag=o['flag'],x=o['x'],y=o['y'],source=script(label)))
    return dict(schemaVersion=1,doors=doors,pads=pads,elevatorFloors=floors,pickups=pickups,hiddenItems=[dict(map=w['stableId'],declaration=e,sourceRef=w['sourceRef']) for w in worlds for e in w['events']['bg_events'] if e['type']=='hidden_item'],cardKeyFlag='FLAG_HIDE_SILPH_CO_5F_CARD_KEY',cardKeyUnlock=script('EventScript_TryUnlockDoor'),lapras=script('SilphCo_7F_EventScript_LaprasGuy'),president=script('SilphCo_11F_EventScript_President'),nurse=script('SilphCo_9F_EventScript_HealWoman'))

def main():
    global ROOT, PRODUCT
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    ROOT=a.project_root.absolute();PRODUCT=a.dependency_root.absolute();out=ROOT/'content/generated/maps/regions/silph';raw=ROOT/'content/raw/pokefirered/037335f4'
    if ROOT==a.reference_root.absolute() or ROOT.is_relative_to(a.reference_root.absolute()) or a.reference_root.absolute().is_relative_to(ROOT):raise ValueError('Reference/output overlap')
    out.mkdir(parents=True,exist_ok=True);src=Source(a.reference_root);worlds=[decode(src,n) for n in CORE];doc=scripts(src,worlds);links=topology(src,worlds)
    doc['profile']=PROFILE
    for name in ['flags','vars','event_objects','trainers','opponents','items','species','moves','songs','map_scripts','field_effects','battle','pokemon','global','menu','game_stat','battle_ai','event_bg','event_object_movement','sound','fame_checker','metatile_labels']:
        p='include/constants/'+name+'.h'
        if p in src.tree:src.read(p,'rule-reference')
    for p in ['src/field_specials.c','src/field_control_avatar.c','src/field_player_avatar.c','src/field_effect.c','src/event_object_movement.c','src/overworld.c','src/script_menu.c','asm/macros/event.inc']:src.read(p,'rule-reference')
    contract=contracts(src,worlds,doc)
    shared=load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS;converter=load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='.stage-',dir=out) as temp:
        stage=Path(temp);rendered=[]
        for n in CORE:
            d=directory(n);shared.import_map(src,n,'map.'+d,stage/d);rendered.append(d)
        npcs=shared.npc_inventory(src,[dict(events=dict(object_events=[o for o in w['events']['object_events'] if o['graphics_id']!='0'])) for w in worlds],stage/'npc');constants=shared.constants_inventory(src,worlds,doc)
        city=src.json('data/maps/SaffronCity/map.json');catalog=[]
        for n,d in zip(CORE,rendered):
            p=stage/d/'runtime-map.json';b=p.read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(b)));(stage/d/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=n,stableId='map.'+d,directory=d,sourceMapSymbol=next(w['events']['id'] for w in worlds if w['sourceName']==n),path=d+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE);lockdata=canonical(lock)
        objects=[dict(map=w['stableId'],sourceMap=w['events']['id'],localId=i+1,sourceObjectIndex=i,declaration=o,sourceRef=w['sourceRef']) for w in worlds for i,o in enumerate(w['events']['object_events'])]
        main_catalog=dict(schemaVersion=1,maps=catalog,tilesets=TILESETS,externalSaffronEvents=city,**links)
        docs=package_documents(generated_root(out), constants, {'silph-region-catalog.json':main_catalog,'scripts.json':doc,'npc-source.json':npcs,'objects.json':dict(objects=objects),'contracts.json':contract}, check=a.check)
        for filename,value in docs.items():(stage/filename).write_bytes(canonical(value))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(raw/p,b) for p,b in sorted(src.data.items())]+[(out/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Output symlink denied')
            if a.check:
                if not p.is_file() or p.read_bytes()!=b:raise ValueError('Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputs=len(outputs),doors=len(contract['doors']),pads=len(contract['pads']),pickups=len(contract['pickups']))))
if __name__=='__main__':main()
