#!/usr/bin/env python3
"""Verify the five added pickup/shop icons against source indices and RGB555 palettes."""
import argparse, io, json, pathlib, re
from PIL import Image

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--project-root', type=pathlib.Path, default=pathlib.Path(__file__).resolve().parents[1])
a = p.parse_args()
root = a.project_root
raw = root / 'content/raw/pokefirered/037335f4'
generated = root / 'content/generated/first-sevii-presentation'
manifest = json.loads((generated / 'manifest.json').read_bytes())
document = json.loads((generated / 'presentation.json').read_bytes())
lock_bytes = (root / 'scripts/first-sevii-presentation.lock.json').read_bytes()
lock = json.loads(lock_bytes)
assert len(lock['files']) == 58 and len(document['assets']) == 18 and len(document['items']) == 8
sources = {row['path']: row for row in lock['files']}
outputs = {row['path']: row for row in manifest['outputs']}
source_items = {row['itemId']: row for row in json.loads((raw / 'src/data/items.json').read_bytes())['items']}
icon_table = (raw / 'src/data/item_icon_table.h').read_text()
graphics_table = (raw / 'src/data/graphics/items.h').read_text()
checks = []
for symbol in ('ITEM_MAX_ELIXIR', 'ITEM_RAZZ_BERRY', 'ITEM_PINAP_BERRY', 'ITEM_FRESH_WATER', 'ITEM_SODA_POP'):
    item = next(row for row in document['items'] if row['symbol'] == symbol)
    source_item = source_items[symbol]
    assert item['id'] == 'pret:' + symbol and item['name'] == source_item['english']
    assert item['description'] == source_item['description_english'].replace(chr(92) + 'n', '\n')
    assert item['pocket'] == source_item['pocket'] and item['fieldUseFunction'] == source_item['fieldUseFunc']
    binding = re.search(r'\[' + symbol + r'\]\s*=\s*\{(\w+),\s*(\w+)\}', icon_table)
    paths = []
    for resource in binding.groups():
        path = re.search(r'const u32 ' + resource + r'\[\] = INCBIN_U32\("([^"]+)"\)', graphics_table)[1]
        paths.append(path.replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    assert all(path in sources for path in paths)
    original = Image.open(io.BytesIO((raw / paths[0]).read_bytes()))
    assert original.mode == 'P' and original.size == (24, 24)
    palette_lines = (raw / paths[1]).read_text().splitlines()
    assert palette_lines[:3] == ['JASC-PAL', '0100', '16']
    colors = [[int(value) for value in line.split()] for line in palette_lines[3:]]
    assert len(colors) == 16
    asset = next(row for row in document['assets'] if row['id'] == item['assetId'])
    assert (asset['width'], asset['height']) == (24, 24) and asset['sourcePaths'] == paths
    rgba = (generated / asset['rgbaPath']).read_bytes()
    png = (generated / asset['pngPath']).read_bytes()
    assert len(rgba) == 2304 and Image.open(io.BytesIO(png)).convert('RGBA').tobytes() == rgba
    for pixel, index in enumerate(original.tobytes()):
        assert 0 <= index < 16
        expected = bytes([((value // 8) * 8) + (value // 32) for value in colors[index]] + [255 if index else 0])
        assert rgba[pixel * 4:pixel * 4 + 4] == expected, (symbol, pixel)
    checks.append(dict(symbol=symbol, name=item['name'], description=item['description'], pocket=item['pocket'], width=24, height=24,
        pixelsCompared=576, sourcePaths=paths))
print(json.dumps(dict(passed=True, sourceCommit=lock['commit'], sourceCount=58, assets=18, itemIcons=8,
    icons=checks,
    guiRun=False, fullPresentationSuiteRerun=False), indent=2))
