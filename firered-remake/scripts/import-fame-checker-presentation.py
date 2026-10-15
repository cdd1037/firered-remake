#!/usr/bin/env python3
"""Import authenticated Fame Checker presentation from pinned Git blobs, without ROM execution.

REFERENCE OUTPUT [--check]. Requires Pillow. Original source sheets stay compact;
the runtime crops source frames and composes tilemap layers, never baked variants.
"""
import argparse
import binascii
import io
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import zlib
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-fame-checker-presentation-v1'
BASE = 'content/generated/fame-checker-presentation/'
RAW = 'content/raw/pokefirered/037335f4/'
LOCK = 'scripts/fame-checker-presentation.lock.json'


def need(ok, message):
    if not ok:
        raise ValueError(message)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n').encode()


def safe(root, path):
    need(path and not Path(path).is_absolute() and '\\' not in path and ':' not in path and
         not any(p in ('', '.', '..') for p in path.split('/')), 'Unsafe resource path')
    target = root / path
    need(not any(p.is_symlink() for p in [target, *target.parents]), 'Resource symlink rejected: ' + path)
    return target


def png(width, height, rgba):
    def chunk(kind, value):
        return struct.pack('>I', len(value)) + kind + value + struct.pack('>I', binascii.crc32(kind + value) & 0xffffffff)
    scan = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
            chunk(b'IDAT', zlib.compress(scan, 9)) + chunk(b'IEND', b''))


def gba_colors(colors):
    # gbagfx quantizes RGB8 to BGR555. Expand those same five-bit values for display.
    return [[((c >> 3) << 3) | ((c >> 3) >> 2) for c in row] for row in colors]


