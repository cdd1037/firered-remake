#!/usr/bin/env python3
"""Extract bounded static multichoice-grid definitions; run no reference-repository code."""
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
PROFILE = 'firered-source-grid-menus-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
BASE = 'content/generated/source-grid-menus/'


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()


def need(value, message):
    if not value:
        raise ValueError(message)


def build(target, dependency, reference, check):
    reader_path = dependency / 'scripts/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('source_grid_reader', reader_path)
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    reader.PROFILE = PROFILE
    source = reader.Source(reference)
    menus = source.text('src/script_menu.c')
    controls = source.text('src/menu.c')
    commands = source.text('src/scrcmd.c')
    constants = source.text('include/constants/menu.h')
    strings = source.text('src/strings.c')
    font = source.text('src/text.c')
    font_info = source.text('src/new_menu_helpers.c')
    charmap = dict((char, int(code, 16)) for char, code in re.findall(r"^'(.)'\s*=\s*([A-F0-9]{2})\s*$", source.text('charmap.txt'), re.M))
    need(re.search(r'^#define SCR_MENU_CANCEL\s+127\s*$', constants, re.M), 'Cancel result differs')
    for snippet in ['rowCount = count / columnCount;', 'width = GetMenuWidthFromList(list, count) + 1;',
                    'width * columnCount, rowCount * 2', 'width * 8, 16, columnCount, rowCount, list',
                    'FONT_NORMAL_COPY_1, 0, 1, width * 8, columnCount, rowCount, 0',
                    's8 input = Menu_ProcessInputGridLayout();', 'if (tIgnoreBPress)\n            return;',
                    'left + 1, top + 1, width, height', '(GetStringWidth(FONT_NORMAL_COPY_1, str, 0) + 7) / 8']:
        need(snippet in menus, 'Source menu semantics differ: ' + snippet)
    grid_input = controls.split('s8 Menu_ProcessInputGridLayout(void)', 1)[1].split('// Unused', 1)[0]
    need(grid_input.count('MultichoiceGrid_MoveCursorIfValid(') == 4 and 'JOY_REPEAT' not in grid_input and
         'MultichoiceGrid_MoveCursor(' not in grid_input, 'Grid no-wrap/new-press input differs')
    command = commands.split('bool8 ScrCmd_multichoicegrid(', 1)[1].split('bool8 ScrCmd_erasebox(', 1)[0]
    need(re.findall(r'(?:u8|bool8) (\w+) = ScriptReadByte\(ctx\);', command) ==
         ['left', 'top', 'multichoiceId', 'numColumns', 'ignoreBPress'], 'Grid operand order differs')
    need('.maxLetterWidth = 8,\n        .maxLetterHeight = 14,' in font_info.split('[FONT_NORMAL_COPY_1]', 1)[1].split('},', 1)[0], 'Grid font geometry differs')
    widths = [int(v) for v in re.search(r'sFontNormalCopy1LatinGlyphWidths\[\]\s*=\s*\{(.*?)\}', font, re.S)[1].replace('\n', '').split(',') if v.strip()]
    normal_widths = [int(v) for v in re.search(r'sFontNormalLatinGlyphWidths\[\]\s*=\s*\{(.*?)\}', font, re.S)[1].replace('\n', '').split(',') if v.strip()]
    definitions = []
    # Adding another mounted definition requires reviewing and re-pinning this inventory.
    for menu_id, list_name in [('MULTICHOICE_TRAINER_SCHOOL_WHITEBOARD', 'sMultichoiceList_TrainerSchoolWhiteboard')]:
        number = int(re.search(r'^#define ' + menu_id + r'\s+(\d+)\s*$', constants, re.M)[1])
        need(re.search(r'\[' + menu_id + r'\]\s*= MULTICHOICE\(' + list_name + r'\)', menus), 'Menu list binding differs')
        block = re.search(r'static const struct MenuAction ' + list_name + r'\[\] = \{(.*?)\};', menus, re.S)[1]
        symbols = re.findall(r'\{ (\w+) \}', block)
        need(len(symbols) in range(1, 13), 'Menu count outside bounded static support')
        options = []
        for symbol in symbols:
            label = re.search(r'const u8 ' + symbol + r'\[\] = _\("([^"\n]+)"\);', strings)[1]
            need(all(32 <= ord(c) <= 126 and c in charmap for c in label), 'Unsupported dynamic/non-Latin label')
            need(all(widths[charmap[c]] == normal_widths[charmap[c]] for c in label), 'Reused normal font width differs')
            options.append(dict(textSymbol=symbol, text=label))
        column_width = max((sum(widths[charmap[c]] for c in option['text']) + 7) // 8 for option in options) + 1
        definitions.append(dict(id=menu_id, sourceId=number, options=options, columnWidthTiles=column_width))
    locked = canonical(source.lock())
    manifest = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                               menus=definitions, semantics=dict(cancelResult=127, rowHeight=16,
                              initialCursor=0, wrap=False, rowMajor=True, textX=8, textY=1, cursorX=0, cursorY=1)))
    outputs = {'scripts/source-grid-menus.lock.json': locked, BASE + 'manifest.json': manifest}
    outputs.update({'content/raw/pokefirered/037335f4/' + p: b for p, b in source.data.items()})
    for path, data in outputs.items():
        destination = target / path
        need(not any(p.is_symlink() for p in [destination, *destination.parents]), 'Symlink output rejected')
        if check:
            need(destination.is_file() and destination.read_bytes() == data, 'Output differs: ' + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(inputs=len(source.entries), files=len(outputs),  menus=definitions), sort_keys=True))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--target', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    build(args.target.resolve(), args.dependency_root.resolve(), args.reference.resolve(), args.check)
