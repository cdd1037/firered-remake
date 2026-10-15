#!/usr/bin/env python3
"""Fetch the bounded, pinned lab UI source set. Local cache git objects are read only."""
import argparse, json, pathlib, urllib.request
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-lab-battle-ui-static-v1'
INPUT_PATHS = frozenset('''Makefile
graphics_file_rules.mk
data/maps/PalletTown_ProfessorOaksLab/map.json
src/battle_bg.c
src/battle_main.c
src/battle_setup.c
src/battle_interface.c
src/battle_gfx_sfx_util.c
src/battle_controller_oak_old_man.c
src/battle_message.c
src/new_menu_helpers.c
src/text_window.c
src/text_window_graphics.c
src/graphics.c
src/sprite.c
src/palette.c
src/blend_palette.c
tools/gbagfx/gfx.c
tools/gbagfx/convert_png.c
graphics/battle_terrain/building/terrain.png
graphics/battle_terrain/building/terrain.pal
graphics/battle_terrain/building/terrain.bin
graphics/battle_interface/textbox.png
graphics/battle_interface/textbox.bin
graphics/battle_interface/textbox1.pal
graphics/battle_interface/textbox2.pal
graphics/battle_interface/healthbox.pal
graphics/battle_interface/healthbar.pal
graphics/battle_interface/healthbox_singles_player.png
graphics/battle_interface/healthbox_singles_opponent.png
graphics/battle_interface/healthbox_elements.png
graphics/text_window/type1.png
graphics/text_window/menu_message.png
graphics/text_window/stdpal_0.pal'''.splitlines())
def canonical(document): return (json.dumps(document, sort_keys=True, indent=2) + '\n').encode()
def source_url(path): return f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}'
def safe_path(root, relative):
    root=pathlib.Path(root).resolve(); p=root/relative
    if pathlib.PurePosixPath(relative).is_absolute() or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative or not p.resolve().is_relative_to(root): raise ValueError('Unsafe path: '+relative)
    return p

def load_lock(path, project_path=ROOT/'content/source-lock.json'):
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
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/lab-battle-ui.lock.json');p.add_argument('--cache',type=pathlib.Path);p.add_argument('--offline',action='store_true');a=p.parse_args()
    lock=load_lock(a.lock); pending=[]
    for e in lock['files']:
        target=safe_path(a.raw,e['path'])
        if target.exists(): data=target.read_bytes()
        elif a.cache and safe_path(a.cache,e['path']).is_file():
            data=safe_path(a.cache,e['path']).read_bytes()
        elif a.offline: raise FileNotFoundError('Missing pinned UI input: '+e['path'])
        else:
            with urllib.request.urlopen(f'https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{e["path"]}',timeout=60) as r: data=r.read()
        pending.append((target,data))
    for target,data in pending: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps({'status':'materialized','files':len(pending)}))
if __name__=='__main__':main()
