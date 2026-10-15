#!/usr/bin/env python3
"""Candidate 114-map package; exact prior 113 maps preserved. No campaign activation."""
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parent)
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
from runtime_map_manifest import compose, compose_region
import argparse,importlib.util,json,pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
SIDECARS = (
 'celadon-approach-region-source-lock.json',
 'city-services-source-lock.json',
 'rocktunnel-region-source-lock.json',
 'route11-region-source-lock.json',
 'ssanne-source-lock.json',
 'vermilion-gym-source-lock.json',
)
def require(ok,msg):
 if not ok:raise ValueError(msg)
def safe(root,p):
 require(p and not pathlib.PurePosixPath(p).is_absolute() and '\\' not in p and ':' not in p and all(x not in ('','.','..') for x in p.split('/')),'Unsafe relative path')
 q=pathlib.Path(root)/p;require(not any(x.is_symlink() for x in (q,*q.parents)),'Symlink path rejected');return q
def read(root,p):
 with safe(root,p).open('rb') as f:b=f.read(16*1024*1024+1)
 require(0 < len(b) <= 16 * 1024 * 1024,'Input size out of bounds: '+p);return b
def read_entry(root,e):return read(root,e['path'])
def run(project,previous,region,out,check=False):
    generated = pathlib.Path(project) / 'content/generated'
    region = pathlib.Path(region)
    catalog = json.loads((region / 'celadon-gym-catalog.json').read_bytes())
    package = region.resolve().relative_to(generated.resolve()).as_posix()
    return compose(generated, out, [package + '/' + row['directory'] for row in catalog['maps']], previous=previous, expected_count=114, check=check)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--previous',type=pathlib.Path);p.add_argument('--region',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-region');p.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-runtime-maps');p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.previous or a.project_root/'content/generated/celadon-approach-runtime-maps',a.region,a.output,a.check),sort_keys=True))
