#!/usr/bin/env python3
"""Copy the exact pinned source map-number table; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from pathlib import Path
import argparse,json,subprocess
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PATH='data/maps/map_groups.json'
BLOB='8c382f504d208286161903244772c93138cd7662'
p=argparse.ArgumentParser();p.add_argument('reference');p.add_argument('output');p.add_argument('--check',action='store_true');a=p.parse_args()
r=Path(a.reference);out=Path(a.output)
b=subprocess.check_output(['git','-C',str(r),'show',COMMIT+':'+PATH])
d=json.loads(b);groups=d['group_order'];assert len(groups)==43 and len(set(groups))==43
names=[n for g in groups for n in d[g]];assert len(names)==425 and len(set(names))==425
assert all(len(d[g])<256 for g in groups)
f=out/'content/raw/pokefirered/037335f4'/PATH
if a.check:assert f.read_bytes()==b
else:f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
print(json.dumps({'checked':a.check,'sourceCommit':COMMIT,'sourceTree':TREE,'path':str(f.relative_to(out)),'bytes':len(b),'gitBlob':BLOB,'groups':43,'maps':425},indent=2))
