#!/usr/bin/env python3
"""Authenticate local pinned Git blobs and import original PC scripts, texts and contracts."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


import argparse
import ast
import collections
import json
import pathlib
import re
import shutil
import subprocess
import tempfile

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-pokemon-storage-source-v1'


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')) + '\n'


def pairs(rows):
    result = {}
    for key, value in rows:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def decode(data):
    return json.loads(data, object_pairs_hook=pairs)


def safe(root, path):
    root, path = pathlib.Path(root), pathlib.Path(path)
    if any(p.is_symlink() for p in [root, path, *path.parents]):
        raise ValueError('Symlink input/output is unsupported')
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('Target escapes selected directory')
    return path


def read_lock(path=None):
    data = (path or ROOT / 'scripts/recipes/source/fetch-pokemon-storage.lock.json').read_bytes()
    lock = decode(data)
    if (lock['schemaVersion'] != 1 or lock['repository'] != 'pret/pokefirered' or lock['commit'] != COMMIT or (lock['profile'] != PROFILE) or (lock['gameVersion'] != 'FIRERED') or (lock['revision'] != 0) or (lock['completeGame'] is not False)):
        raise ValueError('Source profile differs')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path'] or entry['path'] in seen:
            raise ValueError('Unsafe or duplicate source path')
        seen.add(entry['path'])
    return lock




def materialize(raw, reference_repo, lock_path=None):
    raw = pathlib.Path(raw)
    lock = read_lock(lock_path)
    def git(*args):
        return subprocess.check_output(['git', '-C', str(reference_repo), *args])
    tree = {}
    for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
        if row:
            fields, name = row.split(b'\t')
            tree[name.decode()] = tuple(fields.decode().split())
    verified = {}
    for entry in lock['files']:
        safe(raw, raw / entry['path'])
        data = git('cat-file', 'blob', COMMIT + ':' + entry['path'])
        verified[entry['path']] = data
    for path, data in verified.items():
        target = raw / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)


class Source:
    def __init__(self, raw, lock_path=None):
        raw = pathlib.Path(raw)
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.data = {}
        for path, entry in self.entries.items():
            data = safe(raw, raw / path).read_bytes()
            self.data[path] = data

    def text(self, path):
        return self.data[path].decode('utf-8')

    def ref(self, path, symbol, line=None, end=None):
        lines = self.text(path).splitlines()
        if line is None:
            matches = [i for i, text in enumerate(lines, 1) if re.search(r'\b' + re.escape(symbol) + r'\b', text)]
            if not matches:
                raise ValueError('Missing source symbol: ' + symbol)
            line = matches[0]
        return dict(path=path, symbol=symbol, startLine=line, endLine=end or line)


def script_inventory(src):
    blocks = {}
    script_paths = [p for p, e in src.entries.items() if e['roles'][0] in ('script', 'text')]
    for path in script_paths:
        current = None
        for number, line in enumerate(src.text(path).splitlines(), 1):
            label = re.fullmatch(r'(\w+)::?', line.split('@')[0].strip())
            if label:
                if label[1] in blocks:
                    raise ValueError('Duplicate script label: ' + label[1])
                current = dict(label=label[1], source=src.ref(path, label[1], number), lines=[])
                blocks[label[1]] = current
            elif current is not None:
                current['lines'].append(dict(line=number, text=line))
    roots = ['EventScript_PC', 'EventScript_TransferredToPC', 'EventScript_NameReceivedBoxMon',
             'EventScript_GetGiftMonPartySlot', 'Std_MsgboxDefault', 'Std_MsgboxYesNo']
    selected, pending, refs = set(), roots[:], []
    commands, specials = collections.defaultdict(list), collections.defaultdict(list)
    # Pinned compiler baseline. Unknown source opcodes are retained and labelled unsupported.
    supported = set('goto_if_questlog lockall lock releaseall release setvar special playse msgbox goto end message waitmessage waitstate switch case call_if_unset call_if_set return goto_if_unset goto_if_set setflag clearflag call specialvar copyvar goto_if_eq call_if_eq playfanfare waitfanfare textcolor fadescreen faceplayer bufferspeciesname'.split())
    while pending:
        label = pending.pop()
        if label in selected:
            continue
        if label not in blocks:
            raise ValueError('Unresolved source label: ' + label)
        selected.add(label)
        block = blocks[label]
        for row in block['lines']:
            line = row['text'].strip()
            if line.startswith('.string'):
                continue
            line = line.split('@')[0].strip()
            if not line or line.startswith(('.', '#')):
                continue
            op, _, operand = line.partition(' ')
            op = op.split('\t')[0]
            # Assembly indentation may separate opcode/operands by arbitrary whitespace.
            tokens = line.split(None, 1)
            op, operand = tokens[0], tokens[1] if len(tokens) == 2 else ''
            loc = dict(label=label, path=block['source']['path'], line=row['line'])
            commands[op].append(loc)
            if op in ('special', 'specialvar'):
                specials[operand.split(',')[-1].strip()].append(loc)
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'):
                target = operand.strip()
            elif op.startswith(('goto_if_', 'call_if_')):
                target = operand.split(',')[-1].strip()
            elif op == 'case':
                target = re.split(r'[,\s]+', operand)[-1]
            elif op == 'msgbox':
                target = operand.split(',')[0].strip()
            if target and target not in ("0", "0x0", "NULL"):
                if target not in blocks:
                    raise ValueError('Unresolved source label: ' + target)
                refs.append(dict(source=label, target=target, line=row['line']))
                pending.append(target)
    return dict(roots=roots, blocks=[blocks[k] for k in sorted(selected)], references=refs,
                unresolvedLabelReferences=[],
                opcodes=[dict(opcode=op, compilerStatus='supported' if op in supported else 'unsupported', occurrences=locs)
                         for op, locs in sorted(commands.items())],
                specials=[dict(symbol=symbol, runtimeStatus='requiresNamedHandlerAudit', occurrences=locs)
                          for symbol, locs in sorted(specials.items())])


def constants_inventory(src, scripts):
    expressions, locations, values = {}, {}, {}
    for path in src.data:
        if not path.startswith('include/'):
            continue
        for number, line in enumerate(src.text(path).splitlines(), 1):
            m = re.match(r'#define\s+([A-Z][A-Za-z0-9_]+)\s+(.+)', line)
            if m:
                expressions[m[1]] = m[2].split('//')[0].strip()
                locations[m[1]] = (path, number)
    def evaluate(node):
        if isinstance(node, ast.Constant) and type(node.value) is int:
            return node.value
        if isinstance(node, ast.Name):
            return values[node.id]
        if isinstance(node, ast.BinOp):
            a, b = evaluate(node.left), evaluate(node.right)
            if isinstance(node.op, ast.Add): return a + b
            if isinstance(node.op, ast.Sub): return a - b
            if isinstance(node.op, ast.Mult): return a * b
            if isinstance(node.op, ast.LShift): return a << b
            if isinstance(node.op, ast.BitOr): return a | b
        raise ValueError('Unsupported constant expression')
    for _ in range(len(expressions)):
        progress = False
        for name, expression in expressions.items():
            if name in values: continue
            try: value = evaluate(ast.parse(expression, mode='eval').body)
            except (KeyError, ValueError, SyntaxError): continue
            values[name] = value
            progress = True
        if not progress: break
    for path, marker in [('include/pokemon_storage_system_internal.h', 'OPTION_WITHDRAW'),
                         ('include/pokemon_storage_system.h', 'WALLPAPER_FOREST')]:
        text = src.text(path)
        body = next(b for b in re.findall(r'enum\s*\{(.*?)\};', text, re.S) if marker in b)
        for index, name in enumerate(re.findall(r'\b[A-Z][A-Z0-9_]+\b', body)):
            values[name] = index
            locations[name] = (path, next(i for i, line in enumerate(text.splitlines(), 1) if re.search(r'\b' + name + r'\b', line)))
    wanted = set(re.findall(r'\b(?:FLAG_|VAR_|HELPCONTEXT_|SE_|MUS_|SCR_MENU_)[A-Za-z0-9_]+\b',
                           '\n'.join(r['text'] for b in scripts['blocks'] for r in b['lines'])))
    wanted.update('TOTAL_BOXES_COUNT IN_BOX_ROWS IN_BOX_COLUMNS IN_BOX_COUNT BOX_NAME_LENGTH PARTY_SIZE MB_PC SCR_MENU_CANCEL SCR_MENU_UNSET FLAG_SHOWN_BOX_WAS_FULL_MESSAGE VAR_PC_BOX_TO_SEND_MON FLAG_SYS_NOT_SOMEONES_PC FLAG_SYS_POKEDEX_GET FLAG_SYS_GAME_CLEAR FLAG_SYS_PC_STORAGE_DISABLED HELPCONTEXT_BILLS_PC HELPCONTEXT_PLAYERS_PC_ITEMS HELPCONTEXT_PLAYERS_PC_MAILBOX'.split())
    wanted.update(k for k in values if k.startswith(('OPTION_', 'WALLPAPER_')) or k == 'OPTIONS_COUNT')
    result = {}
    for key in sorted(wanted):
        if key not in values:
            raise ValueError('Missing required constant: ' + key)
        path, line = locations[key]
        result[key] = dict(value=values[key], source=src.ref(path, key, line))
    return result


def ui_texts(src):
    names = set('gText_SomeoneSPc gText_BillSPc gText_SPc gText_HallOfFame_2 gText_LogOff gText_ProfOakSPc gText_BoxFull gText_Box gText_JustOnePkmn gText_PartyFull gText_WithdrawPokemon gText_DepositPokemon gText_MovePokemon gText_MoveItems gText_SeeYa gText_WithdrawMonDescription gText_DepositMonDescription gText_MoveMonDescription gText_MoveItemsDescription gText_SeeYaDescription'.split())
    source = src.text('src/strings.c')
    # Every native box action/error caption is retained, including unsupported actions.
    start, end = source.index('const u8 gText_ExitFromBox'), source.index('const u8 gText_CombineFourWordsOrPhrases')
    names.update(re.findall(r'const u8 (\w+)\[\]', source[start:end]))
    result = {}
    for path in ['src/strings.c', 'src/battle_message.c']:
        for m in re.finditer(r'(?:static )?const u8 (\w+)\[\] = _\("((?:\\.|[^"\\])*)"\);', src.text(path)):
            if m[1] in names or m[1] == 'sText_BoxIsFull':
                result[m[1]] = dict(text=m[2] + '$', source=src.ref(path, m[1], src.text(path)[:m.start()].count('\n') + 1))
    if not names <= result.keys():
        raise ValueError('Missing source UI strings: ' + ', '.join(sorted(names - result.keys())))
    return result


def menus(src, constants):
    pc = src.text('src/script_menu.c')
    if 'gSpecialVar_Result = input;' not in pc or 'gSpecialVar_Result = SCR_MENU_CANCEL;' not in pc:
        raise ValueError('PC source menu result rule changed')
    for needle in ['numItems = 5;', 'numItems = 4;', 'numItems = 3;', 'FlagGet(FLAG_SYS_NOT_SOMEONES_PC)',
                   'CreateMCMenuInputHandlerTask(FALSE, numItems, windowId, MULTICHOICE_NONE)']:
        if needle not in pc: raise ValueError('Dynamic PC source menu shape differs')
    variants = []
    for key, pokedex, clear, labels in [
        ('beforePokedex', False, False, ['gText_SomeoneSPc', 'gText_SPc', 'gText_LogOff']),
        ('withPokedex', True, False, ['gText_SomeoneSPc', 'gText_SPc', 'gText_ProfOakSPc', 'gText_LogOff']),
        ('gameClear', True, True, ['gText_SomeoneSPc', 'gText_SPc', 'gText_ProfOakSPc', 'gText_HallOfFame_2', 'gText_LogOff'])]:
        variants.append(dict(id=key, pokedex=pokedex, gameClear=clear, textSymbols=labels,
                             resultValues=list(range(len(labels))), cancelResult=constants['SCR_MENU_CANCEL']['value'],
                             wrapAround=len(labels)>3, source=src.ref('src/script_menu.c', 'CreatePCMenuWindow', 986, 1039)))
    menu_source = src.text('src/pokemon_storage_system_menu.c')
    table = re.search(r'sMainMenuTexts\[OPTIONS_COUNT\] = \{(.*?)\};', menu_source, re.S)[1]
    rows = []
    for option, caption, desc in re.findall(r'\[(OPTION_\w+)\]\s*=\s*\{(\w+),\s*(\w+)\}', table):
        rows.append(dict(option=option, result=constants[option]['value'], textSymbol=caption, descriptionSymbol=desc,
                         runtimeStatus='requiresNamedHandlerAudit' if option in ('OPTION_WITHDRAW','OPTION_DEPOSIT','OPTION_EXIT') else 'unsupported',
                         source=src.ref('src/pokemon_storage_system_menu.c', 'sMainMenuTexts', 37, 43)))
    if len(rows) != constants['OPTIONS_COUNT']['value']: raise ValueError('Storage menu option count differs')
    return variants, rows


def make_document(src):
    scripts = script_inventory(src)
    constants = constants_inventory(src, scripts)
    variants, storage = menus(src, constants)
    rules = [(p, symbol) for p, symbols in {
        'src/field_control_avatar.c': ['GetInteractedMetatileScript'],
        'src/player_pc.c': ['PlayerPC','Task_PlayerPcTurnOff'],
        'src/pokemon.c': ['BoxMonToMon','CalculateMonStats','GiveMonToPlayer','SendMonToPC','CalculatePPWithBonus','MonRestorePP','BoxMonRestorePP','IsPlayerPartyAndPokemonStorageFull'],
        'src/pokemon_storage_system_data.c': ['SetPlacedMonData','TryStorePartyMonInBox','CompactPartySlots','CanMovePartyMon'],
        'src/pokemon_storage_system_menu.c': ['GetFirstFreeBoxSpot','CountPartyAliveNonEggMonsExcept','ShowPokemonStorageSystemPC','Task_PCMainMenu','CB2_ExitPokeStorage','ResetPokemonStorageSystem'],
        'src/pokemon_storage_system_tasks.c': ['Task_WithdrawMon','Task_DepositMenu'],
        'src/field_specials.c': ['SetPCBoxToSendMon','GetPCBoxToSendMon','ShouldShowBoxWasFullMessage'],
        'src/mail_data.c': ['ItemIsMail'],
        'src/item_use.c': ['IsPlayerPartyAndPokemonStorageFull'],
        'src/battle_script_commands.c': ['Cmd_givecaughtmon'],
        'data/maps/Route25_SeaCottage/scripts.inc': ['FLAG_SYS_NOT_SOMEONES_PC'],
    }.items() for symbol in symbols]
    return dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT,
                scripts=scripts, constants=constants, uiTexts=ui_texts(src), pcMenuVariants=variants, storageMenu=storage,
                rulesSources=[src.ref(path,symbol) for path,symbol in rules],
                entry='EventScript_PC', storageEntry='EventScript_AccessPokemonStorage', returnEntry='EventScript_PCMainMenu',
                powerOffEntry='EventScript_TurnOffPC', namingFlag='FLAG_SYS_NOT_SOMEONES_PC', alternateStoragePcText='gText_BillSPc',
                sourceMaps=['MAP_VIRIDIAN_CITY_POKEMON_CENTER_1F','MAP_PEWTER_CITY_POKEMON_CENTER_1F'],
                limitations=['Runtime support is separately implemented and tested; this source catalog enables no native handler.',
                             'Move Pokemon, Move Items, release, rename, wallpapers, Oak rating and Hall of Fame remain explicit unsupported boundaries until certified.',
                             'Original box graphics and wallpapers are not imported by this source-event increment.',
                             'Existing Pokemon, item and map catalogs remain authoritative; no duplicate species or map definitions are created.'])


def import_catalog(raw, out, lock_path=None):
    raw, out = pathlib.Path(raw), pathlib.Path(out)
    safe(out, out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Input and output trees must be disjoint')
    src = Source(raw, lock_path)
    document = make_document(src)
    out.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.pokemon-storage-', dir=out.parent) as tmp:
        stage = pathlib.Path(tmp)
        data = canonical(document).encode('utf-8')
        (stage / 'pokemon-storage.json').write_bytes(data)
        manifest = dict(schemaVersion=1, inputCount=len(src.entries), outputs=[dict(path='pokemon-storage.json')])
        (stage / 'manifest.json').write_text(canonical(manifest), encoding='utf-8')
        for path in stage.iterdir(): safe(out, out / path.name)
        out.mkdir(parents=True, exist_ok=True)
        for path in sorted(stage.iterdir()): shutil.copyfile(path, out / path.name)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ui/pokemon-storage')
    parser.add_argument('--reference-repo',type=pathlib.Path,help='Read immutable local Git blobs only; never run upstream scripts.')
    args = parser.parse_args()
    if args.reference_repo is not None: materialize(args.raw,args.reference_repo)
    print(canonical(import_catalog(args.raw,args.out)),end='')
