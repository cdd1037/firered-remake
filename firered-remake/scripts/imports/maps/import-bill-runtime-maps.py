#!/usr/bin/env python3
"""Authenticated 44-map compact derivative. Keeps all prior converters and source worlds frozen."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_map_manifest import compose, compose_region
import argparse
import importlib.util
import json
from pathlib import Path
ROOT = _PROJECT_ROOT
NEW = {
    'maps/regions/bill/route-24.json',
    'maps/regions/bill/route-25.json',
    'maps/regions/bill/route-25-sea-cottage.json',
}
def export(generated, output, check):
 source = ROOT/'scripts/imports/maps/export-runtime-maps.py'
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 route3_source = ROOT/'scripts/imports/maps/export-route3-runtime-maps.py'
 spec3=importlib.util.spec_from_file_location('_mtmoon_runtime_route3',route3_source)
 route3=importlib.util.module_from_spec(spec3);spec3.loader.exec_module(route3)
 mtmoon_source = ROOT/'scripts/imports/maps/export-mtmoon-runtime-maps.py'
 specm=importlib.util.spec_from_file_location('_cerulean_runtime_mtmoon',mtmoon_source)
 mtmoon=importlib.util.module_from_spec(specm);specm.loader.exec_module(mtmoon)
 cerulean_source = ROOT/'scripts/imports/maps/import-cerulean-runtime-maps.py'
 specc=importlib.util.spec_from_file_location('_bill_runtime_cerulean',cerulean_source)
 cerulean=importlib.util.module_from_spec(specc);specc.loader.exec_module(cerulean)

 return compose(generated, output, base.INPUTS | route3.NEW | mtmoon.NEW | cerulean.NEW | NEW, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'bill-region/runtime-maps',a.check),sort_keys=True))
