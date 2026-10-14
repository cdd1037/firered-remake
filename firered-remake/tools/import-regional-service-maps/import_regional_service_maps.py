#!/usr/bin/env python3
"""Import 17 authentic service interiors; reference all four existing Saffron gates unchanged."""
from pathlib import Path
import argparse, copy, hashlib, importlib.util, json, shutil, tempfile
from build_source_lock import ROOT, COMMIT, TREE, PROFILE, MAPS, NEW_MAPS, REUSED_MAPS, TILESETS
import script_inventory as INVENTORY
LOCK_SHA256='3b15d715a755329e7a0a4da5ee2a834af625fc2c1b16f4b2c1a1f3510c163db2'
MANIFEST_SHA256='a2c99c7d70d926cc0f67efdda254e78f58f3ebafe774c5b424cdda7ebcf8f2a3'
SHARED_SHA256='06652d76f26abf13cd25b5e8a80c72f6e134314f62786cce79eee45d84303a0b'
CONVERTER_SHA256='9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
REUSED_MANIFEST='f487d67103ec1db904fb0bee6caa95f1b1f96f5725ad850e83e42b6f3f868d3f'
REUSED_SOURCE='756a0c0bfe4ab147b9443e71a8937a5b2a661a65e192919c5ac2691b89589f89'
VERMILION_MANIFEST='a82c5a5c5c6c04c743fc39d4bdef8e017a796a3f78a60867bdecfc767540d910'
VERMILION_SOURCE='fffd904ff456351bef0ed808b1043c968f2b03db122a10e056ac3971b88f1759'
def load_helper(name,path,sha):
    if hashlib.sha256(path.read_bytes()).hexdigest()!=sha:raise ValueError('Reviewed converter differs: '+path.name)
    spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
SHARED=load_helper('_regional_service_shared',Path(__file__).with_name('_regional_service_shared.py'),SHARED_SHA256)
CONVERTER=load_helper('_regional_service_compact',Path(__file__).with_name('_reviewed_runtime_converter.py'),CONVERTER_SHA256)
digest,canonical=SHARED.digest,SHARED.canonical
original_read_lock=SHARED.read_lock
def read_lock(path=None):return original_read_lock(path or ROOT/'content/sources/regional-service-maps.lock.json')
for k in ('ROOT','COMMIT','TREE','PROFILE','MAPS','TILESETS','LOCK_SHA256','read_lock'):setattr(SHARED,k,globals()[k])
INVENTORY.PROFILE=PROFILE;INVENTORY.SERVICE_MAPS=[n for n,_,_ in MAPS]

def protect(target,role):
    target=Path(target);SHARED.require_safe_target(ROOT,target)
    for p in [ROOT/x for x in ['tools','src','tests','proof','evidence','content/sources','content/'+('generated' if role=='raw' else 'raw')]]:
        if target.resolve().is_relative_to(p.resolve()) or p.resolve().is_relative_to(target.resolve()):raise ValueError('Protected source/output overlap')

def warp_inventory(src,worlds):
    maps={src.json(p)['id']:(p,src.json(p)) for p in src.data if p.startswith('data/maps/') and p.endswith('/map.json')}
    selected={w['events']['id'] for w in worlds};rows=[]
    for world in worlds:
        if world['events']['connections'] is not None:raise ValueError('Unexpected indoor connection')
        for index,warp in enumerate(world['events']['warp_events']):
            row=dict(sourceMap=world['events']['id'],sourceWarpId=index,source=warp,runtimeStatus='notIntegrated')
            if warp['dest_map']=='MAP_DYNAMIC':
                if world['events']['name']!='CeladonCity_DepartmentStore_Elevator' or warp['dest_warp_id']!='WARP_ID_DYNAMIC':raise ValueError('Unexpected dynamic warp')
                row.update(runtimeStatus='unavailableDepartmentStoreDynamicWarp',destinationMap='MAP_DYNAMIC',destinationWarpId=None)
            else:
                p,target=maps[warp['dest_map']];dest_id=int(warp['dest_warp_id']);destination=target['warp_events'][dest_id]
                if destination['dest_map'] not in (world['events']['id'],'MAP_DYNAMIC'):
                    raise ValueError('Nonreciprocal source warp: '+world['events']['name'])
                row.update(destinationMap=target['id'],destinationWarpId=dest_id,destinationX=destination['x'],destinationY=destination['y'],destinationSourceRef=src.ref(p,target['id']))
                if destination['dest_map']=='MAP_DYNAMIC':row['runtimeStatus']='unavailableDepartmentStoreDynamicWarp'
                elif target['id'] not in selected:row['runtimeStatus']='externalBoundary'
            rows.append(row)
    return rows

def object_inventory(worlds):
    rows=[]
    for w in worlds:
        for i,obj in enumerate(w['events']['object_events'],1):
            rows.append(dict(map=w['stableId'],sourceMap=w['events']['id'],localId=i,sourceObjectIndex=i-1,declaration=obj,sourceRef=w['sourceRef'],availability='sourceOnly',invisible=obj['movement_type']=='MOVEMENT_TYPE_INVISIBLE'))
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,objects=rows)

def reused_inventory(src):
    # The existing loader/profile retains ownership. No duplicate world, compact map or PNG is generated.
    rows=[]
    for n,s,d in REUSED_MAPS:
        north_south=n in ('Route5_SouthEntrance','Route6_NorthEntrance')
        rows.append(dict(sourceName=n,stableId=s,sourceMapSymbol=src.json(f'data/maps/{n}/map.json')['id'],sourceRef=src.ref(f'data/maps/{n}/map.json',src.json(f'data/maps/{n}/map.json')['id']),existingDirectory=('vermilion-region/' if north_south else 'celadon-approach-region/')+d,manifestSha256=VERMILION_MANIFEST if north_south else REUSED_MANIFEST,fullSourceHash=VERMILION_SOURCE if north_south else REUSED_SOURCE,regenerate=False))
    return rows

