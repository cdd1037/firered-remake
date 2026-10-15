#!/usr/bin/env python3
"""Pinned, local-only first-capture dex data, settled page skin and caught-mon backs.

Reads fixed Git blobs or authenticated cache bytes. Never executes upstream code.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import io
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-route1-capture-dex-v1'
BACK_PROFILE = 'firered-route1-captured-back-static-v1'
NAMES = (('Pidgey', 16), ('Rattata', 19))
PATHS = frozenset('''src/pokedex_screen.c
src/data/pokemon/pokedex_entries.h
src/data/pokemon/pokedex_text.h
src/data/pokemon/pokedex_text_fr.h
src/data/pokemon/pokedex_categories.h
src/data/text/species_names.h
src/data/pokemon_graphics/footprint_table.h
src/data/graphics/pokemon.h
include/constants/species.h
include/constants/pokedex.h
include/constants/pokemon.h
include/constants/battle.h
src/strings.c
src/text.c
src/new_menu_helpers.c
include/text.h
src/pokemon.c
src/trainer_pokemon_sprites.c
src/battle_anim_mons.c
src/battle_controller_player.c
src/battle_controller_opponent.c
src/data/pokemon_graphics/back_pic_coordinates.h
src/data/pokemon_graphics/back_pic_table.h
src/data/pokemon_graphics/palette_table.h
src/data/pokemon_graphics/shiny_palette_table.h
tools/gbagfx/gfx.c
tools/gbagfx/main.c
tools/gbagfx/convert_png.c
graphics/pokedex/kanto_dex_bgtiles.png
graphics/pokedex/kanto_dex_bgpals.pal'''.splitlines()) | frozenset(
    f'graphics/pokemon/{name.lower()}/{file}' for name, _ in NAMES
    for file in ('footprint.png', 'back.png', 'normal.pal', 'shiny.pal'))


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


codec = module('capture_dex_pixels', ROOT / 'scripts/imports/presentation/import-lab-battle-ui.py')
battle = module('capture_dex_battle', ROOT / 'scripts/imports/presentation/import-route1-battle-art.py')


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative):
    root = pathlib.Path(root).resolve()
    path = pathlib.PurePosixPath(relative)
    target = root / relative
    if path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
        raise ValueError('Unsafe capture dex path: ' + relative)
    if not target.resolve().is_relative_to(root) or any(p.is_symlink() for p in
            [target, *target.parents] if p != root and p.is_relative_to(root)):
        raise ValueError('Capture dex symlink rejected: ' + relative)
    return target


def load_lock(path):
    data = pathlib.Path(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'),
            lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Capture dex source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Capture dex source dependency set differs')
    return lock


def read_sources(lock, raw, repository=None):
    sources = {}
    for entry in lock['files']:
        path = safe(raw, entry['path'])
        data = (subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                              check=True, capture_output=True, timeout=30).stdout
                if repository is not None else path.read_bytes())
        sources[entry['path']] = data
    return sources


def string(source, symbol):
    matches = list(re.finditer(r'const u8 ' + re.escape(symbol) + r'\[\] = _\(\s*((?:"(?:[^"\\]|\\.)*"\s*)+)\);', source))
    if len(matches) != 1:
        raise ValueError('Expected exactly one source string: ' + symbol)
    return ''.join(json.loads(s) for s in re.findall(r'"(?:[^"\\]|\\.)*"', matches[0][1]))


def reference(lock, sources, path, symbol, needle):
    return battle.reference(lock, sources, path, symbol, needle)


def footprint_indices(data):
    """Read 1-bit grayscale pixels and apply the source gbagfx inversion."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != '1' or image.size != (16, 16):
            raise ValueError('Footprint PNG profile differs')
        # Pillow exposes 1-bit pixels as 0/255; source black becomes palette index 1.
        result = bytes(1 if pixel == 0 else 0 for pixel in image.convert('L').tobytes())
    if not 0 < sum(result) < 256:
        raise ValueError('Empty footprint')
    return result


