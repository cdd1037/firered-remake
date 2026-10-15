#!/usr/bin/env python3
"""Rebuild original, region-local Fly map visuals from pinned private source blobs."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse,  importlib.util, json, pathlib, re, struct, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-fly-map-v1'
BASE = 'graphics/region_map/'
REGIONS = ('kanto', 'sevii_123', 'sevii_45', 'sevii_67')
PATHS = frozenset(['src/region_map.c', 'src/strings.c', 'src/data/region_map/region_map_sections.json',
    *['src/data/region_map/region_map_layout_' + r + '.h' for r in REGIONS],
    *[BASE + p for p in ('region_map.png', 'region_map.pal', 'background.png', 'background.bin',
                        'top_bar.pal', 'fly_icon.png', 'misc_icon.pal')], *[BASE + r + '.bin' for r in REGIONS]])
spec = importlib.util.spec_from_file_location('fly_map_pixels', ROOT / 'scripts/first-sevii-town-map-pixels.py')
pixels = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pixels)
def canonical(data): return (json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    target = root
    if relative is not None:
        p = pathlib.PurePosixPath(relative)
        if not relative or p.is_absolute() or '..' in p.parts or '\\' in relative or str(p) != relative: raise ValueError('Unsafe path')
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]): raise ValueError('Symlink path')
    return target



def convert( source):
    code = source['src/region_map.c'].decode(); outputs = {}; assets = []
    for needle in ('[MAPPERM_HAS_SWITCH_BUTTON]    = FALSE,', '[MAPPERM_HAS_FLY_DESTINATIONS] = TRUE',
        'DecompressAndCopyTileDataToVram(1, sBackground_Gfx, 0, 0, 0);',
        'ANIMCMD_FRAME(0, 30),\n    ANIMCMD_FRAME(4, 60),',
        'FillBgTilemapBufferRect_Palette0(0, 0x003, 13, 11, 3, 2);',
        'FillBgTilemapBufferRect_Palette0(0, 0x003, 21, 16, 3, 3);',
        'mapsec != MAPSEC_ROUTE_10_POKECENTER)\n        offset = 2;',
        'spriteId = CreateSprite(&template, 8 * x + 36 + offset, 8 * y + 36 + offset, 3);',
        'FillWindowPixelBuffer(WIN_TOPBAR_LEFT, PIXEL_FILL(15));',
        'FillWindowPixelBuffer(WIN_TOPBAR_RIGHT, PIXEL_FILL(15));'):
        if needle not in code: raise ValueError('Source semantic differs: ' + needle)
    def add(identifier, w, h, rgba, paths):
        if len(rgba) != w*h*4 or not 0 < w*h <= 38400: raise ValueError('Asset dimensions')
        outputs[identifier + '.png'] = pixels.compressed_png(w, h, rgba)
        assets.append(dict(id=identifier, width=w, height=h, texturePath=identifier + '.png', sourcePaths=paths))
    mp = pixels.palette(source[BASE+'region_map.pal'], 80); tp = pixels.palette(source[BASE+'top_bar.pal'], 16)
    pals = {i:mp[i*16:(i+1)*16].copy() for i in range(5)}
    pals[2] = [tuple((((((c>>3)<<8)//100)*95)>>8) for c in rgb) for rgb in pals[2]]
    pals[2] = [tuple((c<<3)|(c>>2) for c in rgb) for rgb in pals[2]]; pals[2][15] = mp[47]
    for p in pals.values(): p[0] = tp[15]
    tiles = pixels.tile_list(source[BASE+'region_map.png'])
    # InitRegionMapBgs clears VRAM. The background tilemap references zeroed tile 14
    # beyond its five loaded tiles; reproduce that zero tile rather than other art.
    background_tiles = pixels.tile_list(source[BASE+'background.png'])
    if len(background_tiles) != 5: raise ValueError('Background tile profile')
    background_tiles += [bytes(64)] * 10
    background = pixels.canvas(240,160,(*tp[15],255))
    pixels.overlay(background, pixels.render_tilemap(background_tiles, struct.unpack('<600H',source[BASE+'background.bin']), pals))
    layers = [pixels.render_tilemap(tiles, struct.unpack('<600H',source[BASE+r+'.bin']), pals) for r in REGIONS]
    if any(l[3::4] != layers[0][3::4] for l in layers): raise ValueError('Region transparency masks differ')
    surround = bytearray(background)
    for i in range(0,len(surround),4):
        if layers[0][i+3]: surround[i:i+4] = b'\0'*4
    # Nonanimated Fly top-bar windows have opaque palette entry 15 beneath text.
    for left in (144,192):
        pixels.blit(surround,240,pixels.canvas(40,16,(*tp[15],255)),40,16,left,0)
    add('fly-surround',240,160,surround,[BASE+p for p in ('background.png','background.bin','region_map.png','region_map.pal','top_bar.pal',*[r+'.bin' for r in REGIONS])])
    for index,identifier in ((2,'fly-sevii45'),(3,'fly-sevii67')):
        frame=bytearray(background);pixels.overlay(frame,layers[index]);pixels.overlay(frame,surround)
        add(identifier,240,160,frame,[BASE+p for p in ('background.png','background.bin','region_map.png','region_map.pal','top_bar.pal',REGIONS[index]+'.bin')])
    # FillBgTilemapBufferRect_Palette0 keeps each cell's original palette bank
    # and substitutes tile 3. Event areas are bank 0 in these original maps.
    for identifier,region,x,y,w,h in (('fly-hide-navel',2,13,11,3,2),('fly-hide-birth',3,21,16,3,3)):
        entries=struct.unpack('<600H',source[BASE+REGIONS[region]+'.bin'])
        if any(entries[yy*30+xx]>>12 != 0 for yy in range(y,y+h) for xx in range(x,x+w)): raise ValueError('Event mask palette changed')
        frame=pixels.canvas(w*8,h*8)
        for yy in range(h):
            for xx in range(w): pixels.blit(frame,w*8,pixels.tile_rgba(tiles[3],pals[0]),8,8,xx*8,yy*8)
        add(identifier,w*8,h*8,frame,[BASE+'region_map.png',BASE+'region_map.pal',BASE+REGIONS[region]+'.bin','src/region_map.c'])
    pal=pixels.palette(source[BASE+'misc_icon.pal'],16);w,h,indices,_=pixels.indexed_png(source[BASE+'fly_icon.png'])
    if (w,h)!=(16,32):raise ValueError('Fly icon profile differs')
    for index in range(2):add('fly-icon-'+str(index),16,16,pixels.rgba(indices[index*256:(index+1)*256],pal),[BASE+'fly_icon.png',BASE+'misc_icon.pal'])
    sections=json.loads(source['src/data/region_map/region_map_sections.json'])['map_sections'];by={s['id']:s for s in sections}
    flags=dict(re.findall(r'case (MAPSEC_\w+):\s*(?:if \(!GetRegionMapPermission\(MAPPERM_HAS_FLY_DESTINATIONS\)\)\s*return MAPSECTYPE_NONE;\s*)?return FlagGet\((FLAG_\w+)\)',code))
    fly_body=code.split('static u8 GetMapsecType(u8 mapsec)\n{')[1].split('\nstatic u8 GetDungeonMapsecType',1)[0]
    fly_flags={section:flag for section,flag in flags.items() if 'case '+section+':' in fly_body}
    if len(fly_flags)!=20:raise ValueError('Fly destination flag inventory differs')
    regions=[]
    for region,key in enumerate(REGIONS):
        rows=re.findall(r'\{(MAPSEC_[A-Z0-9_, ]+)\}',source['src/data/region_map/region_map_layout_'+key+'.h'].decode())
        grid=[re.findall(r'MAPSEC_[A-Z0-9_]+',r) for r in rows]
        if len(grid)!=30 or any(len(r)!=22 for r in grid):raise ValueError('Region grid differs')
        cells=[];dungeons=[];destinations=[]
        for y in range(15):
            for x in range(22):
                a=grid[y][x];b=grid[y+15][x]
                cells.append(dict(x=x,y=y,sectionId=a,name=by.get(a,{}).get('name',''),dungeonSectionId=b,dungeonName=by.get(b,{}).get('name','')))
                if a in fly_flags:destinations.append(dict(x=x,y=y,centerX=36+x*8,centerY=36+y*8,sectionId=a,visitedFlag=fly_flags[a],visibleFlag=None))
                if b!='MAPSEC_NONE':
                    offset=2 if a in fly_flags and a!='MAPSEC_ROUTE_10_POKECENTER' else 0
                    dungeons.append(dict(x=x,y=y,centerX=36+x*8+offset,centerY=36+y*8+offset,sectionId=b,visitedFlag=flags[b],visibleFlag='FLAG_SYS_CAN_LINK_WITH_RS' if b=='MAPSEC_CERULEAN_CAVE' else None))
        regions.append(dict(index=region,cells=cells,dungeonIcons=dungeons,destinationIcons=destinations))
    sevii=code.split('static const u8 sSeviiMapsecs[3][30] = {')[1].split('\n};',1)[0]
    region_sections={}
    for index,body in enumerate(re.findall(r'\[REGIONMAP_SEVII\w+ - 1\]\s*=\s*\{([^}]+)\}',sevii),1):
        for section in re.findall(r'MAPSEC_\w+',body):
            if section!='MAPSEC_NONE':region_sections[section]=index
    override_body=code.split('static void GetPlayerPositionOnRegionMap_HandleOverrides(void)\n{')[1].split('\nstatic u8 GetSelectedMapSection',1)[0]
    overrides=[]
    for match in re.finditer(r'((?:    case MAPSEC_\w+:\n)+)        sMapCursor->x = (\d+);\n        sMapCursor->y = (\d+);\n        break;',override_body):
        for section in re.findall(r'MAPSEC_\w+',match[1]):overrides.append(dict(sectionId=section,sourceMap=None,x=int(match[2]),y=int(match[3])))
    for source_map,x,y in re.findall(r'MAP_NUM\((MAP_ROUTE21_\w+)\)\)\n        \{\n            sMapCursor->x = (\d+);\n            sMapCursor->y = (\d+);',override_body):
        overrides.append(dict(sectionId='MAPSEC_ROUTE_21',sourceMap=source_map,x=int(x),y=int(y)))
    if len(overrides)!=27:raise ValueError('Player override inventory differs: '+str(len(overrides)))
    positions=[dict(sectionId=s['id'],region=region_sections.get(s['id'],0),x=s['x'],y=s['y'],width=s['width'],height=s['height']) for s in sections if all(k in s for k in ('x','y','width','height'))]
    texts={key:re.search(r'const u8 '+symbol+r'\[\] = _\("(.*)"\);',source['src/strings.c'].decode())[1] for key,symbol in [('move','gText_RegionMap_DPadMove'),('ok','gText_RegionMap_AButtonOK'),('cancel','gText_RegionMap_AButtonCancel'),('space','gText_RegionMap_Space')]}
    outputs['fly-map.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        assets=assets,regions=regions,sections=positions,positionOverrides=overrides,uiTexts=texts,flyIconFrameTicks=[30,60],
        switchAvailable=False,sourceRefs=['InitRegionMapType','LoadRegionMapGfx','BufferRegionMapBg','GetMapsecType','GetDungeonMapsecType','CreateFlyIcons','CreateDungeonIconSprite','Task_FlyMap','GetPlayerPositionOnRegionMap_HandleOverrides'],
        limitations=['Indoor/underground/secret-base projection needs saved escapeWarp/dynamicWarp and is rejected by this outdoor-only Fly helper.',
        'Original Birth Island map-name lookup uses the Navel Rock flag; this source quirk is preserved independently of its own background visibility flag.',
        'Static source pixels and data are authenticated; transition animation, native rendering and audible output are separate gates.']))
    outputs['manifest.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(source),assetCount=len(assets),upstreamScriptsExecuted=False,romDownloaded=False,
        reusedAssets={'town-map':['kanto','cursor-0','cursor-1','player-red','player-leaf','dungeon-visited','dungeon-unvisited','keypad-a','keypad-dpad-any'],
                      'first-sevii-town-map':['sevii123-locked']},
        outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
    return outputs

def pack_backgrounds(outputs, generated):
    """Bake six flag-dependent maps and exact name-window dimming once."""
    import io
    from PIL import Image
    def image(data):
        return Image.open(io.BytesIO(data)).convert('RGBA')
    base = [image((generated/'town-map/kanto.png').read_bytes()),
            image((generated/'first-sevii-town-map/sevii123-locked.png').read_bytes()),
            image(outputs['fly-sevii45.png']), image(outputs['fly-sevii67.png'])]
    surround = image(outputs['fly-surround.png'])
    coefficient = json.loads((generated/'town-map/town-map.json').read_bytes())['layout']['nameWindowDarkeningCoefficient']
    atlas = Image.new('RGBA', (484, 972))
    backgrounds = []
    row = 0
    for region in range(4):
        for hidden in ([False] if region < 2 else [False, True]):
            frame = base[region].copy()
            frame.paste(surround, (0, 0), surround.getchannel('A').point(lambda a: 255 if a else 0))
            if hidden:
                mask = image(outputs['fly-hide-navel.png' if region == 2 else 'fly-hide-birth.png'])
                frame.paste(mask, (104, 88) if region == 2 else (168, 128), mask.getchannel('A').point(lambda a: 255 if a else 0))
            for dark in (False, True):
                tile = frame.copy()
                if dark:
                    data = bytearray(tile.tobytes())
                    for p in range(0, len(data), 4):
                        for c in range(3):
                            five = (data[p+c] * 31 + 127) // 255
                            five += (-five * coefficient) >> 4
                            data[p+c] = five * 255 // 31
                    tile = Image.frombytes('RGBA', tile.size, bytes(data))
                x, y = int(dark) * 242 + 1, row * 162 + 1
                atlas.paste(tile, (x, y))
                backgrounds.append(dict(mapRegion=region, hidden=hidden, dark=dark,
                    texturePath='backgrounds.png', region=dict(x=x,y=y,width=240,height=160)))
            row += 1
    from image_io import encode_png
    outputs['backgrounds.png'] = encode_png(atlas)
    doc = json.loads(outputs['fly-map.json']); doc['backgrounds'] = backgrounds
    doc['assets'] = [asset for asset in doc['assets'] if asset['id'].startswith('fly-icon-')]
    outputs['fly-map.json'] = canonical(doc)
    for name in ('fly-surround', 'fly-sevii45', 'fly-sevii67', 'fly-hide-navel', 'fly-hide-birth'):
        outputs.pop(name + '.png')
    manifest = json.loads(outputs['manifest.json']); manifest['assetCount'] = 2
    manifest['outputs'] = [dict(path=path) for path in sorted(outputs) if path != 'manifest.json']
    outputs['manifest.json'] = canonical(manifest)
    return outputs

def import_content(raw,out,repo=None):
    raw=safe(raw);out=safe(out)
    if raw.is_relative_to(out) or out.is_relative_to(raw):raise ValueError('Overlapping output directories')
    if repo is not None and any(p.is_relative_to(safe(repo)) or safe(repo).is_relative_to(p) for p in (raw,out)):raise ValueError('Outputs overlap source repository')
    lock_bytes=safe(ROOT / 'scripts/import-config/fly-map.json').read_bytes()
    lock=json.loads(lock_bytes)
    if (lock['repository'], lock['commit'], lock['profile'], lock['gameEdition'], lock['romRevision']) != ('pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0) or len(lock['files']) != len(PATHS) or {f['path'] for f in lock['files']} != PATHS:raise ValueError('Source profile differs')
    source={}
    for f in lock['files']:
        b=safe(safe(raw, f['path'])).read_bytes() if repo is None else subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+f['path']],timeout=30)
        source[f['path']]=b
    outputs=pack_backgrounds(convert(source), out.parent)
    material=([(raw,p,b) for p,b in source.items()]+[]) if repo is not None else []
    material += [(out,p,b) for p,b in outputs.items()]
    for root,p,b in material:
        target=safe(root,p);stage=safe(root,p+'.tmp');target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(source),assetCount=2,outputCount=len(outputs))

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path)
    p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/fly-map')
    a=p.parse_args();print(json.dumps(import_content(a.raw,a.out,a.reference_repo),sort_keys=True))
if __name__=='__main__':main()