def build(raw,out,check=False):
    raw,out=Path(raw),Path(out);protect(out,'generated');SHARED.require_safe_target(ROOT,raw)
    if raw.resolve()==out.resolve() or raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):raise ValueError('Input/output overlap')
    src=SHARED.Source(raw);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.regional-services-stage-',dir=out.parent) as t:
        stage=Path(t);worlds=[SHARED.import_map(src,n,s,stage/d) for n,s,d in NEW_MAPS]
        # Existing gates contribute source script/object declarations without emitting replacement map assets.
        reused=[dict(stableId=s,events=src.json(f'data/maps/{n}/map.json'),sourceRef=src.ref(f'data/maps/{n}/map.json',src.json(f'data/maps/{n}/map.json')['id'])) for n,s,_ in REUSED_MAPS]
        all_maps=worlds+reused;scripts=INVENTORY.script_inventory(src,all_maps);constants=SHARED.constants_inventory(src,all_maps,scripts)
        npc_worlds=[dict(events=dict(object_events=[o for o in w['events']['object_events'] if o['graphics_id']!='0'])) for w in worlds]
        npcs=SHARED.npc_inventory(src,npc_worlds,stage/'npc')
        npcs['invisibleObjects']=[r for r in object_inventory(worlds)['objects'] if r['invisible']]
        warps=warp_inventory(src,worlds)
        source_hash=SHARED.source_hash(src.lock)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,tree=TREE,fullSourceHash=source_hash,runtimeIntegrated=False,
            maps=[dict(sourceName=n,stableId=s,directory=d,sourceMapSymbol=w['events']['id'],availability='authenticatedSourceOnly' if n.endswith('_Elevator') else 'requiresExplicitComposition') for (n,s,d),w in zip(NEW_MAPS,worlds)],
            reusedMaps=reused_inventory(src),connections=[],warps=warps,inputFiles=src.lock['files'],tilesets=TILESETS)
        compact_entries=[]
        for (n,s,d),w in zip(NEW_MAPS,worlds):
            rich=(stage/d/'world.json').read_bytes();encoded=CONVERTER.encode(CONVERTER.convert(rich,digest(rich),source_hash));(stage/d/'runtime-map.json').write_bytes(encoded)
            compact_entries.append(dict(stableId=s,path=d+'/runtime-map.json',byteLength=len(encoded),sha256=digest(encoded),sourcePath=d+'/world.json',sourceByteLength=len(rich),sourceWorldSha256=digest(rich),fullSourceHash=source_hash))
        runtime=dict(schemaVersion=1,profile=PROFILE,converterVersion=CONVERTER.VERSION,converterSha256=CONVERTER_SHA256,sourceCommit=COMMIT,maps=compact_entries)
        unavailable=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,enabledServices=[],
            unavailableServices=['Tea gift and shared guard handoff require bounded application integration','Eevee party/PC/storage-full/nickname require bounded application integration','Name Rater ownership, party selection, egg and nickname branches','Department Store marts, Counter tutor, vending, drink rewards, and dynamic elevator','Coin Case, coin economy, Prize Room exchange','Diploma completion and Game Freak computer menus','Fame viewer, invisible journal interaction and ordinary NPC branch replay'],
            dynamicWarpMap='map.celadon-city-department-store-elevator',externalDestinations=sorted({x['destinationMap'] for x in warps if x['runtimeStatus']=='externalBoundary'}),
            gates='Route5/6 maps and art remain owned by Vermilion, Route7/8 by Celadon approach; all four gate source declarations are authenticated here. Saffron city is not imported or enabled.')
        for name,doc in {'regional-service-maps-catalog.json':catalog,'runtime-catalog.json':runtime,'scripts.json':scripts,'constants.json':constants,'npc-source.json':npcs,'objects.json':object_inventory(all_maps),'availability.json':unavailable}.items():(stage/name).write_text(canonical(doc))
        outputs=[dict(path=p.relative_to(stage).as_posix(),sha256=digest(p.read_bytes()),byteLength=p.stat().st_size) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,fullSourceHash=source_hash,lockSha256=LOCK_SHA256,inputCount=len(src.entries),mapCount=len(worlds),reusedMapCount=len(reused),sourceMapCount=len(all_maps),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),warpCount=len(warps),objectCount=sum(len(w['events']['object_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
        if MANIFEST_SHA256 is not None and digest(data)!=MANIFEST_SHA256:raise ValueError('Generated manifest pin differs: '+digest(data))
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage);SHARED.require_safe_target(out,dest)
            if p.is_file():
                if check:
                    if not dest.is_file() or dest.read_bytes()!=p.read_bytes():raise ValueError('Generated file differs: '+str(dest))
                else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    report=dict(manifestSha256=digest(data),**{k:v for k,v in manifest.items() if k!='outputs'})
    print(json.dumps(report,sort_keys=True));return manifest

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path);p.add_argument('--materialize',action='store_true');p.add_argument('--check',action='store_true');p.add_argument('--raw',type=Path,default=ROOT/'content/raw/regional-service-maps');p.add_argument('--out',type=Path,default=ROOT/'content/generated/regional-service-maps');a=p.parse_args()
    if a.materialize:
        if not a.reference_root:p.error('--materialize requires --reference-root')
        if a.check:p.error('--materialize and --check are mutually exclusive')
        protect(a.raw,'raw');SHARED.materialize(a.raw,a.reference_root)
    build(a.raw,a.out,a.check)
