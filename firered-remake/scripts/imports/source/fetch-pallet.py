#!/usr/bin/env python3
"""Fetch a bounded, pinned, local-only five-map Pallet Town source subset. Never execute upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, concurrent.futures, json, pathlib, urllib.request
ROOT = _PROJECT_ROOT
LOCK = ROOT / 'scripts/recipes/source/fetch-pallet.lock.json'
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache',type=pathlib.Path,default=pathlib.Path('/tmp/pokefirered-source-audit'))
    args=parser.parse_args(); lock=json.loads(LOCK.read_text(encoding='utf-8')); dest=ROOT/'content/raw/pokefirered/037335f4'
    if lock['commit'] != json.loads((ROOT/'scripts/recipes/config/source.json').read_text(encoding='utf-8'))['source']['sourceCommit']:
        raise ValueError('Project source lock differs from Pallet subset lock')
    def fetch(e):
        p=pathlib.PurePosixPath(e['path'])
        if p.is_absolute() or '..' in p.parts: raise ValueError('Unsafe source path')
        target=dest/p; cache=args.cache/p
        if target.is_file(): b=target.read_bytes()
        elif cache.is_file(): b=cache.read_bytes()
        else:
            url=f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{p}'
            with urllib.request.urlopen(url,timeout=60) as r: b=r.read()
        target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(b)
        return {'path':e['path'],'sourceUrl':f'https://github.com/{lock["repository"]}/blob/{lock["commit"]}/{p}'}
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool: files=list(pool.map(fetch,sorted(lock['files'],key=lambda f:f['path'])))
    print(f'Materialized {len(files)} pinned files')
if __name__=='__main__': main()
