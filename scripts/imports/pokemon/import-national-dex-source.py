#!/usr/bin/env python3
"""Import the pinned first National Dex node. No maps, ROM, upstream execution or runtime admission."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_constants import package_documents
import argparse, ast, collections,  importlib.util, json, re, sys
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PROFILE = 'firered-national-dex-source-v1'
PACKAGE = 'data/services/national-dex-source'
PALLET = 'PalletTown'
LAB = 'PalletTown_ProfessorOaksLab'
SCRIPT_PATHS = [f'data/maps/{n}/scripts.inc' for n in [PALLET, LAB]] + [
    'data/scripts/pokedex_rating.inc', 'data/scripts/movement.inc', 'data/scripts/obtain_item.inc', 'data/event_scripts.s']
TEXT_PATHS = [f'data/maps/{n}/text.inc' for n in [PALLET, LAB]] + ['data/text/pokedex_rating.inc']
ROOTS = ['PalletTown_EventScript_OakRatingScene'] + [LAB + '_EventScript_' + n for n in [
    'EnterForNationalDexScene', 'TryStartNationalDexScene', 'ReadyOakForNationalDexScene', 'SetNationalDexSceneFinished', 'MonsAroundWorldWait', 'RatePokedex', 'OakJustShownCompleteDex']]
EVIDENCE = ['PalletTown_MapScripts', 'PalletTown_OnFrame', LAB + '_MapScripts', LAB + '_OnTransition', LAB + '_OnFrame',
    LAB + '_EventScript_ProfOak', 'EventScript_SetReadyTryGiveNationalDexScene', 'EventScript_SetDefeatedEliteFourFlagsVars']
HEADERS = ['flags', 'vars', 'global', 'event_objects', 'event_object_movement', 'map_scripts', 'songs', 'help_system', 'opponents', 'pokedex']
def canonical(value): return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink in path: ' + str(path))
def reader():
    path = ROOT / 'scripts/imports/source/import-cinnabar-source-preparation.py'
    spec = importlib.util.spec_from_file_location('national_dex_reader', path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value); value.PROFILE = PROFILE
    return value

def index(src, paths):
    blocks = {}; aliases = {}
    for path in paths:
        current = None; depth = 0
        for line_no, line in enumerate(src.text(path).splitlines(), 1):
            code = line.split('@')[0].strip()
            if code.startswith('.macro'): depth += 1; current = None; continue
            if code == '.endm': depth -= 1; continue
            if depth: continue
            label = re.fullmatch(r'(\w+)::?', code)
            if label:
                current = label[1]; need(current not in blocks, 'Duplicate label: ' + current)
                blocks[current] = dict(label=current, sourceRef=src.ref(path, current, line_no), lines=[])
            elif current: blocks[current]['lines'].append(dict(line=line_no, text=line))
            alias = re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)', code)
            if alias: aliases[alias[1]] = alias[2]
    return blocks, aliases

def code(block):
    return [r['text'].split('@')[0].strip() for r in block['lines'] if r['text'].split('@')[0].strip()]

def script_inventory(src):
    blocks, aliases = index(src, SCRIPT_PATHS + TEXT_PATHS + ['data/scripts/hall_of_fame.inc'])
    selected = set(); pending = list(ROOTS); edges = []
    while pending:
        label = pending.pop()
        if label in selected: continue
        need(label in blocks, 'Unresolved closure label: ' + label); selected.add(label)
        for row in blocks[label]['lines']:
            line = row['text'].split('@')[0].strip()
            if not line or line.startswith('.'): continue
            parts = line.split(None, 1); op = parts[0]; args = parts[1] if len(parts) == 2 else ''
            targets = []
            if op in ['call', 'goto', 'goto_if_questlog', 'message']: targets = [args]
            elif op == 'applymovement' or op.startswith(('goto_if_', 'call_if_')): targets = [args.split(',')[-1].strip()]
            elif op == 'msgbox': targets = [args.split(',')[0].strip()]
            for target in targets:
                target = aliases.get(target, target)
                need(target in blocks, 'Unresolved closure target: ' + target)
                edges.append(dict(source=label, target=target, line=row['line']))
                pending.append(target)
    result = [blocks[n] for n in sorted(selected)]
    opcodes = collections.Counter(); movements = collections.Counter(); native = []
    for block in result:
        lines = code(block); movement = lines and lines[-1] == 'step_end'
        for row in block['lines']:
            line = row['text'].split('@')[0].strip()
            if not line or line.startswith('.'): continue
            op = line.split()[0]
            if movement: movements[op] += 1
            else: opcodes[op] += 1
            if op in ['special', 'specialvar']:
                args = [a.strip() for a in line.split(None, 1)[1].split(',')]
                native.append(dict(opcode=op, symbol=args[-1], arguments=args, sourceRef=src.ref(block['sourceRef']['path'], block['label'], row['line'])))
    scripts = dict(schemaVersion=1, executable=False, roots=ROOTS, blocks=result, references=edges, textPaths=TEXT_PATHS, aliases={k: v for k, v in aliases.items() if any((k in line for b in result for line in code(b)))}, opcodeCounts=dict(sorted(opcodes.items())), movementCounts=dict(sorted(movements.items())), nativeCalls=native, unresolvedLabelReferences=[], dispatchBlocks=[blocks[n] for n in EVIDENCE])
    return scripts, blocks

def constants_inventory(src, scripts):
    definitions = {}
    for path in [f'include/constants/{n}.h' for n in HEADERS] + ['include/gba/defines.h']:
        text = src.text(path)
        for match in re.finditer(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)', text, re.M):
            definitions[match[1]] = (match[2].split('//')[0].strip(), path, text.count('\n', 0, match.start()) + 1)
    text = src.text('asm/macros/event.inc')
    for match in re.finditer(r'^\s*([A-Z][A-Z0-9_]+)\s*=\s*([0-9]+)\s*$', text, re.M):
        definitions[match[1]] = (match[2], 'asm/macros/event.inc', text.count('\n', 0, match.start()) + 1)
    for path, first in [('include/constants/pokedex.h', 'NATIONAL_DEX_NONE'), ('include/pokedex.h', 'FLAG_GET_SEEN')]:
        text = src.text(path)
        enums = re.finditer(r'enum\s*\{(.*?)\};', text, re.S)
        body = next(m for m in enums if first in m[1])
        clean = re.sub(r'//[^\n]*', '', body[1])
        names = [n.strip() for n in clean.split(',') if n.strip()]
        need(names[0] == first and all(re.fullmatch(r'[A-Z][A-Z0-9_]+', n) for n in names), 'Pokedex enum differs')
        for value, symbol in enumerate(names):
            start = text.index(symbol, body.start())
            definitions[symbol] = (str(value), path, text.count('\n', 0, start) + 1)
    values = {}; pending = set()
    def resolve(symbol):
        if symbol in values: return values[symbol]['value']
        need(symbol in definitions and symbol not in pending, 'Unresolved/cyclic constant: ' + symbol)
        pending.add(symbol); expression, path, line = definitions[symbol]
        def number(node):
            if isinstance(node, ast.Constant) and type(node.value) is int: return node.value
            if isinstance(node, ast.Name): return resolve(node.id)
            if isinstance(node, ast.BinOp):
                a, b = number(node.left), number(node.right)
                if isinstance(node.op, ast.Add): return a + b
                if isinstance(node.op, ast.Sub): return a - b
                if isinstance(node.op, ast.BitOr): return a | b
                if isinstance(node.op, ast.LShift) and 0 <= b < 32: return a << b
            raise ValueError('Unsupported source constant: ' + symbol + ': ' + expression)
        value = number(ast.parse(expression, mode='eval').body)
        need(0 <= value <= 0x7fffffff, 'Constant out of range')
        values[symbol] = dict(value=value, sourceRef=src.ref(path, symbol, line)); pending.remove(symbol)
        return value
    generator = src.text('tools/mapjson/mapjson.cpp')
    need('json_to_string(obj_event, "local_id") << " " << i + 1' in generator, 'Map local ID generator differs')
    for name in [PALLET, LAB]:
        path = f'data/maps/{name}/map.json'
        for i, event in enumerate(src.json(path)['object_events'], 1):
            if 'local_id' in event:
                values[event['local_id']] = dict(value=i, sourceRef=src.ref(path, event['local_id']), generatorSource=src.ref('tools/mapjson/mapjson.cpp', 'object_events ordinal plus one', 416))
    material = '\n'.join(line for block in scripts['blocks'] for line in code(block) if not line.startswith('.string'))
    operands = {scripts['aliases'].get(s, s) for s in re.findall(r'\b[A-Z][A-Z0-9_x]+\b', material)}
    operands -= {'0', '1'}
    operands |= {'TRUE', 'FALSE', 'FLAG_SYS_GAME_CLEAR', 'FLAG_SYS_NATIONAL_DEX', 'FLAG_SYS_CAN_LINK_WITH_RS', 'VAR_NATIONAL_DEX', 'VAR_MAP_SCENE_PALLET_TOWN_OAK', 'KANTO_DEX_COUNT', 'JOHTO_DEX_COUNT', 'NATIONAL_DEX_COUNT', 'FLAG_GET_SEEN', 'FLAG_GET_CAUGHT', 'NATIONAL_DEX_MEW', 'NATIONAL_DEX_LUGIA', 'NATIONAL_DEX_HO_OH', 'NATIONAL_DEX_CELEBI', 'NATIONAL_DEX_JIRACHI', 'NATIONAL_DEX_DEOXYS'}
    for symbol in sorted(operands & definitions.keys()): resolve(symbol)
    movement_map = {}
    for match in re.finditer(r'^[ \t]*create_movement_action\s+(\w+),\s+(\w+)', src.text('asm/macros/movement.inc'), re.M):
        if match[1] in scripts['movementCounts']:
            movement_map[match[1]] = dict(symbol=match[2], value=resolve(match[2]), sourceRef=src.ref('asm/macros/movement.inc', match[1], src.text('asm/macros/movement.inc').count('\n', 0, match.start()) + 1))
    need(set(movement_map) == set(scripts['movementCounts']), 'Movement macro binding missing')
    symbolic = sorted(operands - values.keys())
    need(set(symbolic) <= {'STR_VAR_1', 'STR_VAR_2', 'MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB'}, 'Unresolved operand: ' + repr(symbolic))
    return dict(schemaVersion=1, values=values, movementBindings=movement_map, nonNumericOperandTokens=symbolic)

def c_function(src, path, symbol):
    text = src.text(path)
    match = re.search(r'^[^\n;{}]*\b' + re.escape(symbol) + r'\([^;{}]*\)\n\{\n.*?^\}', text, re.M | re.S)
    need(match is not None, 'Missing native function: ' + symbol)
    line = text.count('\n', 0, match.start()) + 1
    return dict(symbol=symbol, sourceRef=src.ref(path, symbol, line), endLine=line + match[0].count('\n'), text=match[0], runtimeAdmitted=False)

def contracts(src, scripts, blocks, constants):
    def expect(label, *commands):
        actual = code(blocks[label]); need(all(c in actual for c in commands), 'Source branch differs: ' + label)
    expect('PalletTown_OnFrame', 'map_script_2 VAR_MAP_SCENE_PALLET_TOWN_OAK, 2, PalletTown_EventScript_OakRatingScene')
    expect('PalletTown_EventScript_OakRatingScene', 'goto_if_unset FLAG_WORLD_MAP_ONE_ISLAND, PalletTown_EventScript_NotBeenToOneIslandYet',
        'setvar VAR_0x8004, 0', 'goto_if_lt VAR_0x8009, 60, PalletTown_EventScript_NotEnoughMonsForNationalDex',
        'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 7', 'setvar VAR_MAP_SCENE_PALLET_TOWN_OAK, 3', 'warp MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB, 6, 12')
    expect('PalletTown_EventScript_EndOakRatingScene', 'setvar VAR_MAP_SCENE_PALLET_TOWN_OAK, 3')
    expect(LAB + '_EventScript_TryStartNationalDexScene', 'call PokedexRating_EventScript_RateInPerson',
        'goto_if_lt VAR_0x8009, 60, ' + LAB + '_EventScript_DontStartNationalDexScene',
        'goto_if_unset FLAG_WORLD_MAP_ONE_ISLAND, ' + LAB + '_EventScript_DontStartNationalDexScene')
    expect(LAB + '_EventScript_NationalDexScene', 'special EnableNationalPokedex', 'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 8')
    expect(LAB + '_EventScript_SetNationalDexSceneFinished', 'setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 9')
    mechanisms = [c_function(src, 'src/event_data.c', n) for n in ['EnableNationalPokedex', 'IsNationalPokedexEnabled', 'DisableNationalPokedex']]
    mechanisms += [c_function(src, 'src/prof_pc.c', n) for n in ['GetPokedexCount', 'GetProfOaksRatingMessage']]
    mechanisms += [c_function(src, 'src/pokedex.c', n) for n in ['GetKantoPokedexCount', 'GetNationalPokedexCount', 'HasAllMons']]
    grant = mechanisms[0]['text']; check = mechanisms[1]['text']
    need(all(s in grant for s in ['nationalMagic = 0xB9', '*nationalDexVar = 0x6258', 'FlagSet(FLAG_SYS_NATIONAL_DEX)']), 'National grant fields differ')
    need(all(s in check for s in ['nationalMagic == 0xB9', 'VarGet(VAR_NATIONAL_DEX) == 0x6258', 'FlagGet(FLAG_SYS_NATIONAL_DEX)']), 'National access conjunction differs')
    bindings = []
    names = {n['symbol'] for n in scripts['nativeCalls']} | {'IsNationalPokedexEnabled'}
    special_index = 0
    for line_no, line in enumerate(src.text('data/specials.inc').splitlines(), 1):
        match = re.match(r'\s*def_special\s+(\w+)', line)
        if not match: continue
        if match[1] in names: bindings.append(dict(symbol=match[1], id=special_index, sourceRef=src.ref('data/specials.inc', match[1], line_no)))
        special_index += 1
    need({b['symbol'] for b in bindings} == names, 'Special dispatch binding missing')
    relational = {'call_if_eq', 'goto_if_eq', 'goto_if_lt'}
    compiler_effects = [dict(opcode=op, count=count, support='base-comparison' if op in relational else 'base-opcode', nativeHandlerRequired=op in ['special', 'specialvar', 'buffernumberstring']) for op, count in scripts['opcodeCounts'].items()]
    return dict(schemaVersion=1,  completeGame=False, mapCount=0, scope='First postgame National Dex node on the existing Pallet Town and Oak lab maps.', entry=dict(gameClearFlag='FLAG_SYS_GAME_CLEAR', outdoorSceneVariable='VAR_MAP_SCENE_PALLET_TOWN_OAK', outdoorReadyScene=2, outdoorResolvedScene=3, oneIslandVisitFlag='FLAG_WORLD_MAP_ONE_ISLAND', selectorVariable='VAR_0x8004', kantoSelector=0, minimumCaught=60, seenResult='VAR_0x8005', caughtResult='VAR_0x8006', bufferedCaught='VAR_0x8009', distinctKantoSpecies=True, failureReentry='Interact with Oak in the lab; outdoor scene does not repeat until source Hall-of-Fame reset readies it again.'), grant=dict(native='EnableNationalPokedex', nationalMagic=185, variable='VAR_NATIONAL_DEX', variableValue=25176, flag='FLAG_SYS_NATIONAL_DEX', accessRule='All three fields must match.', labSceneVariable='VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB', labArrivalScene=7, labGrantedScene=8, labNextTransitionScene=9, mutatedByGrant=['pokedex.nationalMagic', 'VAR_NATIONAL_DEX', 'FLAG_SYS_NATIONAL_DEX']), countRules=dict(kantoCount=constants['values']['KANTO_DEX_COUNT']['value'], nationalCount=constants['values']['NATIONAL_DEX_COUNT']['value'], nationalCountingRange=[1, 386], nationalCountingExcluded=[], nationalCompletionRanges=[[1, 150], [152, 248], [252, 384]], nationalCompletionExcluded=[151, 249, 250, 251, 385, 386], nationalCompletionRequiredCount=380, nationalCompletionExcludedSpecies=['MEW', 'LUGIA', 'HO_OH', 'CELEBI', 'JIRACHI', 'DEOXYS'], selectorZero='Kanto seen/caught counts', selectorNonzero='National seen/caught counts; the source script uses 1', returnValue='IsNationalPokedexEnabled, independent of count selector'), actors={name: src.json(f'data/maps/{name}/map.json')['object_events'] for name in [PALLET, LAB]}, directionCases=[dict(direction=d, value=constants['values'][d]['value'], rivalStart=[5 if d == 'DIR_NORTH' else 6, 10], entryScript=LAB + '_EventScript_NationalDexSceneRivalEnter' + ('North' if d == 'DIR_NORTH' else 'South' if d == 'DIR_SOUTH' else 'EastWest')) for d in ['DIR_NORTH', 'DIR_SOUTH', 'DIR_EAST', 'DIR_WEST']], nativeDependencies=mechanisms, specialBindings=bindings, compilerEffects=compiler_effects, prerequisites=['Completed source Hall of Fame flow', 'Visited One Island', 'At least 60 distinct Kanto species caught'], excluded=['Ruby retrieval', 'Sapphire retrieval', 'Network Machine repair', 'FLAG_SYS_CAN_LINK_WITH_RS', 'Later Sevii maps and ferries', 'Rematch League'])

def generate(reference, generated_directory, check=False):
    helper = reader(); src = helper.Source(reference)
    scripts, blocks = script_inventory(src)
    constants = constants_inventory(src, scripts)
    contract = contracts(src, scripts, blocks, constants)
    contract.update(sourceCommit=helper.COMMIT, sourceTree=helper.TREE)
    documents = package_documents(generated_directory, constants, {'scripts.json': scripts, 'contracts.json': contract}, check=check)
    docs = {name: canonical(value) for name, value in documents.items()}
    manifest = dict(schemaVersion=1, inputCount=len(src.entries), mapCount=0, scriptBlockCount=len(scripts['blocks']), outputCount=len(docs), outputs=[dict(path=p) for p, b in sorted(docs.items())])
    manifest_data = canonical(manifest); docs['manifest.json'] = manifest_data
    writes = {}
    writes.update({'content/raw/pokefirered/037335f4/' + p: b for p, b in src.data.items()})
    writes.update({'content/generated/' + PACKAGE + '/' + p: b for p, b in docs.items()})
    return writes, manifest, dict()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    target = args.project_root.absolute(); reference = args.reference_root.absolute()
    need(not (target == reference or target.is_relative_to(reference) or reference.is_relative_to(target)), 'Reference/output overlap')
    safe(target)
    writes, manifest, identity = generate(reference, target / 'content/generated', args.check)
    for relative, data in sorted(writes.items()):
        path = target / relative; safe(path)
        if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + relative)
        else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
    print(json.dumps({k: v for k, v in manifest.items() if k != 'outputs'} | identity | dict(fileCount=len(writes), totalBytes=sum(map(len, writes.values()))), indent=2))
if __name__ == '__main__': main()
