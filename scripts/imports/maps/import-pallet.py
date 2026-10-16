#!/usr/bin/env python3
"""Deterministic bounded five-map Pallet Town import/render. No ROM, no upstream execution, local assets only."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import LockedSource, render_map

from map_runtime import compact_world as _compact_world, encode as _encode_runtime, VERSION as _RUNTIME_VERSION

import argparse, base64, collections, json, pathlib, re, struct, tempfile, shutil
from PIL import Image
ROOT=_PROJECT_ROOT
RAW=ROOT/'content/raw/pokefirered/037335f4'
def binary(path): return (RAW/path).read_bytes()
def units(path,code):
    b=binary(path); size=struct.calcsize(code)
    if len(b)%size: raise ValueError('Misaligned '+path)
    return list(struct.unpack('<'+str(len(b)//size)+code,b))
def canonical(v): return json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n'
MAPS=[
    ('PalletTown','map.pallet-town','pallet'),
    ('PalletTown_PlayersHouse_1F','map.players-house-1f','players-house-1f'),
    ('PalletTown_PlayersHouse_2F','map.players-house-2f','players-house-2f'),
    ('PalletTown_RivalsHouse','map.rivals-house','rivals-house'),
    ('PalletTown_ProfessorOaksLab','map.professor-oaks-lab','professor-oaks-lab')]
TILESETS={'gTileset_General':'primary/general','gTileset_Building':'primary/building',
    'gTileset_PalletTown':'secondary/pallet_town','gTileset_GenericBuilding1':'secondary/generic_building_1',
    'gTileset_GenericBuilding2':'secondary/generic_building_2','gTileset_Lab':'secondary/lab'}
def import_map(name, stable_id, out):
    lock = json.loads((ROOT / 'scripts/recipes/source/fetch-pallet.lock.json').read_text(encoding='utf-8'))
    src = LockedSource(RAW, lock)
    rendered = render_map(src, name, stable_id, out, tilesets=TILESETS, render_border=False)
    map_path = f'data/maps/{name}/map.json'
    mapjson = rendered['events']
    width, height = rendered['width'], rendered['height']
    borderW, borderH = rendered['borderWidth'], rendered['borderHeight']
    cells, artifacts = rendered['cells'], rendered['artifacts']
    # Small bounded original male protagonist walking sheet, preserving source frame order.
    spritePath='graphics/object_events/pics/people/red_normal.png'
    sprite=Image.open(RAW/spritePath)
    if sprite.mode!='P' or sprite.size!=(144,32):raise ValueError('Unexpected Red normal sheet')
    playerLines=binary('graphics/object_events/palettes/player.pal').decode().splitlines()
    if playerLines[:3]!=['JASC-PAL','0100','16']:raise ValueError('Invalid player palette')
    playerColors=[tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in line.split()) for line in playerLines[3:]]
    rgba=Image.new('RGBA',sprite.size)
    rgba.putdata([(*playerColors[i],0 if i==0 else 255) for i in list(sprite.get_flattened_data())])
    rgba.save(out/'player.png',optimize=False,compress_level=9)
    # Technical smoke-test anchor only: first ordinary elevation-3 neighbor of source entrance warp.
    warp=mapjson['warp_events'][0]
    neighbors=[(warp['x']+dx,warp['y']+dy) for dx,dy in ([(0,1)] if name=='PalletTown' else [(0,-1),(-1,0),(0,1),(1,0)])]
    sx,sy=next((x,y) for x,y in neighbors if 0<=x<width and 0<=y<height and cells[y*width+x]['behaviorId']==0 and cells[y*width+x]['collision']==0 and cells[y*width+x]['elevation']==3)
    world={'schemaVersion':1,'stableId':stable_id,'sourceRef':src.ref(map_path,mapjson['id']),'metadata':{'repository':lock['repository'],'commit':lock['commit'],'rights':lock['rights'],'scope':'Bounded five-map static conversion. Original events/scripts/text retained without executing upstream code; runtime script/NPC/warp behavior belongs to the event engine. No water traversal or tile animation.','renderProfile':'Pinned source indexed PNG tiles, GBA RGB555 expanded to RGB888; no display color correction, tint, or animation. BG3 bottom, BG2 middle, BG1 top.'},'width':width,'height':height,'tileSize':16,'borderWidth':borderW,'borderHeight':borderH,'texturePath':'map.png','artifacts':artifacts,'cells':cells,'borderCells':rendered['borderCells'],'embeddedSource':rendered['embeddedSource'],'technicalSpawn':{'x':sx,'y':sy,'elevation':3,'reason':'Technical safe neighbor of the first source warp; not an original story-start claim.'},'events':mapjson}
    if cells[sy*width+sx]['movement']!='normal' or cells[sy*width+sx]['collision']!=0 or cells[sy*width+sx]['elevation']!=3:raise ValueError('Technical spawn no longer safe')
    (out / 'runtime-map.json').write_bytes(_encode_runtime(_compact_world(world)))
    print(f'Imported {name}: {width}x{height} map; {width*16}x{height*16}px; {len(cells)} cells; behaviors {dict(collections.Counter(c["behaviorName"] for c in cells))}')
    return world


def write_source_registry(generated, metadata):
    """Store common provenance once, independently of each compact map."""
    excluded = {'fullSourceHash', 'tree', 'sha256', 'gitBlobSha1', 'byteLength', 'files'}
    clean = {key: value for key, value in metadata.items() if key not in excluded}
    data = _encode_runtime(dict(schemaVersion=3, metadata=clean))
    target = pathlib.Path(generated) / 'maps/metadata.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    stage = target.with_suffix('.tmp'); stage.write_bytes(data); stage.replace(target)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--collection-output',type=pathlib.Path,default=ROOT/'content/generated')
    args=parser.parse_args()
    maps_out=args.collection_output/'maps/regions/opening'; maps_out.mkdir(parents=True,exist_ok=True)
    sprite_out=args.collection_output/'sprites/overworld'; sprite_out.mkdir(parents=True,exist_ok=True)
    worlds=[]
    with tempfile.TemporaryDirectory(prefix='opening-maps-') as temporary:
        for name,stable,directory in MAPS:
            stage=pathlib.Path(temporary)/directory
            world=import_map(name,stable,stage); worlds.append(world)
            doc=json.loads((stage/'runtime-map.json').read_bytes())
            for artifact in doc['artifacts']:
                original=artifact['path']; artifact['path']=directory+'.'+original
                shutil.copyfile(stage/original,maps_out/artifact['path'])
            doc['texturePath']=directory+'.'+doc['texturePath']
            (maps_out/(directory+'.json')).write_bytes(_encode_runtime(doc))
            player=(stage/'player.png').read_bytes()
            if directory=='pallet': (sprite_out/'opening-player.png').write_bytes(player)
            elif player!=(sprite_out/'opening-player.png').read_bytes(): raise ValueError('Opening player art differs')
    ids=sorted({e['graphics_id'] for w in worlds for e in w['events']['object_events']})
    (sprite_out/'opening.json').write_text(canonical(dict(graphics=[dict(graphicsId=x) for x in ids])))
    write_source_registry(args.collection_output, worlds[0]['metadata'])
    by_symbol={w['events']['id']:w for w in worlds}
    edges=[]
    for world in worlds:
        for index,warp in enumerate(world['events']['warp_events']):
            target=by_symbol.get(warp['dest_map']); dest=int(warp['dest_warp_id'])
            if target is None or not 0<=dest<len(target['events']['warp_events']):raise ValueError('Warp leaves bounded collection or has missing destination')
            exit=target['events']['warp_events'][dest]
            if not (0<=warp['x']<world['width'] and 0<=warp['y']<world['height'] and 0<=exit['x']<target['width'] and 0<=exit['y']<target['height']):raise ValueError('Warp coordinate outside source layout')
            if exit['dest_map']!=world['events']['id']:raise ValueError('Warp destination does not return to originating map')
            edges.append({'sourceMapId':world['stableId'],'sourceWarpId':index,'destinationMapId':target['stableId'],'destinationWarpId':dest,'sourceX':warp['x'],'sourceY':warp['y'],'destinationX':exit['x'],'destinationY':exit['y']})
    print(f'Validated {len(worlds)} maps and {len(edges)} source warp destinations; exact source event arrays retained.')
if __name__=='__main__':main()
