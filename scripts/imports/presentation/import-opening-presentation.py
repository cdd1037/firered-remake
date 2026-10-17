#!/usr/bin/env python3
"""Offline opening presentation import from the pinned pret input; never executes source code."""
import argparse
import ast
import io
import json
from pathlib import Path
import re
import struct
import sys
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from presentation_pixels import gba_4bpp_indices, tilemap as decode_tilemap, palette as decode_palette
RAW = ROOT / 'content/raw/pokefirered/037335f4'
OUTPUT = ROOT / 'content/generated/ui/opening'


def rgba(image, transparent=True, palette=None):
    colors = palette or image.getpalette()
    image = gba_4bpp_indices(image)
    result = Image.new('RGBA', image.size)
    result.putdata([tuple(((colors[i * 3 + c] >> 3) << 3) | (colors[i * 3 + c] >> 5) for c in range(3)) + ((0 if transparent and i == 0 else 255),) for i in image.getdata()])
    return result


def palette(path):
    return [v for line in path.read_text().splitlines()[3:] for v in map(int, line.split())]


def required(pattern, source, label):
    match = re.search(pattern, source, re.S)
    if match is None: raise ValueError('Missing naming source: ' + label)
    return match


def naming_metadata(raw, naming):
    """Compile the source's five callers into one bounded presentation contract."""
    strings = (raw / 'src/strings.c').read_text()
    constants = '\n'.join((raw / path).read_text() for path in
        ['include/constants/global.h', 'include/pokemon_storage_system.h', 'include/gba/defines.h'])
    def constant(name):
        return int(required(r'#define\s+' + name + r'\s+(\d+)\b', constants, name)[1])
    def function(name):
        return required(r'\b' + name + r'\(void\)\s*\{(.*?)\n\}', naming, name)[1]
    def window(name):
        body = required(r'\[' + name + r'\]\s*=\s*\{([^}]+)\}', naming, name)[1]
        return tuple(int(required(r'\.' + field + r'\s*=\s*(\d+)', body, field)[1]) * 8
                     for field in ['tilemapLeft', 'tilemapTop'])
    title_window, entry_window = window('WIN_TEXT_ENTRY_BOX'), window('WIN_TEXT_ENTRY')
    title_pos = required(r'FONT_NORMAL_COPY_1, sNamingScreen->template->title,\s*(\d+),\s*(\d+),', function('DrawNormalTextEntryBox'), 'title position')
    entry_y = int(required(r'i \* 8 \+ xpos \+ extraWidth,\s*(\d+),', function('DrawTextEntry'), 'entry position')[1])
    gender_pos = required(r'FONT_NORMAL,\s*(0x[0-9A-Fa-f]+|\d+),\s*(\d+),', function('DrawGenderIcon'), 'gender position')
    entry_formula = required(r'inputCharBaseXPos = \(DISPLAY_WIDTH - sNamingScreen->template->maxChars \* (\d+)\) / 2 \+ (\d+);', function('NamingScreen_Init'), 'entry centering')
    layout = dict(displayWidth=constant('DISPLAY_WIDTH'), titleX=title_window[0] + int(title_pos[1]),
                  titleY=title_window[1] + int(title_pos[2]), entryY=entry_window[1] + entry_y,
                  entryCellWidth=int(entry_formula[1]), entryCenterOffset=int(entry_formula[2]),
                  genderX=entry_window[0] + int(gender_pos[1], 0), genderY=entry_window[1] + int(gender_pos[2]))
    # Normalize the source's KBPAGE enum to the existing generated keyboard order.
    pages = {'KBPAGE_LETTERS_UPPER': 0, 'KBPAGE_LETTERS_LOWER': 1, 'KBPAGE_SYMBOLS': 2}
    icon_functions = re.findall(r'\bNamingScreen_\w+', required(r'sIconFunctions\[\]\)\(void\)\s*=\s*\{(.*?)\};', naming, 'icon dispatch')[1])
    box_functions = dict(re.findall(r'\[(NAMING_SCREEN_\w+)\]\s*=\s*(\w+)', required(r'sDrawTextEntryBoxFuncs\[\]\)\(void\)\s*=\s*\{(.*?)\};', naming, 'title dispatch')[1]))
    bindings = re.findall(r'\[(NAMING_SCREEN_\w+)\]\s*=\s*&([A-Za-z0-9_]+)', required(r'sNamingScreenTemplates\[\]\s*=\s*\{(.*?)\};', naming, 'template dispatch')[1])
    declared = re.findall(r'#define\s+(NAMING_SCREEN_\w+)\s+\d+', (raw / 'include/naming_screen.h').read_text())
    if set(name for name, _ in bindings) != set(declared) or len(bindings) != len(declared):
        raise ValueError('Naming template bindings incomplete or duplicated')
    templates = {}
    for name, symbol in bindings:
        body = required(r'\b' + symbol + r' =\s*\{(.*?)\};', naming, symbol)[1]
        fields = dict(re.findall(r'\.(\w+)\s*=\s*([A-Za-z0-9_]+)', body))
        if set(fields) != {'copyExistingString', 'maxChars', 'iconFunction', 'addGenderIcon', 'initialPage', 'title'}:
            raise ValueError('Naming template fields invalid: ' + name)
        icon = int(fields['iconFunction'])
        if icon not in range(1, len(icon_functions)) or fields['initialPage'] not in pages or fields['copyExistingString'] not in ('FALSE', 'TRUE') or fields['addGenderIcon'] not in ('0', '1'):
            raise ValueError('Naming template selector invalid: ' + name)
        icon_pos = required(r'(?:CreateObjectGraphicsSprite|CreateSprite|CreateMonIcon)\([^;]*?,\s*(-?\d+),\s*(-?\d+),\s*0\b', function(icon_functions[icon]), 'icon position')
        title = required(r'\b' + fields['title'] + r'\[\]\s*=\s*_\("((?:\\.|[^"\\])*)"\)', strings, 'title text')[1]
        if box_functions.get(name) not in ('DrawNormalTextEntryBox', 'DrawMonTextEntryBox'):
            raise ValueError('Naming title function invalid: ' + name)
        max_chars = constant(fields['maxChars'])
        if not 1 <= max_chars <= 10: raise ValueError('Naming character limit invalid: ' + name)
        templates[name] = dict(copyExistingString=fields['copyExistingString'] == 'TRUE', maxChars=max_chars,
            iconFunction=icon, addGenderIcon=fields['addGenderIcon'] == '1', initialPage=pages[fields['initialPage']],
            title=ast.literal_eval('"' + title + '"'), prefixSpeciesName=box_functions[name] == 'DrawMonTextEntryBox',
            iconX=int(icon_pos[1]), iconY=int(icon_pos[2]))
    return templates, layout


