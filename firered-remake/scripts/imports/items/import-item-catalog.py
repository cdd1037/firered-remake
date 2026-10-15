#!/usr/bin/env python3
"""Compile the single item business catalog from retained pinned source data."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
from item_catalog import ROOT, compile_catalog


def run(root, check=False):
    document = compile_catalog(root)
    data = (json.dumps(document, ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    target = root / 'content/generated/data/items.json'
    if check:
        if target.read_bytes() != data:
            raise ValueError('Item catalog regeneration differs')
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return dict(checked=check, definitions=len(document['items']), bytes=len(data))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root.resolve(), args.check)))
