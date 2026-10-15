#!/usr/bin/env python3
"""Build the Route 3 compact map composition."""
import sys as _map_sys
from pathlib import Path as _MapPath
_map_scripts = str(_MapPath(__file__).resolve().parent)
if _map_scripts not in _map_sys.path:
    _map_sys.path.insert(0, _map_scripts)
from runtime_map_manifest import compose, compose_region
import argparse
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
NEW = {
 'route3-region/route-3',
 'route3-region/route-4',
 'route3-region/route-4-pokemon-center-1f',
}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)

 return compose(generated, output, base.INPUTS | NEW, expected_count=27, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'runtime-maps-route3',a.check),sort_keys=True))
