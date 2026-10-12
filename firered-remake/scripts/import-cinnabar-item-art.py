#!/usr/bin/env python3
"""Decode only Secret Key and TM14/TM22/TM38 icons from pinned Git blobs; no upstream execution."""
from pathlib import Path
import argparse, base64, binascii, importlib.util, io, json, re, struct, sys, zlib
from PIL import Image
sys.dont_write_bytecode = True
SPEC = importlib.util.spec_from_file_location('item_source', Path(__file__).with_name('import-cinnabar-items.py'))
META = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(META)
BASE = META.BASE
PROFILE = 'firered-cinnabar-item-art-source-v1'
ITEMS = ('SECRET_KEY', 'TM14', 'TM22', 'TM38')
LOCK_SHA256 = '721a165d868df5d9094ea64aad31f6c6c7211f0ffe055886b33d02996827e81e'
MANIFEST_SHA256 = 'dd04171631d4758c504da254b11516b55b4c4e0e1befa7c57094d3f5c364b9fe'

def png(width, height, rgba):
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', binascii.crc32(tag + data) & 0xffffffff)
    scan = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    # Stored DEFLATE blocks make the reviewed PNG bytes independent of encoder versions.
    length = len(scan)
    data = b'\x78\x01\x01' + struct.pack('<HH', length, length ^ 65535) + scan + struct.pack('>I', zlib.adler32(scan) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', data) + chunk(b'IEND', b'')

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
            endLine=1 if row is None else source.text(path).count('\n', 0, row.end()) + 1, sha256=source.entries[path]['sha256'])
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
        icons.append(dict(item=META.ident('ITEM_' + item), width=24, height=24, rgbaBase64=base64.b64encode(rgba).decode(),
            rgbaSha256=BASE.sha(rgba), pngPath=name, pngSha256=BASE.sha(assets[name]),
            source=ref(image_path, symbols[0]), paletteSource=ref(palette_path, symbols[1]), bindingSource=ref(table, 'ITEM_' + item, binding)))
    lock = source.lock()
    lock.update(profile=PROFILE, gameEdition='FIRERED', romRevision=0)
    outputs = {'content/generated/cinnabar-item-art/' + name: data for name, data in assets.items()}
    outputs['content/generated/cinnabar-item-art/icons.json'] = BASE.canonical(dict(schemaVersion=1,
        profile='firered-cinnabar-item-art-v1', sourceCommit=BASE.COMMIT, icons=icons,
        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0))
    outputs['scripts/cinnabar-item-art.lock.json'] = BASE.canonical(lock)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=BASE.COMMIT, sourceTree=BASE.TREE,
        fullSourceHash=BASE.sha(''.join(p + '\0' + e['sha256'] + '\n' for p, e in sorted(source.entries.items())).encode()),
        lockSha256=BASE.sha(outputs['scripts/cinnabar-item-art.lock.json']),
        outputs=[dict(path=p, sha256=BASE.sha(b), byteLength=len(b)) for p, b in sorted(outputs.items()) if p.startswith('content/generated/')])
    outputs['content/generated/cinnabar-item-art/manifest.json'] = BASE.canonical(manifest)
    outputs.update({'content/raw/cinnabar-item-art/' + p: b for p, b in source.data.items()})
    return outputs, icons

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).absolute().parents[1])
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs, icons = generate(args.reference_root)
    for path, expected in [('scripts/cinnabar-item-art.lock.json', LOCK_SHA256), ('content/generated/cinnabar-item-art/manifest.json', MANIFEST_SHA256)]:
        META.need(BASE.sha(outputs[path]) == expected, 'Cinnabar item art identity differs: ' + path)
    for path, data in outputs.items():
        target = BASE.safe(args.project_root, path)
        if args.check:
            META.need(target.read_bytes() == data, 'Cinnabar item art regeneration differs: ' + path)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(passed=True, icons=len(icons), sourceFiles=len([p for p in outputs if p.startswith('content/raw/')]),
        lockSha256=BASE.sha(outputs['scripts/cinnabar-item-art.lock.json']), lockByteLength=len(outputs['scripts/cinnabar-item-art.lock.json']),
        manifestSha256=BASE.sha(outputs['content/generated/cinnabar-item-art/manifest.json']),
        manifestByteLength=len(outputs['content/generated/cinnabar-item-art/manifest.json']),
        rgbaHashes={i['item']['value']: i['rgbaSha256'] for i in icons})))

if __name__ == '__main__':
    main()