def kanto_palette(data):
    lines = data.decode('ascii').splitlines()
    if lines[:3] != ['JASC-PAL', '0100', '256'] or len(lines) != 259:
        raise ValueError('Kanto dex palette profile differs')
    values = [tuple(map(int, line.split())) for line in lines[3:]]
    if any(len(row) != 3 or any(c < 0 or c > 255 for c in row) for row in values):
        raise ValueError('Kanto dex palette value differs')
    return codec.colors(values)


def background(sources, palette):
    """Settled scale=6 DexScreen_DexPageZoomEffectFrame, no text or sprite compositing."""
    tiles = codec.tile_list(sources['graphics/pokedex/kanto_dex_bgtiles.png'])
    if len(tiles) != 32:
        raise ValueError('Kanto dex tile count differs')
    out = codec.canvas(240, 160)
    for y in range(20):
        for x in range(30):
            if y < 2 or y >= 18:
                # Header/footer Window 0/1 cover their tiles with PIXEL_FILL(15), palette bank 15.
                tile = bytes([15] * 64)
                pixels = codec.rgba(tile, palette[240:256], transparent=False)
            else:
                flip = x == 29
                edge = x in (0, 29)
                tile_id = (4 if edge else 5) if y == 2 else (10 if edge else 11) if y == 17 else (7 if edge else 8) if y == 11 else (6 if edge else 1) if y < 11 else (9 if edge else 2)
                pixels = codec.tile_rgba(tiles[tile_id], palette[:16], flip, False, False)
            codec.blit(out, 240, pixels, 8, 8, x * 8, y * 8)
    return bytes(out)


def back_art(lock, sources):
    display = battle.display_contract(lock, sources)
    sprites, outputs = [], {}
    for name, sid in NAMES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        path = f'graphics/pokemon/{stem}/back.png'
        indices = battle.pixels.indexed_png(sources[path])
        coords_path = 'src/data/pokemon_graphics/back_pic_coordinates.h'
        row = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},', sources[coords_path].decode())
        if row is None:
            raise ValueError('Captured back coordinates absent')
        width, height, offset = map(int, row.groups())
        image_symbol = 'gMonBackPic_' + name
        for source_path, needle in [('src/data/graphics/pokemon.h', f'const u32 {image_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/back.4bpp.lz");'),
                                    ('src/data/pokemon_graphics/back_pic_table.h', f'    SPECIES_SPRITE({name.upper()}, {image_symbol}),')]:
            codec.require(sources[source_path].decode(), needle)
        base = display['playerBaseCenter']
        center = dict(x=base['x'], y=base['y'] + offset)
        opaque = [i for i, index in enumerate(indices) if index]
        left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
        right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
        for variant in ('normal', 'shiny'):
            palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
            colors, words = battle.pixels.palette(sources[palette_path])
            rgba = battle.pixels.rgba_pixels(indices, colors)
            filename = f'{stem}-back-{variant}.png'
            outputs[filename] = battle.pixels.rgba_png(rgba)
            pal_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
            table = 'shiny_palette_table.h' if variant == 'shiny' else 'palette_table.h'
            macro = 'SPECIES_SHINY_PAL' if variant == 'shiny' else 'SPECIES_PAL'
            codec.require(sources['src/data/pokemon_graphics/' + table].decode(), f'    {macro}({name.upper()}, {pal_symbol}),')
            sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side='back',
                paletteVariant=variant, texturePath=filename,
                sourceImagePath=path, sourcePalettePath=palette_path,
                width=64, height=64, frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                sourceWidth=width, sourceHeight=height, sourceYOffset=offset, sourceEnemyElevation=0,
                singleBattleBaseCenter=base, singleBattleCenter=center,
                singleBattleTopLeft=dict(x=center['x'] - 32, y=center['y'] - 32),
                alphaBounds=dict(x=left, y=top, width=right-left, height=bottom-top), transparentIndex=0,
                paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False))
    outputs['capture-back-art.json'] = canonical(dict(schemaVersion=1, profile=BACK_PROFILE,
        repository='pret/pokefirered', sourceCommit=COMMIT,
        display=display, sprites=sprites, limitations=[
            'Pidgey/Rattata static player back frame only, normal and shiny. Existing front artifacts are reused unchanged.',
            'Default single-battle placement only; no send-out, hit, faint, ball, cry or timing reproduction.',
            'Local source reconstruction; no original-game frame comparison or distribution authorization.']))
    return outputs


def convert(lock, sources):
    if set(sources) != PATHS:
        raise ValueError('Capture dex source set differs')
    screen = sources['src/pokedex_screen.c'].decode()
    for needle in ['DexScreen_DrawMonDexPage(TRUE);', 'DexScreen_DexPageZoomEffectFrame(3, 6);',
            'while ((categoryName[index] != CHAR_SPACE) && (index < 11))',
            'LoadMonPicInWindow(species, SHINY_ODDS, DexScreen_GetDefaultPersonality(species), TRUE, paletteOffset >> 4, windowId);',
            'inches = 10000 * height / 254;', 'lbs = (weight * 100000) / 4536;',
            'printerTemplate.shadowColor = 2;', '236 - GetStringWidth(FONT_SMALL, src, 0)']:
        codec.require(screen, needle)
    codec.require(sources['src/data/pokemon/pokedex_text.h'].decode(), '#if defined(FIRERED)\n#include "pokedex_text_fr.h"')
    codec.require(sources['src/text.c'].decode(), 'if (textPrinter->japanese)\n                textPrinter->printerTemplate.currentX += (gGlyphInfo.width + textPrinter->printerTemplate.letterSpacing);\n            else\n                textPrinter->printerTemplate.currentX += gGlyphInfo.width;')
    font_info = sources['src/new_menu_helpers.c'].decode()
    font = re.search(r'\.fontFunction = FontFunc_Normal,\s*\.maxLetterWidth = (\d+),\s*\.maxLetterHeight = (\d+),', font_info)
    if font is None or int(font[2]) != 14:
        raise ValueError('Normal font line height differs')
    codec.require(sources['tools/gbagfx/main.c'].decode(), 'WriteTileImage(outputPath, options->numTilesMode, options->numTiles, options->metatileWidth, options->metatileHeight, &image, !image.hasPalette);')
    palette = kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])
    outputs, assets = back_art(lock, sources), []

    def asset(id, w, h, pixels, x, y):
        path = id + '.png'
        outputs[path] = codec.png(w, h, pixels)
        assets.append(dict(id=id, texturePath=path, width=w, height=h, x=x, y=y))

    asset('capture-dex-background', 240, 160, background(sources, palette), 0, 0)
    entries = {}
    dex = sources['src/data/pokemon/pokedex_entries.h'].decode()
    names = sources['src/data/text/species_names.h'].decode()
    descriptions = sources['src/data/pokemon/pokedex_text_fr.h'].decode()
    constants = sources['include/constants/pokedex.h'].decode().split('// National Pokedex order', 1)[1].split('};', 1)[0]
    national = re.findall(r'^\s*(NATIONAL_DEX_\w+),', constants, re.M)
    texts = sources['src/strings.c'].decode()
    for name, sid in NAMES:
        stem, symbol, dex_symbol = name.lower(), 'SPECIES_' + name.upper(), 'NATIONAL_DEX_' + name.upper()
        if not re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'$', sources['include/constants/species.h'].decode(), re.M):
            raise ValueError('Species ID differs')
        match = re.search(r'    \[' + dex_symbol + r'\] =\s*\{(.*?)\n    \},', dex, re.S)
        if match is None:
            raise ValueError('Dex entry absent')
        body = match[1]
        category = json.loads(re.search(r'\.categoryName = _\(("[^"]+")\)', body)[1])
        height = int(re.search(r'\.height = (\d+)', body)[1])
        weight = int(re.search(r'\.weight = (\d+)', body)[1])
        desc_symbol = re.search(r'\.description = (\w+),', body)[1]
        english_name = json.loads(re.search(r'\[' + symbol + r'\]\s*= _\(("[^"]+")\)', names)[1])
        habitat_symbol = 'gText_DexCategory_' + ('Forest' if sid == 16 else 'Grassland') + 'Pkmn'
        habitat_array = 'sDexCategory_' + ('ForestPkmn_Page5' if sid == 16 else 'GrasslandPkmn_Page1')
        habitat_source = sources['src/data/pokemon/pokedex_categories.h'].decode()
        habitat = re.search(r'static const u16 ' + habitat_array + r'\[\] = \{(.*?)\};', habitat_source, re.S)
        if habitat is None or symbol + ',' not in habitat[1]:
            raise ValueError('Habitat category differs')
        inches = 10000 * height // 254
        inches += 10 if inches % 10 >= 5 else 0
        feet, remainder = inches // 120, (inches % 120) // 10
        lbs = weight * 100000 // 4536
        lbs += 10 if lbs % 10 >= 5 else 0
        footprint_path = f'graphics/pokemon/{stem}/footprint.png'
        codec.require(sources['src/data/pokemon_graphics/footprint_table.h'].decode(), f'[{symbol}] = gMonFootprint_{name},')
        asset(stem + '-footprint', 16, 16, codec.rgba(footprint_indices(sources[footprint_path]), palette[:16]), 104, 64)
        entries[str(sid)] = dict(sourceSpeciesId=sid, nationalNumber=national.index(dex_symbol), name=english_name,
            category=category, displayCategory=category.split(' ')[0], heightDecimeters=height, weightHectograms=weight,
            heightDisplay=f'{feet}’{remainder:02}”', weightDisplay=f'{lbs//100}.{(lbs//10)%10} {string(texts, "gText_Lbs")}',
            description=string(descriptions, desc_symbol), footprintPath=stem+'-footprint.png',
            frontSpritePath='route1-battle-art/'+stem+'-front-normal.png', habitatTitle=string(texts, habitat_symbol))
    layout = dict(width=240, height=160, frontX=152, frontY=24, footprintX=104, footprintY=64,
        numberX=16, numberY=32, nameX=44, nameY=32, categoryX=16, categoryY=48,
        heightX=16, heightY=60, weightX=16, weightY=72, measureValueOffsetX=30,
        descriptionY=96, descriptionCenterWidth=240, descriptionFont='normal', descriptionLetterSpacing=0,
        sourceDescriptionLetterSpacing=1, sourceNameLetterSpacing=1,
        descriptionLineSpacing=0, descriptionLineHeight=int(font[2]), descriptionShadowColor=list(palette[2]), statsShadowColor=list(palette[3]),
        controlForegroundColor=list(palette[241]), controlShadowColor=list(palette[242]),
        foregroundColor=list(palette[1]), headerY=2, headerCenterWidth=240, footerY=146, footerRightX=236,
        titleFont='normal', statsFont='small', nameFont='normal', footerFont='small',
        descriptionCenterMeasureLetterSpacing=0, statsLetterSpacing=0, nameLetterSpacing=0,
        numberPrefix=string(texts, 'gText_PokedexNo'), categorySuffix=string(texts, 'gText_PokedexPokemon'),
        heightLabel=string(texts, 'gText_HT'), weightLabel=string(texts, 'gText_WT'), footerText=string(texts, 'gText_Next'))
    outputs['capture-dex.json'] = runtime_json_bytes(dict(schemaVersion=1, entries=entries, assets=assets, layout=layout), sort_keys=True)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def run(lock_path, raw, out, repository=None):
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    targets = [(safe(out, p), b) for p, b in outputs.items()]
    cached = [(safe(raw, p), b) for p, b in sources.items()] if repository is not None else []
    for target, data in cached + targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return outputs


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--repository', type=pathlib.Path)
    p.add_argument('--raw', type=pathlib.Path, default=ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--out', type=pathlib.Path, default=ROOT/'content/generated/sprites/pokemon/capture-dex')
    p.add_argument('--lock', type=pathlib.Path, default=ROOT/'scripts/recipes/pokemon/capture-dex.lock.json')
    a = p.parse_args()
    outputs = run(a.lock, a.raw, a.out, a.repository)
    print(json.dumps(dict(status='verified', sourceFiles=len(PATHS), outputs=len(outputs))))


if __name__ == '__main__':
    main()
