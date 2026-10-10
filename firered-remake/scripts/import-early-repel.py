#!/usr/bin/env python3
"""Extract the bounded Repel texts and item facts from authenticated local Git objects only."""
import argparse
import hashlib
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-early-kanto-repel-v1'
LOCK_SHA = '6afd036a2e2c61046d7339ef4f7f441ac78b8b00ced9e73f28b3b4bd8beb9aa8'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_sources(reference, lock_path=None):
    data = (pathlib.Path(lock_path) if lock_path else ROOT / 'scripts/early-repel.lock.json').read_bytes()
    if digest(data) != LOCK_SHA:
        raise ValueError('The reviewed Repel source lock differs')
    lock = json.loads(data)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE or len(lock['files']) != 10:
        raise ValueError('The Repel source scope differs')
    sources = {}
    for item in lock['files']:
        raw = subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + item['path']])
        blob = hashlib.sha1(f'blob {len(raw)}\0'.encode() + raw).hexdigest()
        if (digest(raw), len(raw), blob) != (item['sha256'], item['byteLength'], item['gitBlobSha1']):
            raise ValueError('Repel source identity differs: ' + item['path'])
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
    return dict(schemaVersion=1, sourceCommit=COMMIT, profile=PROFILE, repelVariable=variable,
        itemSymbol=item['itemId'], duration=item['holdEffectParam'], battleUsage=item['battleUsage'],
        battleUseFunction=item['battleUseFunc'], fieldUseFunction=item['fieldUseFunc'],
        sourceFiles=lock['files'], texts=texts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/early-repel/early-repel.json')
    args = parser.parse_args()
    document = extract(args.reference)
    raw = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(raw)
    print(json.dumps(dict(sourceCommit=COMMIT, sourceFiles=len(document['sourceFiles']), texts=len(document['texts']), byteLength=len(raw), sha256=digest(raw))))


if __name__ == '__main__':
    main()
