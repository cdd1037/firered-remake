"""Compile live Rocket Hideout elevator destinations and interactive map patches."""
import io
import re
from collections import defaultdict
from PIL import Image
from source_features import script_blocks, feature_constants
from map_source import tileset_bindings


def patch_image(src, primary, secondary, mid):
    bases = ['data/tilesets/' + primary, 'data/tilesets/' + secondary]
    bank = int(mid >= 640); local = mid - bank * 640
    attributes = src.units(bases[bank] + '/metatile_attributes.bin', 'I')[local]
    entries = src.units(bases[bank] + '/metatiles.bin', 'H')[local * 8:local * 8 + 8]
    headers = src.text('src/data/tilesets/headers.h')
    graphics = src.text('src/data/tilesets/graphics.h')
    bindings = tileset_bindings(src)
    graphics_bases = []
    for base in (primary,secondary):
        symbol = next(k for k,v in bindings.items() if v == base)
        body = re.search(r'const struct Tileset ' + symbol + r' =\s*\{(.*?)\};',headers,re.S)[1]
        tile_symbol = re.search(r'\.tiles = (\w+)',body)[1]
        declaration = re.search(r'\b' + tile_symbol + r'\[\]\s*=\s*INCBIN_U32\("(.*?)"\)',graphics)[1]
        graphics_bases.append(declaration.removesuffix('/tiles.4bpp.lz'))
    images = [Image.open(io.BytesIO(src.data[b + '/tiles.png'])) for b in graphics_bases]
    palettes = []
    for i in range(13):
        lines = src.text(graphics_bases[int(i >= 7)] + f'/palettes/{i:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL','0100','16'] or len(lines) != 19: raise ValueError('Invalid patch palette')
        palettes.append([tuple(((int(v) >> 3) << 3) | (int(v) >> 5) for v in line.split()) for line in lines[3:]])
    palettes[0][0] = (0,0,0)
    groups = {0:[[0x3014]*4,entries[:4],entries[4:]],1:[entries[:4],entries[4:],[0]*4],2:[entries[:4],[0]*4,entries[4:]]}[(attributes >> 29)&3]
    result = Image.new('RGBA',(16,16),(0,0,0,255))
    for group in groups:
        layer = Image.new('RGBA',(16,16))
        for k, entry in enumerate(group):
            tid = entry & 1023; image = images[int(tid >= 640)]; tid -= 640 if tid >= 640 else 0
            for y in range(8):
                for x in range(8):
                    ix = 7-x if entry & 0x400 else x; iy = 7-y if entry & 0x800 else y
                    color = image.getpixel((tid % (image.width//8)*8+ix,tid//(image.width//8)*8+iy))
                    layer.putpixel((k%2*8+x,k//2*8+y),(*palettes[entry>>12][color],0 if color==0 else 255))
        result = Image.alpha_composite(result,layer)
    return attributes,result


def compile_hideout(src, generated):
    names = ['CeladonCity_GameCorner','RocketHideout_B1F','RocketHideout_B4F','RocketHideout_Elevator']
    scripts = script_blocks(src,names)
    constants = feature_constants(src,[],scripts)['values']
    blocks = {b['label']:b for b in scripts['blocks']}
    patches = []
    groups = defaultdict(set)
    bindings = tileset_bindings(src)
    layouts = {l.get('id'):l for l in src.json('data/layouts/layouts.json')['layouts']}
    pairs = {}
    for name in names:
        path = 'data/maps/'+name+'/scripts.inc'
        layout = layouts[src.json('data/maps/'+name+'/map.json')['layout']]
        pair = tuple(bindings[layout[k]] for k in ('primary_tileset','secondary_tileset'))
        for label,block in sorted(blocks.items()):
            if block['sourceRef']['path'] != path: continue
            cells = []
            for line in block['lines']:
                m = re.fullmatch(r'\s*setmetatile (\d+), (\d+), (\w+), ([01])',line['text'])
                if not m: continue
                x,y,symbol,collision = int(m[1]),int(m[2]),m[3],int(m[4])
                cells.append(dict(x=x,y=y,metatile=symbol,metatileId=constants[symbol]['value'],collision=collision))
                groups[pair].add(symbol); pairs[symbol] = pair
            if cells: patches.append(dict(label=label,cells=cells))
    atlas = []
    directory = generated/'maps/regions/rocket-hideout/patches'; directory.mkdir(parents=True,exist_ok=True)
    for pair,symbols in sorted(groups.items()):
        image = Image.new('RGBA',(len(symbols)*16,16))
        name = pair[1].split('/')[-1]+'.png'
        for i,symbol in enumerate(sorted(symbols)):
            mid = constants[symbol]['value']; attr,tile = patch_image(src,*pair,mid)
            image.paste(tile,(i*16,0))
            atlas.append(dict(symbol=symbol,metatileId=mid,rawAttributes=attr,atlas='assets/patches/'+name,x=i*16,y=0))
        image.save(directory/name)
    choose = blocks['RocketHideout_Elevator_EventScript_ChooseFloor']
    menu = []
    for line in choose['lines']:
        m = re.fullmatch(r'\s*case (\d+), (RocketHideout_Elevator_EventScript_To(\w+))',line['text'])
        if not m: continue
        code = '\n'.join(r['text'] for r in blocks[m[2]]['lines'])
        warp = re.search(r'setdynamicwarp (MAP_\w+), 255, (\d+), (\d+)',code)
        floor = re.search(r'setvar VAR_ELEVATOR_FLOOR, (\d+)',code)
        if not warp or not floor: raise ValueError('Missing elevator destination')
        menu.append(dict(index=int(m[1]),label=m[3],value=int(floor[1]),map=warp[1],x=int(warp[2]),y=int(warp[3])))
    if len(patches)!=6 or len(menu)!=3: raise ValueError('Hideout feature scope changed')
    return {'maps/regions/rocket-hideout/rocket-hideout.json':dict(metatilePatches=patches,patchAtlas=atlas,elevator=dict(menu=menu))}
