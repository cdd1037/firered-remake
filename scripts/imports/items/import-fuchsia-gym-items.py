#!/usr/bin/env python3
"""Regenerate the Fuchsia Gym TM06 item description and exact source icon."""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from item_catalog import described_items, item_json
from item_source import ItemSource
from pokemon_import_io import safe, load_recipe, read_sources, write_files
from presentation_source import SOURCE_COMMIT
from project_paths import PROJECT_ROOT
from runtime_json import runtime_json_bytes
from generated_output_compare import check_item_outputs

PROFILE = 'firered-fuchsia-gym-items-v1'


def item_outputs(lock, sources, project):
    items = described_items(sources.__getitem__, ['ITEM_TM06'])
    icon, png = ItemSource(sources).icon('ITEM_TM06', 'item_tm06.png')
    return {'fuchsia-gym-items.json': runtime_json_bytes(
                dict(schemaVersion=1, items=items, icons=[icon]), sort_keys=True, default=item_json),
            'item_tm06.png': png}


def run(root, reference=None, check=False, raw_root=None):
    root = safe(root)
    raw = safe(raw_root or PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    output = safe(root, 'content/generated/data/items/fuchsia-gym')
    if raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if reference is not None:
        repository = safe(reference).resolve()
        if repository.is_relative_to(output.resolve()) or output.resolve().is_relative_to(repository):
            raise ValueError('Output must not overlap the reference repository')
    recipe = load_recipe(PROJECT_ROOT / 'scripts/recipes/items/fuchsia-gym-items.lock.json',
                         commit=SOURCE_COMMIT, profile=PROFILE)
    sources = read_sources(recipe, raw, reference)
    outputs = item_outputs(recipe, sources, PROJECT_ROOT)
    if check:
        check_item_outputs(root / 'content/generated', {'data/items/fuchsia-gym/' + path: data for path, data in outputs.items()})
        return dict(inputCount=len(sources), outputCount=len(outputs), checked=True)
    outputs['manifest.json'] = (json.dumps(dict(schemaVersion=1, inputCount=len(sources),
        outputs=[dict(path=path) for path in sorted(outputs)]), sort_keys=True, separators=(',', ':')) + '\n').encode()
    write_files(output, outputs)
    return dict(inputCount=len(sources), outputCount=len(outputs), checked=check)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--target-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw-root', type=Path)
    parser.add_argument('--reference-repo', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.target_root, args.reference_repo, args.check, args.raw_root), sort_keys=True))


if __name__ == '__main__':
    main()