def generate(reference):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    def git(*args):
        return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks', '-C', str(reference), *args], env=env)
    tree = {}
    for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
        if row:
            fields, path = row.split(b'\t')
            tree[path.decode()] = fields.decode().split()
    sources, inputs, outputs, assets, palettes = {}, {}, {}, [], {}
    def source(path):
        if path not in sources:
            need(path in tree and tree[path][:2] == ['100644', 'blob'], 'Source is not a regular blob: ' + path)
            data = git('cat-file', 'blob', tree[path][2])
            sources[path] = data
            inputs[path] = dict(path=path, rawPath=RAW + path)
        return sources[path]
    def text(path):
        return source(path).decode()
    def block(code, symbol):
        matches = list(re.finditer(r'\b' + re.escape(symbol) + r'(?:\[[^\]]*\])*\s*=\s*\{', code))
        need(len(matches) == 1, 'Unique source table missing: ' + symbol)
        start, depth = matches[0].end(), 1
        for end in range(start, len(code)):
            depth += (code[end] == '{') - (code[end] == '}')
            if depth == 0:
                return re.sub(r'//[^\n]*', '', code[start:end])
        raise ValueError('Unterminated source table: ' + symbol)
    def incbin(code, symbol, extension):
        match = re.search(r'\b' + re.escape(symbol) + r'(?:\[[^\]]*\])+\s*=\s*INCBIN_U\d+\("([^"]+)"\)', code)
        need(match is not None, 'Source image binding missing: ' + symbol)
        return re.sub(r'\.(?:4bpp|gbapal)(?:\.lz)?$', extension, match[1])
    def indexed(path):
        image = Image.open(io.BytesIO(source(path)))
        need(image.mode == 'P' and image.width % 8 == 0 and image.height % 8 == 0 and image.width * image.height <= 65536,
             'Indexed source image profile differs: ' + path)
        rgb = image.getpalette()
        need(rgb and len(rgb) % 3 == 0, 'Missing source palette')
        return image.width, image.height, bytes(i % 16 for i in image.tobytes()), gba_colors([rgb[i:i + 3] for i in range(0, len(rgb), 3)])
    def palette(path):
        rows = text(path).splitlines()
        need(rows[:2] == ['JASC-PAL', '0100'] and len(rows) == int(rows[2]) + 3, 'Palette profile differs')
        colors = [list(map(int, row.split())) for row in rows[3:]]
        need(all(len(row) == 3 and all(0 <= c <= 255 for c in row) for row in colors), 'Palette range differs')
        return gba_colors(colors)
    def rgba(indices, colors):
        return bytes(c for i in indices for c in [*colors[i], 0 if i == 0 else 255])
    def asset(identifier, path, colors=None, **metadata):
        width, height, indices, embedded = indexed(path)
        colors = embedded if colors is None else colors
        need(len(colors) >= 16, 'Source sprite palette too short')
        pixels = rgba(indices, colors)
        outputs[identifier + '.png'] = png(width, height, pixels)
        outputs[identifier + '.indices'] = indices
        assets.append(dict(id=identifier, width=width, height=height, texturePath=BASE + identifier + '.png', indicesPath=BASE + identifier + '.indices', sourcePath=path, transparentIndex=0, **metadata))
        return width, height

    code = text('src/fame_checker.c')
    graphics = text('src/graphics.c')
    rules = text('graphics_file_rules.mk')
    conversion = text('tools/gbagfx/convert_png.c')
    need('% (1 << destBitDepth)' in conversion, 'Original 4bpp modulo conversion differs')
    for name, count in [('bg', 165), ('spinning_pokeball', 15)]:
        need(f'$(FAMECHECKERGFXDIR)/{name}.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles {count} -Wnum_tiles' in rules,
             'Source tile count differs')
    event = text('src/event_object_movement.c')
    event_header = text('include/event_object_movement.h')
    info = text('src/data/object_events/object_event_graphics_info.h')
    pointers = text('src/data/object_events/object_event_graphics_info_pointers.h')
    object_graphics = text('src/data/object_events/object_event_graphics.h')
    pics = text('src/data/object_events/object_event_pic_tables.h')
    anims = text('src/data/object_events/object_event_anims.h')
    constants = text('include/constants/event_objects.h')
    trainers = text('src/data/graphics/trainers.h')
    trainer_tables = text('src/data/trainer_graphics/front_pic_tables.h')
    text('src/trainer_pokemon_sprites.c')
    text('src/data/trainer_graphics/front_pic_anims.h')
    text('src/bg.c')
    text('src/text_window.c')
    text('include/constants/songs.h')
    text('sound/song_table.inc')
    need('InitObjectEventPalettes(0);' in code and 'gReservedSpritePaletteCount = 7;' in code and
         'sprite->y += sprite->centerToCornerVecY;' in event and
         'StartSpriteAnim(sprite, GetFaceDirectionAnimNum(DIR_SOUTH));' in event,
         'Original icon placement/palette setup differs')
    palette_bindings = dict((tag, symbol) for symbol, tag in re.findall(r'\{(gObjectEventPal_\w+),\s*(OBJ_EVENT_PAL_TAG_\w+)\}', block(event, 'sObjectEventSpritePalettes')))
    slot_tags = re.findall(r'OBJ_EVENT_PAL_TAG_\w+', block(event, 'sObjectPaletteTags0'))
    enum = re.search(r'enum\s*\{(.*?)\}', event_header, re.S)[1]
    slot_ids = {name: i for i, name in enumerate(re.findall(r'\bPALSLOT_\w+', enum))}
    def object_palette(tag):
        path = incbin(object_graphics, palette_bindings[tag], '.pal')
        palettes[tag] = palette(path)
        return palettes[tag], path
    # Question-mark OAM explicitly uses palette slot 2, initialized as NPC blue.
    question_colors, question_palette = object_palette(slot_tags[2])
    asset('question-mark', incbin(code, 'sQuestionMarkSpriteGfx', '.png'), question_colors,
          paletteSourcePath=question_palette, paletteSlot=2)
    for identifier, symbol in [('cursor', 'sSelectorCursorSpriteGfx'), ('pokeball', 'sSpinningPokeballSpriteGfx')]:
        asset(identifier, incbin(code, symbol, '.png'))
    indicators = text('src/menu_indicators.c')
    arrow_palette_path = incbin(indicators, 'sRedArrowPal', '.pal')
    asset('scroll-arrows', incbin(indicators, 'sRedArrowOtherGfx', '.png'), palette(arrow_palette_path),
          paletteSourcePath=arrow_palette_path, paletteSlot=1)
    need('LoadPalette(sRedArrowPal, OBJ_PLTT_ID(arrowInfo->palNum), sizeof(sRedArrowPal));' in indicators and
         'ANIMCMD_FRAME(4, 30, 0, 1)' in indicators, 'Source scroll arrow binding differs')
    sine = [int(float(value) * 256) for value in re.findall(r'Q_8_8\((-?[\d.]+)\)', block(text('src/trig.c'), 'gSineTable'))]
    need(len(sine) == 320, 'Source sine-table size differs')
    need(next(a for a in assets if a['id'] == 'cursor')['height'] == 64, 'Cursor two-frame sheet differs')
    # The final Pokeball tile is omitted by -num_tiles 15 and is transparent in the source PNG.
    pw, ph, pi, _ = indexed('graphics/fame_checker/spinning_pokeball.png')
    need((pw, ph) == (32, 32) and all(pi[y * pw + x] == 0 for y in range(24, 32) for x in range(24, 32)),
         'Trimmed Pokeball tile is not transparent')
    portraits = []
    person_symbols = re.findall(r'\[(FAMECHECKER_\w+)\]', block(code, 'sTrainerIdxs'))
    trainer_pic = dict(re.findall(r'\[(FAMECHECKER_\w+)\]\s*=\s*(TRAINER_PIC_\w+)', block(code, 'sFameCheckerTrainerPicIdxs')))
    nontrainers = {'FAMECHECKER_OAK': 'sOakSpriteGfx', 'FAMECHECKER_DAISY': 'sDaisySpriteGfx',
                   'FAMECHECKER_BILL': 'sBillSpriteGfx', 'FAMECHECKER_MRFUJI': 'sFujiSpriteGfx'}
    palettes['silhouette'] = palette('graphics/fame_checker/silhouette.pal')
    for index, person in enumerate(person_symbols):
        identifier = 'portrait-' + str(index)
        if person in nontrainers:
            path = incbin(code, nontrainers[person], '.png')
            size = asset(identifier, path)
            palette_path = path
        else:
            key = trainer_pic[person].removeprefix('TRAINER_PIC_')
            picture_symbol = re.search(r'TRAINER_SPRITE\(' + key + r',\s*(\w+),\s*0x800\)', trainer_tables)[1]
            pal_symbol = re.search(r'TRAINER_PAL\(' + key + r',\s*(\w+)\)', trainer_tables)[1]
            path = incbin(trainers, picture_symbol, '.png')
            palette_path = incbin(trainers, pal_symbol, '.pal')
            size = asset(identifier, path, palette(palette_path), paletteSourcePath=palette_path)
        need(size == (64, 64), 'Portrait dimensions differ')
        portraits.append(dict(sourceIndex=index, sourceSymbol=person, assetId=identifier, paletteSourcePath=palette_path,
                              trainerPictureSymbol=trainer_pic[person], nonTrainer=person in nontrainers))
    need(len(portraits) == 16, 'Portrait inventory differs')
    facts = re.findall(r'OBJ_EVENT_GFX_\w+', block(code, 'sFameCheckerArrayNpcGraphicsIds'))
    need(len(facts) == 96, 'Fact icon table differs')
    icon_rows = []
    for symbol in sorted(set(facts)):
        graphics_id = int(re.search(r'^#define\s+' + symbol + r'\s+(\d+)\b', constants, re.M)[1])
        info_symbol = re.search(r'\[' + symbol + r'\]\s*=\s*&?(\w+)', pointers)[1]
        body = block(info, info_symbol)
        field = dict(re.findall(r'\.(\w+)\s*=\s*(&?\w+)', body))
        width, height = int(field['width']), int(field['height'])
        first_anim = re.search(r'=\s*(sAnim_\w+)', block(anims, field['anims']))[1]
        animation = block(anims, first_anim)
        frame = int(re.search(r'ANIMCMD_FRAME\(\s*(\d+),', animation)[1])
        need(frame == 0 and '.hFlip' not in animation and '.vFlip' not in animation,
             'Unsupported south-facing fact frame')
        frame_rows = re.findall(r'overworld_frame\((\w+),\s*(\d+),\s*(\d+),\s*(\d+)\)', block(pics, field['images']))
        picture, tiles_w, tiles_h, source_frame = frame_rows[frame]
        need((int(tiles_w) * 8, int(tiles_h) * 8) == (width, height), 'Object frame size differs')
        slot = slot_ids[field['paletteSlot']]
        tag = slot_tags[slot] if slot < len(slot_tags) else field['paletteTag']
        colors, palette_path = object_palette(tag)
        path = incbin(object_graphics, picture, '.png')
        identifier = 'object-' + str(graphics_id)
        sheet_w, sheet_h = asset(identifier, path, colors, paletteSourcePath=palette_path)
        columns = sheet_w // width
        x, y = int(source_frame) % columns * width, int(source_frame) // columns * height
        need(x + width <= sheet_w and y + height <= sheet_h, 'Object crop outside sheet')
        icon_rows.append(dict(symbol=symbol, sourceId=graphics_id, assetId=identifier, x=x, y=y, width=width, height=height,
                              paletteSlot=slot, paletteTag=tag, paletteSourcePath=palette_path,
                              graphicsInfoSymbol=info_symbol, animationSymbol=first_anim, sourceFrame=frame,
                              placement='centerX-bottomY'))
    bg_path = incbin(graphics, 'gFameCheckerBgTiles', '.png')
    bw, bh, bi, bp = indexed(bg_path)
    need((bw, bh, len(bp)) == (128, 88, 32), 'BG tiles/palette dimensions differ')
    palettes['background'] = bp
    palettes['text'] = palette('graphics/text_window/stdpal_2.pal')
    # The source right-aligns three help strings inside the transparent 24×2 tile window.
    # Keypad symbols retain their source pixels; only literal glyphs use White={0,1,2}.
    strings = text('src/strings.c')
    font_code = text('src/text.c')
    source('src/window.c')
    source('src/blit.c')
    character_constants = text('include/characters.h')
    charmap = text('charmap.txt').split('@ Hiragana')[0]
    characters = {("'" if spelling == "\\'" else spelling): int(value, 16) for spelling, value in
                  re.findall(r"^'((?:\\.|[^'])*)'\s*=\s*([0-9A-F]{2})\s*$", charmap, re.M)}
    widths = list(map(int, re.findall(r'\d+', block(font_code, 'sFontSmallLatinGlyphWidths'))))
    need(len(widths) == 512, 'Small font width table differs')
    font_w, font_h, font_indices, _ = indexed('graphics/fonts/latin_small.png')
    keypad_w, keypad_h, keypad_indices, _ = indexed('graphics/fonts/keypad_icons.png')
    need((font_w, font_h, keypad_w, keypad_h) == (256, 256, 128, 32), 'Font/keypad source shape differs')
    help_rows = []
    for mode, symbol in [('main', 'gFameCheckerText_MainScreenUI'), ('pick', 'gFameCheckerText_PickScreenUI'),
                         ('facts', 'gFameCheckerText_FlavorTextUI')]:
        literal = re.search(r'const u8 ' + symbol + r'\[\] = _\(("(?:\\.|[^"\\])*")\);', strings)
        need(literal is not None, 'Source help text missing')
        value = json.loads(literal[1])
        cells, tokens = [], []
        for token in re.finditer(r'\{(\w+)\}|([^{}]+)', value):
            if token[1]:
                key = 'DPAD_NONE' if token[1] == 'DPAD_ANY' else token[1]
                if token[1] == 'DPAD_ANY':
                    need(re.search(r'^DPAD_ANY\s*=\s*F8 0C\s*$', text('charmap.txt'), re.M) and
                         re.search(r'^#define\s+CHAR_DPAD_NONE\s+0x0C\b', character_constants, re.M), 'DPAD_ANY alias differs')
                match = re.search(r'\[CHAR_' + key + r'\]\s*=\s*\{\s*(0x[0-9a-fA-F]+),\s*(\d+),\s*(\d+)\s*\}', font_code)
                need(match is not None, 'Source keypad symbol missing: ' + token[1])
                tile, width, height = int(match[1], 16), int(match[2]), int(match[3])
                sx, sy = tile % 16 * 8, tile // 16 * 8
                pixels = bytes(keypad_indices[(sy + y) * keypad_w + sx + x] for y in range(height) for x in range(width))
                tokens.append(dict(symbol=token[1], tileOffset=tile, width=width, height=height))
                cells.append((width, height, pixels))
            else:
                for char in token[2]:
                    glyph = characters[char]
                    width, height = widths[glyph], 13
                    sx, sy = glyph % 32 * 8, glyph // 32 * 16
                    roles = [font_indices[(sy + y) * font_w + sx + x] for y in range(height) for x in range(width)]
                    cells.append((width, height, bytes(role if role in (1, 2) else 0 for role in roles)))
        total = sum(cell[0] for cell in cells)
        x = 236 - total
        need(x >= 48 and total <= 188, 'Source help overflows its window')
        indices = bytearray(240 * 16)
        for width, height, pixels in cells:
            for y in range(height):
                indices[y * 240 + x:y * 240 + x + width] = pixels[y * width:(y + 1) * width]
            x += width
        identifier = 'help-' + mode
        pixels = rgba(indices, palettes['text'])
        outputs[identifier + '.png'] = png(240, 16, pixels)
        outputs[identifier + '.indices'] = bytes(indices)
        assets.append(dict(id=identifier, width=240, height=16, texturePath=BASE + identifier + '.png', indicesPath=BASE + identifier + '.indices', sourcePaths=['src/strings.c', 'src/text.c', 'graphics/fonts/latin_small.png', 'graphics/fonts/keypad_icons.png'], transparentIndex=0))
        help_rows.append(dict(mode=mode, sourceSymbol=symbol, sourceText=value, assetId=identifier,
                              x=236-total, y=0, width=total, font='FONT_SMALL', colorIndices=[0,1,2], keypad=tokens))
    outputs['background.indices'] = bytes(bi[(y + dy) * bw + x + dx]
        for y in range(0, bh, 8) for x in range(0, bw, 8) for dy in range(8) for dx in range(8))[:165 * 64]
    for number in range(1, 4):
        path = 'graphics/fame_checker/tilemap' + str(number) + '.bin'
        data = source(path)
        need(len(data) == 2048 and all((entry & 1023) < 165 and (entry >> 12) < 2 for entry in struct.unpack('<1024H', data)),
             'BG source map dimensions/tile/palette index differ')
        outputs['bg' + str(number) + '.tilemap'] = data
    colors = {name: list(map(int, re.findall(r'\d+', block(code, 'sTextColor_' + name)))) for name in ['White', 'DkGrey', 'Green']}
    windows = []
    for symbol, body in re.findall(r'\[(FCWINDOWID_\w+)\]\s*=\s*\{(.*?)\}', block(code, 'sUIWindowTemplates'), re.S):
        row = dict((k, int(v)) for k, v in re.findall(r'\.(\w+)\s*=\s*(\d+)', body) if k != 'baseBlock')
        windows.append(dict(symbol=symbol, **row))
    info_updates = {}
    update_code = code[code.index('static void UpdateInfoBoxTilemap(u8 bg, s16 state)\n{'):]
    for states, body in re.findall(r'(?:if|else if)\s*\((state == [^)]*)\)\s*\{(.*?)\}', update_code, re.S):
        rows = [[int(x.strip(), 0) for x in match.split(',')] for match in
                re.findall(r'FillBgTilemapBufferRect\(bg,\s*([^;]+)\);', body)]
        for state in re.findall(r'state == (\d+)', states):
            info_updates[state] = rows
    need(set(info_updates) == set('012345'), 'Info box state inventory differs')
    for required in ['PERSON_X  148', 'PERSON_Y   66', 'ANIMCMD_FRAME(16, 15)', 'AFFINEANIMCMD_FRAME(0, 0, 4, 20)',
                     '47 * (i % 3) + 0x72', '27 * (i / 3) + 0x2F', '14 * cursorY + 4', '188 - width',
                     'ChangeBgX(1, 0xA00, 1)', 'sprite->x2 -= 10', 'SetGpuReg(REG_OFFSET_BLDALPHA, 0x07)']:
        need(required in code, 'Source layout contract differs: ' + required)
    layout = dict(schemaVersion=1, profile=PROFILE, screen=dict(width=240, height=160), windows=windows,
        textColors=colors, fonts=dict(list='FONT_NORMAL', message='FONT_NORMAL', help='FONT_SMALL', origin='FONT_SMALL'),
        list=dict(itemX=8, cursorX=0, firstY=4, rowHeight=14, maximumVisible=5),
        help=dict(rightX=188, y=0, letterSpacing=0, lineSpacing=2, layers=help_rows),
        message=dict(foreground=2, background=1, shadow=3, width=208, height=32, sourcePrinter='AddTextPrinterParameterized2',
                     preserveControlCodes=['\\n', '\\l', '\\p', '{PLAYER}', '{RIVAL}']),
        origin=dict(centerWidth=84, locationY=0, objectY=10),
        factGrid=dict(columns=3, rows=2, firstCenterX=114, columnStep=47, firstBottomY=47, rowStep=27),
        question=dict(firstCenterY=31, width=16, height=32, paletteSlot=2),
        cursor=dict(firstCenterY=34, width=32, height=32, frames=[dict(x=0,y=0,duration=15),dict(x=0,y=32,duration=15)], loop=True),
        portrait=dict(centerX=148, centerY=66, width=64, height=64, paletteSlot=6),
        pokeball=dict(centerX=226, centerY=66, width=32, height=32, rotationStep=4, rotationPeriod=256, frameDuration=20),
        pickTransition=dict(initialOffsetX=240, stepPixels=10, bg1SourceX=240),
        infoBox=dict(framesPerStep=4, updates=info_updates),
        blend=dict(nonselectedObjMode='ST_OAM_OBJ_BLEND', eva=7, evb=0, brightness=8),
        scrollArrows=dict(x=40, upY=26, downY=100, paletteSlot=1, assetId='scroll-arrows',
                          crop=dict(x=0,y=16,width=16,height=16), downVFlip=True,
                          bounceAxis='y', bounceMultiplier=2, upFrequency=8, downFrequency=-8, anglePeriod=256,
                          upOffsets=[int(sine[i * 8] * 2 / 256) for i in range(32)],
                          downOffsets=[int(sine[(-i * 8) % 256] * 2 / 256) for i in range(32)]))
    # Retain cue identities only. Playback reuses the existing synth pack; no new PCM.
    cues = [dict(role=role, sourceSymbol=symbol) for role, symbol in [
        ('open-close', 'SE_M_SWIFT'), ('person-list-select', 'SE_SELECT'), ('fact-enter-back', 'SE_SELECT'),
        ('fact-move', 'SE_M_SWAGGER2'), ('pick-toggle', 'SE_M_LOCK_ON')]]
    for cue in cues:
        need('PlaySE(' + cue['sourceSymbol'] + ')' in code, 'Source sound cue differs')
        cue['sourceId'] = int(re.search(r'^#define\s+' + cue['sourceSymbol'] + r'\s+(\d+)\b', text('include/constants/songs.h'), re.M)[1])
    outputs['layout.json'] = encoded(layout)
    outputs['palettes.json'] = encoded(dict(schemaVersion=1, profile=PROFILE, palettes=palettes))
    source_lock = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                       inputs=[inputs[p] for p in sorted(inputs)], policy='Pinned source blobs only; no ROM, native reference execution, or PCM')
    lock = encoded(source_lock)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                    assets=assets, portraits=portraits, factIconSymbols=facts, objectIcons=icon_rows, audio=dict(cues=cues),
                    outputs=[dict(path=p) for p, d in sorted(outputs.items())])
    result = {RAW + p: d for p, d in sources.items()}
    result.update({BASE + p: d for p, d in outputs.items()})
    result[BASE + 'manifest.json'] = encoded(manifest)
    result[LOCK] = lock
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = generate(args.reference)
    for path, expected in data.items():
        target = safe(args.output, path)
        if args.check:
            need(target.read_bytes() == expected, 'Imported resource differs: ' + path)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(expected)
    print(json.dumps(dict(files=len(data), bytes=sum(map(len, data.values())),
                          lockBytes=len(data[LOCK]),
                          manifestBytes=len(data[BASE + 'manifest.json']), checked=args.check), sort_keys=True))


if __name__ == '__main__':
    main()
