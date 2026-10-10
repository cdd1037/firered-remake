#!/usr/bin/env python3
"""Authenticated static Pokemon Storage System pixels and icon palettes.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-storage-art-v1'
LOCK_SHA256 = '56f010572588b32392d6ca95428fcb737d780e2e53fce33862282f3f2c5565ea'
MANIFEST_SHA256 = '5b2cab19c6636313ad7b0cdd02f4ecca6c5b2e6548f114656c445bdbb8198c88'
SPECIES = (('Bulbasaur',1),('Ivysaur',2),('Venusaur',3),('Charmander',4),('Charmeleon',5),('Charizard',6),
           ('Squirtle',7),('Wartortle',8),('Blastoise',9),('Caterpie',10),('Metapod',11),('Butterfree',12),
           ('Weedle',13),('Kakuna',14),('Beedrill',15),('Pidgey',16),('Pidgeotto',17),('Pidgeot',18),
           ('Rattata',19),('Raticate',20),('Pikachu',25),('Raichu',26),('Sandshrew',27),('Sandslash',28),
           ('Geodude',74),('Graveler',75),('Golem',76),('Onix',95),('Steelix',208))
PATHS = frozenset("""include/constants/species.h
include/pokemon_storage_system.h
include/pokemon_storage_system_internal.h
src/pokemon_storage_system_graphics.c
src/pokemon_storage_system_tasks.c
src/pokemon_storage_system_data.c
src/pokemon_storage_system_menu.c
src/pokemon_icon.c
src/graphics.c
src/data/graphics/pokemon.h
src/bg.c
src/tilemap_util.c
graphics_file_rules.mk
tools/gbagfx/gfx.c
tools/gbagfx/convert_png.c
graphics/pokemon_storage/menu.png
graphics/pokemon_storage/menu.bin
graphics/pokemon_storage/interface.pal
graphics/pokemon_storage/interface_no_display_mon.pal
graphics/pokemon_storage/scrolling_bg.png
graphics/pokemon_storage/scrolling_bg.bin
graphics/pokemon_storage/party_menu.bin
graphics/pokemon_storage/party_menu.pal
graphics/pokemon_storage/close_box_button.bin
graphics/pokemon_storage/pkmn_data.bin
graphics/pokemon_storage/party_slot_empty.bin
graphics/pokemon_storage/party_slot_filled.bin
graphics/pokemon_storage/misc2.pal
graphics/pokemon_storage/box_scroll_arrow.png
graphics/pokemon_storage/wallpapers/forest/tiles.png
graphics/pokemon_storage/wallpapers/forest/tilemap.bin""".splitlines()) | frozenset(
    f'graphics/pokemon/{name.lower()}/icon.png' for name,_ in SPECIES) | frozenset(
    f'graphics/pokemon/icon_palettes/icon_palette_{i}.pal' for i in range(3))
_spec = importlib.util.spec_from_file_location('storage_pixels', ROOT / 'scripts/import-lab-battle-ui.py')
pixels = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pixels)


def source_set_hash(files):
    return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n' for e in sorted(files, key=lambda e: e['path'])).encode('utf-8'))


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe storage art path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Storage art path escapes root')
    return target


def load_lock(path=None):
    path = path or ROOT / 'scripts/fetch-storage-art.lock.json'
    data = safe(path).read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed storage art source lock differs')
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('tree'),
            lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (
            1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Storage art source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Storage art source dependency set differs')
    return lock


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    if (len(data), sha(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
        raise ValueError('Storage art source hash differs: ' + entry['path'])


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                          lockSha256=LOCK_SHA256, fullSourceHash=source_set_hash(lock['files']),
                          inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Storage art raw manifest differs')
    else:
        # ls-tree is a read-only object identity check; source contents use git show only.
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
        if tree != TREE:
            raise ValueError('Storage art source tree differs')
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            row = subprocess.run(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', entry['path']],
                                 check=True, capture_output=True, timeout=30).stdout.decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Storage art input is not the pinned regular Git blob: ' + entry['path'])
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        verify(data, entry)
        sources[entry['path']] = data
    return sources


def reference(lock, sources, path, symbol, needle=None):
    entry = next(e for e in lock['files'] if e['path'] == path)
    result = dict(path=path, symbol=symbol, sha256=entry['sha256'])
    if needle is not None:
        source = sources[path].decode('utf-8')
        if source.count(needle) != 1:
            raise ValueError('Expected one source anchor: ' + symbol)
        result.update(startLine=source.count('\n', 0, source.index(needle)) + 1,
                      endLine=source.count('\n', 0, source.index(needle) + len(needle)) + 1)
    return result


def render_tilemap(tiles, data, width, height, palettes, base_tile=0):
    import struct
    if len(data) != width * height * 2:
        raise ValueError('Unexpected storage tilemap dimensions')
    result = pixels.canvas(width * 8, height * 8)
    for offset, entry in enumerate(struct.unpack('<' + str(width * height) + 'H', data)):
        number, bank = (entry & 1023) - base_tile, entry >> 12
        if not 0 <= number < len(tiles):
            raise ValueError('Storage tile outside source tiles')
        tile = tiles[number]
        palette = palettes.get(bank)
        if palette is None:
            if any(tile):
                raise ValueError('Missing storage palette bank')
            continue
        rgba = pixels.tile_rgba(tile, palette, bool(entry & 1024), bool(entry & 2048))
        pixels.blit(result, width * 8, rgba, 8, 8, (offset % width) * 8, (offset // width) * 8)
    return bytes(result)


def crop(rgba, width, x, y, w, h):
    if x < 0 or y < 0 or x+w > width or y+h > len(rgba)//width//4:
        raise ValueError('Storage crop outside image')
    return b''.join(rgba[((y+r)*width+x)*4:((y+r)*width+x+w)*4] for r in range(h))


def overlay(destination, width, source, source_width, source_height, x, y):
    if x < 0 or y < 0 or x+source_width > width or y+source_height > len(destination)//width//4:
        raise ValueError('Storage overlay outside image')
    for row in range(source_height):
        for col in range(source_width):
            start = (row*source_width+col)*4
            if source[start+3]:
                target = ((row+y)*width+col+x)*4
                destination[target:target+4] = source[start:start+4]


def convert(lock, sources):
    icons, assets, outputs = [], [], {}
    gp = 'src/pokemon_storage_system_graphics.c'
    tp = 'src/pokemon_storage_system_tasks.c'
    ip = 'src/pokemon_icon.c'
    def require(path, needle):
        if needle not in sources[path].decode():
            raise ValueError('Unsupported storage semantics: ' + needle)
    for path, needle in [(gp, '8 * (3 * j) + 100, 8 * (3 * i) + 44'),
                         (gp, 'CpuCopy32(GetMonIconTiles(species, TRUE), (void *)(OBJ_VRAM0) + offset * 32, 0x200);'),
                         (gp, 'template.paletteTag = PALTAG_MON_ICON_0 + gMonIconPaletteIndices[species];'),
                         (gp, '.shape = SPRITE_SHAPE(32x32)'),
                         (gp, 'CopyRectToBgTilemapBufferRect(2, tilemap, 0, 0, 20, 18, x, 2, 20, 18, 17, offset << 8, paletteNum);'),
                         (gp, 's16 paletteNum = (offset * 2) + 3;'),
                         (gp, 's16 x = ((gStorage->bg2_X / 8 + 10) + (direction * 24)) & 0x3F;'),
                         (gp, '{sWallpaperTiles_Forest,     sWallpaperTilemap_Forest,     *sWallpaperPalettes_Forest    }'),
                         ('src/pokemon_storage_system_menu.c', 'SetBoxWallpaper(boxId, boxId % (MAX_DEFAULT_WALLPAPER + 1));'),
                         ('src/bg.c', 'var = *src + tileOffset + (palette2 << 12);'),
                         (tp, '.baseTile = 0x100'),
                         (tp, 'LoadPalette(gPokeStorageInterface_Pal, BG_PLTT_ID(0), PLTT_SIZE_4BPP);'),
                         (tp, 'TilemapUtil_SetPos(TILEMAP_CLOSE_BUTTON, 21, 0);'),
                         (tp, 'TilemapUtil_SetRect(TILEMAP_PARTY_MENU, 0, 20, 12, 2);'),
                         ('graphics_file_rules.mk', '$(WALLPAPERGFXDIR)/forest/tiles.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 53 -Wnum_tiles'),
                         ('tools/gbagfx/convert_png.c', '% (1 << destBitDepth)')]:
        require(path, needle)
    icon_text = sources[ip].decode()
    palette_section = icon_text.split('const u8 gMonIconPaletteIndices[] = {',1)[1].split('};',1)[0]
    icon_section = icon_text.split('const u8 *const gMonIconTable[] = {',1)[1].split('};',1)[0]
    for name, sid in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        require('include/constants/species.h', '#define ' + symbol + ' ')
        if re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'\s*$', sources['include/constants/species.h'].decode(), re.M) is None:
            raise ValueError('Species ID differs')
        palette_match = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),',palette_section,re.M)
        icon_match = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',',icon_section,re.M)
        if palette_match is None or icon_match is None:
            raise ValueError('Missing icon palette/source mapping')
        palette_index = int(palette_match[1])
        if palette_index not in range(3):
            raise ValueError('Unsupported icon palette')
        image_path = f'graphics/pokemon/{stem}/icon.png'
        palette_path = f'graphics/pokemon/icon_palettes/icon_palette_{palette_index}.pal'
        if re.search(r"const u8\s+gMonIcon_" + name + r'\[\]\s*=\s*INCBIN_U8\(\s*"graphics/pokemon/' + stem + r'/icon\.4bpp"\);', sources['src/data/graphics/pokemon.h'].decode()) is None:
            raise ValueError('Missing icon graphics declaration: ' + name)
        width,height,indices,_ = pixels.indexed_png(sources[image_path])
        if (width,height) != (32,64):
            raise ValueError('Unsupported icon source frames')
        indices = indices[:32*32]
        colors = pixels.palette(sources[palette_path])
        rgba = pixels.rgba(indices, colors)
        png_path,rgba_path = f'icons/{stem}.png', f'icons/{stem}.rgba'
        outputs[png_path],outputs[rgba_path] = pixels.png(32,32,rgba),rgba
        icons.append(dict(speciesId='pret:'+symbol, sourceSpeciesId=sid, texturePath=png_path,
            rgbaPath=rgba_path, sha256=sha(outputs[png_path]), rgbaSha256=sha(rgba),
            width=32,height=32,sourceFrameIndex=0,sourceFrameCount=2,paletteIndex=palette_index,
            paletteVariant='shared-normal-shiny',transparentIndex=0,indexSha256=sha(indices),
            paletteRgb555=[(r>>3)|((g>>3)<<5)|((b>>3)<<10) for r,g,b in colors],
            sourceRefs=[reference(lock,sources,image_path,'gMonIcon_'+name),
                        reference(lock,sources,palette_path,'gMonIconPalettes['+str(palette_index)+']'),
                        reference(lock,sources,ip,'gMonIconPaletteIndices['+symbol+']',palette_match[0]),
                        reference(lock,sources,gp,'CreateMonIconSprite', 'template.paletteTag = PALTAG_MON_ICON_0 + gMonIconPaletteIndices[species];'),
                        reference(lock,sources,gp,'TryLoadMonIconTiles', 'CpuCopy32(GetMonIconTiles(species, TRUE), (void *)(OBJ_VRAM0) + offset * 32, 0x200);')]))
    def add(identifier,w,h,rgba,x=0,y=0):
        if len(rgba)!=w*h*4: raise ValueError('Asset size differs')
        png_path,rgba_path=identifier+'.png',identifier+'.rgba'
        outputs[png_path],outputs[rgba_path]=pixels.png(w,h,rgba),bytes(rgba)
        assets.append(dict(id=identifier,texturePath=png_path,rgbaPath=rgba_path,width=w,height=h,x=x,y=y,
                           sha256=sha(outputs[png_path]),rgbaSha256=sha(rgba)))
    base='graphics/pokemon_storage/'
    menu_tiles=pixels.tile_list(sources[base+'menu.png'])
    scrolling_palette=pixels.colors(pixels.indexed_png(sources[base+'scrolling_bg.png'])[3])
    palettes={0:pixels.palette(sources[base+'interface.pal']),1:pixels.palette(sources[base+'party_menu.pal']),
              2:pixels.palette(sources[base+'interface_no_display_mon.pal']),3:scrolling_palette}
    scrolling=render_tilemap(pixels.tile_list(sources[base+'scrolling_bg.png']),sources[base+'scrolling_bg.bin'],32,32,{3:scrolling_palette})
    scrolling=crop(scrolling,256,0,0,240,160)
    add('scrolling-background-phase0',240,160,scrolling)
    wallpaper_path=base+'wallpapers/forest/'
    wallpaper_palette=pixels.colors(pixels.indexed_png(sources[wallpaper_path+'tiles.png'])[3])
    wallpaper=render_tilemap(pixels.tile_list(sources[wallpaper_path+'tiles.png'])[:53],sources[wallpaper_path+'tilemap.bin'],20,18,
        {1:wallpaper_palette[:16],2:wallpaper_palette[16:32]})
    add('wallpaper-forest',160,144,wallpaper,80,16)
    frame=bytearray(crop(render_tilemap(menu_tiles,sources[base+'menu.bin'],32,20,palettes,256),256,0,0,240,160))
    party_map=bytearray(sources[base+'party_menu.bin'])
    for pos in range(1,6):
        index=12*(3*(pos-1)+1)+7
        for row in range(3):
            party_map[(index+row*12)*2:(index+row*12+4)*2]=sources[base+'party_slot_empty.bin'][row*8:row*8+8]
    party=render_tilemap(menu_tiles,party_map,12,22,palettes,256)
    add('party-panel',96,160,crop(party,96,0,0,96,160),80,0)
    add('party-slot-empty',32,24,render_tilemap(menu_tiles,sources[base+'party_slot_empty.bin'],4,3,palettes,256))
    add('party-slot-filled',32,24,render_tilemap(menu_tiles,sources[base+'party_slot_filled.bin'],4,3,palettes,256))
    # TilemapUtil_Draw copies source rows20..21 to the closed-panel destination (10,0).
    overlay(frame,240,crop(party,96,0,160,96,16),96,16,80,0)
    close=render_tilemap(menu_tiles,sources[base+'close_box_button.bin'],9,4,palettes,256)
    overlay(frame,240,crop(close,72,0,0,72,16),72,16,168,0)
    pkmn=render_tilemap(menu_tiles,sources[base+'pkmn_data.bin'],8,4,palettes,256)
    overlay(frame,240,crop(pkmn,64,0,16,64,16),64,16,8,0)
    # Original blank information window; text is added by the presentation consumer.
    pixels.blit(frame,240,bytes((*scrolling_palette[1],255))*72*56,72,56,0,88)
    add('interface-frame',240,160,frame)
    background=bytearray(scrolling)
    overlay(background,240,wallpaper,160,144,80,16)
    overlay(background,240,frame,240,160,0,0)
    add('box-background',240,160,background)
    arrow_w,arrow_h,arrow_indices,_=pixels.indexed_png(sources[base+'box_scroll_arrow.png'])
    if (arrow_w,arrow_h)!=(8,32): raise ValueError('Unexpected box arrow dimensions')
    arrow=pixels.rgba(arrow_indices,pixels.palette(sources[base+'misc2.pal']))
    add('box-arrow-left',8,16,crop(arrow,8,0,0,8,16),88,20)
    add('box-arrow-right',8,16,crop(arrow,8,0,16,8,16),224,20)
    display=dict(logicalWidth=240,logicalHeight=160,textureFilter='nearest',
        boxColumns=6,boxRows=5,boxIconWidth=32,boxIconHeight=32,boxSlotSpacing=24,
        boxFirstIconCenter=dict(x=100,y=44),boxFirstIconTopLeft=dict(x=84,y=28),
        boxIconCenters=[dict(x=100+24*(i%6),y=44+24*(i//6)) for i in range(30)],
        partyIconCenters=[dict(x=104,y=64)]+[dict(x=152,y=16+24*(i-1)) for i in range(1,6)],
        wallpaper=dict(id='wallpaper-forest',x=80,y=16,width=160,height=144,sourceWallpaperId=0),
        backgroundAssetId='box-background',frameAssetId='interface-frame',partyPanelAssetId='party-panel',
        displayMonCenter=dict(x=40,y=48),displayMonTopLeft=dict(x=8,y=16),
        boxTitleCenter=dict(x=160,y=28),boxTitleTextY=20,boxTitleTextXFormula='160 - sourceFontTextWidth / 2',
        informationWindow=dict(x=0,y=88,width=72,height=56),nicknameText=dict(x=6,y=88),
        speciesNameText=dict(x=6,y=102),genderAndLevelText=dict(x=10,y=116),heldItemText=dict(x=6,y=132),
        boxTitleCenterY=28,boxArrowCenters=[dict(x=92,y=28),dict(x=228,y=28)],
        normalShinyIconsIdentical=True,iconAnimationImplemented=False,scrollingPhase=0,
        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',
        sourceRefs=[reference(lock,sources,gp,'InitBoxMonSprites','gStorage->boxMonsSprites[count] = CreateMonIconSprite(species, personality, 8 * (3 * j) + 100, 8 * (3 * i) + 44, 2, 19 - j);'),
                    reference(lock,sources,gp,'DrawWallpaper','CopyRectToBgTilemapBufferRect(2, tilemap, 0, 0, 20, 18, x, 2, 20, 18, 17, offset << 8, paletteNum);'),
                    reference(lock,sources,tp,'InitPalettesAndSprites'),reference(lock,sources,tp,'InitSupplementalTilemaps'),
                    reference(lock,sources,gp,'GetBoxTitleBaseX','return DISPLAY_WIDTH - 64 - GetStringWidth(FONT_NORMAL_COPY_1, string, 0) / 2;'),
                    reference(lock,sources,tp,'PrintDisplayMonInfo'),
                    reference(lock,sources,'src/pokemon_storage_system_data.c','displayMonTexts')])
    outputs['storage-art.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        fullSourceHash=source_set_hash(lock['files']),icons=icons,assets=assets,display=display,
        limitations=['Only the first default forest wallpaper is converted; other box wallpapers are not implemented.',
            'Static settled source layers with scrolling phase zero. No waveform, hand cursor, box-name text, dynamic information text, marking sprites or animated transitions.',
            'The composed background uses the empty-selection top data strip. Dynamic display-mon data chrome requires separate runtime composition.',
            'Party panel begins with one filled first slot and five empty slots; occupancy overlays are separate source assets.',
            'The original storage system uses one shared normal/shiny palette per species and frame zero for both party and box icons.',
            'Pixel/source-authenticated reconstruction; no claim of complete native frame or timing equivalence.']))
    outputs['manifest.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,
        lockSha256=LOCK_SHA256,fullSourceHash=source_set_hash(lock['files']),inputCount=len(lock['files']),
        iconCount=len(icons),assetCount=len(assets),textureCount=len(icons)+len(assets),
        upstreamScriptsExecuted=False,romDownloaded=False,runtimeShadersUsed=False,
        outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw,out=safe(raw),safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo=pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw,out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock=load_lock(lock_path)
    sources=read_sources(lock,raw,repository)
    outputs=convert(lock,sources)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed storage art generated output hashes differ')
    material={(raw,p):b for p,b in sources.items()} if repository is not None else {}
    if repository is not None: material[(raw,'manifest.json')]=raw_manifest(lock)
    material.update({(out,p):b for p,b in outputs.items()})
    targets=[(safe(root,p),safe(root,p+'.tmp'),b) for (root,p),b in material.items()]
    if any(p.exists() and not p.is_file() for target,stage,_ in targets for p in (target,stage)):
        raise ValueError('Input/output target is not a regular file')
    if any(parent.exists() and not parent.is_dir() for target,_,_ in targets for parent in target.parents):
        raise ValueError('Input/output ancestor is not a directory')
    for target,stage,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(sources),outputCount=len(outputs),
        storageArtSha256=sha(outputs['storage-art.json']),manifestSha256=sha(outputs['manifest.json']),
        fullSourceHash=source_set_hash(lock['files']))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path,default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/storage-art')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/storage-art')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/fetch-storage-art.lock.json')
    args=parser.parse_args()
    print(json.dumps(import_content(args.raw,args.out,args.reference_repo,args.lock),sort_keys=True))


if __name__=='__main__': main()
