#!/usr/bin/env python3
"""Independent source-indexed palette vectors for the generated slot presentation atlas."""
import sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
import importlib.util
import io
import json
import re
import struct
from PIL import Image
import ui_tiles

spec = importlib.util.spec_from_file_location('slot_import', ROOT / 'scripts/imports/items/import-slot-machine-presentation.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
files = module.generate(None, ROOT)
base = module.BASE
manifest = json.loads(files[base + 'manifest.json'])
layout = json.loads(files[base + 'layout.json'])['layout']
atlas = Image.open(io.BytesIO(files[base + 'presentation.png'])).convert('RGBA')
assets = {a['id']: a for a in manifest['assets']}
raw = ROOT / module.RAW
entries = struct.unpack('<640H', (raw / 'graphics/slot_machine/firered/bg.bin').read_bytes())
tiles = ui_tiles.tile_list((raw / 'graphics/slot_machine/firered/bg.png').read_bytes())
colors = ui_tiles.palette((raw / 'graphics/slot_machine/firered/match_lines.pal').read_bytes())
checks = 0
for line in layout['matchLines']:
    for coefficient in [0, 1, 3, 4, 5, 6, 7, 8]:
        name = 'win-%d-%d' % (line['id'], coefficient)
        region = assets[name]['region']
        for sx, sy, x, y, width, height in layout['preparedPatches']['line-%d-5' % line['id']]:
            entry = entries[(y // 8) * 32 + x // 8]
            source = tiles[entry & 1023]
            for dy in range(height):
                for dx in range(width):
                    index = source[(7-dy if entry & 2048 else dy)*8 + (7-dx if entry & 1024 else dx)]
                    expected = colors[index]
                    if index not in (2, 4):
                        rgb5 = [channel >> 3 for channel in expected]
                        rgb5 = [v + ((-v * coefficient) >> 4) for v in rgb5]
                        expected = [(v << 3) | (v >> 2) for v in rgb5]
                    rgba = (*expected, 0 if index == 0 else 255)
                    assert atlas.getpixel((region['x']+sx+dx, region['y']+sy+dy)) == rgba, (name, x+dx, y+dy, index)
                    checks += 1
assert layout['winningFlash'] == {'lightFrames':[0,1,2,1], 'lightHoldTicks':9,
    'darkenCoefficients':[1,3,4,5,6,7,7,8,7,7,6,5,4,3,1,0]}
# Independent source glyph samples for both complete message windows, including the second line.
font_spec = importlib.util.spec_from_file_location('slot_source_font', ROOT / 'scripts/imports/presentation/import-source-font.py')
font = importlib.util.module_from_spec(font_spec); font_spec.loader.exec_module(font)
charmap = font.character_map((raw / 'charmap.txt').read_text())
advances = font.widths((raw / 'src/text.c').read_text(), 'Normal')
normal = Image.open(raw / 'graphics/fonts/latin_normal.png')
message_colors = ui_tiles.palette((raw / 'graphics/text_window/stdpal_3.pal').read_bytes())
message_checks = 0
for identity, text in [('message-quit', 'Quit playing?'), ('message-no-coins', "You've run out of COINS.\nGame over!")]:
    asset = assets[identity]; region = asset['region']; x, y = 9, 10
    assert (asset['width'], asset['height']) == (176, 48)
    for char in text:
        if char == '\n': x, y = 9, y + 16; continue
        gid = charmap[char]; width = advances[gid]
        for dy in range(14):
            for dx in range(width):
                role = normal.getpixel((gid % 16 * 16 + dx, gid // 16 * 16 + dy))
                expected = (*message_colors[{1:2,2:3}.get(role,1)], 255)
                assert atlas.getpixel((region['x']+x+dx,region['y']+y+dy)) == expected
                message_checks += 1
        x += width
for path, data in files.items():
    assert (ROOT / path).read_bytes() == data, 'Regeneration differs: ' + path
print(json.dumps({'passed':True, 'sourceIndexedPixelChecks': checks, 'sourceMessageGlyphChecks':message_checks, 'regeneration':True}))
