#!/usr/bin/env python3
"""Import two local-only quest pocket skins from fixed pret blobs; never execute upstream code."""
import argparse, hashlib, importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-quest-item-ui-static-v1'
PATHS = frozenset('''src/data/items.json
src/data/items.json.txt
src/data/item_icon_table.h
src/item_menu.c
src/bag.c
src/item_menu_icons.c
src/item_use.c
src/strings.c
graphics/interface/bag_male.png
graphics/interface/bag.pal
graphics/interface/red_arrow_other.png
graphics/interface/red_arrow.pal
graphics/item_menu/bg.png
graphics/item_menu/bg.bin
graphics/items/icons/oaks_parcel.png
graphics/items/icon_palettes/oaks_parcel.pal
graphics/items/icons/poke_ball.png
graphics/items/icon_palettes/poke_ball.pal'''.splitlines())
TEXTS = ('gText_Items2','gText_KeyItems2','gText_PokeBalls2','gText_OakForbidsUseOfItemHere')

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result
base = module('quest_item_ui_base', ROOT/'scripts/import-item-ui.py')
codec = base.codec

def digest(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, indent=2)+'\n').encode()
def pairs(values):
    result = {}
    for key, value in values:
        if key in result: raise ValueError('Duplicate JSON member: '+key)
        result[key] = value
    return result

def safe(root, relative):
    root = pathlib.Path(root).resolve(); candidate = root/relative
    if pathlib.PurePosixPath(relative).is_absolute() or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative or not candidate.resolve().is_relative_to(root):
        raise ValueError('Unsafe quest UI path: '+relative)
    # Even an in-root symlink makes source/output identity depend on mutable external state.
    if any(p.is_symlink() for p in [candidate, *candidate.parents] if p != root and p.is_relative_to(root)):
        raise ValueError('Quest UI symlink rejected: '+relative)
    return candidate

