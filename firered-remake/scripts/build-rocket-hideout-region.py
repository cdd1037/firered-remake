#!/usr/bin/env python3
"""Authenticate prepared Rocket source and add six compact maps to the exact prior 114-map package."""
import argparse, base64, hashlib, json, pathlib, re, struct
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
SOURCE='ef06d493b1ddcd9cc86aa831576e0778d04e90882a3d827b2448d577ea7afefb'
LOCK='e5447964ad69cb9568018feb793d419a6fb3f26d752c8242ab5bc8136977c0b7'
GENERATED='d531cab6720c13996f7b154acef311ad917395bb5035434ab6211c525e0fdf1e'
PREVIOUS='50df37627109ea609f2b2cab2591849a81554077bccfe996e31bc573c4a9a9d4'
SIDECARS={'celadon-approach-region-source-lock.json': 'b09ce111e7a091e7cc551f0d4660fb468f9a805340af0f80056b12d1db00242a', 'celadon-gym-region-source-lock.json': '0237b9b50415fd60f6eb9fd4c7ba6adbababdfcddc085d8147c4d767dcf04be5', 'city-services-source-lock.json': '4b71a3820a2d50ff80c32589c60979cb519dd7aeecec9bec13696bf1ad317eb5', 'rocktunnel-region-source-lock.json': '4c687593e368f67cc7db73c0255de541a8cb5b19590326c5562993087f4114c8', 'route11-region-source-lock.json': '6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304', 'ssanne-source-lock.json': '72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1', 'vermilion-gym-source-lock.json': '569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8'}
def sha(b): return hashlib.sha256(b).hexdigest()
def encode(d): return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def need(ok,msg):
 if not ok: raise ValueError(msg)
def safe(root,path):
 need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and ':' not in path and all(x not in ('','.','..') for x in path.split('/')),'Unsafe path')
 q=root/path;need(not any(x.is_symlink() for x in (q,*q.parents)),'Symlink rejected');return q

