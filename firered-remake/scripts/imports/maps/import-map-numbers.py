#!/usr/bin/env python3
"""Convert original map numbers used by roaming history and existing save data."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,json
from pathlib import Path
ROOT=_PROJECT_ROOT
def run(root,check=False):
 source=json.loads((root/'content/raw/pokefirered/037335f4/data/maps/map_groups.json').read_bytes())
 values={name:[group,index] for group,key in enumerate(source['group_order']) for index,name in enumerate(source[key])}
 data=(json.dumps(values,sort_keys=True,separators=(',',':'))+'\n').encode()
 output=root/'content/generated/maps/numbers.json'
 if check:
  if output.read_bytes()!=data:raise ValueError('Regenerate map numbers')
 else:output.write_bytes(data)
 print(f'{len(values)} map numbers, {len(data)} bytes')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--root',type=Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();run(a.root,a.check)
