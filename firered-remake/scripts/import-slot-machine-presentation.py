#!/usr/bin/env python3
"""Import bounded original slot art/layout/text/cue identities; no gameplay or reference execution.
REFERENCE OUTPUT --dependency EXISTING_PROJECT [--check]. Outputs remain local/recoverable.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import zlib

sys.dont_write_bytecode = True
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-slot-machine-presentation-v1'
BASE = 'content/generated/slot-machine-presentation/'
RAW = 'content/raw/pokefirered/037335f4/'
HELPERS = (
    'scripts/slot_machine_layers.py',
    'scripts/presentation_atlas.py',
    'scripts/import-lab-battle-ui.py',
    'scripts/fetch-lab-battle-ui.py',
    'scripts/import-source-font.py',
    'scripts/fetch-source-font.py',
)
SHARED = {
    'src/window.c': 'content/raw/pokefirered/037335f4/src/window.c',
    'src/blit.c': 'content/raw/pokefirered/037335f4/src/blit.c',
    'src/text.c': 'content/raw/pokefirered/037335f4/src/text.c',
    'src/text_window.c': 'content/raw/pokefirered/037335f4/src/text_window.c',
    'src/strings.c': 'content/raw/pokefirered/037335f4/src/strings.c',
    'src/sound.c': 'content/raw/pokefirered/037335f4/src/sound.c',
    'include/constants/songs.h': 'content/raw/pokefirered/037335f4/include/constants/songs.h',
    'sound/song_table.inc': 'content/raw/pokefirered/037335f4/sound/song_table.inc',
    'charmap.txt': 'content/raw/pokefirered/037335f4/charmap.txt',
    'graphics/fonts/latin_small.png': 'content/raw/pokefirered/037335f4/graphics/fonts/latin_small.png',
    'graphics/fonts/keypad_icons.png': 'content/raw/pokefirered/037335f4/graphics/fonts/keypad_icons.png',
    'graphics/text_window/stdpal_2.pal': 'content/raw/pokefirered/037335f4/graphics/text_window/stdpal_2.pal'}


def need(ok, message):
    if not ok:
        raise ValueError(message)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n').encode()


def safe(root, path):
    need(path and not Path(path).is_absolute() and '\\' not in path and ':' not in path and
         not any(part in ('', '.', '..') for part in path.split('/')), 'Unsafe resource path')
    result = root / path
    need(not any(p.is_symlink() for p in [result, *result.parents]), 'Symlink resource rejected: ' + path)
    return result


def generate(reference, dependency):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')

    def git(*args):
        return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks', '-C', str(reference), *args], env=env)

    tree = {}
    for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
        if row:
            fields, path = row.split(b'\t')
            tree[path.decode()] = fields.decode().split()
    sources, source_rows, inherited, outputs, assets = {}, {}, {}, {}, []

    def source(path):
        if path not in sources:
            need(path in tree and tree[path][:2] == ['100644', 'blob'], 'Source is not regular blob: ' + path)
            data = git('cat-file', 'blob', tree[path][2])
            sources[path] = data
            raw = SHARED.get(path, RAW + path)
            if path in SHARED:
                need(safe(dependency, raw).read_bytes() == data, 'Inherited raw source differs: ' + path)
            source_rows[path] = dict(path=path, rawPath=raw)
        return sources[path]

    def reuse(path):
        data = safe(dependency, path).read_bytes()
        inherited[path] = dict(path=path)
        return data


    def helper(path, name):
        spec = importlib.util.spec_from_file_location(name, dependency / path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod

    pixels = helper('scripts/import-lab-battle-ui.py', 'slot_pixels')
    font = helper('scripts/import-source-font.py', 'slot_font')
    original = source('src/slot_machine.c').decode()
    # Resolve only the FireRed branch, preserving the independently locked original bytes.
    code = re.sub(r'#if defined\(FIRERED\)\n(.*?)#elif defined\(LEAFGREEN\)\n.*?#endif', r'\1', original, flags=re.S)

    def declaration(symbol):
        found = list(re.finditer(r'^static const [^\n]+\b' + symbol + r'[^=]*=\s*\{', code, re.M))
        need(len(found) == 1, 'Unique source declaration missing: ' + symbol)
        start, depth = found[0].end(), 1
        for end in range(start, len(code)):
            depth += (code[end] == '{') - (code[end] == '}')
            if depth == 0:
                return code[start:end]
        raise ValueError('Unterminated declaration: ' + symbol)

    def table(symbol):
        text = re.sub(r'//[^\n]*', '', declaration(symbol))
        text = re.sub(r'\[\w+\]\s*=', '', text)
        text = re.sub(r'0x[0-9a-fA-F]+', lambda m: str(int(m[0], 16)), text)
        text = '[' + text.replace('{', '[').replace('}', ']') + ']'
        return json.loads(re.sub(r',\s*\]', ']', text))

    def structures(symbol):
        block = declaration(symbol)
        return [{k: int(v, 0) for k, v in re.findall(r'\.(\w+)\s*=\s*(0x[\da-fA-F]+|\d+)', row)}
                for row in re.findall(r'\{(.*?)\}', block, re.S)]

    def require(needle):
        need(needle in code, 'Slot presentation source contract differs: ' + needle)

    def emit(name, width, height, rgba, **metadata):
        png = pixels.png(width, height, rgba)
        outputs[name + '.png'] = png
        row = dict(id=name, width=width, height=height, texturePath=BASE + name + '.png', **metadata)
        assets.append(row)
        return row

    paths = {name: 'graphics/slot_machine/' + ('digits' if name == 'digits' else 'firered/' + name) + '.png'
             for name in ['bg', 'reel_icons', 'clefairy', 'digits', 'button_pressed', 'combos_window']}
    images = {name: pixels.indexed_png(source(path)) for name, path in paths.items()}
    palettes = {name: pixels.colors(value[3]) for name, value in images.items()}
    expected = {'bg': (128, 72, 80), 'reel_icons': (32, 224, 80), 'clefairy': (32, 192, 16),
                'digits': (8, 160, 16), 'button_pressed': (16, 16, 16), 'combos_window': (112, 40, 48)}
    for name, (w, h, count) in expected.items():
        need((images[name][0], images[name][1], len(palettes[name])) == (w, h, count), 'Indexed source image differs: ' + name)
    rules = source('graphics_file_rules.mk').decode()
    for name, count in [('bg', 138), ('combos_window', 66)]:
        need(f'$(SLOTMACHINEGFXDIR)/firered/{name}.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles {count} -Wnum_tiles' in rules, 'Source tile trim differs')
    conversion = source('tools/gbagfx/convert_png.c').decode()
    need('% (1 << destBitDepth)' in conversion, 'Source 4bpp modulo palette conversion differs')
    palette_tags = [int(n) for n in re.findall(r'PALTAG_REEL_ICONS_(\d+)', declaration('sReelIconPaletteTags'))]
    need(palette_tags == [2, 2, 0, 0, 2, 4, 3], 'FireRed icon palette bindings differ')
    for name in ['reel_icons', 'clefairy', 'digits']:
        w, h, indices, _ = images[name]
        if name == 'reel_icons':
            rgba = b''.join(pixels.rgba(indices[i * 1024:(i + 1) * 1024], palettes[name][bank * 16:(bank + 1) * 16])
                            for i, bank in enumerate(palette_tags))
        else:
            rgba = pixels.rgba(indices, palettes[name])
        emit(name.replace('_', '-'), w, h, rgba, sourcePaths=[paths[name]], transparentIndex=0)
    # Intermediate source tiles/maps are consumed offline by slot_machine_layers; never shipped to the runtime.
    tile_sets = {name: pixels.tile_list(source(paths[name]))[:count] for name, count in [('bg', 138), ('combos_window', 66), ('button_pressed', 4)]}
    for name, tiles in tile_sets.items():
        outputs[name + '.indices'] = b''.join(tiles)
    maps = {}
    for name in ['bg', 'combos_window']:
        data = source('graphics/slot_machine/firered/' + name + '.bin')
        need(len(data) == 1280, 'Source map is not 32x20')
        maps[name] = list(struct.unpack('<640H', data))
        outputs[name + '.tilemap'] = data
        need(max(x & 1023 for x in maps[name]) < len(tile_sets[name]), 'Source tile index out of bounds')
    palettes['match_lines'] = pixels.palette(source('graphics/slot_machine/firered/match_lines.pal'))
    palettes['payout_lights'] = pixels.palette(source('graphics/slot_machine/firered/payout_lights.pal'))
    palettes['controls'] = pixels.palette(source('graphics/text_window/stdpal_2.pal'))
    bgpals = {i: palettes['bg'][16 * i:16 * (i + 1)] for i in range(5)}
    bgpals.update({5: palettes['match_lines'], 6: palettes['match_lines']})
    bgpals.update({7 + i: palettes['combos_window'][16 * i:16 * (i + 1)] for i in range(3)})

    def map_pixels(name):
        result = pixels.canvas(240, 160)
        for y in range(20):
            for x in range(30):
                entry = maps[name][y * 32 + x]
                pixels.blit(result, 240, pixels.tile_rgba(tile_sets[name][entry & 1023], bgpals[entry >> 12], bool(entry & 1024), bool(entry & 2048)), 8, 8, x * 8, y * 8)
        return bytes(result)

    emit('background', 240, 160, map_pixels('bg'), sourcePaths=[paths['bg'], 'graphics/slot_machine/firered/bg.bin'])
    emit('help', 240, 160, map_pixels('combos_window'), sourcePaths=[paths['combos_window'], 'graphics/slot_machine/firered/combos_window.bin'])
    strings = source('src/strings.c').decode()
    texts = {}
    for symbol in ['gString_OutOfCoins', 'gString_QuitPlaying', 'gString_SlotMachineControls']:
        match = re.search(r'const u8 ' + symbol + r'\[\] = _\(("(?:\\.|[^"\\])*")\);', strings)
        need(match is not None, 'Source slot text missing')
        texts[symbol] = json.loads(match[1])
    # Source font and original keypad roles; control strip is an exact independent static layer.
    chars = font.character_map(source('charmap.txt').decode())
    font_indices = font.decode_indices(source('graphics/fonts/latin_small.png'), 256)
    advances = font.widths(source('src/text.c').decode(), 'Small')
    keypad_w, keypad_h, keypad_indices, _ = pixels.indexed_png(source('graphics/fonts/keypad_icons.png'))
    need((keypad_w, keypad_h) == (128, 32), 'Keypad source shape differs')
    need('BlitBitmapRect4Bit(&sourceRect, &destRect, srcX, srcY, destX, destY, rectWidth, rectHeight, 0);' in source('src/window.c').decode() and
         'if (toOrr != colorKey)' in source('src/blit.c').decode(), 'Source keypad transparent color key differs')
    keypad, tokens = {}, []
    for match in re.finditer(r'\{(\w+)\}|([^{}]+)', texts['gString_SlotMachineControls']):
        if match[1]:
            token = match[1]
            m = re.search(r'\[CHAR_' + token + r'\]\s*=\s*\{\s*(0x[0-9a-fA-F]+),\s*(\d+),\s*(\d+)\s*\}', source('src/text.c').decode())
            need(m is not None, 'Keypad binding missing')
            offset, width, height = int(m[1], 16), int(m[2]), int(m[3])
            x, y = offset % 16 * 8, offset // 16 * 8
            indices = bytes(keypad_indices[(y + dy) * keypad_w + x + dx] for dy in range(height) for dx in range(width))
            keypad[token] = dict(tileOffset=offset, width=width, height=height)
            tokens.append((width, height, pixels.rgba(bytes(index if index else 15 for index in indices), palettes['controls'], False)))
        else:
            for char in match[2]:
                gid = chars[char]
                width, height = advances[gid], 13
                x, y = gid % 32 * 8, gid // 32 * 16
                roles = [font_indices[(y + dy) * 256 + x + dx] for dy in range(height) for dx in range(width)]
                indices = bytes(1 if v == 1 else 3 if v == 2 else 15 for v in roles)
                tokens.append((width, height, pixels.rgba(indices, palettes['controls'], False)))
    controls_width = sum(t[0] for t in tokens)
    controls_x = 240 - 4 - controls_width
    need(controls_x >= 0, 'Controls overflow')
    controls = pixels.canvas(240, 16, (*palettes['controls'][15], 255))
    x = controls_x
    for width, height, rgba in tokens:
        pixels.blit(controls, 240, rgba, width, height, x, 0)
        x += width
    emit('controls', 240, 16, bytes(controls), sourcePaths=['graphics/fonts/latin_small.png', 'graphics/fonts/keypad_icons.png', 'graphics/text_window/stdpal_2.pal'])
    require('x = DISPLAY_WIDTH - 4 - GetStringWidth(FONT_SMALL, gString_SlotMachineControls, 0);')
    require('textColor[0] = TEXT_DYNAMIC_COLOR_6;\n        textColor[1] = TEXT_COLOR_WHITE;\n        textColor[2] = TEXT_COLOR_DARK_GRAY;')
    # Extract layout and animation tables, with source-authenticated geometry guards.
    for needle in ['80 + 40 * i, 44 + 24 * j, 2', '85 + 7 * i, 30, 0', '133 + 7 * i, 30, 0',
                   'DISPLAY_WIDTH - 16, 136, 1', 'sSlotMachineGfxManager->clefairySprites[1]->hFlip = TRUE;',
                   's32 vcount = REG_VCOUNT - 0x2B;', 'if (vcount < 0x54u)', 'data->buttonPressedTiles[i][j] = j + 0xC0;',
                   'DecompressAndCopyTileDataToVram(2, sButtonPressed_Tiles, 0, 0xC0, 0);',
                   'ptr->bg1X += 16;', 'ptr->bg1X -= 16;', 'Slot_PrintOnWindow0(gString_OutOfCoins);',
                   'Slot_PrintOnWindow0(gString_QuitPlaying);']:
        require(needle)
    animations = {}
    for symbol in ['sAnimCmd_Clefairy_Neutral', 'sAnimCmd_Clefairy_Spinning', 'sAnimCmd_Clefairy_Payout', 'sAnimCmd_Clefairy_Lose']:
        body = declaration(symbol)
        animations[symbol.removeprefix('sAnimCmd_Clefairy_').lower()] = dict(frames=[dict(tileOffset=int(t, 0), durationTicks=int(d)) for t, d in re.findall(r'ANIMCMD_FRAME\(\s*(0x[\da-fA-F]+|\d+),\s*(\d+)\)', body)], loop='ANIMCMD_JUMP(0)' in body)
    line_names = ['TLBR', 'TopRow', 'MiddleRow', 'BottomRow', 'BLTR']
    layout = dict(width=240, height=160, textureFilter='nearest', paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', mapWidthTiles=32, mapHeightTiles=20, bgTemplates=structures('sBgTemplates'), windows=structures('sWindowTemplates'), yesNoWindow={k: int(v, 0) for k, v in re.findall('\\.(\\w+)\\s*=\\s*(0x[\\da-fA-F]+|\\d+)', declaration('sYesNoWindowTemplate'))}, reels=dict(centers=[[80 + 40 * i, 44 + 24 * j] for i in range(3) for j in range(5)], spriteWidth=32, spriteHeight=32, priority=3, subpriority=2, subpixelScale=8, paletteBanks=palette_tags, iconSymbols=['ICON_7', 'ICON_ROCKET', 'ICON_PIKACHU', 'ICON_PSYDUCK', 'ICON_CHERRIES', 'ICON_MAGNEMITE', 'ICON_SHELLDER'], tileOffsets=[0, 16, 32, 48, 64, 80, 96]), digits=dict(creditCenters=[[85 + 7 * i, 30] for i in range(4)], payoutCenters=[[133 + 7 * i, 30] for i in range(4)], spriteWidth=8, spriteHeight=16, tileOffsets=list(range(0, 20, 2)), leadingZeros=True, priority=1), clefairy=dict(centers=[[16, 136], [224, 136]], rightHFlip=True, spriteWidth=32, spriteHeight=32, priority=1, animations=animations), scanline=dict(registerOffset=43, activeRows=84, affineParameters=table('sReelIconAffineParams'), darkenCoefficients=table('sReelIconBldY'), effect='darken BG3, OBJ and backdrop; outside range affine 0x100 and darken 0'), matchLines=[dict(id=i, name=n, minimumBet=[3, 2, 1, 2, 3][i], tileIndices=table('sLineTiles_' + n)) for i, n in enumerate(line_names)], normalLinePalette=4, betLinePalette=5, winningLinePalette=6, winningUnblendedColorIndices=table('sWinningLineFlashPalIdxs'), buttonTileIndices=table('sReelButtonMapTileIdxs'), pressedButtonTileBase=192, controls=dict(font='small', fontId=0, x=controls_x, y=0, width=controls_width, paletteBank=14, colorIndices=[15, 1, 3], keypad=keypad), help=dict(bg=1, initialScrollPixels=256, finalScrollPixels=0, stepPixels=16, openButton='Right', closeButton='Left'), message=dict(fontId=2, x=1, y=2, letterSpacing=1, lineSpacing=2, borderTileStart=1, borderPalette=15), yesNo=dict(fontId=2, x=0, y=2, borderTileStart=10, borderPalette=13, initialCursor=0, wraps=False), backdropRgb555=[30, 30, 31])
    need(len(layout['scanline']['affineParameters']) == len(layout['scanline']['darkenCoefficients']) == 96, 'Scanline table length differs')
    # Reuse the existing source sound pack. This extends cue metadata only, never samples/PCM.
    pack_path = 'content/generated/synth-audio/firered.pack'
    pack = reuse(pack_path)
    need(len(pack) == 141541028 - 138975592, 'Original synth pack differs')
    songs = source('include/constants/songs.h').decode()
    song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source('sound/song_table.inc').decode(), re.M)
    sound = source('src/sound.c').decode()
    inherited_cues = json.loads(reuse('content/generated/synth-audio/manifest.json'))['cues']
    cues, new_cues = [], []
    for symbol in ['SE_RS_SHOP', 'SE_CONTEST_PLACE', 'SE_PIN', 'SE_WIN_OPEN', 'MUS_SLOTS_WIN', 'MUS_SLOTS_JACKPOT']:
        source_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
        address, player, player2 = struct.unpack_from('<IHH', pack, 0x084A32CC + source_id * 8 - 0x08489968)
        need(song_table[source_id] == (symbol.lower(), str(player), str(player2)), 'Source cue table differs')
        path = 'sound/songs/midi/' + symbol.lower() + '.mid'
        midi = source(path)
        need(midi[:4] == b'MThd' and b'\xff\x06\x01[' not in midi, 'Slot cue is not admitted non-looping MIDI')
        counter = int(re.search(r'\{\s*' + symbol + r',\s*(\d+)\s*\}', sound)[1]) if symbol.startswith('MUS_') else None
        cue = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path, sourcePlayer=player,
                   priority=pack[address - 0x08489968 + 2], sourceCounter=counter, songAddress=address, species=0, looping=False)
        old = next((c for c in inherited_cues if c['id'] == symbol), None)
        if old:
            need(old == cue, 'Inherited cue identity differs')
        else:
            new_cues.append(cue)
        call_sites = [dict(line=original.count('\n', 0, m.start()) + 1, function=m[1])
                      for m in re.finditer(r'\b(PlaySE|PlayFanfare)\(' + symbol + r'\)', original)]
        need(call_sites, 'Missing source cue call site')
        cues.append(dict(cue=cue, sourceId=source_id, inherited=old is not None, callSites=call_sites))
    need(len(new_cues) == 4, 'Expected four new cue identities')
    outputs['synth-manifest.json'] = encoded(dict(schemaVersion=1, sourceCommit=COMMIT, cues=new_cues))
    outputs['palettes.json'] = encoded(dict(schemaVersion=1, profile=PROFILE, palettes=palettes))
    outputs['layout.json'] = encoded(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, layout=layout, texts=texts))
    source('src/text_window.c')
    source_lock = dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', sourceCommit=COMMIT,
         helpers=HELPERS,
        inputs=[source_rows[p] for p in sorted(source_rows)], inheritedInputs=[inherited[p] for p in sorted(inherited)])
    lock_bytes = encoded(source_lock)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        assets=assets, audio=dict(packPath=pack_path, newCueCount=4, inheritedCueCount=2, cues=cues, preservesMapMusic=True),
        outputs=[dict(path=path) for path, data in sorted(outputs.items())],
        limitations=['Resource candidate only; no Application betting, probabilities or initial-state policy.',
                     'Background, help and controls are individual source layers, not complete game screenshots.',
                     'No scanline shader, task timings, native render or physical audio acceptance is claimed.'])
    from slot_machine_layers import bake
    outputs, manifest = bake(outputs, manifest)
    outputs['manifest.json'] = encoded(manifest)
    files = {BASE + p: b for p, b in outputs.items()}
    files.update({RAW + p: b for p, b in sources.items() if p not in SHARED})
    for path, data in files.items():
        if path.endswith('.json'):
            json.loads(data)
    return files


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--dependency', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    files = generate(args.reference, args.dependency)
    for path, data in files.items():
        destination = safe(args.output, path)
        if args.check:
            need(destination.read_bytes() == data, 'Regeneration differs: ' + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(passed=True, checked=args.check, outputs=len(files), bytes=sum(map(len, files.values())))))
