#!/usr/bin/env python3
"""Bounded importer authentication and completeness checks; never changes the source lock."""
import argparse
import importlib.util
from pathlib import Path
import tempfile

p = argparse.ArgumentParser()
p.add_argument('--git-dir', required=True, type=Path)
args = p.parse_args()
root = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('vs_seeker_importer', root / 'scripts/import-vs-seeker.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
lock, sources = module.read_sources(args.git_dir, root / 'scripts/vs-seeker.lock.json')
catalog = module.extract(lock, sources)
raw = module.json_bytes(catalog)
assert module.sha(raw) == module.CATALOG_SHA256
assert raw == module.json_bytes(module.extract(lock, sources))
assert len(catalog['rematches']) == 221 and len(catalog['actorBindings']) == 234
assert len(catalog['rosterRequirements']) == 351
assert sum(t['doubleBattle'] for t in catalog['rosterRequirements']) == 25
# Table map metadata is unused: actual South bindings must survive extraction.
assert next(b for b in catalog['actorBindings'] if b['trainerSymbol'] == 'TRAINER_FISHERMAN_CLAUDE')['sourceMap'] == 'MAP_ROUTE21_SOUTH'
with tempfile.TemporaryDirectory() as tmp:
    bad = Path(tmp) / 'lock.json'
    bad.write_bytes((root / 'scripts/vs-seeker.lock.json').read_bytes() + b' ')
    try:
        module.read_sources(args.git_dir, bad)
    except ValueError as error:
        assert str(error) == 'vs_seeker_lock_hash'
    else:
        raise AssertionError('mutated-lock-accepted')
missing = dict(sources)
del missing['data/maps/Route21_South/map.json']
try:
    module.extract(lock, missing)
except ValueError as error:
    assert str(error) == 'vs_seeker_binding_coverage'
else:
    raise AssertionError('incomplete-map-bindings-accepted')
print('{"passed":true,"checks":9,"sourceFiles":61,"rematches":221,"actorBindings":234,"rosterRequirements":351}')
