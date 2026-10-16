#!/usr/bin/env python3
"""Offline opening presentation import from the pinned pret input; never executes source code."""
import argparse
import ast
import io
import json
from pathlib import Path
import re
import struct
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
RAW = ROOT / 'content/raw/pokefirered/037335f4'
OUTPUT = ROOT / 'content/generated/ui/opening'


def rgba(image, transparent=True, palette=None):
    colors = palette or image.getpalette()
    if image.mode == 'L':
        image = image.point(lambda value: value // 17)
    result = Image.new('RGBA', image.size)
    result.putdata([tuple(((colors[i * 3 + c] >> 3) << 3) | (colors[i * 3 + c] >> 5) for c in range(3)) + ((0 if transparent and i == 0 else 255),) for i in image.getdata()])
    return result


def palette(path):
    return [v for line in path.read_text().splitlines()[3:] for v in map(int, line.split())]


def generate(raw=RAW):
    source = (raw / 'data/text/new_game_intro.inc').read_text()
    texts = {}
    for symbol, body in re.findall(r'^(\w+)::\s*\n(.*?)(?=^\w+::|\Z)', source, re.M | re.S):
        texts[symbol] = ''.join(ast.literal_eval('"' + s.replace('\\p', '\\f').replace('\\l', '\\n') + '"') for s in re.findall(r'\.string "(.*?)"', body)).rstrip('$')
    naming = (raw / 'src/naming_screen.c').read_text()
    keyboard = []
    for name in ['UPPER', 'LOWER', 'SYMBOLS']:
        body = re.search(r'\[KEYBOARD_' + ('SYMBOLS' if name == 'SYMBOLS' else 'LETTERS_' + name) + r'\] = \{(.*?)\}', naming, re.S)[1]
        rows = re.findall(r'__\("(.*?)"\)', body)
        columns = 6 if name == 'SYMBOLS' else 8
        keyboard.append([s.ljust(columns) for s in rows])
    if any(len(p) != 4 for p in keyboard):
        raise ValueError('Incomplete source naming keyboard')
    value = dict(schemaVersion=1, texts=texts, keyboard=keyboard)
    files = {'opening.json': (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()}
    def png(name, image):
        buffer = io.BytesIO(); image.save(buffer, format='PNG'); files[name + '.png'] = buffer.getvalue()
    for name in ['oak', 'rival', 'red', 'leaf']:
        png(name, rgba(Image.open(raw / f'graphics/oak_speech/{name}/pic.png')))
    tiles = Image.open(raw / 'graphics/oak_speech/oak_speech_bg.png')
    pal = Image.open(raw / 'graphics/oak_speech/bg_tiles.png').getpalette()
    data = (raw / 'graphics/oak_speech/oak_speech_bg.bin').read_bytes()
    bg = Image.new('RGBA', (256, 160))
    for index, (entry,) in enumerate(struct.iter_unpack('<H', data)):
        tile, bank = entry & 1023, entry >> 12
        crop = tiles.crop(((tile % (tiles.width // 8)) * 8, (tile // (tiles.width // 8)) * 8, (tile % (tiles.width // 8)) * 8 + 8, (tile // (tiles.width // 8)) * 8 + 8))
        crop = rgba(crop, False, pal[bank * 48:])
        if entry & 1024: crop = crop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if entry & 2048: crop = crop.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        bg.paste(crop, (index % 32 * 8, index // 32 * 8))
    png('background', bg.crop((0, 0, 240, 160)))
    platform = rgba(Image.open(raw / 'graphics/oak_speech/platform.png'), palette=palette(raw / 'graphics/oak_speech/platform.pal'))
    combined = Image.new('RGBA', (96, 32))
    for i in range(3): combined.paste(platform.crop((0, i * 32, 32, (i + 1) * 32)), (i * 32, 0))
    png('platform', combined)
    mon = Image.open(raw / 'graphics/pokemon/nidoran_f/front.png')
    png('nidoran-f', rgba(mon.crop((0, 0, 64, 64)), palette=palette(raw / 'graphics/pokemon/nidoran_f/normal.pal')))
    title_root = raw / 'graphics/title_screen'
    title = Image.new('RGBA', (256, 256), (0, 0, 0, 255))
    layers = [('border_bg', 'firered/border_bg', 'firered/background', False),
              ('copyright_press_start', 'copyright_press_start', 'firered/background', False),
              ('firered/box_art_mon', 'firered/box_art_mon', 'firered/box_art_mon', False),
              ('firered/game_title_logo', 'firered/game_title_logo', 'firered/game_title_logo', True)]
    for gfx, tilemap, colors, eight_bit in layers:
        tiles = Image.open(title_root / (gfx + '.png'))
        pal = palette(title_root / (colors + '.pal'))
        layer = Image.new('RGBA', (256, 256))
        for index, (entry,) in enumerate(struct.iter_unpack('<H', (title_root / (tilemap + '.bin')).read_bytes())):
            tile = entry & 1023
            x, y = tile % (tiles.width // 8) * 8, tile // (tiles.width // 8) * 8
            if y >= tiles.height: raise ValueError('Title tile outside image')
            crop = tiles.crop((x, y, x + 8, y + 8))
            crop = rgba(crop, True, pal)
            if entry & 1024: crop = crop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            if entry & 2048: crop = crop.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            layer.paste(crop, (index % 32 * 8, index // 32 * 8))
        title.alpha_composite(layer)
    png('title', title.crop((0, 0, 240, 160)))
    return files


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--raw', type=Path, default=RAW)
    parser.add_argument('--out', type=Path, default=OUTPUT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    files = generate(args.raw)
    for name, data in files.items():
        path = args.out / name
        if args.check:
            if not path.is_file() or path.read_bytes() != data: raise ValueError('Generated opening differs: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    print(f'Opening presentation: {len(files)} generated files verified' if args.check else f'Opening presentation: {len(files)} files generated')

if __name__ == '__main__': main()
