#!/usr/bin/env python3
"""Import the complete pinned FireRed Easy Chat vocabulary and composer contracts.

Reads authenticated Git blobs only; never executes upstream code. No graphics,
inventory layering, multiplayer service, or made-up free-text words are added.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import importlib.util
import json
from pathlib import Path
import re
import sys
sys.dont_write_bytecode = True

ROOT = _PROJECT_ROOT
PACKAGE = 'mail-composer'
PROFILE = 'firered-mail-composer-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'

def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(test, message):
    if not test: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))
def unescape(text): return text.replace('\\n', '\n').replace('\\l', '\n').replace('\\p', '\n\n').replace('\\"', '"').replace("\\'", "'")
def capture(src, path, pattern, symbol):
    text = src.text(path); match = re.search(pattern, text, re.M | re.S)
    need(match is not None, 'Missing source symbol: ' + symbol)
    return match, dict(path=path, symbol=symbol, startLine=text[:match.start()].count('\n') + 1,
        endLine=text[:match.end()].count('\n') + 1)
def function(src, path, name, needles):
    match, ref = capture(src, path, r'^(?:static )?[\w *]+\b' + re.escape(name) + r'\([^;]*?\)\n\{.*?^\}', name)
    for needle in needles: need(needle in match[0], 'Original contract differs: ' + name + ': ' + needle)
    return ref
def constants(src, path):
    return {k: int(v, 0) for k, v in re.findall(r'^#define\s+(\w+)\s+(0x[\da-fA-F]+|\d+)\b', src.text(path), re.M)}
def source_string(src, path, symbol):
    match, ref = capture(src, path, r'^(?:static )?const u8 ' + re.escape(symbol) + r'\[\] = _\("((?:\\.|[^"\\])*)"\);', symbol)
    return unescape(match[1]), ref

def vocabulary(src):
    ec = constants(src, 'include/constants/easy_chat.h')
    species = constants(src, 'include/constants/species.h'); moves = constants(src, 'include/constants/moves.h')
    defines = src.text('include/constants/easy_chat.h')
    token_values = {symbol: (ec[group] << 9) | int(index, 0) for symbol, group, index in re.findall(
        r'^#define\s+(EC_WORD_\w+)\s+\((EC_GROUP_\w+) << 9\) \| (0x[\da-fA-F]+)', defines, re.M)}
    symbol_by_value = {v: k for k, v in token_values.items()}
    tables = src.text('src/data/easy_chat/easy_chat_groups.h')
    includes = re.findall(r'#include "(easy_chat_group_\w+.h)"', tables)
    group_rows = re.findall(r'\.wordData = \{\.(words|valueList) = (\w+)\},\s*\.numWords = (\d+),\s*\.numEnabledWords = (\d+)', tables)
    group_symbols = {v: k for k, v in ec.items() if k.startswith('EC_GROUP_')}
    group_names = dict(re.findall(r'\[(EC_GROUP_\w+)\] = (gEasyChatGroupName_\w+)', src.text('src/easy_chat.c')))
    groups = []; words = []
    for group_id, (kind, table, count, enabled_count) in enumerate(group_rows):
        path = 'src/data/easy_chat/' + includes[group_id]
        body, group_ref = capture(src, path, r'^static const (?:u16|struct EasyChatWordInfo) ' + table + r'\[\] = \{(.*?)^\};', table)
        group_symbol = group_symbols[group_id]; name, name_ref = source_string(src, 'src/strings.c', group_names[group_symbol])
        group_words = []
        if kind == 'valueList':
            indexes = re.findall(r'\b(?:SPECIES|MOVE)_\w+', body[1])
            for symbol in indexes:
                source_path = 'include/constants/species.h' if symbol.startswith('SPECIES_') else 'include/constants/moves.h'
                index = (species if symbol.startswith('SPECIES_') else moves)[symbol]
                prefix = {'EC_GROUP_POKEMON': 'EC_POKEMON', 'EC_GROUP_POKEMON_2': 'EC_POKEMON2', 'EC_GROUP_MOVE_1': 'EC_MOVE', 'EC_GROUP_MOVE_2': 'EC_MOVE2'}[group_symbol]
                text_path = 'src/data/text/species_names.h' if symbol.startswith('SPECIES_') else 'src/data/text/move_names.h'
                match, text_ref = capture(src, text_path, r'^\s*\[' + symbol + r'\]\s*=\s*_\("((?:\\.|[^"\\])*)"\),?', symbol)
                _, definition = capture(src, source_path, r'^#define\s+' + symbol + r'\s+(?:0x[\da-fA-F]+|\d+)\b[^\n]*', symbol)
                group_words.append(dict(value=(group_id << 9) | index, group=group_id, index=index, symbol=prefix + '(' + symbol.split('_', 1)[1] + ')',
                    text=unescape(match[1]), enabled=True))
            order = [w['value'] for w in group_words]
        else:
            entries = re.findall(r'\.text = (sEasyChatWord_\w+),\s*\.alphabeticalOrder = (\d+),\s*\.enabled = (TRUE|FALSE)', body[1])
            for index, (text_symbol, _, enabled) in enumerate(entries):
                value = (group_id << 9) | index; symbol = symbol_by_value[value]
                text, text_ref = source_string(src, path, text_symbol)
                _, definition = capture(src, 'include/constants/easy_chat.h', r'^#define\s+' + symbol + r'\s+\(EC_GROUP_\w+ << 9\) \| 0x[\da-fA-F]+[^\n]*', symbol)
                group_words.append(dict(value=value, group=group_id, index=index, symbol=symbol, text=text,
                    enabled=enabled == 'TRUE'))
            permutation = [int(e[1]) for e in entries]
            need(sorted(permutation) == list(range(len(entries))), 'Invalid original alphabetical permutation: ' + table)
            order = [(group_id << 9) | index for index in permutation]
        need(len(group_words) == int(count), 'Group count differs: ' + table)
        need(sum(w['enabled'] for w in group_words) == int(enabled_count), 'Enabled count differs: ' + table)
        groups.append(dict(id=group_id, symbol=group_symbol, name=name, wordValues=order))
        words.extend(group_words)
    need(len(groups) == 22 and len(words) == 1813, 'Complete vocabulary scope differs')
    by_value = {w['value']: w for w in words}
    def resolve(token):
        if token in token_values: return token_values[token]
        m = re.fullmatch(r'(EC_POKEMON2?|EC_MOVE2?)\((\w+)\)', token)
        need(m is not None, 'Unsupported alphabetical token: ' + token)
        macro, stem = m.groups()
        group = {'EC_POKEMON': 'EC_GROUP_POKEMON', 'EC_POKEMON2': 'EC_GROUP_POKEMON_2', 'EC_MOVE': 'EC_GROUP_MOVE_1', 'EC_MOVE2': 'EC_GROUP_MOVE_2'}[macro]
        index = species['SPECIES_' + stem] if 'POKEMON' in macro else moves['MOVE_' + stem]
        return (ec[group] << 9) | index
    letters_path = 'src/data/easy_chat/easy_chat_words_by_letter.h'; letters = src.text(letters_path)
    pointer_rows = re.findall(r'\.words = (sEasyChatWordsByLetter_\w+),\s*\.numWords = (\d+)', letters)
    alphabetical = []
    for bucket, (table, count) in enumerate(pointer_rows):
        body, ref = capture(src, letters_path, r'^static const u16 ' + table + r'\[\] = \{(.*?)^\};', table)
        tokens = [x.strip() for x in re.sub(r'//[^\n]*', '', body[1]).split(',') if x.strip()]
        need(len(tokens) == int(count), 'Alphabetical source count differs: ' + table)
        choices = []; offset = 0
        while offset < len(tokens):
            size = 1
            if tokens[offset] == '-1':
                size = int(tokens[offset + 1]); offset += 2
            values = [resolve(t) for t in tokens[offset:offset + size]]; offset += size
            need(len(values) == size and all(v in by_value for v in values), 'Unknown alphabetical word')
            choices.append(values)
        label = source_string(src, 'src/keyboard_text.c', 'gText_EasyChatKeyboard_ABCDEFothers')[0].split('}')[-1] if bucket == 0 else table.rsplit('_', 1)[-1]
        alphabetical.append(dict(id=bucket, name=label, alternatives=choices))
    need(len(alphabetical) == 27, 'Alphabetical bucket count differs')
    return dict(groups=groups, words=words, alphabetical=alphabetical)

def contracts(src):
    refs = [function(src, 'src/easy_chat.c', name, needles) for name, needles in [
        ('PopulateECGroups', ['GetNationalPokedexCount(FLAG_GET_SEEN)', 'EC_GROUP_POKEMON;', 'i = EC_GROUP_TRAINER; i <= EC_GROUP_ADJECTIVES', 'FlagGet(FLAG_SYS_GAME_CLEAR)', 'IsNationalPokedexEnabled()', 'EC_GROUP_POKEMON_2;']),
        ('UnlockedECMonOrMove', ['case EC_GROUP_POKEMON:', 'GetSetPokedexFlag(SpeciesToNationalPokedexNum(wordIndex), FLAG_GET_SEEN)', 'EC_IsDeoxys(wordIndex)', 'words[wordIndex].enabled']),
        ('PopulateAlphabeticalGroups', ['*words == EC_WORD_UNDEFINED', 'IsWordUnlocked(words[k])', 'break;']),
        ('GetUnlockedWordsInECGroup', ['wordInfo[i].alphabeticalOrder', 'UnlockedECMonOrMove(alphabeticalOrder, groupId)']),
        ('IsECWordInvalid', ['easyChatWord == EC_WORD_UNDEFINED', 'index == list[i]', 'index >= numWords'])]]
    refs += [function(src, path, name, needles) for path, name, needles in [
        ('src/mail_data.c', 'GiveMailToMon', ['words[i] = 0xFFFF', 'for (; i <= 5; i++)', 'playerName[i] = CHAR_SPACE', 'trainerId[i] = gSaveBlock2Ptr->playerTrainerId[i]', 'SpeciesToMailSpecies(species, personality)']),
        ('src/mail_data.c', 'SpeciesToMailSpecies', ['SPECIES_UNOWN', 'GetUnownLetterByPersonality(personality) + UNOWN_OFFSET']),
        ('src/pokemon_icon.c', 'GetUnownLetterByPersonality', ['((personality & 0x3000000) >> 18)', '((personality & 0x30000) >> 12)', '((personality & 0x300) >> 6)', '(personality & 0x3)', '% 0x1C']),
        ('src/easy_chat_2.c', 'Confirm_CreateYesNoMenu', ['IsEcWordBufferUninitialized()', 'sEasyChatScreen->state = 4;', 'sEasyChatScreen->state = 6;']),
        ('src/easy_chat_2.c', 'Confirm_HandleYesNoMenu', ['gSpecialVar_Result = HasECMessageChanged();', 'CommitECWords();']),
        ('src/easy_chat_2.c', 'IsEcWordBufferUninitialized', ['ecWordBuffer[i] != 0xFFFF']),
        ('src/party_menu.c', 'Task_HandleSwitchItemsYesNoInput', ['RemoveBagItem(gSpecialVar_ItemId, 1);', 'AddBagItem(sPartyMenuItemId, 1)', 'ItemIsMail(gSpecialVar_ItemId)']),
        ('src/party_menu.c', 'CB2_ReturnToPartyMenuFromWritingMail', ['gSpecialVar_Result == FALSE', 'TakeMailFromMon(mon)', 'SetMonData(mon, MON_DATA_HELD_ITEM, &sPartyMenuItemId)', 'RemoveBagItem(sPartyMenuItemId, 1)', 'AddBagItem(item, 1)']),
        ('src/party_menu.c', 'CB2_ReturnToPartyOrBagMenuFromWritingMail', ['gSpecialVar_Result == FALSE', 'TakeMailFromMon(mon)', 'ReturnGiveItemToBagOrPC(item)'])]]
    ec = constants(src, 'include/constants/easy_chat.h'); sp = constants(src, 'include/constants/species.h')
    chars = dict(re.findall(r"^'((?:\\.|[^'\\])*)'\s*=\s*([0-9A-F]{2})\s*$", src.text('charmap.txt'), re.M))
    # Single-character text encoding. EOS is never a player-name character.
    chars = {unescape(c): int(v, 16) for c, v in chars.items() if len(unescape(c)) == 1 and int(v, 16) < 0xFA}
    need(chars[' '] == 0 and chars['A'] == 0xBB, 'Source character map differs')
    texts = []
    for symbol in ['gText_CombineNineWordsOrPhrases', 'gText_AndMakeAMessage', 'gText_TheMailMessage', 'gText_IsAsShownOkay', 'gText_AllTextBeingEditedWill', 'gText_BeDeletedThatOkay', 'gText_StopGivingPkmnMail', 'gText_DelAllCancelOk']:
        text, ref = source_string(src, 'src/strings.c', symbol); texts.append(dict(symbol=symbol, text=text))
    for suffix in ['ABCDEFothers', 'GHIJKL', 'MNOPQRS', 'TUVWXYZ']:
        symbol = 'gText_EasyChatKeyboard_' + suffix
        text, ref = source_string(src, 'src/keyboard_text.c', symbol); texts.append(dict(symbol=symbol, text=text))
    return dict(seenPokemonGroup=ec['EC_GROUP_POKEMON'], nationalPokemonGroup=ec['EC_GROUP_POKEMON_2'],
        deoxysSpecies=sp['SPECIES_DEOXYS'], unownSpecies=sp['SPECIES_UNOWN'],
        nameCharacters=chars, texts=texts)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True); parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--dependency-root', type=Path, default=ROOT); parser.add_argument('--check', action='store_true')
    args = parser.parse_args(); target = args.project_root.absolute(); dependency = args.dependency_root.absolute()
    helper = dependency/'scripts/imports/source/import-cinnabar-source-preparation.py'; safe(helper)
    spec = importlib.util.spec_from_file_location('mail_composer_source', helper); reader = importlib.util.module_from_spec(spec); spec.loader.exec_module(reader)
    reader.PROFILE = PROFILE; src = reader.Source(args.reference_root.absolute())
    docs = {'catalog.json': vocabulary(src), 'contracts.json': contracts(src)}
    lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=[], completeGame=False); lock_bytes = canonical(lock)
    generated = {p: canonical(value) for p, value in docs.items()}
    manifest = dict(schemaVersion=1,     inputCount=len(src.entries), wordCount=len(docs['catalog.json']['words']), groupCount=22,
        outputs=[dict(path=p) for p, b in sorted(generated.items())])
    generated['manifest.json'] = canonical(manifest)
    writes = []
    writes += [(target/'content/raw/pokefirered/037335f4'/p, b) for p, b in sorted(src.data.items())]
    writes += [(target/'content/generated'/PACKAGE/p, b) for p, b in sorted(generated.items())]
    for path, data in writes:
        safe(path)
        if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    print(json.dumps(dict( inputCount=len(src.entries), outputCount=2,
         wordCount=len(docs['catalog.json']['words']), check=args.check), indent=2))

if __name__ == '__main__': main()
