#!/usr/bin/env python3
"""Deterministic bounded five-map Pallet Town import/render. No ROM, no upstream execution, local assets only."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

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
def import_map(name,stable_id,out):
    lock=json.loads((ROOT/'scripts/recipes/source/fetch-pallet.lock.json').read_text(encoding='utf-8')); manifest=lock
    for entry in lock['files']:
        binary(entry['path'])
    def source(path,symbol): return {'kind':'pret','path':path,'symbol':symbol,'repository':lock['repository'],'commit':lock['commit']}
    map_path=f'data/maps/{name}/map.json'
    mapjson=json.loads(binary(map_path))
    layouts=json.loads(binary('data/layouts/layouts.json'))['layouts']; layout=next(x for x in layouts if x.get('id')==mapjson['layout'])
    width,height=layout['width'],layout['height']; borderW,borderH=layout['border_width'],layout['border_height']; blocks=units(layout['blockdata_filepath'],'H'); border=units(layout['border_filepath'],'H')
    if len(blocks)!=width*height or len(border)!=borderW*borderH:raise ValueError('Grid dimensions mismatch')
    bases=['data/tilesets/'+TILESETS[layout[k]] for k in ['primary_tileset','secondary_tileset']]
    headers=binary('src/data/tilesets/headers.h').decode(); graphics_text=binary('src/data/tilesets/graphics.h').decode()+binary('src/graphics.c').decode()
    for k,base in zip(['primary_tileset','secondary_tileset'],bases):
        header=re.search(r'const struct Tileset '+layout[k]+r' =\s*\{(.*?)\n\};',headers,re.S).group(1)
        symbol=re.search(r'\.tiles = (\w+)',header).group(1)
        declared=re.search(r'\b'+symbol+r'\[\] = INCBIN_U32\(\"(.*?)\"\)',graphics_text).group(1)
        if declared!=base+'/tiles.4bpp.lz':raise ValueError('Source tileset binding differs')
    attrs=[units(p+'/metatile_attributes.bin','I') for p in bases]; mts=[units(p+'/metatiles.bin','H') for p in bases]
    if len(attrs[0])!=640 or any(len(m)!=len(a)*8 for m,a in zip(mts,attrs)):raise ValueError('Each metatile must have 8 u16 tile entries and one u32 attribute')
    graphics=[Image.open(RAW/(p+'/tiles.png')) for p in bases]
    if any(i.mode!='P' or i.width%8 or i.height%8 or i.getextrema()[1]>15 for i in graphics):raise ValueError('Only indexed 4-bit tiled images supported')
    rules=binary('tileset_rules.mk').decode()
    emitted=[]
    for base,img in zip(bases,graphics):
        rule=re.search(re.escape('$(TILESETGFXDIR)/'+base.removeprefix('data/tilesets/')+'/tiles.4bpp:')+r'.*?\n\t[^\n]*?-num_tiles (\d+)',rules)
        emitted.append(int(rule.group(1)) if rule else img.width*img.height//64)
        if emitted[-1]>img.width*img.height//64:raise ValueError('Emitted graphics exceeds PNG slots')
    palettes=[]
    for i in range(13):
        lines=binary(bases[0 if i<7 else 1]+f'/palettes/{i:02d}.pal').decode().splitlines()
        if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19:raise ValueError('Invalid JASC palette')
        # GBA's 5-bit channel values are expanded by bit replication for display.
        vals=[tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in s.split()) for s in lines[3:]]
        if any(len(v)!=3 for v in vals):raise ValueError('Invalid RGB color')
        palettes.append(vals)
    palettes[0][0]=(0,0,0)
    behaviorNames={int(v,16):n for n,v in re.findall(r'^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)',binary('include/constants/metatile_behaviors.h').decode(),re.M)}
    def attr_and_tiles(mid):
        s=0 if mid<640 else 1; local=mid if s==0 else mid-640
        if local>=len(attrs[s]):raise ValueError(f'Missing metatile {mid}')
        return attrs[s][local],mts[s][local*8:local*8+8],bases[s],local
    def movement(b):
        if b==0:return 'normal'
        if b==2:return 'grassUnsupported'
        if b in range(0x38,0x3c):return 'ledgeUnsupported'
        if b==0x69:return 'doorUnsupported'
        if 0x62<=b<=0x65:return 'arrowWarpUnsupported'
        if 0x6c<=b<=0x6f:return 'stairWarpUnsupported'
        if b in [0x60,0x61,0x66,0x67,0x68,0x71]:return 'warpUnsupported'
        if b in [0x10,0x11,0x12,0x13,0x15]:return 'waterUnsupported'
        if b==0x84:return 'signBlocked'
        return 'unsupported'
    def cell(raw,x,y):
        mid=raw&1023; a,t,path,local=attr_and_tiles(mid); b=a&511
        return {'x':x,'y':y,'rawBlock':raw,'metatileId':mid,'collision':(raw>>10)&3,'elevation':raw>>12,'rawAttributes':a,'behaviorId':b,'behaviorName':behaviorNames.get(b,f'UNKNOWN_0x{b:03X}'),'terrain':(a>>9)&31,'attribute2':(a>>14)&15,'attribute3':(a>>18)&63,'encounterType':(a>>24)&7,'attribute5':(a>>27)&3,'layerType':(a>>29)&3,'attribute7':a>>31,'movement':movement(b),'sourceRef':source(path+'/metatile_attributes.bin',f'metatile_{local:03x}')}
    cells=[cell(b,i%width,i//width) for i,b in enumerate(blocks)]
    layers=[Image.new('RGBA',(width*16,height*16),(0,0,0,0)) for _ in range(3)] # bottom BG3, middle BG2, top BG1
    tilecache={}
    def tile(entry):
        if entry in tilecache:return tilecache[entry]
        tid=entry&1023; p=entry>>12; s=0 if tid<640 else 1; local=tid if s==0 else tid-640
        # Honor per-tileset -num_tiles overrides; padding pixels are not initialized VRAM.
        if p>=13 or local>=emitted[s]:raise ValueError(f'Uninitialized tile/palette reference: {entry:04x}')
        img=graphics[s]; tile=Image.new('RGBA',(8,8)); pixels=[]
        for y in range(8):
            for x in range(8):
                ix=7-x if entry&0x400 else x; iy=7-y if entry&0x800 else y
                index=img.getpixel(((local%(img.width//8))*8+ix,(local//(img.width//8))*8+iy))
                pixels.append((*palettes[p][index],0 if index==0 else 255))
        tile.putdata(pixels);tilecache[entry]=tile;return tile
    for c in cells:
        a,t,_,_=attr_and_tiles(c['metatileId']); lt=c['layerType']
        # Exact field_camera.c DrawMetatile mapping; transparent entries remain real tile 0.
        if lt==0: groups=[[0x3014]*4,t[:4],t[4:]]
        elif lt==1:groups=[t[:4],t[4:],[0]*4]
        elif lt==2:groups=[t[:4],[0]*4,t[4:]]
        else:raise ValueError('Unsupported reserved metatile layer type 3')
        for layer,entries in zip(layers,groups):
            for k,e in enumerate(entries):layer.paste(tile(e),(c['x']*16+(k%2)*8,c['y']*16+(k//2)*8))
    composite=Image.new('RGBA',layers[0].size,(0,0,0,255))
    for layer in layers:composite=Image.alpha_composite(composite,layer)
    out.mkdir(parents=True,exist_ok=True)
    artifacts=[]
    for artifact_name,img in zip(['bottom.png','middle.png','top.png','map.png'],layers+[composite]):
        img.save(out/artifact_name,optimize=False,compress_level=9)
        artifacts.append({'path':artifact_name,'width':img.width,'height':img.height})
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
    embedded=[]
    for p in [map_path,f'data/maps/{name}/scripts.inc',f'data/maps/{name}/text.inc','data/layouts/layouts.json','include/constants/metatile_behaviors.h',layout['blockdata_filepath'],layout['border_filepath']]+[base+'/metatile_attributes.bin' for base in bases]:
        embedded.append({'path':p,'base64':base64.b64encode(binary(p)).decode()})
    # Technical smoke-test anchor only: first ordinary elevation-3 neighbor of source entrance warp.
    warp=mapjson['warp_events'][0]
    neighbors=[(warp['x']+dx,warp['y']+dy) for dx,dy in ([(0,1)] if name=='PalletTown' else [(0,-1),(-1,0),(0,1),(1,0)])]
    sx,sy=next((x,y) for x,y in neighbors if 0<=x<width and 0<=y<height and cells[y*width+x]['behaviorId']==0 and cells[y*width+x]['collision']==0 and cells[y*width+x]['elevation']==3)
    world={'schemaVersion':1,'stableId':stable_id,'sourceRef':source(map_path,mapjson['id']),'metadata':{'repository':lock['repository'],'commit':lock['commit'],'rights':lock['rights'],'scope':'Bounded five-map static conversion. Original events/scripts/text retained without executing upstream code; runtime script/NPC/warp behavior belongs to the event engine. No water traversal or tile animation.','renderProfile':'Pinned source indexed PNG tiles, GBA RGB555 expanded to RGB888; no display color correction, tint, or animation. BG3 bottom, BG2 middle, BG1 top.'},'width':width,'height':height,'tileSize':16,'borderWidth':borderW,'borderHeight':borderH,'texturePath':'map.png','artifacts':artifacts,'cells':cells,'borderCells':[cell(b,i%borderW,i//borderW) for i,b in enumerate(border)],'embeddedSource':embedded,'technicalSpawn':{'x':sx,'y':sy,'elevation':3,'reason':'Technical safe neighbor of the first source warp; not an original story-start claim.'},'events':mapjson}
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
