#!/usr/bin/env python3
"""Authenticate the prepared Tower data; add seven maps without changing the prior 120 payloads."""
import argparse, ast, base64, hashlib, json, pathlib, re, struct
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SOURCE = 'b70e9238770d5952aff6c8a075a5de1447e947cf6c31428b54bb8d77a7b03e51'
LOCK = '35ee161c56cb74091f695faa506adc1443ac700e2165705db380c0d12cbf76e6'
GENERATED = 'bad1aa9ffd6b9e2ffeadb51f3e089b4635a637fb027659fef4c00f4680f77f8b'
PREVIOUS = 'ee99ed635f7355c82f26373274caf3d1bc2706e7329f4d17383d3fc6b614c56b'
SIDECARS={'celadon-approach-region-source-lock.json': 'b09ce111e7a091e7cc551f0d4660fb468f9a805340af0f80056b12d1db00242a', 'celadon-gym-region-source-lock.json': '0237b9b50415fd60f6eb9fd4c7ba6adbababdfcddc085d8147c4d767dcf04be5', 'city-services-source-lock.json': '4b71a3820a2d50ff80c32589c60979cb519dd7aeecec9bec13696bf1ad317eb5', 'rocktunnel-region-source-lock.json': '4c687593e368f67cc7db73c0255de541a8cb5b19590326c5562993087f4114c8', 'route11-region-source-lock.json': '6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304', 'ssanne-source-lock.json': '72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1', 'rocket-hideout-region-source-lock.json':'e5447964ad69cb9568018feb793d419a6fb3f26d752c8242ab5bc8136977c0b7', 'vermilion-gym-source-lock.json': '569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8'}
HOUSE = 'map.lavender-town-volunteer-pokemon-house'
def sha(b): return hashlib.sha256(b).hexdigest()
def encode(d): return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def need(ok,msg):
    if not ok: raise ValueError(msg)
def safe(root,path):
    need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and ':' not in path and all(x not in ('','.','..') for x in path.split('/')), 'Unsafe path')
    q=root/path
    need(not any(x.is_symlink() for x in (q,*q.parents)), 'Symlink rejected')
    return q
