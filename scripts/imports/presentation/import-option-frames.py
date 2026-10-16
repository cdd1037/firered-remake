#!/usr/bin/env python3
"""Authenticate and convert FireRed's ten user window frames from pinned local data.

Uses this project's existing, independently tested lab UI pixel codec. Reads only
local immutable Git objects or authenticated raw files; never runs upstream code.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


from presentation_atlas import pack
from PIL import Image
import argparse
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-option-frames-v1'
PATHS = frozenset('''Makefile
tools/gbagfx/gfx.c
tools/gbagfx/convert_png.c
src/text_window.c
src/text_window_graphics.c
src/new_menu_helpers.c
src/battle_bg.c
src/option_menu.c
src/start_menu.c
src/script_menu.c
src/player_pc.c
graphics/battle_interface/textbox.png
graphics/battle_interface/textbox.bin
graphics/battle_interface/textbox1.pal'''.splitlines()) | frozenset(
    f'graphics/text_window/type{i}.png' for i in range(1, 11))
import ui_tiles as pixels


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    target = root
    if relative is not None:
        path = pathlib.PurePosixPath(relative)
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe option-frame path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Option-frame symlink or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Option-frame path escapes root')
    return target.resolve()


def load_lock(path=None):
    data = safe(path or ROOT / 'scripts/recipes/source/fetch-option-frames.lock.json').read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Option-frame source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Option-frame dependency set differs')
    return lock


def read_sources(lock, raw, repository=None):
    safe(raw)
    if repository is not None:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
    result = {}
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        # Corrupt owned raw inputs are never silently repaired by --source.
        if target.exists() or repository is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        result[entry['path']] = data
    return result


def validate_semantics(sources):
    def require(path, needle):
        pixels.require(sources[path].decode('utf-8'), needle)
    source = sources['src/text_window_graphics.c'].decode('utf-8')
    table = re.search(r'const struct TextWindowGraphics gUserFrames\[\] = \{(.*?)\n\};', source, re.S)
    expected = [(str(i), str(i)) for i in range(1, 11)]
    if table is None or re.findall(r'\{sUserFrame_Type(\d+)_Gfx,\s+sUserFrame_Type(\d+)_Pal\}', table[1]) != expected:
        raise ValueError('Original ten user frames/order differ')
    for i in range(1, 11):
        require('src/text_window_graphics.c', f'INCBIN_U16("graphics/text_window/type{i}.4bpp")')
        require('src/text_window_graphics.c', f'INCBIN_U16("graphics/text_window/type{i}.gbapal")')
    for path, needle in [
        ('src/text_window.c', 'LoadUserWindowGfxByFrame(windowId, gSaveBlock2Ptr->optionsWindowFrameType, destOffset, palOffset);'),
        ('src/text_window.c', 'LoadBgTiles(GetWindowAttribute(windowId, WINDOW_BG), gUserFrames[frameType].tiles, 0x120, destOffset);'),
        ('src/text_window.c', 'LoadPalette(gUserFrames[frameType].palette, palOffset, PLTT_SIZE_4BPP);'),
        ('src/text_window.c', 'LoadBgTiles(GetWindowAttribute(windowId, WINDOW_BG), gStdTextWindow_Gfx, 0x120, destOffset);'),
        ('src/text_window.c', 'LoadBgTiles(GetWindowAttribute(windowId, WINDOW_BG), gMenuMessageWindow_Gfx, 0x280, destOffset);'),
        ('src/battle_bg.c', 'LoadUserWindowGfx(2, 0x012, BG_PLTT_ID(1));'),
        ('src/battle_bg.c', 'LoadUserWindowGfx(2, 0x022, BG_PLTT_ID(1));'),
        ('src/new_menu_helpers.c', 'LoadUserWindowGfx(0, STD_WINDOW_BASE_TILE_NUM, BG_PLTT_ID(STD_WINDOW_PALETTE_NUM));'),
        ('src/new_menu_helpers.c', 'LoadMenuMessageWindowGfx(0, DLG_WINDOW_BASE_TILE_NUM, BG_PLTT_ID(DLG_WINDOW_PALETTE_NUM));'),
        ('src/start_menu.c', 'LoadStdWindowFrameGfx();\n        DrawStdWindowFrame(CreateStartMenuWindow(sNumStartMenuItems), FALSE);'),
        ('src/start_menu.c', 'LoadStdWindowGfx(sSaveStatsWindowId, 0x21D, BG_PLTT_ID(13));'),
        ('src/script_menu.c', 'SetStdWindowBorderStyle(windowId, FALSE);'),
        ('src/player_pc.c', 'SetStdWindowBorderStyle(tWindowId, 0);'),
        ('src/option_menu.c', 'GetUserWindowGraphics(sOptionMenuPtr->option[MENUITEM_FRAMETYPE])->tiles'),
        ('src/option_menu.c', 'LoadStdWindowGfxOnBg(1, 0x1B3, BG_PLTT_ID(3));'),
        ('tools/gbagfx/convert_png.c', '% (1 << destBitDepth)'),
    ]:
        require(path, needle)


def convert(lock, sources):
    if set(sources) != PATHS:
        raise ValueError('Option-frame source inventory differs')
    validate_semantics(sources)
    assets, outputs, images = [], {}, []

    def add(index, kind, width, height, rgba):
        stem = f'type{index + 1}/{kind}'
        # The fixed message and default frame already belong to the battle UI.
        if kind == 'battle-message' or index == 0:
            name = 'frame1' if kind == 'frame' else kind
            texture_path = 'content/generated/ui/lab-battle-ui/' + name + '.png'
        else:
            images.append((stem, Image.frombytes('RGBA', (width, height), bytes(rgba))))
            texture_path = 'content/generated/ui/option-frames/frames.png'
        assets.append(dict(frameIndex=index, kind=kind, texturePath=texture_path,
                           width=width, height=height, x=0, y=0 if kind == 'frame' else 112))

    battle_paths = ['graphics/battle_interface/textbox.png', 'graphics/battle_interface/textbox.bin',
                    'graphics/battle_interface/textbox1.pal']
    text_tiles = pixels.tile_list(sources[battle_paths[0]])
    text_palette = pixels.palette(sources[battle_paths[2]])
    for index in range(10):
        frame_path = f'graphics/text_window/type{index + 1}.png'
        data = sources[frame_path]
        width, height, indices, rgb = pixels.indexed_png(data)
        if (width, height, len(rgb)) != (24, 24, 16):
            raise ValueError('User frame dimensions/palette differ')
        palette = pixels.colors(rgb)
        frame_tiles = pixels.tile_list(data)
        add(index, 'frame', width, height, pixels.rgba(indices, palette))
        tiles = list(text_tiles)
        # Both banks are loaded by source. The three supported crops only reference 18..26.
        tiles[18:27] = frame_tiles
        tiles[34:43] = frame_tiles
        for kind, top in [('battle-message', 14), ('action-menu', 34), ('move-menu', 54)]:
            rgba = pixels.render_map(tiles, sources[battle_paths[1]], {0: text_palette, 1: palette}, 240, 48, top)
            add(index, kind, 240, 48, rgba)
    outputs['frames.png'], regions = pack(images, 'content/generated/ui/option-frames/frames.png')
    regions = {row['id']: row['region'] for row in regions}
    for asset in assets:
        stem = f"type{asset['frameIndex'] + 1}/{asset['kind']}"
        if stem in regions:
            asset['region'] = regions[stem]
    display = dict(logicalWidth=240, logicalHeight=160, frameIndexMinimum=0, frameIndexMaximum=9,
                   frameTileSize=8, textureFilter='nearest', transparentIndex=0,
                   paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', battleWindowY=112,
                   battleMessageUsesUserFrame=False, sourceTileOffsets=[18, 34],
                   affectedFieldWindows=['start-menu', 'standard-yes-no', 'script-multichoice', 'player-pc-top-menu'],
                   fixedWindows=['ordinary-dialogue', 'signpost', 'save-statistics', 'option-menu-secondary-panel'])
    catalog = dict(schemaVersion=1,
                    display=display, assets=assets)
    outputs['option-frames.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1,
         inputCount=len(PATHS), assetCount=40,
        outputs=[dict(path=p) for p, data in sorted(outputs.items())],
         ))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=pathlib.Path, help='Pinned local Git repository; omitted to use authenticated raw files')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/ui/option-frames')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/source/fetch-option-frames.lock.json')
    args = parser.parse_args()
    raw, output = safe(args.raw), safe(args.output)
    if output.is_relative_to(raw) or raw.is_relative_to(output):
        raise ValueError('Option-frame raw/output roots overlap')
    if args.source is not None:
        repository = safe(args.source)
        if any(repository.is_relative_to(p) or p.is_relative_to(repository) for p in (raw, output)):
            raise ValueError('Option-frame source repository overlaps a write root')
    lock = load_lock(args.lock)
    sources = read_sources(lock, raw, args.source)
    outputs = convert(lock, sources)
    # Resolve all targets before any write; all source/conversion authentication precedes mutation.
    writes = [(safe(output, name), data) for name, data in outputs.items()]
    if args.source is not None:
        writes += [(safe(raw, name), data) for name, data in sources.items()]
    writes = [(target, safe(target.parent, target.name + '.tmp'), data) for target, data in writes]
    for target, stage, data in writes:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    # This importer owns the former per-option images. Remove them only after the
    # replacement page and catalog have both been written; shared lab images stay put.
    for index in range(2, 11):
        directory = safe(output, f'type{index}')
        for kind in ('frame', 'action-menu', 'move-menu'):
            safe(directory, kind + '.png').unlink(missing_ok=True)
        if directory.is_dir() and not any(directory.iterdir()):
            directory.rmdir()
    print(json.dumps(dict(status='verified', assets=40, sources=len(PATHS))))


if __name__ == '__main__':
    main()
