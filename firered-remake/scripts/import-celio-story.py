#!/usr/bin/env python3
"""Copy the exact pinned source map-number table; no upstream execution."""
from pathlib import Path
import argparse,hashlib,json,subprocess
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PATH='data/maps/map_groups.json'
BLOB='8c382f504d208286161903244772c93138cd7662'
SHA='ce296c9b54c8bb85cc35870b30a533ab017e72107fd40cf471224a42127af79a'
p=argparse.ArgumentParser();p.add_argument('reference');p.add_argument('output');p.add_argument('--check',action='store_true');a=p.parse_args()
r=Path(a.reference);out=Path(a.output)
assert subprocess.check_output(['git','-C',str(r),'rev-parse',COMMIT+'^{tree}'],text=True).strip()==TREE
b=subprocess.check_output(['git','-C',str(r),'show',COMMIT+':'+PATH])
assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==BLOB
assert hashlib.sha256(b).hexdigest()==SHA and len(b)==16262
d=json.loads(b);groups=d['group_order'];assert len(groups)==43 and len(set(groups))==43
names=[n for g in groups for n in d[g]];assert len(names)==425 and len(set(names))==425
assert all(len(d[g])<256 for g in groups)
f=out/'content/raw/celio-story'/PATH
if a.check:assert f.read_bytes()==b
else:f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
print(json.dumps({'checked':a.check,'sourceCommit':COMMIT,'sourceTree':TREE,'path':str(f.relative_to(out)),'bytes':len(b),'sha256':SHA,'gitBlob':BLOB,'groups':43,'maps':425},indent=2))
