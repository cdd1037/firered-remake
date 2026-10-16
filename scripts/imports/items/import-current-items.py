#!/usr/bin/env python3
"""Compile current item admission and ordered Mart stock directly from pinned source.

The recipe selects capabilities and availability. It contains no business
metadata, source spans, predecessor profiles, copied descriptions or effects.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
import re
from item_catalog import ROOT, RAW, COMMIT, item_definition, inventory_item, item_json
from item_effects import read_effects
from machine_catalog import load_catalog
from item_icon_exports import item_icon_path, decode_icon_page
from PIL import Image

RECIPE = Path('scripts/recipes/config/current-item-admissions.json')
TARGET = Path('content/generated/data/available-items.json')
USES = {'separateCapture', 'potion', 'statusCure', 'deferredSleep', 'deferredEscape',
        'deferredRepel', 'fieldRevive', 'fieldEther', 'deferredRareCandy',
        'separateEvolution', 'noFieldUse', 'fossil', 'fieldRestoreAllPp'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate member: ' + key)
        result[key] = value
    return result


def read_recipe(root):
    return json.loads((root / RECIPE).read_text(encoding='utf-8'), object_pairs_hook=unique)


def compile_catalog(root, recipe=None):
    root = Path(root).resolve()
    recipe = read_recipe(root) if recipe is None else recipe
    require(set(recipe) == {'schemaVersion', 'sourceCommit', 'items', 'shops'} and
            recipe['schemaVersion'] == 1 and recipe['sourceCommit'] == COMMIT, 'Current item recipe schema')
    raw = root / RAW
    source = {row['itemId']: row for row in json.loads((raw / 'src/data/items.json').read_text())['items']}
    effects = read_effects(raw)
    machines = load_catalog(root)
    battle = (raw / 'include/constants/battle.h').read_text()
    status_bits = {status: 1 << int(re.search(r'^#define STATUS1_' + status + r'\s+\(1 << (\d+)\)', battle, re.M)[1])
                   for status in ('POISON', 'BURN', 'FREEZE', 'PARALYSIS')}
    cure_bits = {16: status_bits['POISON'], 8: status_bits['BURN'],
                 4: status_bits['FREEZE'], 2: status_bits['PARALYSIS']}
    items, admitted = [], set()
    for entry in recipe['items']:
        require({'item', 'useSupport'} <= entry.keys() <= {'item', 'useSupport', 'effects', 'description', 'icon'},
                'Current item entry schema')
        require(type(entry.get('icon', False)) is bool, 'Invalid icon admission')
        item = item_definition(entry['item'], root)
        require(item.identity not in admitted, 'Duplicate current item: ' + item.identity)
        admitted.add(item.identity)
        support = entry['useSupport']
        require(support in USES, 'Unknown item use: ' + str(support))
        require(type(entry.get('effects', False)) is bool, 'Invalid effect admission')
        effect = effects[item['sourceItemSymbol']] if entry.get('effects', False) else []
        require(support not in ('potion', 'statusCure') or bool(effect), 'Medicine effects missing')
        heal = effect[6] if support == 'potion' else 0
        cure = cure_bits[effect[3]] if support == 'statusCure' else 0
        description = source[item['sourceItemSymbol']]['description_english'].replace('\\n', '\n')
        if 'description' in entry:
            require(entry['description'] == 'machine', 'Unknown item description selection')
            description = machines.machines[item.identity]['machine']['description']
        items.append(inventory_item(item, description, heal, cure, effect, support))
    shops, shop_ids = [], set()
    for entry in recipe['shops']:
        require(set(entry) == {'id', 'path'}, 'Current stock entry schema')
        label, path = entry['id'], Path(entry['path'])
        require(label not in shop_ids, 'Duplicate current shop: ' + label)
        shop_ids.add(label)
        require(not path.is_absolute() and '..' not in path.parts, 'Invalid stock source path')
        source_text = (raw / path).read_text()
        matches = re.findall(r'^' + re.escape(label) + r'::?\s*\n((?:\s*\.2byte ITEM_\w+\s*\n)+)', source_text, re.M)
        require(len(matches) == 1, 'Current stock label missing or ambiguous: ' + label)
        symbols = re.findall(r'\.2byte (ITEM_\w+)', matches[0])
        require(symbols[-1] == 'ITEM_NONE' and symbols.count('ITEM_NONE') == 1, 'Stock terminator missing: ' + label)
        stock = [item_definition(symbol, root).identity for symbol in symbols[:-1]]
        require(stock and len(stock) == len(set(stock)) and set(stock) <= admitted, 'Invalid stock admission: ' + label)
        require(all(item_definition(symbol, root)['price'] > 0 for symbol in symbols[:-1]), 'Unpriced stock: ' + label)
        shops.append(dict(id=label, items=[dict(value=item) for item in stock]))
    require(items and shops, 'Current items and Mart stocks are required')
    constants = (raw / 'include/constants/global.h').read_text()
    def constant(symbol):
        return int(re.search(r'^#define ' + symbol + r'\s+(\d+)\b', constants, re.M)[1])
    stack_limits = {int(value) for value in re.findall(r'quantity \+ count <= (\d+)', (raw / 'src/item.c').read_text())}
    require(stack_limits == {999}, 'Item stack limits differ')
    maximum_money = int(re.search(r'^#define MAX_MONEY (\d+)', (raw / 'src/money.c').read_text(), re.M)[1])
    maximum_quantity = int(re.search(r'if \(maxQuantity > (\d+)\)\s+sShopData.maxQuantity = \1;', (raw / 'src/shop.c').read_text())[1])
    return dict(limits=dict(bagItemSlots=constant('BAG_ITEMS_COUNT'), pcItemSlots=constant('PC_ITEMS_COUNT'), maximumStack=999),
                questLimits=dict(keyItemSlots=constant('BAG_KEYITEMS_COUNT'), ballSlots=constant('BAG_POKEBALLS_COUNT'), maximumStack=999),
                maximumTransactionQuantity=maximum_quantity, maximumMoney=maximum_money, items=items, shops=shops)


def import_icons(root, check=False):
    """Render explicitly selected current-item icons with the common source table/palette recipe.

    These supplement retained icon families; runtime consumers need no region-specific loader.
    A check compares logical pixels whether the image is standalone or already atlased.
    """
    raw = root / RAW
    table = (raw / 'src/data/item_icon_table.h').read_text()
    declarations = (raw / 'src/data/graphics/items.h').read_text()
    icons, images = [], {}
    for entry in read_recipe(root)['items']:
        if not entry.get('icon', False):
            continue
        symbol = item_definition(entry['item'], root)['sourceItemSymbol']
        binding = re.findall(r'^\s*\[' + re.escape(symbol) + r'\]\s*= \{(\w+), (\w+)\},$', table, re.M)
        require(len(binding) == 1, 'Missing source icon binding: ' + symbol)
        paths = []
        for name, suffix in zip(binding[0], ('.4bpp.lz', '.gbapal.lz')):
            matches = re.findall(r'^const u32 ' + re.escape(name) + r'\[\] = INCBIN_U32\("([^"\n]+)' + re.escape(suffix) + r'"\);$', declarations, re.M)
            require(len(matches) == 1, 'Missing source icon declaration: ' + name)
            relative = matches[0] + ('.png' if suffix == '.4bpp.lz' else '.pal')
            require(not Path(relative).is_absolute() and '..' not in Path(relative).parts, 'Unsafe icon source path')
            paths.append(relative)
        with Image.open(raw / paths[0]) as indexed:
            require(indexed.mode == 'P' and indexed.size == (24, 24) and max(indexed.tobytes()) < 16,
                    'Invalid source item icon: ' + symbol)
            indices = indexed.tobytes()
        lines = (raw / paths[1]).read_text().splitlines()
        require(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Invalid item palette')
        colors = [tuple(int(value) for value in line.split()) for line in lines[3:]]
        require(all(len(color) == 3 and all(0 <= value <= 255 for value in color) for color in colors), 'Invalid palette RGB')
        colors = [tuple(((value >> 3) << 3) | ((value >> 3) >> 2) for value in color) for color in colors]
        pixels = bytes(value for index in indices for value in (*colors[index], 255 if index else 0))
        image = Image.frombytes('RGBA', (24, 24), pixels)
        path = item_icon_path(*paths)
        icons.append(dict(item=dict(value=entry['item']), width=24, height=24, pngPath=path))
        images[entry['item']] = image
    target = root / 'content/generated/data/items/current-icons.json'
    if check:
        actual = json.loads(target.read_text())['icons']
        require(len(actual) == len(icons), 'Current icon count differs')
        for expected, row in zip(icons, actual):
            require(all(row[key] == expected[key] for key in ('item', 'width', 'height')), 'Current icon identity differs')
            path = Path(row['pngPath'])
            require(not path.is_absolute() and '..' not in path.parts, 'Unsafe icon output path')
            image = decode_icon_page((root / path).read_bytes(), row)
            require(image.tobytes() == images[row['item']['value']].tobytes(), 'Current icon RGBA differs')
    else:
        for icon in icons:
            destination = root / icon['pngPath']
            destination.parent.mkdir(parents=True, exist_ok=True)
            images[icon['item']['value']].save(destination, format='PNG')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(dict(icons=icons), separators=(',', ':')) + '\n')
    return len(icons)


def run(root, check=False):
    document = compile_catalog(root)
    data = (json.dumps(document, ensure_ascii=False, separators=(',', ':'), default=item_json) + '\n').encode()
    target = root / TARGET
    if check:
        require(target.read_bytes() == data, 'Current item regeneration differs')
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    icons = import_icons(root, check)
    return dict(checked=check, items=len(document['items']), shops=len(document['shops']), icons=icons, bytes=len(data))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root.resolve(), args.check)))
