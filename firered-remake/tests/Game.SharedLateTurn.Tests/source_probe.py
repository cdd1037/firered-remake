#!/usr/bin/env python3
"""Execute unchanged late-turn command bodies from the authenticated FireRed pin.

This is a bounded host C proof, not an upstream build or remake-derived oracle.
Capture refuses to overwrite vectors; normal execution verifies the stored capture.
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
NAMES = ('BIDE', 'PERISH_SONG', 'DESTINY_BOND', 'GRUDGE')


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


def case(text, name, next_name):
    return text.split('        case ' + name + ':', 1)[1].split('        case ' + next_name + ':', 1)[0]


def commands(scripts, label):
    body = scripts.split(label + '::\n', 1)[1]
    return [line.strip() for line in body.split('\n\n', 1)[0].splitlines() if line.strip()]


def ordered(text, tokens):
    offsets = [text.index(token) for token in tokens]
    assert offsets == sorted(offsets), tokens


def audit(c):
    source = c['src/battle_script_commands.c']
    util = c['src/battle_util.c']
    scripts = c['data/battle_scripts_1.s']
    checks = []
    assert commands(scripts, 'BattleScript_EffectBide') == [
        'attackcanceler', 'attackstring', 'ppreduce', 'attackanimation', 'waitanimation',
        'orword gHitMarker, HITMARKER_CHARGING', 'setbide', 'goto BattleScript_MoveEnd']
    checks.append('Bide setup script has one PP command, no accuracy or damage commands')
    for label in ('BattleScript_BideAttack', 'BattleScript_BideNoEnergyToAttack'):
        ordered('\n'.join(commands(scripts, label)), ['attackcanceler', 'setmoveeffect MOVE_EFFECT_CHARGING', 'clearstatusfromeffect BS_ATTACKER'])
        assert 'ppreduce' not in commands(scripts, label)
    release = '\n'.join(commands(scripts, 'BattleScript_BideAttack'))
    ordered(release, ['accuracycheck', 'typecalc', 'copyword gBattleMoveDamage, sBIDE_DMG', 'adjustsetdamage', 'datahpupdate BS_TARGET', 'tryfaintmon BS_TARGET'])
    checks.append('Bide release/no-energy scripts clear charging lock; release uses accuracy, type immunity and fixed stored damage with no PP deduction')
    damage = block(source, r'^static void Cmd_datahpupdate\(void\)\n')
    ordered(damage, ['STATUS2_SUBSTITUTE', 'HITMARKER_SKIP_DMG_TRACK', 'gTakenDmg[gActiveBattler] += gBattleMoveDamage;', 'gTakenDmgByBattler[gActiveBattler] = gBattlerAttacker;', 'if (gBattleMons[gActiveBattler].hp > gBattleMoveDamage)'])
    assert 'gTakenDmgByBattler[gActiveBattler] = gBattlerTarget;' in damage
    checks.append('Damage tracking adds raw damage before HP clamp and remembers latest source; substitute and skip-damage-track paths bypass it')
    canceller = block(util, r'^u8 AtkCanceller_UnableToUseMove\(void\)\n')
    ordered(canceller, ['case CANCELLER_FLAGS:', '~STATUS2_DESTINY_BOND', '~STATUS3_GRUDGE', 'case CANCELLER_ASLEEP:', 'case CANCELLER_BIDE:'])
    checks.append('Action flags clear Destiny Bond/Grudge before sleep and other incapacity checks')
    perish = block(source, r'^static void Cmd_trysetperishsong\(void\)\n')
    assert 'for (i = 0; i < gBattlersCount; i++)' in perish
    assert all(token not in perish for token in ('gAbsentBattlerFlags', '.hp', 'gBattlerByTurnOrder'))
    ordered(perish, ['STATUS3_PERISH_SONG', 'ABILITY_SOUNDPROOF', 'perishSongTimer = 3', 'PressurePPLoseOnUsingPerishSong', 'notAffectedCount == gBattlersCount'])
    checks.append('Perish application uses numeric battler count including absent/zero-HP slots; existing status and Soundproof block without timer refresh')
    pressure = block(util, r'^void PressurePPLoseOnUsingPerishSong\(u8 attacker\)\n')
    assert all(token not in pressure for token in ('gAbsentBattlerFlags', '.hp', 'GetBattlerSide'))
    ordered(pressure, ['ABILITY_PRESSURE && i != attacker', 'MOVE_PERISH_SONG', 'pp[j] != 0', 'pp[j]--'])
    checks.append('Perish extra Pressure PP loss includes allies/absent/zero-HP holders, excludes user and saturates at zero even when application fails')
    delayed = block(util, r'^bool8 HandleWishPerishSongOnTurnEnd\(void\)\n')
    ordered(delayed, ['HITMARKER_GRUDGE | HITMARKER_SKIP_DMG_TRACK', 'futureSightCounter', 'case 1:', 'gBattlerByTurnOrder', 'perishSongTimer == 0', '~STATUS3_PERISH_SONG', 'gBattleMoveDamage = gBattleMons[gActiveBattler].hp', 'perishSongTimer--'])
    perish_end = delayed.split('    case 1:', 1)[1]
    assert 'gAbsentBattlerFlags & gBitTable[gActiveBattler]' in perish_end
    assert '.hp != 0' not in perish_end
    checks.append('Delayed Future attacks precede retained-order Perish; Perish skips absent slots and tests zero before decrement: visible counts 3,2,1,0')
    faint = block(source, r'^static void Cmd_tryfaintmon\(void\)\n')
    ordered(faint, ['gBattleMons[gActiveBattler].hp == 0', 'HITMARKER_DESTINYBOND', 'gBattleMons[gBattlerAttacker].hp != 0', 'BattleScript_DestinyBondTakesLife', 'STATUS3_GRUDGE', '!(gHitMarker & HITMARKER_GRUDGE)', 'GetBattlerSide(gBattlerAttacker) != GetBattlerSide(gBattlerTarget)', 'gCurrentMove != MOVE_STRUGGLE', 'chosenMovePositions + gBattlerAttacker', 'pp[moveIndex] = 0'])
    checks.append('Faint hook requires living attacker for Bond/Grudge; Grudge excludes passive GRUDGE marker, allies and Struggle and clears retained chosen-slot PP')
    for label in ('BattleScript_DestinyBondTakesLife', 'BattleScript_PerishSongTakesLife'):
        assert 'orword gHitMarker, HITMARKER_IGNORE_SUBSTITUTE | HITMARKER_PASSIVE_DAMAGE' in commands(scripts, label)
    checks.append('Destiny Bond and Perish lethal scripts ignore Substitute and mark passive HP damage')
    for label, cmd in (('BattleScript_EffectDestinyBond', 'setdestinybond'), ('BattleScript_EffectGrudge', 'trysetgrudge BattleScript_ButItFailed')):
        ordered('\n'.join(commands(scripts, label)), ['attackcanceler', 'attackstring', 'ppreduce', cmd])
    checks.append('Destiny Bond and Grudge scripts run ordinary action cancellation and PP cost before setup')
    expected = {'BIDE': (1, 100, 10), 'PERISH_SONG': (0, 0, 5), 'DESTINY_BOND': (0, 0, 5), 'GRUDGE': (0, 100, 5)}
    metadata = {}
    for name in NAMES:
        fields = dict(re.findall(r'\.(\w+) = ([^,]+),', block(c['src/data/battle_moves.h'], rf'^    \[MOVE_{name}\] =')))
        assert tuple(int(fields[k]) for k in ('power', 'accuracy', 'pp')) == expected[name]
        assert fields['priority'] == '0' and fields['target'] == 'MOVE_TARGET_USER'
        metadata[name] = fields
    checks.append('All four original moves use user target and priority zero, with exact source power/accuracy/PP')
    return checks, metadata


def validate(rows):
    assert len(rows) == 34, len(rows)
    assert {r['kind'] for r in rows} == {'bide-setup', 'bide-step', 'perish-setup', 'destiny-trigger', 'grudge-setup', 'action-flags-clear'}
    for r in rows:
        if r['kind'] == 'bide-setup':
            assert (r['status2'], r['duration'], r['damage'], r['lastSource'], r['lockedMove'], r['cursor']) == (0x101200, 2, 0, 3, 117, 1)
        elif r['kind'] == 'bide-step':
            expected = [(1,-7,1,45,1,1,0), (0,34,3,117,2,1,0), (0,34,1,117,2,1,1), (0,-7,1,45,3,1,0), (0,34,2,117,2,1,0), (0,-7,1,45,0,0,0)]
            assert tuple(r[k] for k in ('duration','damage','target','move','branch','effect','retargetCalls')) == expected[r['case']]
            assert r['multipleTurns'] == r['tracker'] == 1
        elif r['kind'] == 'perish-setup':
            expected = [
                ([1056]*4, [3]*4, [3]*4, 5, 5, 0),
                ([1056,1024,1056,1056], [1,7,3,3], [3,7,3,3], 5, 5, 0),
                ([1056,1024,1056,1024], [1,7,1,7], [3,7,3,7], -1, 5, 0),
                ([1056,1056,1024,1024], [3,3,7,7], [3,3,7,7], 5, 5, 0),
                ([1056]*4, [3]*4, [3]*4, 5, 0, 1),
                ([1056]*4, [1]*4, [3]*4, -1, 3, 1),
            ]
            assert tuple(r[k] for k in ('status3','timers','starts','cursor','pp','ppWrites')) == expected[r['case']]
            assert r['absent'] == 4 and r['battlers'] == (2 if r['case'] == 3 else 4)
        elif r['kind'] == 'destiny-trigger':
            assert r['trigger'] == [1,0,0,1,1,0,0,0,1,0][r['case']]
            assert r['armed'] == (r['case'] != 6)
            expected_marker = 0x08000000 | (0x40 if r['trigger'] else 0)
            expected_marker |= 0x01000000 if r['case'] == 7 else 0
            expected_marker |= 0x00100000 if r['case'] == 8 else 0
            assert r['hitMarker'] == expected_marker
            assert r['status2'] == (0x00100000 | (0x02000000 if r['armed'] else 0))
        elif r['kind'] == 'grudge-setup':
            assert (r['firstCursor'],r['secondCursor'],r['firstStatus3'],r['secondStatus3']) == (5,-1,0x4400,0x4400)
        elif r['kind'] == 'action-flags-clear':
            assert (r['status2'],r['status3'],r['tracker']) == (0x100000,0x400,1)
        else:
            raise AssertionError(r)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, default=default_source())
    p.add_argument('--vectors', type=Path, default=HERE / 'source-vectors.json')
    p.add_argument('--capture', action='store_true')
    p.add_argument('--report', type=Path)
    a = p.parse_args()
    assert not a.capture or not a.vectors.exists(), 'Immutable vectors already exist'
    root = a.source.resolve()
    assert run(['git','rev-parse','HEAD'], cwd=root).strip() == PIN
    paths = ['src/battle_script_commands.c','src/battle_util.c','src/battle_anim_mons.c','src/data/battle_moves.h','data/battle_scripts_1.s','include/battle.h']
    paths += [f'include/constants/{name}.h' for name in ('battle','global','pokemon','moves','abilities')]
    c = {path: (root/path).read_text() for path in paths}
    for path in paths:
        assert c[path] == run(['git','show',f'{PIN}:{path}'], cwd=root), f'Modified pinned input: {path}'
    checks, metadata = audit(c)
    source = c['src/battle_script_commands.c']; util = c['src/battle_util.c']
    functions = {'GetBattlerSide': block(c['src/battle_anim_mons.c'], r'^u8 GetBattlerSide\(u8 battlerId\)\n')}
    functions['PressurePPLoseOnUsingPerishSong'] = block(util, r'^void PressurePPLoseOnUsingPerishSong\(u8 attacker\)\n')
    for name in ('Cmd_setbide','Cmd_setdestinybond','TrySetDestinyBondToHappen','Cmd_trysetperishsong','Cmd_trysetgrudge'):
        functions[name] = block(source, rf'^static void {name}\(void\)\n')
    fragments = {name: '        case ' + name + ':' + case(util, name, next_name)
                 for name, next_name in (('CANCELLER_BIDE','CANCELLER_THAW'),('CANCELLER_FLAGS','CANCELLER_ASLEEP'))}
    macros = '\n'.join(re.search(r'^#define ' + name + r'\([^\n]+', c['include/battle.h'], re.M).group()
                       for name in ('GET_BATTLER_POSITION','GET_BATTLER_SIDE2'))
    unit = (HERE/'source-prefix.h').read_text() + '\n' + macros + '\n' + '\n\n'.join(functions.values())
    unit += '\nstatic u8 SourceBideCanceller(void) { u8 effect = 0; s32 *bideDmg = &gBattleScripting.bideDmg; switch (CANCELLER_BIDE) {\n'
    unit += fragments['CANCELLER_BIDE'] + '} return effect; }\n'
    unit += 'static void SourceFlagsCanceller(void) { switch (CANCELLER_FLAGS) {\n' + fragments['CANCELLER_FLAGS'] + '} }\n'
    unit += (HERE/'source-main.c').read_text()
    with tempfile.TemporaryDirectory(prefix='firered-late-turn-probe-') as tmp:
        tmp = Path(tmp); (tmp/'probe.c').write_text(unit); outputs = []
        for label, flags in (('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])):
            run([os.environ.get('CC','cc'),'-std=c11','-Wall','-Wextra','-Werror',*flags,'-I',str(root/'include'),str(tmp/'probe.c'),'-o',str(tmp/label)])
            outputs.append(run([str(tmp/label)]))
    assert outputs[0] == outputs[1], 'Optimized/UBSan divergence'
    rows = [json.loads(line) for line in outputs[0].splitlines()]
    validate(rows)
    snap = {'schema':'firered-late-turn-source-vectors-v1','sourceCommit':PIN,
            'sourceFiles':{path:sha(c[path]) for path in paths},
            'extractedFunctions':{name:sha(body) for name,body in functions.items()},
            'extractedCases':{name:sha(body) for name,body in fragments.items()},
            'scriptAssertions':checks,'moves':metadata,'vectors':rows}
    if a.capture:
        with a.vectors.open('x') as f: json.dump(snap,f,indent=2); f.write('\n')
    else:
        assert snap == json.loads(a.vectors.read_text()), 'Immutable source vector mismatch'
    report = {'passed':True,'mode':'capture' if a.capture else 'verify','sourceCommit':PIN,
              'vectors':len(rows),'scriptAssertions':len(checks),'extractedFunctions':len(functions),
              'extractedCases':len(fragments),'builds':['-O2','-O1 -fsanitize=undefined -fno-sanitize-recover=all'],
              'vectorsSha256':hashlib.sha256(a.vectors.read_bytes()).hexdigest()}
    if a.report: a.report.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))


if __name__ == '__main__':
    main()
