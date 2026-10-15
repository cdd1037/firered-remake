#!/usr/bin/env python3
"""Compile the native Move Deleter UI strings, retaining their control sequences."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import re
ROOT = _PROJECT_ROOT
SYMBOLS = ('gText_ChoosePokemon', 'gText_PokeSum_PageName_KnownMoves', 'gText_PokeSum_Controls_PickDelete', 'gFameCheckerText_Cancel')

def convert(source):
    texts = {}
    for symbol in SYMBOLS:
        matches = re.findall(r'const u8 ' + symbol + r'\[\] = _\("([^\"]*)"\);', source)
        if len(matches) != 1: raise ValueError('Move Deleter text missing: ' + symbol)
        texts[symbol] = matches[0]
    return texts

def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    data = (json.dumps(convert((ROOT / 'content/raw/pokefirered/037335f4/src/strings.c').read_text()), sort_keys=True, indent=2) + '\n').encode()
    path = ROOT / 'content/generated/maps/regions/move-deleter/texts.json'
    if args.check:
        if path.read_bytes() != data: raise ValueError('Move Deleter texts differ')
    else:
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    print(json.dumps(dict(texts=4, checked=args.check)))
if __name__ == '__main__': main()
