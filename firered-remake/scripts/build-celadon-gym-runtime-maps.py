#!/usr/bin/env python3
"""Candidate 114-map package; exact prior 113 maps preserved. No campaign activation."""
import argparse,hashlib,importlib.util,json,pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PREVIOUS_MANIFEST='c18a66b9b2ce6adbfb1ad6e6e789e3d14e361fd70e362a29b9f6c4725d316c2c'
REGION_MANIFEST='91987e76510d0499fb043ab05abeac562d569911df0ec9c5375324cb74a97f76'
REGION_LOCK='0237b9b50415fd60f6eb9fd4c7ba6adbababdfcddc085d8147c4d767dcf04be5'
SOURCE='ad4d0ea8ef39bb5a64f65ca53cb5c843d8cca4cef88a01f19031aa1bac14e746'
SIDECARS={
'celadon-approach-region-source-lock.json':'b09ce111e7a091e7cc551f0d4660fb468f9a805340af0f80056b12d1db00242a',
'city-services-source-lock.json':'4b71a3820a2d50ff80c32589c60979cb519dd7aeecec9bec13696bf1ad317eb5',
'rocktunnel-region-source-lock.json':'4c687593e368f67cc7db73c0255de541a8cb5b19590326c5562993087f4114c8',
'route11-region-source-lock.json':'6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304',
'ssanne-source-lock.json':'72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1',
'vermilion-gym-source-lock.json':'569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8'}
def sha(b):return hashlib.sha256(b).hexdigest()
def require(ok,msg):
 if not ok:raise ValueError(msg)
def safe(root,p):
 require(p and not pathlib.PurePosixPath(p).is_absolute() and '\\' not in p and ':' not in p and all(x not in ('','.','..') for x in p.split('/')),'Unsafe relative path')
 q=pathlib.Path(root)/p;require(not any(x.is_symlink() for x in (q,*q.parents)),'Symlink path rejected');return q
def read(root,p,digest,length=None):
 with safe(root,p).open('rb') as f:b=f.read(16*1024*1024+1)
 require(0<len(b)<=16*1024*1024 and (length is None or len(b)==length) and sha(b)==digest,'Pinned input differs: '+p);return b
def authenticated(root,e):return read(root,e['path'],e['sha256'],e['byteLength'])
def run(project,previous,region,out,check=False):
 project,previous,region,out=map(pathlib.Path,(project,previous,region,out))
 for source in (previous,region,project/'scripts',ROOT/'scripts',ROOT/'content/raw',project/'src',project/'tests',project/'.git',ROOT/'src',ROOT/'tests'):
  require(not source.resolve().is_relative_to(out.resolve()) and not out.resolve().is_relative_to(source.resolve()),'Source/output overlap')
 converter=safe(project,'scripts/export-runtime-maps.py');require(sha(converter.read_bytes())=='9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8','Reviewed converter differs')
 spec=importlib.util.spec_from_file_location('_celadon_gym_compact',converter);base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
 old=json.loads(read(previous,'manifest.json',PREVIOUS_MANIFEST));require(len(old['maps'])==113 and old['sourceCommit']==COMMIT and old['converterVersion']==base.VERSION,'Prior runtime scope differs')
 manifest=json.loads(read(region,'manifest.json',REGION_MANIFEST));require(manifest['mapCount']==1 and manifest['cellCount']==260 and manifest['fullSourceHash']==SOURCE,'Gym region scope differs')
 inventory={e['path']:e for e in manifest['outputs']};lock=read(ROOT,'scripts/celadon-gym-region.lock.json',REGION_LOCK)
 catalog=json.loads(authenticated(region,inventory['celadon-gym-catalog.json']));require(json.loads(lock)['files']==catalog['inputFiles'],'Gym source lock/catalog differs')
 payload={};entries=[]
 for e in old['maps']:
  require(e['path'] not in payload,'Duplicate prior path');payload[e['path']]=authenticated(previous,e);entries.append(e)
 registry=old['sourceRegistry'];require(registry['path']=='source-registry.json','Source registry differs');payload[registry['path']]=authenticated(previous,registry)
 for p,h in SIDECARS.items():payload[p]=read(previous,p,h)
 payload['celadon-gym-region-source-lock.json']=lock
 rich=authenticated(region,inventory['celadon-city-gym/world.json']);compact=authenticated(region,inventory['celadon-city-gym/runtime-map.json'])
 expected=base.convert(rich,sha(rich),SOURCE);require(compact==base.encode(expected) and expected['stableId']=='map.celadon-city-gym','Gym compact derivative differs')
 p='celadon-gym-region/celadon-city-gym/runtime-map.json';payload[p]=compact
 entries.append(dict(stableId='map.celadon-city-gym',path=p,byteLength=len(compact),sha256=sha(compact),sourcePath='celadon-gym-region/celadon-city-gym/world.json',sourceByteLength=len(rich),sourceWorldSha256=sha(rich),fullSourceHash=SOURCE))
 entries.sort(key=lambda e:e['path']);require(len(entries)==114 and len({e['stableId'] for e in entries})==114,'Candidate map scope differs')
 result=dict(schemaVersion=1,converterVersion=base.VERSION,sourceCommit=COMMIT,converterSha256=sha(pathlib.Path(__file__).read_bytes()),maps=entries,sourceRegistry=registry)
 payload['manifest.json']=base.encode(result)
 for p in payload:safe(out,p);safe(out,p+'.tmp')
 for p,b in payload.items():
  q=safe(out,p)
  if check:require(q.is_file() and q.read_bytes()==b,'Stale runtime output: '+p)
  else:q.parent.mkdir(parents=True,exist_ok=True);stage=safe(out,p+'.tmp');stage.write_bytes(b);stage.replace(q)
 return dict(maps=114,preservedMaps=113,newCells=260,newBorderCells=4,newWarps=3,runtimeBytes=sum(map(len,payload.values())),manifestSha256=sha(payload['manifest.json']),runtimeIntegrated=False)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--previous',type=pathlib.Path);p.add_argument('--region',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-region');p.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-runtime-maps');p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.previous or a.project_root/'content/generated/celadon-approach-runtime-maps',a.region,a.output,a.check),sort_keys=True))
