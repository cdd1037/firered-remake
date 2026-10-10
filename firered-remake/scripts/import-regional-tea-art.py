#!/usr/bin/env python3
"""Reproduce the original Tea key-item icon from six fixed local pret blobs. No upstream execution."""
import argparse, hashlib, importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-regional-tea-art-v1'
LOCK_SHA256 = 'ade0658bf9e37e19575a954c0e70254769ab1f2e3e8a1ebcc2f985a3f15c95ac'
PATHS = {'src/data/items.json', 'src/data/item_icon_table.h', 'src/data/graphics/items.h', 'src/item_use.c', 'graphics/items/icons/tea.png', 'graphics/items/icon_palettes/tea.pal'}
CODEC_SHA256 = 'b03954e07eb38a9b2dee1f98cc8ed9f92a1f35b32fe9197a16795db4e527795a'
CODEC_INPUT_SHA256 = '805779208815c0a7f26fb00d9187ca6b1041064e7b8909090ce7f7f048bc7f32'
def digest(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, indent=2)+'\n').encode()
def safe(root, relative):
    root=pathlib.Path(root).absolute(); p=pathlib.PurePosixPath(relative)
    if p.is_absolute() or any(x in ('..','.') for x in p.parts) or '\\' in relative or str(p)!=relative: raise ValueError('Unsafe Tea path: '+relative)
    path=root/relative
    if any(x.is_symlink() for x in (path,*path.parents)): raise ValueError('Tea symlink rejected: '+relative)
    return path

def run(args):
    lockbytes=safe(ROOT,'scripts/regional-tea-art.lock.json').read_bytes()
    if digest(lockbytes)!=LOCK_SHA256: raise ValueError('Reviewed Tea lock differs')
    lock=json.loads(lockbytes)
    if (lock['schemaVersion'],lock['profile'],lock['repository'],lock['commit'],lock['tree'])!=(1,PROFILE,'pret/pokefirered',COMMIT,'7515cd4067ea55f8cdbc73624478afd2736c58b9') or {x['path'] for x in lock['files']}!=PATHS or len(lock['files'])!=6: raise ValueError('Tea source scope differs')
    source={};pending=[]
    for e in lock['files']:
        path=safe(ROOT,'content/raw/regional-tea-art/'+e['path'])
        data=path.read_bytes() if path.exists() else subprocess.check_output(['git','-C',str(args.repository),'show',COMMIT+':'+e['path']]) if args.repository else path.read_bytes()
        blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
        if (len(data),digest(data),blob)!=(e['byteLength'],e['sha256'],e['gitBlobSha1']): raise ValueError('Tea source differs: '+e['path'])
        source[e['path']]=data;pending.append((path,data))
    base=args.dependency_root or ROOT
    for rel,sha in [('scripts/import-lab-battle-ui.py',CODEC_SHA256),('scripts/fetch-lab-battle-ui.py',CODEC_INPUT_SHA256)]:
        if digest(safe(base,rel).read_bytes())!=sha: raise ValueError('Reviewed local Tea codec differs: '+rel)
    spec=importlib.util.spec_from_file_location('_tea_icon_codec',base/'scripts/import-lab-battle-ui.py');codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
    items=json.loads(source['src/data/items.json'])['items']; item=next(x for x in items if x.get('itemId')=='ITEM_TEA')
    expected=dict(english='TEA',price=0,importance=1,registrability=1,pocket='POCKET_KEY_ITEMS',type='ITEM_TYPE_BAG_MENU',holdEffect='HOLD_EFFECT_NONE',holdEffectParam=0,fieldUseFunc='FieldUseFunc_OakStopsYou',battleUsage=0,battleUseFunc='NULL',secondaryId=0)
    if any(item[k]!=v for k,v in expected.items()) or items.index(item)!=369: raise ValueError('Tea source metadata differs')
    table=source['src/data/item_icon_table.h'].decode();bind=re.search(r'^    \[ITEM_TEA\]\s*=\s*\{(\w+), (\w+)\},',table,re.M)
    if not bind: raise ValueError('Tea icon binding missing')
    headers=source['src/data/graphics/items.h'].decode();paths=[]
    for symbol in bind.groups():
        match=re.search(r'\b'+symbol+r'\[\]\s*= INCBIN_U32\("(.*?)"\)',headers)
        if not match: raise ValueError('Tea asset declaration missing')
        paths.append(match[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    if paths!=['graphics/items/icons/tea.png','graphics/items/icon_palettes/tea.pal']: raise ValueError('Tea source icon/palette differs')
    use=source['src/item_use.c'].decode()
    if not re.search(r'void FieldUseFunc_OakStopsYou\(u8 taskId\).*?PrintNotTheTimeToUseThat\(taskId, gTasks\[taskId\].data\[3\]\);',use,re.S) or 'DisplayItemMessageInCurrentContext(taskId, inField, FONT_MALE, gText_OakForbidsUseOfItemHere);' not in use: raise ValueError('Tea field refusal differs')
    w,h,indices,_=codec.indexed_png(source[paths[0]])
    if (w,h)!=(24,24): raise ValueError('Tea source icon size differs')
    rgba=codec.rgba(indices,codec.palette(source[paths[1]]));png=codec.png(w,h,rgba)
    description=item['description_english'].replace(chr(92)+'n','\n')
    if '\\' in description or '{' in description: raise ValueError('Unsupported Tea description control')
    document=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,lockSha256=LOCK_SHA256,
      inputs=lock['files'],item=dict(id='pret:ITEM_TEA',sourceSymbol='ITEM_TEA',name='TEA',description=description,icon='regional-tea',fieldUseFunction=item['fieldUseFunc']),
      asset=dict(id='regional-tea',texturePath='regional-tea.png',width=w,height=h,x=8,y=124,sha256=digest(png),rgbaSha256=digest(rgba)),rgbaPath='regional-tea.rgba',
      limitations=['Settled source 24x24 icon only. Field Use retains the existing Oak refusal; no new use, toss, sale, held-item or registration behavior.'])
    outputs={'regional-tea.png':png,'regional-tea.rgba':rgba,'tea-art.json':canonical(document)}
    outputs['manifest.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,lockSha256=LOCK_SHA256,
      outputs=[dict(path=p,byteLength=len(b),sha256=digest(b)) for p,b in sorted(outputs.items())],upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False))
    for p,b in outputs.items():
        target=safe(ROOT,'content/generated/regional-tea-art/'+p)
        if args.check:
            if target.read_bytes()!=b: raise ValueError('Tea output differs: '+p)
        else: target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
    if not args.check:
        for p,b in pending:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
    print(json.dumps(dict(status='verified',sourceFiles=6,sourceBytes=sum(len(b) for b in source.values()),width=w,height=h,outputs=len(outputs),lockSha256=LOCK_SHA256,documentSha256=digest(outputs['tea-art.json']),pngSha256=digest(png),rgbaSha256=digest(rgba))))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--repository',type=pathlib.Path);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--check',action='store_true');run(p.parse_args())