def load_lock(path=ROOT/'scripts/quest-item-ui.lock.json'):
    lock = json.loads(pathlib.Path(path).read_bytes(), object_pairs_hook=pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile')) != (1,'pret/pokefirered',COMMIT,PROFILE):
        raise ValueError('Quest UI source identity differs')
    entries = lock.get('files',[])
    if len(entries) != len(PATHS) or {e['path'] for e in entries} != PATHS: raise ValueError('Quest UI dependency set differs')
    tree = json.loads((ROOT/'content/raw/starter-audit/tree.json').read_bytes())
    if tree.get('sha') != COMMIT or tree.get('truncated') is not False: raise ValueError('Incomplete fixed Git tree')
    lookup = {e['path']:e for e in tree['tree'] if e['type']=='blob'}
    for e in entries:
        safe(ROOT,e['path']); t=lookup[e['path']]
        if (e['byteLength'],e['gitBlobSha1']) != (t['size'],t['sha']) or not re.fullmatch('[0-9a-f]{64}',e['sha256']):
            raise ValueError('Quest UI lock differs from fixed Git tree')
    return lock

def verify(data, entry):
    blob = hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
    if (len(data),digest(data),blob) != (entry['byteLength'],entry['sha256'],entry['gitBlobSha1']):
        raise ValueError('Quest UI source hash differs: '+entry['path'])

def read_sources(lock, raw, repository=None):
    pending=[]; sources={}
    for e in lock['files']:
        target=safe(raw,e['path'])
        data=target.read_bytes() if target.exists() else subprocess.check_output(['git','-C',str(repository),'show',COMMIT+':'+e['path']]) if repository else target.read_bytes()
        verify(data,e); sources[e['path']]=data; pending.append((target,data))
    for target,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
    return sources

def convert(lock, sources):
    for e in lock['files']: verify(sources[e['path']],e)
    texts={}; refs=[]
    source=sources['src/strings.c'].decode()
    for symbol in TEXTS:
        found=list(re.finditer(r'^const u8 '+symbol+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);',source,re.M))
        if len(found)!=1: raise ValueError('Expected one quest UI string: '+symbol)
        m=found[0]; value=json.loads(m[1]); controls=[]
        if value.endswith('{PAUSE_UNTIL_PRESS}'): value=value.removesuffix('{PAUSE_UNTIL_PRESS}'); controls=['PAUSE_UNTIL_PRESS']
        if any(t!='PLAYER' for t in re.findall(r'\{([^}]+)\}',value)): raise ValueError('Unsupported quest UI text control')
        texts[symbol]=value; refs.append(dict(path='src/strings.c',symbol=symbol,field=None,startLine=source[:m.start()].count('\n')+1,terminalControls=controls))
    menu=sources['src/item_menu.c'].decode(); icons=sources['src/item_menu_icons.c'].decode(); use=sources['src/item_use.c'].decode()
    for needle in ['gText_Items2,\n    gText_KeyItems2,\n    gText_PokeBalls2','if (pocketId >= POCKET_POKE_BALLS - 1)','if (pocketId == POCKET_ITEMS - 1)']: codec.require(menu,needle)
    for symbol,offset in [('Items',0x80),('KeyItems',0xc0),('PokeBalls',0x40)]:
        if not re.search(r'sAnim_Bag_Open'+symbol+r'Pocket\[\].*?ANIMCMD_FRAME\(0x'+format(offset,'x')+r', 0\)',icons,re.S): raise ValueError('Settled bag frame differs')
    codec.require(use,'DisplayItemMessageInCurrentContext(taskId, inField, FONT_MALE, gText_OakForbidsUseOfItemHere);')
    codec.require(sources['src/data/items.json.txt'].decode(),'gItemDescription_{{ item.itemId }}[] = _("{{ item.description_english }}")')
    items=[]; parsed=json.loads(sources['src/data/items.json'])['items']; item_source=sources['src/data/items.json'].decode()
    for symbol,id,icon in [('ITEM_OAKS_PARCEL','pret:ITEM_OAKS_PARCEL','oaks-parcel'),('ITEM_POKE_BALL','pret:ITEM_POKE_BALL','poke-ball')]:
        values=[i for i in parsed if i.get('itemId')==symbol]
        if len(values)!=1: raise ValueError('Quest item text record differs')
        item=values[0]; description=item['description_english'].replace(r'\n','\n')
        if '\\' in description or '{' in description: raise ValueError('Unsupported item description control')
        items.append(dict(id=id,sourceSymbol=symbol,name=item['english'],description=description,icon=icon,fieldUseFunction=item['fieldUseFunc']))
        for field in ['english','description_english']:
            refs.append(dict(path='src/data/items.json',symbol=symbol,field=field,startLine=item_source[:item_source.index('"itemId": "'+symbol+'"')].count('\n')+1,terminalControls=[]))
    assets=[]; outputs={}
    def add(id,w,h,pixels,x=0,y=0):
        data=codec.png(w,h,pixels); outputs[id+'.png']=data
        assets.append(dict(id=id,texturePath=id+'.png',width=w,height=h,x=x,y=y,sha256=digest(data),rgbaSha256=digest(pixels)))
    w,h,indices,_=codec.indexed_png(sources['graphics/interface/bag_male.png'])
    if (w,h)!=(64,256): raise ValueError('Male bag source layout differs')
    for id,row in [('bag-key-items-background',192),('bag-poke-balls-background',64)]:
        out=bytearray(base.render_map(sources['graphics/item_menu/bg.png'],sources['graphics/item_menu/bg.bin']))
        bag=codec.rgba(indices[row*64:(row+64)*64],codec.palette(sources['graphics/interface/bag.pal']))
        base.composite(out,240,bag,64,64,8,36); add(id,240,160,bytes(out))
    for name in ['oaks_parcel','poke_ball']:
        w,h,indices,_=codec.indexed_png(sources['graphics/items/icons/'+name+'.png'])
        if (w,h)!=(24,24): raise ValueError('Quest item icon source dimensions differ')
        add(name.replace('_','-'),24,24,codec.rgba(indices,codec.palette(sources['graphics/items/icon_palettes/'+name+'.pal'])),8,124)
    w,h,indices,_=codec.indexed_png(sources['graphics/interface/red_arrow_other.png'])
    if (w,h)!=(16,32): raise ValueError('Pocket arrow source dimensions differ')
    add('pocket-left-arrow',16,16,codec.rgba(indices[:256],codec.palette(sources['graphics/interface/red_arrow.pal'])),0,64)
    source_hash=digest(''.join(e['path']+'\0'+e['sha256']+'\n' for e in sorted(lock['files'],key=lambda e:e['path'])).encode())
    document=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,fullSourceHash=source_hash,inputs=lock['files'],
        pockets=['POCKET_ITEMS','POCKET_KEY_ITEMS','POCKET_POKE_BALLS'],texts=texts,textSources=refs,items=items,assets=assets,
        limitations=['Settled male bag frames only; no pocket slide, bag shake, arrow bounce, audio or original-game full-frame comparison.',
                     'Only Parcel and Poke Ball quest lists; Items delegates to existing Potion UI. No capture, giving, tossing or registration.'])
    outputs['quest-item-ui.json']=canonical(document)
    outputs['manifest.json']=canonical(dict(profile=PROFILE,sourceCommit=COMMIT,fullSourceHash=source_hash,outputs=[dict(path=p,byteLength=len(b),sha256=digest(b)) for p,b in sorted(outputs.items())],upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False))
    return outputs

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--repository',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/quest-item-ui');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/quest-item-ui');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/quest-item-ui.lock.json');a=p.parse_args()
    lock=load_lock(a.lock); outputs=convert(lock,read_sources(lock,a.raw,a.repository))
    for name,data in outputs.items():
        target=safe(a.out,name);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(status='verified',sourceFiles=len(lock['files']),outputs=len(outputs),catalogSha256=digest(outputs['quest-item-ui.json']))))
if __name__=='__main__':main()
