#!/usr/bin/env python3
"""Build the pinned 81-map Vermilion Gym profile without rewriting older map payloads."""
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


def export(generated, output, check=False):
    return compose_region(generated, output, 'vermilion-gym-region', 'vermilion-gym-catalog.json', 'runtime-ssanne-maps', 81, check)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(args.generated, args.output or args.generated / 'runtime-vermilion-gym-maps', args.check), sort_keys=True))
