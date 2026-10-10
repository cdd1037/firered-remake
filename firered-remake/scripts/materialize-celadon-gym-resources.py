#!/usr/bin/env python3
"""Retain the locked 44 required original presentation inputs; no upstream execution or sound conversion."""
import argparse,hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOCK='369fb53ee4c06a96502bc99a50a8100e208436e30e21f5028058ae16c70bc0c8'
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
def sha(b):return hashlib.sha256(b).hexdigest()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe input path')
 q=pathlib.Path(root)/p
 if any(x.is_symlink() for x in [q,*q.parents]):raise ValueError('Symlink rejected')
 return q

def run(reference=None):
 raw=ROOT/'content/raw/celadon-gym-resources';b=safe(ROOT,'scripts/celadon-gym-resources.lock.json').read_bytes()
 if sha(b)!=LOCK:raise ValueError('Required resource lock differs')
 lock=json.loads(b)
 if (lock['commit'],lock['tree'],len(lock['files']))!=(COMMIT,TREE,44):raise ValueError('Resource identity differs')
 def git(*args):return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args])
 if reference and (git('rev-parse','HEAD').decode().strip()!=COMMIT or git('rev-parse',COMMIT+'^{tree}').decode().strip()!=TREE):raise ValueError('Reference commit/tree differs')
 verified=[]
 for e in lock['files']:
  p=e['path'];q=safe(raw,p)
  if reference:
   if git('ls-tree',COMMIT,'--',p).decode().strip()!='100644 blob '+e['gitBlobSha1']+'\t'+p:raise ValueError('Resource is not its pinned regular blob')
   b=git('show',COMMIT+':'+p)
  else:b=q.read_bytes()
  if len(b)!=e['byteLength'] or sha(b)!=e['sha256'] or hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()!=e['gitBlobSha1']:raise ValueError('Resource source bytes differ: '+p)
  verified.append((q,b))
 if reference:
  for q,b in verified:q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 return dict(sourceFiles=44,bytes=sum(len(b) for _,b in verified),lockSha256=LOCK,decoded=False,publiclyUploaded=False)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);a=p.parse_args();print(json.dumps(run(a.reference_repo),sort_keys=True))
