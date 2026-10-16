#!/usr/bin/env python3
"""Convert selected trainer portraits from pinned raw or Git inputs, offline."""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT
from trainer_portraits import Sources, build, publish, recipe, safe


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--group', action='append', required=True, help='Recipe group; repeat to rebuild several groups')
    source = parser.add_mutually_exclusive_group()
    source.add_argument('--raw-root', type=Path, help='Trusted pinned raw cache; default: project content/raw')
    source.add_argument('--reference-root', type=Path, help='Read pinned Git blobs, never checkout scripts')
    parser.add_argument('--local-palettes', type=Path, help='Root containing forest-trainer-art local palette variants')
    parser.add_argument('--output', type=Path, help='Override output directory for a single selected group')
    parser.add_argument('--check', action='store_true', help='Compare all JSON values and full decoded RGBA, without writing')
    args = parser.parse_args()
    document = recipe(args.project_root)
    if len(set(args.group)) != len(args.group) or any(g not in document['groups'] for g in args.group):
        parser.error('Groups must be known and unique: ' + ', '.join(document['groups']))
    if args.output and len(args.group) != 1:
        parser.error('--output requires exactly one --group')
    sources = Sources(args.project_root, args.raw_root, args.reference_root, args.local_palettes)
    pending = []
    for name in args.group:
        selection = document['groups'][name]
        output = args.output or safe(args.project_root / 'content/generated', selection['output'])
        for input_root in (sources.raw, sources.local_palettes, sources.reference):
            if input_root and (output.resolve().is_relative_to(input_root.resolve()) or input_root.resolve().is_relative_to(output.resolve())):
                parser.error('Portrait input/output directories overlap')
        pending.append((name, output, build(sources, selection, document['portraits'])))
    for name, output, outputs in pending:
        publish(outputs, output, check=args.check)
        print(json.dumps({'group': name, 'outputs': len(outputs), 'status': 'checked' if args.check else 'written'}))


if __name__ == '__main__':
    main()
