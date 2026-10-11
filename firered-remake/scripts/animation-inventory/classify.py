#!/usr/bin/env python3
"""Small static grouping of pinned revision-0 animation script closures."""
import argparse
import bisect
import collections
import importlib.util
import json
from pathlib import Path
import re

spec = importlib.util.spec_from_file_location('animation_inventory', Path(__file__).resolve().parents[1] / 'import-battle-animation-catalog.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def classify(reference):
    inv = module.inventory(reference)  # Authenticates every input before parsing.
    rows, labels, pending = {}, {}, []
    enabled, stack = True, []
    for number, raw in enumerate((reference / 'data/battle_anim_scripts.s').read_text().splitlines(), 1):
        row = raw.split('@', 1)[0].strip()
        if row.startswith('.if'):
            assert row == '.if REVISION >= 0xA'
            stack.append(enabled); enabled = False; continue
        if row == '.else': enabled = stack[-1] and not enabled; continue
        if row == '.endif': enabled = stack.pop(); continue
        if not enabled: continue
        if re.fullmatch(r'\w+\s*::?', row): pending.append(row.rstrip(':').strip()); continue
        parts = row.split(None, 1)
        if not parts or parts[0] not in inv['macroDefinitions']: continue
        for label in pending: labels[label] = number
        pending.clear()
        rows[number] = (parts[0], parts[1] if len(parts) > 1 else '')
    assert not stack
    graphs, aliases, dependencies = (collections.defaultdict(list) for _ in range(3))
    conditional = 0
    for move in inv['moves']:
        lines = move['reachableInstructionLines']; local = {line: i for i, line in enumerate(lines)}
        def normalize(match):
            token = match[0]
            if token not in labels: return token
            return '@' + str(local[labels[token]])
        body = [(rows[line][0], re.sub(r'\s+', '', re.sub(r'\b\w+\b', normalize, rows[line][1]))) for line in lines]
        signature = json.dumps([local[move['entryLine']], body], separators=(',', ':'))
        graphs[signature].append(move['move'])
        aliases[move['entryLine']].append(move['move'])
        deps = tuple(tuple(move[k]) for k in ('spriteTemplates', 'visualTasks', 'soundTasks'))
        dependencies[deps].append(move['move'])
        conditional += any(op.startswith('jump') or op == 'choosetwoturnanim' for op in move['commands'])
    return dict(schemaVersion=1, sourceCommit=inv['sourceCommit'], revision=0, inputs=inv['inputs'],
        namedRoots=len(inv['moves']), uniqueEntryPositions=len(aliases), exactConservativeInstructionGraphs=len(graphs),
        identicalScriptGroups=[v for v in graphs.values() if len(v) > 1], entryAliases=[v for v in aliases.values() if len(v) > 1],
        directDependencySetGroups=len(dependencies), summary=inv['summary'], conditionalMoves=conditional,
        scriptCommandsUsedByMoves=len({op for move in inv['moves'] for op in move['commands']}),
        limits=inv['limitations'] + ['Strict graph identity retains all opcode arguments, sprite and task names; only label spelling and whitespace are normalized. Similar visuals can have distinct graphs. Equal dependency sets do not establish equal animations. This does not measure implementation work or accepted rendering.'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists(): parser.error('Output already exists')
    result = classify(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({k: result[k] for k in ('namedRoots', 'uniqueEntryPositions', 'exactConservativeInstructionGraphs', 'directDependencySetGroups', 'conditionalMoves')}, sort_keys=True))
