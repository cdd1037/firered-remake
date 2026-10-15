#!/usr/bin/env python3
"""Import pinned Oak rating logic, original text and bounded script dependencies, offline.

Reads immutable local Git objects or an authenticated raw cache. No upstream code is
executed, no repository is fetched, and no gameplay or National Dex gate is enabled.
"""
import argparse
import ast
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-oak-rating-v1'
RATING_SCRIPT = 'data/scripts/pokedex_rating.inc'
PC_SCRIPT = 'data/scripts/pc.inc'
LAB_SCRIPT = 'data/maps/PalletTown_ProfessorOaksLab/scripts.inc'


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is not None:
        p = pathlib.PurePosixPath(relative)
        if not relative or str(p) != relative or p.is_absolute() or '\\' in relative or any(x in ('', '.', '..') for x in relative.split('/')):
            raise ValueError('Unsafe Oak rating path: ' + relative)
        root = root / relative
    for p in [root, *root.parents]:
        if p.is_symlink():
            raise ValueError('Oak rating symlink is unsupported: ' + str(p))
    return root


def read(path, expected_length=None):
    path = safe(path)
    maximum = expected_length if expected_length is not None else 2_000_000
    if not path.is_file() or path.stat().st_size > maximum:
        raise ValueError('Missing or oversized Oak rating input: ' + str(path))
    with path.open('rb') as stream:
        data = stream.read(maximum + 1)
    if len(data) > maximum or (expected_length is not None and len(data) != expected_length):
        raise ValueError('Oak rating input size differs: ' + str(path))
    return data


def read_lock(path=None):
    data = read(path or ROOT / 'content/oak-rating-source-lock.json')
    lock = json.loads(data, object_pairs_hook=unique)
    if (lock['schemaVersion'], lock['commit'], lock['profile'], len(lock['files'])) != (1, COMMIT, PROFILE, 34):
        raise ValueError('Oak rating source identity differs')
    return lock


def disjoint(*roots):
    roots = [safe(root) for root in roots]
    if any(a == b or a in b.parents or b in a.parents for i, a in enumerate(roots) for b in roots[i + 1:]):
        raise ValueError('Oak rating repository, raw and output roots must be disjoint')


def preflight(targets):
    for target, _ in targets:
        safe(target)
        if target.exists() and not target.is_file():
            raise ValueError('Oak rating destination is not a file: ' + str(target))
        for parent in target.parents:
            if parent.exists() and not parent.is_dir():
                raise ValueError('Oak rating destination parent is not a directory: ' + str(parent))


def write_all(targets):
    preflight(targets)
    for target, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + '.tmp')
        if temporary.exists() or temporary.is_symlink():
            raise ValueError('Unexpected Oak rating temporary file: ' + str(temporary))
        created = False
        try:
            with temporary.open('xb') as stream:
                created = True
                stream.write(data)
            temporary.replace(target)
        finally:
            if created and temporary.is_file() and not temporary.is_symlink():
                temporary.unlink()


