#!/usr/bin/env python3
"""Bounded importer semantics and completeness checks; never changes the source lock."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import importlib.util
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--git-dir', required=True, type=Path)
args = p.parse_args()
root = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location('vs_seeker_importer', root / 'scripts/imports/trainers/import-vs-seeker.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
lock, sources = module.read_sources(args.git_dir, root / 'scripts/recipes/trainers/vs-seeker.lock.json')
catalog = module.extract(sources)
raw = module.json_bytes(catalog)
assert raw == module.json_bytes(module.extract(sources))
assert len(catalog['rematches']) == 221 and len(catalog['actorBindings']) == 234
assert len(catalog['rosterRequirements']) == 351
assert sum(t['doubleBattle'] for t in catalog['rosterRequirements']) == 25
# Table map metadata is unused: actual South bindings must survive extraction.
assert next(b for b in catalog['actorBindings'] if b['trainerSymbol'] == 'TRAINER_FISHERMAN_CLAUDE')['sourceMap'] == 'MAP_ROUTE21_SOUTH'
missing = dict(sources)
del missing['data/maps/Route21_South/map.json']
try:
    module.extract(missing)
except ValueError as error:
    assert str(error) == 'vs_seeker_binding_coverage'
else:
    raise AssertionError('incomplete-map-bindings-accepted')
print('{"passed":true,"checks":7,"sourceFiles":61,"rematches":221,"actorBindings":234,"rosterRequirements":351}')
