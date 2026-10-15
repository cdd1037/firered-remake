#!/usr/bin/env python3
"""Import 17 authentic service interiors; reference all four existing Saffron gates unchanged."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parents[2] / 'scripts/lib')
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
from pathlib import Path
import argparse, importlib.util, json, shutil, tempfile
_lock_spec=importlib.util.spec_from_file_location('_regional_service_source_lock', Path(__file__).with_name('build_source_lock.py'))
_lock=importlib.util.module_from_spec(_lock_spec);_lock_spec.loader.exec_module(_lock)
for _name in ('ROOT','COMMIT','PROFILE','MAPS','NEW_MAPS','REUSED_MAPS','TILESETS'): globals()[_name]=getattr(_lock,_name)
from map_runtime import generated_root, publish_map_package
import script_inventory as INVENTORY
def load_helper(name, path):
    spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
SHARED=load_helper('_regional_service_shared', Path(__file__).with_name('_regional_service_shared.py'))
CONVERTER=load_helper('_regional_service_compact', ROOT/'scripts/imports/maps/export-runtime-maps.py')
canonical=SHARED.canonical
original_read_lock=SHARED.read_lock
def read_lock(path=None):return original_read_lock(path or ROOT/'scripts/recipes/config/regional-service-maps.json')
for k in ('ROOT','COMMIT','PROFILE','MAPS','TILESETS','read_lock'):setattr(SHARED,k,globals()[k])
INVENTORY.PROFILE=PROFILE;INVENTORY.SERVICE_MAPS=[n for n,_,_ in MAPS]

def protect(target,role):
    target=Path(target);SHARED.require_safe_target(ROOT,target)
    for p in [ROOT/x for x in ['tools','src','tests','proof','evidence','scripts/recipes/config','content/'+('generated' if role=='raw' else 'raw')]]:
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
        rows.append(dict(sourceName=n,stableId=s,sourceMapSymbol=src.json(f'data/maps/{n}/map.json')['id'],sourceRef=src.ref(f'data/maps/{n}/map.json',src.json(f'data/maps/{n}/map.json')['id']),existingDirectory=('vermilion-region/' if north_south else 'celadon-approach-region/')+d,regenerate=False))
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
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=n,stableId=s,directory=d,sourceMapSymbol=w['events']['id'],availability='authenticatedSourceOnly' if n.endswith('_Elevator') else 'requiresExplicitComposition') for (n,s,d),w in zip(NEW_MAPS,worlds)],
            reusedMaps=reused_inventory(src),connections=[],warps=warps,tilesets=TILESETS)
        compact_entries=[]
        for (n,s,d),w in zip(NEW_MAPS,worlds):
            rich=(stage/d/'runtime-map.json').read_bytes();encoded=CONVERTER.encode(CONVERTER.normalize_runtime(json.loads(rich)));(stage/d/'runtime-map.json').write_bytes(encoded)
            compact_entries.append(dict(stableId=s,path=d+'/runtime-map.json'))
        runtime=dict(schemaVersion=3,maps=compact_entries)
        unavailable=dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,enabledServices=[],
            unavailableServices=['Tea gift and shared guard handoff require bounded application integration','Eevee party/PC/storage-full/nickname require bounded application integration','Name Rater ownership, party selection, egg and nickname branches','Department Store marts, Counter tutor, vending, drink rewards, and dynamic elevator','Coin Case, coin economy, Prize Room exchange','Diploma completion and Game Freak computer menus','Fame viewer, invisible journal interaction and ordinary NPC branch replay'],
            dynamicWarpMap='map.celadon-city-department-store-elevator',externalDestinations=sorted({x['destinationMap'] for x in warps if x['runtimeStatus']=='externalBoundary'}),
            gates='Route5/6 maps and art remain owned by Vermilion, Route7/8 by Celadon approach; all four gate source declarations are authenticated here. Saffron city is not imported or enabled.')
        for name,doc in package_documents(generated_root(out), constants, {'regional-service-maps-catalog.json':catalog,'runtime-catalog.json':runtime,'scripts.json':scripts,'npc-source.json':npcs,'objects.json':object_inventory(all_maps),'availability.json':unavailable}, check=check).items():(stage/name).write_text(canonical(doc))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,outputs=outputs)
        data=canonical(manifest).encode()
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage);SHARED.require_safe_target(out,dest)
            if p.is_file():
                if check:
                    if not dest.is_file() or dest.read_bytes()!=p.read_bytes():raise ValueError('Generated file differs: '+str(dest))
                else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    report=dict(**{k:v for k,v in manifest.items() if k!='outputs'})
    print(json.dumps(report,sort_keys=True));return manifest

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path);p.add_argument('--materialize',action='store_true');p.add_argument('--check',action='store_true');p.add_argument('--raw',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=Path,default=ROOT/'content/generated/maps/regions/regional-service-maps');a=p.parse_args()
    if a.materialize:
        if not a.reference_root:p.error('--materialize requires --reference-root')
        if a.check:p.error('--materialize and --check are mutually exclusive')
        protect(a.raw,'raw');SHARED.materialize(a.raw,a.reference_root)
    build(a.raw,a.out,a.check)
