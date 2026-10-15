#!/usr/bin/env python3
"""Pinned Red/Green normal walking sheets, read only from local Git or locked cache.

No upstream programs, ROMs, downloads, game rules or running frames are used.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse
import io
import json
import pathlib
import re
import subprocess

from PIL import Image
from image_io import png

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-player-gender-art-v1'
OBJECT_ROOT = 'src/data/object_events/'
PALETTE_PATH = 'graphics/object_events/palettes/player.pal'
PATHS = frozenset([
    'include/constants/global.h', 'include/constants/event_objects.h',
    'src/field_player_avatar.c', 'src/event_object_movement.c', 'tools/gbagfx/gfx.c',
    PALETTE_PATH, 'graphics/object_events/pics/people/red_normal.png',
    'graphics/object_events/pics/people/green_normal.png',
    *[OBJECT_ROOT + name + '.h' for name in ('object_event_graphics', 'object_event_graphics_info',
        'object_event_graphics_info_pointers', 'object_event_pic_tables', 'object_event_anims')]])


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is not None:
        path = pathlib.PurePosixPath(relative)
        require(bool(relative) and relative != '.' and not path.is_absolute() and '..' not in path.parts
                and '\\' not in relative and str(path) == relative, 'Unsafe player art path')
    target = root if relative is None else root / relative
    require(not any(p.is_symlink() for p in [target, *target.parents]), 'Player art symlink rejected')
    require(target.resolve().is_relative_to(root.resolve()), 'Player art path escapes root')
    return target


def load_lock(path=None):
    data = safe(path or ROOT / 'scripts/import-config/player-gender-art.json').read_bytes()
    lock = json.loads(data)
    require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], lock['gameEdition'], lock['romRevision']) == (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0), 'Player art source identity differs')
    require(len(lock['files']) == len(PATHS) and {e['path'] for e in lock['files']} == PATHS,
            'Player art dependency set differs')
    return lock


def read_sources(lock, raw, repository):
    if repository is not None:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode().strip()
    sources = {}
    for entry in lock['files']:
        path = entry['path']
        if repository is None:
            data = safe(raw, path).read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + path],
                                  check=True, capture_output=True, timeout=30).stdout
        sources[path] = data
    return sources


def indexed_png(data):
    """Read the source sheet's palette indices without color conversion."""
    with Image.open(io.BytesIO(data)) as image:
        require(image.format == 'PNG' and image.mode == 'P' and image.size == (144, 32),
                'Source PNG geometry/format differs')
        require(len(image.getpalette('RGB')) == 48 and image.info.get('transparency') == 0,
                'Source PNG palette differs')
        indices = image.tobytes()
        require(all(index < 16 for index in indices), 'Source palette index exceeds four bits')
        return indices


