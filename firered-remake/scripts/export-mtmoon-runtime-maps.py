#!/usr/bin/env python3
"""Build the Mt. Moon compact map composition."""
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
 'mtmoon-region/mt-moon-1f',
 'mtmoon-region/mt-moon-b1f',
 'mtmoon-region/mt-moon-b2f',
}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 route3_source = ROOT/'scripts/export-route3-runtime-maps.py'
 spec3=importlib.util.spec_from_file_location('_mtmoon_runtime_route3',route3_source)
 route3=importlib.util.module_from_spec(spec3);spec3.loader.exec_module(route3)

 return compose(generated, output, base.INPUTS | route3.NEW | NEW, expected_count=30, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'mtmoon-region/runtime-maps',a.check),sort_keys=True))
