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

from trainer_import import generate, class_names
from runtime_json import runtime_json_bytes

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
    classes = class_names(args.raw_root)
    if any(trainer['trainerClass'] not in classes for trainer in definitions):
        raise ValueError('Shared trainer class has no source display name')
    class_path = args.generated_root / 'data/trainer-class-names.json'
    if any(path.is_symlink() for path in (class_path, *class_path.parents)):
        raise ValueError('Symlink in trainer class name output: ' + str(class_path))
    class_data = runtime_json_bytes(classes, sort_keys=True)
    if args.check and (not class_path.is_file() or class_path.read_bytes() != class_data):
        raise ValueError('Shared trainer class names differ: ' + str(class_path))
    publish_trainers(args.generated_root, definitions, check=args.check)
    if not args.check:
        class_path.parent.mkdir(parents=True, exist_ok=True)
        if not class_path.is_file() or class_path.read_bytes() != class_data:
            class_path.write_bytes(class_data)
    if set(read_trainers(args.generated_root)) != set(identities):
        raise ValueError('Shared trainer table contains unreferenced definitions')
    print(json.dumps(dict(checked=args.check, trainers=len(definitions), members=sum(len(t['party']) for t in definitions))))


if __name__ == '__main__': main()
