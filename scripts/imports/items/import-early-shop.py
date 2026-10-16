#!/usr/bin/env python3
"""Pinned, local-only early Mart extraction. Never executes upstream programs or downloads a ROM."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import item_icon_path, output_materials
from image_io import indexed_png, png
from ui_tiles import palette, rgba
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-viridian-pewter-shop-v1'
SYMBOLS = ['ITEM_POKE_BALL', 'ITEM_POTION', 'ITEM_ANTIDOTE', 'ITEM_PARALYZE_HEAL',
           'ITEM_AWAKENING', 'ITEM_BURN_HEAL', 'ITEM_ESCAPE_ROPE', 'ITEM_REPEL']
TEXTS = ['gText_ShopBuy', 'gText_ShopSell', 'gText_ShopQuit', 'gText_InBagVar1', 'gText_QuitShopping',
         'gText_Var1CertainlyHowMany', 'gText_Var1AndYouWantedVar2', 'gText_HereYouGoThankYou',
         'gText_YouDontHaveMoney', 'gText_NoMoreRoomForThis', 'gText_AnythingElseICanHelp',
         'gText_OhNoICantBuyThat', 'gText_HowManyWouldYouLikeToSell',
         'gText_ICanPayThisMuch_WouldThatBeOkay', 'gText_TurnedOverItemsWorthYen', 'gText_PokedollarVar1']


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read_lock():
    data = (ROOT / 'scripts/recipes/items/early-shop.lock.json').read_bytes()
    lock = json.loads(data, object_pairs_hook=unique)
    if (lock['commit'], lock['profile'], lock['itemSymbols'], len(lock['files'])) != (COMMIT, PROFILE, SYMBOLS, 35):
        raise ValueError('Early-shop source scope differs')
    return lock


def materialize(repo, raw):
    collected = []
    for entry in read_lock()['files']:
        data = subprocess.check_output(['git', '-C', str(repo), 'show', COMMIT + ':' + entry['path']])
        collected.append((entry['path'], data))
    for path, data in collected:
        dest = raw / path
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)


def convert(raw, out):
    lock = read_lock()
    inputs = {entry['path']: entry for entry in lock['files']}
    sources = {}
    for path, entry in inputs.items():
        data = (raw / path).read_bytes()
        sources[path] = data
    def text(path):
        return sources[path].decode('utf-8')
    def match(path, pattern):
        matches = list(re.finditer(pattern, text(path), re.M))
        if len(matches) != 1:
            raise ValueError('Expected exactly one source match: ' + path + ' / ' + pattern)
        return matches[0]
    def reference(path, symbol, m=None):
        start = 1 if m is None else text(path)[:m.start()].count('\n') + 1
        end = max(1, text(path).count('\n')) if m is None else text(path)[:m.end()].count('\n') + 1
        return dict(path=path, symbol=symbol, startLine=start, endLine=end)
    def number(path, symbol):
        value = match(path, r'^#define\s+' + re.escape(symbol) + r'\s+(0x[0-9a-fA-F]+|\d+)\b')[1]
        return int(value, 0)
    rules = []
    for path, symbol, needle in [
        ('src/shop.c', 'Task_BuyHowManyDialogueInit', 'maxQuantity = GetMoney(&gSaveBlock1Ptr->money) / ItemId_GetPrice(tItemId);'),
        ('src/shop.c', 'BuyMenuTryMakePurchase', 'if (AddBagItem(tItemId, tItemCount) == TRUE)'),
        ('src/item_menu.c', 'Task_ItemContext_Sell', 'if (data[2] > 99)'),
        ('src/item_menu.c', 'SellItem', 'AddMoney(&gSaveBlock1Ptr->money, ItemId_GetPrice(gSpecialVar_ItemId) / 2 * data[8]);'),
        ('src/money.c', 'AddMoney', 'if (toSet + toAdd > MAX_MONEY)'),
        ('src/item.c', 'AddBagItem', 'if (quantity + count <= 999)'),
        ('src/pokemon.c', 'HealStatusConditions', 'status &= ~healMask;'),
        ('src/party_menu.c', 'ItemUseCB_MedicineStep', 'RemoveBagItem(item, 1);'),
        ('tools/gbagfx/convert_png.c', 'ConvertBitDepth', '% (1 << destBitDepth)')]:
        if needle not in text(path):
            raise ValueError('Source semantic anchor differs: ' + symbol)
        # A whole-file source reference avoids presenting a fragment as a complete function.
        rules.append(reference(path, symbol))
    maximum_money = number('src/money.c', 'MAX_MONEY')
    if maximum_money != 999999 or 'sShopData.maxQuantity = 99;' not in text('src/shop.c'):
        raise ValueError('Money or menu limits differ')
    shops = []
    for city, count in [('ViridianCity', 4), ('PewterCity', 8)]:
        path = 'data/maps/' + city + '_Mart/scripts.inc'
        symbol = city + '_Mart_Items'
        m = match(path, r'^' + symbol + r'::\n((?:\s*\.2byte ITEM_\w+\n)+)')
        stock = re.findall(r'\.2byte (ITEM_\w+)', m[1])
        if stock != SYMBOLS[:count] + ['ITEM_NONE']:
            raise ValueError('Mart stock or terminal ITEM_NONE differs')
        shops.append(dict(id=symbol, items=[dict(value='pret:' + i) for i in stock[:-1]]))
    source_items = json.loads(text('src/data/items.json'), object_pairs_hook=unique)['items']
    items = []
    effect_specs = {'ITEM_POTION': ('Potion', 'ITEM4_HEAL_HP', 4, 'potion', 0),
                    'ITEM_ANTIDOTE': ('Antidote', 'ITEM3_POISON', 3, 'statusCure', 8),
                    'ITEM_BURN_HEAL': ('BurnHeal', 'ITEM3_BURN', 3, 'statusCure', 16),
                    'ITEM_PARALYZE_HEAL': ('ParalyzeHeal', 'ITEM3_PARALYSIS', 3, 'statusCure', 64),
                    'ITEM_AWAKENING': ('Awakening', 'ITEM3_SLEEP', 3, 'deferredSleep', 0)}
    for symbol in SYMBOLS:
        found = [i for i in source_items if i.get('itemId') == symbol]
        if len(found) != 1:
            raise ValueError('Missing or duplicate item ' + symbol)
        i = found[0]
        item_match = match('src/data/items.json', r'^    \{\n(?:(?!^    \}).*\n)*?      "itemId": "' + symbol + r'",\n(?:(?!^    \}).*\n)*?    \}')
        effects, effect_source, heal, cure = [], None, 0, 0
        support = {'ITEM_POKE_BALL': 'separateCapture', 'ITEM_ESCAPE_ROPE': 'deferredEscape', 'ITEM_REPEL': 'deferredRepel'}.get(symbol)
        if symbol in effect_specs:
            name, flag, offset, support, cure = effect_specs[symbol]
            ep = 'src/data/pokemon/item_effects.h'
            em = match(ep, r'^static const u8 sItemEffect_' + name + r'\[(\d+)\] = \{\n((?:.*\n)*?)\};')
            effects = [0] * int(em[1])
            assignments = re.findall(r'^\s*\[(\d+)\] = (\w+),', em[2], re.M)
            expected = [('4', 'ITEM4_HEAL_HP'), ('6', '20')] if symbol == 'ITEM_POTION' else [('3', flag)]
            if assignments != expected:
                raise ValueError('Medicine effect shape differs')
            effects[offset] = number('include/constants/item_effects.h', flag)
            if symbol == 'ITEM_POTION':
                effects[6] = heal = int(assignments[1][1])
            effect_source = reference(ep, 'sItemEffect_' + name, em)
        items.append(inventory_item(shared_item(symbol), i['description_english'].replace(r'\n', '\n'), heal, cure, effects, support))
    for status, expected in [('POISON', 8), ('BURN', 16), ('PARALYSIS', 64)]:
        shift = int(match('include/constants/battle.h', r'^#define STATUS1_' + status + r'\s+\(1 << (\d+)\)')[1])
        if (1 << shift) != expected:
            raise ValueError('Supported status1 bit differs')
    icons, icon_outputs = [], {}
    for symbol in SYMBOLS:
        table = 'src/data/item_icon_table.h'
        m = match(table, r'^\s*\[' + symbol + r'\]\s*= \{(\w+), (\w+)\},')
        bindings = []
        for binding, extension in [(m[1], '.png'), (m[2], '.pal')]:
            gm = match('src/data/graphics/items.h', r'^const u32 ' + binding + r'\[\] = INCBIN_U32\("([^"]+)"\);')
            path = gm[1].replace('.4bpp.lz', extension).replace('.gbapal.lz', extension)
            if path not in sources:
                raise ValueError('Icon binding outside source lock')
            bindings.append(path)
        image_path, palette_path = bindings
        w, h, indices, _ = indexed_png(sources[image_path])
        if (w, h) != (24, 24):
            raise ValueError('Item icon dimensions differ')
        pixels = rgba(indices, palette(sources[palette_path]))
        filename = item_icon_path(image_path, palette_path)
        icon_outputs[filename] = png(w, h, pixels)
        # Binary sources use a one-line identity span; source text line counts are not meaningful for PNG.
        image_ref = dict(path=image_path, symbol=m[1], startLine=1, endLine=1)
        icons.append(dict(item=dict(value='pret:' + symbol), width=w, height=h, pngPath=filename))
    texts = []
    for symbol in TEXTS:
        p = 'src/strings.c'
        m = match(p, r'^const u8 ' + symbol + r'\[\] = _\(("(?:[^"\\]|\\.)*")\);')
        texts.append(dict(symbol=symbol, text=json.loads(m[1]), source=reference(p, symbol, m)))
    for p, symbol in [('data/text/poke_mart.inc', 'Text_MayIHelpYou'), ('data/event_scripts.s', 'Text_PleaseComeAgain')]:
        m = match(p, '^' + symbol + r'::\n((?:\s*\.string "[^\n]*"\n)+)')
        value = ''.join(json.loads(s) for s in re.findall(r'\.string ("[^\n]*")', m[1]))
        if not value.endswith('$'):
            raise ValueError('Mart text terminator differs')
        texts.append(dict(symbol=symbol, text=value, source=reference(p, symbol, m)))
    limits = dict(bagItemSlots=number('include/constants/global.h', 'BAG_ITEMS_COUNT'),
                  pcItemSlots=number('include/constants/global.h', 'PC_ITEMS_COUNT'), maximumStack=999)
    quest_limits = dict(keyItemSlots=number('include/constants/global.h', 'BAG_KEYITEMS_COUNT'),
                        ballSlots=number('include/constants/global.h', 'BAG_POKEBALLS_COUNT'), maximumStack=999)
    catalog = dict(limits=limits,
                   questLimits=quest_limits, maximumTransactionQuantity=99, maximumMoney=maximum_money,
                   items=items, shops=shops)
    document = dict(schemaVersion=1,  catalog=catalog,
        icons=icons, texts=texts)
    data = (json.dumps(document, ensure_ascii=False, indent=2, default=item_json) + '\n').encode()
    manifest = dict(   inputCount=len(inputs),
         iconCount=len(icons), itemCount=len(items), textCount=len(texts),
        outputs=[dict(path='early-shop.json')] +
                [dict(path=p) for p, b in sorted(icon_outputs.items())])
    out.mkdir(parents=True, exist_ok=True)
    for (directory, path), content in output_materials(out, {'early-shop.json': data, **icon_outputs}).items():
        target = directory / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
    (out / 'manifest.json').write_text(json.dumps(manifest, indent=2, default=item_json) + '\n', encoding='utf-8', newline='\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', type=pathlib.Path, help='Read fixed Git objects into the local raw directory first')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/data/services/early-shop')
    args = parser.parse_args()
    if args.repo:
        materialize(args.repo, args.raw)
    print(json.dumps(convert(args.raw, args.out), default=item_json))
