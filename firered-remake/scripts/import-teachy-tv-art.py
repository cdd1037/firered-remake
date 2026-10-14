#!/usr/bin/env python3
"""Decode authenticated Teachy TV overlays, host and original Route1 backdrop.
Uses the adjacent authored standard-library codec; no upstream build or ROM.
"""
import argparse
import sys
sys.dont_write_bytecode = True
import importlib.util
import json
from pathlib import Path
import struct
import teachy_tv_codec as codec

ROOT=Path(__file__).resolve().parent.parent
MANIFEST_SHA256='b2e9452ae639b28cb55cc6d97b93161ceb437234fd07e263fed0d7146e5ff0a2'
spec=importlib.util.spec_from_file_location('teachy_catalog',ROOT/'scripts/import-teachy-tv.py')
catalog=importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalog)


def convert(sources):
    get=lambda p:sources[p]
    tiles=codec.tile_list(get('graphics/teachy_tv/tiles.png'))[:233]
    palettes=codec.colors(codec.indexed_png(get('graphics/teachy_tv/tiles.png'))[3])
    catalog.need(len(palettes)==64 and palettes==codec.palette(get('graphics/teachy_tv/palettes.pal')),'teachy_tv_palettes')
    # Source explicitly replaces entry zero with RGB_BLACK after loading palettes.
    palettes[0]=(0,0,0)
    artifacts={}
    def save(name,w,h,pixels):
        artifacts[name]=dict(bytes=codec.png(w,h,pixels),width=w,height=h)
    def tilemap(raw,w=240,h=160):
        catalog.need(len(raw)==2048,'teachy_tv_tilemap_bytes')
        values=struct.unpack('<1024H',raw);out=codec.canvas(w,h)
        for y in range(h//8):
            for x in range(w//8):
                value=values[y*32+x];tile=value&1023;bank=value>>12
                catalog.need(tile<len(tiles) and bank<4,'teachy_tv_tilemap_reference')
                codec.blit(out,w,codec.tile_rgba(tiles[tile],palettes[bank*16:bank*16+16],bool(value&1024),bool(value&2048)),8,8,x*8,y*8)
        return out
    save('screen-frame.png',240,160,tilemap(get('graphics/teachy_tv/screen.bin')))
    save('title-layer.png',240,160,tilemap(get('graphics/teachy_tv/title.bin')))
    end=codec.canvas(64,16)
    for i,tile in enumerate([*range(0xD1,0xD9),*range(0xE1,0xE9)]):
        codec.blit(end,64,codec.tile_rgba(tiles[tile],palettes[:16]),8,8,(i%8)*8,(i//8)*8)
    save('end-graphic.png',64,16,end)
    save('static-tile.png',8,8,codec.tile_rgba(tiles[0x1F],palettes[48:64]))
    for name,image,palette in [
        ('host-sheet.png','graphics/object_events/pics/people/teachy_tv_host.png','graphics/object_events/palettes/npc_white.pal'),
        ('pokedude-back-sheet.png','graphics/trainers/back_pics/pokedude_back_pic.png','graphics/trainers/palettes/pokedude_back_pic.pal'),
        ('teachy-tv-icon.png','graphics/items/icons/teachy_tv.png','graphics/items/icon_palettes/teachy_tv.pal'),
        ('tall-grass-sheet.png','graphics/field_effects/pics/tall_grass.png','graphics/field_effects/palettes/general_1.pal')]:
        w,h,indices,_=codec.indexed_png(get(image));save(name,w,h,codec.rgba(indices,codec.palette(get(palette))))
    w,h,indices,_=codec.indexed_png(get('graphics/interface/red_arrow_other.png'))
    save('scroll-arrows.png',w,h,codec.rgba(indices,codec.palette(get('graphics/interface/red_arrow.pal'))))

    # Reproduce TeachyTvLoadBg3Map's 16x9 source metatile crop and palette remap.
    # Each composite tile retains the BASE layer's palette even when its overlay
    # is nonzero, exactly as TeachyTvPushBackNewMapPalIndexArrayEntry does.
    layouts=json.loads(get('data/layouts/layouts.json'))['layouts']
    route=next(x for x in layouts if x.get('name')=='Route1_Layout')
    catalog.need((route['width'],route['height'])==(24,40),'teachy_tv_route1_layout')
    primary='data/tilesets/primary/general/'
    secondary='data/tilesets/secondary/pallet_town/'
    map_tiles=codec.tile_list(get(primary+'tiles.png'))+codec.tile_list(get(secondary+'tiles.png'))
    catalog.need(len(codec.tile_list(get(primary+'tiles.png')))==640,'teachy_tv_primary_tiles')
    entries=struct.unpack('<960H',get('data/layouts/Route1/map.bin'))
    unique=[];pal_indices=[];composites=[];tilemap_entries=[0]*1024
    for y in range(9):
        for x in range(16):
            mid=entries[8+(y+6)*24+x]&1023
            if mid not in unique:
                unique.append(mid)
                base=primary if mid<640 else secondary
                local=mid if mid<640 else mid-640
                meta=struct.unpack_from('<8H',get(base+'metatiles.bin'),local*16)
                four=[]
                for k in range(4):
                    combined=bytearray(64)
                    for value in [meta[k],meta[k+4]]:
                        tile=map_tiles[value&1023]
                        for ty in range(8):
                            for tx in range(8):
                                value_at=tile[(7-ty if value&2048 else ty)*8+(7-tx if value&1024 else tx)]
                                if value_at:combined[ty*8+tx]=value_at
                    four.append(bytes(combined))
                composites.append((meta,four))
            block=unique.index(mid);meta,four=composites[block]
            for k in range(4):
                bank=meta[k]>>12
                if bank not in pal_indices:pal_indices.append(bank)
                tilemap_entries[(y*2+k//2)*32+x*2+k%2]=(15-pal_indices.index(bank))*4096+4*block+k
    catalog.need(len(pal_indices)<=12,'teachy_tv_route_palette_collision')
    actual_palettes=[palettes[i:i+16] for i in range(0,64,16)]+[[(0,0,0)]*16 for _ in range(12)]
    for i,bank in enumerate(pal_indices):
        path=(primary if bank<7 else secondary)+'palettes/'+str(bank).zfill(2)+'.pal'
        actual_palettes[15-i]=codec.palette(get(path))
    composite_tiles=[tile for _,four in composites for tile in four]
    out=codec.canvas(256,256)
    for y in range(32):
        for x in range(32):
            value=tilemap_entries[y*32+x]
            codec.blit(out,256,codec.tile_rgba(composite_tiles[value&1023],actual_palettes[value>>12]),8,8,x*8,y*8)
    save('route1-background.png',256,256,out)
    return artifacts,dict(palettes=palettes,hostFrameWidth=16,hostFrameHeight=32,hostFrameCount=9,
        backFrameWidth=64,backFrameHeight=64,backFrameCount=4,initialBackgroundScroll=dict(x=-16,y=40),
        route1Crop=dict(x=8,y=6,width=16,height=9),route1PaletteOrder=pal_indices)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--git-dir',type=Path,required=True)
    parser.add_argument('--output',type=Path,default=ROOT/'content/generated/teachy-tv-art')
    args=parser.parse_args()
    lock,sources=catalog.read_sources(args.git_dir,ROOT/'scripts/teachy-tv.lock.json')
    artifacts,contract=convert(sources)
    manifest=dict(schemaVersion=1,profile='firered-teachy-tv-art-v1',sourceCommit=catalog.COMMIT,
        sourceLockSha256=catalog.LOCK_SHA256,contract=contract,assets=[])
    catalog.need(not any(p.is_symlink() for p in [args.output,*args.output.parents]),'teachy_tv_art_symlink')
    for name,value in artifacts.items():
        catalog.need(not (args.output/name).is_symlink(),'teachy_tv_art_symlink')
        manifest['assets'].append(dict(path=name,bytes=len(value['bytes']),sha256=catalog.sha(value['bytes']),width=value['width'],height=value['height']))
    catalog.need(catalog.sha(catalog.encoded(manifest))==MANIFEST_SHA256,'teachy_tv_art_manifest_hash')
    args.output.mkdir(parents=True,exist_ok=True)
    for name,value in artifacts.items():(args.output/name).write_bytes(value['bytes'])
    (args.output/'teachy-tv-art.json').write_bytes(catalog.encoded(manifest))
    print(json.dumps(dict(assets=len(artifacts),bytes=sum(len(v['bytes']) for v in artifacts.values()),manifestSha256=catalog.sha(catalog.encoded(manifest)))))


if __name__=='__main__':main()
