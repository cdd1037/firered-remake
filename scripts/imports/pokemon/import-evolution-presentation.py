#!/usr/bin/env python3
"""Build recipe-selected evolution presentations from pinned source and shared assets."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))

import argparse
import json
from project_paths import PROJECT_ROOT
from pokemon_import_io import safe
from evolution_presentation import RECIPE, build


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--selection', required=True)
    parser.add_argument('--recipe', type=Path, default=RECIPE)
    parser.add_argument('--reference-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--dependency-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dependency = safe(args.project_root), safe(args.dependency_root)
    reference = safe(args.reference_root) if args.reference_root is not None else None
    if reference is not None and (target == reference or target.is_relative_to(reference) or reference.is_relative_to(target)):
        raise ValueError('Source/output overlap')
    print(json.dumps(build(args.selection, target, dependency, reference, args.check, args.recipe), sort_keys=True))


if __name__ == '__main__':
    main()
