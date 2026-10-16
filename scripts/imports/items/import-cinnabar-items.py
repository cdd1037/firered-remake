#!/usr/bin/env python3
"""Import Cinnabar item descriptions and its original Mart stock from pinned inputs."""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from item_catalog import described_items, item_json
from pokemon_import_io import SourceReader, safe, write_files
from presentation_source import SOURCE_COMMIT
from project_paths import PROJECT_ROOT
from generated_output_compare import generated_output_matches

ITEMS = ('SECRET_KEY', 'TM14', 'TM22', 'TM38')


def encoded(document):
    return (json.dumps(document, sort_keys=True, indent=2, default=item_json) + '\n').encode()


def convert(source, dependency):
    items = described_items(source.read, ['ITEM_' + name for name in ITEMS])
    script = source.text('data/maps/CinnabarIsland_Mart/scripts.inc')
    matches = list(re.finditer(r'^CinnabarIsland_Mart_Items::\s*\n((?:\s*\.2byte ITEM_\w+\n)+)', script, re.M))
    if len(matches) != 1:
        raise ValueError('Expected one Cinnabar Mart stock')
    stock = re.findall(r'\.2byte (ITEM_\w+)', matches[0][1])
    if stock != ['ITEM_ULTRA_BALL', 'ITEM_GREAT_BALL', 'ITEM_HYPER_POTION', 'ITEM_REVIVE',
                 'ITEM_FULL_HEAL', 'ITEM_ESCAPE_ROPE', 'ITEM_MAX_REPEL', 'ITEM_NONE']:
        raise ValueError('Cinnabar Mart source stock differs')
    return dict(schemaVersion=1, items=items,
                mart=dict(id='CinnabarIsland_Mart_Items', items=[dict(value='pret:' + symbol) for symbol in stock[:-1]]))


def generate(reference, dependency, raw_root=None):
    source = SourceReader(raw_root or PROJECT_ROOT / 'content/raw/pokefirered/037335f4',
                          commit=SOURCE_COMMIT, repository=reference)
    items = convert(source, dependency)
    relative = 'content/generated/data/items/cinnabar/items.json'
    outputs = {relative: encoded(items),
               'content/generated/data/items/cinnabar/manifest.json':
                   encoded(dict(schemaVersion=1, outputs=[dict(path=relative)]))}
    outputs.update({'content/raw/pokefirered/037335f4/' + path: data for path, data in source.data.items()})
    return outputs, items


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path)
    parser.add_argument('--raw-root', type=Path)
    parser.add_argument('--dependency-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs, items = generate(args.reference_root, args.dependency_root, args.raw_root)
    if args.check:
        for path, data in outputs.items():
            if not generated_output_matches(safe(args.project_root, path), data):
                raise ValueError('Regeneration differs: ' + path)
    else:
        write_files(args.project_root, outputs)
    print(json.dumps(dict(checked=args.check, items=len(items['items']), shopItems=len(items['mart']['items']))))


if __name__ == '__main__':
    main()
