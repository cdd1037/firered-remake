#!/usr/bin/env python3
"""Import the bounded Oak's Parcel/Poke Ball storage profile from verified local Git blobs.

No upstream programs, ROMs, network requests or asset publication are involved.
"""
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

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-oaks-parcel-items-v1'
SYMBOLS = ['ITEM_OAKS_PARCEL', 'ITEM_POKE_BALL']
PATHS = {'asm/macros/event.inc', 'data/maps/PalletTown_ProfessorOaksLab/scripts.inc',
         'data/maps/ViridianCity_Mart/scripts.inc', 'include/constants/global.h',
         'include/constants/items.h', 'include/global.h', 'include/item.h',
         'src/data/items.json', 'src/data/items.json.txt', 'src/item.c', 'src/item_menu.c', 'src/scrcmd.c'}


def object_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def read_lock():
    lock = json.loads((ROOT / 'scripts/recipes/items/quest-items.lock.json').read_text(encoding='utf-8'), object_pairs_hook=object_pairs)
    if (set(lock) != {'schemaVersion', 'repository', 'commit', 'profile', 'completeItemCatalog', 'itemSymbols', 'files'} or
            type(lock['schemaVersion']) is not int or lock['schemaVersion'] != 1 or lock['repository'] != 'pret/pokefirered' or
            lock['commit'] != COMMIT or lock['profile'] != PROFILE or lock['completeItemCatalog'] is not False or
            lock['itemSymbols'] != SYMBOLS or not isinstance(lock['files'], list)):
        raise ValueError('Unknown source identity, profile or item scope')
    seen = set()
    for entry in lock['files']:
        if not isinstance(entry, dict):
            raise ValueError('Malformed source identity')
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path'] or str(path) in seen:
            raise ValueError('Unsafe or duplicate source path')
        seen.add(str(path))
    if seen != PATHS:
        raise ValueError('The complete bounded 12-file source manifest is required')
    return lock


def materialize(repo, raw):
    """Read a fixed commit with Git; do not read/execute repository worktree scripts."""
    inputs = []
    for entry in read_lock()['files']:
        data = subprocess.check_output(['git', '-C', str(repo), 'show', COMMIT + ':' + entry['path']])
        inputs.append((entry['path'], data))
    for path, data in inputs:
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)