def run(project,preparation,output,check=False):
    project,preparation,output=map(pathlib.Path,(project,preparation,output)); payload={}
    need(not output.resolve().is_relative_to(preparation.resolve()) and not preparation.resolve().is_relative_to(output.resolve()), 'Preparation output overlap')
    def read(root,path,digest,length=None):
        with safe(root,path).open('rb') as f: b=f.read(16*1024*1024+1)
        need(0<len(b)<=16*1024*1024 and sha(b)==digest and (length is None or len(b)==length), 'Pinned bytes differ: '+path)
        return b
    lockbytes=read(preparation,'manifests/source-lock.json',LOCK); lock=json.loads(lockbytes)
    generated=json.loads(read(preparation,'manifests/generated.json',GENERATED)); raw={}
    need(lock['commit']==COMMIT and len(lock['files'])==244 and generated['fullSourceHash']==SOURCE, 'Source identity/count differs')
    for f in lock['files']:
        b=read(preparation,'raw/'+f['path'],f['sha256'],f['byteLength'])
        need(hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==f['gitBlobSha1'], 'Git blob differs')
        raw[f['path']]=b; payload['content/raw/pokemon-tower-region/'+f['path']]=b
    need(sha(''.join(p+'\0'+sha(b)+'\n' for p,b in sorted(raw.items())).encode())==SOURCE, 'Source set differs')
    assets={f['path']:read(preparation,f['path'],f['sha256'],f['byteLength']) for f in generated['outputs']}
    region='content/generated/pokemon-tower-region/'; runtime='content/generated/pokemon-tower-runtime-maps/'
    previous=project/'content/generated/rocket-hideout-runtime-maps'
    previousbytes=read(previous,'manifest.json',PREVIOUS); old=json.loads(previousbytes)
    need(len(old['maps'])==120, 'Previous 120-map scope differs'); entries=list(old['maps'])
    for e in entries: payload[runtime+e['path']]=read(previous,e['path'],e['sha256'],e['byteLength'])
    registry=old['sourceRegistry']; payload[runtime+registry['path']]=read(previous,registry['path'],registry['sha256'],registry['byteLength'])
    for p,h in SIDECARS.items(): payload[runtime+p]=read(previous,p,h)
    payload[runtime+'pokemon-tower-region-source-lock.json']=lockbytes
    payload[runtime+'rocket-baseline-manifest.json']=previousbytes
    payload['scripts/pokemon-tower-region.lock.json']=lockbytes
    contract=json.loads(assets['overlays/pokemon-tower.json']); specs=[]
    for s in contract['maps']:
        name=s['directory'].split('/')[-1]; prefix='assets/maps/'+name+'/'
        world=json.loads(assets[prefix+'world.json']); compact=json.loads(assets[prefix+'runtime-map.json'])
        need(compact['sourceWorldSha256']==sha(assets[prefix+'world.json']) and compact['fullSourceHash']==SOURCE, 'Compact rich binding differs')
        need(compact['events']==json.loads(raw['data/maps/'+s['sourceName']+'/map.json']), 'Events differ')
        need(base64.b64decode(compact['blocks'])==b''.join(struct.pack('<H',c['rawBlock']) for c in world['cells']), 'Cell blocks differ')
        need(base64.b64decode(compact['borderBlocks'])==b''.join(struct.pack('<H',c['rawBlock']) for c in world['borderCells']), 'Border blocks differ')
        for script in compact['scripts']: need(script['text'].encode()==raw[script['path']], 'Script bytes differ')
        for art in compact['artifacts']: need(sha(assets[prefix+art['path']])==art['sha256'], 'PNG differs')
        is_new=s['stableId']!=HOUSE
        if is_new:
            for p,b in assets.items():
                if p.startswith(prefix) and p.endswith('.png'): payload[region+name+'/'+p.split('/')[-1]]=b
            path='pokemon-tower-region/'+name+'/runtime-map.json'; b=assets[prefix+'runtime-map.json']; payload[runtime+path]=b
            entries.append(dict(stableId=s['stableId'],path=path,byteLength=len(b),sha256=sha(b),sourcePath=path.replace('runtime-map.json','world.json'),sourceByteLength=len(assets[prefix+'world.json']),sourceWorldSha256=sha(assets[prefix+'world.json']),fullSourceHash=SOURCE))
        else:
            old_house=next(e for e in old['maps'] if e['stableId']==HOUSE)
            prior=json.loads(payload[runtime+old_house['path']])
            need({k:v for k,v in compact.items() if k not in ('fullSourceHash','sourceWorldSha256')}=={k:v for k,v in prior.items() if k not in ('fullSourceHash','sourceWorldSha256')}, 'Existing Fuji house gameplay source differs')
        specs.append(dict(stableId=s['stableId'],sourceName=s['sourceName'],sourceMapSymbol=compact['sourceRef']['symbol'],directory=name,width=compact['width'],height=compact['height'],isNew=is_new))
    for p,b in assets.items():
        if p.startswith('assets/npc/') or p.startswith('assets/presentation/'): payload[region+p.removeprefix('assets/')]=b
    for name in ['dependencies','npc-source','scripts','mechanics','pokemon-tower']: payload[region+name+'.json']=assets['overlays/'+name+'.json']
    constant_source=json.loads(assets['overlays/constants.json']); constants=constant_source['values']
    # The preparation's literal inventory excludes these source expressions. Resolve only
    # the bounded required aliases, never eval/execute upstream declarations.
    for path,names in [('include/constants/vars.h',['VAR_TEMP_1']),('include/constants/battle.h',['BATTLE_TYPE_GHOST','BATTLE_TYPE_GHOST_UNVEILED'])]:
        declarations={m[1]:(m[2].split('//')[0].strip(),n) for n,line in enumerate(raw[path].decode().splitlines(),1) if (m:=re.match(r'#define\s+(\w+)\s+(.+)',line))}
        def value(node,seen):
            if isinstance(node,ast.Constant) and type(node.value) is int: return node.value
            if isinstance(node,ast.Name):
                need(node.id not in seen and node.id in declarations,'Invalid constant alias')
                return value(ast.parse(declarations[node.id][0],mode='eval').body,seen|{node.id})
            if isinstance(node,ast.BinOp) and isinstance(node.op,(ast.Add,ast.LShift)):
                a,b=value(node.left,seen),value(node.right,seen)
                return a+b if isinstance(node.op,ast.Add) else a<<b
            raise ValueError('Unsupported constant expression')
        for name in names:
            expression,line=declarations[name]
            constants[name]=dict(value=value(ast.parse(expression,mode='eval').body,{name}),sourceRef=dict(kind='pret',path=path,symbol=name,repository='pret/pokefirered',commit=COMMIT,sha256=sha(raw[path]),line=line))
    payload[region+'constants.json']=encode({k:v['value'] for k,v in constants.items()})
    payload[region+'constants-source.json']=encode(constant_source)
    inventory=json.loads(assets['overlays/scripts.json']); scriptpaths=sorted({b['sourceRef']['path'] for b in inventory['blocks']})
    payload[region+'runtime.json']=encode(dict(schemaVersion=1,profile='firered-pokemon-tower-region-v1',sourceCommit=COMMIT,fullSourceHash=SOURCE,maps=specs,scriptPaths=scriptpaths,initiallySetFlags=['FLAG_HIDE_POKEHOUSE_FUJI'],runtimeIntegrated=False))
    outputs=[dict(path=p.removeprefix(region),byteLength=len(b),sha256=sha(b)) for p,b in sorted(payload.items()) if p.startswith(region)]
    manifest=encode(dict(schemaVersion=1,profile='firered-pokemon-tower-region-v1',sourceCommit=COMMIT,fullSourceHash=SOURCE,sourceLockSha256=LOCK,preparationManifestSha256=GENERATED,outputs=outputs))
    payload[region+'manifest.json']=manifest
    entries.sort(key=lambda e:e['path']); need(len(entries)==127 and len({e['stableId'] for e in entries})==127, 'Map IDs differ')
    compactmanifest=encode(dict(schemaVersion=1,converterVersion='firered-runtime-map-v1',sourceCommit=COMMIT,converterSha256=sha(pathlib.Path(__file__).read_bytes()),maps=entries,sourceRegistry=registry))
    payload[runtime+'manifest.json']=compactmanifest
    for p,b in payload.items():
        q=safe(output,p)
        if check: need(q.is_file() and q.read_bytes()==b, 'Stale output '+p)
        else:
            q.parent.mkdir(parents=True,exist_ok=True); stage=safe(output,p+'.tmp'); stage.write_bytes(b); stage.replace(q)
    return dict(maps=127,preservedMaps=120,newCells=3360,newBorderCells=28,newWarpEvents=15,existingHouseWarps=3,sourceFiles=244,regionManifestSha256=sha(manifest),runtimeManifestSha256=sha(compactmanifest),converterSha256=sha(pathlib.Path(__file__).read_bytes()),files=len(payload),bytes=sum(map(len,payload.values())),runtimeIntegrated=False)
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__); p.add_argument('--project-root',type=pathlib.Path,required=True); p.add_argument('--preparation',type=pathlib.Path,required=True); p.add_argument('--output',type=pathlib.Path,required=True); p.add_argument('--check',action='store_true'); a=p.parse_args()
    print(json.dumps(run(a.project_root,a.preparation,a.output,a.check),sort_keys=True))
