#!/usr/bin/env python3
"""Strict, bounded Potion extraction from locked public reference texts, all local-only."""
import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-opening-potion-v1'
PATHS = {'include/constants/global.h', 'include/constants/items.h', 'include/constants/item_effects.h',
         'include/global.h', 'include/item.h', 'src/data/items.json', 'src/data/items.json.txt',
         'src/data/pokemon/item_effects.h', 'src/item.c', 'src/item_use.c', 'src/new_game.c',
         'src/party_menu.c', 'src/player_pc.c', 'src/pokemon.c', 'src/item_menu.c', 'src/item_pc.c',
         'src/battle_main.c'}


def object_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def read_lock():
    lock = json.loads((ROOT / 'scripts/opening-items.lock.json').read_text(encoding='utf-8'), object_pairs_hook=object_pairs)
    if (set(lock) != {'schemaVersion', 'repository', 'commit', 'profile', 'completeItemCatalog', 'itemSymbols', 'files'} or
            type(lock['schemaVersion']) is not int or lock['schemaVersion'] != 1 or lock['repository'] != 'pret/pokefirered' or
            lock['commit'] != COMMIT or lock['profile'] != PROFILE or lock['completeItemCatalog'] is not False or
            lock['itemSymbols'] != ['ITEM_POTION']):
        raise ValueError('Unknown source identity, profile or item scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if str(path) in seen:
            raise ValueError('Duplicate source path')
        seen.add(str(path))
    if seen != PATHS:
        raise ValueError('The complete bounded 17-file source manifest is required')
    return lock


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
        match = one(path, r'^#define ' + re.escape(symbol) + r'\s+(\d+|0x[0-9a-fA-F]+)(?=\s*\n)')
        return int(match[1], 0), reference(path, symbol, match)

    def function(path, symbol):
        match = one(path, r'^(?:static )?[^\n;{}]+\b' + re.escape(symbol) + r'\([^;{}]*\)\n\{.*?^\}')
        return reference(path, symbol, match)

    items_path = 'src/data/items.json'
    all_items = json.loads(texts[items_path], object_pairs_hook=object_pairs)
    if not isinstance(all_items, dict) or set(all_items) != {'items'} or not isinstance(all_items['items'], list) or any(not isinstance(x, dict) for x in all_items['items']):
        raise ValueError('Unsupported item JSON envelope')
    potion_records = [item for item in all_items['items'] if item.get('itemId') == 'ITEM_POTION']
    if len(potion_records) != 1:
        raise ValueError('Expected exactly one Potion record')
    p = potion_records[0]
    expected = {'english': 'POTION', 'itemId': 'ITEM_POTION', 'price': 300, 'holdEffect': 'HOLD_EFFECT_NONE',
                'holdEffectParam': 20, 'importance': 0, 'registrability': 0, 'pocket': 'POCKET_ITEMS',
                'type': 'ITEM_TYPE_PARTY_MENU', 'fieldUseFunc': 'FieldUseFunc_Medicine', 'battleUsage': 1,
                'battleUseFunc': 'BattleUseFunc_Medicine', 'secondaryId': 0}
    if set(p) != set(expected) | {'description_english'} or any(type(p[k]) is not type(v) or p[k] != v for k, v in expected.items()) or not isinstance(p['description_english'], str):
        raise ValueError('Unknown or unsupported Potion field')
    # Keep description in the local source; the Domain does not need localized prose.
    item_match = one(items_path, r'^    \{\n      "english": "POTION",.*?^    \}')
    effect_path = 'src/data/pokemon/item_effects.h'
    effect_match = one(effect_path, r'^static const u8 sItemEffect_Potion\[7\] = \{(.*?)^\};')
    effect_body = re.sub(r'//[^\n]*', '', effect_match[1])
    effects = list(re.finditer(r'\[(\d+)\]\s*=\s*([A-Z0-9_]+|\d+)\s*,', effect_body))
    remainder = re.sub(r'\[(\d+)\]\s*=\s*([A-Z0-9_]+|\d+)\s*,', '', effect_body).strip()
    if remainder or [(x[1], x[2]) for x in effects] != [('4', 'ITEM4_HEAL_HP'), ('6', '20')]:
        raise ValueError('Unsupported Potion effect initializer')
    heal_flag, flag_ref = constant('include/constants/item_effects.h', 'ITEM4_HEAL_HP')
    source_id, item_id_ref = constant('include/constants/items.h', 'ITEM_POTION')
    bag_slots, bag_ref = constant('include/constants/global.h', 'BAG_ITEMS_COUNT')
    pc_slots, pc_ref = constant('include/constants/global.h', 'PC_ITEMS_COUNT')
    pc_match = one('src/player_pc.c', r'^static const struct ItemSlot gNewGamePCItems\[\] = \{(.*?)^\};')
    if re.sub(r'\s+', '', pc_match[1]) != '{ITEM_POTION,1},{ITEM_NONE,0}':
        raise ValueError('Unsupported initial PC items')
    limits = [int(x) for x in re.findall(r'quantity \+ count <= (\d+)', texts['src/item.c'])]
    if limits != [999, 999, 999] or (heal_flag, source_id, bag_slots, pc_slots) != (4, 13, 42, 30):
        raise ValueError('Unsupported source inventory/effect limits')
    rules = [item_id_ref, flag_ref, bag_ref, pc_ref, reference('src/player_pc.c', 'gNewGamePCItems', pc_match)]
    for path, symbol in [('src/new_game.c', 'NewGameInitData'), ('src/player_pc.c', 'NewGameInitPCItems'),
                         ('src/item.c', 'AddBagItem'), ('src/item.c', 'RemoveBagItem'), ('src/item.c', 'AddPCItem'),
                         ('src/item.c', 'RemovePCItem'), ('src/item_pc.c', 'ItemPc_DoWithdraw'),
                         ('src/item_pc.c', 'Task_ItemPcWaitButtonAndFinishWithdrawMultiple'),
                         ('src/item_menu.c', 'Task_TryDoItemDeposit'), ('src/item_menu.c', 'Task_WaitAB_RedrawAndReturnToBag'),
                         ('src/party_menu.c', 'ItemUseCB_Medicine'), ('src/party_menu.c', 'ItemUseCB_MedicineStep'),
                         ('src/pokemon.c', 'PokemonUseItemEffects'), ('src/pokemon.c', 'PokemonItemUseNoEffect'),
                         ('src/party_menu.c', 'Task_ClosePartyMenuAfterText'),
                         ('src/battle_main.c', 'HandleTurnActionSelectionState'),
                         ('src/battle_main.c', 'SetActionsAndBattlersTurnOrder'), ('src/battle_main.c', 'HandleAction_UseItem'),
                         ('src/item_use.c', 'FieldUseFunc_Medicine'), ('src/item_use.c', 'BattleUseFunc_Medicine')]:
        rules.append(function(path, symbol))
    catalog = dict(sourceCommit=COMMIT, profile=PROFILE, completeItemCatalog=False,
                   limits=dict(bagItemSlots=bag_slots, pcItemSlots=pc_slots, maximumStack=limits[0]),
                   initialPcItems=[dict(item={'value': 'pret:ITEM_POTION'}, quantity=1)],
                   potion=dict(item='pret:ITEM_POTION', healHp=int(effects[1][2]), effectBytes=[0, 0, 0, 0, heal_flag, 0, int(effects[1][2])]))
    document = dict(schemaVersion=1, inputs=[inputs[x] for x in sorted(inputs)], limitations=['Only ITEM_POTION is imported and executable.',
                                 'Only the ordinary Items pocket and PC are represented; other pockets are unsupported.',
                                 'No shops, held-item actions, status ailments, eggs, reusable tutorial items or multiplayer.',
                                 'Battle action scheduling and active-combatant synchronization belong to the caller.',
                                 'This local source profile does not prove ROM execution parity or native UI behavior.'],
                    catalog=catalog)
    data = (json.dumps(document, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    manifest = dict(sourceCommit=COMMIT, profile=PROFILE, itemCount=1, inputCount=len(inputs))
    out.mkdir(parents=True, exist_ok=True)
    (out / 'opening-items.json').write_bytes(data)
    (out / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/opening-items')
    args = parser.parse_args()
    import_catalog(args.raw, args.out)