class Source:
    def __init__(self, raw, lock_path=None, repo=None):
        self.lock = read_lock(lock_path)
        self.entries = {e['path']: e for e in self.lock['files']}
        self.bytes = {}
        for path, entry in self.entries.items():
            if repo is None:
                data = read(safe(raw, path))
            else:
                data = subprocess.check_output(['git', '-C', str(safe(repo)), 'show', COMMIT + ':' + path])
            self.bytes[path] = data
        self.texts = {p: b.decode('utf-8') for p, b in self.bytes.items()}

    def one(self, path, pattern):
        matches = list(re.finditer(pattern, self.texts[path], re.M))
        if len(matches) != 1:
            raise ValueError('Expected one original source match: ' + path + ': ' + pattern)
        return matches[0]

    def reference(self, path, symbol, match):
        text = self.texts[path]
        return dict(path=path, symbol=symbol, startLine=text.count('\n', 0, match.start()) + 1,
                    endLine=text.count('\n', 0, match.end() - 1) + 1)

    def block(self, path, symbol):
        match = self.one(path, r'^' + re.escape(symbol) + r'::?\n(?:(?!^\w+::?).*\n)*')
        start = self.texts[path].count('\n', 0, match.start()) + 1
        rows = [dict(line=start + offset, text=line) for offset, line in enumerate(match[0].splitlines()) if offset and line.strip() and not line.strip().startswith('@')]
        return dict(symbol=symbol, source=self.reference(path, symbol, match), lines=rows)

    def function(self, path, symbol):
        match = self.one(path, r'^(?:static )?[^\n;{}]+\b' + re.escape(symbol) + r'\([^;{}]*\)\n\{[\s\S]*?^\}')
        return dict(symbol=symbol, source=self.reference(path, symbol, match), text=match[0])

    def constant(self, path, symbol):
        match = self.one(path, r'^#define\s+' + re.escape(symbol) + r'\s+([^\n]+)')
        value = match[1].split('//')[0].strip()
        def evaluate(node):
            if isinstance(node, ast.Constant) and type(node.value) is int:
                return node.value
            if isinstance(node, ast.Name):
                constant_path = 'include/constants/opponents.h' if node.id == 'MAX_TRAINERS_COUNT' else path
                return self.constant(constant_path, node.id)['value']
            if isinstance(node, ast.BinOp) and isinstance(node.op, (ast.Add, ast.Sub)):
                left, right = evaluate(node.left), evaluate(node.right)
                return left + right if isinstance(node.op, ast.Add) else left - right
            raise ValueError('Unsupported Oak rating constant: ' + symbol)
        number = evaluate(ast.parse(value, mode='eval').body)
        return dict(value=number, source=self.reference(path, symbol, match))

    def text(self, path, symbol):
        block = self.block(path, symbol)
        # Preserve original Gen3 controls (\\p / \\l), without interpreting arbitrary escapes.
        pieces = []
        for row in block['lines']:
            match = re.fullmatch(r'\s*\.string "(.*)"', row['text'])
            if not match:
                raise ValueError('Unexpected text directive: ' + symbol)
            value = match[1]
            if re.search(r'\\(?![npl"\\])', value):
                raise ValueError('Unknown source text escape: ' + symbol)
            pieces.append(value.replace('\\n', '\n').replace('\\"', '"'))
        value = ''.join(pieces)
        if not value.endswith('$'):
            raise ValueError('Missing text terminator: ' + symbol)
        return dict(symbol=symbol, text=value, source=block['source'])


