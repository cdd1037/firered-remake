#!/usr/bin/env python3
"""Authenticate all original Vs Seeker parties through pinned inert Git blobs.

The existing reviewed Git reader and canonical species/move catalogs are dependencies.
No upstream executable runs and no substitute trainer, species, or move is permitted.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import selected_trainers, natural_moves as source_natural_moves
import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vs-seeker-parties-v1'

def need(ok, message):
    if not ok: raise ValueError(message)

def compact(value): return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()
def output(value): return compact(value) + b'\n'
def identity(symbol): return {'value': 'pret:' + symbol}

def generate(reference, dependency, rules_root, raw_root=None):
    helper = dependency/'scripts/imports/source/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('vs_seeker_source_reader', helper)
    reader = importlib.util.module_from_spec(spec); spec.loader.exec_module(reader)
    src = reader.Source(reference, raw_root=raw_root)
    dependencies = []
    def dependent(root, path):
        raw = (root/path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(raw)
    rules = dependent(rules_root, 'content/generated/data/services/vs-seeker.json')
    canonical = dependent(dependency, 'content/generated/data/pokemon/canonical/catalog.json')
    move_document = dependent(dependency, 'content/generated/data/services/metronome-moves.json')
    species = {x['id']['value'][5:]: x for x in canonical['species']}
    moves = {x['id']['value'][5:]: x for x in move_document['moves']}

    def one(path, pattern):
        found = list(re.finditer(pattern, src.text(path), re.M | re.S))
        need(len(found) == 1, 'Expected one source declaration: ' + pattern)
        return found[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=src.text(path).count('\n', 0, match.start()) + 1,
            endLine=src.text(path).count('\n', 0, match.end()) + 1)
    def number(path, symbol):
        value = one(path, r'^#define\s+' + re.escape(symbol) + r'\s+([^\n]+)')[1].split('//')[0].strip()
        if re.fullmatch(r'0x[0-9a-fA-F]+|\d+', value): return int(value, 0)
        shift = re.fullmatch(r'\(1 << (\d+)\)', value)
        need(shift, 'Unsupported source constant: ' + symbol)
        return 1 << int(shift[1])
    def natural(species_symbol, level):
        result = source_natural_moves(src.text, species_symbol, level)
        projected = []
        for row in species[species_symbol]['levelUpMoves']:
            move = row['move']['value'][5:]
            if row['level'] <= level and move not in projected: projected = (projected + [move])[-4:]
        need(result == projected and 1 <= len(result) <= 4, 'Independent canonical/default move comparison: ' + species_symbol)
        return result

    trainers, resolved = [], []
    source_ids = {}
    for row in rules['rematches']:
        need(len(row['trainers']) == len(row['trainerSourceIds']) == 6, 'Rematch slots differ')
        for symbol, source_id in zip(row['trainers'], row['trainerSourceIds']):
            expected_id = 65535 if symbol == 'SKIP' else number('include/constants/opponents.h', symbol)
            need(type(source_id) is int and source_id == expected_id, 'Rematch source identity differs: ' + symbol)
            if symbol in ('SKIP', 'TRAINER_NONE'): continue
            need(symbol not in source_ids or source_ids[symbol] == source_id, 'Conflicting rematch trainer ID')
            source_ids[symbol] = source_id
    need(source_ids and len(set(source_ids.values())) == len(source_ids), 'Empty or conflicting rematch roster')
    trainers = selected_trainers(src.text, sorted(source_ids, key=source_ids.get))
    for trainer in trainers:
        expected = []
        custom = bool(trainer['partyFlags'] & 1)
        for member in trainer['party']:
            symbol = member['species']['value'][5:]
            default_moves = natural(symbol, member['level'])
            custom_moves = [slot['value'][5:] for slot in member['customMoves']]
            actual = custom_moves[:custom_moves.index('MOVE_NONE')] if 'MOVE_NONE' in custom_moves else custom_moves
            need(not custom or all(move == 'MOVE_NONE' for move in custom_moves[len(actual):]), 'Nontrailing MOVE_NONE')
            need(symbol in species and species[symbol]['sourceSpeciesId'] == member['sourceSpeciesId'],
                 'Missing canonical species: ' + symbol)
            chosen = actual if custom else default_moves
            need(all(move in moves for move in chosen), 'Missing canonical moves: ' + symbol)
            expected.append([identity(move) for move in chosen])
        resolved.append(dict(trainer=trainer['id'], partyMoves=expected))
    trainers.sort(key=lambda t: t['sourceTrainerId']); resolved.sort(key=lambda r: next(t['sourceTrainerId'] for t in trainers if t['id'] == r['trainer']))
    # Authenticate the actual actor-script branch, including its rematch label and exact line.
    script_path = 'data/scripts/trainers.inc'; script_text = src.text(script_path)
    blocks = {m[1]: m for m in re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)', script_text, re.M | re.S)}
    occurrences = []
    modes = {'trainerbattle_single': 'TRAINER_BATTLE_SINGLE', 'trainerbattle_double': 'TRAINER_BATTLE_DOUBLE',
             'trainerbattle_rematch': 'TRAINER_BATTLE_REMATCH', 'trainerbattle_rematch_double': 'TRAINER_BATTLE_REMATCH_DOUBLE'}
    macros = src.text('asm/macros/event.inc')
    for opcode, mode in modes.items():
        macro = re.search(r'^\s*\.macro ' + opcode + r'\b(.*?)^\s*\.endm', macros, re.M | re.S)
        need(macro and mode in macro[1], 'Trainer battle macro mode differs: ' + opcode)
    for actor in rules['actorBindings']:
        root = actor['scriptLabel']; need(root in blocks, 'Missing original actor script: ' + root)
        pending, seen, found = [root], set(), []
        while pending:
            label = pending.pop()
            if label in seen: continue
            seen.add(label); block = blocks[label]
            for match in re.finditer(r'^\t(trainerbattle_\w+) ([^\n]+)$', block[2], re.M):
                opcode = match[1]; args = [x.strip() for x in match[2].split(',')]
                need(opcode in modes and len(args) == (4 if 'double' in opcode else 3) and args[0] == actor['trainerSymbol'], 'Actor battle occurrence differs: ' + root)
                offset = block.start(2) + match.start()
                line = script_text.count('\n', 0, offset) + 1
                found.append(dict(actorScript=root, scriptEntry=label, opcode=opcode, arguments=args, trainer=identity(args[0]),
                    isRematch='rematch' in opcode, mode=number('include/constants/battle_setup.h', modes[opcode]),
                    source=dict(path=script_path, symbol=label, startLine=line, endLine=line)))
            pending += [x for x in re.findall(r'\b\w+\b', block[2]) if x in blocks and x not in seen]
        need(len(found) == 2 and sum(o['isRematch'] for o in found) == 1, 'Exactly one original/rematch branch per actor: ' + root)
        occurrences += found
    occurrences.sort(key=lambda x: (x['actorScript'], x['isRematch']))
    document = dict(  trainers=trainers, resolvedMoves=resolved)
    occurrence_document = dict(occurrences=occurrences)
    lock = dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT,  profile=PROFILE,
        reader=dict(path='scripts/imports/source/import-cinnabar-source-preparation.py'), dependencies=dependencies,
        files=[src.entries[p] for p in sorted(src.entries)])
    summary = dict(trainers=len(trainers), singles=sum(not t['doubleBattle'] for t in trainers), doubles=sum(t['doubleBattle'] for t in trainers),
        partyMembers=sum(len(t['party']) for t in trainers), sourceSpecies=sorted({m['species']['value'] for t in trainers for m in t['party']}),
        moveCount=len({m['value'] for row in resolved for party in row['partyMoves'] for m in party}),
        partyFlags=sorted({t['partyFlags'] for t in trainers}), aiFlags=sorted({t['aiFlags'] for t in trainers}),
        heldItems=sorted({m['heldItem'] for t in trainers for m in t['party']}),
        missingCanonicalSpecies=[], missingCanonicalMoves=[])
    return {'catalog.json': output(document), 'battle-occurrences.json': output(occurrence_document), 'manifest.json': output(dict(schemaVersion=1,
           summary=summary))}, src

def main():
    p = argparse.ArgumentParser()
    source = p.add_mutually_exclusive_group(required=True)
    source.add_argument('--source-root', type=Path)
    source.add_argument('--raw-root', type=Path)
    p.add_argument('--dependency-root', type=Path, required=True)
    p.add_argument('--rules-root', type=Path, required=True); p.add_argument('--output-root', type=Path, default=_PROJECT_ROOT); p.add_argument('--check', action='store_true')
    a = p.parse_args(); artifacts, src = generate(a.source_root, a.dependency_root, a.rules_root, a.raw_root)
    artifacts['catalog.json'] = trainer_json(a.output_root/'content/generated', json.loads(artifacts['catalog.json']), check=a.check)
    for name, data in artifacts.items():
        path = a.output_root/'content/generated/data/services/vs-seeker-parties'/name
        if a.check: need(path.read_bytes() == data, 'Generated artifact differs: ' + str(path))
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    for name, raw in src.data.items():
        path = a.output_root/'content/raw/pokefirered/037335f4'/name
        if a.check: need(path.read_bytes() == raw, 'Raw source differs: ' + name)
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
    print(artifacts['manifest.json'].decode().strip())

if __name__ == '__main__': main()
