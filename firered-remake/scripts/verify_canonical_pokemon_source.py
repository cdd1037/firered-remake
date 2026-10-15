#!/usr/bin/env python3
"""Standalone source completeness/reproducibility/negative fixtures, no external packages."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'scripts'))
from import_canonical_pokemon import LOCK_PATH, authenticate, build, extract, select_firered
from build_source_lock import encode, inventory
from semantic_fingerprint import semantic_bytes

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
    check(encode(inventory(args.source_root)) == (ROOT / LOCK_PATH).read_bytes(), 'Every source tree/Git blob/worktree byte must agree')
test('all 15 source inputs independently match pinned Git tree and worktree', pinned_inventory)

def cold_generation():
    expected = (ROOT / 'content/generated/canonical-pokemon/catalog.json').read_bytes()
    outputs = []
    with tempfile.TemporaryDirectory(prefix='cold-source-', dir=ROOT / 'evidence') as temporary:
        for name in ('first', 'second'):
            root = Path(temporary) / name
            target = root / LOCK_PATH
            target.parent.mkdir(parents=True)
            target.write_bytes((ROOT / LOCK_PATH).read_bytes())
            build(root, args.source_root, args.move_root)
            outputs.append((root / 'content/generated/canonical-pokemon/catalog.json').read_bytes())
    check(outputs[0] == outputs[1] == expected, 'Two cold generations must match reviewed output bytes')
test('two independent cold offline generations are byte-identical', cold_generation)

def independent_source_rows():
    info = sources.text['src/data/pokemon/species_info.h']
    real_blocks = re.findall(r'^    \[(SPECIES_\w+)\] =\n    \{', info, re.M)
    check(len(real_blocks) == 386 and set(real_blocks) == {s['source']['symbol'] for s in doc['species']}, 'No real source species skipped')
    source_evolutions = re.findall(r'\{(EVO_\w+),\s+(\d+|ITEM_\w+),\s+(SPECIES_\w+)\}', sources.text['src/data/pokemon/evolution.h'])
    emitted_evolutions = [(e['method'], e['parameter'], e['target']['value'][5:]) for m in doc['metadata'] for e in m['sourceEvolutions']]
    check(len(source_evolutions) == 184 and sorted(source_evolutions) == sorted(emitted_evolutions), 'Every original evolution triple retained')
    # A separate linear pass over the raw FR/LG source, with exact known edition directive vocabulary.
    active = True
    raw_rows, excluded_rows = [], []
    array_name = ''
    for line in sources.text['src/data/pokemon/level_up_learnsets.h'].splitlines():
        if line == '#if defined(FIRERED)': active = True
        elif line == '#elif defined(LEAFGREEN)': active = False
        elif line == '#endif': active = True
        elif active:
            declaration = re.fullmatch(r'static const u16 (\w+)\[\] = \{', line)
            if declaration: array_name = declaration[1]
            match = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if match:
                placeholder = re.fullmatch(r'sSpecies(\d+)LevelUpLearnset', array_name)
                if placeholder:
                    check(252 <= int(placeholder[1]) <= 276, 'Unknown placeholder learnset')
                    excluded_rows.append((int(match[1]), match[2]))
                else:
                    raw_rows.append((int(match[1]), match[2]))
    check(excluded_rows == [(1, 'MOVE_TACKLE')] * 25, 'All25 old-Unown placeholder learnsets are separately excluded')
    generated_rows = [(e['level'], e['move']['value'][5:]) for s in doc['species'] for e in s['levelUpMoves']]
    check(len(raw_rows) == 4011 and sorted(raw_rows) == sorted(generated_rows), 'Every FR level-up row retained with multiplicity')
test('complete species, evolution and level rows independently match original source', independent_source_rows)

def references():
    texts = {k: v.splitlines() for k, v in sources.text.items()}
    for s in doc['species']:
        for e in s['levelUpMoves']:
            r = e['source']
            check(r['startLine'] == r['endLine'], 'Learnset row source must be exact')
            line = texts[r['path']][r['startLine'] - 1].strip()
            check(line == 'LEVEL_UP_MOVE(%d, %s),' % (e['level'], e['move']['value'][5:]), 'Exact raw source learnset line differs')
    for m in doc['metadata']:
        for e in m['sourceEvolutions']:
            r = e['source']
            text = '\n'.join(texts[r['path']][r['startLine'] - 1:r['endLine']])
            check(re.search(r'\{' + e['method'] + r',\s+' + e['parameter'] + r',\s+' + e['target']['value'][5:] + r'\}', text) is not None, 'Exact evolution source line differs')
test('all 4011 learnset and184 evolution references point to actual source rows', references)

def fingerprint():
    data = semantic_bytes(doc, move_doc)
    check(len(data) == 1617174 and hashlib.sha256(data).hexdigest() == 'c84202efe70af7801c2d603c9165b460407dabce1fec7df3b0f08641399d2f3b', 'Frozen Domain semantic fingerprint')
    check(data == (ROOT / 'content/generated/canonical-pokemon/semantic-envelope.json').read_bytes(), 'Semantic artifact must match direct source extraction')
    check(not data.endswith(b'\n'), 'Domain fingerprint has no LF')
test('Domain semantic envelope is independently source-derived and hash-locked', fingerprint)

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
        original = target.read_bytes()
        target.write_bytes(b'X' + original[1:])
        rejects(lambda: authenticate(root, None, args.move_root))
        target.write_bytes(original[:-1])
        rejects(lambda: authenticate(root, None, args.move_root))
        target.write_bytes(original)
        lockfile = root / LOCK_PATH
        lockfile.write_bytes(lockfile.read_bytes() + b' ')
        rejects(lambda: authenticate(root, None, args.move_root))
test('source same-length tampering, truncation and lock edits are rejected', corrupted_sources)

def corrupted_move_dependency():
    with tempfile.TemporaryDirectory(prefix='move-tamper-', dir=ROOT / 'evidence') as temporary:
        root = Path(temporary)
        for directory in ('sources', 'generated', 'raw'):
            shutil.copytree(args.move_root / 'content' / directory / 'metronome-moves', root / 'content' / directory / 'metronome-moves')
        target = root / 'content/generated/metronome-moves/catalog.json'
        original = target.read_bytes()
        target.write_bytes(original[:-1])
        rejects(lambda: authenticate(ROOT, None, root))
        target.write_bytes(b'X' + original[1:])
        rejects(lambda: authenticate(ROOT, None, root))
test('354-move dependency is authenticated, not accepted by count or claimed provenance', corrupted_move_dependency)

report = dict(passed=sum(r['passed'] for r in results), failed=sum(not r['passed'] for r in results), tests=results)
(ROOT / 'evidence/source-tests.json').write_text(json.dumps(report, indent=2) + '\n')
if len(results) < 8 or report['failed']:
    raise SystemExit(1)
print('PASS', len(results), 'nonempty source fixtures')
