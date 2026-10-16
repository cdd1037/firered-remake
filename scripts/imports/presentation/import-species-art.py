#!/usr/bin/env python3
"""Rebuild selected source species art, preserving existing runtime paths."""
import argparse
import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT
from species_art import build, safe
from generated_output_compare import generated_output_matches


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', required=True)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw-root', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output = args.output or args.project_root / 'content/generated/sprites/pokemon' / args.profile
    raw = args.raw_root or args.project_root / 'content/raw/pokefirered/037335f4'
    if output.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(output.resolve()):
        raise ValueError('Raw and output roots must be disjoint')
    results = build(args.project_root, args.profile, raw)
    targets = [(safe(output, name), data) for name, data in results.items()]
    for path, data in targets:
        if args.check:
            if not generated_output_matches(path, data):
                raise ValueError("Generated art differs: " + str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(passed=True, profile=args.profile, files=len(results))))


if __name__ == '__main__':
    main()
