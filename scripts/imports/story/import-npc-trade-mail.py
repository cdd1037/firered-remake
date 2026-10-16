#!/usr/bin/env python3
"""Authenticate Dontae's original Fab Mail, source lifecycle, and exact GBA pixels.

Bounded nine-word content, not an Easy Chat composer. Upstream blobs are inert
data. No map, trade definition, inventory ancestry, or runtime state is changed.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PACKAGE = 'ui/npc-trade-mail'
RESOURCE_ID = 'firered-npc-trade-mail-v1'


def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':'), default=item_json) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
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
    return dict( tradeId=trade_id, mailNumber=number('mailNum'), words=[w['value'] for w in words],
        authorName=name, authorNameBytes=encoded, authorTrainerId=trainer, authorTrainerIdBytes=trainer_bytes,
        sourceSpeciesId=species, sourceSpeciesSymbol=species_symbol, sourceItemId=item, item=dict(value='pret:' + item_symbol),
        sourceItemSymbol=item_symbol, constructorSource=constructor, tradeSource=trade_ref, messageSource=message_ref,
        structureSource=structure, speciesSource=species_ref, itemSource=item_ref), words


def item(src, codec, stage):
    shared_item = ItemSource(src.read).definition
    path, symbol = 'src/data/items.json', 'ITEM_FAB_MAIL'; text = src.text(path)
    row = next(i for i in json.loads(text)['items'] if i['itemId'] == symbol)
    need(row == dict(english='FAB MAIL', itemId=symbol, price=50, holdEffect='HOLD_EFFECT_NONE', holdEffectParam=0,
        description_english='A piece of MAIL featuring a\\ngorgeous, extravagant print.\\nIt is to be held by a POKéMON.',
        importance=0, registrability=0, pocket='POCKET_ITEMS', type='ITEM_TYPE_MAIL', fieldUseFunc='FieldUseFunc_Mail',
        battleUsage=0, battleUseFunc='NULL', secondaryId=10), 'Original Fab Mail metadata differs')
    _, ref = capture(src, path, r'^    \{\n      "english": "FAB MAIL",.*?^    \}', symbol)
    identity = dict(value='pret:' + symbol)
    metadata = shared_item(symbol)
    value = dict(item=metadata)
    value.update(description=unescape(row['description_english']), healHp=0, cureStatus=0, effectBytes=[], useSupport=10)
    m, binding_ref = capture(src, 'src/data/item_icon_table.h', r'^    \[ITEM_FAB_MAIL\]\s*= \{(\w+), (\w+)\},', symbol)
    paths = []
    for binding in m.groups():
        declaration, _ = capture(src, 'src/data/graphics/items.h', r'^const u32 ' + binding + r'\[\]\s*= INCBIN_U32\("([^"]+)"\);', binding)
        paths.append(declaration[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    width, height, indices, _ = codec.indexed_png(src.read(paths[0])); need((width, height) == (24, 24), 'Fab icon dimensions differ')
    rgba = codec.rgba(indices, codec.palette(src.read(paths[1]))); png = codec.png(width, height, rgba)
    (stage/'item-fab-mail.png').write_bytes(png)
    icon = dict(item=identity, width=width, height=height, texturePath='item-fab-mail.png',  bindingSource=binding_ref)
    return dict(  previousItemCount=126, itemCount=127,
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
        assets.append(dict(id=stem, playerGender=gender, width=240, height=160, texturePath=stem + '.png',  genderPaletteSource=gender_ref))
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


def generate(raw):
    from world_source import WorldSource
    import ui_tiles as codec
    src = WorldSource(raw)
    with tempfile.TemporaryDirectory(prefix='mail-') as folder:
        stage = Path(folder)
        dontae, words = payload(src)
        items = item(src, codec, stage)
        assets = render(src, codec, stage)
        docs = {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, tradeId=1, assets=assets),
                'dontae.json': dontae, 'easy-chat-words.json': words, 'read-layout.json': read_layout(src, words),
                'items.json': items, 'texts.json': strings(src)}
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        return {PACKAGE + '/' + p.name: p.read_bytes() for p in stage.iterdir()}


def main():
    from generated_output_compare import check_item_outputs
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs = generate(args.raw)
    if args.check: check_item_outputs(args.out, outputs)
    else:
        for name, data in outputs.items():
            target = args.out / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(outputs=len(outputs), check=args.check)))


if __name__ == '__main__': main()
