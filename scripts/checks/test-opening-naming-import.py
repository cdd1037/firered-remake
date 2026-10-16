#!/usr/bin/env python3
"""Independent naming tile/palette oracle and malformed input rejection."""
import importlib.util
import io
from pathlib import Path
import struct
from unittest.mock import patch
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('opening', ROOT / 'scripts/imports/presentation/import-opening-presentation.py')
module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
raw = ROOT / 'content/raw/pokefirered/037335f4'
source = raw / 'graphics/naming_screen'
files = module.generate(raw)
for name, data in files.items():
    assert data == (ROOT / 'content/generated/ui/opening' / name).read_bytes(), name
# Reconstruct each displayed pixel independently from the tile word and the six palettes.
colors = []
for name in ['menu', 'page_swap_upper', 'page_swap_lower', 'page_swap_others', 'buttons', 'cursor']:
    for line in (source / (name + '.pal')).read_text().splitlines()[3:]:
        colors.append(tuple(((int(v) >> 3) << 3) | (int(v) >> 5) for v in line.split()))
tiles = Image.open(source / 'menu.png')
for name in ['background', 'keyboard_upper', 'keyboard_lower', 'keyboard_symbols']:
    output = Image.open(io.BytesIO(files['naming-' + name.replace('_', '-') + '.png']))
    words = list(struct.unpack('<640H', (source / (name + '.bin')).read_bytes()))
    for y in range(160):
        for x in range(240):
            word = words[(y // 8) * 32 + x // 8]
            if word == 0 or name == 'background' and y < 16:
                assert output.getpixel((x, y)) == (0, 0, 0, 0)
                continue
            tile, bank = word & 1023, word >> 12
            px, py = x % 8, y % 8
            if word & 1024: px = 7 - px
            if word & 2048: py = 7 - py
            index = tiles.getpixel(((tile % 16) * 8 + px, (tile // 16) * 8 + py))
            expected = (*colors[bank * 16 + index], 255 if index else 0)
            assert output.getpixel((x, y)) == expected, (name, x, y)
# Mutate a real keyboard tile input. No partial generated result may escape.
read_bytes = Path.read_bytes
def altered(path):
    data = read_bytes(path)
    if path == source / 'keyboard_upper.bin':
        data = bytearray(data); struct.pack_into('<H', data, 9 * 64 + 4, 0x13FF); return bytes(data)
    return data
with patch.object(Path, 'read_bytes', altered):
    try: module.generate(raw)
    except ValueError as error: assert 'Invalid tile reference' in str(error)
    else: raise AssertionError('Invalid naming tile was accepted')
assert Image.open(io.BytesIO(files['naming-cursor.png'])).size == (16, 16)
assert Image.open(io.BytesIO(files['naming-page-swap-lower.png'])).size == (24, 8)
print('PASS: naming layers match independent tile/palette oracle; all opening outputs match; malformed tile rejected; source sprite tile extents preserved.')