def generate(raw=RAW):
    source = (raw / 'data/text/new_game_intro.inc').read_text()
    texts = {}
    for symbol, body in re.findall(r'^(\w+)::\s*\n(.*?)(?=^\w+::|\Z)', source, re.M | re.S):
        texts[symbol] = ''.join(ast.literal_eval('"' + s.replace('\\p', '\\f').replace('\\l', '\\n') + '"') for s in re.findall(r'\.string "(.*?)"', body)).rstrip('$')
    naming = (raw / 'src/naming_screen.c').read_text()
    keyboard = []
    for name in ['UPPER', 'LOWER', 'SYMBOLS']:
        body = re.search(r'\[KEYBOARD_' + ('SYMBOLS' if name == 'SYMBOLS' else 'LETTERS_' + name) + r'\]\s*=\s*\{(.*?)\}', naming, re.S)[1]
        rows = re.findall(r'__\("(.*?)"\)', body)
        columns = 6 if name == 'SYMBOLS' else 8
        keyboard.append([s.ljust(columns) for s in rows])
    if any(len(p) != 4 for p in keyboard):
        raise ValueError('Incomplete source naming keyboard')
    oak = (raw / 'src/oak_speech.c').read_text()
    title_source = (raw / 'src/title_screen.c').read_text()
    tracks = {}
    for key, symbol, code, tiles in [('pikachu-body', 'sPikachuIntro_PikachuBody_Anim', oak, 16),
                                     ('pikachu-ears', 'sPikachuIntro_PikachuEars_Anim', oak, 8),
                                     ('pikachu-eyes', 'sPikachuIntro_PikachuEyes_Anim', oak, 2),
                                     ('flames', 'sSpriteAnim_Flame', title_source, 4)]:
        body = re.search(r'\b' + symbol + r'\[\] =\s*\{(.*?)\};', code, re.S)[1]
        tracks[key] = [dict(frame=int(a) // tiles, duration=int(b)) for a, b in re.findall(r'ANIMCMD_FRAME\(\s*(\d+),\s*(\d+)\)', body)]
        if not tracks[key]: raise ValueError('Empty animation: ' + key)
    rival_block = re.search(r'sRivalNameChoices\[\] =\s*\{(.*?)\};', oak, re.S)[1]
    rival_block = rival_block.split('#elif')[0]
    rival_names = [texts[name] for name in re.findall(r'gNameChoice_\w+', rival_block)]
    if len(rival_names) != 4: raise ValueError('Rival suggestions incomplete')
    # The source cursor uses deliberately uneven letter groups and wider symbols.
    offsets_body = re.search(r'sPageColumnXPos\[KBPAGE_COUNT\]\[KBCOL_COUNT\] =\s*\{(.*?)\};', naming, re.S)[1]
    column_offsets = []
    for page in ['LETTERS_UPPER', 'LETTERS_LOWER', 'SYMBOLS']:
        row = re.search(r'\[KEYBOARD_' + page + r'\]\s*=\s*\{([^}]+)\}', offsets_body)[1]
        column_offsets.append([int(n) for n in re.findall(r'\d+', row)])
    object_anims = (raw / 'src/data/object_events/object_event_anims.h').read_text()
    for key, symbol, code, tiles in [('naming-player', 'sAnim_GoSouth', object_anims, 1), ('naming-rival', 'sAnim_Rival', naming, 8)]:
        body = re.search(r'\b' + symbol + r'\[\] =\s*\{(.*?)\};', code, re.S)[1]
        tracks[key] = [dict(frame=int(a) // tiles, duration=int(b)) for a, b in re.findall(r'ANIMCMD_FRAME\(\s*(\d+),\s*(\d+)\)', body)]
    pc_animation = required(r'sAnim_PCIcon\[\] =\s*\{(.*?)\};', naming, 'PC icon animation')[1]
    tracks['naming-pc'] = [dict(frame=int(a), duration=int(b)) for a, b in re.findall(r'ANIMCMD_FRAME\(\s*(\d+),\s*(\d+)\)', pc_animation)]
    if not tracks['naming-pc'] or any(f['frame'] not in (0, 1) or not 1 <= f['duration'] <= 60 for f in tracks['naming-pc']):
        raise ValueError('Naming PC animation invalid')
    templates, layout = naming_metadata(raw, naming)
    value = dict(schemaVersion=1, texts=texts, keyboard=keyboard, animations=tracks, rivalNames=rival_names,
                 namingColumnOffsets=column_offsets, namingTemplates=templates, namingLayout=layout)
    files = {'opening.json': (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()}
    def png(name, image):
        buffer = io.BytesIO(); image.save(buffer, format='PNG'); files[name + '.png'] = buffer.getvalue()
    for name in ['oak', 'rival', 'red', 'leaf']:
        png(name, rgba(Image.open(raw / f'graphics/oak_speech/{name}/pic.png')))
        if name in ['red', 'leaf']:
            white = rgba(Image.open(raw / f'graphics/oak_speech/{name}/pic.png'))
            white.putdata([(255, 255, 255, a) for r, g, b, a in white.getdata()])
            png(name + '-white', white)

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
    pikachu = raw / 'graphics/oak_speech/pikachu_intro'
    for part in ['body', 'ears', 'eyes']:
        png('pikachu-' + part, rgba(Image.open(pikachu / (part + '.png')), palette=palette(pikachu / 'pikachu.pal')))
    # Compact source tilemap rectangles, not full-screen frame baking.
    def tilemap_image(tiles, data, width, pal):
        result = Image.new('RGBA', (width * 8, len(data) // 2 // width * 8))
        for index, (entry,) in enumerate(struct.iter_unpack('<H', data)):
            tile, bank = entry & 1023, entry >> 12
            x, y = tile % (tiles.width // 8) * 8, tile // (tiles.width // 8) * 8
            if y >= tiles.height: raise ValueError('Guide tile outside atlas')
            crop = rgba(tiles.crop((x, y, x + 8, y + 8)).point(lambda i: i % 16), False, pal[bank * 48:])
            if entry & 1024: crop = crop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            if entry & 2048: crop = crop.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            result.paste(crop, (index % width * 8, index // width * 8))
        return result
    guide_tiles = Image.open(raw / 'graphics/oak_speech/bg_tiles.png')
    for page in [2, 3]:
        png('controls-' + str(page), tilemap_image(guide_tiles, (raw / f'graphics/oak_speech/controls_guide_page_{page}.bin').read_bytes(), 5, guide_tiles.getpalette()))
    png('pikachu-background', tilemap_image(guide_tiles, (pikachu / 'tilemap.bin').read_bytes(), 30, guide_tiles.getpalette()))
    title_root = raw / 'graphics/title_screen'
    png('flames', rgba(Image.open(title_root / 'firered/flames.png')))
    png('slash', rgba(Image.open(title_root / 'slash.png')))
    title = Image.new('RGBA', (256, 256), (0, 0, 0, 255))
    layers = [('border_bg', 'firered/border_bg', 'firered/background', False),
              ('copyright_press_start', 'copyright_press_start', 'firered/background', False),
              ('firered/box_art_mon', 'firered/box_art_mon', 'firered/box_art_mon', False),
              ('firered/game_title_logo', 'firered/game_title_logo', 'firered/game_title_logo', True)]
    for layer_name, (gfx, tilemap, colors, eight_bit) in zip(['border', 'lettering', 'mon', 'logo'], layers):
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
        png('title-' + layer_name, layer.crop((0, 0, 240, 160)))
        if layer_name == 'mon':
            white = layer.copy(); white.putdata([(247, 247, 255, a) for r, g, b, a in white.getdata()])
            png('title-mon-white', white.crop((0, 0, 240, 160)))
            rounded = [0]*5 + [5]*5 + [11]*5 + [16]*5 + [21]*5 + [27]*5 + [31]*2
            gray = layer.copy()
            def gray_pixel(pixel):
                r, g, b, a = pixel
                value = rounded[min(31, ((r >> 3) * 76 + (g >> 3) * 151 + (b >> 3) * 29) >> 8)]
                value = (value << 3) | (value >> 2)
                return (value, value, value, a)
            gray.putdata([gray_pixel(p) for p in gray.getdata()])
            png('title-mon-gray', gray.crop((0, 0, 240, 160)))

        title.alpha_composite(layer)
        if layer_name == 'lettering':
            # The source blinks only palette entries 1..5, replacing them with 6.
            # Copyright uses entries 7..10 in the same palette bank and stays visible.
            hidden = Image.new('RGBA', (256, 256))
            blink_pal = pal.copy()
            for color in range(1, 6): blink_pal[color * 3:color * 3 + 3] = pal[18:21]
            for index, (entry,) in enumerate(struct.iter_unpack('<H', (title_root / (tilemap + '.bin')).read_bytes())):
                tile = entry & 1023
                x, y = tile % (tiles.width // 8) * 8, tile // (tiles.width // 8) * 8
                crop = rgba(tiles.crop((x, y, x + 8, y + 8)), True, blink_pal)
                if entry & 1024: crop = crop.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                if entry & 2048: crop = crop.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                hidden.paste(crop, (index % 32 * 8, index // 32 * 8))
            png('title-lettering-hidden', hidden.crop((0, 0, 240, 160)))
    png('title', title.crop((0, 0, 240, 160)))
    naming_root = raw / 'graphics/naming_screen'
    naming_tiles = gba_4bpp_indices(Image.open(naming_root / 'menu.png'))
    naming_colors = []
    for name in ['menu', 'page_swap_upper', 'page_swap_lower', 'page_swap_others', 'buttons', 'cursor']:
        naming_colors.extend(decode_palette((naming_root / (name + '.pal')).read_bytes()))
    for name in ['background', 'keyboard_upper', 'keyboard_lower', 'keyboard_symbols']:
        data = bytearray((naming_root / (name + '.bin')).read_bytes())
        if name == 'background':
            # The source draws its separate controls banner over these first two rows.
            data[:128] = bytes(128)
        layer = decode_tilemap(naming_tiles, data, naming_colors, width=256, height=160, transparent=True)
        png('naming-' + name.replace('_', '-'), layer.crop((0, 0, 240, 160)))
    for name, pal in [('back_button', 'buttons'), ('ok_button', 'buttons'), ('page_swap_frame', 'buttons'),
                      ('page_swap_upper', 'buttons'), ('page_swap_lower', 'buttons'), ('page_swap_others', 'buttons'),
                      ('cursor', 'cursor'), ('input_arrow', 'menu'), ('underscore', 'menu'), ('rival', 'rival')]:
        image = rgba(Image.open(naming_root / (name + '.png')), palette=palette(naming_root / (pal + '.pal')))
        # Page-swap label sprites consume only the first three source tiles.
        if name.startswith('page_swap_') and name != 'page_swap_frame': image = image.crop((0, 0, 24, 8))
        if name == 'cursor': image = image.crop((0, 0, 16, 16))
        png('naming-' + name.replace('_', '-'), image)
    for name in ['upper', 'lower', 'others']:
        png('naming-page-button-' + name, rgba(Image.open(naming_root / 'page_swap_button.png'), palette=palette(naming_root / ('page_swap_' + name + '.pal'))))
    for name, path in [('red', 'red_normal'), ('leaf', 'green_normal')]:
        png('naming-' + name, rgba(Image.open(raw / ('graphics/object_events/pics/people/' + path + '.png')), palette=palette(raw / 'graphics/object_events/palettes/player.pal')))
    # Three source 16x8 subsprites form each 16x24 PC frame. Keep both frames in
    # one compact atlas, using the PC sprite's PALTAG_MENU palette.
    pc_parts = required(r'sSubsprites_PCIcon\[\] =\s*\{(.*?)\n\};', naming, 'PC subsprites')[1]
    parts = [tuple(map(int, match)) for match in re.findall(r'\.x =\s*(-?\d+),\s*\.y =\s*(-?\d+),\s*\.shape = SPRITE_SHAPE\((\d+)x(\d+)\),\s*\.size = SPRITE_SIZE\(\d+x\d+\),\s*\.tileOffset =\s*(\d+)', pc_parts)]
    if not parts or any(w <= 0 or h <= 0 or w % 8 or h % 8 for x, y, w, h, tile in parts):
        raise ValueError('Naming PC subsprite geometry invalid')
    left, top = min(p[0] for p in parts), min(p[1] for p in parts)
    width, height = max(x + w for x, y, w, h, tile in parts) - left, max(y + h for x, y, w, h, tile in parts) - top
    if width > 64 or height > 64: raise ValueError('Naming PC frame geometry invalid')
    pc_atlas = Image.new('RGBA', (width, height * 2))
    for frame, name in enumerate(['pc_icon_off', 'pc_icon_on']):
        source = Image.open(naming_root / (name + '.png'))
        if source.width % 8 or source.height % 8: raise ValueError('Naming PC frame geometry invalid')
        source = rgba(source, palette=palette(naming_root / 'menu.pal'))
        for x, y, w, h, tile in parts:
            for dy in range(0, h, 8):
                for dx in range(0, w, 8):
                    index = tile + dy // 8 * (w // 8) + dx // 8
                    tx, ty = index % (source.width // 8) * 8, index // (source.width // 8) * 8
                    if ty + 8 > source.height: raise ValueError('Invalid naming PC tile reference')
                    pc_atlas.paste(source.crop((tx, ty, tx + 8, ty + 8)), (x - left + dx, frame * height + y - top + dy))
    png('naming-pc-icon', pc_atlas)
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
