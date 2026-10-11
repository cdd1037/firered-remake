#!/usr/bin/env python3
"""Compile unchanged pinned dynamic-power commands with bounded dependency stubs.
Default verifies immutable grouped vectors; --capture refuses replacement.
No remake binary, sibling helper, upstream script, ROM or network is used.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
PIN = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
NAMES = ('RETURN', 'FRUSTRATION', 'FLAIL', 'REVERSAL', 'ERUPTION', 'WATER_SPOUT', 'LOW_KICK')
MAXIMA = [1, 2, 3, 7, 47, 48, 49, 97, 100, 146, 149, 150, 151, 178, 226, 242, 255, 256, 322, 714, 1023, 32767]


def run(argv, **kwargs):
    result = subprocess.run(argv, check=False, text=True, capture_output=True, **kwargs)
    if result.returncode:
        raise RuntimeError(f'{argv!r}\n{result.stdout}\n{result.stderr}')
    return result.stdout


def sha(value):
    return hashlib.sha256(value.encode()).hexdigest()


def default_source():
    for ancestor in HERE.parents:
        for rel in ('audio-reference-build/pokefirered', 'firered-restored-20261011/audio-reference-build/pokefirered'):
            if (ancestor / rel / 'src/battle_script_commands.c').is_file():
                return ancestor / rel
    return Path('audio-reference-build/pokefirered')


def block(text, pattern):
    match = re.search(pattern, text, re.M)
    assert match, pattern
    depth = 0
    for i in range(text.index('{', match.start()), len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0:
            return text[match.start():i + 1]
    raise AssertionError('Unclosed source block')


def commands(scripts, label):
    body = scripts.split(label + '::\n', 1)[1]
    return [line.strip() for line in body.split('\n\n', 1)[0].splitlines() if line.strip()]


def ordered(text, tokens):
    offsets = [text.index(token) for token in tokens]
    assert offsets == sorted(offsets), tokens


def audit(c):
    source = c['src/battle_script_commands.c']; scripts = c['data/battle_scripts_1.s']
    script_rows = {}
    for label in ('BattleScript_EffectFlail', 'BattleScript_EffectEruption', 'BattleScript_EffectReturn', 'BattleScript_EffectFrustration', 'BattleScript_EffectLowKick', 'BattleScript_EffectHit'):
        script_rows[label] = commands(scripts, label)
    assert script_rows['BattleScript_EffectFlail'] == ['remaininghptopower', 'goto BattleScript_EffectHit']
    assert script_rows['BattleScript_EffectEruption'] == ['scaledamagebyhealthratio', 'goto BattleScript_EffectHit']
    assert script_rows['BattleScript_EffectReturn'][0] == 'BattleScript_EffectFrustration::'
    assert script_rows['BattleScript_EffectFrustration'] == ['attackcanceler', 'accuracycheck BattleScript_PrintMoveMissed, ACC_CURR_MOVE', 'friendshiptodamagecalculation', 'goto BattleScript_HitFromAtkString']
    assert script_rows['BattleScript_EffectLowKick'] == ['attackcanceler', 'attackstring', 'ppreduce', 'weightdamagecalculation', 'accuracycheck BattleScript_MoveMissedPause, ACC_CURR_MOVE', 'goto BattleScript_HitFromCritCalc']
    hit = '\n'.join(script_rows['BattleScript_EffectHit'])
    ordered(hit, ['attackcanceler', 'accuracycheck BattleScript_PrintMoveMissed, ACC_CURR_MOVE', 'attackstring', 'ppreduce', 'critcalc', 'damagecalc', 'typecalc', 'adjustnormaldamage', 'datahpupdate BS_TARGET', 'tryfaintmon BS_TARGET'])
    assert 'NO_ACC_CALC' not in hit
    adjust = block(source, r'^static void Cmd_adjustnormaldamage\(void\)\n')
    assert 'ApplyRandomDmgMultiplier();' in adjust
    damage = block(source, r'^static void Cmd_damagecalc\(void\)\n')
    cleanup = block(source, r'^static void MoveValuesCleanUp\(void\)\n')
    assert not re.search(r'gDynamicBasePower\s*=', damage + cleanup)
    ai = block(source, r'^void AI_CalcDmg\(u8 attacker, u8 defender\)\n')
    assert 'gDynamicBasePower = 0;' in ai
    next_target = source.split('        case MOVEEND_NEXT_TARGET:', 1)[1].split('        case MOVEEND_COUNT:', 1)[0]
    ordered(next_target, ['MOVE_TARGET_BOTH', 'gBattleMons[battlerId].hp != 0', 'gBattlerTarget = battlerId;', 'MoveValuesCleanUp();', 'BattleScriptPush(gBattleScriptsForMoveEffects[gBattleMoves[gCurrentMove].effect]);'])
    assert 'gDynamicBasePower' not in next_target
    finish = block(c['src/battle_main.c'], r'^static void HandleAction_ActionFinished\(void\)\n')
    assert 'gDynamicBasePower = 0;' in finish
    checks = [
        'Flail/Reversal calculate before the attack canceller, then take ordinary accuracy, critical and random-damage commands; no NO_ACC_CALC path.',
        'Eruption/Water Spout calculate before the attack canceller and preserve any nonzero dynamic power across later target passes.',
        'Return/Frustration calculate after accuracy success and before attack string/PP; zero command power falls back to move metadata power 1.',
        'Low Kick calculates after attack string/PP and before accuracy; strict threshold > weight promotes exact boundaries to the next tier.',
        'Gameplay Cmd_damagecalc and MoveValuesCleanUp retain dynamic power; only adjacent AI_CalcDmg clears it.',
        'MOVEEND_NEXT_TARGET restarts the move effect after MoveValuesCleanUp without clearing power; HandleAction_ActionFinished clears power.',
        'GetScaledHPFraction returns at least 1 for positive HP, with signed 16-bit HP/maxHP parameters; probes stay within 0 <= HP <= maxHP <= 32767.',
    ]
    metadata = {}
    ids = dict((name, int(value)) for name, value in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$', c['include/constants/moves.h'], re.M))
    expected_effects = ('RETURN', 'FRUSTRATION', 'FLAIL', 'FLAIL', 'ERUPTION', 'ERUPTION', 'LOW_KICK')
    for name, effect in zip(NAMES, expected_effects):
        fields = dict(re.findall(r'\.(\w+) = ([^,]+),', block(c['src/data/battle_moves.h'], rf'^    \[MOVE_{name}\] =')))
        assert fields['effect'] == 'EFFECT_' + effect
        assert int(fields['power']) == (150 if effect == 'ERUPTION' else 1)
        assert fields['accuracy'] == '100' and fields['priority'] == '0'
        assert fields['target'] == ('MOVE_TARGET_BOTH' if effect == 'ERUPTION' else 'MOVE_TARGET_SELECTED')
        metadata[name] = dict(id=ids[name], **fields)
    return checks, script_rows, metadata, finish


def validate(rows):
    assert len(rows) == 135
    counts = {kind: sum(row['kind'] == kind for row in rows) for kind in ('friendship', 'hp', 'weight', 'eruption-cache', 'flail-crit-random')}
    assert counts == {'friendship': 2, 'hp': 44, 'weight': 1, 'eruption-cache': 24, 'flail-crit-random': 64}
    for row in rows:
        if row['kind'] == 'friendship':
            assert len(row['powers']) == len(row['effectivePowers']) == 256
            assert sorted(set(row['powers'])) == list(range(103))
            assert row['effectivePowers'].count(0) == 0
        elif row['kind'] == 'hp':
            assert row['maxHp'] in MAXIMA and len(row['powers']) == row['maxHp'] + 1
            assert all(value > 0 for value in row['powers'])
        elif row['kind'] == 'weight':
            assert row['ranges'] == [{'from': 0, 'through': 99, 'power': 20}, {'from': 100, 'through': 249, 'power': 40}, {'from': 250, 'through': 499, 'power': 60}, {'from': 500, 'through': 999, 'power': 80}, {'from': 1000, 'through': 1999, 'power': 100}, {'from': 2000, 'through': 65535, 'power': 120}]
        elif row['kind'] == 'eruption-cache':
            assert row['firstPower'] == row['afterDamagePower'] == row['afterCleanupPower'] == row['secondPower']
            assert row['afterActionFinishPower'] == 0 and row['nextActionPower'] == 1
            assert row['secondOffset'] == (3 if row['damageCalc'] else 2)
        else:
            assert row['criticalMultiplier'] == (2 if row['rawRandom'] % 16 == 0 else 1)
            assert row['randomCalls'] == 2
    return counts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=default_source())
    parser.add_argument('--vectors', type=Path, default=HERE / 'source-vectors.json')
    parser.add_argument('--capture', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    assert not args.capture or not args.vectors.exists(), 'Immutable vectors already exist'
    root = args.source.resolve()
    assert run(['git', 'rev-parse', 'HEAD'], cwd=root).strip() == PIN
    paths = ['src/battle_script_commands.c', 'src/battle_interface.c', 'src/battle_main.c', 'src/pokemon.c', 'src/data/battle_moves.h', 'data/battle_scripts_1.s', 'include/battle.h']
    paths += [f'include/constants/{name}.h' for name in ('battle', 'battle_move_effects', 'battle_script_commands', 'pokemon', 'moves', 'abilities', 'species', 'items', 'hold_effects')]
    c = {path: (root / path).read_text() for path in paths}
    for path, text in c.items():
        assert text == run(['git', 'show', f'{PIN}:{path}'], cwd=root), f'Modified pinned input: {path}'
    checks, script_rows, metadata, finish = audit(c)
    source = c['src/battle_script_commands.c']
    functions = {name: block(source, rf'^static (?:inline )?void {name}\(void\)\n') for name in ('Cmd_friendshiptodamagecalculation', 'Cmd_remaininghptopower', 'Cmd_scaledamagebyhealthratio', 'Cmd_weightdamagecalculation', 'Cmd_damagecalc', 'MoveValuesCleanUp', 'Cmd_critcalc', 'ApplyRandomDmgMultiplier')}
    functions = {'GetScaledHPFraction': block(c['src/battle_interface.c'], r'^u8 GetScaledHPFraction\(s16 hp, s16 maxhp, u8 scale\)\n'), **functions}
    tables = {name: block(source, rf'^static const u(?:8|16) {name}\[\] =') + ';' for name in ('sFlailHpScaleToPowerTable', 'sWeightToDamageTable', 'sCriticalHitChance')}
    select_start = c['src/pokemon.c'].index('    if (!powerOverride)\n')
    select_end = c['src/pokemon.c'].index('\n\n', select_start)
    selection = c['src/pokemon.c'][select_start:select_end]
    assert selection == '    if (!powerOverride)\n        gBattleMovePower = gBattleMoves[move].power;\n    else\n        gBattleMovePower = powerOverride;'
    reset = re.search(r'^    gDynamicBasePower = 0;$', finish, re.M).group()
    macros = '\n'.join(line for line in c['include/battle.h'].splitlines() if re.match(r'#define (MOVE_TARGET_\w+\s|GET_BATTLER_SIDE\()', line))
    assert 'MOVE_TARGET_BOTH' in macros and 'GET_BATTLER_SIDE' in macros
    prefix = (HERE / 'source-prefix.h').read_text().replace('/* SOURCE_BATTLE_MACROS */', macros).replace('    /* SOURCE_POWER_SELECTION */', selection)
    unit = prefix + '\nstatic const struct MoveHost gBattleMoves[355]={\n' + ',\n'.join(block(c['src/data/battle_moves.h'], rf'^    \[MOVE_{name}\] =') for name in NAMES) + '\n};\n'
    unit += '\n\n'.join(tables.values()) + '\n\n' + '\n\n'.join(functions.values())
    unit += '\nstatic void SourceActionFinishDynamicReset(void)\n{\n' + reset + '\n}\n' + (HERE / 'source-main.c').read_text()
    with tempfile.TemporaryDirectory(prefix='firered-dynamic-power-probe-') as temporary:
        temporary = Path(temporary); (temporary / 'probe.c').write_text(unit); outputs = []
        for name, flags in (('optimized', ['-O2']), ('ubsan', ['-O1', '-g', '-fsanitize=undefined', '-fno-sanitize-recover=all'])):
            run([os.environ.get('CC', 'cc'), '-std=c11', '-Wall', '-Wextra', '-Werror', *flags, '-I', str(root / 'include'), str(temporary / 'probe.c'), '-o', str(temporary / name)])
            outputs.append(run([str(temporary / name)]))
    assert outputs[0] == outputs[1], 'Optimized/UBSan divergence'
    rows = [json.loads(line) for line in outputs[0].splitlines()]; counts = validate(rows)
    snapshot = {'schema': 'firered-dynamic-power-source-vectors-v1', 'sourceCommit': PIN,
        'fixtureScope': 'Grouped unchanged command oracle; powers indexed by all friendship/HP integers for listed HP denominators; every u16 weight tested then range-compressed. Damage dependency returns selected power only. Script position, cache lifetime, ordinary critical and variance are separately observed; this is not full-battle or damage-formula conformance.',
        'sourceFiles': {path: sha(text) for path, text in c.items()},
        'extractedFunctions': {name: sha(text) for name, text in functions.items()},
        'extractedTables': {name: sha(text) for name, text in tables.items()},
        'extractedStatements': {'CalculateBaseDamagePowerSelection': sha(selection), 'HandleAction_ActionFinishedDynamicReset': sha(reset)},
        'harnessFiles': {name: sha((HERE / name).read_text()) for name in ('source-prefix.h', 'source-main.c')},
        'scriptAssertions': checks, 'scripts': script_rows, 'moves': metadata, 'vectors': rows}
    if args.capture:
        with args.vectors.open('x') as file:
            json.dump(snapshot, file, separators=(',', ':')); file.write('\n')
    else:
        assert snapshot == json.loads(args.vectors.read_text()), 'Immutable source vector mismatch'
    report = {'passed': True, 'mode': 'capture' if args.capture else 'verify', 'sourceCommit': PIN, 'groups': len(rows), 'groupsByKind': counts, 'hpCommandExecutions': 4 * sum(maximum + 1 for maximum in MAXIMA), 'friendshipInputs': 512, 'weightInputs': 65536, 'builds': ['-O2', '-O1 -g -fsanitize=undefined -fno-sanitize-recover=all'], 'vectorsSha256': hashlib.sha256(args.vectors.read_bytes()).hexdigest()}
    if args.report:
        args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
