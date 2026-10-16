#!/usr/bin/env python3
"""Retain the locked 44 required original presentation inputs; no upstream execution or sound conversion."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json,pathlib,subprocess
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe input path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in [q,*q.parents]):raise ValueError('Symlink rejected')
 return q

def run(reference=None):
 raw=ROOT/'content/raw/pokefirered/037335f4';b=safe(ROOT,'scripts/recipes/story/celadon-gym-resources.lock.json').read_bytes()
 lock=json.loads(b)
 if (lock['commit'],len(lock['files']))!=(COMMIT,44):raise ValueError('Resource identity differs')
 def git(*args):return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args])
 verified=[]
 for e in lock['files']:
  p=e['path'];q=safe(raw,p)
  if reference:
   b=git('show',COMMIT+':'+p)
  else:b=q.read_bytes()
  verified.append((q,b))
 if reference:
  for q,b in verified:q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 return dict(sourceFiles=44,bytes=sum(len(b) for _,b in verified),decoded=False)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);a=p.parse_args();print(json.dumps(run(a.reference_repo),sort_keys=True))
