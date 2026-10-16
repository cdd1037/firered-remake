#!/usr/bin/env python3
"""Decode only Secret Key and TM14/TM22/TM38 icons from pinned Git blobs; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from pathlib import Path
import argparse, base64, importlib.util, io, json, re, sys
from PIL import Image
sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location('item_source', (_PROJECT_ROOT / 'scripts/imports/items/import-cinnabar-items.py'))
META = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(META)
BASE = META.BASE
PROFILE = 'firered-cinnabar-item-art-source-v1'
ITEMS = ('SECRET_KEY', 'TM14', 'TM22', 'TM38')

from image_io import png as encode_rgba_png

png = encode_rgba_png

def generate(reference):
    source = BASE.Source(reference)
    icons, assets = [], {}
    def match(path, pattern):
        text = source.text(path)
        rows = list(re.finditer(pattern, text, re.M))
        META.need(len(rows) == 1, 'Nonunique Cinnabar item icon binding: ' + pattern)
        return rows[0]
    def ref(path, symbol, row=None):
        source.read(path)
        return dict(path=path, symbol=symbol, startLine=1 if row is None else source.text(path).count('\n', 0, row.start()) + 1,
            endLine=1 if row is None else source.text(path).count('\n', 0, row.end()) + 1)
    for item in ITEMS:
        table = 'src/data/item_icon_table.h'
        binding = match(table, r'^    \[ITEM_' + item + r'\]\s*=\s*\{(\w+),\s*(\w+)\},')
        symbols = binding.groups()
        paths = []
        for symbol in symbols:
            row = match('src/data/graphics/items.h', r'^const u32 ' + symbol + r'\[\]\s*=\s*INCBIN_U32\("([^"]+)"\);')
            paths.append(row[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
        image_path, palette_path = paths
        image = Image.open(io.BytesIO(source.read(image_path)))
        META.need(image.mode == 'P' and image.size == (24, 24), 'Cinnabar source icon dimensions differ')
        indices = image.tobytes()
        lines = source.text(palette_path).splitlines()
        META.need(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19 and max(indices) < 16, 'Cinnabar source icon palette differs')
        palette = [tuple((int(c) >> 3 << 3) | (int(c) >> 3 >> 2) for c in line.split()) for line in lines[3:]]
        META.need(all(len(rgb) == 3 and all(0 <= c <= 255 for c in rgb) for rgb in palette), 'Invalid source palette channels')
        rgba = bytes(c for index in indices for c in (*palette[index], 0 if index == 0 else 255))
        name = 'item_' + item.lower() + '.png'
        assets[name] = png(24, 24, rgba)
        icons.append(dict(item=META.ident('ITEM_' + item), width=24, height=24, pngPath=name))
    lock = source.lock()
    lock.update(profile=PROFILE, gameEdition='FIRERED', romRevision=0)
    outputs = {'content/generated/data/services/cinnabar-item-art/' + name: data for name, data in assets.items()}
    outputs['content/generated/data/services/cinnabar-item-art/icons.json'] = BASE.canonical(dict(schemaVersion=1,
          icons=icons,
        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0))
    outputs['scripts/recipes/items/cinnabar-item-art.lock.json'] = BASE.canonical(lock)
    manifest = dict(schemaVersion=1,

        outputs=[dict(path=p) for p, b in sorted(outputs.items()) if p.startswith('content/generated/')])
    outputs['content/generated/data/services/cinnabar-item-art/manifest.json'] = BASE.canonical(manifest)
    outputs.update({'content/raw/pokefirered/037335f4/' + p: b for p, b in source.data.items()})
    return outputs, icons

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=_PROJECT_ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs, icons = generate(args.reference_root)
    for path, data in outputs.items():
        target = BASE.safe(args.project_root, path)
        if args.check:
            META.need(target.read_bytes() == data, 'Cinnabar item art regeneration differs: ' + path)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(passed=True, icons=len(icons), sourceFiles=len([p for p in outputs if p.startswith('content/raw/')]))))

if __name__ == '__main__':
    main()
