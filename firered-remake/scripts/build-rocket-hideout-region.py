#!/usr/bin/env python3
"""Build the Rocket Hideout compact map package from its source recipe."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import publish_constants
import sys as _map_sys
_map_sys.path.insert(0, str(__import__('pathlib').Path(__file__).resolve().parent))
from map_runtime import normalize_runtime, VERSION
import argparse, base64,  json, pathlib, re, struct
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
SIDECARS = (
    'celadon-approach-region-source-lock.json',
    'celadon-gym-region-source-lock.json',
    'city-services-source-lock.json',
    'rocktunnel-region-source-lock.json',
    'route11-region-source-lock.json',
    'ssanne-source-lock.json',
    'vermilion-gym-source-lock.json',
)
def encode(d): return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def need(ok,msg):
 if not ok: raise ValueError(msg)
def safe(root,path):
 need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and ':' not in path and all(x not in ('','.','..') for x in path.split('/')),'Unsafe path')
 q=root/path;need(not any(x.is_symlink() for x in (q,*q.parents)),'Symlink rejected');return q

def run(project,preparation,output,check=False):
 project,preparation,output=map(pathlib.Path,(project,preparation,output));payload={}
 need(not output.resolve().is_relative_to(preparation.resolve()) and not preparation.resolve().is_relative_to(output.resolve()),'Preparation output overlap')
 def read(root,path):
  with safe(root,path).open('rb') as f: b=f.read(16*1024*1024+1)
  need(0 < len(b) <= 16 * 1024 * 1024,'Input size out of bounds: '+path);return b
 lockbytes=read(preparation, 'manifests/source-lock.json');lock=json.loads(lockbytes)
 generated=json.loads(read(preparation, 'manifests/generated.json'));raw={}
 need(lock['commit'] == COMMIT and len(lock['files']) == 175,'Source identity/count differs')
 for f in lock['files']:
  b=read(preparation, 'raw/' + f['path'])
  raw[f['path']]=b;payload['content/raw/pokefirered/037335f4/'+f['path']]=b
 assets={f['path']:read(preparation, f['path']) for f in generated['outputs']}
 region='content/generated/rocket-hideout-region/';runtime='content/generated/rocket-hideout-runtime-maps/'
 previous=project/'content/generated/celadon-gym-runtime-maps';old=json.loads(read(previous, 'manifest.json'))
 need(len(old['maps'])==114,'Previous 114-map scope differs');entries=[{k:e[k] for k in ('stableId','path')} for e in old['maps']]

 registry=dict(path='map-source-registry.json')

 support=read(pathlib.Path(__file__).resolve().parent, 'rocket-hideout-runtime-support/maps.h')
 payload['content/raw/pokefirered/037335f4/include/constants/maps.h']=support
 payload[runtime+'rocket-hideout-region-source-lock.json']=lockbytes
 payload['scripts/rocket-hideout-region.lock.json']=lockbytes
 contract=json.loads(assets['overlays/rocket-hideout.json']);specs=[]
 for s in contract['maps']:
  name=s['directory'].split('/')[-1];prefix='assets/maps/'+name+'/';compact=normalize_runtime(json.loads(assets[prefix+'runtime-map.json']))
  mapjson=raw['data/maps/'+s['sourceName']+'/map.json'];need(compact['events']==json.loads(mapjson),'Events differ')
  for path in compact['scriptPaths']: need(path in raw, 'Missing script dependency: ' + path)
  for p,b in assets.items():
   if p.startswith(prefix) and p.endswith('.png'):payload[region+name+'/'+p.split('/')[-1]]=b
  path='rocket-hideout-region/'+name+'/runtime-map.json';b=encode(compact);payload['content/generated/'+path]=b
  entries.append(dict(stableId=s['stableId'], path=path))
  specs.append(dict(stableId=s['stableId'],sourceName=s['sourceName'],sourceMapSymbol=compact['sourceRef']['symbol'],directory=name,width=compact['width'],height=compact['height']))
 for p,b in assets.items():
  if p.startswith('assets/patches/'): payload[region+p.removeprefix('assets/')]=b
 for name in ['dependencies','npc-source','spin-routes','rocket-hideout']:payload[region+name+'.json']=assets['overlays/'+name+'.json']
 npcs=json.loads(assets['overlays/npc-source.json'])
 npcs['graphics']=[{'graphicsId':row['graphicsId']} for row in npcs['graphics']]
 payload[region+'npc-source.json']=encode(npcs)
 constants=json.loads(assets['overlays/constants.json'])
 publish_constants(output / 'content/generated', constants, check=check)
 inventory=json.loads(assets['overlays/scripts.json']);scriptpaths=sorted({b['sourceRef']['path'] for b in inventory['blocks']})
 payload[region+'runtime.json']=encode(dict(schemaVersion=1, profile='firered-rocket-hideout-region-v1', sourceCommit=COMMIT, maps=specs, scriptPaths=scriptpaths, initiallySetFlags=['FLAG_HIDE_LIFT_KEY', 'FLAG_HIDE_SILPH_SCOPE']))
 outputs=[dict(path=p.removeprefix(region)) for p,b in sorted(payload.items()) if p.startswith(region)]
 manifest=encode(dict(schemaVersion=1, profile='firered-rocket-hideout-region-v1', sourceCommit=COMMIT, outputs=outputs));payload[region+'manifest.json']=manifest
 entries.sort(key=lambda e:e['path']);need(len(entries)==120 and len({e['stableId'] for e in entries})==120,'Map IDs differ')
 compactmanifest=encode(dict(schemaVersion=2, converterVersion=VERSION, sourceCommit=COMMIT, maps=entries, sourceRegistry=registry));payload[runtime+'manifest.json']=compactmanifest
 for p,b in payload.items():
  q=safe(output,p)
  if check:need(q.is_file() and q.read_bytes()==b,'Stale output '+p)
  else:q.parent.mkdir(parents=True,exist_ok=True);stage=safe(output,p+'.tmp');stage.write_bytes(b);stage.replace(q)
 return dict(maps=120, preservedMaps=114, newCells=3179, newBorderCells=24, newWarpEvents=22, dynamicExits=2, sourceFiles=175, files=len(payload), bytes=sum(map(len, payload.values())))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,required=True);p.add_argument('--preparation',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.preparation,a.output,a.check),sort_keys=True))
