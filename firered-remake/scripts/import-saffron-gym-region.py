#!/usr/bin/env python3
"""Import the three exact Saffron Gym, Center1F and Mart source interiors.

No upstream scripts are executed. Reviewed map/compact converters are pinned by
SHA-256. Thirty reciprocal pad endpoints, nurse, journals and Mart stock retain exact source contracts.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from pathlib import Path
import argparse, base64, collections,  importlib.util, json, os, re, struct, subprocess, sys, tempfile
sys.dont_write_bytecode=True

ROOT = Path(__file__).resolve().parents[1]
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-saffron-gym-region-import-v1'
CORE = ['SaffronCity_Gym','SaffronCity_PokemonCenter_1F','SaffronCity_Mart']
OPTIONAL = []
TILESETS = {'gTileset_Building':'primary/building','gTileset_SaffronGym':'secondary/saffron_gym','gTileset_PokemonCenter':'secondary/pokemon_center','gTileset_Mart':'secondary/mart'}

def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2)+'\n').encode()
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

def decode(src,name):
    events=src.json(f'data/maps/{name}/map.json');layout=next(x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id')==events['layout'])
    attrs=[src.units('data/tilesets/'+TILESETS[layout[k]]+'/metatile_attributes.bin','I') for k in ('primary_tileset','secondary_tileset')]
    names={int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)',src.text('include/constants/metatile_behaviors.h'),re.M)}
    blocks=src.units(layout['blockdata_filepath'],'H');border=src.units(layout['border_filepath'],'H')
    if len(blocks)!=layout['width']*layout['height']:raise ValueError('Grid size differs')
    cells=[]
    for i,b in enumerate(blocks):
        mid=b&1023;a=attrs[mid>=640][mid if mid<640 else mid-640];bh=a&511
        cells.append(dict(x=i%layout['width'],y=i//layout['width'],rawBlock=b,metatileId=mid,rawAttributes=a,collision=(b>>10)&3,elevation=b>>12,behaviorId=bh,behaviorName=names.get(bh,'UNKNOWN'),encounterType=(a>>24)&7))
    return dict(sourceName=name,stableId='map.'+directory(name),sourceRef=src.ref(f'data/maps/{name}/map.json',events['id']),width=layout['width'],height=layout['height'],layout=layout,events=events,cells=cells,borderBlocks=border)

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
    roots.add('EventScript_SetSaffronGymTrainers')
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
                if w['sourceName']!='Saffron GymCo_Elevator' or e['dest_warp_id']!='WARP_ID_DYNAMIC':raise ValueError('Unexpected dynamic warp')
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
    p=PRODUCT/('scripts' if name == 'export-runtime-maps.py' else 'tools/import-regional-service-maps')/name
    spec=importlib.util.spec_from_file_location('_saffron-gym_'+p.stem,p);module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def contracts(src,worlds,doc):
    blocks={b['label']:b for b in doc['blocks']}
    def script(label):return blocks[label]
    gym=next(w for w in worlds if w['sourceName']=='SaffronCity_Gym')
    pads=[]
    for i,e in enumerate(gym['events']['warp_events']):
        cell=gym['cells'][e['y']*gym['width']+e['x']]
        if cell['behaviorName']!='MB_REGULAR_WARP':continue
        j=int(e['dest_warp_id']);end=gym['events']['warp_events'][j]
        if e['dest_map']!='MAP_SAFFRON_CITY_GYM' or int(end['dest_warp_id'])!=i or end['dest_map']!=e['dest_map']:raise ValueError('Nonreciprocal source gym pad')
        pads.append(dict(map=gym['stableId'],sourceMap=gym['events']['id'],warpIndex=i,x=e['x'],y=e['y'],elevation=e['elevation'],destinationMap=e['dest_map'],destinationWarp=j,behaviorId=cell['behaviorId'],sourceRef=gym['sourceRef']))
    if len(pads)!=30:raise ValueError('Expected30 source pad endpoints')
    city=src.json('data/maps/SaffronCity/map.json')
    entries=[]
    for name,index in [('SaffronCity_Gym',3),('SaffronCity_PokemonCenter_1F',6),('SaffronCity_Mart',5)]:
        w=next(w for w in worlds if w['sourceName']==name);e=city['warp_events'][index];j=int(e['dest_warp_id'])
        if e['dest_map']!=w['events']['id']:raise ValueError('Exterior entry mismatch')
        entries.append(dict(sourceMap=city['id'],sourceWarpId=index,source=e,destinationMap=w['events']['id'],destinationWarpId=j,destination=w['events']['warp_events'][j]))
    mart=script('SaffronCity_Mart_Items');stock=re.findall(r'\.2byte (ITEM_\w+)', '\n'.join(r['text'] for r in mart['lines']))
    if stock!=['ITEM_GREAT_BALL','ITEM_HYPER_POTION','ITEM_REVIVE','ITEM_FULL_HEAL','ITEM_ESCAPE_ROPE','ITEM_MAX_REPEL','ITEM_NONE']:raise ValueError('Mart stock mismatch')
    center=next(w for w in worlds if w['sourceName']=='SaffronCity_PokemonCenter_1F')
    nurse=next(dict(localId=i+1,declaration=o) for i,o in enumerate(center['events']['object_events']) if o['script']=='SaffronCity_PokemonCenter_1F_EventScript_Nurse')
    journals=[dict(localId=i+1,declaration=o) for i,o in enumerate(center['events']['object_events']) if o['graphics_id']=='0']
    heal=src.json('src/data/heal_locations.json')
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,pads=pads,entries=entries,martStock=stock[:-1],martSource=mart,
        sabrinaVictory=script('SaffronCity_Gym_EventScript_DefeatedSabrina'),gymTrainerFlags=script('EventScript_SetSaffronGymTrainers'),
        tm04Reward=script('SaffronCity_Gym_EventScript_GiveTM04'),tm04NoRoom=script('SaffronCity_Gym_EventScript_NoRoomForTM04'),
        nurse=nurse,journals=journals,centerTransition=script('SaffronCity_PokemonCenter_1F_OnTransition'),healingSource=heal,
        limitations=['Three source interiors only; Center2F, Dojo, trades and tutors remain outside admission.','Source data does not grant gameplay admission, battle execution, ownership, rewards or field-use support.'])

def main():
    global ROOT, PRODUCT
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    ROOT=a.project_root.absolute();PRODUCT=a.dependency_root.absolute();out=ROOT/'content/generated/saffron-gym-region';raw=ROOT/'content/raw/pokefirered/037335f4'
    if ROOT==a.reference_root.absolute() or ROOT.is_relative_to(a.reference_root.absolute()) or a.reference_root.absolute().is_relative_to(ROOT):raise ValueError('Reference/output overlap')
    for target in (ROOT,out,raw):
        if any(p.is_symlink() for p in (target,*target.parents)):raise ValueError('Output parent symlink denied')
    out.mkdir(parents=True,exist_ok=True);src=Source(a.reference_root);worlds=[decode(src,n) for n in CORE];doc=scripts(src,worlds);links=topology(src,worlds)
    doc['profile']=PROFILE;doc['sourcePaths']=sorted({b['sourceRef']['path'] for b in doc['blocks']})
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
        main_catalog=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,maps=catalog,tilesets=TILESETS,inputFiles=lock['files'],externalSaffronEvents=city,**links)
        docs=package_documents(out.parent, constants, {'saffron-gym-region-catalog.json':main_catalog,'scripts.json':doc,'npc-source.json':npcs,'objects.json':dict(objects=objects),'contracts.json':contract}, check=a.check)
        for filename,value in docs.items():(stage/filename).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=len(CORE),cellCount=sum(w['width']*w['height'] for w in worlds),borderCellCount=sum(len(w['borderBlocks']) for w in worlds),warpCount=sum(len(w['events']['warp_events']) for w in worlds),objectCount=len(objects),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(doc['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(raw/p,b) for p,b in sorted(src.data.items())]+[(out/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for p,b in writes:
            if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Output symlink denied')
            if a.check:
                if not p.is_file() or p.read_bytes()!=b:raise ValueError('Regeneration differs: '+str(p))
            else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputs=len(outputs),pads=len(contract['pads']),journalActors=len(contract['journals']))))
if __name__=='__main__':main()
