#!/usr/bin/env python3
"""Import all 354 pinned ordinary moves, without granting any execution capability.

Offline by default. --reference reads fixed Git blobs, never runs upstream programs.
--verify recomputes all outputs without changing any file.
"""
import argparse
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-metronome-metadata-v1'
SOURCE_PATHS = ['include/battle.h', 'include/constants/battle_move_effects.h', 'include/constants/moves.h', 'include/constants/pokemon.h', 'src/data/battle_moves.h', 'src/data/text/move_names.h']
OUTPUT_PATH = 'content/generated/metronome-moves/catalog.json'
# These independent output pins change only with a reviewed source/importer change.


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode()


def safe(root, relative):
    if pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative or any(p in ('', '.', '..') for p in relative.split('/')):
        raise ValueError('Unsafe relative path: ' + relative)
    path = pathlib.Path(root).absolute() / relative
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError('Symlink rejected: ' + relative)
    return path


def authenticate(root, reference=None):
    lock = dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT, profile=PROFILE, gameEdition='FIRERED', romRevision=0, ordinaryMoveCount=354, files=[dict(path=p) for p in SOURCE_PATHS])
    if reference and subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD']).decode().strip() != COMMIT:
        raise ValueError('Reference HEAD differs')
    raw = {}
    for row in lock['files']:
        path = safe(root, 'content/raw/pokefirered/037335f4/' + row['path'])
        data = subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + row['path']]) if reference else path.read_bytes()
        raw[row['path']] = data
    return lock, raw


def generate(lock, raw):
    text = {p: b.decode('utf-8') for p, b in raw.items()}

    def one(path, pattern):
        matches = list(re.finditer(pattern, text[path], re.M | re.S))
        if len(matches) != 1:
            raise ValueError(f'Expected one source match ({len(matches)}): {path}: {pattern}')
        return matches[0]

    def source(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
                    endLine=text[path].count('\n', 0, match.end()) + 1)

    def ident(symbol):
        return dict(value='pret:' + symbol)



    constants = []
    specs = [('include/constants/moves.h', 'MOVE_'), ('include/constants/pokemon.h', 'TYPE_'),
             ('include/constants/pokemon.h', 'FLAG_'), ('include/constants/battle_move_effects.h', 'EFFECT_'),
             ('include/battle.h', 'MOVE_TARGET_')]
    for path, prefix in specs:
        for match in re.finditer(r'^#define (' + prefix + r'\w+)[ \t]+([^\n]+)', text[path], re.M):
            symbol, expression = match[1], match[2].split('//')[0].strip()
            shift = re.fullmatch(r'\(1 << (\d+)\)', expression)
            if shift:
                value = 1 << int(shift[1])
            elif re.fullmatch(r'(?:0x[0-9A-Fa-f]+|\d+)', expression):
                value = int(expression, 0) if expression.startswith('0x') else int(expression)
            else:
                raise ValueError('Unsupported constant syntax: ' + symbol + '=' + expression)
            constants.append(dict(symbol=symbol, value=value))
    constants.sort(key=lambda row: row['symbol'])
    numbers = {c['symbol']: c['value'] for c in constants}
    if len(numbers) != len(constants):
        raise ValueError('Duplicate source constant')
    move_ids = {s: v for s, v in numbers.items() if s.startswith('MOVE_') and not s.startswith('MOVE_TARGET_')}
    if move_ids.pop('MOVE_UNAVAILABLE') != 65535 or sorted(move_ids.values()) != list(range(355)):
        raise ValueError('Ordinary source move ID closure differs')

    moves = []
    move_names = []
    path = 'src/data/battle_moves.h'
    fields = {'effect', 'power', 'type', 'accuracy', 'pp', 'secondaryEffectChance', 'target', 'priority', 'flags'}
    blocks = list(re.finditer(r'^    \[(MOVE_\w+)\] =\s*\n    \{\n(.*?)^    \},', text[path], re.M | re.S))
    if len(blocks) != 355 or len({b[1] for b in blocks}) != 355:
        raise ValueError('Source move table scope differs')
    for match in blocks:
        symbol = match[1]
        pairs = re.findall(r'^\s*\.(\w+) = (.+),$', match[2], re.M)
        values = dict(pairs)
        if len(pairs) != len(fields) or set(values) != fields:
            raise ValueError('Unknown or duplicate move fields: ' + symbol)
        if symbol == 'MOVE_NONE':
            continue
        flags = [] if values['flags'] == '0' else values['flags'].split(' | ')
        if len(set(flags)) != len(flags) or any(not f.startswith('FLAG_') or f not in numbers for f in flags):
            raise ValueError('Unsupported source flags: ' + symbol)
        for field, prefix in [('effect', 'EFFECT_'), ('type', 'TYPE_'), ('target', 'MOVE_TARGET_')]:
            if not values[field].startswith(prefix) or values[field] not in numbers:
                raise ValueError('Unsupported move constant: ' + symbol + ':' + field)
        moves.append(dict(id=ident(symbol), sourceMoveId=move_ids[symbol], pp=int(values['pp']), power=int(values['power']), type=values['type'], accuracy=int(values['accuracy']), effect=values['effect'], secondaryEffectChance=int(values['secondaryEffectChance']), target=values['target'], priority=int(values['priority']), flags=flags, battleEffectImplemented=False))
        name_path = 'src/data/text/move_names.h'
        name_match = one(name_path, r'^    \[' + symbol + r'\]\s*= _\("([^"\n]+)"\),?$')
        move_names.append(dict(id=ident(symbol), name=name_match[1]))
    moves.sort(key=lambda m: m['sourceMoveId'])
    move_names.sort(key=lambda m: move_ids[m['id']['value'][5:]])
    if [m['sourceMoveId'] for m in moves] != list(range(1, 355)):
        raise ValueError('Missing or duplicate ordinary move')

    document = dict(moves=moves, moveNames=move_names)
    return {OUTPUT_PATH: canonical(document)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference', type=pathlib.Path)
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    if args.verify and args.reference:
        parser.error('--verify is read-only; reseed with --reference separately')
    lock, raw = authenticate(args.project_root, args.reference)
    outputs = generate(lock, raw)
    if args.verify:
        for path, data in outputs.items():
            if safe(args.project_root, path).read_bytes() != data:
                raise ValueError('Generated Metronome output differs: ' + path)
    else:
        if args.reference:
            for path, data in raw.items():
                target = safe(args.project_root, 'content/raw/pokefirered/037335f4/' + path)
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(data)
        for path, data in outputs.items():
            target = safe(args.project_root, path)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(verified=args.verify, ordinaryMoves=354,
                          effectExecutionGranted=False, outputs={p: dict() for p, b in outputs.items()}), sort_keys=True))


if __name__ == '__main__':
    main()
