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
        require({'item', 'useSupport'} <= entry.keys() <= {'item', 'useSupport', 'effects', 'description'},
                'Current item entry schema')
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


def run(root, check=False):
    document = compile_catalog(root)
    data = (json.dumps(document, ensure_ascii=False, separators=(',', ':'), default=item_json) + '\n').encode()
    target = root / TARGET
    if check:
        require(target.read_bytes() == data, 'Current item regeneration differs')
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return dict(checked=check, items=len(document['items']), shops=len(document['shops']), bytes=len(data))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root.resolve(), args.check)))
