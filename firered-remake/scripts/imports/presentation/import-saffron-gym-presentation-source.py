#!/usr/bin/env python3
"""Authenticate original Saffron presentation blobs without executing source scripts."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json,pathlib,subprocess,sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-saffron-gym-presentation-v1'
SOURCE_COUNT=31
SPECIES=('SLOWBRO',)
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 require(not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and not any(x in ('','.','..') for x in rel.split('/')),'Unsafe relative path')
 p=pathlib.Path(root).absolute()/rel
 require(not any(x.is_symlink() for x in (p,*p.parents)),'Symlink rejected')
 return p
def authenticated(root,reference=None):
 b=safe(root,'scripts/recipes/presentation/saffron-gym-presentation.lock.json').read_bytes();lock=json.loads(b)
 require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], len(lock['files'])) == (1, 'pret/pokefirered', COMMIT, PROFILE, SOURCE_COUNT),'Saffron source scope differs')
 if reference:require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/pokefirered/037335f4/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw
