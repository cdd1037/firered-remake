"""Stateless Pokemon art conversion and FireRed learnset parsing.

Each region supplies its own species, profile and scope. Source image decoding
uses Pillow; all PNG output goes through the shared exact image writer.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import io
import re
from PIL import Image
from image_io import indexed_png, png
from pokemon_import_io import canonical



def source_symbol(name):
    return {'NidoranF': 'NIDORAN_F', 'NidoranM': 'NIDORAN_M'}.get(name, name.upper())


def source_stem(name):
    return source_symbol(name).lower()



def battle_indices(data):
    """Read source indices directly; external GBA palettes provide output colors."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != 'P' or image.size != (64, 64):
            raise ValueError('Expected a 64x64 indexed PNG')
        if len(image.getpalette('RGB')) != 48 or image.info.get('transparency') != 0:
            raise ValueError('Expected sixteen colors and transparent palette index zero')
        indices = image.tobytes()
        if any(index > 15 for index in indices):
            raise ValueError('Source palette index exceeds four bits')
        return indices


def battle_palette(data):
    lines = data.decode('ascii').splitlines()
    if len(lines) != 19 or lines[:3] != ['JASC-PAL', '0100', '16']:
        raise ValueError('Expected a sixteen-color JASC-PAL 0100 palette')
    colors, words = [], []
    for line in lines[3:]:
        if not re.fullmatch(r'[0-9]+ [0-9]+ [0-9]+', line):
            raise ValueError('Invalid JASC RGB row')
        rgb = [int(channel) for channel in line.split(' ')]
        if any(channel > 255 for channel in rgb):
            raise ValueError('Palette channel outside 0..255')
        five = [channel // 8 for channel in rgb]
        words.append(five[0] | (five[1] << 5) | (five[2] << 10))
        # gbagfx WriteGbaPalette then ReadGbaPalette's exact integer mapping.
        colors.append(tuple(channel * 255 // 31 for channel in five))
    return colors, words


def rgba_pixels(indices, colors):
    if len(indices) != 4096 or len(colors) != 16 or any(index > 15 for index in indices):
        raise ValueError('Unsupported pixel/palette profile')
    return bytes(channel for index in indices for channel in (*colors[index], 0 if index == 0 else 255))


def icon_palette(data):
    lines=data.decode('ascii').splitlines()
    if lines[:2]!=['JASC-PAL','0100'] or len(lines)!=int(lines[2])+3 or int(lines[2]) not in (16,48):raise ValueError('Unsupported palette')
    rgb=[]
    for line in lines[3:]:
        if not re.fullmatch(r'\d+ \d+ \d+',line):raise ValueError('Malformed palette')
        row=tuple(map(int,line.split()))
        if any(c>255 for c in row):raise ValueError('Palette range differs')
        rgb.append(row)
    return expand_palette(rgb)


def rgba(indices,pal,transparent=True):return bytes(c for i in indices for c in (*pal[i],0 if i==0 and transparent else 255))


def expand_palette(rgb):
    return [tuple(((c // 8) << 3) | ((c // 8) >> 2) for c in row) for row in rgb]


def footprint_indices(data):
    """Read 1-bit grayscale pixels and apply the source gbagfx inversion."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != '1' or image.size != (16, 16):
            raise ValueError('Footprint PNG profile differs')
        # Pillow exposes 1-bit pixels as 0/255; source black becomes palette index 1.
        result = bytes(1 if pixel == 0 else 0 for pixel in image.convert('L').tobytes())
    return result


def kanto_palette(data):
    lines = data.decode('ascii').splitlines()
    if lines[:3] != ['JASC-PAL', '0100', '256'] or len(lines) != 259:
        raise ValueError('Kanto dex palette profile differs')
    values = [tuple(map(int, line.split())) for line in lines[3:]]
    if any(len(row) != 3 or any(c < 0 or c > 255 for c in row) for row in values):
        raise ValueError('Kanto dex palette value differs')
    return expand_palette(values)


def reference(lock, sources, path, symbol, needle=None):
    result = dict(kind='pret', repository=lock['repository'], commit=lock['commit'], path=path,
                  symbol=symbol,
                  url=f"https://github.com/{lock['repository']}/blob/{lock['commit']}/{path}")
    if needle is not None:
        text = sources[path].decode('utf-8')
        if text.count(needle) != 1:
            raise ValueError('Missing/ambiguous source binding: ' + symbol)
        result['startLine'] = text.count('\n', 0, text.index(needle)) + 1
        result['endLine'] = result['startLine'] + needle.count('\n')
        result['url'] += f'#L{result["startLine"]}-L{result["endLine"]}'
    return result


def section(text, signature):
    if text.count(signature) != 1:
        raise ValueError('Missing/ambiguous source function: ' + signature)
    start = text.index(signature)
    return text[start:text.index('\n}', start) + 2]


def display_contract(lock, sources):
    path = 'src/battle_anim_mons.c'
    source = sources[path].decode()
    coords = section(source, 'static const struct UCoords8 sBattlerCoords[][MAX_BATTLERS_COUNT] =')
    single = re.search(r'\{ // Single battle\s*((?:\{ \d+, \d+ \},\s*){4})\}', coords)
    if single is None:
        raise ValueError('Unsupported single-battle coordinates')
    points = [tuple(map(int, pair)) for pair in re.findall(r'\{ (\d+), (\d+) \}', single.group(1))]
    constants = sources['include/constants/battle.h'].decode()
    for symbol, index in [('B_POSITION_PLAYER_LEFT', 0), ('B_POSITION_OPPONENT_LEFT', 1)]:
        if re.search(r'^#define ' + symbol + r'\s+' + str(index) + r'$', constants, re.M) is None:
            raise ValueError('Unsupported battler position mapping')
    default = section(source, 'u8 GetBattlerSpriteDefault_Y(u8 battlerId)\n')
    getter = section(source, 'u8 GetBattlerSpriteCoord(u8 battlerId, u8 coordType)\n')
    delta = section(source, 'static u8 GetBattlerYDelta(u8 battlerId, u16 species)\n')
    final = section(source, 'static u8 GetBattlerSpriteFinal_Y(u8 battlerId, u16 species, bool8 a3)\n')
    elevation = section(source, 'static u8 GetBattlerElevation(u8 battlerId, u16 species)\n')
    expected = [(default, 'return GetBattlerSpriteCoord(battlerId, BATTLER_COORD_Y_PIC_OFFSET_DEFAULT);'),
                (getter, 'retVal = GetBattlerSpriteFinal_Y(battlerId, species, FALSE);'),
                (delta, 'ret = gMonBackPicCoords[species].y_offset;'),
                (delta, 'ret = gMonFrontPicCoords[species].y_offset;'),
                (final, 'offset -= GetBattlerElevation(battlerId, species);'),
                (final, 'y = offset + sBattlerCoords[IS_DOUBLE_BATTLE()][GetBattlerPosition(battlerId)].y;'),
                (elevation, 'ret = gEnemyMonElevation[species];')]
    if any(needle not in text for text, needle in expected):
        raise ValueError('Unsupported battle-coordinate semantics')
    for controller in ('player', 'opponent'):
        cpath = f'src/battle_controller_{controller}.c'
        text = section(sources[cpath].decode(), 'static void StartSendOutAnim(u8 battlerId, bool8 dontClearSubstituteBit)\n')
        if 'GetBattlerSpriteCoord(battlerId, BATTLER_COORD_X_2)' not in text or 'GetBattlerSpriteDefault_Y(battlerId)' not in text:
            raise ValueError('Unsupported send-out coordinate binding')
    return dict(logicalWidth=240, logicalHeight=160, frameWidth=64, frameHeight=64, playerBaseCenter={'x': points[0][0], 'y': points[0][1]}, opponentBaseCenter={'x': points[1][0], 'y': points[1][1]}, playerSide='back', opponentSide='front', textureFilter='nearest', flipH=False, frameIndex=0, applySourceYOffset=True, applyAnimationTargetExtra8=False, centerFormula='baseCenterY + sourceYOffset - sourceEnemyElevation')


def local_reference(lock, sources, path, symbol, needle=None):
    result = dict(path=path, symbol=symbol)
    if needle is not None:
        source = sources[path].decode('utf-8')
        if source.count(needle) != 1:
            raise ValueError('Expected one source anchor: ' + symbol)
        result.update(startLine=source.count('\n', 0, source.index(needle)) + 1,
                      endLine=source.count('\n', 0, source.index(needle) + len(needle)) + 1)
    return result


def battle_art(lock, sources, *, profile, species, limitation):
    display = display_contract(lock, sources)
    outputs, sprites = {}, []
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    elevation_source = section(sources[elevation_path].decode('utf-8'), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
    body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
    if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', body):
        raise ValueError('Unsupported enemy elevation table')
    for name, sid in species:
        stem, symbol = source_stem(name), 'SPECIES_' + source_symbol(name)
        row = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', elevation_source, re.M)
        front_elevation = int(row[1]) if row else 0
        for side in ('front', 'back'):
            image_path = f'graphics/pokemon/{stem}/{side}.png'
            coords_path = f'src/data/pokemon_graphics/{side}_pic_coordinates.h'
            coords = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},', sources[coords_path].decode('utf-8'))
            if coords is None:
                raise ValueError('Missing source sprite coordinates')
            width, height, offset = map(int, coords.groups())
            indices = battle_indices(sources[image_path])
            opaque = [i for i, index in enumerate(indices) if index]
            if not opaque:
                raise ValueError('Empty sprite')
            left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
            right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
            base = display['playerBaseCenter' if side == 'back' else 'opponentBaseCenter']
            elevation = 0 if side == 'back' else front_elevation
            center = dict(x=base['x'], y=base['y'] + offset - elevation)
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle_palette(sources[palette_path])
                rgba = rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}'
                outputs[filename + '.png'] = png(64, 64, rgba)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side, paletteVariant=variant, texturePath=filename + '.png', sourceImagePath=image_path, sourcePalettePath=palette_path, width=64, height=64, frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0, sourceWidth=width, sourceHeight=height, sourceYOffset=offset, sourceEnemyElevation=elevation, singleBattleBaseCenter=base, singleBattleCenter=center, singleBattleTopLeft=dict(x=center['x'] - 32, y=center['y'] - 32), alphaBounds=dict(x=left, y=top, width=right - left, height=bottom - top), transparentIndex=0, paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False))
    outputs['battle-art.json'] = canonical(dict(schemaVersion=1, display=display, sprites=sprites))
    return outputs


def supplement(sources, *, species):
    """Only source icons and footprints; shared dex/audio own metadata and cries."""
    icon_text = sources['src/pokemon_icon.c'].decode()
    palette_section = icon_text.split('const u8 gMonIconPaletteIndices[] = {', 1)[1].split('};', 1)[0]
    icon_section = icon_text.split('const u8 *const gMonIconTable[] = {', 1)[1].split('};', 1)[0]
    foot_palette = kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])
    graphics = sources['src/data/graphics/pokemon.h'].decode()
    foot_table = sources['src/data/pokemon_graphics/footprint_table.h'].decode()
    outputs, icons, assets = {}, [], []
    for name, sid in species:
        stem, symbol = source_stem(name), 'SPECIES_' + source_symbol(name)
        image_path = f'graphics/pokemon/{stem}/icon.png'
        m = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', palette_section, re.M)
        binding = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_section, re.M)
        if m is None or binding is None or int(m[1]) not in range(3): raise ValueError('Missing source icon binding')
        declaration = re.search(r'const u8\s+gMonIcon_' + name + r'\[\]\s*=\s*INCBIN_U8\(\s*"graphics/pokemon/' + stem + r'/icon\.4bpp"\);', graphics)
        if declaration is None: raise ValueError('Missing source icon declaration')
        index = int(m[1])
        palette_path = f'graphics/pokemon/icon_palettes/icon_palette_{index}.pal'
        width, height, pixels, _ = indexed_png(sources[image_path])
        if (width, height) != (32, 64): raise ValueError('Unexpected source icon dimensions')
        pixels = pixels[:1024]
        colors = icon_palette(sources[palette_path])
        icon_pixels = rgba(pixels, colors)
        path = f'icons/{stem}'
        outputs[path + '.png'] = png(32, 32, icon_pixels)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, texturePath=path + '.png', width=32, height=32, sourceFrameIndex=0, sourceFrameCount=2, paletteIndex=index, paletteVariant='shared-normal-shiny', transparentIndex=0, paletteRgb555=[r >> 3 | g >> 3 << 5 | b >> 3 << 10 for r, g, b in colors]))
        foot_path = f'graphics/pokemon/{stem}/footprint.png'
        if f'[{symbol}] = gMonFootprint_{name},' not in foot_table or f'const u8 gMonFootprint_{name}[] = INCBIN_U8("graphics/pokemon/{stem}/footprint.1bpp");' not in graphics:
            raise ValueError('Missing source footprint binding')
        foot = rgba(footprint_indices(sources[foot_path]), foot_palette[:16])
        path = stem + '-footprint'
        outputs[path + '.png'] = png(16, 16, foot)
        assets.append(dict(sourceSpeciesId=sid, texturePath=path + '.png', width=16, height=16))
    outputs['supplement.json'] = canonical(dict(schemaVersion=1, dexSpeciesIds=[sid for _, sid in species], icons=icons, footprints=assets))
    return outputs


def learn_rows(text, learn):
    """Select the source FIRERED branch without executing or flattening original references."""
    offset = learn.start(1)
    active, conditional, ended, result = True, False, False, []
    for line in learn[1].splitlines(keepends=True):
        if line == '#if defined(FIRERED)\n':
            if conditional: raise ValueError('Nested learnset preprocessor is unsupported')
            conditional, active = True, True
        elif line == '#elif defined(LEAFGREEN)\n':
            if not conditional or not active: raise ValueError('Unexpected learnset branch')
            active = False
        elif line == '#endif\n':
            if not conditional: raise ValueError('Unexpected learnset endif')
            conditional, active = False, True
        elif line == '    LEVEL_UP_END\n':
            if ended or conditional or not active: raise ValueError('Unexpected learnset terminator')
            ended = True
        elif not line.strip():
            pass  # Offset-preserving edition selection leaves blank source rows.
        else:
            m = re.compile(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),\n').match(text, offset, offset + len(line))
            if m is None or m.end() != offset + len(line) or ended: raise ValueError('Unsupported or incomplete source learnset line')
            if active: result.append(m)
        offset += len(line)
    if conditional or not ended or not result: raise ValueError('Incomplete FireRed learnset')
    return result
