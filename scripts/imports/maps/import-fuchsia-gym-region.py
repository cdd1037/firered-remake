#!/usr/bin/env python3
"""Offline authenticated one-map Fuchsia Gym source candidate. Does not activate gameplay."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_runtime import publish_map_package, generated_root

from shared_constants import package_documents
import argparse,importlib.util,json,pathlib,re,shutil,tempfile
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-fuchsia-gym-region-import-v1'
MAPS=[('FuchsiaCity_Gym','map.fuchsia-city-gym','fuchsia-city-gym')]
TILESETS={'gTileset_Building':'primary/building','gTileset_FuchsiaGym':'secondary/fuchsia_gym'}
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,relative):
 p=pathlib.PurePosixPath(relative)
 if p.is_absolute() or '\\' in relative or any(x in ('','.','..') for x in relative.split('/')):raise ValueError('Unsafe relative path')
 q=pathlib.Path(root)/relative
 if any(x.is_symlink() for x in [q,*q.parents]):raise ValueError('Symlink rejected')
 return q

def module(path, name):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def read_lock(path=None):
 b=(path or safe(ROOT,'scripts/recipes/maps/fuchsia-gym-region.lock.json')).read_bytes()
 d=json.loads(b)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile'], d['gameVersion'], d['revision'], d['completeGame'], d['maps']) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0, False, ['FuchsiaCity_Gym']):raise ValueError('Gym source identity differs')
 if len(d['files'])!=95 or len({e['path'] for e in d['files']})!=95:raise ValueError('Gym source closure differs')
 for e in d['files']:safe(ROOT,e['path'])
 return d

def protect(target,project,allowed_stem,kind):
 target=pathlib.Path(target).absolute();safe(target,'sentinel')
 for root in {ROOT.resolve(),pathlib.Path(project).resolve()}:
  for p in [root/'scripts',root/'src',root/'tests',root/'.git',root/'scripts/recipes/config']:
   if target.resolve().is_relative_to(p) or p.is_relative_to(target.resolve()):raise ValueError('Protected project path overlap')
  for k in ('raw','generated'):
   base=root/'content'/k
   if base.exists():
    for p in base.iterdir():
     if k==kind and p.name==allowed_stem:continue
     if target.resolve().is_relative_to(p.resolve()) or p.resolve().is_relative_to(target.resolve()):raise ValueError('Existing profile overlap')


def run(project,raw,out,reference=None):
 project,raw,out=map(pathlib.Path,(project,raw,out));protect(raw,project,'fuchsia-gym-region','raw');protect(out,project,'fuchsia-gym-region','generated')
 if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):raise ValueError('Source/output overlap')
 shared=module(safe(project, 'scripts/imports/maps/import-route22-region.py'), '_fuchsia_gym_renderer')
 for key in ('ROOT','COMMIT','TREE','PROFILE','MAPS','TILESETS','read_lock'):setattr(shared,key,globals()[key])
 if reference:shared.materialize(raw,pathlib.Path(reference),source_package='fuchsia-gym-region')
 src=shared.Source(raw);inventory=module(safe(ROOT, 'scripts/imports/story/import-fuchsia-gym-scripts.py'), '_fuchsia_gym_inventory')
 converter=module(safe(project, 'scripts/imports/maps/export-runtime-maps.py'), '_fuchsia_gym_compact')
 out.parent.mkdir(parents=True,exist_ok=True)
 with tempfile.TemporaryDirectory(prefix='.fuchsia-gym-stage-',dir=out.parent) as tmp:
  stage=pathlib.Path(tmp);world=shared.import_map(src,*MAPS[0][:2],stage/MAPS[0][2]);worlds=[world]
  scripts=inventory.script_inventory(src,worlds);constants=shared.constants_inventory(src,worlds,scripts)
  npcs=shared.npc_inventory(src,worlds,stage/'npc');connections,warps=shared.connections_and_warps(src,worlds)
  catalog=dict(schemaVersion=1,commit=COMMIT,maps=[dict(sourceName=MAPS[0][0],stableId=MAPS[0][1],directory=MAPS[0][2],sourceMapSymbol=world['events']['id'])],connections=connections,warps=warps)
  for p,doc in package_documents(generated_root(out), constants, {'fuchsia-gym-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs}).items():safe(stage,p).write_bytes(canonical(doc))
  rich=safe(stage,'fuchsia-city-gym/runtime-map.json').read_bytes();safe(stage,'fuchsia-city-gym/runtime-map.json').write_bytes(converter.encode(converter.normalize_runtime(json.loads(rich))))
  publish_map_package(stage)
  outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
  manifest=dict(schemaVersion=1,outputs=outputs)
  b=canonical(manifest)
  safe(stage,'manifest.json').write_bytes(b)
  for p in sorted(stage.rglob('*')):
   if p.is_file():safe(out,p.relative_to(stage).as_posix())
  for p in sorted(stage.rglob('*')):
   if p.is_file():q=safe(out,p.relative_to(stage).as_posix());q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
 safe(out,'dependencies.json').unlink(missing_ok=True)
 return {k:v for k,v in manifest.items() if k!='outputs'}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/fuchsia-gym');a=p.parse_args();print(json.dumps(run(a.project_root,a.raw,a.out,a.reference_repo),sort_keys=True))
