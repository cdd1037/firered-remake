#!/usr/bin/env python3
"""Import pinned text.c down-arrow frames; never run upstream code or read ROMs."""
import argparse
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-text-down-arrow-v1'
INPUT_PATHS = frozenset('''Makefile
graphics/fonts/down_arrows.png
graphics/text_window/stdpal_0.pal
graphics/battle_interface/textbox1.pal
include/text.h
src/text.c
src/window.c
src/blit.c
src/battle_message.c
src/battle_bg.c
src/field_message_box.c
src/new_menu_helpers.c
src/text_window.c
src/text_window_graphics.c
tools/gbagfx/gfx.c'''.splitlines())
# Reuse this project's strict PNG/palette codec only, not its UI conversion.
spec = importlib.util.spec_from_file_location('cursor_png_codec', ROOT / 'scripts/import-lab-battle-ui.py')
codec = importlib.util.module_from_spec(spec)
spec.loader.exec_module(codec)


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def safe_path(root, relative):
    root = pathlib.Path(root).resolve()
    target = root / relative
    if (not relative or pathlib.PurePosixPath(relative).is_absolute()
            or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative
            or not target.resolve().is_relative_to(root)):
        raise ValueError('Unsafe cursor path: ' + relative)
    return target


def load_lock(path):
    lock = json.loads(pathlib.Path(path).read_bytes())
    project = json.loads((ROOT / 'scripts/import-config/source.json').read_bytes())
    if (lock.get('schemaVersion'), lock.get('profile'), lock.get('repository'), lock.get('commit'),
            lock.get('gameEdition'), lock.get('romRevision')) != (1, PROFILE, 'pret/pokefirered', COMMIT, 'FIRERED', 0):
        raise ValueError('Unsupported cursor source identity')
    if (project['source']['sourceCommit'], project['source']['repository'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (COMMIT, 'pret/pokefirered', 'FIRERED', 0):
        raise ValueError('Project source identity differs')
    entries = lock.get('files', [])
    paths = [e['path'] for e in entries]
    if len(paths) != len(set(paths)) or set(paths) != INPUT_PATHS:
        raise ValueError('Cursor dependency set differs')
    for e in entries:
        safe_path(ROOT, e['path'])
        if (e['sourceUrl'] != f"https://github.com/pret/pokefirered/blob/{COMMIT}/{e['path']}"):
            raise ValueError('Cursor lock differs from pinned tree: ' + e['path'])
    return lock


def read_verified(lock, raw, cache=None):
    result = {}
    for e in lock['files']:
        path = safe_path(raw, e['path'])
        if path.is_file():
            data = path.read_bytes()  # Existing corrupt inputs fail; do not silently replace them.
        elif cache is not None:
            data = subprocess.check_output(['git', '-C', str(cache), 'show', COMMIT + ':' + e['path']])
        else:
            raise FileNotFoundError('Missing cursor source; pass --cache for pinned Git blobs: ' + str(path))
        result[e['path']] = data
    return result


def convert(lock, sources):
    if set(sources) != INPUT_PATHS:
        raise ValueError('Incomplete cursor sources')
    source = sources['src/text.c'].decode()
    delay = int(re.search(r'^#define CURSOR_DELAY (\d+)$', source, re.M)[1])
    alternate_offset = int(re.search(r'^#define DARK_DOWN_ARROW_OFFSET (\d+)$', source, re.M)[1])
    sequence = [int(v) for v in re.findall(r'\d+', re.search(r'sDownArrowYCoords\[\]\s*=\s*\{([^}]+)\}', source)[1])]
    if (delay, alternate_offset, sequence) != (8, 256, [0, 16, 32, 16]):
        raise ValueError('Unsupported down-arrow source contract')
    compact = re.sub(r'\s+', ' ', source)
    for fragment in [
        'subStruct->downArrowYPosIdx = 0; subStruct->downArrowDelay = 0;',
        'arrowTiles = &sDownArrowTiles[DARK_DOWN_ARROW_OFFSET];',
        'BlitBitmapRectToWindow( textPrinter->printerTemplate.windowId, arrowTiles, sDownArrowYCoords[subStruct->downArrowYPosIdx], 0, 0x80, 0x10, textPrinter->printerTemplate.currentX, textPrinter->printerTemplate.currentY, 10, 12);',
        'subStruct->downArrowDelay = CURSOR_DELAY;',
    ]:
        if fragment not in compact:
            raise ValueError('Source text printer semantics differ')
    if 'rectWidth, rectHeight, 0);' not in sources['src/window.c'].decode():
        raise ValueError('Source transparent color key differs')
    w, h, indices, _ = codec.indexed_png(sources['graphics/fonts/down_arrows.png'])
    if (w, h, set(indices)) != (128, 16, {0, 2, 4, 9}):
        raise ValueError('Unsupported down-arrow PNG')
    # GBA 4bpp uses 8x8 tiles, 32 bytes each. DARK offset256 is eight tiles,
    # hence +64 source pixels in each 128-wide row, not +256 PNG pixels.
    if alternate_offset % 32:
        raise ValueError('Unaligned arrow tile offset')
    alternate_x = (alternate_offset // 32) * 8
    assets, outputs = [], {}
    for name, offset, palette_path in [
        ('red', 0, 'graphics/text_window/stdpal_0.pal'),
        ('alternate', alternate_x, 'graphics/battle_interface/textbox1.pal'),
    ]:
        pal = codec.palette(sources[palette_path])
        frames = []
        for sx in sorted(set(sequence)):
            frame = bytes(indices[y * w + offset + sx + x] for y in range(12) for x in range(10))
            frames.append(bytes(c for index in frame for c in ((*pal[index], 255) if index else (0, 0, 0, 0))))
        rgba = b''.join(frame[y * 40:(y + 1) * 40] for y in range(12) for frame in frames)
        filename = name + '.png'
        outputs[filename] = codec.png(30, 12, rgba)
        assets.append(dict(id=name, texturePath=filename, width=30, height=12, paletteSource=palette_path,
            sourceByteOffset=offset * 4, sourcePngXOffset=offset, paletteRgb8=pal))
    catalog = dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', sourceCommit=COMMIT,
         sourceSize=[128, 16], frameSize=[10, 12],
        sourceXSequence=sequence, atlasFrameSequence=[0, 1, 2, 1], cursorDelay=delay,
        presentationTicksPerFrame=delay + 1, destination='TextPrinterTemplate.currentX/currentY',
        destinationOffset=[0, 0], transparentPaletteIndex=0, paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',
        assets=assets, sourceRefs=lock['files'], limitations=[
            'Presentation ticks choose the visual phase; this does not implement source text-printer/60fps timing.',
            'Caller redraws the text-window background before each draw; no persistent source window buffer.',
            'Only field/Oak red arrow and ordinary battle alternate arrow palettes are supported.',
            'No double-arrow sprite, auto-scroll policy, text-control-code or input-wait implementation.'])
    outputs['text-cursor.json'] = canonical(catalog)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         inputs=lock['files'],
        outputs=[dict(path=p) for p, d in sorted(outputs.items())],
        upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/text-cursor')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/text-cursor.lock.json')
    parser.add_argument('--cache', type=pathlib.Path, help='Read-only local pinned reference Git repository')
    args = parser.parse_args()
    if args.output.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.output.resolve()):
        raise ValueError('Raw/output roots overlap')
    lock = load_lock(args.lock)
    sources = read_verified(lock, args.raw, args.cache)
    outputs = convert(lock, sources)
    pending = [(safe_path(args.raw, p), d) for p, d in sources.items()]
    pending += [(safe_path(args.output, p), d) for p, d in outputs.items()]
    for target, data in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage = safe_path(target.parent, target.name + '.tmp')
        stage.write_bytes(data)
        stage.replace(target)
    print(json.dumps(dict(status='imported', sources=len(sources), textures=2)))


if __name__ == '__main__':
    main()
