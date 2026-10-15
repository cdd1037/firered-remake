#!/usr/bin/env python3
"""Authenticate Dontae's original Fab Mail, source lifecycle, and exact GBA pixels.

Bounded nine-word content, not an Easy Chat composer. Upstream blobs are inert
data. No map, trade definition, inventory ancestry, or runtime state is changed.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import sys
import tempfile
import zlib
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'npc-trade-mail'
RESOURCE_ID = 'firered-npc-trade-mail-v1'
SHOP_PROFILE = 'firered-npc-trade-inventory-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result
def capture(src, path, pattern, symbol):
    text = src.text(path); match = re.search(pattern, text, re.M | re.S)
    need(match is not None, 'Missing source symbol: ' + symbol)
    return match, dict(path=path, symbol=symbol, startLine=text[:match.start()].count('\n') + 1,
        endLine=text[:match.end()].count('\n') + 1)
def function(src, path, name, needles=()):
    match, ref = capture(src, path, r'^(?:static )?[\w *]+\b' + re.escape(name) + r'\([^;]*?\)\n\{.*?^\}', name)
    for text in needles: need(text in match[0], 'Original contract differs: ' + name + ': ' + text)
    return ref
def integer(src, path, symbol):
    match, ref = capture(src, path, r'^#define\s+' + re.escape(symbol) + r'\s+(0x[\da-fA-F]+|\d+)\b[^\n]*', symbol)
    return int(match[1], 0), ref
def unescape(text):
    return text.replace('\\n', '\n').replace('\\l', '\n').replace('\\p', '\n\n').replace('\\"', '"').replace("\\'", "'")


def payload(src):
    path = 'src/data/ingame_trades.h'
    trade, trade_ref = capture(src, path, r'^    \[INGAME_TRADE_JYNX\]\s*=\s*\{(.*?)^    \}', 'INGAME_TRADE_JYNX')
    body = trade[1]
    number = lambda field: int(re.search(r'\.' + field + r'\s*=\s*(\d+)', body)[1])
    name = re.search(r'\.otName\s*=\s*_\("([^"]+)"\)', body)[1]
    species_symbol = re.search(r'\.species\s*=\s*(\w+)', body)[1]
    item_symbol = re.search(r'\.heldItem\s*=\s*(\w+)', body)[1]
    trade_id, _ = integer(src, 'include/constants/trade.h', 'INGAME_TRADE_JYNX')
    species, species_ref = integer(src, 'include/constants/species.h', species_symbol)
    item, item_ref = integer(src, 'include/constants/items.h', item_symbol)
    count, _ = integer(src, 'include/constants/global.h', 'MAIL_WORDS_COUNT')
    party, _ = integer(src, 'include/constants/global.h', 'PARTY_SIZE')
    need(count == 9 and party == 6 and '#define MAIL_COUNT         (PARTY_SIZE + 10)' in src.text('include/constants/global.h'), 'Original mail capacities differ')
    message, message_ref = capture(src, path, r'^static const u16 sInGameTradeMailMessages\[\]\[10\] = \{\s*\{(.*?)\}\s*\};', 'sInGameTradeMailMessages')
    symbols = [s.strip() for s in message[1].split(',') if s.strip()]
    need(len(symbols) == count and number('mailNum') == 0, 'Bounded source mail row differs')
    constants = src.text('include/constants/easy_chat.h')
    group_values = {k: int(v, 0) for k, v in re.findall(r'^#define\s+(EC_GROUP_\w+)\s+(0x[\da-fA-F]+|\d+)\s*$', constants, re.M)}
    groups = src.text('src/data/easy_chat/easy_chat_groups.h')
    group_tables = re.findall(r'\.wordData = \{\.(?:words|valueList) = (\w+)\}', groups)
    words = []
    for token in symbols:
        if token.startswith('EC_POKEMON('):
            need(token == 'EC_POKEMON(JYNX)' and '#define EC_POKEMON(mon) ((EC_GROUP_POKEMON << 9) | SPECIES_##mon)' in constants, 'Original EC Pokemon encoding differs')
            group, index = group_values['EC_GROUP_POKEMON'], species
            need(group_tables[group] == 'sEasyChatGroup_Pokemon2' and 'SPECIES_JYNX,' in src.text('src/data/easy_chat/easy_chat_group_pokemon2.h'), 'Jynx Easy Chat membership differs')
            m, text_ref = capture(src, 'src/data/text/species_names.h', r'^    \[SPECIES_JYNX\] = _\("([^"]+)"\),', 'SPECIES_JYNX')
            text = m[1]
            definition_ref = species_ref
        else:
            m, definition_ref = capture(src, 'include/constants/easy_chat.h', r'^#define\s+' + token + r'\s+\((EC_GROUP_\w+) << 9\) \| (0x[\da-fA-F]+)\s*$', token)
            group, index = group_values[m[1]], int(m[2], 16)
            table = group_tables[group]
            group_path = 'src/data/easy_chat/easy_chat_group_' + m[1].removeprefix('EC_GROUP_').lower() + '.h'
            group_text = src.text(group_path)
            need('static const struct EasyChatWordInfo ' + table + '[]' in group_text, 'Original Easy Chat group binding differs')
            entries = re.findall(r'\.text = (sEasyChatWord_\w+),', group_text)
            word_symbol = entries[index]
            m, text_ref = capture(src, group_path, r'^static const u8 ' + word_symbol + r'\[\] = _\("([^"]+)"\);', word_symbol)
            text = unescape(m[1])
        words.append(dict(symbol=token, value=(group << 9) | index, group=group, index=index,
            text=text, definitionSource=definition_ref, textSource=text_ref))
    need([w['value'] for w in words] == [0x0E1D, 0x1020, 0x141B, 0x2A7C, 0x0C00, 0x1006, 0x143F, 0x102F, 0x1029], 'Authenticated mail word numbers differ')
    need([w['text'] for w in words] == ["THAT'S", 'A', 'HEALTHY', 'JYNX', '!', 'BE', 'KIND', 'TO', 'IT'], 'Authenticated mail strings differ')
    charmap = src.text('charmap.txt')
    encoded = [int(re.search(r"^'" + re.escape(c) + r"'\s*=\s*([0-9A-F]{2})\s*$", charmap, re.M)[1], 16) for c in name] + [255]
    need("'$'         = FF" in charmap, 'Original EOS character differs')
    trainer = number('otId'); trainer_bytes = list(trainer.to_bytes(4, 'big'))
    need((trade_id, name, trainer, species, item) == (1, 'DONTAE', 36728, 124, 131), 'Original Dontae mail identity differs')
    need(trainer_bytes == [0, 0, 143, 120], 'Original high-byte-first trainer ID differs')
    constructor = function(src, 'src/trade_scene.c', 'GetInGameTradeMail', [
        'i < MAIL_WORDS_COUNT', 'mail->words[i] = sInGameTradeMailMessages[inGameTrade->mailNum][i];',
        'StringCopy(mail->playerName, inGameTrade->otName);', 'mail->trainerId[0] = inGameTrade->otId >> 24;',
        'mail->trainerId[1] = inGameTrade->otId >> 16;', 'mail->trainerId[2] = inGameTrade->otId >> 8;',
        'mail->trainerId[3] = inGameTrade->otId;', 'mail->species = inGameTrade->species;', 'mail->itemId = inGameTrade->heldItem;'])
    _, structure = capture(src, 'include/global.h', r'^struct Mail\n\{.*?^\};', 'Mail')
    return dict(sourceCommit=COMMIT, tradeId=trade_id, mailNumber=number('mailNum'), words=[w['value'] for w in words],
        authorName=name, authorNameBytes=encoded, authorTrainerId=trainer, authorTrainerIdBytes=trainer_bytes,
        sourceSpeciesId=species, sourceSpeciesSymbol=species_symbol, sourceItemId=item, item=dict(value='pret:' + item_symbol),
        sourceItemSymbol=item_symbol, constructorSource=constructor, tradeSource=trade_ref, messageSource=message_ref,
        structureSource=structure, speciesSource=species_ref, itemSource=item_ref), words


def item(src, codec, stage):
    path, symbol = 'src/data/items.json', 'ITEM_FAB_MAIL'; text = src.text(path)
    row = next(i for i in json.loads(text)['items'] if i['itemId'] == symbol)
    need(row == dict(english='FAB MAIL', itemId=symbol, price=50, holdEffect='HOLD_EFFECT_NONE', holdEffectParam=0,
        description_english='A piece of MAIL featuring a\\ngorgeous, extravagant print.\\nIt is to be held by a POKéMON.',
        importance=0, registrability=0, pocket='POCKET_ITEMS', type='ITEM_TYPE_MAIL', fieldUseFunc='FieldUseFunc_Mail',
        battleUsage=0, battleUseFunc='NULL', secondaryId=10), 'Original Fab Mail metadata differs')
    _, ref = capture(src, path, r'^    \{\n      "english": "FAB MAIL",.*?^    \}', symbol)
    identity = dict(value='pret:' + symbol)
    metadata = dict(id=identity, sourceItemSymbol=symbol, sourceItemId=131, name=row['english'], price=row['price'],
        importance=0, registrability=0, pocket=row['pocket'], type=row['type'], holdEffect=row['holdEffect'], holdEffectParam=0,
        fieldUseFunction=row['fieldUseFunc'], battleUsage=0, battleUseFunction='NULL', secondaryId=row['secondaryId'],
        canToss=True, canSell=True, canGive=True, source=ref)
    value = {k: metadata[k] for k in ('id', 'sourceItemSymbol', 'sourceItemId', 'name', 'price', 'pocket', 'importance',
        'type', 'fieldUseFunction', 'battleUsage', 'battleUseFunction', 'holdEffectParam', 'source')}
    value.update(description=unescape(row['description_english']), healHp=0, cureStatus=0, effectBytes=[], useSupport=10,
        effectSource=None, sourceMetadata=metadata)
    m, binding_ref = capture(src, 'src/data/item_icon_table.h', r'^    \[ITEM_FAB_MAIL\]\s*= \{(\w+), (\w+)\},', symbol)
    paths = []
    for binding in m.groups():
        declaration, _ = capture(src, 'src/data/graphics/items.h', r'^const u32 ' + binding + r'\[\]\s*= INCBIN_U32\("([^"]+)"\);', binding)
        paths.append(declaration[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    width, height, indices, _ = codec.indexed_png(src.read(paths[0])); need((width, height) == (24, 24), 'Fab icon dimensions differ')
    rgba = codec.rgba(indices, codec.palette(src.read(paths[1]))); png = codec.png(width, height, rgba)
    (stage/'item-fab-mail.png').write_bytes(png)
    icon = dict(item=identity, width=width, height=height, texturePath='item-fab-mail.png', sourcePaths=paths, bindingSource=binding_ref)
    return dict(sourceCommit=COMMIT, profile=SHOP_PROFILE, previousItemCount=126, itemCount=127,
        additions=[value],  shopsAdded=[], icons=[icon])


def render(src, codec, stage):
    tile_path, palette_path, map_path = ['graphics/mail/fab/' + x for x in ('tiles.png', 'palette.pal', 'map.bin')]
    graphics = src.text('src/data/graphics/mail.h')
    for expected in ('gFile_graphics_mail_fab_palette_pal[] = INCBIN_U16("graphics/mail/fab/palette.gbapal")',
        'gFile_graphics_mail_fab_tiles_sheet[] = INCBIN_U32("graphics/mail/fab/tiles.4bpp.lz")',
        'gFile_graphics_mail_fab_map_tilemap[] = INCBIN_U32("graphics/mail/fab/map.bin.lz")'):
        need(expected in graphics, 'Fab original graphics declaration differs')
    tiles = codec.tile_list(src.read(tile_path)); palette = codec.palette(src.read(palette_path)); data = src.read(map_path)
    need(len(tiles) == 21 and len(data) == 1280, 'Original Fab graphics dimensions differ')
    tilemap = struct.unpack('<640H', data)
    need(all(v >> 12 == 0 and (v & 1023) < len(tiles) for v in tilemap), 'Fab source tile reference differs')
    text = src.text('src/mail.c')
    need('.size = 0x2a0,' in text and 'FillBgTilemapBufferRect_Palette0(2, 0x001, 0, 0, 30, 20);' in text,
        'Original Fab tile size/background differs')
    m, gender_ref = capture(src, 'src/mail.c', r'^static const u16 sGenderPals\[\]\[2\] = \{(.*?)^\};', 'sGenderPals')
    rgb5 = [tuple(map(int, v)) for v in re.findall(r'RGB\((\d+), (\d+), (\d+)\)', m[1])]
    need(rgb5 == [(13, 22, 26), (5, 13, 20), (28, 15, 17), (20, 6, 14)], 'Gender palette RGB555 values differ')
    assets = []
    for gender in (0, 1):
        pal = list(palette); pal[10:12] = [tuple((c << 3) | (c >> 2) for c in rgb) for rgb in rgb5[gender*2:gender*2+2]]
        out = codec.canvas(240, 160)
        for y in range(20):
            for x in range(30):
                entry = tilemap[y*32+x]; tile = tiles[entry & 1023]
                # Source BG1 index zero is transparent; BG2 is repeated tile 1.
                indices = [tile[(7-py if entry & 2048 else py)*8+(7-px if entry & 1024 else px)] or tiles[1][py*8+px]
                    for py in range(8) for px in range(8)]
                codec.blit(out, 240, codec.rgba(indices, pal, False), 8, 8, x*8, y*8)
        png = codec.png(240, 160, bytes(out))
        stem = 'fab-mail-' + ('male' if gender == 0 else 'female')
        (stage/(stem+'.png')).write_bytes(png)
        assets.append(dict(id=stem, playerGender=gender, width=240, height=160, texturePath=stem + '.png', sourcePaths=[tile_path, palette_path, map_path], genderPaletteSource=gender_ref))
    return assets


def read_layout(src, words):
    m, layout_ref = capture(src, 'src/mail.c', r'^static const struct MailEcWordLayout sLayout_5x2\[\] = \{(.*?)^\};', 'sLayout_5x2')
    rows = [tuple(map(int, t)) for t in re.findall(r'\.numWordsInLine = (\d+), \.lineXoffset = (\d+), \.lineHeight = (\d+)', m[1])]
    need(rows == [(2, 0, 16)]*4+[(1, 0, 16)], 'Five-row source mail arrangement differs')
    m, attrs_ref = capture(src, 'src/mail.c', r'^static const struct MailAttrStruct sMessageLayouts_5x2\[\] = \{.*?\[ITEM_TO_MAIL\(ITEM_FAB_MAIL\)\]\s*= \{(.*?)\n    \}', 'sMessageLayouts_5x2[ITEM_TO_MAIL(ITEM_FAB_MAIL)]')
    attrs = {k: int(v) for k, v in re.findall(r'\.(\w+) = (\d+),', m[1])}
    need(attrs == dict(numRows=5, nameY=8, nameX=16, messageTop=3, messageLeft=8) and '.linesLayout = sLayout_5x2' in m[1], 'Original Fab text positioning differs')
    _, windows_ref = capture(src, 'src/mail.c', r'^static const struct WindowTemplate sWindowTemplates\[\] = \{.*?^\};', 'sWindowTemplates')
    layout = dict(screenWidth=240, screenHeight=160, arrangementType=1, fontSymbol='FONT_NORMAL_COPY_1', messageX=32, messageY=35, lineHeight=16, authorX=136, authorY=128, authorPrefix='From ', textRgb=[82, 82, 82], shadowRgb=[206, 206, 206], showPokemonIcon=False, wordsPerLine=[r[0] for r in rows], lines=[' '.join((w['text'] for w in words[i:i + 2])) for i in range(0, 9, 2)], layoutSource=layout_ref, attributesSource=attrs_ref, windowsSource=windows_ref, readSource=function(src, 'src/mail.c', 'ReadMail', ['sMailViewResources->mailArrangementType = 1;', 'sMailViewResources->messageLayout = &sMessageLayouts_5x2[sMailViewResources->mailType];']), bufferSource=function(src, 'src/mail.c', 'BufferMailMessage', ['ConvertEasyChatWordsToString(', 'j += sMailViewResources->messageLayout->linesLayout[i].numWordsInLine;']), printerSource=function(src, 'src/mail.c', 'AddMailMessagePrinters', ['FONT_NORMAL_COPY_1', 'width = GetStringWidth(FONT_NORMAL_COPY_1, gText_From, 0);']), conversionSource=function(src, 'src/easy_chat.c', 'ConvertEasyChatWordsToString', ['*dest = CHAR_SPACE;', '*dest = EOS;']))
    need(layout['lines'] == ["THAT'S A", 'HEALTHY JYNX', '! BE', 'KIND TO', 'IT'], 'Five-row mail string differs')
    return layout


def contracts(src):
    specs = [
        ('tradeReceive', 'src/trade_scene.c', 'TradeMons', ['ClearMailStruct(&gSaveBlock1Ptr->mail[playerMail]);', 'GiveMailToMon2(playerMon, &gLinkPartnerMail[partnerMail]);']),
        ('attach', 'src/mail_data.c', 'GiveMailToMon2', ['u8 mailId = GiveMailToMon(mon, itemId);', 'gSaveBlock1Ptr->mail[mailId] = *mail;']),
        ('allocatePartySlot', 'src/mail_data.c', 'GiveMailToMon', ['id < PARTY_SIZE', 'if (gSaveBlock1Ptr->mail[id].itemId == 0)', 'return 0xFF;']),
        ('detachErase', 'src/mail_data.c', 'TakeMailFromMon', ['gSaveBlock1Ptr->mail[mailId].itemId = ITEM_NONE;', 'mailId = 0xFF;']),
        ('detachToMailbox', 'src/mail_data.c', 'TakeMailFromMon2', ['i = PARTY_SIZE; i < MAIL_COUNT', 'sizeof(struct Mail)', 'return 0xFF;']),
        ('emptyMail', 'src/mail_data.c', 'ClearMailStruct', ['mail->words[i] = 0xFFFF;', 'mail->itemId = ITEM_NONE;']),
        ('readHeld', 'src/party_menu.c', 'CB2_ReadHeldMail', ['ReadMail(&gSaveBlock1Ptr->mail[GetMonData(&gPlayerParty[gPartyMenu.slotId], MON_DATA_MAIL)]']),
        ('takeChoice', 'src/party_menu.c', 'Task_HandleSendMailToPCYesNoInput', ['TakeMailFromMon2(', 'gText_PCMailboxFull', 'gText_MailMessageWillBeLost']),
        ('eraseChoice', 'src/party_menu.c', 'Task_HandleLoseMailMessageYesNoInput', ['if (AddBagItem(item, 1) == TRUE)', 'TakeMailFromMon(', 'case MENU_B_PRESSED:']),
        ('giveMailbox', 'src/party_menu.c', 'TryGiveMailToSelectedMon', ['if (GetMonData(mon, MON_DATA_HELD_ITEM) != ITEM_NONE)', 'GiveMailToMon2(mon, mail);', 'ClearMailStruct(mail);']),
        ('countMailbox', 'src/player_pc.c', 'CountPCMail', ['i = PC_MAIL_NUM(0); i < MAIL_COUNT']),
        ('compactMailbox', 'src/player_pc.c', 'PCMailCompaction', ['i = PC_MAIL_NUM(0); i < MAIL_COUNT - 1']),
        ('readMailbox', 'src/player_pc.c', 'Task_WaitFadeAndReadSelectedMail', ['ReadMail(&SELECTED_MAIL, CB2_SetCbToReturnToMailbox, 1);']),
        ('mailboxEraseWarning', 'src/player_pc.c', 'Task_PlayerPcMoveMailToBag', ['gText_MessageWillBeLost']),
        ('mailboxEraseChoice', 'src/player_pc.c', 'Task_MoveToBagYesNoMenuHandleInput', ['Task_TryPutMailInBag_DestroyMsgIfSuccessful(taskId);', 'Task_DeclinedMoveMailToBag(taskId);']),
        ('mailboxReturnToBag', 'src/player_pc.c', 'Task_TryPutMailInBag_DestroyMsgIfSuccessful', ['if (!AddBagItem(mail->itemId, 1))', 'ClearMailStruct(mail);', 'PCMailCompaction();']),
        ('readClose', 'src/mail.c', 'ShowMailCB_WaitButton', ['JOY_NEW(A_BUTTON | B_BUTTON)'])]
    refs = [dict(id=key, source=function(src, path, symbol, needles)) for key, path, symbol, needles in specs]
    # Existing Cerulean geometry is reused; no room resource is imported.
    map_path = 'data/maps/CeruleanCity_House3/map.json'; events = src.json(map_path)
    actor = next(a for a in events['object_events'] if a['script'] == 'CeruleanCity_House3_EventScript_Dontae')
    scripts = src.text('data/maps/CeruleanCity_House3/scripts.inc')
    need('setvar VAR_0x8008, INGAME_TRADE_JYNX' in scripts and 'FLAG_DID_ZYNX_TRADE' in scripts, 'Original Dontae map binding differs')
    src.read('data/maps/CeruleanCity_House3/text.inc')
    return dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, tradeId=1,
         changesToTradeDefinitions=[], newMaps=[], newShops=[],
        mapSymbol=events['id'], tradeDefinitionMapId='map.cerulean-city-house3', runtimeMapId='map.cerulean-city-house-3', actor=actor,
        partyMailCapacity=6, mailboxCapacity=10, totalMailCapacity=16, noneMailIndex=255, emptyWord=65535,
        lifecycle=refs, limitations=['No Easy Chat composer or generic arbitrary-word rendering.',
            'No map geometry, trade definition, store, trade animation, or audio addition.',
            'Runtime mail ownership, commands, save validation, and menu admission are separate.'])


def strings(src):
    names = ['gText_From', 'gText_Mail', 'gText_Mailbox', 'gText_DoWhatWithMail', 'gText_SendMailToPC',
        'gText_MailSentToPC', 'gText_PCMailboxFull', 'gText_MailMessageWillBeLost', 'gText_RemoveMailBeforeItem',
        'gText_MailTakenFromPkmn', 'gText_PkmnHoldingItemCantHoldMail', 'gText_MailTransferredFromMailbox',
        'gText_TheresNoMailHere', 'gText_WhatWouldYouLikeToDoWithPlayersMail', 'gText_MessageWillBeLost',
        'gText_BagIsFull', 'gText_MailReturnedToBagMessageErased', 'gText_PleaseRemoveTheMail', 'gText_MailCantBeStored',
        'gText_Read2', 'gText_Take2', 'gOtherText_Read', 'gOtherText_MoveToBag', 'gOtherText_Give2', 'gOtherText_Exit',
        'gFameCheckerText_Cancel', 'gText_GiveToWhichPokemon']
    out = []
    for name in names:
        m, ref = capture(src, 'src/strings.c', r'^const u8 ' + name + r'\[\] = _\("((?:[^"\\]|\\.)*)"\);', name)
        out.append(dict(symbol=name, text=unescape(m[1]), source=ref))
    return out


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference-root', type=Path, required=True)
    p.add_argument('--dependency-root', type=Path, default=ROOT)
    p.add_argument('--project-root', type=Path, default=ROOT)
    p.add_argument('--check', action='store_true'); args = p.parse_args()
    target, dep, ref = [Path(os.path.abspath(x)) for x in (args.project_root, args.dependency_root, args.reference_root)]
    for path in (target, dep, ref): safe(path)
    need(not (target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', 'mail_reader')
    codec_inputs = dep/'scripts/fetch-lab-battle-ui.py'; safe(codec_inputs)
    codec = module(dep / 'scripts/import-lab-battle-ui.py', 'mail_codec')
    reader.PROFILE = RESOURCE_ID; src = reader.Source(ref)
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='mail-', dir=target) as folder:
        stage = Path(folder)
        dontae, words = payload(src); items = item(src, codec, stage); assets = render(src, codec, stage)
        layout = read_layout(src, words); source_contracts = contracts(src); texts = strings(src)
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=[], completeGame=False)
        lock_bytes = canonical(lock)
        docs = {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, tradeId=1, assets=assets),
            'dontae.json': dontae, 'easy-chat-words.json': words, 'read-layout.json': layout,
            'contracts.json': source_contracts, 'items.json': items, 'texts.json': texts}
        for name, value in docs.items(): (stage/name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix())
            for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
             inputCount=len(src.entries), outputs=outputs,
            tradeCount=1, mailWordCount=9, itemCount=1, backgroundCount=2, mapCount=0)
        manifest_bytes = canonical(manifest); (stage/'manifest.json').write_bytes(manifest_bytes)
        writes = []
        writes += [(target/'content/raw/pokefirered/037335f4'/p, b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: '+str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
            inputCount=len(src.entries), outputCount=len(outputs), mailWords=dontae['words'], textLines=layout['lines'], check=args.check), indent=2))


if __name__ == '__main__': main()
