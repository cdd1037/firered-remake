#!/usr/bin/env python3
"""Compile fixed-revision Name Rater prompts; retain source evidence tool-side."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, pathlib, re
ROOT = _PROJECT_ROOT
def read(path, size=None):
    with path.open('rb') as f:
        import os
        if size is None: size = os.fstat(f.fileno()).st_size
        if os.fstat(f.fileno()).st_size != size: raise ValueError('source size: ' + str(path))
        data = f.read(size + 1)
        if len(data) != size or os.fstat(f.fileno()).st_size != size: raise ValueError('source changed')
        return data
def main():
    p=argparse.ArgumentParser();p.add_argument('--source',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    raw=read(ROOT/'scripts/recipes/story/name-rater.lock.json')
    lock=json.loads(raw); output=ROOT/'content/raw/pokefirered/037335f4'; source=a.source or output
    rows=[]
    for f in lock['files']:
        rel=pathlib.PurePosixPath(f['path'])
        if rel.is_absolute() or '..' in rel.parts:raise ValueError('unsafe path')
        src=source/rel
        if src.is_symlink():raise ValueError('source symlink')
        data=read(src,f.get('bytes'))
        rows.append((output/rel,data))
    files = {f['path']: data for f, (_, data) in zip(lock['files'], rows, strict=True)}
    texts = {}
    for key, symbol in [('choosePokemonText', 'gText_ChoosePokemon'), ('choosePokemonCancelText', 'gText_ChoosePokemonCancel')]:
        matches = re.findall(r'const u8 ' + symbol + r'\[\] = _\("([^"]*)"\);', files['src/strings.c'].decode())
        if len(matches) != 1 or chr(92) in matches[0]: raise ValueError('Name Rater prompt: ' + symbol)
        texts[key] = matches[0]
    rows.append((ROOT/'content/generated/data/services/name-rater.json', (json.dumps(texts, sort_keys=True, indent=2) + '\n').encode()))
    for path,data in rows:
        if a.check:
            if read(path,len(data))!=data:raise ValueError('output differs')
        else:
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    print(json.dumps({'passed':True,'files':len(rows),'bytes':sum(len(b) for _,b in rows),'check':a.check}))
if __name__=='__main__':main()
