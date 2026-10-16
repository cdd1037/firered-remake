#!/usr/bin/env python3
"""Rebuild shared trainer business data from local, inert source declarations."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path

from shared_trainers import publish_trainers, read_trainers

from trainer_import import generate

ROOT = _PROJECT_ROOT


def reference_ids(generated):
    identities = set()
    for path in sorted(Path(generated).rglob('*.json')):
        if path.parent.name == 'shared-trainers': continue
        document = json.loads(path.read_text())
        if not isinstance(document, dict): continue
        references = document.get('trainers')
        if isinstance(document.get('trainer'), str) and document['trainer'].startswith('pret:TRAINER_'):
            identities.add(document['trainer'])
        if isinstance(references, list) and references and all(isinstance(row, str) and row.startswith('pret:TRAINER_') for row in references):
            if len(set(references)) != len(references):
                raise ValueError('Duplicate trainer reference in ' + str(path))
            identities.update(references)
    if not identities: raise ValueError('No regional trainer references')
    return sorted(identities)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw-root', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--generated-root', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    identities = reference_ids(args.generated_root)
    definitions = generate(args.raw_root, identities)
    publish_trainers(args.generated_root, definitions, check=args.check)
    if set(read_trainers(args.generated_root)) != set(identities):
        raise ValueError('Shared trainer table contains unreferenced definitions')
    print(json.dumps(dict(checked=args.check, trainers=len(definitions), members=sum(len(t['party']) for t in definitions))))


if __name__ == '__main__': main()