def build(source):
    rating_symbols = re.findall(r'^(PokedexRating_EventScript_\w+)::', source.texts[RATING_SCRIPT], re.M)
    if len(rating_symbols) != 11:
        raise ValueError('Rating script scope differs')
    blocks = [source.block(RATING_SCRIPT, symbol) for symbol in rating_symbols]
    callers = [source.block(PC_SCRIPT, s) for s in ['EventScript_PCMainMenu', 'EventScript_ChoosePCMenu', 'EventScript_AccessProfOaksPC', 'EventScript_ExitOaksPC', 'EventScript_TurnOffPC']]
    callers += [source.block(LAB_SCRIPT, 'PalletTown_ProfessorOaksLab_EventScript_' + s) for s in ['ProfOak', 'RatePokedexOrTryGiveBalls', 'RatePokedex', 'CheckIfPlayerNeedsBalls', 'PlayerOutOfBalls', 'GivePlayerMoreBalls', 'MonsAroundWorldWait', 'PlayerAlreadyGotBalls', 'TryStartNationalDexScene', 'DontStartNationalDexScene', 'DexCompleted']]
    callers += [source.block('data/event_scripts.s', 'EventScript_ReleaseEnd')]
    texts = [source.text('data/text/pokedex_rating.inc', s) for s in re.findall(r'^(\w+)::', source.texts['data/text/pokedex_rating.inc'], re.M)]
    texts += [source.text('data/event_scripts.s', s) for s in ['Text_AccessedProfOaksPC', 'Text_HavePokedexRated', 'Text_ClosedLinkToProfOaksPC']]
    for symbol in ['gText_ProfOakSPc']:
        match = source.one('src/strings.c', r'^const u8 ' + symbol + r'\[\] = _\((".*")\);')
        texts.append(dict(symbol=symbol, text=json.loads(match[1]), source=source.reference('src/strings.c', symbol, match)))
    native_pairs = {
        'src/prof_pc.c': ['GetPokedexCount', 'GetProfOaksRatingMessageByCount', 'GetProfOaksRatingMessage'],
        'src/pokedex.c': ['GetNationalPokedexCount', 'GetKantoPokedexCount', 'HasAllKantoMons', 'HasAllMons', 'GetSetPokedexFlag'],
        'src/pokedex_screen.c': ['DexScreen_GetSetPokedexFlag'],
        'src/event_data.c': ['IsNationalPokedexEnabled'],
        'src/script_menu.c': ['CreatePCMenu', 'CreatePCMenuWindow'],
        'src/scrcmd.c': ['ScrCmd_buffernumberstring', 'ScrCmd_special', 'ScrCmd_specialvar', 'ScrCmd_waitbuttonpress'],
        'src/help_system.c': ['BackupHelpContext', 'Script_SetHelpContext', 'RestoreHelpContext'],
        'src/quest_log.c': ['QuestLog_CutRecording'],
        'src/field_message_box.c': ['ShowFieldMessage'],
        'src/string_util.c': ['ConvertIntToDecimalStringN'],
        'src/field_specials.c': ['CountDigits']}
    native = [source.function(path, symbol) for path, symbols in native_pairs.items() for symbol in symbols]
    rating = next(n['text'] for n in native if n['symbol'] == 'GetProfOaksRatingMessageByCount')
    thresholds = [(int(a), b) for a, b in re.findall(r'if \(count < (\d+)\)\s+return (\w+);', rating)]
    if thresholds != [(n, 'PokedexRating_Text_LessThan' + str(n)) for n in range(10, 151, 10)]:
        raise ValueError('Rating thresholds differ')
    if 'GetSetPokedexFlag(SpeciesToNationalPokedexNum(SPECIES_MEW), 1)' not in rating:
        raise ValueError('Mew completion exception missing')
    bands = [dict(minimumCaught=n - 10, maximumCaught=n - 1, textSymbol=s) for n, s in thresholds]
    constants = {}
    for path, symbols in {
        'include/constants/flags.h': ['FLAG_SYS_POKEDEX_GET', 'FLAG_SYS_NATIONAL_DEX', 'FLAG_SYS_GAME_CLEAR', 'FLAG_OAKS_RATING_IS_VIA_PC', 'FLAG_OAK_SAW_DEX_COMPLETION'],
        'include/constants/vars.h': ['VAR_NATIONAL_DEX', 'VAR_0x8004', 'VAR_0x8005', 'VAR_0x8006', 'VAR_0x8008', 'VAR_0x8009', 'VAR_0x800A', 'VAR_RESULT', 'NPC_TEXT_COLOR_MALE'],
        'include/constants/help_system.h': ['HELPCONTEXT_PC', 'HELPCONTEXT_PC_MISC'],
        'include/constants/songs.h': ['MUS_DEX_RATING', 'MUS_EVOLVED', 'SE_PC_LOGIN'],
        'include/gba/defines.h': ['TRUE', 'FALSE']}.items():
        for symbol in symbols:
            constants[symbol] = source.constant(path, symbol)
    for symbol, value in [('FLAG_GET_SEEN', 0), ('FLAG_GET_CAUGHT', 1)]:
        match = source.one('include/pokedex.h', r'^    ' + symbol + ',$')
        constants[symbol] = dict(value=value, source=source.reference('include/pokedex.h', symbol, match))
    enum = re.search(r'enum \{(.*?)\};', source.texts['include/constants/pokedex.h'], re.S)[1]
    enum = re.sub(r'//[^\n]*', '', enum)
    dex = [s.strip() for s in enum.split(',') if s.strip()]
    if (dex.index('NATIONAL_DEX_MEW'), dex.index('NATIONAL_DEX_CELEBI'), dex.index('NATIONAL_DEX_DEOXYS')) != (151, 251, 386):
        raise ValueError('Dex numbering differs')
    special_names = re.findall(r'^\s*def_special (\w+)', source.texts['data/specials.inc'], re.M)
    required_specials = sorted(set(re.findall(r'^\s*special(?:var\s+\w+,)?\s+(\w+)', source.texts[RATING_SCRIPT], re.M)))
    special_bindings = []
    for symbol in required_specials:
        match = source.one('data/specials.inc', r'^\s*def_special ' + re.escape(symbol) + '$')
        special_bindings.append(dict(symbol=symbol, sourceId=special_names.index(symbol), source=source.reference('data/specials.inc', symbol, match)))
    opcodes = sorted(set(row['text'].strip().split()[0] for block in blocks for row in block['lines']))
    macros = []
    for symbol in opcodes:
        match = source.one('asm/macros/event.inc', r'^\s*\.macro ' + re.escape(symbol) + r'(?:\s[^\n]*)?\n[\s\S]*?^\s*\.endm')
        macros.append(dict(symbol=symbol, source=source.reference('asm/macros/event.inc', symbol, match), text=match[0]))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        inputFiles=list(source.entries.values()), constants=constants, ratingBands=bands, texts=texts,
        scriptBlocks=blocks, callerBlocks=callers, nativeDependencies=native, macroDependencies=macros,
        requiredOpcodes=opcodes, specialBindings=special_bindings,
        rules=dict(regionalCount=151, nationalCount=386, nationalMagic=185, nationalVariableValue=25176,
            nationalCompletionExcluded=[151, 249, 250, 251, 385, 386], normalFanfare='MUS_DEX_RATING',
            completionFanfare='MUS_EVOLVED', count150CompleteOnlyWithoutMew=True, pcRequiresPokedex=True,
            nationalStatsRequireThreeFieldGate=True, kantoRatingAlwaysFirst=True,
            countInputs='Validated persistent distinct national dex numbers, never current party/box counts',
            originalSeenValidation='seen == seen1 == seen2; caught also requires matching owned bit',
            observedCompletionFlag='FLAG_OAK_SAW_DEX_COMPLETION'),
        boundaries=[
            dict(target='PalletTown_ProfessorOaksLab_EventScript_CheckIfPlayerNeedsBalls', reason='Scene 6 with exactly one Kanto caught follows the original ball/Route22 branch, not rating; imported for routing review, no item effect implementation.'),
            dict(target='PalletTown_ProfessorOaksLab_EventScript_TryStartNationalDexScene', reason='Game-clear, 60-caught and One Island branches do not grant National mode in this content module.'),
            dict(target='PalletTown_ProfessorOaksLab_EventScript_DexCompleted', reason='Facing-dependent Oak movement celebration is preserved in raw callers and remains an application/presentation boundary.'),
            dict(target='EventScript_PCMainMenu', reason='Caller owns waitstate/menu callbacks, PC power, cancellation and restoration. This module returns no menu actions.'),
            dict(target='QuestLog_CutRecording', reason='Original cut-recording and questlog playback guards are preserved, not treated as generic no-ops.'),
            dict(target='GetProfOaksRatingMessage', reason='Dynamic ShowFieldMessage plus waitmessage, fanfare, waitfanfare and waitbuttonpress need correlated application/presentation handling.'),
            dict(target='buffernumberstring', reason='Original variable-to-decimal command is currently unsupported by PretStoryCompiler; importing the macro does not authorize execution.'),
            dict(target='HasAllMons', reason='National completion flag is an explicit requested effect in the result; this pure API never writes flags or unlocks National mode.')],
        limitations=['Authenticated content and pure projections only; no VM, application, Godot, audio or native acceptance.',
            'Whole pinned source files preserve unrelated branches for provenance, never implicitly authorize their execution.',
            'Raw and generated copyrighted source text remain local and gitignored; no new sprites, dex metadata or ROM data.'])


def import_catalog(raw, out, lock_path=None, repo=None):
    disjoint(raw, out, *([repo] if repo is not None else []))
    source = Source(raw, lock_path, repo)
    doc = build(source)
    data = canonical(doc)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,  inputCount=len(source.entries),
        outputs=[dict(path='oak-rating.json')])
    targets = [(safe(out, 'oak-rating.json'), data), (safe(out, 'manifest.json'), canonical(manifest))]
    if repo is not None:
        targets = [(safe(raw, path), data) for path, data in source.bytes.items()] + targets
    # All source parsing and every destination are checked before any prior file is replaced.
    preflight(targets)
    for target, _ in targets:
        temporary = target.with_name(target.name + '.tmp')
        if temporary.exists() or temporary.is_symlink():
            raise ValueError('Unexpected Oak rating temporary file: ' + str(temporary))
    write_all(targets)
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=pathlib.Path, help='Read immutable pinned local Git objects into raw')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/oak-rating')
    args = parser.parse_args()
    print(json.dumps(import_catalog(args.raw, args.out, repo=args.repo), ensure_ascii=False))
