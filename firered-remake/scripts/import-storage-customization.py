#!/usr/bin/env python3
"""Narrow original PC wallpaper/marking import. Reads pinned Git objects or verified offline cache.

Reuses storage-art's indexed PNG/tilemap conversion without changing its source lock or outputs.
No upstream executable, ROM, network fetch, engine scene or runtime shader is used.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
import argparse
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('storage_art', ROOT / 'scripts/import-storage-art.py')
art = importlib.util.module_from_spec(spec)
spec.loader.exec_module(art)
pixels,  canonical, safe = art.pixels,  art.canonical, art.safe
COMMIT, TREE = art.COMMIT, art.TREE
PROFILE = 'firered-storage-customization-v1'
WALLPAPERS = ('forest', 'city', 'desert', 'savanna', 'crag', 'volcano', 'snow', 'cave',
              'beach', 'seafloor', 'river', 'sky', 'stars', 'pokecenter', 'tiles', 'simple')
PATHS = frozenset('''include/pokemon_storage_system.h
include/pokemon_storage_system_internal.h
include/pokemon.h
include/mon_markings.h
src/pokemon_storage_system.c
src/pokemon_storage_system_menu.c
src/pokemon_storage_system_data.c
src/pokemon_storage_system_graphics.c
src/pokemon_storage_system_tasks.c
src/naming_screen.c
src/mon_markings.c
src/pokemon.c
src/daycare.c
src/strings.c
src/graphics.c
src/bg.c
charmap.txt
graphics_file_rules.mk
graphics/misc/mon_markings.png
graphics/misc/markings2.png'''.splitlines()) | frozenset(
    f'graphics/pokemon_storage/wallpapers/{name}/{leaf}' for name in WALLPAPERS for leaf in ('tiles.png', 'tilemap.bin'))


def load_lock(path):
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=art.unique_pairs)
    expected = (1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0)
    if tuple(lock.get(k) for k in ('schemaVersion', 'repository', 'commit', 'tree', 'profile', 'gameEdition', 'romRevision')) != expected:
        raise ValueError('Storage customization source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Storage customization dependency set differs')
    return lock


def raw_manifest():
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT,
         inputCount=len(PATHS)))


def read_sources(lock, raw, repository=None):
    if repository is not None:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
            check=True, capture_output=True, timeout=30).stdout.decode().strip()
    sources = {}
    for entry in lock['files']:
        path = entry['path']
        target = safe(raw, path)
        if repository is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + path],
                check=True, capture_output=True, timeout=30).stdout
        sources[path] = data
    return sources


def convert( sources):
    def text(path): return sources[path].decode('utf-8')
    def require(path, needle):
        if needle not in text(path): raise ValueError('Unsupported customization source: ' + needle)
    hp, gp, tp, dp = ('include/pokemon_storage_system.h', 'src/pokemon_storage_system_graphics.c',
                      'src/pokemon_storage_system_tasks.c', 'src/pokemon_storage_system_data.c')
    for path, needle in [(hp, '#define BOX_NAME_LENGTH         8'), (hp, '#define MAX_DEFAULT_WALLPAPER WALLPAPER_SAVANNA'),
        ('include/mon_markings.h', '#define NUM_MON_MARKINGS 4'),
        ('src/pokemon_storage_system_menu.c', 'SetBoxWallpaper(boxId, boxId % (MAX_DEFAULT_WALLPAPER + 1));'),
        ('src/pokemon_storage_system_menu.c', 'ConvertIntToDecimalStringN(dest, boxId + 1, STR_CONV_MODE_LEFT_ALIGN, 2);'),
        ('src/naming_screen.c', '.maxChars = BOX_NAME_LENGTH'),
        ('src/naming_screen.c', 'if (sNamingScreen->textBuffer[i] != CHAR_SPACE && sNamingScreen->textBuffer[i] != EOS)'),
        ('src/mon_markings.c', 'sMenu->markings |= sMenu->markingsArray[i] << i;'),
        ('src/mon_markings.c', 'RequestDma3Copy(&sMonMarkings_Gfx[64 * markings], dest, 0x80, DMA3_32BIT);'),
        ('src/daycare.c', 'SetMonData(temp, MON_DATA_MARKINGS, &markings);'),
        (dp, 'SetMonData(&gStorage->movingMon, MON_DATA_MARKINGS, &markings);'),
        (gp, 'CopyRectToBgTilemapBufferRect(2, tilemap, 0, 0, 20, 18, x, 2, 20, 18, 17, offset << 8, paletteNum);'),
        (tp, 'OpenMonMarkingsMenu(gStorage->displayMonMarkings, 176, 16);')]: require(path, needle)
    symbols = re.findall(r'^    (WALLPAPER_\w+),', text(hp), re.M)
    if symbols != ['WALLPAPER_' + name.upper() for name in WALLPAPERS]:
        raise ValueError('Wallpaper enumeration changed')
    strings = dict(re.findall(r'const u8 (\w+)\[\] = _\("([^"\n]*)"\);', text('src/strings.c')))
    menu_strings = dict(re.findall(r'\[(MENU_TEXT_\w+)\]\s*=\s*(gPCText_\w+),', text(dp)))
    sets = text(tp).split('static void AddWallpaperSetsMenu(void)\n{')[1].split('\n}')[0]
    category_symbols = re.findall(r'SetMenuText\((MENU_TEXT_\w+)\);', sets)
    menu = text(tp).split('static void AddWallpapersMenu(u8 wallpaperSet)\n{')[1].split('\n}')[0]
    selections = re.findall(r'SetMenuText\((MENU_TEXT_\w+)\);', menu)
    if len(category_symbols) != 4 or len(selections) != 16:
        raise ValueError('Wallpaper menu dimensions changed')
    keyboard = text('src/naming_screen.c').split('static const u8 sKeyboardChars')[1].split('\n};')[0]
    rows = re.findall(r'__\("([^"\n]*)"\)', keyboard)
    alphabet = ''.join(dict.fromkeys(''.join(rows)))
    # Every admitted Unicode BMP character is exactly one original encoded byte.
    for char in alphabet:
        if not re.search(r"^'" + re.escape("\\'" if char == "'" else char) + r"'\s*=\s*[0-9A-F]{2}\s*(?:@.*)?$", text('charmap.txt'), re.M):
            raise ValueError('Naming character lacks a source byte: ' + char)
    title_rows = re.findall(r'\{RGB\(\s*(\d+),\s*(\d+),\s*(\d+)\), RGB_WHITE\}',
        text(gp).split('static const u16 sBoxTitleColors[][2]')[1].split('\n};')[0])
    if len(title_rows) != 16: raise ValueError('Box title colors changed')
    outputs, assets, wallpapers = {}, [], []
    def add(identifier, w, h, rgba, x=0, y=0):
        if len(rgba) != w*h*4: raise ValueError('Customization asset dimensions differ')
        png_path = identifier + '.png'
        outputs[png_path] = pixels.png(w, h, rgba)
        assets.append(dict(id=identifier, texturePath=png_path, width=w, height=h, x=x, y=y))
    for index, name in enumerate(WALLPAPERS):
        base = f'graphics/pokemon_storage/wallpapers/{name}/'
        count = int(re.search(r'\$\(WALLPAPERGFXDIR\)/'+name+r'/tiles\.4bpp:[^\n]+\n\s*\$\(GFX\) \$< \$@ -num_tiles (\d+) -Wnum_tiles',
            text('graphics_file_rules.mk'))[1])
        palette = pixels.colors(pixels.indexed_png(sources[base+'tiles.png'])[3])
        rgba = art.render_tilemap(pixels.tile_list(sources[base+'tiles.png'])[:count], sources[base+'tilemap.bin'],
            20,18,{1:palette[:16],2:palette[16:32]})
        asset_id = 'wallpaper-' + name
        add(asset_id,160,144,rgba,80,16)
        red,green,blue = map(int,title_rows[index])
        wallpapers.append(dict(id=index,symbol=symbols[index],name=strings[menu_strings[selections[index]]],
            categoryId=index//4,assetId=asset_id,titleShadowRgb555=red|(green<<5)|(blue<<10),titleTextRgb555=32767))
    categories = [dict(id=i,name=strings[menu_strings[symbol]],wallpaperIds=list(range(i*4,i*4+4)))
                  for i,symbol in enumerate(category_symbols)]
    def sprite(source, first, w, h):
        tiles = pixels.tile_list(sources[source]); palette = pixels.colors(pixels.indexed_png(sources[source])[3])[:16]
        result = pixels.canvas(w,h)
        for row in range(h//8):
            for col in range(w//8):
                pixels.blit(result,w,pixels.tile_rgba(tiles[first+row*(w//8)+col],palette),8,8,col*8,row*8)
        return bytes(result)
    marking_rows = []
    for index,name in enumerate(('circle','square','triangle','heart')):
        for active in range(2):
            add('marking-'+name+('-on' if active else '-off'),8,8,
                sprite('graphics/misc/markings2.png',index*2+active,8,8))
        marking_rows.append(dict(bit=1<<index,name=name,offAssetId='marking-'+name+'-off',onAssetId='marking-'+name+'-on',
            menuCenterX=208,menuCenterY=32+16*index))
    add('marking-menu-cursor',8,8,sprite('graphics/misc/markings2.png',8,8,8))
    add('marking-menu-ok-cancel',32,32,sprite('graphics/misc/markings2.png',9,32,32),192,88)
    for markings in range(16):
        add('marking-combo-'+str(markings),32,8,sprite('graphics/misc/mon_markings.png',markings*4,32,8),24,146)
    document = dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        boxNameLength=8,boxNameAlphabet=alphabet,defaultNames=[strings['gText_Box']+str(i+1) for i in range(14)],
        defaultWallpapers=[i%4 for i in range(14)],categories=categories,wallpapers=wallpapers,assets=assets,
        texts={key:strings[key] for key in sorted(set(menu_strings.values()) | {
            'gText_WhatDoYouWantToDo','gText_PleasePickATheme','gText_PickTheWallpaper','gText_MarkYourPkmn','gText_BoxName'})},
        display=dict(logicalWidth=240,logicalHeight=160,wallpaperX=80,wallpaperY=16,
            titleCenterX=160,titleY=20,markingMenuX=176,markingMenuY=16,markingComboX=24,markingComboY=146,
            markings=marking_rows,markingCancelPreservesOriginal=True,
            labels={key:strings[key] for key in ('gPCText_Name','gPCText_Wallpaper','gPCText_Mark','gPCText_Cancel')}),
        limitations=['Static source pixels and committed Domain metadata only; Application/View integration is separate.',
            'Marking menu border uses the separately owned user-selected window frame; no border is baked here.',
            'No summary screen, rectangle selection, original binary save or wallpaper transition animation.'])
    outputs['storage-customization.json'] = canonical(document)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        inputCount=len(PATHS),
        assetCount=len(assets),upstreamScriptsExecuted=False,romDownloaded=False,
        outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw,out = safe(raw),safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw,out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock = load_lock(lock_path or ROOT/'scripts/fetch-storage-customization.lock.json')
    sources = read_sources(lock,raw,repository)
    outputs = convert(sources)
    material = {(raw,p):b for p,b in sources.items()} if repository is not None else {}
    if repository is not None: material[(raw_metadata_directory(raw, 'storage-customization'),'manifest.json')] = raw_manifest()
    material.update({(out,p):b for p,b in outputs.items()})
    targets = [(safe(root,p),safe(root,p+'.tmp'),b) for (root,p),b in material.items()]
    if any(p.exists() and not p.is_file() for target,stage,_ in targets for p in (target,stage)):
        raise ValueError('Customization target is not a regular file')
    if any(parent.exists() and not parent.is_dir() for target,_,_ in targets for parent in target.parents):
        raise ValueError('Customization ancestor is not a directory')
    for target,stage,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        stage.write_bytes(data); stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(sources),outputCount=len(outputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/storage-customization')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/fetch-storage-customization.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw,args.out,args.reference_repo,args.lock),sort_keys=True))
