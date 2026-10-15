#!/usr/bin/env python3
"""Compile the native Move Deleter UI strings, retaining their control sequences."""
import argparse
import json
import pathlib
import re
ROOT = pathlib.Path(__file__).resolve().parents[1]
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
    path = ROOT / 'content/generated/move-deleter/texts.json'
    if args.check:
        if path.read_bytes() != data: raise ValueError('Move Deleter texts differ')
    else:
        path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    print(json.dumps(dict(texts=4, checked=args.check)))
if __name__ == '__main__': main()
