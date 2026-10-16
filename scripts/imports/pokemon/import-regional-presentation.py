#!/usr/bin/env python3
"""Rebuild selected Pokemon battle art, icons, footprints and supplements offline."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))

import argparse
import json
import os
from project_paths import PROJECT_ROOT
from pokemon_presentation import RECIPE, import_content


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--region', required=True, help='Selection in the presentation recipe')
    parser.add_argument('--recipe', type=Path, default=RECIPE)
    parser.add_argument('--lock', type=Path)
    parser.add_argument('--raw', type=Path, help='Explicit local inputs; no implicit Git fallback')
    parser.add_argument('--out', type=Path)
    parser.add_argument('--reference-repo', type=Path)
    args = parser.parse_args()
    raw = args.raw or PROJECT_ROOT / 'content/raw/pokefirered/037335f4'
    reference = args.reference_repo
    if reference is None and args.raw is None:
        configured = os.environ.get('FIRERED_ORIGINAL_REFERENCE')
        if configured:
            reference = Path(configured)
        elif not raw.exists():
            reference = Path((PROJECT_ROOT / '.tools/reference-root').read_text().strip())
    out = args.out or PROJECT_ROOT / 'content/generated/sprites/pokemon' / args.region
    result = import_content(args.region, raw, out, reference, args.lock, args.recipe)
    print(json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    main()
