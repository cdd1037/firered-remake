#!/usr/bin/env python3
"""Focused offline source semantics and optional regeneration checks; never builds the product."""
import argparse, ast, json, re, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'national-dex-source'
def need(value, message):
    if not value: raise AssertionError(message)
def load(path): return json.loads(path.read_bytes())
def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    args = parser.parse_args(); root = args.project_root.absolute()
    generated = root / 'content/generated' / PACKAGE
    lock_path = root / 'content/sources' / (PACKAGE + '.lock.json')
    lock = load(lock_path); manifest = load(generated / 'manifest.json')
    scripts = load(generated / 'scripts.json'); constants = load(generated.parent / 'shared-constants/values.json'); contract = load(generated / 'contracts.json')
    raw = {}
    for entry in lock['files']:
        p = entry['path']; raw[p] = (root / 'content/raw/pokefirered/037335f4' / p).read_text()
    for entry in manifest['outputs']:
        load(generated / entry['path'])
    need(contract['inputFiles'] == lock['files'] and manifest['mapCount'] == contract['mapCount'] == 0 and not contract['runtimeIntegrated'], 'Package admits maps/runtime')
    need(len(raw) == manifest['inputCount'] == 31, 'Unexpected input count')
    blocks = {b['label']: b for b in scripts['blocks']}
    def commands(label): return [r['text'].split('@')[0].strip() for r in blocks[label]['lines'] if r['text'].split('@')[0].strip()]
    for b in scripts['blocks'] + scripts['dispatchBlocks']:
        lines = raw[b['sourceRef']['path']].splitlines()
        need(lines[b['sourceRef']['line'] - 1].split('@')[0].strip().rstrip(':') == b['label'], 'Label provenance differs')
        for row in b['lines']: need(lines[row['line'] - 1] == row['text'], 'Instruction provenance differs')
    need(not scripts['unresolvedLabelReferences'] and all(e['source'] in blocks and e['target'] in blocks for e in scripts['references']), 'Closure is unresolved')
    # Evaluate source branch predicates, retaining their original order rather than reimplementing a product eligibility method.
    def gate(label, caught, visited):
        for line in commands(label):
            if line.startswith('goto_if_lt VAR_0x8009, '):
                _, value, target = [s.strip() for s in line.split(',')]
                if caught < int(value): return target
            if line.startswith('goto_if_unset FLAG_WORLD_MAP_ONE_ISLAND, ') and not visited: return line.split(',')[-1].strip()
        return 'eligible'
    lab = 'PalletTown_ProfessorOaksLab'
    for caught in [0, 1, 59, 60, 61, 151]:
        for visited in [False, True]:
            outside = gate('PalletTown_EventScript_OakRatingScene', caught, visited)
            inside = gate(lab + '_EventScript_TryStartNationalDexScene', caught, visited)
            need((outside == 'eligible') == (inside == 'eligible') == (visited and caught >= 60), 'Source eligibility boundary differs')
            if not visited: need(outside.endswith('NotBeenToOneIslandYet'), 'Outdoor prerequisite precedence differs')
    need('setvar VAR_MAP_SCENE_PALLET_TOWN_OAK, 3' in commands('PalletTown_EventScript_EndOakRatingScene'), 'Under60 repeats outdoor scene')
    grant = commands(lab + '_EventScript_NationalDexScene')
    enable = grant.index('special EnableNationalPokedex')
    need(sum('EnableNationalPokedex' in line for line in grant) == 1 and enable < grant.index('setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 8'), 'Grant ordering differs')
    need('setvar VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB, 9' in commands(lab + '_EventScript_SetNationalDexSceneFinished'), 'Reentry scene differs')
    dispatch = next(b for b in scripts['dispatchBlocks'] if b['label'] == lab + '_EventScript_ProfOak')
    text = '\n'.join(r['text'] for r in dispatch['lines'])
    need(text.index('LAB, 9,') < text.index('LAB, 8,') < text.index('FLAG_SYS_GAME_CLEAR'), 'Already-granted dispatch order differs')
    need(not any(token in '\n'.join(commands(lab + '_EventScript_NationalDexScene')) for token in ['FLAG_SYS_CAN_LINK_WITH_RS', 'ITEM_RUBY', 'ITEM_SAPPHIRE']), 'First node admits later Sevii')
    values = constants
    need([values[k] for k in ['KANTO_DEX_COUNT', 'JOHTO_DEX_COUNT', 'NATIONAL_DEX_COUNT']] == [151, 251, 386], 'Dex enum dimensions differ')
    natives = {n['symbol']: n for n in contract['nativeDependencies']}
    def number(expression):
        def visit(n):
            if isinstance(n, ast.Constant): return n.value
            if isinstance(n, ast.Name): return values[n.id]
            if isinstance(n, ast.BinOp) and isinstance(n.op, ast.Sub): return visit(n.left) - visit(n.right)
            raise AssertionError('Unexpected source loop expression')
        return visit(ast.parse(expression, mode='eval').body)
    loops = re.findall(r'for \(i = ([^;]+); i < ([^;]+); i\+\+\)', natives['HasAllMons']['text'])
    required = {i + 1 for start, end in loops for i in range(number(start), number(end))}
    need(len(loops) == 3 and len(required) == 380 and sorted(set(range(1, 387)) - required) == [151, 249, 250, 251, 385, 386], 'HasAllMons ranges differ')
    need(contract['countRules']['nationalCompletionExcluded'] == sorted(set(range(1, 387)) - required), 'Completion projection differs')
    for name, count in [('GetKantoPokedexCount', 151), ('GetNationalPokedexCount', 386)]:
        loop = re.findall(r'for \(i = ([^;]+); i < ([^;]+); i\+\+\)', natives[name]['text'])
        need(len(loop) == 1 and [number(e) for e in loop[0]] == [0, count] and 'GetSetPokedexFlag(i + 1, FLAG_GET_CAUGHT)' in natives[name]['text'], 'Count range differs')
    need(values['FLAG_GET_SEEN'] == 0 and values['FLAG_GET_CAUGHT'] == 1, 'Count operation IDs differ')
    for case in contract['directionCases']:
        lines = commands(case['entryScript']); x, y = case['rivalStart']
        need(f'setobjectxyperm LOCALID_OAKS_LAB_RIVAL, {x}, {y}' in lines, 'Rival placement differs')
        steps = commands(lab + '_Movement_RivalEnter')
        for move in steps:
            if move == 'walk_up': y -= 1
            elif move != 'step_end': raise AssertionError('Unexpected rival entry movement')
        need((x, y) == (5 if case['direction'] == 'DIR_NORTH' else 6, 4), 'Rival approach endpoint differs')
    movement_values = load(generated / 'movement-bindings.json')
    need(movement_values == dict(delay_4=26, delay_8=27, delay_16=28, set_invisible=96, step_end=254, walk_down=16, walk_up=17, walk_left=18, walk_right=19,
        walk_in_place_faster_down=45, walk_in_place_faster_up=46, walk_in_place_faster_left=47, walk_in_place_faster_right=48,
        face_player=74, walk_in_place_down=37, walk_in_place_up=38, walk_in_place_left=39, walk_in_place_right=40,
        walk_in_place_fast_down=41, walk_in_place_fast_up=42, walk_in_place_fast_left=43, walk_in_place_fast_right=44), 'Movement action bindings differ')
    need(set(scripts['movementCounts']) == set(movement_values), 'Movement binding incomplete')
    if args.reference_root:
        command = [sys.executable, str(ROOT / 'scripts/import-national-dex-source.py'), '--reference-root', str(args.reference_root)]
        checked = subprocess.run(command + ['--project-root', str(root), '--check'], capture_output=True, text=True)
        need(checked.returncode == 0, checked.stderr)
        with tempfile.TemporaryDirectory(prefix='national-dex-source-symlink-') as directory:
            target = Path(directory); (target / 'redirect').mkdir(); (target / 'content').symlink_to(target / 'redirect', target_is_directory=True)
            rejected = subprocess.run(command + ['--project-root', str(target)], capture_output=True, text=True)
            need(rejected.returncode != 0 and 'Symlink in path' in rejected.stderr, 'Symlink output accepted')
    print(json.dumps(dict(status='passed', sourceBlobs=len(raw), closureBlocks=len(blocks), gateBoundaryCases=12, directions=4,
        movementBindings=len(movement_values), nationalCompletionRequired=len(required), sourceRegenerationCheck=bool(args.reference_root), productBuildRun=False)))
if __name__ == '__main__': main()
