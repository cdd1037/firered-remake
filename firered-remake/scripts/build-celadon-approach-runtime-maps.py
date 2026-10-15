#!/usr/bin/env python3
"""Build the 113-map Celadon approach package, preserving all prior 98 maps byte for byte."""
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
ROOT = Path(__file__).resolve().parent.parent
MAXIMUM_MAP_BYTES = 2 * 1024 * 1024

def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe(root, relative):
    path = Path(relative)
    require(bool(relative) and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in relative.split('/'))
            and path.as_posix() == relative, 'Unsafe runtime path')
    target = Path(root) / path
    require(not any(p.is_symlink() for p in [target, *target.parents]), 'Symlink source/output rejected')
    return target


def bounded(root, relative, maximum):
    with safe(root, relative).open('rb') as stream:
        raw = stream.read(maximum + 1)
    require(0 < len(raw) <= maximum, 'Runtime input size out of bounds: ' + relative)
    return raw


def read_entry(root, entry, maximum):
    raw = bounded(root, entry['path'], maximum)
    return raw


def export_runtime(generated, output, check=False):
    return compose_region(generated, output, 'celadon-approach-region', 'celadon-approach-catalog.json', 'rocktunnel-runtime-maps', 113, check)


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated',type=Path,default=ROOT/'content/generated')
    parser.add_argument('--output',type=Path,default=ROOT/'content/generated/celadon-approach-runtime-maps')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    print(json.dumps(export_runtime(args.generated,args.output,args.check),sort_keys=True))
