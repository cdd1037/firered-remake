#!/usr/bin/env python3
"""Authenticated 54-map compact package; all 44 Bill payloads and provenance stay byte-identical."""
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
NEW = (
    'vermilion-region/route-5',
    'vermilion-region/route-5-south-entrance',
    'vermilion-region/underground-path-north-entrance',
    'vermilion-region/underground-path-north-south-tunnel',
    'vermilion-region/underground-path-south-entrance',
    'vermilion-region/route-6-north-entrance',
    'vermilion-region/route-6',
    'vermilion-region/vermilion-city',
    'vermilion-region/vermilion-city-pokemon-center-1f',
    'vermilion-region/vermilion-city-mart',
)


def export(generated, output, check=False):
    return compose(generated, output, NEW, previous=Path(generated)/'bill-region/runtime-maps', expected_count=54, check=check)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--generated',type=Path,default=ROOT/'content/generated')
    p.add_argument('--output',type=Path)
    p.add_argument('--check',action='store_true')
    a=p.parse_args()
    print(json.dumps(export(a.generated,a.output or a.generated/'runtime-vermilion-maps',a.check),sort_keys=True))
