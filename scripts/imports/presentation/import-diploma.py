#!/usr/bin/env python3
"""Rebuild the original Kanto/National diploma backgrounds and screen text from pinned raw inputs."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
import argparse
import json
import re
import struct
from image_io import indexed_png, png
from presentation_source import Source, canonical, need, safe
from project_paths import PROJECT_ROOT
from ui_tiles import colors, tile_list, tile_rgba, blit, canvas, require
from world_source import WorldSource

BASE = 'content/generated/ui/diploma/'


def render_background(image, tilemap):
    width, height, indices, rgb = indexed_png(image)
    need((width, height) == (128, 128) and len(rgb) == 32, 'Diploma indexed sheet/palette differs')
    need(len(tilemap) == 4096, 'Diploma horizontal screenblock size differs')
    tiles = tile_list(image)
    palette = colors(rgb)
    backdrop = palette[0]
    banks = [palette[:16], palette[16:]]
    # GBA BG color zero is transparent to the backdrop, including in palette bank one.
    for bank in banks:
        bank[0] = backdrop
    out = canvas(512, 256, (*backdrop, 255))
    entries = struct.unpack('<2048H', tilemap)
    for y in range(32):
        for x in range(64):
            entry = entries[(x // 32) * 1024 + y * 32 + x % 32]
            tile, bank = entry & 1023, entry >> 12
            need(tile < len(tiles) and bank < len(banks), 'Diploma tile or palette outside source')
            pixels = tile_rgba(tiles[tile], banks[bank], bool(entry & 1024), bool(entry & 2048), transparent=False)
            blit(out, 512, pixels, 8, 8, x * 8, y * 8)
    return bytes(out)


def crop(background, x):
    return b''.join(background[(y * 512 + x) * 4:(y * 512 + x + 240) * 4] for y in range(160))


def generate(source):
    code = source.text('src/diploma.c')
    for value in ['.screenSize = 1,', 'SetGpuReg(REG_OFFSET_BG1HOFS, 0x100);',
                  'BeginNormalPaletteFade(PALETTES_ALL, 0, 16, 0, RGB_BLACK);',
                  'BeginNormalPaletteFade(PALETTES_ALL, 0, 0, 16, RGB_BLACK);',
                  'PlayFanfareByFanfareNum(FANFARE_OBTAIN_BADGE);', 'if (WaitFanfare(FALSE))',
                  'if (JOY_NEW(A_BUTTON))', 'SetMainCallback2(CB2_ReturnToFieldFromDiploma);',
                  'DynamicPlaceholderTextUtil_SetPlaceholderPtr(0, gSaveBlock2Ptr->playerName);',
                  'static const ALIGNED(4) u8 sTextColors[3] = {0, 2, 3};']:
        require(code, value)
    # Window-local y coordinates become absolute screen coordinates at tilemapTop * 8.
    window_top = int(re.search(r'\.tilemapTop = (\d+)', code)[1]) * 8
    positions = re.findall(r'AddTextPrinterParameterized3\(WIN_TEXT, FONT_NORMAL, (120(?: - \(width / 2\))?), (\d+), sTextColors', code)
    need(len(positions) == 3 and [x[0] for x in positions] == ['120 - (width / 2)', '120 - (width / 2)', '120'], 'Diploma text alignment differs')
    text = source.text('src/strings.c')
    def string(symbol):
        match = re.search(r'const u8 ' + symbol + r'\[\] = _\("((?:[^"\\]|\\.)*)"\);', text)
        need(match is not None, 'Diploma source text missing: ' + symbol)
        return match[1].replace('\\n', '\n').replace('\\"', '"').replace('{DYNAMIC 0x00}', '{PLAYER}').replace('{DYNAMIC 0x01}', '{DEX}')
    font = source.text('src/new_menu_helpers.c')
    normal = re.search(r'\[FONT_NORMAL\]\s*=\s*\{(.*?)\n\s*\}', font, re.S)
    need(normal is not None, 'Normal font metadata missing')
    height = int(re.search(r'\.maxLetterHeight = (\d+)', normal[1])[1])
    spacing = int(re.search(r'\.lineSpacing = (\d+)', normal[1])[1])
    sound = source.text('src/sound.c')
    fanfare = re.search(r'\[FANFARE_OBTAIN_BADGE\]\s*=\s*\{\s*(MUS_\w+)\s*,\s*(\d+)\s*\}', sound)
    need(fanfare is not None, 'Diploma fanfare timing missing')
    background = render_background(source.read('graphics/diploma/diploma.png'), source.read('graphics/diploma/diploma.bin'))
    assets = [dict(id=name, width=240, height=160, texturePath=name + '.png') for name in ['kanto', 'national']]
    document = dict(schemaVersion=1, assets=assets, playerTemplate=string('gText_Diploma_Player'),
                    bodyTemplate=string('gText_Diploma_ThisDocument'), kanto=string('gText_Diploma_Kanto'),
                    national=string('gText_Diploma_National'), signature=string('gText_Diploma_GameFreak'),
                    centerX=120, playerY=window_top + int(positions[0][1]), bodyY=window_top + int(positions[1][1]),
                    lineHeight=height + spacing, signatureX=120, signatureY=window_top + int(positions[2][1]),
                    fadeFrames=16, fanfare=fanfare[1], fanfareCounterFrames=int(fanfare[2]))
    return {'diploma.json': canonical(document), 'kanto.png': png(240, 160, crop(background, 0)),
            'national.png': png(240, 160, crop(background, 256))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    inputs = parser.add_mutually_exclusive_group()
    inputs.add_argument('--reference-root', type=Path)
    inputs.add_argument('--raw', type=Path, help='Explicit pinned raw input directory')
    parser.add_argument('--out', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target = args.out or args.project_root / BASE
    raw = args.raw or args.reference_root or args.project_root / 'content/raw/pokefirered/037335f4'
    need(not target.resolve().is_relative_to(raw.resolve()) and not raw.resolve().is_relative_to(target.resolve()), 'Source/output overlap')
    outputs = generate(WorldSource(args.raw) if args.raw else Source(args.project_root, args.reference_root))
    for name, data in outputs.items():
        path = safe(target, name)
        if args.check:
            need(path.read_bytes() == data, 'Diploma regeneration differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(check=args.check, outputs=sorted(outputs), directory=str(target))))


if __name__ == '__main__':
    main()
