#!/usr/bin/env python3
"""Bounded importer semantics and completeness checks; never changes the source lock."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import importlib.util
from pathlib import Path
import json
import tempfile
from unittest.mock import patch

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
assert 'rosterRequirements' not in catalog
assert catalog == json.loads((root / 'content/generated/data/services/vs-seeker.json').read_bytes())
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

# Party compilation reads the actual retained raw source through the shared reader.
spec = importlib.util.spec_from_file_location('vs_seeker_parties', root / 'scripts/imports/trainers/import-vs-seeker-parties.py')
parties = importlib.util.module_from_spec(spec)
spec.loader.exec_module(parties)

source_root = root / 'content/raw/pokefirered/037335f4'
artifacts, _ = parties.generate(source_root, root, root)
document = json.loads(artifacts['catalog.json'])
assert len(document['trainers']) == 351
assert sum(t['doubleBattle'] for t in document['trainers']) == 25
artifacts['catalog.json'] = parties.trainer_json(root / 'content/generated', document, check=True)
for name, data in artifacts.items():
    existing = (root / 'content/generated/data/services/vs-seeker-parties' / name).read_bytes()
    assert data == existing, name

with tempfile.TemporaryDirectory(prefix='vs-seeker-rules-') as temporary:
    target = Path(temporary) / 'content/generated/data/services/vs-seeker.json'
    target.parent.mkdir(parents=True)
    for kind in ('wrong_id', 'conflicting_id', 'missing_slot', 'unknown_trainer'):
        broken = json.loads(raw)
        row = broken['rematches'][0]
        if kind == 'wrong_id': row['trainerSourceIds'][0] += 1
        elif kind == 'conflicting_id':
            broken['rematches'].append(dict(row, trainerSourceIds=[-1] + row['trainerSourceIds'][1:]))
        elif kind == 'missing_slot': row['trainers'].pop()
        else: row['trainers'][0] = 'TRAINER_UNKNOWN_TEST'
        target.write_text(json.dumps(broken))
        try:
            parties.generate(source_root, root, Path(temporary))
        except ValueError:
            pass
        else:
            raise AssertionError('invalid rematch accepted: ' + kind)
original_text = parties.WorldSource.text
def malformed_declaration(self, path):
    text = original_text(self, path)
    return text.replace('.doubleBattle = FALSE', '.doubleBattle = INVALID') if path == 'src/data/trainers.h' else text
with patch.object(parties.WorldSource, 'text', malformed_declaration):
    try:
        parties.generate(source_root, root, root)
    except ValueError as error:
        assert str(error).startswith('Unrecognized trainer items or battle mode:')
    else:
        raise AssertionError('invalid declaration accepted')
print(json.dumps(dict(passed=True, rematches=len(catalog['rematches']), trainers=351, doubles=25,
                     partyArtifactsByteEqual=len(artifacts), rejectedPartyInputs=5)))