def run(project,preparation,output,check=False):
 project,preparation,output=map(pathlib.Path,(project,preparation,output));payload={}
 need(not output.resolve().is_relative_to(preparation.resolve()) and not preparation.resolve().is_relative_to(output.resolve()),'Preparation output overlap')
 def read(root,path,digest,length=None):
  with safe(root,path).open('rb') as f: b=f.read(16*1024*1024+1)
  need(0<len(b)<=16*1024*1024 and sha(b)==digest and (length is None or len(b)==length),'Pinned bytes differ: '+path);return b
 lockbytes=read(preparation,'manifests/source-lock.json',LOCK);lock=json.loads(lockbytes)
 generated=json.loads(read(preparation,'manifests/generated.json',GENERATED));raw={}
 need(lock['commit']==COMMIT and len(lock['files'])==175 and generated['fullSourceHash']==SOURCE,'Source identity/count differs')
 for f in lock['files']:
  b=read(preparation,'raw/'+f['path'],f['sha256'],f['byteLength']);need(hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==f['gitBlobSha1'],'Git blob differs')
  raw[f['path']]=b;payload['content/raw/rocket-hideout-region/'+f['path']]=b
 need(sha(''.join(p+'\0'+sha(b)+'\n' for p,b in sorted(raw.items())).encode())==SOURCE,'Source set differs')
 assets={f['path']:read(preparation,f['path'],f['sha256'],f['byteLength']) for f in generated['outputs']}
 region='content/generated/rocket-hideout-region/';runtime='content/generated/rocket-hideout-runtime-maps/'
 previous=project/'content/generated/celadon-gym-runtime-maps';old=json.loads(read(previous,'manifest.json',PREVIOUS))
 need(len(old['maps'])==114,'Previous 114-map scope differs');entries=list(old['maps'])
 for e in entries: payload[runtime+e['path']]=read(previous,e['path'],e['sha256'],e['byteLength'])
 registry=old['sourceRegistry'];payload[runtime+registry['path']]=read(previous,registry['path'],registry['sha256'],registry['byteLength'])
 for p,h in SIDECARS.items(): payload[runtime+p]=read(previous,p,h)
 support=read(pathlib.Path(__file__).resolve().parent,'rocket-hideout-runtime-support/maps.h','fd72a95bb4bd8c589630826d745814beaafe7a8518ffa3fb7927bf16d670a7dd',1114)
 need(hashlib.sha1(b'blob 1114\0'+support).hexdigest()=='fe2b7fd14fe6d26fd86c0e50c2bbbc7997d0a4af','Dynamic warp declaration blob differs')
 payload['content/raw/rocket-hideout-runtime-support/include/constants/maps.h']=support
 payload[runtime+'rocket-hideout-region-source-lock.json']=lockbytes
 payload['scripts/rocket-hideout-region.lock.json']=lockbytes
 contract=json.loads(assets['overlays/rocket-hideout.json']);specs=[]
 for s in contract['maps']:
  name=s['directory'].split('/')[-1];prefix='assets/maps/'+name+'/';world=json.loads(assets[prefix+'world.json']);compact=json.loads(assets[prefix+'runtime-map.json'])
  need(compact['sourceWorldSha256']==sha(assets[prefix+'world.json']) and compact['fullSourceHash']==SOURCE,'Compact rich binding differs')
  mapjson=raw['data/maps/'+s['sourceName']+'/map.json'];need(compact['events']==json.loads(mapjson),'Events differ')
  need(base64.b64decode(compact['blocks'])==b''.join(struct.pack('<H',c['rawBlock']) for c in world['cells']),'Cell blocks differ')
  for script in compact['scripts']:need(script['text'].encode()==raw[script['path']],'Script bytes differ')
  for art in compact['artifacts']: need(sha(assets[prefix+art['path']])==art['sha256'],'PNG differs')
  for p,b in assets.items():
   if p.startswith(prefix) and p.endswith('.png'):payload[region+name+'/'+p.split('/')[-1]]=b
  path='rocket-hideout-region/'+name+'/runtime-map.json';b=assets[prefix+'runtime-map.json'];payload[runtime+path]=b
  entries.append(dict(stableId=s['stableId'],path=path,byteLength=len(b),sha256=sha(b),sourcePath=path.replace('runtime-map.json','world.json'),sourceByteLength=len(assets[prefix+'world.json']),sourceWorldSha256=sha(assets[prefix+'world.json']),fullSourceHash=SOURCE))
  specs.append(dict(stableId=s['stableId'],sourceName=s['sourceName'],sourceMapSymbol=compact['sourceRef']['symbol'],directory=name,width=compact['width'],height=compact['height']))
 for p,b in assets.items():
  if p.startswith('assets/npc/') or p.startswith('assets/patches/'): payload[region+p.removeprefix('assets/')]=b
 for name in ['dependencies','npc-source','scripts','spin-routes','rocket-hideout']:payload[region+name+'.json']=assets['overlays/'+name+'.json']
 constants=json.loads(assets['overlays/constants.json'])['values'];payload[region+'constants.json']=encode({k:v['value'] for k,v in constants.items()})
 inventory=json.loads(assets['overlays/scripts.json']);scriptpaths=sorted({b['sourceRef']['path'] for b in inventory['blocks']})
 payload[region+'runtime.json']=encode(dict(schemaVersion=1,profile='firered-rocket-hideout-region-v1',sourceCommit=COMMIT,fullSourceHash=SOURCE,maps=specs,scriptPaths=scriptpaths,initiallySetFlags=['FLAG_HIDE_LIFT_KEY','FLAG_HIDE_SILPH_SCOPE']))
 outputs=[dict(path=p.removeprefix(region),byteLength=len(b),sha256=sha(b)) for p,b in sorted(payload.items()) if p.startswith(region)]
 manifest=encode(dict(schemaVersion=1,profile='firered-rocket-hideout-region-v1',sourceCommit=COMMIT,fullSourceHash=SOURCE,sourceLockSha256=LOCK,preparationManifestSha256=GENERATED,outputs=outputs));payload[region+'manifest.json']=manifest
 entries.sort(key=lambda e:e['path']);need(len(entries)==120 and len({e['stableId'] for e in entries})==120,'Map IDs differ')
 compactmanifest=encode(dict(schemaVersion=1,converterVersion='firered-runtime-map-v1',sourceCommit=COMMIT,converterSha256=sha(pathlib.Path(__file__).read_bytes()),maps=entries,sourceRegistry=registry));payload[runtime+'manifest.json']=compactmanifest
 for p,b in payload.items():
  q=safe(output,p)
  if check:need(q.is_file() and q.read_bytes()==b,'Stale output '+p)
  else:q.parent.mkdir(parents=True,exist_ok=True);stage=safe(output,p+'.tmp');stage.write_bytes(b);stage.replace(q)
 return dict(maps=120,preservedMaps=114,newCells=3179,newBorderCells=24,newWarpEvents=22,dynamicExits=2,sourceFiles=175,regionManifestSha256=sha(manifest),runtimeManifestSha256=sha(compactmanifest),converterSha256=sha(pathlib.Path(__file__).read_bytes()),files=len(payload),bytes=sum(map(len,payload.values())))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,required=True);p.add_argument('--preparation',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.preparation,a.output,a.check),sort_keys=True))
