#!/usr/bin/env python3
"""Materialize or verify fixed Parcel event inputs as data; never execute upstream code."""
from pathlib import Path
import argparse, subprocess, json, hashlib
ROOT=Path(__file__).resolve().parents[1]
LOCK_SHA='7fb853f5dc081c71fe80acd16396ec7e9b661768583c26344ff0940c414014a9'
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference-repo',type=Path);args=parser.parse_args()
    lock=(ROOT/'scripts/parcel-events.lock.json').read_bytes()
    d=json.loads(lock);out=ROOT/'content/raw/pokefirered/037335f4';verified=[]
    for e in d['files']:
        rel=Path(e['path'])
        if rel.is_absolute() or '..' in rel.parts:raise ValueError('Unsafe source path')
        dest=out/rel
        if any(p.is_symlink() for p in [dest,*dest.parents]):raise ValueError('Symlink in source destination')
        b=subprocess.check_output(['git','-C',str(args.reference_repo),'show',d['commit']+':'+e['path']]) if args.reference_repo else dest.read_bytes()
        verified.append((dest,b))
    if args.reference_repo:
        for dest,b in verified:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
    print(json.dumps({'verified':len(verified),'sourceCommit':d['commit'],'upstreamExecuted':False}))
if __name__=='__main__':main()
