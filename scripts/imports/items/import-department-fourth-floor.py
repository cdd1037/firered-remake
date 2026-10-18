#!/usr/bin/env python3
"""Compile the selected item icon from retained raw files or a pinned Git revision.

RAW OUTPUT [--repository REPOSITORY] [--check]. OUTPUT is a project root.
Item definitions and shop availability belong to the canonical item compiler.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))

import argparse
import json
from generated_output_compare import check_item_outputs
from item_source import ItemSource
from pokemon_import_io import SourceReader, canonical, safe, write_files

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SYMBOL = 'ITEM_RETRO_MAIL'
DOCUMENT = 'content/generated/data/services/department-fourth-floor/items.json'


def generate(raw, repository=None):
    source = SourceReader(raw, commit=COMMIT, repository=repository)
    icon, png = ItemSource(source.read).icon(SYMBOL)
    document = dict(schemaVersion=1, icons=[icon])
    return {DOCUMENT: canonical(document), icon['pngPath']: png}, document


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('raw', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--repository', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    files, document = generate(safe(args.raw), args.repository)
    if args.check:
        check_item_outputs(safe(args.output) / 'content/generated',
            {path.removeprefix('content/generated/'): data for path, data in files.items()})
    else:
        write_files(args.output, files)
    print(json.dumps(dict(checked=args.check, icons=len(document['icons']))))


if __name__ == '__main__':
    main()
