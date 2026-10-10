#!/usr/bin/env python3
"""Fetch the bounded, pinned lab UI source set. Local cache git objects are read only."""
import argparse, hashlib, json, pathlib, re, subprocess, urllib.request
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
def digest(data): return hashlib.sha256(data).hexdigest()
def canonical(document): return (json.dumps(document, sort_keys=True, indent=2) + '\n').encode()
def blob(data): return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
def source_url(path): return f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}'
def safe_path(root, relative):
    root=pathlib.Path(root).resolve(); p=root/relative
    if pathlib.PurePosixPath(relative).is_absolute() or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative or not p.resolve().is_relative_to(root): raise ValueError('Unsafe path: '+relative)
    return p

def load_lock(path, project_path=ROOT/'content/source-lock.json', tree_path=ROOT/'content/raw/starter-audit/tree.json'):
    lock=json.loads(pathlib.Path(path).read_bytes()); project=json.loads(pathlib.Path(project_path).read_bytes()); tree=json.loads(pathlib.Path(tree_path).read_bytes())
    if (lock.get('schemaVersion'),lock.get('profile'),lock.get('repository'),lock.get('commit')) != (1,PROFILE,REPOSITORY,COMMIT): raise ValueError('Unsupported UI lock identity')
    if (project['source']['sourceCommit'],project['source']['repository'],project['referenceGame']['gameEdition'],project['referenceGame']['romRevision']) != (COMMIT,REPOSITORY,'FIRERED',0): raise ValueError('Project source identity differs')
    if tree.get('sha')!=COMMIT or tree.get('truncated') is not False: raise ValueError('Incomplete/wrong pinned source tree')
    entries=lock.get('files',[]); paths=[e['path'] for e in entries]
    if len(paths)!=len(set(paths)) or set(paths)!=INPUT_PATHS: raise ValueError('UI dependency set differs')
    lookup={e['path']:e for e in tree['tree'] if e['type']=='blob'}
    for e in entries:
        safe_path(ROOT,e['path']); t=lookup[e['path']]
        if type(e['byteLength']) is not int or not 0<e['byteLength']<=1_000_000 or (e['gitBlobSha1'],e['byteLength'])!=(t['sha'],t['size']): raise ValueError('UI lock differs from pinned source tree: '+e['path'])
        if not re.fullmatch('[0-9a-f]{64}',e['sha256']) or e['sourceUrl']!=source_url(e['path']): raise ValueError('Invalid UI hash/URL')
    return lock

def verify(data,e):
    if (len(data),digest(data),blob(data))!=(e['byteLength'],e['sha256'],e['gitBlobSha1']): raise ValueError('Source hash mismatch: '+e['path'])
def source_set_hash(lock): return digest(''.join(e['path']+'\0'+e['sha256']+'\n' for e in sorted(lock['files'],key=lambda e:e['path'])).encode())
def read_verified(lock,raw):
    out={}
    for e in lock['files']:
        data=safe_path(raw,e['path']).read_bytes();verify(data,e);out[e['path']]=data
    return out

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/lab-battle-ui');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/lab-battle-ui.lock.json');p.add_argument('--cache',type=pathlib.Path);p.add_argument('--offline',action='store_true');a=p.parse_args()
    lock=load_lock(a.lock); pending=[]; recovered=[]
    for e in lock['files']:
        target=safe_path(a.raw,e['path'])
        if target.exists(): data=target.read_bytes() # Corrupt owned raw inputs always fail.
        elif a.cache and safe_path(a.cache,e['path']).is_file():
            data=safe_path(a.cache,e['path']).read_bytes()
            if (len(data),blob(data))!=(e['byteLength'],e['gitBlobSha1']):
                data=subprocess.check_output(['git','-C',str(a.cache),'show',COMMIT+':'+e['path']]);recovered.append(e['path'])
        elif a.offline: raise FileNotFoundError('Missing pinned UI input: '+e['path'])
        else:
            with urllib.request.urlopen(f'https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{e["path"]}',timeout=60) as r: data=r.read(e['byteLength']+1)
        verify(data,e);pending.append((target,data))
    for target,data in pending: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    a.raw.mkdir(parents=True,exist_ok=True);safe_path(a.raw,'manifest.json').write_bytes(canonical({**lock,'fullSourceHash':source_set_hash(lock),'gitObjectRecoveries':recovered,'upstreamScriptsExecuted':False,'romDownloaded':False,'publiclyUploaded':False}))
    print(json.dumps({'status':'verified','files':len(pending),'fullSourceHash':source_set_hash(lock),'gitObjectRecoveries':recovered}))
if __name__=='__main__':main()
