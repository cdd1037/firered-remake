#!/usr/bin/env python3
"""Authenticated 41-map compact derivative. Keeps all prior converters and source worlds frozen."""
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
    'maps/regions/cerulean/cerulean-city.json',
    'maps/regions/cerulean/cerulean-city-pokemon-center-1f.json',
    'maps/regions/cerulean/cerulean-city-pokemon-center-2f.json',
    'maps/regions/cerulean/cerulean-city-mart.json',
    'maps/regions/cerulean/cerulean-city-gym.json',
    'maps/regions/cerulean/cerulean-city-bike-shop.json',
    'maps/regions/cerulean/cerulean-city-house-1.json',
    'maps/regions/cerulean/cerulean-city-house-2.json',
    'maps/regions/cerulean/cerulean-city-house-3.json',
    'maps/regions/cerulean/cerulean-city-house-4.json',
    'maps/regions/cerulean/cerulean-city-house-5.json',
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

 return compose(generated, output, base.INPUTS | route3.NEW | mtmoon.NEW | NEW, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'cerulean-region/runtime-maps',a.check),sort_keys=True))
