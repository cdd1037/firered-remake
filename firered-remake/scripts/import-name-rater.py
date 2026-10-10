#!/usr/bin/env python3
"""Copy only authenticated, fixed-revision Name Rater inputs; never execute source."""
import argparse, hashlib, json, pathlib
ROOT = pathlib.Path(__file__).resolve().parents[1]
PIN = 'bba2d7d3d50d79593a60ab969478e5d9547450a340b01727d2b35af7bd8e369e'
def digest(data): return hashlib.sha256(data).hexdigest()
def read(path, size):
    with path.open('rb') as f:
        import os
        if os.fstat(f.fileno()).st_size != size: raise ValueError('source size: ' + str(path))
        data = f.read(size + 1)
        if len(data) != size or os.fstat(f.fileno()).st_size != size: raise ValueError('source changed')
        return data
def main():
    p=argparse.ArgumentParser();p.add_argument('--source',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    raw=read(ROOT/'scripts/name-rater.lock.json',3963)
    if digest(raw)!=PIN:raise ValueError('lock differs')
    lock=json.loads(raw); output=ROOT/'content/raw/name-rater'; source=a.source or output
    rows=[]
    for f in lock['files']:
        rel=pathlib.PurePosixPath(f['path'])
        if rel.is_absolute() or '..' in rel.parts:raise ValueError('unsafe path')
        src=source/rel
        if src.is_symlink():raise ValueError('source symlink')
        data=read(src,f['bytes'])
        if digest(data)!=f['sha256'] or hashlib.sha1(f'blob {len(data)}\0'.encode()+data).hexdigest()!=f['gitBlobSha1']:raise ValueError('source hash: '+f['path'])
        rows.append((output/rel,data))
    for path,data in rows:
        if a.check:
            if read(path,len(data))!=data:raise ValueError('output differs')
        else:
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    print(json.dumps({'passed':True,'files':len(rows),'bytes':sum(len(b) for _,b in rows),'lockSha256':PIN,'check':a.check}))
if __name__=='__main__':main()
