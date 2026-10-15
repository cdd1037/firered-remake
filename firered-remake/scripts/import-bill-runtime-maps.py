#!/usr/bin/env python3
"""Authenticated 44-map compact derivative. Keeps all prior converters and source worlds frozen."""
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
    'bill-region/route-24',
    'bill-region/route-25',
    'bill-region/route-25-sea-cottage',
}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 route3_source = ROOT/'scripts/export-route3-runtime-maps.py'
 spec3=importlib.util.spec_from_file_location('_mtmoon_runtime_route3',route3_source)
 route3=importlib.util.module_from_spec(spec3);spec3.loader.exec_module(route3)
 mtmoon_source = ROOT/'scripts/export-mtmoon-runtime-maps.py'
 specm=importlib.util.spec_from_file_location('_cerulean_runtime_mtmoon',mtmoon_source)
 mtmoon=importlib.util.module_from_spec(specm);specm.loader.exec_module(mtmoon)
 cerulean_source = ROOT/'scripts/import-cerulean-runtime-maps.py'
 specc=importlib.util.spec_from_file_location('_bill_runtime_cerulean',cerulean_source)
 cerulean=importlib.util.module_from_spec(specc);specc.loader.exec_module(cerulean)

 return compose(generated, output, base.INPUTS | route3.NEW | mtmoon.NEW | cerulean.NEW | NEW, expected_count=44, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'bill-region/runtime-maps',a.check),sort_keys=True))
