#!/usr/bin/env python3
"""Compile the three city gift/exchange items and their original icons from raw."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
import argparse
import json
from item_source import ItemSource
from generated_output_compare import check_item_outputs
from project_paths import PROJECT_ROOT
from runtime_json import runtime_json_bytes
from world_source import WorldSource

ITEMS = ('ITEM_BIKE_VOUCHER', 'ITEM_OLD_ROD', 'ITEM_BICYCLE')


def generate(raw):
    source = WorldSource(raw)
    icons = ItemSource(source.data)
    originals = {row['itemId']: row for row in source.json('src/data/items.json')['items']}
    items, images, outputs = [], [], {}
    for symbol in ITEMS:
        original = originals[symbol]
        if original['pocket'] != 'POCKET_KEY_ITEMS' or original['price'] != 0:
            raise ValueError('City key item definition differs: ' + symbol)
        items.append(dict(item='pret:' + symbol, description=original['description_english'].replace('\\n', '\n')))
        icon, png = icons.icon(symbol)
        images.append(icon)
        outputs[icon['pngPath'].removeprefix('content/generated/')] = png
    outputs['maps/regions/city-services/items.json'] = runtime_json_bytes(dict(schemaVersion=1, items=items, icons=images), sort_keys=True)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated')
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    outputs = generate(args.raw)
    if args.check:
        check_item_outputs(args.out, outputs)
    else:
        for relative, data in outputs.items():
            target = args.out / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(items=len(ITEMS))))


if __name__ == '__main__': main()
