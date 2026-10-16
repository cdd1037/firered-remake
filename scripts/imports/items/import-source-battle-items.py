#!/usr/bin/env python3
"""Compile battle-use capability rows from retained pinned source, without C execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from item_catalog import ROOT, RAW, COMMIT, definitions
from item_effects import read_effects


def compile_battle_items(root):
    effects = read_effects(root / RAW)
    reusable = {'ITEM_BLUE_FLUTE', 'ITEM_RED_FLUTE', 'ITEM_YELLOW_FLUTE', 'ITEM_POKE_FLUTE'}
    handlers = {'BattleUseFunc_Medicine', 'BattleUseFunc_Ether', 'BattleUseFunc_StatBooster'}
    items = []
    for symbol, item in definitions(root).items():
        values = effects.get(symbol, []) if item['battleUseFunction'] in handlers else []
        items.append(dict(item=item.identity, effectBytes=values, reusable=symbol in reusable))
    return dict(items=items)


def run(root, check=False):
    document = compile_battle_items(root)
    data = (json.dumps(document, ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    path = root / 'content/generated/battle/items.json'
    if check:
        if path.read_bytes() != data:
            raise ValueError('Battle item regeneration differs')
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    return dict(checked=check, items=len(document['items']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root.resolve(), args.check)))
