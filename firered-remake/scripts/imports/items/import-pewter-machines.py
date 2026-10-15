#!/usr/bin/env python3
"""Read only authenticated local Git blobs/cache for Brock's TM39. Never run upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from item_icon_exports import item_icon_path, output_materials

import argparse
import base64
import importlib.util
import sys
import json
import os
import pathlib
import re
import subprocess

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
import sys
sys.path.insert(0, str(ROOT / 'scripts/lib'))
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-pewter-machines-v1'



def encoded(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':')) + '\n').encode()

def pairs(values):
    result = {}
    for k, v in values:
        if k in result:
            raise ValueError('Duplicate JSON member: ' + k)
        result[k] = v
    return result

def safe(path):
    path = pathlib.Path(path).absolute()
    for p in [path, *path.parents]:
        if p.is_symlink():
            raise ValueError('Symlink path rejected: ' + str(p))
    return path

def directory(path):
    path = safe(path)
    for p in [path, *path.parents]:
        if p.exists() and not p.is_dir():
            raise ValueError('Non-directory ancestor: ' + str(p))
    return path

def read_lock():
    data = safe(ROOT / 'scripts/recipes/items/pewter-machines.lock.json').read_bytes()
    lock = json.loads(data, object_pairs_hook=pairs)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE or len(lock['files']) != 23:
        raise ValueError('Incomplete or incorrect source identity')
    return lock


def build(lock, data):
    texts = {p: b.decode('utf-8') for p, b in data.items() if not p.endswith('.png')}
    def match(path, pattern):
        values = list(re.finditer(pattern, texts[path], re.M | re.S))
        if len(values) != 1:
            raise ValueError('Non-unique source pattern: ' + path + ':' + pattern)
        return values[0]
    def ref(path, symbol, m):
        return dict(path=path, symbol=symbol, startLine=texts[path][:m.start()].count('\n') + 1,
                    endLine=texts[path][:m.end()].count('\n') + 1)
    spec = importlib.util.spec_from_file_location('pewter_machine_codec', ROOT/'scripts/imports/presentation/import-lab-battle-ui.py')
    codec = importlib.util.module_from_spec(spec); spec.loader.exec_module(codec)
    icons, icon_outputs = [], {}
    for symbol in ('ITEM_TM39','ITEM_TM_CASE'):
        table = 'src/data/item_icon_table.h'
        m = match(table, r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},')
        paths = []
        for binding, ext in ((m[1],'.png'),(m[2],'.pal')):
            gm = match('src/data/graphics/items.h', r'^const u32 '+binding+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);')
            path = gm[1].replace('.4bpp.lz',ext).replace('.gbapal.lz',ext)
            if path not in data: raise ValueError('Icon binding outside locked scope')
            paths.append(path)
        image_path, palette_path = paths
        w,h,indices,_ = codec.indexed_png(data[image_path])
        if (w,h)!=(24,24): raise ValueError('Icon dimensions differ')
        rgba = codec.rgba(indices,codec.palette(data[palette_path])); png = codec.png(w,h,rgba)
        png_path = item_icon_path(image_path, palette_path); icon_outputs[png_path] = png
        def whole(path, sym): return dict(path=path,symbol=sym,startLine=1,
            endLine=max(1,texts[path].count('\n')) if path in texts else 1)
        icons.append(dict(item={'value': 'pret:' + symbol}, width=w, height=h, pngPath=png_path))
    return dict(schemaVersion=1, icons=icons), icon_outputs

def run(reference, raw, out):
    raw = directory(raw); out = safe(out); directory(out.parent)
    if out.suffix != ".json": raise ValueError("Machine output must be a JSON file")
    if raw == out or raw in out.parents or out in raw.parents:
        raise ValueError('Raw/output overlap')
    lock = read_lock()
    data = {}
    for entry in lock['files']:
        path = safe(raw / entry['path'])
        directory(path.parent)
        if path.exists() and not path.is_file():
            raise ValueError('Non-file raw path')
        b = subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+entry['path']]) if reference else path.read_bytes()
        data[entry['path']] = b
    parsed, icon_outputs = build(lock,data)
    document = encoded(parsed)
    outputs = {'pewter.json':document, **icon_outputs}
    targets = [(raw / p,b) for p,b in data.items()] if reference else []
    targets.extend((base / name, data) for (base, name), data in output_materials(out, outputs).items())
    for target,_ in targets:
        safe(target); directory(target.parent)
        safe(target.with_name(target.name+'.tmp'))
        for candidate in (target,target.with_name(target.name+'.tmp')):
            if candidate.exists() and not candidate.is_file():
                raise ValueError('Non-file output path')
    for target,b in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        temp=target.with_name(target.name+'.tmp');temp.write_bytes(b);os.replace(temp,target)
    return dict(inputCount=len(data), outputCount=len(outputs))

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path,default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/machine-learning/pewter.json')
    args=parser.parse_args()
    print(json.dumps(run(args.reference_repo,args.raw,args.out)))