def palette(data):
    lines = data.decode('ascii').splitlines()
    require(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Player palette profile differs')
    colors, words = [], []
    for line in lines[3:]:
        require(re.fullmatch(r'\d+ \d+ \d+', line) is not None, 'Player palette RGB differs')
        rgb = [int(c) for c in line.split()]
        require(all(c <= 255 for c in rgb), 'Player palette channel exceeds 255')
        five = [c // 8 for c in rgb]
        words.append(five[0] | five[1] << 5 | five[2] << 10)
        colors.append(tuple(c * 255 // 31 for c in five))
    return colors, words


def rgba_png(rgba):
    require(len(rgba) == 144 * 32 * 4, 'Player RGBA length differs')
    return png(144, 32, rgba)


def block(text, symbol):
    matches = re.findall(r'\b' + re.escape(symbol) + r'(?:\[[^\n]*?\])?\s*=\s*\{(.*?)\n\};', text, re.S)
    require(len(matches) == 1, 'Missing/ambiguous player art declaration: ' + symbol)
    return matches[0]


def convert( sources):
    text = {p: b.decode() for p, b in sources.items() if not p.endswith('.png')}
    def ref(path, symbol):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path,
                    symbol=symbol)
    def has(path, pattern):
        require(re.search(pattern, text[path], re.M) is not None, 'Player source binding differs: ' + pattern)
    for name, value in [('MALE', 0), ('FEMALE', 1), ('GENDER_COUNT', 2)]:
        has('include/constants/global.h', r'^#define\s+' + name + r'\s+' + str(value) + r'\s*$')
    has('src/field_player_avatar.c', r'\[PLAYER_AVATAR_GFX_NORMAL\]\s*=\s*\{OBJ_EVENT_GFX_RED_NORMAL,\s*OBJ_EVENT_GFX_GREEN_NORMAL\}')
    has(OBJECT_ROOT + 'object_event_graphics.h', r'gObjectEventPal_Player\[\] = INCBIN_U16\("graphics/object_events/palettes/player.gbapal"\)')
    has('tools/gbagfx/gfx.c', r'#define DOWNCONVERT_BIT_DEPTH\(x\) \(\(x\) / 8\)')
    has('tools/gbagfx/gfx.c', r'#define UPCONVERT_BIT_DEPTH\(x\) \(\(\(x\) \* 255\) / 31\)')
    has('src/event_object_movement.c', r'sprite->centerToCornerVecX = -\(graphicsInfo->width >> 1\);\s*'
        r'sprite->centerToCornerVecY = -\(graphicsInfo->height >> 1\);\s*'
        r'sprite->x \+= 8;\s*sprite->y \+= 16 \+ sprite->centerToCornerVecY;')
    animations = text[OBJECT_ROOT + 'object_event_anims.h']
    animation_table = block(animations, 'sAnimTable_RedGreenNormal')
    directions = {}
    for direction, stand, walk in [('south', 0, [3, 0, 4, 0]), ('north', 1, [5, 1, 6, 1]),
                                    ('west', 2, [7, 2, 8, 2]), ('east', 2, [7, 2, 8, 2])]:
        flip = direction == 'east'
        for prefix, enum, frames, ticks in [('Face', 'FACE', [stand], 16), ('Go', 'GO', walk, 8)]:
            symbol = 'sAnim_' + prefix + direction.title()
            require(re.search(r'\[ANIM_STD_' + enum + '_' + direction.upper() + r'\]\s*=\s*' + symbol + ',', animation_table),
                    'Player animation table binding differs')
            commands = block(animations, symbol)
            expected = ''.join(f'ANIMCMD_FRAME({frame},{ticks}' + (',.hFlip=TRUE' if flip else '') + '),' for frame in frames) + 'ANIMCMD_JUMP(0),'
            require(re.sub(r'\s+', '', commands) == expected, 'Player standing/walking animation differs')
        directions[direction] = dict(stand=stand, walk=walk, flipH=flip)
    colors, words = palette(sources[PALETTE_PATH])
    outputs, assets = {}, []
    for gender, name, gfx_id in [(0, 'Red', 0), (1, 'Green', 7)]:
        stem, symbol = name.lower(), name + 'Normal'
        graphics_id = 'OBJ_EVENT_GFX_' + name.upper() + '_NORMAL'
        image_path = f'graphics/object_events/pics/people/{stem}_normal.png'
        has('include/constants/event_objects.h', r'^#define ' + graphics_id + r' ' + str(gfx_id) + r'$')
        has(OBJECT_ROOT + 'object_event_graphics_info_pointers.h', r'\[' + graphics_id + r'\]\s*=\s*&gObjectEventGraphicsInfo_' + symbol + ',')
        info = block(text[OBJECT_ROOT + 'object_event_graphics_info.h'], 'gObjectEventGraphicsInfo_' + symbol)
        fields = dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)', info))
        palette_tag = 'OBJ_EVENT_PAL_TAG_PLAYER_' + name.upper()
        expected = dict(width='16', height='32', paletteTag=palette_tag, paletteSlot='PALSLOT_PLAYER',
                        anims='sAnimTable_RedGreenNormal', images='sPicTable_' + symbol, inanimate='FALSE')
        require(all(fields.get(k) == v for k, v in expected.items()), 'Player graphics info differs')
        has('src/event_object_movement.c', r'\{gObjectEventPal_Player,\s*' + palette_tag + r'\}')
        has(OBJECT_ROOT + 'object_event_graphics.h', r'gObjectEventPic_' + symbol + r'\[\] = INCBIN_U16\("' + image_path.replace('.png', '.4bpp') + r'"\)')
        table = block(text[OBJECT_ROOT + 'object_event_pic_tables.h'], 'sPicTable_' + symbol)
        frames = re.findall(r'overworld_frame\((\w+), (\d+), (\d+), (\d+)\)', table)
        require(frames[:9] == [('gObjectEventPic_' + symbol, '2', '4', str(i)) for i in range(9)]
                and len(frames) == 20, 'Player normal frame table differs')
        indices = indexed_png(sources[image_path])
        rgba = bytes(c for i in indices for c in (*colors[i], 0 if i == 0 else 255))
        filename = 'player-' + ('male' if gender == 0 else 'female') + '.png'
        outputs[filename] = rgba_png(rgba)
        assets.append(dict(gender=gender, genderSymbol='MALE' if gender == 0 else 'FEMALE', graphicsId=graphics_id,
            sourceGraphicsId=gfx_id, texturePath=filename,  width=144, height=32, frameWidth=16, frameHeight=32, frameCount=9,
            frameLayout='horizontal', sourceFrameIndices=list(range(9)), sourceImagePath=image_path,
            sourcePalettePath=PALETTE_PATH, paletteTag=palette_tag, paletteSlot='PALSLOT_PLAYER', paletteRgb555=words,
            transparentIndex=0, directions=directions, tileCenterAnchor=dict(x=8, y=24),
            drawOffsetFromTileOrigin=dict(x=0, y=-16), walkFrameDurationSourceFrames=8, sourceTimingHz=60,
            sourceRefs=[ref(image_path, 'gObjectEventPic_' + symbol), ref(PALETTE_PATH, 'gObjectEventPal_Player'),
                ref(OBJECT_ROOT + 'object_event_graphics.h', 'gObjectEventPic_' + symbol),
                ref(OBJECT_ROOT + 'object_event_graphics_info.h', 'gObjectEventGraphicsInfo_' + symbol),
                ref(OBJECT_ROOT + 'object_event_graphics_info_pointers.h', graphics_id),
                ref(OBJECT_ROOT + 'object_event_pic_tables.h', 'sPicTable_' + symbol),
                ref(OBJECT_ROOT + 'object_event_anims.h', 'sAnimTable_RedGreenNormal'),
                ref('src/event_object_movement.c', 'sObjectEventSpritePalettes'),
                ref('src/field_player_avatar.c', 'sPlayerAvatarGfxIds'),
                ref('include/constants/global.h', 'MALE' if gender == 0 else 'FEMALE'),
                ref('include/constants/event_objects.h', graphics_id), ref('tools/gbagfx/gfx.c', 'UPCONVERT_BIT_DEPTH')]))
    outputs['player-gender-art.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         paletteExpansion='floor(rgb5 * 255 / 31)', assets=assets,
        limitations=['Standing and ordinary walking only. Running/surfing frames 9-19 are deliberately excluded.',
                    'No cycling, surfing, fishing, reflections, field-action frames or trainer portraits.']))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         inputCount=len(PATHS),
        upstreamScriptsExecuted=False, romDownloaded=False,
        outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, output, repository=None):
    raw, output = safe(raw), safe(output)
    require(not (raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve())),
            'Player raw/output paths overlap')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        require(not any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw, output)),
                'Player materialization overlaps reference repository')
    lock = load_lock()
    sources = read_sources(lock, raw, repository)
    outputs = convert( sources)
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        pass
    material.update({(output, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    require(not any(p.exists() and not p.is_file() for target, stage, _ in targets for p in (target, stage)),
            'Player art target is not a regular file')
    require(not any(p.exists() and not p.is_dir() for target, _, _ in targets for p in target.parents),
            'Player art ancestor is not a directory')
    require(not any(stage.exists() for _, stage, _ in targets), 'Player art staging path already exists')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        with stage.open('xb') as stream:
            stream.write(data)
        stage.replace(target)
    return dict(profile=PROFILE, sourceCount=len(sources), outputCount=len(outputs))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--offline', action='store_true', help='Use only the authenticated existing raw cache')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/player-gender-art')
    args = parser.parse_args()
    require(not (args.offline and args.reference_repo), 'Choose either local Git or offline cache')
    repository = args.reference_repo
    if repository is None and not args.offline:
        repository = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.output, repository), sort_keys=True))


if __name__ == '__main__':
    main()
