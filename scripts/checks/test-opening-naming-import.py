#!/usr/bin/env python3
"""Independent naming tile/palette oracle and malformed input rejection."""
import importlib.util
import io
import json
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
# The PC source uses three consecutive 16x8 subsprites and two complete frames.
# Compare every atlas pixel against the raw image index and actual menu palette.
pc = Image.open(io.BytesIO(files['naming-pc-icon.png']))
assert pc.size == (16, 48)
for frame, name in enumerate(['pc_icon_off', 'pc_icon_on']):
    original = Image.open(source / (name + '.png'))
    assert original.mode == 'P' and original.size == (16, 24)
    for y in range(24):
        for x in range(16):
            index = original.getpixel((x, y))
            assert pc.getpixel((x, y + frame * 24)) == (*colors[index], 255 if index else 0)
metadata = json.loads(files['opening.json'])
templates = metadata['namingTemplates']
assert metadata['rivalNameSymbols'] == ['gNameChoice_Green', 'gNameChoice_Gary', 'gNameChoice_Kaz', 'gNameChoice_Toru']
assert 'rivalNames' not in metadata
assert [metadata['texts'][symbol] for symbol in metadata['rivalNameSymbols']] == ['GREEN', 'GARY', 'KAZ', 'TORU']
title_symbols = {'PLAYER': 'gText_YourName', 'BOX': 'gText_BoxName', 'CAUGHT_MON': 'gText_PkmnsNickname',
                 'NICKNAME': 'gText_PkmnsNickname', 'RIVAL': 'gText_RivalsName'}
assert set(templates) == {'NAMING_SCREEN_' + name for name in ['PLAYER', 'BOX', 'CAUGHT_MON', 'NICKNAME', 'RIVAL']}
for name, limit, icon, y, title in [('PLAYER', 7, 1, 37, 'YOUR NAME?'), ('BOX', 8, 2, 41, 'BOX NAME?'),
                                  ('CAUGHT_MON', 10, 3, 40, "'s nickname?"), ('NICKNAME', 10, 3, 40, "'s nickname?"),
                                  ('RIVAL', 7, 4, 37, "RIVAL's NAME?")]:
    assert templates['NAMING_SCREEN_' + name] == dict(copyExistingString=False, maxChars=limit, iconFunction=icon,
        addGenderIcon=icon == 3, initialPage=0, title=title, titleSymbol=title_symbols[name], prefixSpeciesName=icon == 3, iconX=56, iconY=y)
assert metadata['namingLayout'] == dict(displayWidth=240, titleX=73, titleY=33, entryY=49,
                                      entryCellWidth=8, entryCenterOffset=6, genderX=168, genderY=49)
assert metadata['animations']['naming-pc'] == [dict(frame=0, duration=2), dict(frame=1, duration=2)]
# Source mutations prove these values are parsed rather than repeated runtime constants.
naming = (raw / 'src/naming_screen.c').read_text()
read_text = Path.read_text
def changed_inputs(path, *args, **kwargs):
    text = read_text(path, *args, **kwargs)
    if path == raw / 'include/constants/global.h': text = text.replace('PLAYER_NAME_LENGTH   7', 'PLAYER_NAME_LENGTH   6')
    if path == raw / 'src/strings.c': text = text.replace('YOUR NAME?', 'NEW NAME?')
    return text
with patch.object(Path, 'read_text', changed_inputs):
    altered_templates, _ = module.naming_metadata(raw, naming.replace('SpriteCallbackDummy, 56, 37, 0', 'SpriteCallbackDummy, 58, 37, 0'))
    assert altered_templates['NAMING_SCREEN_PLAYER']['maxChars'] == 6
    assert altered_templates['NAMING_SCREEN_PLAYER']['title'] == 'NEW NAME?'
    assert altered_templates['NAMING_SCREEN_PLAYER']['iconX'] == 58
for invalid in [naming.replace('.iconFunction = 2,', '.iconFunction = 99,'),
                naming.replace('.initialPage = KBPAGE_LETTERS_UPPER,', '.initialPage = KBPAGE_UNKNOWN,', 1),
                naming.replace('[NAMING_SCREEN_NICKNAME]   = &sMonNamingScreenTemplate,', '')]:
    try: module.naming_metadata(raw, invalid)
    except ValueError: pass
    else: raise AssertionError('Invalid naming source template accepted')
def invalid_pc_tile(path, *args, **kwargs):
    text = read_text(path, *args, **kwargs)
    if path == raw / 'src/naming_screen.c':
        prefix, parts = text.split('static const struct Subsprite sSubsprites_PCIcon[] = {', 1)
        text = prefix + 'static const struct Subsprite sSubsprites_PCIcon[] = {' + parts.replace('.tileOffset = 0,', '.tileOffset = 999,', 1)
    return text
with patch.object(Path, 'read_text', invalid_pc_tile):
    try: module.generate(raw)
    except ValueError as error: assert 'Invalid naming PC tile reference' in str(error)
    else: raise AssertionError('Invalid naming PC tile accepted')
print('PASS: naming layers and PC atlas match independent pixel oracle; templates/layout are source-derived; malformed tile/template inputs rejected; generated outputs match.')
