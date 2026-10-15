#!/usr/bin/env python3
"""Generate focused FireRed Ghost vectors by executing verbatim source functions.

No network, ROM, or full original engine is used. Every reference byte comes from
`git show PIN:path` and must match the SHA-256 committed in source-lock.json.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import hashlib
import itertools
import json
from pathlib import Path
import re
import subprocess

PIN = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
HERE = Path(__file__).resolve().parent
MODES = [('ordinary', 0), ('disguisedGhost', 1 << 15), ('revealedGhost', (1 << 15) | (1 << 13))]

def sha(data):
    return hashlib.sha256(data).hexdigest()

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])

def extract_function(text, signature):
    start = text.index(signature + '\n{')
    opening = text.index('{', start)
    depth = 1
    index = opening + 1
    while depth:
        depth += (text[index] == '{') - (text[index] == '}')
        index += 1
    return text[start:index] + '\n', text.count('\n', 0, start) + 1

def macro(text, name):
    match = re.search(r'^#define\s+' + re.escape(name) + r'(?:\([^\n]*?\))?\s+[^\n]+$', text, re.M)
    if not match:
        raise ValueError('Missing macro ' + name)
    return match.group(0)

def number(text, name):
    return int(re.search(r'^#define\s+' + re.escape(name) + r'\s+(\d+)\b', text, re.M).group(1))

def source_assertions(sources):
    util = sources['src/battle_util.c']
    enum_start = util.index('    CANCELLER_FLAGS,')
    enum_end = util.index('\n};', enum_start)
    order = re.findall(r'CANCELLER_\w+', util[enum_start:enum_end])
    expected = ['FLAGS', 'ASLEEP', 'FROZEN', 'TRUANT', 'RECHARGE', 'FLINCH', 'DISABLED', 'TAUNTED', 'IMPRISONED', 'CONFUSED', 'PARALYSED', 'GHOST', 'IN_LOVE', 'BIDE', 'THAW', 'END']
    assert order == ['CANCELLER_' + name for name in expected]
    ghost_start = util.index('        case CANCELLER_GHOST:')
    ghost_end = util.index('        case CANCELLER_IN_LOVE:', ghost_start)
    branch = util[ghost_start:ghost_end]
    for required in ['IS_BATTLE_TYPE_GHOST_WITHOUT_SCOPE(gBattleTypeFlags)', 'GetBattlerSide(gBattlerAttacker) == B_SIDE_PLAYER', 'BattleScript_TooScaredToMove', 'BattleScript_GhostGetOutGetOut', 'effect = 1;', 'gBattleStruct->atkCancellerTracker++;']:
        assert required in branch
    ball, ball_line = extract_function(sources['src/battle_script_commands.c'], 'static void Cmd_handleballthrow(void)')
    assert ball.index('if (gBattleTypeFlags & BATTLE_TYPE_GHOST)') < ball.index('else if (gBattleTypeFlags & BATTLE_TYPE_TRAINER)') < ball.index('u32 odds;') < ball.index('Random()')
    ghost_ball = ball[ball.index('if (gBattleTypeFlags & BATTLE_TYPE_GHOST)'):ball.index('else if (gBattleTypeFlags & BATTLE_TYPE_TRAINER)')]
    assert 'BALL_GHOST_DODGE' in ghost_ball and 'BattleScript_GhostBallDodge' in ghost_ball and 'Random(' not in ghost_ball
    scripts = {}
    for path, labels in [('data/battle_scripts_1.s', ['BattleScript_TooScaredToMove', 'BattleScript_GhostGetOutGetOut', 'BattleScript_SilphScopeUnveiled']), ('data/battle_scripts_2.s', ['BattleScript_GhostBallDodge'])]:
        for label in labels:
            pattern = r'^' + label + r'::\n(.*?)(?=^\w+::|\Z)'
            body = re.search(pattern, sources[path], re.M | re.S).group(0).rstrip() + '\n'
            scripts[label] = {'path': path, 'line': sources[path][:sources[path].index(label + '::')].count('\n') + 1, 'text': body, 'sha256': sha(body.encode())}
    script_expectations = {
        'BattleScript_TooScaredToMove': ['STRINGID_MONTOOSCAREDTOMOVE', 'B_ANIM_MON_SCARED', 'goto BattleScript_MoveEnd'],
        'BattleScript_GhostGetOutGetOut': ['STRINGID_GHOSTGETOUTGETOUT', 'B_ANIM_GHOST_GET_OUT', 'goto BattleScript_MoveEnd'],
        'BattleScript_SilphScopeUnveiled': ['STRINGID_SILPHSCOPEUNVEILED', 'B_ANIM_SILPH_SCOPED', 'STRINGID_GHOSTWASMAROWAK', 'end2'],
        'BattleScript_GhostBallDodge': ['STRINGID_ITDODGEDBALL', 'finishaction'],
    }
    for label, required in script_expectations.items():
        for token in required:
            assert token in scripts[label]['text']
    strings = {}
    for name in ['sText_TooScaredToMove', 'sText_GetOutGetOut', 'sText_SilphScopeUnveil', 'sText_TheGhostWas', 'sText_ItDodgedBall']:
        strings[name] = re.search(r'^static const u8 ' + name + r'\[\] = _\("(.*?)"\);', sources['src/battle_message.c'], re.M).group(1)
    return {
        'executionScope': 'Static authenticated source assertions only; canceller, capture command, scripts, text and complete engine are NOT executed by this oracle.',
        'cancellerOrder': order,
        'ghostCanceller': {'path': 'src/battle_util.c', 'line': util[:ghost_start].count('\n') + 1, 'text': branch, 'sha256': sha(branch.encode())},
        'ballGhostBranchBeforeCapture': {'path': 'src/battle_script_commands.c', 'functionLine': ball_line, 'text': ghost_ball, 'sha256': sha(ghost_ball.encode()), 'checksRawGhostBitEvenWhenUnveiled': True, 'branchContainsNoRandomCall': True},
        'scripts': scripts, 'textLiterals': strings,
    }

STUBS = r'''
/* Minimal data/accessor stubs. They are NOT extracted engine implementations. */
struct BattlePokemon { u16 item, speed; u8 ability; } gBattleMons[4];
struct { u8 holdEffect; } gEnigmaBerries[4];
struct { u32 fleeType:2; } gProtectStructs[4];
struct BattleState { u8 runTries; } battleState, *gBattleStruct = &battleState;
struct Pokemon { u16 species, heldItem; u8 friendship, ball, metLocation; } gEnemyParty[6];
struct SpeciesInfo { u16 itemCommon, itemRare; };
struct { u8 inBattle; } gMain;
struct SaveBlock { struct { u8 holdEffect; } enigmaBerry; } saveBlock, *gSaveBlock1Ptr = &saveBlock;
struct { u8 trainerClass; } gTrainers[1];
u16 gTrainerBattleOpponent_A;
u8 currentRegion;
u8 GetCurrentRegionMapSectionId(void) { return currentRegion; }
u32 gBattleTypeFlags, gRngValue;
u8 gPotentialItemEffectBattler, gLastUsedAbility, gCurrentTurnActionNumber, gBattlersCount, gBattleOutcome;
u16 gLastUsedItem;
unsigned setMonDataCalls;
u16 rawDraws[16];
unsigned drawCount;
u8 GetBattlerSide(u8 battler) { return battler & 1; }
u8 ItemId_GetHoldEffect(u16 item) { return item == ITEM_SMOKE_BALL ? HOLD_EFFECT_CAN_ALWAYS_RUN : item == ITEM_SOOTHE_BELL ? HOLD_EFFECT_FRIENDSHIP_UP : 0; }
u32 GetMonData(struct Pokemon *mon, int field, void *unused) {
    (void)unused;
    switch (field) {
        case MON_DATA_SPECIES: case MON_DATA_SPECIES_OR_EGG: return mon->species;
        case MON_DATA_HELD_ITEM: return mon->heldItem;
        case MON_DATA_FRIENDSHIP: return mon->friendship;
        case MON_DATA_POKEBALL: return mon->ball;
        case MON_DATA_MET_LOCATION: return mon->metLocation;
        default: assert(0); return 0;
    }
}
void SetMonData(struct Pokemon *mon, int field, const void *value) {
    if (field == MON_DATA_HELD_ITEM) mon->heldItem = *(const u16 *)value;
    /* AdjustFriendship passes an s16; explicit conversion preserves GBA little-endian byte semantics on any host. */
    else if (field == MON_DATA_FRIENDSHIP) mon->friendship = (u8)*(const s16 *)value;
    else assert(0);
    ++setMonDataCalls;
}
'''

MAIN = r'''
static void reset(unsigned seed, unsigned flags) {
    memset(gBattleMons, 0, sizeof gBattleMons);
    memset(gProtectStructs, 0, sizeof gProtectStructs);
    memset(gEnigmaBerries, 0, sizeof gEnigmaBerries);
    memset(gEnemyParty, 0, sizeof gEnemyParty);
    battleState.runTries = 0;
    gRngValue = seed; gBattleTypeFlags = flags; drawCount = 0; setMonDataCalls = 0;
    gPotentialItemEffectBattler = 255; gLastUsedAbility = 0; gLastUsedItem = 0;
    gCurrentTurnActionNumber = 0; gBattlersCount = 2; gBattleOutcome = 0;
}
static void print_tail(void) {
    printf(",\"rawDraws\":[");
    for (unsigned i=0; i<drawCount; ++i) printf("%s%u", i ? "," : "", rawDraws[i]);
    printf("],\"finalSeed\":%u}\n", gRngValue);
}
int main(void) {
    char kind;
    unsigned flags, seed, speed, opposingSpeed, attempts, ability, item, enigma, battler, species, initialHeld, count, initial, sootheBell, sameRegion;
    while (scanf(" %c", &kind) == 1) {
        if (kind == 'R') {
            if (scanf("%u %u %u %u %u %u %u %u %u", &flags, &seed, &speed, &opposingSpeed, &attempts, &ability, &item, &enigma, &battler) != 9) return 2;
            reset(seed, flags);
            gBattleMons[battler].speed = speed;
            gBattleMons[BATTLE_OPPOSITE(battler)].speed = opposingSpeed;
            gBattleMons[battler].ability = ability; gBattleMons[battler].item = item;
            gEnigmaBerries[battler].holdEffect = enigma; battleState.runTries = attempts;
            unsigned result = TryRunFromBattle(battler);
            printf("{\"result\":%u,\"attemptsAfter\":%u,\"fleeType\":%u,\"battleOutcome\":%u,\"currentTurnActionNumber\":%u,\"potentialItemEffectBattler\":%u,\"lastUsedAbility\":%u,\"lastUsedItem\":%u", result, battleState.runTries, gProtectStructs[battler].fleeType, gBattleOutcome, gCurrentTurnActionNumber, gPotentialItemEffectBattler, gLastUsedAbility, gLastUsedItem);
            print_tail();
        } else if (kind == 'H') {
            if (scanf("%u %u %u %u", &flags, &seed, &species, &initialHeld) != 4) return 2;
            reset(seed, flags); gEnemyParty[0].species = species; gEnemyParty[0].heldItem = initialHeld;
            SetWildMonHeldItem();
            printf("{\"heldItem\":%u,\"setMonDataCalls\":%u", gEnemyParty[0].heldItem, setMonDataCalls);
            print_tail();
        } else if (kind == 'G') {
            if (scanf("%u %u", &seed, &count) != 2 || count > 16) return 2;
            reset(seed, 0);
            for (unsigned i=0; i<count; ++i) (void)Random();
            printf("{\"drawCount\":%u", drawCount);
            print_tail();
        } else if (kind == 'F') {
            if (scanf("%u %u %u", &initial, &sootheBell, &sameRegion) != 3) return 2;
            reset(0, 0); gEnemyParty[0].species = SPECIES_CUBONE;
            gEnemyParty[0].friendship = initial;
            gEnemyParty[0].heldItem = sootheBell ? ITEM_SOOTHE_BELL : ITEM_NONE;
            gEnemyParty[0].ball = ITEM_POKE_BALL;
            currentRegion = 1; gEnemyParty[0].metLocation = sameRegion ? 1 : 2;
            AdjustFriendship(&gEnemyParty[0], FRIENDSHIP_EVENT_GROW_LEVEL);
            printf("{\"final\":%u,\"setMonDataCalls\":%u", gEnemyParty[0].friendship, setMonDataCalls);
            print_tail();
        } else return 2;
    }
    return 0;
}
'''

def build_source(sources):
    selections = {
        'include/constants/battle.h': ['BATTLE_TYPE_DOUBLE', 'BATTLE_TYPE_TRAINER', 'BATTLE_TYPE_LEGENDARY', 'BATTLE_TYPE_GHOST_UNVEILED', 'BATTLE_TYPE_GHOST', 'BATTLE_TYPE_POKEDUDE', 'IS_BATTLE_TYPE_GHOST_WITHOUT_SCOPE', 'IS_BATTLE_TYPE_GHOST_WITH_SCOPE', 'BATTLE_OPPOSITE', 'B_SIDE_PLAYER', 'B_OUTCOME_RAN', 'FLEE_ITEM', 'FLEE_ABILITY'],
        'include/constants/items.h': ['ITEM_NONE', 'ITEM_ENIGMA_BERRY', 'ITEM_SMOKE_BALL', 'ITEM_SPELL_TAG', 'ITEM_THICK_CLUB', 'ITEM_SOOTHE_BELL', 'ITEM_LUXURY_BALL', 'ITEM_POKE_BALL'],
        'include/constants/abilities.h': ['ABILITY_RUN_AWAY'],
        'include/constants/hold_effects.h': ['HOLD_EFFECT_CAN_ALWAYS_RUN', 'HOLD_EFFECT_FRIENDSHIP_UP'],
        'include/constants/pokemon.h': ['MON_DATA_SPECIES', 'MON_DATA_HELD_ITEM', 'MON_DATA_SPECIES_OR_EGG', 'MON_DATA_FRIENDSHIP', 'MON_DATA_MET_LOCATION', 'MON_DATA_POKEBALL', 'MAX_FRIENDSHIP'] + re.findall(r'^#define (FRIENDSHIP_EVENT_\w+)', sources['include/constants/pokemon.h'], re.M),
        'include/constants/species.h': ['SPECIES_GASTLY', 'SPECIES_HAUNTER', 'SPECIES_CUBONE', 'SPECIES_MAROWAK', 'SPECIES_EGG'],
        'include/constants/trainers.h': ['TRAINER_CLASS_LEADER', 'TRAINER_CLASS_ELITE_FOUR', 'TRAINER_CLASS_CHAMPION'],
        'include/random.h': ['RAND_MULT', 'ISO_RANDOMIZE1'],
    }
    parts = ['/* GENERATED: verbatim source functions with minimal explicitly labeled stubs. */\n#include <stdint.h>\n#include <stdio.h>\n#include <string.h>\n#include <assert.h>\ntypedef uint8_t u8;\ntypedef uint16_t u16;\ntypedef uint32_t u32;\ntypedef int8_t s8;\ntypedef int16_t s16;\ntypedef u8 bool8;\n#define FALSE 0\n']
    for path, names in selections.items():
        parts.append('/* Verbatim macros from ' + path + ' */\n' + '\n'.join(macro(sources[path], name) for name in names))
    parts.append('_Static_assert(BATTLE_TYPE_GHOST_UNVEILED == BATTLE_TYPE_LEGENDARY, "Unveiled and Legendary alias");\n_Static_assert(BATTLE_TYPE_GHOST_UNVEILED == (1u << 13), "Unveiled bit 13");\n_Static_assert(BATTLE_TYPE_GHOST == (1u << 15), "Ghost bit 15");')
    parts.append(STUBS)
    species = []
    parts.append('/* Held fields copied from authenticated species entries; other fields omitted. */\nconst struct SpeciesInfo gSpeciesInfo[106] = {')
    for name in ['GASTLY', 'HAUNTER', 'CUBONE', 'MAROWAK']:
        block = re.search(r'\[SPECIES_' + name + r'\]\s*=\s*\{(.*?)\n    \}', sources['src/data/pokemon/species_info.h'], re.S).group(1)
        common = re.search(r'\.itemCommon = (\w+)', block).group(1)
        rare = re.search(r'\.itemRare = (\w+)', block).group(1)
        parts.append(f'    [SPECIES_{name}] = {{.itemCommon = {common}, .itemRare = {rare}}},')
        species.append({'name': name, 'species': number(sources['include/constants/species.h'], 'SPECIES_' + name), 'itemCommon': number(sources['include/constants/items.h'], common), 'itemRare': number(sources['include/constants/items.h'], rare)})
    parts.append('};')
    friendship_table = re.search(r'static const s8 sFriendshipEventDeltas\[\]\[3\] =\s*\{.*?\n\};', sources['src/pokemon.c'], re.S).group(0)
    parts.append('/* Verbatim friendship delta table from src/pokemon.c. */\n' + friendship_table)
    extracted = []
    for path, signature in [('src/random.c', 'u16 Random(void)'), ('src/battle_main.c', 'bool8 TryRunFromBattle(u8 battler)'), ('src/pokemon.c', 'void SetWildMonHeldItem(void)'), ('src/pokemon.c', 'void AdjustFriendship(struct Pokemon *mon, u8 event)')]:
        body, line = extract_function(sources[path], signature)
        extracted.append({'path': path, 'function': signature, 'line': line, 'sha256': sha(body.encode()), 'text': body})
        if path == 'src/random.c':
            parts.append('#define Random SourceRandom\n')
        parts.append(f'#line {line} "{path}"\n' + body)
        if path == 'src/random.c':
            parts.append('#undef Random\n/* Instrumentation wrapper records unchanged source RNG return values. */\nu16 Random(void) { u16 value = SourceRandom(); assert(drawCount < 16); rawDraws[drawCount++] = value; return value; }')
    parts.append(MAIN)
    return '\n'.join(parts), extracted, species

def boundary_seeds():
    held_targets = {0, 1, 44, 45, 94, 95, 99}
    run_targets = {0, 33, 34, 47, 48, 63, 64, 93, 94, 255}
    held, run = {}, {}
    seed = 0
    while len(held) < len(held_targets) or len(run) < len(run_targets):
        raw = ((seed * 1103515245 + 24691) & 0xffffffff) >> 16
        if raw % 100 in held_targets:
            held.setdefault(raw % 100, seed)
        if raw & 255 in run_targets:
            run.setdefault(raw & 255, seed)
        seed += 1
    return held, run

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--output-dir', type=Path, required=True)
    parser.add_argument('--cc', default='cc')
    args = parser.parse_args()
    lock = json.loads((HERE / 'source-lock.json').read_text())
    assert lock['sourceCommit'] == PIN
    assert git(args.reference_root, 'rev-parse', PIN + '^{commit}').decode().strip() == PIN
    sources = {}
    for path in lock['sourceHashes']:
        raw = git(args.reference_root, 'show', PIN + ':' + path)
        sources[path] = raw.decode('utf-8')
    assertions = source_assertions(sources)
    soothe = next(item for item in json.loads(sources['src/data/items.json'])['items'] if item['itemId'] == 'ITEM_SOOTHE_BELL')
    assert soothe['holdEffect'] == 'HOLD_EFFECT_FRIENDSHIP_UP'
    friendship_table = re.search(r'static const s8 sFriendshipEventDeltas\[\]\[3\] =\s*\{.*?\n\};', sources['src/pokemon.c'], re.S).group(0)
    assert re.search(r'\[FRIENDSHIP_EVENT_GROW_LEVEL\]\s*=\s*\{\s*5,\s*3,\s*2\s*\}', friendship_table)
    assertions['friendshipSourceData'] = {'path': 'src/pokemon.c', 'deltaTableText': friendship_table, 'deltaTableSha256': sha(friendship_table.encode()), 'growLevelBaseDeltas': [5, 3, 2], 'heldItemPath': 'src/data/items.json', 'heldItemName': soothe['itemId'], 'heldItem': number(sources['include/constants/items.h'], soothe['itemId']), 'holdEffectName': soothe['holdEffect'], 'holdEffect': number(sources['include/constants/hold_effects.h'], soothe['holdEffect']), 'ordinaryPokeBallUsed': True, 'luxuryBallBonusExcludedFromVectors': True}
    assert re.search(r'\bu8 runTries;', sources['include/battle.h'])
    generated, extracted, species_data = build_source(sources)
    output = args.output_dir.resolve()
    output.mkdir(parents=True, exist_ok=True)
    cpath = output / 'ghost-oracle.c'
    cpath.write_text(generated)
    binary = output / 'ghost-oracle'
    command = ['flock', '/tmp/firered-remake-build.lock', args.cc, '-std=c11', '-O2', '-Wall', '-Wextra', '-Werror', str(cpath), '-o', str(binary)]
    subprocess.run(command, check=True)
    held_seeds, run_seeds = boundary_seeds()
    run_vectors, held_vectors, rng_vectors, friendship_vectors, lines = [], [], [], [], []
    for (mode, flags), speeds, attempts, ability, seed, battler in itertools.product(MODES, [(25, 50), (50, 50), (75, 50), (29, 32), (29, 41)], [0, 1, 8, 255], [0, 50], sorted({0, 1, 0x12345678, 0xffffffff, *run_seeds.values()}), [0, 1]):
        speed, opposing = speeds
        row = {'id': f'run-{len(run_vectors):04d}', 'mode': mode, 'flags': flags, 'seed': seed, 'speed': speed, 'opposingSpeed': opposing, 'attemptsBefore': attempts, 'ability': ability, 'item': 0, 'enigmaHoldEffect': 0, 'battler': battler}
        run_vectors.append(row)
        lines.append(f'R {flags} {seed} {speed} {opposing} {attempts} {ability} 0 0 {battler}')
    for (mode, flags), item, battler in itertools.product(MODES, [194, 175], [0, 1]):
        row = {'id': f'run-{len(run_vectors):04d}', 'mode': mode, 'flags': flags, 'seed': 0x12345678, 'speed': 25, 'opposingSpeed': 50, 'attemptsBefore': 255, 'ability': 50, 'item': item, 'enigmaHoldEffect': 37, 'battler': battler}
        run_vectors.append(row)
        lines.append(f'R {flags} 305419896 25 50 255 50 {item} 37 {battler}')
    for (mode, flags), species, seed, initial in itertools.product(MODES, [92, 93, 104, 105], sorted({0, 1, 0x12345678, 0xffffffff, *held_seeds.values()}), [0, 224]):
        row = {'id': f'held-{len(held_vectors):04d}', 'mode': mode, 'flags': flags, 'seed': seed, 'species': species, 'initialHeldItem': initial}
        held_vectors.append(row)
        lines.append(f'H {flags} {seed} {species} {initial}')
    for seed, count in itertools.product([0, 1, 0x12345678, 0xffffffff], [0, 1, 2, 8]):
        rng_vectors.append({'id': f'rng-{len(rng_vectors):04d}', 'seed': seed, 'drawCount': count})
        lines.append(f'G {seed} {count}')
    for initial, soothe, same_region in itertools.product([0, 98, 99, 100, 198, 199, 200, 250, 254, 255], [False, True], [False, True]):
        friendship_vectors.append({'id': f'friendship-{len(friendship_vectors):04d}', 'initial': initial, 'sootheBell': soothe, 'sameRegion': same_region})
        lines.append(f'F {initial} {int(soothe)} {int(same_region)}')
    run = subprocess.run([str(binary)], input='\n'.join(lines) + '\n', text=True, capture_output=True, check=True)
    observed = [json.loads(line) for line in run.stdout.splitlines()]
    rows = run_vectors + held_vectors + rng_vectors + friendship_vectors
    assert len(rows) == len(observed)
    for row, result in zip(rows, observed):
        row.update(result)
    for row in run_vectors:
        if row['mode'] == 'disguisedGhost' or row['ability'] or row['item']:
            assert row['attemptsAfter'] == row['attemptsBefore'] and not row['rawDraws'] and row['seed'] == row['finalSeed']
        else:
            assert row['attemptsAfter'] == (row['attemptsBefore'] + 1) % 256
            assert len(row['rawDraws']) == int(row['speed'] < row['opposingSpeed'])
    for row in held_vectors:
        if row['mode'] == 'revealedGhost':
            assert not row['rawDraws'] and row['seed'] == row['finalSeed'] and row['initialHeldItem'] == row['heldItem']
        else:
            assert len(row['rawDraws']) == 1
    for row in friendship_vectors:
        assert not row['rawDraws'] and row['finalSeed'] == 0 and row['setMonDataCalls'] == 1
        assert row['initial'] <= row['final'] <= 255
    result = {
        'schemaVersion': 1, 'sourceCommit': PIN, 'sourceHashes': lock['sourceHashes'],
        'executionScope': 'Host C executes verbatim TryRunFromBattle, SetWildMonHeldItem, and AdjustFriendship, plus verbatim Random renamed only by preprocessor. Data/global/accessor and item-effect lookup stubs are explicit. Friendship vectors execute only GROW_LEVEL with a normal Poke Ball; no complete original engine is executed.',
        'exactExtractedFunctions': extracted, 'sourceAssertions': assertions,
        'speciesHeldItems': species_data,
        'rngConstants': {'multiplier': 1103515245, 'increment': 24691, 'outputShift': 16, 'stateBits': 32, 'outputBits': 16},
        'rngVectors': rng_vectors,
        'friendshipVectors': friendship_vectors,
        'boundarySeeds': {'heldModulo100': held_seeds, 'runLowByte': run_seeds},
        'runVectors': run_vectors, 'heldItemVectors': held_vectors,
    }
    (output / 'ghost-vectors.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    (output / 'source-lock.json').write_text(json.dumps(lock, indent=2) + '\n')
    (output / 'ghost-inputs.txt').write_text('\n'.join(lines) + '\n')
    manifest = {'sourceCommit': PIN, 'runVectors': len(run_vectors), 'heldItemVectors': len(held_vectors), 'rngVectors': len(rng_vectors), 'friendshipVectors': len(friendship_vectors), 'compiler': subprocess.check_output([args.cc, '--version'], text=True).splitlines()[0], 'compileArguments': command, 'artifacts': {name: sha((output / name).read_bytes()) for name in ['ghost-oracle.c', 'ghost-vectors.json', 'source-lock.json', 'ghost-inputs.txt']}}
    (output / 'generation-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps({'outputDir': str(output), 'runVectors': len(run_vectors), 'heldItemVectors': len(held_vectors), 'rngVectors': len(rng_vectors), 'friendshipVectors': len(friendship_vectors), 'allSourceAssertionsPassed': True}))

if __name__ == '__main__':
    main()
