#!/usr/bin/env python3
"""Extract the bounded Repel texts and item facts from authenticated local Git objects only."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import pathlib
import re
import subprocess

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-early-kanto-repel-v1'


def read_sources(reference, lock_path=None):
    data = (pathlib.Path(lock_path) if lock_path else ROOT / 'scripts/recipes/items/early-repel.lock.json').read_bytes()
    lock = json.loads(data)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE or len(lock['files']) != 10:
        raise ValueError('The Repel source scope differs')
    sources = {}
    for item in lock['files']:
        raw = subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + item['path']])
        sources[item['path']] = raw.decode('utf-8', errors='strict')
    return lock, sources


def extract(reference):
    lock, sources = read_sources(reference)
    items = json.loads(sources['src/data/items.json'])
    rows = items if isinstance(items, list) else items['items']
    item, = [i for i in rows if i['itemId'] == 'ITEM_REPEL']
    assert item['holdEffectParam'] == 100 and item['battleUsage'] == 0 and item['battleUseFunc'] == 'NULL'
    assert item['fieldUseFunc'] == 'FieldUseFunc_Repel' and item['type'] == 'ITEM_TYPE_BAG_MENU'
    variable = int(re.search(r'^#define\s+VAR_REPEL_STEP_COUNT\s+(0x[0-9A-Fa-f]+)', sources['include/constants/vars.h'], re.M)[1], 16)
    texts = []
    for symbol in ['gText_PlayerUsedVar2', 'gText_RepelEffectsLingered']:
        pattern = r'^const u8 ' + symbol + r'\[\] = _\("([^"\n]*)"\);'
        match, = list(re.finditer(pattern, sources['src/strings.c'], re.M))
        texts.append(dict(symbol=symbol, text=match[1].replace('\\n', '\n'), path='src/strings.c', line=sources['src/strings.c'][:match.start()].count('\n') + 1))
    worn = sources['data/scripts/repel.inc']
    assert 'msgbox Text_RepelWoreOff, MSGBOX_SIGN\n\tend' in worn
    match, = list(re.finditer(r'^Text_RepelWoreOff::\n\t.string "([^"\n]+)\$"', worn, re.M))
    texts.append(dict(symbol='Text_RepelWoreOff', text=match[1], path='data/scripts/repel.inc', line=worn[:match.start()].count('\n') + 1))
    return dict(schemaVersion=1,   repelVariable=variable,
        itemSymbol=item['itemId'], duration=item['holdEffectParam'], battleUsage=item['battleUsage'],
        battleUseFunction=item['battleUseFunc'], fieldUseFunction=item['fieldUseFunc'],
         texts=texts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/data/services/early-repel.json')
    args = parser.parse_args()
    document = extract(args.reference)
    raw = runtime_json_bytes(document)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(raw)
    print(json.dumps(dict(sourceCommit=COMMIT, texts=len(document['texts']))))


if __name__ == '__main__':
    main()
