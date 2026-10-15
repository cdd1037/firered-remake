#!/usr/bin/env python3
"""Pinned, local-only early Mart extraction. Never executes upstream programs or downloads a ROM."""
import argparse
import base64
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
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
    data = (ROOT / 'scripts/early-shop.lock.json').read_bytes()
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
        shops.append(dict(id=symbol, items=[dict(value='pret:' + i) for i in stock[:-1]], source=reference(path, symbol, m)))
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
        items.append(dict(id=dict(value='pret:' + symbol), sourceItemSymbol=symbol,
            sourceItemId=number('include/constants/items.h', symbol), name=i['english'],
            description=i['description_english'].replace(r'\n', '\n'), price=i['price'], pocket=i['pocket'],
            importance=i['importance'], type=str(i['type']), fieldUseFunction=i['fieldUseFunc'],
            battleUsage=i['battleUsage'], battleUseFunction=i['battleUseFunc'], holdEffectParam=i['holdEffectParam'],
            healHp=heal, cureStatus=cure, effectBytes=effects, useSupport=support,
            source=reference('src/data/items.json', symbol, item_match), effectSource=effect_source))
    for status, expected in [('POISON', 8), ('BURN', 16), ('PARALYSIS', 64)]:
        shift = int(match('include/constants/battle.h', r'^#define STATUS1_' + status + r'\s+\(1 << (\d+)\)')[1])
        if (1 << shift) != expected:
            raise ValueError('Supported status1 bit differs')
    # Reuse the already-reviewed local PNG codec, not an upstream tool or its scripts.
    spec = importlib.util.spec_from_file_location('early_shop_codec', ROOT / 'scripts/import-lab-battle-ui.py')
    codec = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(codec)
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
        w, h, indices, _ = codec.indexed_png(sources[image_path])
        if (w, h) != (24, 24):
            raise ValueError('Item icon dimensions differ')
        rgba = codec.rgba(indices, codec.palette(sources[palette_path]))
        png = codec.png(w, h, rgba)
        filename = symbol.lower() + '.png'
        icon_outputs[filename] = png
        # Binary sources use a one-line identity span; source text line counts are not meaningful for PNG.
        image_ref = dict(path=image_path, symbol=m[1], startLine=1, endLine=1)
        icons.append(dict(item=dict(value='pret:' + symbol), width=w, height=h,

            pngPath=filename,  source=image_ref,
            paletteSource=reference(palette_path, m[2]), bindingSource=reference(table, symbol, m)))
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
    catalog = dict(sourceCommit=COMMIT, profile=PROFILE, completeItemCatalog=False, limits=limits,
                   questLimits=quest_limits, maximumTransactionQuantity=99, maximumMoney=maximum_money,
                   items=items, shops=shops)
    document = dict(schemaVersion=1, inputs=list(inputs.values()), rulesSources=rules, catalog=catalog,
        icons=icons, texts=texts, limitations=[
            'Only the exact Viridian/Pewter Mart stock union is in this catalog. ITEM_NONE is a terminator, never merchandise.',
            'This is a new opt-in inventory validator. Existing save formats, null pockets and opening validator remain unchanged.',
            'Only Potion plus standalone poison/burn/paralysis medicine effects execute here; battle synchronization and turn scheduling belong to Application.',
            'Awakening is stock data only while sleep remains unsupported. Escape Rope and Repel have no executable map/counter actions.',
            'Poké Ball capture belongs to the existing capture pipeline, not the medicine API.',
            'No shop command/UI, script gating, quest log, ROM parity, audio or native-window acceptance is claimed.',
            'The source AddBagItem rejects an existing stack above 999 instead of splitting it; the inherited one-stack-per-item model preserves that FireRed rule.',
            'Only eight merchandise identities are allowed; a legal state cannot fill the 42 distinct ordinary-item slots in this bounded profile.'])
    data = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode()
    manifest = dict(sourceCommit=COMMIT, profile=PROFILE,  inputCount=len(inputs),
         iconCount=len(icons), itemCount=len(items), textCount=len(texts),
        outputs=[dict(path='early-shop.json')] +
                [dict(path=p) for p, b in sorted(icon_outputs.items())])
    out.mkdir(parents=True, exist_ok=True)
    for path, content in {'early-shop.json': data, **icon_outputs}.items():
        (out / path).write_bytes(content)
    (out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', type=pathlib.Path, help='Read fixed Git objects into the local raw directory first')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/early-shop')
    args = parser.parse_args()
    if args.repo:
        materialize(args.repo, args.raw)
    print(json.dumps(convert(args.raw, args.out)))
