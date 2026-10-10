#!/usr/bin/env python3
"""Fetch the bounded, pinned opening item UI source set. Local cache git objects are read only."""
import argparse, hashlib, json, pathlib, re, subprocess
ROOT = pathlib.Path(__file__).resolve().parent.parent
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
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/item-ui')
    p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/item-ui.lock.json')
    p.add_argument('--cache',type=pathlib.Path,required=True,help='Read-only local pret git repository; working tree files are never used')
    p.add_argument('--offline',action='store_true',help='Compatibility flag: this fetcher always stays offline')
    a=p.parse_args();lock=load_lock(a.lock);pending=[]
    for e in lock['files']:
        target=safe_path(a.raw,e['path'])
        if target.exists():data=target.read_bytes()
        else:data=subprocess.check_output(['git','-C',str(a.cache),'show',COMMIT+':'+e['path']])
        verify(data,e);pending.append((target,data))
    for target,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    a.raw.mkdir(parents=True,exist_ok=True)
    safe_path(a.raw,'manifest.json').write_bytes(canonical({**lock,'fullSourceHash':source_set_hash(lock),'inputRoute':'read-only pinned local git blobs','upstreamScriptsExecuted':False,'romDownloaded':False,'publiclyUploaded':False}))
    print(json.dumps({'status':'verified','files':len(pending),'fullSourceHash':source_set_hash(lock)}))
if __name__=='__main__':main()
