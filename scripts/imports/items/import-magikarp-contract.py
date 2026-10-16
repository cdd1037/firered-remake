#!/usr/bin/env python3
"""Authenticate the bounded Route4 gift contract; never execute upstream code or fetch assets."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-route4-magikarp-source-contract-v1'
# Only dependencies absent from the Route3 region import are materialized here.
# Reviewed Git object identity is embedded in this script, not trusted from generated output.
MISSING = (
    'include/random.h',
    'src/random.c',
    'src/naming_screen.c',
    'src/data/region_map/region_map_sections.json',
    'src/data/region_map/region_map_sections.constants.json.txt',
)
REGION_PATHS = (
    'asm/macros/event.inc', 'data/event_scripts.s',
    'data/maps/Route4_PokemonCenter_1F/map.json',
    'data/maps/Route4_PokemonCenter_1F/scripts.inc',
    'data/maps/Route4_PokemonCenter_1F/text.inc',
    'data/scripts/pc_transfer.inc', 'data/scripts/std_msgbox.inc', 'data/text/pc_transfer.inc',
    'include/constants/global.h', 'include/constants/items.h', 'include/constants/flags.h',
    'include/constants/pokemon.h', 'include/constants/species.h', 'include/constants/vars.h',
    'src/data/pokemon/species_info.h', 'src/data/pokemon/level_up_learnsets.h',
    'src/data/battle_moves.h', 'src/field_specials.c', 'src/money.c', 'src/overworld.c',
    'src/pokemon.c', 'src/pokemon_storage_system.c', 'src/scrcmd.c', 'src/script_pokemon_util.c',
)
COMMON_SCRIPTS = (
    'EventScript_GetGiftMonPartySlot', 'EventScript_TransferredToPC',
    'EventScript_TransferredSomeonesPC', 'EventScript_TransferredSomeonesPCBoxFull',
    'EventScript_TransferredBillsPC', 'EventScript_TransferredBillsPCBoxFull',
    'Std_MsgboxDefault', 'Std_MsgboxYesNo',
)
COMMON_TEXTS = (
    'Text_GiveNicknameToThisMon', 'Text_MonSentToBoxInSomeonesPC',
    'Text_MonSentToBoxInBillsPC', 'Text_MonSentToBoxSomeonesBoxFull',
    'Text_MonSentToBoxBillsBoxFull',
)


def encode(document):
    return (json.dumps(document, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')


def safe_file(base, relative):
    base = Path(base).absolute()
    path = base / relative
    if Path(relative).is_absolute() or '..' in Path(relative).parts or path == base:
        raise ValueError('Unsafe relative file: ' + str(relative))
    for cursor in (path, *path.parents):
        if cursor.is_symlink():
            raise ValueError('Symlink source/output is unsupported: ' + str(cursor))
    return path


def missing_entries():
    return [dict(path=p) for p in MISSING]


def materialize(reference, raw):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env['GIT_NO_REPLACE_OBJECTS'] = '1'
    def git(*args):
        return subprocess.check_output(['git', '-C', str(reference), *args], env=env)
    pending = []
    for entry in missing_entries():
        path = safe_file(raw, entry['path'])
        kind = git('ls-tree', COMMIT, '--', entry['path']).decode('ascii').split()
        data = git('show', COMMIT + ':' + entry['path'])
        pending.append((path, data))
    for path, data in pending:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def read_sources(project=ROOT, raw=None, region_raw=None):
    project = Path(project)
    raw = Path(raw) if raw is not None else project / 'content/raw/pokefirered/037335f4'
    region_raw = Path(region_raw) if region_raw is not None else project / 'content/raw/pokefirered/037335f4'
    sources, entries = {}, []
    for path in sorted(set(REGION_PATHS) | set(MISSING)):
        base = region_raw if path in REGION_PATHS else raw
        sources[path] = safe_file(base, path).read_bytes().decode('utf-8', errors='strict')
        entries.append(dict(path=path, sourceDirectory='content/raw/pokefirered/037335f4'))
    return sources, sorted(entries, key=lambda entry: entry['path'])


def reference(sources, path, symbol, start, end):
    return dict(repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol,
                 line=start, endLine=end)


def blocks(sources):
    result = {}
    for path in ('data/maps/Route4_PokemonCenter_1F/scripts.inc',
                 'data/maps/Route4_PokemonCenter_1F/text.inc', 'data/scripts/pc_transfer.inc',
                 'data/scripts/std_msgbox.inc', 'data/text/pc_transfer.inc', 'data/event_scripts.s'):
        text = sources[path]
        labels = list(re.finditer(r'^(\w+)::?\s*$', text, re.M))
        for i, label in enumerate(labels):
            finish = labels[i + 1].start() if i + 1 < len(labels) else len(text)
            body = text[label.end():finish].splitlines()
            # A .include is not part of the previous label's body.
            body = [line for line in body if line.strip() and not line.lstrip().startswith(('.include', '@'))]
            if label[1] in result:
                raise ValueError('Duplicate source label: ' + label[1])
            line = text[:label.start()].count('\n') + 1
            result[label[1]] = dict(label=label[1], sourceRef=reference(sources, path, label[1], line,
                text[:finish].count('\n')), lines=body)
    return result


def function(sources, path, symbol):
    text = sources[path]
    matches = list(re.finditer(r'^[^\n;{}]+\b' + re.escape(symbol) + r'\([^\n]*\)\n\{', text, re.M))
    if len(matches) != 1:
        raise ValueError('Expected one function definition: ' + symbol)
    start = matches[0].start()
    ending = re.search(r'^\}', text[matches[0].end():], re.M)
    if ending is None:
        raise ValueError('Unclosed source function: ' + symbol)
    end = matches[0].end() + ending.end()
    return dict(sourceRef=reference(sources, path, symbol, text[:start].count('\n') + 1,
                text[:end].count('\n') + 1), text=text[start:end] + '\n')


def build(sources, entries):
    all_blocks = blocks(sources)
    root = 'Route4_PokemonCenter_1F_EventScript_MagikarpSalesman'
    selected, pending = {}, [root]
    while pending:
        symbol = pending.pop()
        if symbol in selected:
            continue
        selected[symbol] = all_blocks[symbol]
        for line in selected[symbol]['lines']:
            if line.strip().startswith('.string'):
                continue
            for token in re.findall(r'\b\w+\b', line):
                if token in all_blocks and token not in selected:
                    pending.append(token)
            if line.strip().startswith('msgbox '):
                pending.append('Std_MsgboxYesNo' if 'MSGBOX_YESNO' in line else 'Std_MsgboxDefault')
    if not set(COMMON_SCRIPTS + COMMON_TEXTS) <= selected.keys():
        raise ValueError('Gift common-script closure is incomplete')
    extra_common = {name for name in selected if not name.startswith('Route4_PokemonCenter_1F_')} - set(COMMON_SCRIPTS + COMMON_TEXTS)
    if extra_common:
        raise ValueError('Unexpected common-script scope: ' + ', '.join(sorted(extra_common)))
    sections = json.loads(sources['src/data/region_map/region_map_sections.json'])['map_sections']
    met_location, = [i for i, section in enumerate(sections) if section['id'] == 'MAPSEC_ROUTE_4']
    if met_location != 104 or json.loads(sources['data/maps/Route4_PokemonCenter_1F/map.json'])['region_map_section'] != 'MAPSEC_ROUTE_4':
        raise ValueError('Gift met-location mapping differs')
    snippets = []
    for path, names in (
        ('src/script_pokemon_util.c', ('ScriptGiveMon',)),
        ('src/pokemon.c', ('CreateMon', 'CreateBoxMon', 'GiveBoxMonInitialMoveset', 'GiveMonToPlayer', 'SendMonToPC')),
        ('src/field_specials.c', ('ChangePokemonNickname', 'ChangePokemonNickname_CB', 'ChangeBoxPokemonNickname', 'ChangeBoxPokemonNickname_CB', 'SetPCBoxToSendMon', 'GetPCBoxToSendMon', 'ShouldShowBoxWasFullMessage')),
        ('src/scrcmd.c', ('ScrCmd_givemon', 'ScrCmd_getpartysize', 'ScrCmd_bufferboxname', 'ScrCmd_checkplayergender', 'ScrCmd_checkmoney', 'ScrCmd_removemoney')),
        ('src/money.c', ('IsEnoughMoney', 'RemoveMoney')),
        ('src/overworld.c', ('GetCurrentRegionMapSectionId',)),
        ('src/pokemon_storage_system.c', ('StorageGetCurrentBox', 'SetCurrentBox')),
        ('src/naming_screen.c', ('SaveInputText', 'MainState_PressedOKButton')),
        ('src/random.c', ('Random',)),
    ):
        snippets.extend(function(sources, path, name) for name in names)
    constants = dict(MAGIKARP_PRICE=500, SPECIES_MAGIKARP=129, MAGIKARP_LEVEL=5,
        MON_GIVEN_TO_PARTY=0, MON_GIVEN_TO_PC=1, MON_CANT_GIVE=2, USE_RANDOM_IVS=32,
        FLAG_BOUGHT_MAGIKARP=0x249, FLAG_SYS_NOT_SOMEONES_PC=0x834,
        FLAG_SHOWN_BOX_WAS_FULL_MESSAGE=0x843, VAR_TEMP_1=0x4001,
        VAR_PC_BOX_TO_SEND_MON=0x4037, VAR_0x8004=0x8004, VAR_RESULT=0x800D,
        MAPSEC_ROUTE_4=met_location, ITEM_NONE=0, ITEM_POKE_BALL=4,
        VERSION_FIRE_RED=4, LANGUAGE_ENGLISH=2, POKEMON_NAME_LENGTH=10)
    contract = dict(schemaVersion=1,

        gameplayEnabled=False, constants=constants,
        gift=dict(species='SPECIES_MAGIKARP', level=5, price=500, heldItem='ITEM_NONE',
            personality='Random() | (Random() << 16)', shinyLock=False, ivDraws=2, totalRngDraws=4,
            ivOrder=[['HP', 'Attack', 'Defense'], ['Speed', 'SpecialAttack', 'SpecialDefense']],
            ivBitShifts=[0, 5, 10], ivMask=31, originalTrainer='player-full-32-bit-id-name-gender',
            metLocation='MAPSEC_ROUTE_4', metLevel=5, metGame=4, language=2, ball='ITEM_POKE_BALL',
            defaultNickname='MAGIKARP', friendship=70, initialMoves=['MOVE_SPLASH'], initialPp=[40],
            evs=[0, 0, 0, 0, 0, 0], status=0, fullHp=True),
        resultSemantics=[
            dict(result=0, destination='first-empty-party-slot', consumesRng=True, setsSeenCaught=True, chargesMoney=True, setsBoughtAfterDialogue=True),
            dict(result=1, destination='first-empty-PC-slot-current-box-then-wrap', consumesRng=True, setsSeenCaught=True, chargesMoney=True, setsBoughtAfterDialogue=True),
            dict(result=2, destination=None, consumesRng=True, setsSeenCaught=False, chargesMoney=False, setsBoughtAfterDialogue=False)],
        ordering=['already-bought-check', 'show-money', 'gender-offer', 'yes-no', 'money-check',
            'generate-before-capacity-check', 'deliver-and-set-seen-caught-on-success', 'remove-500',
            'update-money', 'paid-text-and-level-up-fanfare', 'nickname-choice',
            'optional-nickname-callback', 'PC-transfer-message-if-boxed', 'set-bought-flag'],
        pc=dict(currentBoxChanges=False, slotsPerBox=30, boxCount=14,
            oldBox='previous VAR_PC_BOX_TO_SEND_MON, saved by SetPCBoxToSendMon before scanning',
            destination='VAR_PC_BOX_TO_SEND_MON after success; gSpecialVar_MonBoxId and gSpecialVar_MonBoxPos',
            fullNotice='not FLAG_SHOWN_BOX_WAS_FULL_MESSAGE and currentBox != destination; sets flag when true',
            clearsFullNotice='old VAR_PC_BOX_TO_SEND_MON != actual destination',
            buffers=dict(STR_VAR_1='actual destination box name', STR_VAR_2='MAGIKARP species name even after nickname', STR_VAR_3='previous VAR_PC_BOX_TO_SEND_MON box name, only when full notice')),
        nickname=dict(partySlot='getpartysize - 1 copied to VAR_0x8004',
            boxSlot='gSpecialVar_MonBoxId and gSpecialVar_MonBoxPos',
            oldNameBuffer='STR_VAR_3', editableNameAndResultBuffer='STR_VAR_2',
            namingTemplate='NAMING_SCREEN_NICKNAME', maximumCharacters=10,
            initialInputEmpty=True, emptyOrAllSpaces='preserve original name',
            transferHandledBy='script after callback; not NAMING_SCREEN_CAUGHT_MON'),
        commonScripts=list(COMMON_SCRIPTS), commonTexts=list(COMMON_TEXTS),
        scripts=[selected[name] for name in sorted(selected)], sourceFunctions=snippets)
    outputs = {'magikarp-contract.json': runtime_json_bytes(contract, sort_keys=True)}
    manifest = dict(schemaVersion=1,
         reusedRegionSourceCount=len(REGION_PATHS),
        newSourceCount=len(MISSING), scriptBlockCount=len(selected), commonScriptCount=len(COMMON_SCRIPTS),
        commonTextCount=len(COMMON_TEXTS), sourceFunctionCount=len(snippets),
        outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    outputs['manifest.json'] = runtime_json_bytes(manifest, sort_keys=True)
    return outputs


def run(project=ROOT, raw=None, region_raw=None, output=None, check=False):
    sources, entries = read_sources(project, raw, region_raw)
    outputs = build(sources, entries)
    output = Path(output) if output is not None else Path(project) / 'content/generated/data/services/magikarp-gift'
    for relative, data in outputs.items():
        path = safe_file(output, relative)
        if check:
            if path.read_bytes() != data:
                raise ValueError('Generated contract differs: ' + relative)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    return json.loads(outputs['manifest.json'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--materialize', action='store_true', help='Read the five missing source blobs from the fixed local Git commit')
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--check', action='store_true', help='Verify existing output without writing')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if args.materialize:
        reference_root = args.reference or Path((ROOT / '.tools/reference-root').read_text(encoding='utf-8').strip())
        materialize(reference_root, ROOT / 'content/raw/pokefirered/037335f4')
    print(json.dumps(run(output=args.output, check=args.check), sort_keys=True))


if __name__ == '__main__':
    main()
