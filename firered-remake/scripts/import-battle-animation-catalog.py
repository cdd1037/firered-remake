#!/usr/bin/env python3
"""Pinned animation dependency inventory; does not execute animation or claim rendered coverage."""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
BASE_PATHS = ('data/battle_anim_scripts.s', 'asm/macros/battle_anim_script.inc',
              'include/constants/moves.h', 'include/constants/battle_anim.h',
              'include/constants/songs.h', 'include/constants/sound.h',
              'src/data/battle_anim.h')


def inventory(reference, cache_root=None):
    lock_bytes = Path(__file__).with_name('battle-animation-source.lock.json').read_bytes()
    lock = json.loads(lock_bytes)
    expected = {entry['path']: entry for entry in lock['files']}
    if lock['sourceCommit'] != COMMIT or tuple(expected) != BASE_PATHS:
        raise ValueError('Source inventory differs')

    def pinned(path):
        source = reference / path
        record = expected[path]
        with source.open('rb') as stream:
            if source.stat().st_size != record['bytes']:
                raise ValueError('Source length differs: ' + path)
            data = stream.read(record['bytes'] + 1)
        if len(data) != record['bytes']:
            raise ValueError('Source bytes differ: ' + path)
        if (reference / '.git').exists() and subprocess.check_output(
                ['git', '-C', str(reference), 'show', COMMIT + ':' + path]) != data:
            raise ValueError('Reference differs from fixed Git blob: ' + path)
        return data

    raw = {p: pinned(p) for p in BASE_PATHS}
    if cache_root is not None:
        for path, data in raw.items():
            destination = cache_root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    text = {p: b.decode() for p, b in raw.items()}
    script = text[BASE_PATHS[0]]
    macro_names = set(re.findall(r'^\s*\.macro (\w+)', text[BASE_PATHS[1]], re.M))
    move_ids = {m: int(n) for m, n in re.findall(r'^#define (MOVE_\w+)\s+(\d+)\s*$', text[BASE_PATHS[2]], re.M) if 1 <= int(n) <= 354}
    if len(move_ids) != 354 or set(move_ids.values()) != set(range(1, 355)):
        raise ValueError('Expected all 354 exact move IDs')
    table = re.search(r'^gBattleAnims_Moves::\n(.*?)(?=^\s*\.align)', script, re.M | re.S)
    if table is None:
        raise ValueError('Missing source move animation table')
    roots = re.findall(r'^\s*\.4byte (\w+)', table[1], re.M)
    if len(roots) != 356 or roots[0] != 'Move_NONE' or roots[-1] != 'Move_COUNT':
        raise ValueError('Source table cardinality differs')

    # The reviewed game is original revision 0. The one Switch revision conditional
    # in Explosion is selected explicitly; no runtime preprocessor is invented.
    labels, instructions, condition = {}, [], []
    enabled = True
    for line_number, original in enumerate(script.splitlines(), 1):
        row = original.split('@', 1)[0].strip()
        if row.startswith('.if'):
            if row != '.if REVISION >= 0xA':
                raise ValueError('Unreviewed animation conditional: ' + row)
            condition.append(enabled); enabled = False
            continue
        if row == '.else':
            if not condition: raise ValueError('Unbalanced else')
            enabled = condition[-1] and not enabled
            continue
        if row == '.endif':
            if not condition: raise ValueError('Unbalanced endif')
            enabled = condition.pop(); continue
        if not enabled or not row or row.startswith(('#', '.')):
            continue
        if re.fullmatch(r'\w+\s*::?', row):
            label = row.rstrip(':').strip()
            if label in labels: raise ValueError('Duplicate label: ' + label)
            labels[label] = len(instructions)
            continue
        command, _, arguments = row.partition(' ')
        if command not in macro_names:
            command, *parts = row.split(None, 1)
            arguments = parts[0] if parts else ''
        if command not in macro_names:
            raise ValueError(f'Unknown macro at {line_number}: {row}')
        instructions.append({'line': line_number, 'command': command, 'arguments': arguments.strip()})
    if condition: raise ValueError('Unclosed conditional')

    def targets(instruction):
        op, args = instruction['command'], instruction['arguments']
        if op not in ('call', 'goto', 'choosetwoturnanim') and not op.startswith('jump'):
            return []
        names = [word for word in re.findall(r'\b\w+\b', args) if word in labels]
        if not names: raise ValueError('Branch without source label: ' + repr(instruction))
        return names

    edges = []
    for i, instruction in enumerate(instructions):
        follow = [labels[name] for name in targets(instruction)]
        if instruction['command'] not in ('end', 'return', 'goto', 'choosetwoturnanim') and i + 1 < len(instructions):
            follow.append(i + 1)
        edges.append(follow)

    def closure(root):
        pending, visited = [labels[root]], set()
        while pending:
            i = pending.pop()
            if i in visited: continue
            if i >= len(instructions): raise ValueError('Label past script')
            visited.add(i); pending.extend(edges[i])
        return [instructions[i] for i in sorted(visited)]

    def symbols(rows, ops):
        return sorted({r['arguments'].split(',', 1)[0].strip() for r in rows if r['command'] in ops})

    entries = []
    for move, number in sorted(move_ids.items(), key=lambda x: x[1]):
        root = roots[number]
        if root not in labels: raise ValueError('Unknown root: ' + root)
        rows = closure(root)
        entries.append({'move': 'pret:' + move, 'sourceMoveId': number, 'script': root,
            'entryLine': instructions[labels[root]]['line'],
            'reachableInstructionLines': [r['line'] for r in rows],
            'commands': sorted({r['command'] for r in rows}),
            'spriteTemplates': symbols(rows, {'createsprite'}),
            'visualTasks': symbols(rows, {'createvisualtask'}),
            'soundTasks': symbols(rows, {'createsoundtask'}),
            'scriptGraphicTags': symbols(rows, {'loadspritegfx'}),
            'scriptDirectSounds': sorted({s for r in rows for s in re.findall(r'\bSE_[A-Z0-9_]+\b', r['arguments'])}),
            'delayCommands': [{'line': r['line'], 'framesExpression': r['arguments']} for r in rows if r['command'] == 'delay'],
            'dedicatedRenderer': 'not-implemented', 'renderedAcceptance': False,
            'soundTimingAcceptance': False})
    commands = sorted({r['command'] for r in instructions})
    return {'schemaVersion': 1, 'sourceCommit': COMMIT, 'game': 'FIRERED', 'revision': 0,
        'kind': 'conservative-script-dependency-inventory',
        'limitations': [
            'Conditional branch closure is a dependency superset, not one executed animation or exact duration.',
            'Calls include continuation and callee; return-stack reachability is not interpreted.',
            'C task and sprite callbacks can load additional graphics and trigger sounds; these transitive dependencies remain to inspect.',
            'No animation frame, sound timing, original runtime or renderer is accepted by this inventory.'],
        'inputs': [{'path': p, 'bytes': len(raw[p])} for p in BASE_PATHS],
        'macroDefinitions': sorted(macro_names), 'usedCommands': commands,
        'instructionCount': len(instructions), 'labelCount': len(labels), 'moves': entries,
        'summary': {'moves': len(entries), 'scriptRoots': len({e['script'] for e in entries}),
            'spriteTemplates': len({s for e in entries for s in e['spriteTemplates']}),
            'visualTasks': len({s for e in entries for s in e['visualTasks']}),
            'soundTasks': len({s for e in entries for s in e['soundTasks']}),
            'graphicTags': len({s for e in entries for s in e['scriptGraphicTags']}),
            'directSounds': len({s for e in entries for s in e['scriptDirectSounds']})}}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('reference_root', type=Path)
    parser.add_argument('new_output', type=Path)
    parser.add_argument('--cache-root', type=Path, help='Optional source cache for subsequent project resource backups')
    args = parser.parse_args()
    if args.new_output.exists(): parser.error('Output already exists')
    result = inventory(args.reference_root, args.cache_root)
    args.new_output.parent.mkdir(parents=True, exist_ok=True)
    args.new_output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result['summary']))
