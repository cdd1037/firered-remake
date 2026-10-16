#!/usr/bin/env python3
"""Fetch the bounded, pinned opening item UI source set. Local cache git objects are read only."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, json, pathlib, subprocess
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-opening-item-ui-static-v1'
INPUT_PATHS = frozenset('''src/strings.c
src/data/items.json
src/data/items.json.txt
src/text.c
src/player_pc.c
src/item_pc.c
src/item_menu.c
src/item_menu_icons.c
src/bag.c
src/graphics.c
src/text_window.c
src/text_window_graphics.c
src/new_menu_helpers.c
src/menu_indicators.c
src/data/item_icon_table.h
tools/gbagfx/convert_png.c
graphics/item_pc/bg.png
graphics/item_pc/bg.bin
graphics/item_menu/bg.png
graphics/item_menu/bg.bin
graphics/item_menu/bg_item_pc.bin
graphics/item_menu/list.bin
graphics/item_menu/bag_window_pal.pal
graphics/interface/bag_male.png
graphics/interface/bag.pal
graphics/interface/std_menu.pal
graphics/interface/red_arrow_other.png
graphics/interface/red_arrow.pal
graphics/items/icons/potion.png
graphics/items/icon_palettes/potion.pal
graphics/text_window/std.png
graphics/text_window/stdpal_2.pal
graphics/text_window/stdpal_3.pal'''.splitlines())
def canonical(document): return (json.dumps(document, sort_keys=True, indent=2) + '\n').encode()
def source_url(path): return f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}'
def safe_path(root, relative):
    root=pathlib.Path(root).resolve(); p=root/relative
    if pathlib.PurePosixPath(relative).is_absolute() or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative or not p.resolve().is_relative_to(root): raise ValueError('Unsafe path: '+relative)
    return p

def load_lock(path, project_path=ROOT/'scripts/recipes/config/source.json'):
    lock=json.loads(pathlib.Path(path).read_bytes()); project=json.loads(pathlib.Path(project_path).read_bytes())
    if (lock.get('schemaVersion'),lock.get('profile'),lock.get('repository'),lock.get('commit')) != (1,PROFILE,REPOSITORY,COMMIT): raise ValueError('Unsupported UI lock identity')
    if (project['source']['sourceCommit'],project['source']['repository'],project['referenceGame']['gameEdition'],project['referenceGame']['romRevision']) != (COMMIT,REPOSITORY,'FIRERED',0): raise ValueError('Project source identity differs')
    entries=lock.get('files',[]); paths=[e['path'] for e in entries]
    if len(paths)!=len(set(paths)) or set(paths)!=INPUT_PATHS: raise ValueError('UI dependency set differs')
    for e in entries:
        safe_path(ROOT,e['path'])
        if e['sourceUrl'] != source_url(e['path']): raise ValueError('Invalid UI hash/URL')
    return lock

def read_verified(lock,raw):
    out={}
    for e in lock['files']:
        data=safe_path(raw,e['path']).read_bytes();out[e['path']]=data
    return out

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/item-ui.lock.json')
    p.add_argument('--cache',type=pathlib.Path,required=True,help='Read-only local pret git repository; working tree files are never used')
    p.add_argument('--offline',action='store_true',help='Compatibility flag: this fetcher always stays offline')
    a=p.parse_args();lock=load_lock(a.lock);pending=[]
    for e in lock['files']:
        target=safe_path(a.raw,e['path'])
        if target.exists():data=target.read_bytes()
        else:data=subprocess.check_output(['git','-C',str(a.cache),'show',COMMIT+':'+e['path']])
        pending.append((target,data))
    for target,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    a.raw.mkdir(parents=True,exist_ok=True)
    print(json.dumps({'status':'materialized','files':len(pending)}))
if __name__=='__main__':main()
