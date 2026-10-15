#!/usr/bin/env python3
"""Authenticated 54-map compact package; all 44 Bill payloads and provenance stay byte-identical."""
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
NEW = (
    'maps/regions/vermilion/route-5.json',
    'maps/regions/vermilion/route-5-south-entrance.json',
    'maps/regions/vermilion/underground-path-north-entrance.json',
    'maps/regions/vermilion/underground-path-north-south-tunnel.json',
    'maps/regions/vermilion/underground-path-south-entrance.json',
    'maps/regions/vermilion/route-6-north-entrance.json',
    'maps/regions/vermilion/route-6.json',
    'maps/regions/vermilion/vermilion-city.json',
    'maps/regions/vermilion/vermilion-city-pokemon-center-1f.json',
    'maps/regions/vermilion/vermilion-city-mart.json',
)


def export(generated, output, check=False):
    return compose(generated, output, NEW, previous=Path(generated)/'bill-region/runtime-maps', check=check)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--generated',type=Path,default=ROOT/'content/generated')
    p.add_argument('--output',type=Path)
    p.add_argument('--check',action='store_true')
    a=p.parse_args()
    print(json.dumps(export(a.generated,a.output or a.generated/'maps/catalogs/vermilion-maps.json',a.check),sort_keys=True))
