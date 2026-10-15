#!/usr/bin/env python3
"""Build the Pokemon Tower compact map package from its source recipe."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import publish_constants
import sys as _map_sys
_map_sys.path.insert(0, str(__import__('pathlib').Path(__file__).resolve().parent))
from map_runtime import normalize_runtime, VERSION
import argparse, ast, base64,  json, pathlib, re, struct
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SIDECARS = (
    'celadon-approach-region-source-lock.json',
    'celadon-gym-region-source-lock.json',
    'city-services-source-lock.json',
    'rocktunnel-region-source-lock.json',
    'route11-region-source-lock.json',
    'ssanne-source-lock.json',
    'rocket-hideout-region-source-lock.json',
    'vermilion-gym-source-lock.json',
)
HOUSE = 'map.lavender-town-volunteer-pokemon-house'
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
    def read(root,path):
        with safe(root,path).open('rb') as f: b=f.read(16*1024*1024+1)
        need(0 < len(b) <= 16 * 1024 * 1024, 'Input size out of bounds: '+path)
        return b
    lockbytes=read(preparation, 'manifests/source-lock.json'); lock=json.loads(lockbytes)
    generated=json.loads(read(preparation, 'manifests/generated.json')); raw={}
    need(lock['commit'] == COMMIT and len(lock['files']) == 244, 'Source identity/count differs')
    for f in lock['files']:
        b=read(preparation, 'raw/' + f['path'])
        raw[f['path']]=b; payload['content/raw/pokefirered/037335f4/'+f['path']]=b
    assets={f['path']:read(preparation, f['path']) for f in generated['outputs']}
    region='content/generated/pokemon-tower-region/'; runtime='content/generated/pokemon-tower-runtime-maps/'
    previous=project/'content/generated/rocket-hideout-runtime-maps'
    previousbytes=read(previous, 'manifest.json'); old=json.loads(previousbytes)
    need(len(old['maps'])==120, 'Previous 120-map scope differs'); entries=[{k:e[k] for k in ('stableId','path')} for e in old['maps']]

    registry=dict(path='map-source-registry.json')

    payload[runtime+'pokemon-tower-region-source-lock.json']=lockbytes
    payload[runtime+'rocket-baseline-manifest.json']=previousbytes
    payload['scripts/pokemon-tower-region.lock.json']=lockbytes
    contract=json.loads(assets['overlays/pokemon-tower.json']); specs=[]
    for s in contract['maps']:
        name=s['directory'].split('/')[-1]; prefix='assets/maps/'+name+'/'
        compact=normalize_runtime(json.loads(assets[prefix+'runtime-map.json']))
        need(compact['events']==json.loads(raw['data/maps/'+s['sourceName']+'/map.json']), 'Events differ')
        for path in compact['scriptPaths']: need(path in raw, 'Missing script dependency: ' + path)
        is_new=s['stableId']!=HOUSE
        if is_new:
            for p,b in assets.items():
                if p.startswith(prefix) and p.endswith('.png'): payload[region+name+'/'+p.split('/')[-1]]=b
            path='pokemon-tower-region/'+name+'/runtime-map.json'; b=encode(compact); payload['content/generated/'+path]=b
            entries.append(dict(stableId=s['stableId'], path=path))
        else:
            old_house=next(e for e in old['maps'] if e['stableId']==HOUSE)
            prior=json.loads(read(project/'content/generated', old_house['path']))
            need(compact == prior, 'Existing Fuji house gameplay source differs')
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
            constants[name]=dict(value=value(ast.parse(expression,mode='eval').body,{name}),sourceRef=dict(kind='pret', path=path, symbol=name, repository='pret/pokefirered', commit=COMMIT, line=line))
    publish_constants(output / 'content/generated', constant_source, check=check)
    inventory=json.loads(assets['overlays/scripts.json']); scriptpaths=sorted({b['sourceRef']['path'] for b in inventory['blocks']})
    payload[region+'runtime.json']=encode(dict(schemaVersion=1, profile='firered-pokemon-tower-region-v1', sourceCommit=COMMIT, maps=specs, scriptPaths=scriptpaths, initiallySetFlags=['FLAG_HIDE_POKEHOUSE_FUJI'], runtimeIntegrated=False))
    outputs=[dict(path=p.removeprefix(region)) for p,b in sorted(payload.items()) if p.startswith(region)]
    manifest=encode(dict(schemaVersion=1, profile='firered-pokemon-tower-region-v1', sourceCommit=COMMIT, outputs=outputs))
    payload[region+'manifest.json']=manifest
    entries.sort(key=lambda e:e['path']); need(len(entries)==127 and len({e['stableId'] for e in entries})==127, 'Map IDs differ')
    compactmanifest=encode(dict(schemaVersion=2, converterVersion=VERSION, sourceCommit=COMMIT, maps=entries, sourceRegistry=registry))
    payload[runtime+'manifest.json']=compactmanifest
    for p,b in payload.items():
        q=safe(output,p)
        if check: need(q.is_file() and q.read_bytes()==b, 'Stale output '+p)
        else:
            q.parent.mkdir(parents=True,exist_ok=True); stage=safe(output,p+'.tmp'); stage.write_bytes(b); stage.replace(q)
    return dict(maps=127, preservedMaps=120, newCells=3360, newBorderCells=28, newWarpEvents=15, existingHouseWarps=3, sourceFiles=244, files=len(payload), bytes=sum(map(len, payload.values())), runtimeIntegrated=False)
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__); p.add_argument('--project-root',type=pathlib.Path,required=True); p.add_argument('--preparation',type=pathlib.Path,required=True); p.add_argument('--output',type=pathlib.Path,required=True); p.add_argument('--check',action='store_true'); a=p.parse_args()
    print(json.dumps(run(a.project_root,a.preparation,a.output,a.check),sort_keys=True))