def import_catalog(raw, out):
    lock = read_lock()
    inputs, texts = {}, {}
    for entry in lock['files']:
        data = (raw / entry['path']).read_bytes()
        inputs[entry['path']] = entry
        texts[entry['path']] = data.decode('utf-8')

    def one(path, pattern):
        matches = list(re.finditer(pattern, texts[path], re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one supported source record: ' + pattern)
        return matches[0]

    def reference(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=texts[path].count('\n', 0, match.start()) + 1,
                    endLine=texts[path].count('\n', 0, match.end()) + 1)

    def constant(path, symbol):
        match = one(path, r'^#define ' + re.escape(symbol) + r'\s+(\d+)(?=\s*\n)')
        return int(match[1]), reference(path, symbol, match)

    def function(path, symbol):
        match = one(path, r'^(?:static )?[^\n;{}]+\b' + re.escape(symbol) + r'\([^;{}]*\)\n\{.*?^\}')
        return match, reference(path, symbol, match)

    key_slots, key_ref = constant('include/constants/global.h', 'BAG_KEYITEMS_COUNT')
    ball_slots, ball_ref = constant('include/constants/global.h', 'BAG_POKEBALLS_COUNT')
    limits = [int(x) for x in re.findall(r'quantity \+ count <= (\d+)', texts['src/item.c'])]
    if (key_slots, ball_slots, limits) != (30, 13, [999, 999, 999]):
        raise ValueError('Unsupported source pocket/stack capacities')
    rules = [key_ref, ball_ref]
    for path, symbol in [('src/item.c', 'SetBagPocketsPointers'), ('src/item.c', 'CheckBagHasItem'),
                         ('src/item.c', 'CheckBagHasSpace'), ('src/item.c', 'AddBagItem'),
                         ('src/item.c', 'RemoveBagItem'), ('src/item_menu.c', 'OpenContextMenu'),
                         ('src/item_menu.c', 'Task_ItemContext_Sell'), ('src/item_menu.c', 'Task_ItemMenuAction_Give'),
                         ('src/scrcmd.c', 'ScrCmd_additem'), ('src/scrcmd.c', 'ScrCmd_removeitem'),
                         ('src/scrcmd.c', 'ScrCmd_checkitem')]:
        match, source = function(path, symbol)
        rules.append(source)
        if symbol.startswith('ScrCmd_') and '(u8)quantity' not in match[0]:
            raise ValueError('Unsupported script count conversion')
    menu = one('src/item_menu.c', r'^static const u8 sContextMenuItems_Field\[\]\[4\] = \{(.*?)^\};')
    actions = re.findall(r'ITEMMENUACTION_[A-Z_]+', menu[1])
    if actions != ['ITEMMENUACTION_USE', 'ITEMMENUACTION_GIVE', 'ITEMMENUACTION_TOSS', 'ITEMMENUACTION_CANCEL',
                   'ITEMMENUACTION_USE', 'ITEMMENUACTION_REGISTER', 'ITEMMENUACTION_CANCEL', 'ITEMMENUACTION_DUMMY',
                   'ITEMMENUACTION_GIVE', 'ITEMMENUACTION_TOSS', 'ITEMMENUACTION_CANCEL', 'ITEMMENUACTION_DUMMY']:
        raise ValueError('Unsupported field pocket menu actions')
    rules.append(reference('src/item_menu.c', 'sContextMenuItems_Field', menu))
    for symbol in ['additem', 'removeitem', 'checkitem', 'giveitem_msg']:
        match = one('asm/macros/event.inc', r'^\t\.macro ' + symbol + r'\b.*?^\t\.endm')
        if ('amount=1' if symbol == 'giveitem_msg' else 'quantity=1') not in match[0]:
            raise ValueError('Unsupported default script item count')
        rules.append(reference('asm/macros/event.inc', symbol, match))

    all_items = json.loads(texts['src/data/items.json'], object_pairs_hook=object_pairs)
    if (not isinstance(all_items, dict) or set(all_items) != {'items'} or not isinstance(all_items['items'], list) or
            any(not isinstance(x, dict) for x in all_items['items'])):
        raise ValueError('Unsupported item JSON envelope')
    items = []
    for symbol in SYMBOLS:
        found = [item for item in all_items['items'] if item.get('itemId') == symbol]
        if len(found) != 1:
            raise ValueError('Expected exactly one ' + symbol)
        item = found[0]
        parcel = symbol == 'ITEM_OAKS_PARCEL'
        expected = dict(english="OAK'S PARCEL" if parcel else 'POKé BALL', itemId=symbol,
                        price=0 if parcel else 200, holdEffect='HOLD_EFFECT_NONE', holdEffectParam=0,
                        importance=2 if parcel else 0, registrability=0,
                        pocket='POCKET_KEY_ITEMS' if parcel else 'POCKET_POKE_BALLS',
                        type='ITEM_TYPE_BAG_MENU' if parcel else 3,
                        fieldUseFunc='FieldUseFunc_OakStopsYou' if parcel else 'NULL',
                        battleUsage=0 if parcel else 2, battleUseFunc='NULL' if parcel else 'BattleUseFunc_PokeBallEtc',
                        secondaryId=0 if parcel else 3)
        if (set(item) != set(expected) | {'description_english'} or
                any(type(item[k]) is not type(v) or item[k] != v for k, v in expected.items()) or
                not isinstance(item['description_english'], str)):
            raise ValueError('Unknown or unsupported item field: ' + symbol)
        source_id, id_ref = constant('include/constants/items.h', symbol)
        if source_id != (349 if parcel else 4):
            raise ValueError('Unsupported source item ID')
        rules.append(id_ref)
        match = one('src/data/items.json', r'^    \{\n      "english": ' + re.escape(json.dumps(item['english'], default=item_json)) + r',.*?^    \}')
        items.append(shared_item(symbol))

    for path, symbol, pattern in [
        ('data/maps/ViridianCity_Mart/scripts.inc', 'ViridianCity_Mart_EventScript_ParcelScene',
         r'^\tgiveitem_msg ViridianCity_Mart_Text_ReceivedOaksParcelFromClerk, ITEM_OAKS_PARCEL, 1, MUS_OBTAIN_KEY_ITEM$'),
        ('data/maps/PalletTown_ProfessorOaksLab/scripts.inc', 'PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene', r'^\tremoveitem ITEM_OAKS_PARCEL$')]:
        rules.append(reference(path, symbol, one(path, pattern)))
    lab = 'data/maps/PalletTown_ProfessorOaksLab/scripts.inc'
    balls = list(re.finditer(r'^\tgiveitem_msg PalletTown_ProfessorOaksLab_Text_ReceivedFivePokeBalls, ITEM_POKE_BALL, 5$', texts[lab], re.M))
    if len(balls) != 2:
        raise ValueError('Unsupported Oak gift sites')
    rules.append(reference(lab, 'PalletTown_ProfessorOaksLab_EventScript_ReceiveDexScene', balls[0]))

    catalog = dict(limits=dict(keyItemSlots=key_slots, ballSlots=ball_slots, maximumStack=limits[0]), items=items)
    document = dict(schemaVersion=1, catalog=catalog)
    data = (json.dumps(document, ensure_ascii=False, indent=2, default=item_json) + '\n').encode('utf-8')
    manifest = dict(  itemCount=2, inputCount=len(inputs))
    out.mkdir(parents=True, exist_ok=True)
    (out / 'quest-items.json').write_bytes(data)
    print(json.dumps(manifest, default=item_json))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/data/items/quest')
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Materialize locked inputs by git show from this local checkout.')
    args = parser.parse_args()
    if args.reference_repo is not None:
        materialize(args.reference_repo, args.raw)
    import_catalog(args.raw, args.out)
