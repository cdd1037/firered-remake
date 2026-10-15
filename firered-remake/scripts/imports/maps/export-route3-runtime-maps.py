#!/usr/bin/env python3
"""Build the Route 3 compact map composition."""
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
 'maps/regions/route3/route-3.json',
 'maps/regions/route3/route-4.json',
 'maps/regions/route3/route-4-pokemon-center-1f.json',
}
def export(generated, output, check):
 source = ROOT/'scripts/imports/maps/export-runtime-maps.py'
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)

 return compose(generated, output, base.INPUTS | NEW, check=check)
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'maps/catalogs/maps-route3.json',a.check),sort_keys=True))
