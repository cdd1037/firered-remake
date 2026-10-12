#!/usr/bin/env python3
"""Authenticate original Saffron presentation blobs without executing source scripts."""
import hashlib,json,pathlib,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-saffron-gym-presentation-v1'
LOCK_SHA='fa1f08584e533ca0de9d0b53f5929ffb42d0adf6e4ee005664ee8213ee3be9bb'
SOURCE_COUNT=31
SPECIES=('SLOWBRO',)
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def require(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 require(not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and not any(x in ('','.','..') for x in rel.split('/')),'Unsafe relative path')
 p=pathlib.Path(root).absolute()/rel
 require(not any(x.is_symlink() for x in (p,*p.parents)),'Symlink rejected')
 return p
def authenticated(root,reference=None):
 b=safe(root,'scripts/saffron-gym-presentation.lock.json').read_bytes();require(sha(b)==LOCK_SHA,'Saffron source lock differs');lock=json.loads(b)
 require((lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(1,'pret/pokefirered',COMMIT,TREE,PROFILE,SOURCE_COUNT),'Saffron source scope differs')
 if reference:require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=safe(root,'content/raw/saffron-gym-presentation/'+row['path'])
  b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  require((len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Source bytes differ: '+row['path']);raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw
