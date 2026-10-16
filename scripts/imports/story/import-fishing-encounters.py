#!/usr/bin/env python3
"""Generate every ordinary FireRed fishing table from retained pinned raw data."""
import argparse
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT
from fishing_tables import convert


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated/data/encounters/fishing.json')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    source = convert(json.loads((args.raw / 'src/data/wild_encounters.json').read_text(encoding='utf-8')))
    document = dict(schemaVersion=1, tables=source['tables'])
    payload = (json.dumps(document, sort_keys=True, separators=(',', ':')) + '\n').encode()
    if args.check:
        if not args.out.is_file() or args.out.read_bytes() != payload:
            raise ValueError('Generated ordinary fishing tables differ')
    else:
        if args.out.resolve().is_relative_to(args.raw.resolve()):
            raise ValueError('Refusing source overwrite')
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_bytes(payload)
    print(json.dumps(dict(tables=len(document['tables']), checked=args.check)))


if __name__ == '__main__':
    main()
