#!/usr/bin/env python3
"""Import the four missing Lost Cave/Selphy inventory definitions and source icons.
REFERENCE DEPENDENCY OUTPUT [--check]. Reads pinned Git blobs; never executes upstream code.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/data/items/lost-cave/'
RAW = 'content/raw/pokefirered/037335f4/'
PROFILE = 'firered-ordinary-item-supplement-v1'
SYMBOLS = ['ITEM_SILK_SCARF', 'ITEM_SEA_INCENSE', 'ITEM_LAX_INCENSE', 'ITEM_LUXURY_BALL']
CONTRACT = 'content/generated/maps/regions/lost-cave/contracts.json'
SHARED = {
    'src/data/items.json': 'content/raw/pokefirered/037335f4/src/data/items.json',
    'include/constants/items.h': 'content/raw/pokefirered/037335f4/include/constants/items.h',
    'src/field_specials.c': 'content/raw/pokefirered/037335f4/src/field_specials.c',
    'src/data/item_icon_table.h': 'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
    'src/data/graphics/items.h': 'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
}

def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value, indent=2, ensure_ascii=False, default=item_json) + '\n').encode()

def generate(reference, dependency):
    inputs, raw, cache, generated = {}, {}, {}, {}
    def read(path):
        if path in cache: return cache[path]
        data = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
        local = SHARED.get(path, RAW + path)
        if path in SHARED: need((dependency / local).read_bytes() == data, 'Shared source bytes: ' + path)
        else: raw[local] = data
        inputs[path] = dict(path=path, localPath=local, shared=path in SHARED)
        cache[path] = data
        return data
    shared_item = ItemSource(read).definition
    def text(path): return read(path).decode()
    def one(path, pattern):
        found = list(re.finditer(pattern, text(path), re.M | re.S))
        need(len(found) == 1, 'Exact source declaration: ' + pattern)
        return found[0]
    def source(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text(path).count('\n', 0, match.start()) + 1,
                    endLine=text(path).count('\n', 0, match.end()) + 1)
    all_items = json.loads(text('src/data/items.json'))['items']
    def metadata(symbol):
        item = next(i for i in all_items if i['itemId'] == symbol)
        block = one('src/data/items.json', r'^    \{\n      "english": "' + re.escape(item['english']) + r'",.*?^    \}')
        number = int(one('include/constants/items.h', r'^#define ' + symbol + r'\s+(\d+)\s*$')[1])
        ref = source('src/data/items.json', symbol, block)
        meta = shared_item(symbol)
        return item, meta
    additions, icons = [], []
    for symbol, effect, parameter, price in [
            ('ITEM_SILK_SCARF', 'NORMAL_POWER', 10, 100), ('ITEM_SEA_INCENSE', 'WATER_POWER', 5, 9600),
            ('ITEM_LAX_INCENSE', 'EVASION_UP', 5, 9600), ('ITEM_LUXURY_BALL', 'NONE', 0, 1000)]:
        item, meta = metadata(symbol)
        ball = symbol == 'ITEM_LUXURY_BALL'
        need(item['holdEffect'] == 'HOLD_EFFECT_' + effect and item['holdEffectParam'] == parameter and item['price'] == price and
             item['importance'] == 0 and item['registrability'] == 0 and
             item['pocket'] == ('POCKET_POKE_BALLS' if ball else 'POCKET_ITEMS') and
             item['fieldUseFunc'] == ('NULL' if ball else 'FieldUseFunc_OakStopsYou') and
             item['battleUsage'] == (2 if ball else 0) and item['battleUseFunc'] == ('BattleUseFunc_PokeBallEtc' if ball else 'NULL'), 'Source item semantics')
        addition = dict(item=meta)
        addition.update(description=item['description_english'].replace('\\n', '\n'), healHp=0, cureStatus=0, effectBytes=[], useSupport=0 if ball else 10)
        additions.append(addition)
        icon_path = 'src/data/item_icon_table.h'
        binding = one(icon_path, r'^    \[' + symbol + r'\]\s*= \{(\w+), (\w+)\},$')
        image = one('src/data/graphics/items.h', r'^const u32 ' + binding[1] + r'\[\]\s*= INCBIN_U32\("([^"\n]+)\.4bpp.lz"\);')[1] + '.png'
        palette = one('src/data/graphics/items.h', r'^const u32 ' + binding[2] + r'\[\]\s*= INCBIN_U32\("([^"\n]+)\.gbapal.lz"\);')[1] + '.pal'
        im = Image.open(io.BytesIO(read(image)))
        need(im.mode == 'P' and im.size == (24, 24) and max(im.tobytes()) < 16, 'Original icon indices')
        lines = text(palette).splitlines()
        need(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Original palette')
        colors = [tuple(((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split()) for line in lines[3:]]
        rgba = bytes(v for index in im.tobytes() for v in (*colors[index], 255 if index else 0))
        stream = io.BytesIO(); Image.frombytes('RGBA', (24, 24), rgba).save(stream, format='PNG', compress_level=9)
        png = stream.getvalue(); stem = symbol[5:].lower().replace('_', '-')
        icon = dict(item=meta['id'], width=24, height=24, texturePath=BASE + stem + '.png',  bindingSource=source(icon_path, symbol, binding))
        icons.append(icon); generated[icon['texturePath']] = png
    contract_bytes = (dependency / CONTRACT).read_bytes()
    contract = json.loads(contract_bytes)
    pickups = [row['item'] for row in contract['itemPickups']]
    pickups += [row['event']['item'] for row in contract['hiddenItems']]
    reward_path = 'src/field_specials.c'
    deluxe = one(reward_path, r'^static const u16 sResortGorgeousDeluxeRewards\[\] = \{(.*?)^\};')
    reward = one(reward_path, r'^static u16 SampleResortGorgeousReward\(void\)\n\{.*?^\}')
    rewards = ['ITEM_LUXURY_BALL'] + re.findall(r'ITEM_\w+', deluxe[1])
    need(len(pickups) == 9 and len(rewards) == 7 and 'return ITEM_LUXURY_BALL;' in reward[0], 'Complete nine pickup/seven reward scope')
    need(rewards == contract['serviceBindings'][0]['itemSymbols'], 'Selphy reward source matches service binding')
    coverage = []
    for symbol in dict.fromkeys(pickups + rewards):
        item, meta = metadata(symbol)
        coverage.append(dict(item=meta['id'], description=item['description_english'].replace('\\n', '\n'), added=symbol in SYMBOLS))
    items = dict(  previousItemCount=147, itemCount=151, additions=additions,
                  shopsAdded=[], icons=icons)
    facts = dict(  pickupItemSymbols=pickups, rewardItemSymbols=rewards,
                 coverage=coverage, deluxeRewardsSource=source(reward_path, 'sResortGorgeousDeluxeRewards', deluxe),
                 rewardSelectionSource=source(reward_path, 'SampleResortGorgeousReward', reward), acquisitionRoutesAdded=[])

    files = {BASE + 'items.json': encoded(items), BASE + 'source-contract.json': encoded(facts),
              **generated, **raw}
    return files, items

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ['reference', 'dependency', 'output']: parser.add_argument(arg, type=Path)
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    files, items = generate(args.reference, args.dependency)
    for path, data in files.items():
        target = args.output / path
        if args.check: need(target.read_bytes() == data, 'Regeneration differs: ' + path)
        else: target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    print(json.dumps(dict(checked=args.check, itemCount=items['itemCount']), indent=2, default=item_json))
