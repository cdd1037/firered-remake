#!/usr/bin/env python3
"""Import six authenticated southern-route maps and preserve Route12 source reuse.

No upstream scripts are executed. Reviewed map/compact converters are pinned by
SHA-256. Optional services are inventoried without creating extra runtime maps.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
from pathlib import Path
import argparse, base64, collections,  importlib.util, json, os, re, struct, subprocess, sys, tempfile

ROOT = Path(__file__).resolve().parents[1]
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PRODUCT_COMMIT = 'e85553866e96a975957464cd6efd3b327278e6ab'
PROFILE = 'firered-fuchsia-route-region-import-v1'
CORE = ['Route12', 'Route13', 'Route14', 'Route15', 'Route15_WestEntrance_1F', 'FuchsiaCity', 'FuchsiaCity_PokemonCenter_1F']
OPTIONAL = ['Route12_FishingHouse', 'Route15_WestEntrance_2F']
TILESETS = {'gTileset_General':'primary/general', 'gTileset_LavenderTown':'secondary/lavender_town', 'gTileset_FuchsiaCity':'secondary/fuchsia_city', 'gTileset_Building':'primary/building', 'gTileset_GenericBuilding2':'secondary/generic_building_2', 'gTileset_PokemonCenter':'secondary/pokemon_center', 'gTileset_GenericBuilding1':'secondary/generic_building_1'}

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
    roots.update(['EventScript_AfterWhiteOutHeal','EventScript_TryDoNormalTrainerBattle','EventScript_DoTrainerBattleFromApproach','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo'])
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
    by_symbol={w['events']['id']:w for w in worlds};warps=[];connections=[]
    def target(symbol):
        if symbol in by_symbol:return by_symbol[symbol]['events']
        for p in src.tree:
            if p.startswith('data/maps/') and p.endswith('/map.json'):
                # Names are generated deterministically by mapjson; do not guess a URL.
                name=p.split('/')[2]
                if re.sub(r'([a-z])([A-Z])',r'\1_\2',name).upper()==symbol.removeprefix('MAP_'):
                    return src.json(p)
        raise ValueError('Unknown external map '+symbol)
    opposite={'up':'down','down':'up','left':'right','right':'left'}
    for w in worlds:
        m=w['events']
        for i,c in enumerate(m['connections'] or []):
            d=target(c['map']);back=[x for x in d['connections'] or [] if x['map']==m['id'] and x['direction']==opposite[c['direction']]]
            if len(back)!=1 or back[0]['offset']!=-c['offset']:raise ValueError('Nonreciprocal connection')
            connections.append(dict(sourceMap=m['id'],sourceIndex=i,**c,coordinateRule='destination transverse coordinate = source transverse coordinate - offset'))
        for i,e in enumerate(m['warp_events']):
            d=target(e['dest_map']);j=int(e['dest_warp_id']);end=d['warp_events'][j]
            if end['dest_map']!=m['id']:raise ValueError('Nonreciprocal warp')
            warps.append(dict(sourceMap=m['id'],sourceIndex=i,source=e,destinationMap=d['id'],destinationIndex=j,destination=end))
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
    spec=importlib.util.spec_from_file_location('_fuchsia_'+p.stem,p);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--check',action='store_true')
    a=ap.parse_args()
    out=ROOT/'content/generated/fuchsia-route-region'
    raw=ROOT/'content/raw/pokefirered/037335f4'
    source_lock_path=ROOT/'content/sources/fuchsia-route-region.lock.json'
    for target in (out,raw,source_lock_path):
        if target.is_symlink() or any(p.is_symlink() for p in target.parents):raise ValueError('Output root symlink denied')
    out.mkdir(parents=True,exist_ok=True)
    src=Source(a.reference_root);worlds=[decode(src,n) for n in CORE+OPTIONAL]
    doc=scripts(src,worlds);links=topology(src,worlds)
    doc['profile']=PROFILE
    doc['sourcePaths']=sorted({b['sourceRef']['path'] for b in doc['blocks']})
    for p in ['src/heal_location.c','src/data/heal_locations.json','src/overworld.c','src/field_player_avatar.c','src/field_control_avatar.c','src/event_object_movement.c','tools/mapjson/mapjson.cpp','src/data/wild_encounters.json','src/data/trainers.h','src/data/trainer_parties.h']:
        src.read(p,'rule-reference')
    for name in ['flags','vars','event_objects','trainers','opponents','items','species','moves','songs','map_scripts','field_effects','battle','pokemon','global','menu','game_stat','battle_ai','event_bg','event_object_movement','sound']:
        p='include/constants/'+name+'.h'
        if p in src.tree:src.read(p,'rule-reference')
    shared=load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS
    converter=load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='.stage-',dir=out) as temp:
        stage=Path(temp);rendered=[]
        for n in CORE[1:]:
            d=directory(n);shared.import_map(src,n,'map.'+d,stage/d);rendered.append(d)
        npc_worlds=[dict(events=dict(object_events=[o for o in w['events']['object_events'] if o['graphics_id']!='OBJ_EVENT_GFX_VAR_0'])) for w in worlds if w['sourceName'] in CORE]
        npc_worlds.append(dict(events=dict(object_events=[dict(graphics_id=x) for x in ['OBJ_EVENT_GFX_KABUTO','OBJ_EVENT_GFX_OMANYTE']])))
        npcs=shared.npc_inventory(src,npc_worlds,stage/'npc')
        npcs['dynamicGraphics']=[dict(map='MAP_FUCHSIA_CITY',localId=9,sourceGraphics='OBJ_EVENT_GFX_VAR_0',variable='VAR_OBJ_GFX_ID_0',flag='FLAG_GOT_DOME_FOSSIL',whenSet='OBJ_EVENT_GFX_OMANYTE',whenUnset='OBJ_EVENT_GFX_KABUTO')]
        constants=shared.constants_inventory(src,worlds,doc)
        heals=src.json('src/data/heal_locations.json')['heal_locations']
        for i,h in enumerate(heals,1):constants['values'][h['id']]=dict(value=i,sourceRef=src.ref('src/data/heal_locations.json',h['id']),derivation='one-based source array index generated by mapjson')
        catalog=[]
        for n,d in zip(CORE[1:],rendered):
            p=stage/d/'runtime-map.json';b=p.read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(b)));(stage/d/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=n,stableId='map.'+d,directory=d,sourceMapSymbol=next(w['events']['id'] for w in worlds if w['sourceName']==n),path=d+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE,optionalSourceOnlyMaps=OPTIONAL)
        lock_data=canonical(lock)
        inventory=dict(schemaVersion=1,profile=PROFILE,productBaseline=PRODUCT_COMMIT,sourceCommit=COMMIT,runtimeIntegrated=False,completeGame=False,coreMaps=CORE,optionalMaps=OPTIONAL,reusedMaps=['Route12'],newMapCount=6,maps=geometry_inventory(worlds),**links,trainers=trainers(worlds,doc),healLocation=next(x for x in heals if x['id']=='HEAL_LOCATION_FUCHSIA_CITY'))
        reused=dict(sourceName='Route12',stableId='map.route-12',sourceMapSymbol='MAP_ROUTE12',sourceRef=src.ref('data/maps/Route12/map.json','MAP_ROUTE12'),regenerate=False)
        main_catalog=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,maps=catalog,reusedMaps=[reused],tilesets=TILESETS,inputFiles=lock['files'],connections=links['connections'],warps=links['warps'])
        objects=[dict(map=w['stableId'],sourceMap=w['events']['id'],localId=i+1,sourceObjectIndex=i,declaration=o,sourceRef=w['sourceRef']) for w in worlds if w['sourceName'] in CORE for i,o in enumerate(w['events']['object_events'])]
        availability=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,enabledServices=[],loadedMapSymbols=[w['events']['id'] for w in worlds if w['sourceName'] in CORE[1:]],reusedMapSymbols=['MAP_ROUTE12'],deferredDestinations=sorted({w['dest_map'] for m in worlds if m['sourceName'] in CORE for w in m['events']['warp_events']} - {m['events']['id'] for m in worlds if m['sourceName'] in CORE}),deferredConnections=['MAP_ROUTE18','MAP_ROUTE19'],notes=['Map import preserves full original cells, events, clones, water and grass. Admission belongs to the successor application profile.','The no-grass arrival witness is a topology proof, not a permanent encounter ban.','Optional fishing house, gate 2F, other Fuchsia interiors, Safari and Koga are source boundaries in this map increment.'])
        docs=package_documents(out.parent, constants, {'fuchsia-route-region-catalog.json':main_catalog,'runtime-catalog.json':dict(schemaVersion=2,profile=PROFILE,converterVersion=converter.VERSION,sourceCommit=COMMIT,maps=catalog),'scripts.json':doc,'npc-source.json':npcs,'objects.json':dict(objects=objects),'availability.json':availability,'route-inventory.json':inventory}, check=a.check)
        for filename,value in docs.items():(stage/filename).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        selected=[w for w in worlds if w['sourceName'] in CORE[1:]]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=6,reusedMapCount=1,cellCount=sum(w['width']*w['height'] for w in selected),borderCellCount=sum(len(w['borderBlocks']) for w in selected),warpCount=sum(len(w['events']['warp_events']) for w in selected),objectCount=sum(len(w['events']['object_events']) for w in selected),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(doc['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_data=canonical(manifest)
        (stage/'manifest.json').write_bytes(manifest_data)
        raw_manifest=canonical(dict(schemaVersion=1,commit=COMMIT,inputCount=len(src.entries)))
        writes=[(source_lock_path,lock_data),(raw_metadata_directory(raw, 'fuchsia-route-region')/'manifest.json',raw_manifest)]+[(raw/p,b) for p,b in sorted(src.data.items())]+[(out/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for target,data in writes:
            if target.is_symlink() or any(p.is_symlink() for p in target.parents):raise ValueError('Output symlink denied: '+str(target))
            if a.check:
                if not target.is_file() or target.read_bytes()!=data:raise ValueError('Reproduction differs: '+str(target))
            else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(inputCount=len(src.entries),mapCount=6,reusedMapCount=1,cellCount=manifest['cellCount'],borderCellCount=manifest['borderCellCount'],warpCount=manifest['warpCount'],objectCount=manifest['objectCount'],npcGraphicsCount=manifest['npcGraphicsCount'],scriptBlockCount=len(doc['blocks']),outputCount=len(outputs))))

if __name__=='__main__':main()
