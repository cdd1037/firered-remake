#!/usr/bin/env python3
"""Import twelve authenticated Safari maps and preserve the Fuchsia entrance identity.

No upstream scripts are executed. Map and compact-data converters run only during import. Session, quest rewards, encounters and field-action boundaries are retained as source contracts.
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

from pathlib import Path
import argparse, collections,  importlib.util, json, os, re, struct, subprocess, sys, tempfile

ROOT = Path(__file__).resolve().parents[1]
PRODUCT = ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PRODUCT_COMMIT = '48f9bf97'
PROFILE = 'firered-safari-region-import-v1'
CORE = ['FuchsiaCity_SafariZone_Entrance', 'FuchsiaCity_SafariZone_Office', 'SafariZone_Center', 'SafariZone_East', 'SafariZone_North', 'SafariZone_West', 'SafariZone_Center_RestHouse', 'SafariZone_East_RestHouse', 'SafariZone_North_RestHouse', 'SafariZone_West_RestHouse', 'SafariZone_SecretHouse', 'FuchsiaCity_WardensHouse']
OPTIONAL = []
TILESETS = {'gTileset_General':'primary/general', 'gTileset_FuchsiaCity':'secondary/fuchsia_city', 'gTileset_Building':'primary/building', 'gTileset_SafariZoneBuilding':'secondary/safari_zone_building', 'gTileset_Museum':'secondary/museum'}

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
    roots.update(n for n,b in blocks.items() if b['path']=='data/scripts/safari_zone.inc')
    roots.update(['Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo','Std_FindItem','Std_ReceivedItem','Std_PutItemAway','EventScript_UseSurf'])
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



def load_helper(name):
    p=PRODUCT/('scripts' if name == 'export-runtime-maps.py' else 'tools/import-regional-service-maps')/name;b=p.read_bytes()
    spec=importlib.util.spec_from_file_location('_safari_'+p.stem,p);module=importlib.util.module_from_spec(spec)
    if name=='_regional_service_shared.py':
        # ResetScreenForMapLoad clears VRAM. These three source maps use one tile
        # immediately beyond the emitted Fuchsia secondary graphics: retain zero.
        marker="            if palette >= 13 or local >= emitted[index]:\n"
        if b.decode().count(marker)!=1:raise ValueError('Reviewed renderer insertion point differs')
        zero="            if name in ('SafariZone_Center','SafariZone_East','SafariZone_North') and tid == 831 and palette == 11 and index == 1 and local == 191 and emitted[index] == 191:\n                result = Image.new('RGBA', (8, 8)); tile_cache[entry] = result; return result\n"
        exec(compile(b.decode().replace(marker,zero+marker),str(p),'exec'),module.__dict__)
    else:spec.loader.exec_module(module)
    return module


def source_contracts(src, worlds, doc):
    """Extract source values; runtime enablement belongs to explicit application handlers."""
    by_label={b['label']:b for b in doc['blocks']}
    def script(label):
        b=by_label[label]
        return dict(label=label,sourceRef=b['sourceRef'],lines=b['lines'])
    c=src.text('src/safari_zone.c')
    enter=re.search(r'void EnterSafariMode\(void\)\n\{(.*?)\n\}',c,re.S)
    if not enter:raise ValueError('Missing EnterSafariMode source')
    balls=int(re.search(r'gNumSafariBalls = (\d+);',enter[1])[1]);steps=int(re.search(r'gSafariZoneStepCounter = (\d+);',enter[1])[1])
    entry=script('FuchsiaCity_SafariZone_Entrance_EventScript_TryEnterSafariZone')
    fees=[int(re.search(r'\b'+opcode+r' (\d+)\b','\n'.join(r['text'] for r in entry['lines']))[1]) for opcode in ('checkmoney','removemoney')]
    if fees!=[500,500] or balls!=30 or steps!=600:raise ValueError('Safari source session contract differs')
    rewards=[]
    for symbol,item,flag in [('SafariZone_SecretHouse_EventScript_Attendant','ITEM_HM03','FLAG_GOT_HM03'),('FuchsiaCity_WardensHouse_EventScript_GiveGoldTeeth','ITEM_HM04','FLAG_GOT_HM04')]:
        rewards.append(dict(item=item,flag=flag,**script(symbol)))
    items=[]
    for w in worlds:
        for i,o in enumerate(w['events']['object_events'],1):
            if o['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
            body=script(o['script']);item=re.search(r'(?:finditem|giveitem(?:_std|_msg)?)\s+(ITEM_\w+)', '\n'.join(x['text'] for x in body['lines']))
            if not item:raise ValueError('Missing source item pickup: '+o['script'])
            items.append(dict(map=w['stableId'],localId=i,item=item[1],flag=o['flag'],x=o['x'],y=o['y'],**body))
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,entryFee=fees[0],startingBalls=balls,startingSteps=steps,
        entry=entry,sessionSource=src.ref('src/safari_zone.c','EnterSafariMode',c[:enter.start()].count('\n')+1),
        sessionSceneVariable='VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE',safariFlag='FLAG_SYS_SAFARI_MODE',
        sceneValues=dict(inactive=0,exitWarpIn=1,activeOrExitEarly=2,outOfBallsMidBattle=3),
        sessionEvents=[script(n) for n in sorted(by_label) if n.startswith('SafariZone_EventScript_')],
        rewards=rewards,pickups=items,hiddenItems=[dict(map=w['stableId'],declaration=e,sourceRef=w['sourceRef']) for w in worlds for e in w['events']['bg_events'] if e['type']=='hidden_item'],
        goldTeeth=dict(item='ITEM_GOLD_TEETH',flag='FLAG_HIDE_SAFARI_ZONE_WEST_GOLD_TEETH',sourceMap='MAP_SAFARI_ZONE_WEST',localId=1,x=28,y=14,
            pickup=script('SafariZone_West_EventScript_ItemGoldTeeth'),warden=script('FuchsiaCity_WardensHouse_EventScript_Warden'),handoff=script('FuchsiaCity_WardensHouse_EventScript_GiveGoldTeeth')),
        fieldMoveDependencies=[dict(name='Surf',sourceFiles=['data/scripts/surf.inc','src/field_effect.c','src/field_player_avatar.c','src/field_control_avatar.c'],sourceEntry='EventScript_UseSurf',enabled=False),dict(name='Strength',sourceFiles=['data/scripts/field_moves.inc','src/field_effect.c','src/field_player_avatar.c'],sourceEntry='EventScript_StrengthBoulder',enabled=False)],
        limitations=['Source contract only; importing HM03/HM04 does not admit field Surf/Strength.', 'Original Gold Teeth object hide flag gates Warden handoff; grant HM04, set FLAG_GOT_HM04, then remove Gold Teeth in source order.', 'Unknown object scripts and unavailable field actions must fail explicitly.'])

def encounter_contract(src):
    path='src/data/wild_encounters.json';doc=src.json(path);weights=[20,20,10,10,10,10,5,5,4,4,1,1];tables=[];excluded=[];species=set()
    for area in ('Center','East','North','West'):
        symbol='sSafariZone'+area+'_FireRed';matches=[e for g in doc['wild_encounter_groups'] for e in g.get('encounters',[]) if e['base_label']==symbol]
        if len(matches)!=1 or len(matches[0]['land_mons']['mons'])!=12:raise ValueError('Missing exact FireRed Safari table')
        row=matches[0];land=row['land_mons'];mapid='map.safari-zone-'+area.lower()
        tables.append(dict(map=dict(value=mapid),sourceCommit=COMMIT,sourcePath=path,sourceSymbol=symbol,rawEncounterRate=land['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(land['mons'])]))
        species.update(m['species'] for m in land['mons'])
        for method in ('water_mons','fishing_mons'):
            excluded.append(dict(map=mapid,sourceSymbol=symbol,method=method,sourceTable=row[method],enabled=False))
    held=[];text=src.text('src/data/pokemon/species_info.h')
    for speciesid in sorted(species):
        m=re.search(r'^    \['+speciesid+r'\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S)
        if not m:raise ValueError('Missing species held-item row')
        f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
        held.append(dict(species='pret:'+speciesid,common=f['itemCommon'],rare=f['itemRare'],sourceRef=src.ref('src/data/pokemon/species_info.h',speciesid,text[:m.start()].count('\n')+1)))
    return dict(schemaVersion=1,profile='firered-safari-encounters-v1',sourceCommit=COMMIT,tables=tables,heldItems=held,heldItemDrawBound=100,excludedTables=excluded,
        missingGroundSpecies=['SPECIES_CHANSEY','SPECIES_SCYTHER','SPECIES_TAUROS'],missingDeferredSpecies=['SPECIES_DRAGONAIR','SPECIES_DRATINI','SPECIES_PSYDUCK'],limitations=['Four exact FireRed ground tables only; every source slot and level is preserved.', 'Water and fishing tables are retained as source evidence without encounter admission or map accessibility.'])

def main():
    global ROOT, PRODUCT
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--reference-root',type=Path,required=True)
    ap.add_argument('--project-root',type=Path,default=ROOT,help='Reviewed helper location')
    ap.add_argument('--output-root',type=Path,default=ROOT,help='Independent package destination')
    ap.add_argument('--check',action='store_true')
    a=ap.parse_args();ROOT=a.output_root.absolute();PRODUCT=a.project_root.absolute()
    out=ROOT/'content/generated/safari-region';raw=ROOT/'content/raw/pokefirered/037335f4'
    if ROOT==a.reference_root.absolute() or ROOT.is_relative_to(a.reference_root.absolute()) or a.reference_root.absolute().is_relative_to(ROOT):raise ValueError('Reference/output overlap')
    for target in (out,raw):
        if target.is_symlink() or any(p.is_symlink() for p in target.parents):raise ValueError('Output root symlink denied')
    out.mkdir(parents=True,exist_ok=True)
    src=Source(a.reference_root);worlds=[decode(src,n) for n in CORE]
    doc=scripts(src,worlds);topology(src,worlds)
    doc['profile']=PROFILE;doc['sourcePaths']=sorted({b['sourceRef']['path'] for b in doc['blocks']})
    for p in ['src/safari_zone.c','src/wild_encounter.c','src/pokemon.c','src/data/pokemon/species_info.h','src/data/items.json','src/overworld.c','src/fieldmap.c','src/new_menu_helpers.c','include/fieldmap.h','src/field_player_avatar.c','src/field_control_avatar.c','src/field_effect.c','src/event_object_movement.c','tools/mapjson/mapjson.cpp','src/data/wild_encounters.json','asm/macros/event.inc','data/scripts/field_moves.inc']:
        src.read(p,'rule-reference')
    for name in ['flags','vars','event_objects','trainers','opponents','items','species','moves','songs','map_scripts','field_effects','battle','pokemon','global','menu','game_stat','battle_ai','event_bg','event_object_movement','sound','fame_checker']:
        p='include/constants/'+name+'.h'
        if p in src.tree:src.read(p,'rule-reference')
    contracts=source_contracts(src,worlds,doc);encounters=encounter_contract(src)
    vram=src.text('src/overworld.c');copy=src.text('src/new_menu_helpers.c');field=src.text('src/fieldmap.c')
    if 'DmaFillLarge16(3, 0, (void *)(VRAM + 0x0), 0x18000, 0x1000);' not in vram or 'CopyDecompressedTileDataToVram(bgId, ptr, sizeOut, offset, mode)' not in copy or 'CopyTilesetToVramUsingHeap(mapLayout->secondaryTileset, NUM_TILES_TOTAL - NUM_TILES_IN_PRIMARY, NUM_TILES_IN_PRIMARY);' not in field:raise ValueError('Safari zero VRAM source chain differs')
    contracts['zeroVramTile']=dict(maps=['SafariZone_Center','SafariZone_East','SafariZone_North'],metatileId=772,tileId=831,palette=11,secondaryOffset=640,emittedTiles=191,loadedTileRange=[640,830],clearByteRange=[0,98304],tileByteRange=[26592,26623],sourceRefs=[src.ref('src/overworld.c','ResetScreenForMapLoad'),src.ref('src/fieldmap.c','CopyMapTilesetsToVram'),src.ref('src/new_menu_helpers.c','DecompressAndLoadBgGfxUsingHeap2'),src.ref('include/fieldmap.h','NUM_TILES_IN_PRIMARY'),src.ref('tileset_rules.mk','secondary/fuchsia_city/tiles.4bpp'),src.ref('src/data/tilesets/headers.h','gTileset_FuchsiaCity')],note='Compressed copy uses decompressed sizeOut (191*32 bytes), offset640; tile831 remains in cleared VRAM. Fuchsia secondary tileset has no animation callback.')
    shared=load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS;converter=load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='.stage-',dir=out) as temp:
        stage=Path(temp);rendered=[]
        for n in CORE:
            d=directory(n);shared.import_map(src,n,'map.'+d,stage/d);rendered.append(d)
        npc_worlds=[dict(events=dict(object_events=[o for o in w['events']['object_events'] if o['graphics_id']!='0'])) for w in worlds]
        npc_worlds.append(dict(events=dict(object_events=[dict(graphics_id=g) for g in ('OBJ_EVENT_GFX_RED_SURF','OBJ_EVENT_GFX_GREEN_SURF')])))
        npcs=shared.npc_inventory(src,npc_worlds,stage/'npc');constants=shared.constants_inventory(src,worlds,doc)
        # Fuchsia is authenticated for unchanged entrance matching, never regenerated.
        city=src.json('data/maps/FuchsiaCity/map.json')
        reused=dict(sourceName='FuchsiaCity',stableId='map.fuchsia-city',sourceMapSymbol='MAP_FUCHSIA_CITY',sourceRef=src.ref('data/maps/FuchsiaCity/map.json','MAP_FUCHSIA_CITY'),regenerate=False)
        catalog=[]
        for n,d in zip(CORE,rendered):
            p=stage/d/'runtime-map.json';b=p.read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(b)));(stage/d/'runtime-map.json').write_bytes(compact)
            catalog.append(dict(sourceName=n,stableId='map.'+d,directory=d,sourceMapSymbol=next(w['events']['id'] for w in worlds if w['sourceName']==n),path=d+'/runtime-map.json'))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=CORE,optionalSourceOnlyMaps=OPTIONAL)
        lock_data=canonical(lock)
        main_catalog=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,maps=catalog,reusedMaps=[reused],tilesets=TILESETS,inputFiles=lock['files'],connections=links['connections'],warps=links['warps'])
        objects=[dict(map=w['stableId'],sourceMap=w['events']['id'],localId=i+1,sourceObjectIndex=i,declaration=o,sourceRef=w['sourceRef']) for w in worlds for i,o in enumerate(w['events']['object_events'])]
        availability=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,enabledServices=[],loadedMapSymbols=[w['events']['id'] for w in worlds],reusedMapSymbols=['MAP_FUCHSIA_CITY'],deferredDestinations=[],deferredConnections=[],notes=['Full original map grids, events, warp offsets, water and grass are retained.', 'All four areas, four rest houses, Secret House, Entrance, Office and Warden house require explicit successor application admission.', 'Water traversal, fishing and Strength boulder use are separate field capabilities; HM rewards do not authorize a movement shortcut.'])
        docs=package_documents(out.parent, constants, {'safari-region-catalog.json':main_catalog,'runtime-catalog.json':dict(schemaVersion=2,profile=PROFILE,converterVersion=converter.VERSION,sourceCommit=COMMIT,maps=catalog),'scripts.json':doc,'npc-source.json':npcs,'objects.json':dict(objects=objects),'availability.json':availability,'contracts.json':contracts,'encounters.json':encounters}, check=a.check)
        for filename,value in docs.items():(stage/filename).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=len(CORE),reusedMapCount=1,cellCount=sum(w['width']*w['height'] for w in worlds),borderCellCount=sum(len(w['borderBlocks']) for w in worlds),warpCount=sum(len(w['events']['warp_events']) for w in worlds),objectCount=len(objects),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(doc['blocks']),unresolvedLabelCount=0,outputs=outputs)
        manifest_data=canonical(manifest)
        (stage/'manifest.json').write_bytes(manifest_data)
        raw_manifest=canonical(dict(schemaVersion=1,commit=COMMIT,inputCount=len(src.entries)))
        writes=[]+[(raw/p,b) for p,b in sorted(src.data.items())]+[(out/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for target,data in writes:
            if target.is_symlink() or any(p.is_symlink() for p in target.parents):raise ValueError('Output symlink denied: '+str(target))
            if a.check:
                if not target.is_file() or target.read_bytes()!=data:raise ValueError('Reproduction differs: '+str(target))
            else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs))))

if __name__=='__main__':main()
