#!/usr/bin/env python3
"""Authenticate original Silph presentation blobs without executing source scripts."""
import json,pathlib,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-silph-presentation-v1'
SOURCE_COUNT=116
SPECIES=('ALAKAZAM','MR_MIME','LAPRAS')
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 require(not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and not any(x in ('','.','..') for x in rel.split('/')),'Unsafe relative path')
 p=pathlib.Path(root).absolute()/rel
 require(not any(x.is_symlink() for x in (p,*p.parents)),'Symlink rejected')
 return p
def authenticated(root,reference=None):
 b=safe(root,'scripts/silph-presentation.lock.json').read_bytes();lock=json.loads(b)
 require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], len(lock['files'])) == (1, 'pret/pokefirered', COMMIT, PROFILE, SOURCE_COUNT),'Silph source scope differs')
 if reference:
  require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/pokefirered/037335f4/'+row['path'])
  require(row['path'] not in raw,'Duplicate source input')
  b=subprocess.check_output(['git','-C',str(reference),'cat-file','blob',COMMIT + ':' + row['path']]) if reference else p.read_bytes()
  raw[row['path']]=b
 if reference:
  for path,b in raw.items():
   p=safe(root,'content/raw/pokefirered/037335f4/'+path);p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw

if __name__=='__main__':
 import argparse
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);a=p.parse_args();lock,raw=authenticated(a.project_root,a.reference_root);print(json.dumps(dict(passed=True,inputs=len(raw))))
