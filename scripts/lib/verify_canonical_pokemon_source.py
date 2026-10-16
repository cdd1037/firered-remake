#!/usr/bin/env python3
"""Standalone source completeness/reproducibility/negative fixtures, no external packages."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile

ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from import_canonical_pokemon import authenticate, build, extract, select_firered
from build_source_lock import encode, inventory, FILES

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source-root', type=Path, required=True)
parser.add_argument('--move-root', type=Path, required=True)
args = parser.parse_args()
results = []

def check(condition, message):
    if not condition:
        raise AssertionError(message)

def rejects(action):
    try:
        action()
    except (ValueError, FileNotFoundError):
        return
    raise AssertionError('Expected explicit source rejection')

def test(name, action):
    try:
        action()
        results.append(dict(name=name, passed=True))
        print('PASS', name)
    except Exception as error:
        results.append(dict(name=name, passed=False, error=repr(error)))
        print('FAIL', name, repr(error))

lock, sources, move_doc = authenticate(ROOT, args.source_root, args.move_root)
doc = extract(lock, sources, move_doc)

def pinned_inventory():
    check([r['path'] for r in inventory(args.source_root)['files']] == FILES, 'Pinned Git source inventory must match the importer recipe')
test('all 15 source inputs independently match pinned Git tree and worktree', pinned_inventory)

def cold_generation():
    expected = (ROOT / 'content/generated/data/pokemon/canonical/catalog.json').read_bytes()
    outputs = []
    with tempfile.TemporaryDirectory(prefix='cold-source-', dir=ROOT / 'evidence') as temporary:
        for name in ('first', 'second'):
            root = Path(temporary) / name
            build(root, args.source_root, args.move_root)
            outputs.append((root / 'content/generated/data/pokemon/canonical/catalog.json').read_bytes())
    check(outputs[0] == outputs[1] == expected, 'Two cold generations must match reviewed output bytes')
test('two independent cold offline generations are byte-identical', cold_generation)

def independent_source_rows():
    info = sources.text['src/data/pokemon/species_info.h']
    real_blocks = re.findall(r'^    \[(SPECIES_\w+)\] =\n    \{', info, re.M)
    check(len(real_blocks) == 386 and set(real_blocks) == {s['id']['value'][5:] for s in doc['species']}, 'No real source species skipped')
    source_evolutions = re.findall(r'\{(EVO_\w+),\s+(\d+|ITEM_\w+),\s+(SPECIES_\w+)\}', sources.text['src/data/pokemon/evolution.h'])
    emitted_evolutions = [(e['method'], e['parameter'], e['target']['value'][5:]) for m in doc['metadata'] for e in m['sourceEvolutions']]
    check(len(source_evolutions) == 184 and sorted(source_evolutions) == sorted(emitted_evolutions), 'Every original evolution triple retained')
    # A separate linear pass over the raw FR/LG source, with exact known edition directive vocabulary.
    active = True
    raw_rows, excluded_rows, learnsets = [], [], {}
    array_name = ''
    for line in sources.text['src/data/pokemon/level_up_learnsets.h'].splitlines():
        if line == '#if defined(FIRERED)': active = True
        elif line == '#elif defined(LEAFGREEN)': active = False
        elif line == '#endif': active = True
        elif active:
            declaration = re.fullmatch(r'static const u16 (\w+)\[\] = \{', line)
            if declaration:
                array_name = declaration[1]
                check(array_name not in learnsets, 'Duplicate source learnset array')
                learnsets[array_name] = []
            match = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if match:
                placeholder = re.fullmatch(r'sSpecies(\d+)LevelUpLearnset', array_name)
                if placeholder:
                    check(252 <= int(placeholder[1]) <= 276, 'Unknown placeholder learnset')
                    excluded_rows.append((int(match[1]), match[2]))
                else:
                    row = (int(match[1]), match[2])
                    raw_rows.append(row)
                    learnsets[array_name].append(row)
    check(excluded_rows == [(1, 'MOVE_TACKLE')] * 25, 'All25 old-Unown placeholder learnsets are separately excluded')
    generated_rows = [(e['level'], e['move']['value'][5:]) for s in doc['species'] for e in s['levelUpMoves']]
    check(len(raw_rows) == 4011 and sorted(raw_rows) == sorted(generated_rows), 'Every FR level-up row retained with multiplicity')
    pointers = dict(re.findall(r'^    \[(SPECIES_\w+)\] = (\w+),$',
        sources.text['src/data/pokemon/level_up_learnset_pointers.h'], re.M))
    for species in doc['species']:
        check([(row['level'], row['move']['value'][5:]) for row in species['levelUpMoves']] ==
              learnsets[pointers[species['id']['value'][5:]]], 'Every species retains the exact ordered FR learnset')
test('complete species, evolution and level rows independently match original source', independent_source_rows)

def bad_editions():
    raw = sources.text['src/data/pokemon/level_up_learnsets.h']
    rejects(lambda: select_firered(raw.replace('#elif defined(LEAFGREEN)', '#else', 1)))
    rejects(lambda: select_firered(raw.replace('#if defined(FIRERED)', '#if defined(EMERALD)', 1)))
    rejects(lambda: select_firered(raw.replace('#endif', '', 1)))
test('unknown, alternate and unterminated preprocessor grammar fails closed', bad_editions)

def corrupted_sources():
    with tempfile.TemporaryDirectory(prefix='source-tamper-', dir=ROOT / 'evidence') as temporary:
        root = Path(temporary)
        shutil.copytree(ROOT / 'content', root / 'content')
        target = root / 'content/raw/pokefirered/037335f4/src/data/pokemon/species_info.h'
        original = target.read_text()
        target.write_text(original.replace('[SPECIES_BULBASAUR]', '[SPECIES_UNKNOWN]', 1))
        rejects(lambda: extract(*authenticate(root, None, args.move_root)))
        target.unlink()
        rejects(lambda: authenticate(root, None, args.move_root))
test('unknown species and missing source inputs are rejected', corrupted_sources)

def corrupted_move_dependency():
    with tempfile.TemporaryDirectory(prefix='move-tamper-', dir=ROOT / 'evidence') as temporary:
        root = Path(temporary)
        for directory in ('generated',):
            shutil.copytree(args.move_root / 'content' / directory / 'metronome-moves', root / 'content' / directory / 'metronome-moves')
        target = root / 'content/generated/data/services/metronome-moves.json'
        original = target.read_bytes()
        target.write_bytes(original[:-1])
        rejects(lambda: authenticate(ROOT, None, root))
        target.write_bytes(b'X' + original[1:])
        rejects(lambda: authenticate(ROOT, None, root))
test('malformed move dependency is rejected', corrupted_move_dependency)

report = dict(passed=sum(r['passed'] for r in results), failed=sum(not r['passed'] for r in results), tests=results)
(ROOT / 'evidence/source-tests.json').write_text(json.dumps(report, indent=2) + '\n')
if len(results) < 6 or report['failed']:
    raise SystemExit(1)
print('PASS', len(results), 'nonempty source fixtures')
