#!/usr/bin/env python3
"""Build the Rocket Hideout compact map package from its source recipe."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_constants import publish_constants
from map_runtime import normalize_runtime
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
 region='content/generated/maps/regions/rocket-hideout/';runtime='content/generated/maps/catalogs/rocket-hideout-maps.json'
 previous=project/'content/generated/maps/catalogs/celadon-gym-maps.json';old=json.loads(read(previous.parent, previous.name))
 need(len(old['maps'])==114,'Previous 114-map scope differs');entries=[{k:e[k] for k in ('stableId','path')} for e in old['maps']]

 registry=dict(path='maps/metadata.json')

 support=read(project, 'content/raw/pokefirered/037335f4/include/constants/maps.h')
 payload['content/raw/pokefirered/037335f4/include/constants/maps.h']=support
 payload['scripts/recipes/maps/rocket-hideout-region.lock.json']=lockbytes
 contract=json.loads(assets['overlays/rocket-hideout.json']);specs=[]
 for s in contract['maps']:
  name=s['directory'].split('/')[-1];prefix='assets/maps/'+name+'/';compact=normalize_runtime(json.loads(assets[prefix+'runtime-map.json']))
  compact['texturePath']=name+'.'+compact['texturePath']
  compact['artifacts']=[dict(a,path=name+'.'+a['path']) for a in compact['artifacts']]
  mapjson=raw['data/maps/'+s['sourceName']+'/map.json'];need(compact['events']==json.loads(mapjson),'Events differ')
  for path in compact['scriptPaths']: need(path in raw, 'Missing script dependency: ' + path)
  for p,b in assets.items():
   if p.startswith(prefix) and p.endswith('.png'):payload[region+name+'.'+p.split('/')[-1]]=b
  path='maps/regions/rocket-hideout/'+name+'.json';b=encode(compact);payload['content/generated/'+path]=b
  entries.append(dict(stableId=s['stableId'], path=path))
  specs.append(dict(stableId=s['stableId'],sourceName=s['sourceName'],sourceMapSymbol=compact['sourceMapSymbol'],mapPath=name+'.json',width=compact['width'],height=compact['height']))
 for p,b in assets.items():
  if p.startswith('assets/patches/'): payload[region+p.removeprefix('assets/')]=b
 for name in ['npc-source','spin-routes','rocket-hideout']:payload[region+name+'.json']=assets['overlays/'+name+'.json']
 npcs=json.loads(assets['overlays/npc-source.json'])
 npcs['graphics']=[{'graphicsId':row['graphicsId']} for row in npcs['graphics']]
 payload[region+'npc-source.json']=encode(npcs)
 constants=json.loads(assets['overlays/constants.json'])
 publish_constants(output / 'content/generated', constants, check=check)
 inventory=json.loads(assets['overlays/scripts.json']);scriptpaths=sorted({b['sourceRef']['path'] for b in inventory['blocks']})
 payload[region+'runtime.json']=encode(dict(schemaVersion=1,   maps=specs, scriptPaths=scriptpaths, initiallySetFlags=['FLAG_HIDE_LIFT_KEY', 'FLAG_HIDE_SILPH_SCOPE']))
 entries.sort(key=lambda e:e['path']);need(len(entries)==120 and len({e['stableId'] for e in entries})==120,'Map IDs differ')
 compactmanifest=encode(dict(schemaVersion=3, maps=entries, sourceRegistry=registry));payload[runtime]=compactmanifest
 for p,b in payload.items():
  q=safe(output,p)
  if check:need(q.is_file() and q.read_bytes()==b,'Stale output '+p)
  else:q.parent.mkdir(parents=True,exist_ok=True);stage=safe(output,p+'.tmp');stage.write_bytes(b);stage.replace(q)
 retired=safe(output,region+'dependencies.json')
 if check:need(not retired.exists(),'Retired dependency inventory remains')
 else:retired.unlink(missing_ok=True)
 return dict(maps=120, preservedMaps=114, newCells=3179, newBorderCells=24, newWarpEvents=22, dynamicExits=2, sourceFiles=175, files=len(payload), bytes=sum(map(len, payload.values())))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,required=True);p.add_argument('--preparation',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.preparation,a.output,a.check),sort_keys=True))
