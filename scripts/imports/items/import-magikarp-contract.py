#!/usr/bin/env python3
"""Compile the Route4 Magikarp sale's shared story references from local source."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SOURCE_PATHS = (
    'data/maps/Route4_PokemonCenter_1F/scripts.inc',
    'data/maps/Route4_PokemonCenter_1F/text.inc',
    'data/scripts/pc_transfer.inc', 'data/scripts/std_msgbox.inc',
    'data/text/pc_transfer.inc', 'data/event_scripts.s',
)
COMMON_SCRIPTS = (
    'EventScript_GetGiftMonPartySlot', 'EventScript_TransferredToPC',
    'EventScript_TransferredSomeonesPC', 'EventScript_TransferredSomeonesPCBoxFull',
    'EventScript_TransferredBillsPC', 'EventScript_TransferredBillsPCBoxFull',
    'Std_MsgboxDefault', 'Std_MsgboxYesNo',
)
COMMON_TEXTS = (
    'Text_GiveNicknameToThisMon', 'Text_MonSentToBoxInSomeonesPC',
    'Text_MonSentToBoxInBillsPC', 'Text_MonSentToBoxSomeonesBoxFull',
    'Text_MonSentToBoxBillsBoxFull',
)
SALESMAN = 'Route4_PokemonCenter_1F_EventScript_MagikarpSalesman'


def safe_file(base, relative):
    base = Path(base).absolute()
    path = base / relative
    if Path(relative).is_absolute() or '..' in Path(relative).parts or path == base:
        raise ValueError('Unsafe relative file: ' + str(relative))
    for cursor in (path, *path.parents):
        if cursor.is_symlink():
            raise ValueError('Symlink source/output is unsupported: ' + str(cursor))
    return path


def materialize(reference, raw):
    """Read only this import's inputs from the fixed local Git commit."""
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env['GIT_NO_REPLACE_OBJECTS'] = '1'
    pending = []
    for relative in SOURCE_PATHS:
        path = safe_file(raw, relative)
        data = subprocess.check_output(
            ['git', '-C', str(reference), 'show', COMMIT + ':' + relative], env=env)
        pending.append((path, data))
    for path, data in pending:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def read_sources(project=ROOT, raw=None):
    raw = Path(raw) if raw is not None else Path(project) / 'content/raw/pokefirered/037335f4'
    return {path: safe_file(raw, path).read_text(encoding='utf-8') for path in SOURCE_PATHS}


def blocks(sources):
    result = {}
    for path in SOURCE_PATHS:
        text = sources[path]
        labels = list(re.finditer(r'^(\w+)::?\s*$', text, re.M))
        for i, label in enumerate(labels):
            finish = labels[i + 1].start() if i + 1 < len(labels) else len(text)
            # Includes and comments do not belong to the preceding label's body.
            body = [line.strip() for line in text[label.end():finish].splitlines()
                    if line.strip() and not line.lstrip().startswith(('.include', '@'))]
            if label[1] in result:
                raise ValueError('Duplicate source label: ' + label[1])
            result[label[1]] = body
    return result


def build(sources):
    all_blocks = blocks(sources)
    if SALESMAN not in all_blocks:
        raise ValueError('Magikarp salesman script is missing')
    selected, pending = set(), [SALESMAN]
    while pending:
        symbol = pending.pop()
        if symbol in selected:
            continue
        if symbol not in all_blocks:
            raise ValueError('Gift common-script closure is incomplete: ' + symbol)
        selected.add(symbol)
        for line in all_blocks[symbol]:
            if line.startswith('.string'):
                continue
            for token in re.findall(r'\b\w+\b', line):
                if token in all_blocks and token not in selected:
                    pending.append(token)
            if line.startswith('msgbox '):
                pending.append('Std_MsgboxYesNo' if 'MSGBOX_YESNO' in line else 'Std_MsgboxDefault')
    if not set(COMMON_SCRIPTS + COMMON_TEXTS) <= selected:
        raise ValueError('Gift common-script closure is incomplete')
    extra_common = {name for name in selected if not name.startswith('Route4_PokemonCenter_1F_')} - set(COMMON_SCRIPTS + COMMON_TEXTS)
    if extra_common:
        raise ValueError('Unexpected common-script scope: ' + ', '.join(sorted(extra_common)))
    for label in COMMON_SCRIPTS:
        if not all_blocks[label] or any(line.startswith('.string') for line in all_blocks[label]):
            raise ValueError('Expected common script body: ' + label)
    for label in COMMON_TEXTS:
        if not all_blocks[label] or any(not line.startswith('.string') for line in all_blocks[label]):
            raise ValueError('Expected common text body: ' + label)
    # The compiled story owns script bodies, dialogue and gameplay. This document
    # supplies only the references required by game composition.
    contract = dict(schemaVersion=1, commonScripts=list(COMMON_SCRIPTS), commonTexts=list(COMMON_TEXTS))
    return {'magikarp-contract.json': runtime_json_bytes(contract, sort_keys=True)}


def run(project=ROOT, raw=None, output=None, check=False):
    outputs = build(read_sources(project, raw))
    output = Path(output) if output is not None else Path(project) / 'content/generated/data/services/magikarp-gift'
    for relative, data in outputs.items():
        path = safe_file(output, relative)
        if check:
            if path.read_bytes() != data:
                raise ValueError('Generated contract differs: ' + relative)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    return json.loads(outputs['magikarp-contract.json'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--materialize', action='store_true', help='Read required source blobs from the fixed local Git commit')
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--raw', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--check', action='store_true', help='Verify existing output without writing')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.materialize:
        reference_root = args.reference or Path((ROOT / '.tools/reference-root').read_text(encoding='utf-8').strip())
        materialize(reference_root, args.raw)
    print(json.dumps(run(raw=args.raw, output=args.output, check=args.check), sort_keys=True))


if __name__ == '__main__':
    main()
