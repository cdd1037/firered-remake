#!/usr/bin/env python3
"""Authenticate pinned FireRed source; execute compact unchanged C command vectors.
--capture refuses to replace existing vectors. Default verifies immutable vectors.
No remake binary, sibling test helper, upstream build or ROM is involved.
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
NAMES = ('FLY', 'BOUNCE', 'DIG', 'DIVE', 'SOLAR_BEAM', 'RAZOR_WIND', 'SKY_ATTACK', 'SKULL_BASH')


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
    source, scripts, util, main, pokemon = (c[p] for p in ('src/battle_script_commands.c', 'data/battle_scripts_1.s', 'src/battle_util.c', 'src/battle_main.c', 'src/pokemon.c'))
    checks = []
    assert commands(scripts, 'BattleScriptFirstChargingTurn') == [
        'attackcanceler', 'printstring STRINGID_EMPTYSTRING3', 'ppreduce', 'attackanimation', 'waitanimation',
        'orword gHitMarker, HITMARKER_CHARGING', 'setmoveeffect MOVE_EFFECT_CHARGING | MOVE_EFFECT_AFFECTS_USER',
        'seteffectprimary', 'copybyte cMULTISTRING_CHOOSER, sTWOTURN_STRINGID',
        'printfromtable gFirstTurnOfTwoStringIds', 'waitmessage B_WAIT_TIME_LONG', 'return']
    checks.append('Charge turn: exact command order; no accuracy, critical, damage or target effect rolls')
    assert commands(scripts, 'BattleScript_TwoTurnMovesSecondTurn') == [
        'attackcanceler', 'setmoveeffect MOVE_EFFECT_CHARGING', 'setbyte sB_ANIM_TURN, 1',
        'clearstatusfromeffect BS_ATTACKER', 'orword gHitMarker, HITMARKER_NO_PPDEDUCT',
        'jumpifnotmove MOVE_SKY_ATTACK, BattleScript_HitFromAccCheck',
        'setmoveeffect MOVE_EFFECT_FLINCH', 'goto BattleScript_HitFromAccCheck']
    checks.append('Release: incapacity first, clear lock, suppress PP; Sky Attack applies flinch')
    semi = '\n'.join(commands(scripts, 'BattleScript_SecondTurnSemiInvulnerable'))
    ordered(semi, ['attackcanceler', 'clearstatusfromeffect BS_ATTACKER', 'HITMARKER_NO_PPDEDUCT', 'MOVE_BOUNCE', 'MOVE_EFFECT_PARALYSIS', 'accuracycheck', 'clearsemiinvulnerablebit'])
    assert commands(scripts, 'BattleScript_SemiInvulnerableMiss') == ['clearsemiinvulnerablebit', 'goto BattleScript_PrintMoveMissed']
    checks.append('Semi release clears semi bits on accuracy success and failure; Bounce applies paralysis')
    solar = '\n'.join(commands(scripts, 'BattleScript_EffectSolarBeam'))
    ordered(solar, ['ABILITY_CLOUD_NINE', 'ABILITY_AIR_LOCK', 'B_WEATHER_SUN', 'STATUS2_MULTIPLETURNS'])
    assert commands(scripts, 'BattleScript_SolarBeamOnFirstTurn') == [
        'orword gHitMarker, HITMARKER_CHARGING', 'setmoveeffect MOVE_EFFECT_CHARGING | MOVE_EFFECT_AFFECTS_USER',
        'seteffectprimary', 'ppreduce', 'goto BattleScript_TwoTurnMovesSecondTurn']
    checks.append('Unsuppressed sun precedes existing-lock check; sun appearing on release deducts PP again before canceller')
    solar_damage = '(gBattleWeather & (B_WEATHER_RAIN | B_WEATHER_SANDSTORM | B_WEATHER_HAIL_TEMPORARY)) && gCurrentMove == MOVE_SOLAR_BEAM'
    at = pokemon.index(solar_damage)
    assert 'damage /= 2;' in pokemon[at:at + 220]
    assert 'if (WEATHER_HAS_EFFECT2)' in pokemon[at - 850:at]
    checks.append('Weather-effective rain/sand/hail halves Solar Beam damage')
    assert 'setstatchanger STAT_DEF, 1, FALSE' in '\n'.join(commands(scripts, 'BattleScript_EffectSkullBash'))
    critical = block(source, r'^static void Cmd_critcalc\(void\)\n')
    assert 'EFFECT_SKY_ATTACK' in critical and 'EFFECT_RAZOR_WIND' not in critical
    checks.append('Skull Bash raises Defense on charge; source gives Sky Attack high critical stage, not Razor Wind')
    cancel = block(source, r'^static void Cmd_attackcanceler\(void\)\n')
    assert '(!IsTwoTurnsMove(gCurrentMove) || (gBattleMons[gBattlerAttacker].status2 & STATUS2_MULTIPLETURNS))' in cancel
    assert 'CancelMultiTurnMoves(gBattlerAttacker);' in cancel
    assert '&& !(gBattleMons[gBattlerAttacker].status2 & STATUS2_MULTIPLETURNS)' in cancel
    checks.append('Protect skips setup, cancels release; zero PP does not stop locked release')
    for name in ('TRUANT', 'RECHARGE', 'FLINCH', 'DISABLED', 'TAUNTED', 'IMPRISONED', 'IN_LOVE'):
        case = util.split('case CANCELLER_' + name + ':', 1)[1].split('case CANCELLER_', 1)[0]
        assert 'CancelMultiTurnMoves(gBattlerAttacker);' in case
    assert 'cancelmultiturnmoves BS_ATTACKER' in commands(scripts, 'BattleScript_MoveUsedIsParalyzed')
    assert 'cancelmultiturnmoves BS_ATTACKER' in commands(scripts, 'BattleScript_DoSelfConfusionDmg')
    assert 'cancelmultiturnmoves BS_ATTACKER' not in commands(scripts, 'BattleScript_MoveUsedIsFrozen')
    sleep = block(main, r'^static void HandleEndTurn_ContinueBattle\(void\)\n')
    ordered(sleep, ['STATUS1_SLEEP', 'STATUS2_MULTIPLETURNS', 'CancelMultiTurnMoves(i)'])
    checks.append('Paralysis/confusion self-hit cancel in scripts; frozen action preserves; sleep cancels at end-turn; other incapacity C branches cancel')
    freeze = source.split('        case STATUS1_FREEZE:', 1)[1].split('        case STATUS1_PARALYSIS:', 1)[0]
    assert 'CancelMultiTurnMoves(gEffectBattler);' in freeze
    checks.append('Successful freeze infliction cancels charge immediately; frozen-action retention is a separate code path')
    helper = block(source, r'^static bool8 AccuracyCalcHelper\(u16 move\)\n')
    ordered(helper, ['STATUS3_ALWAYS_HITS', 'STATUS3_ON_AIR', 'STATUS3_UNDERGROUND', 'STATUS3_UNDERWATER', 'EFFECT_ALWAYS_HIT'])
    checks.append('Lock-On bypass precedes semi checks; ordinary always-hit moves do not bypass semi states')
    for label, marker, multiplier in (
        ('BattleScript_EffectGust', 'HITMARKER_IGNORE_ON_AIR', True),
        ('BattleScript_EffectTwister', 'HITMARKER_IGNORE_ON_AIR', True),
        ('BattleScript_EffectThunder', 'HITMARKER_IGNORE_ON_AIR', False),
        ('BattleScript_EffectSkyUppercut', 'HITMARKER_IGNORE_ON_AIR', False),
        ('BattleScript_HitsAllWithUndergroundBonusLoop', 'HITMARKER_IGNORE_UNDERGROUND', True),
        ('BattleScript_EffectHit', 'HITMARKER_IGNORE_UNDERWATER', True),
        ('BattleScript_EffectTrap', 'HITMARKER_IGNORE_UNDERWATER', True)):
        body = '\n'.join(commands(scripts, label))
        assert marker in body and ('setbyte sDMG_MULTIPLIER, 2' in body) == multiplier, label
    assert 'goto BattleScript_HitsAllWithUndergroundBonusLoop' in commands(scripts, 'BattleScript_EffectMagnitude')
    checks.append('Exact air/underground/underwater exception flags and double-damage script commands')
    target = block(main, r'^static void HandleAction_UseMove\(void\)\n')
    ordered(target, ['STATUS2_MULTIPLETURNS', 'gLockedMoves[gBattlerAttacker]', '// choose target', 'followmeTimer'])
    tail = target[target.rindex('gBattlerTarget = *(gBattleStruct->moveTarget + gBattlerAttacker);'):]
    ordered(tail, ['gAbsentBattlerFlags & gBitTable[gBattlerTarget]', 'GetBattlerPosition(gBattlerTarget) ^ BIT_FLANK', 'gBattleScriptsForMoveEffects'])
    assert 'CancelMultiTurnMoves' not in tail
    checks.append('Locked release uses normal slot targeting/Follow Me; absent slot retargets before release script, no vacancy retention branch')
    order = block(main, r'^u8 GetWhoStrikesFirst\(u8 battler1, u8 battler2, bool8 ignoreChosenMoves\)\n')
    assert 'gBattleMons[battler1].moves[*(gBattleStruct->chosenMovePositions + battler1)]' in order
    assert 'gLockedMoves' not in order and 'gBattleMoves[moveBattler1].priority' in order
    checks.append('Priority follows current move at retained chosen slot; execution separately follows locked move')
    metadata = {}
    expected = {'FLY':(70,95,15), 'BOUNCE':(85,85,5), 'DIG':(60,100,10), 'DIVE':(60,100,10), 'SOLAR_BEAM':(120,100,10), 'RAZOR_WIND':(80,100,10), 'SKY_ATTACK':(140,90,5), 'SKULL_BASH':(100,100,15)}
    for name in NAMES:
        fields = dict(re.findall(r'\.(\w+) = ([^,]+),', block(c['src/data/battle_moves.h'], rf'^    \[MOVE_{name}\] =')))
        assert tuple(int(fields[k]) for k in ('power','accuracy','pp')) == expected[name]
        assert fields['priority'] == '0'
        assert fields['target'] == ('MOVE_TARGET_BOTH' if name == 'RAZOR_WIND' else 'MOVE_TARGET_SELECTED')
        metadata[name] = fields
    checks.append('All eight exact metadata: power, accuracy, PP, priority zero; Razor Wind both foes, others selected')
    return checks, metadata


def validate(rows):
    assert len(rows) == 26
    assert {r['move'] for r in rows if r['kind']=='lifecycle'} == {13,19,76,91,130,143,291,340}
    for r in rows:
        if r['kind']=='lifecycle':
            assert r['twoTurn']==r['chargingTurn']==1 and r['move']==r['lockedMove']
            assert r['ppAfterCharge']==r['ppAfterRelease']==0 and r['ppWrites']==1
            assert r['releaseStatus2']==0x100000 and r['releaseStatus3']==0x400
        elif r['kind']=='pp':
            assert (r['firstPp'],r['secondPp'],r['writes']) == [(3,1,2),(3,3,1),(2,0,2),(4,3,2)][r['case']]
        elif r['kind']=='sun-on-release-pp':
            assert (r['afterCharge'],r['afterRelease'],r['writes']) == (4,3,2)
        elif r['kind']=='cancel':
            assert (r['status2'],r['status3'],r['rollout'],r['furyCutter']) == (0x100000,0x400,0,0)
        elif r['kind']=='semi-accuracy':
            assert (r['handled'],r['missed']) == [(1,1),(0,0),(1,0),(1,1)][r['mode']]
            assert r['remainingIgnore']==0
        else:
            raise AssertionError(r)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,default=default_source());p.add_argument('--vectors',type=Path,default=HERE/'source-vectors.json')
    p.add_argument('--capture',action='store_true');p.add_argument('--report',type=Path)
    a=p.parse_args();assert not a.capture or not a.vectors.exists(), 'Immutable vectors already exist'
    root=a.source.resolve();assert run(['git','rev-parse','HEAD'],cwd=root).strip()==PIN
    paths=['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/pokemon.c','src/data/battle_moves.h','data/battle_scripts_1.s','include/battle.h','include/battle_util.h']
    paths += [f'include/constants/{name}.h' for name in ('battle','battle_move_effects','battle_script_commands','pokemon','moves','abilities')]
    c={path:(root/path).read_text() for path in paths}
    for path in paths: assert c[path]==run(['git','show',f'{PIN}:{path}'],cwd=root), f'Modified pinned input: {path}'
    assert '#define ABILITYEFFECT_COUNT_OTHER_SIDE           16' in c['include/battle_util.h']
    assert '#define ABILITYEFFECT_COUNT_ON_FIELD             18' in c['include/battle_util.h']
    checks,metadata=audit(c);source=c['src/battle_script_commands.c']
    functions={name:block(source,rf'^static (?:void|bool8) {name}\([^;\n]*\)\n') for name in ('Cmd_ppreduce','Cmd_setsemiinvulnerablebit','Cmd_clearsemiinvulnerablebit','Cmd_clearstatusfromeffect','IsTwoTurnsMove','AccuracyCalcHelper')}
    functions['CancelMultiTurnMoves']=block(c['src/battle_util.c'],r'^void CancelMultiTurnMoves\(u8 battler\)\n')
    charge=source.split('            case MOVE_EFFECT_CHARGING:\n',1)[1].split('                break;',1)[0]
    assert 'gLockedMoves[gEffectBattler] = gCurrentMove;' in charge
    charge_table=re.search(r'^    \[MOVE_EFFECT_CHARGING\].*= STATUS2_MULTIPLETURNS,$',source,re.M).group()
    functions['SourceChargeCase']=charge
    unit=(HERE/'source-prefix.h').read_text()+'\n'
    unit+='static const struct MoveHost gBattleMoves[355]={\n'+',\n'.join(block(c['src/data/battle_moves.h'],rf'^    \[MOVE_{name}\] =') for name in NAMES+('POUND','AERIAL_ACE'))+'\n};\n'
    unit+='static const u32 sStatusFlagsForMoveEffects[MOVE_EFFECT_CHARGING+1]={\n'+charge_table+'\n};\n'
    unit+='\n\n'.join(value for key,value in functions.items() if key!='SourceChargeCase')
    unit+='\nstatic void SourceCharge(void) {\n'+charge+'}\n'+(HERE/'source-main.c').read_text()
    with tempfile.TemporaryDirectory(prefix='firered-charge-probe-') as tmp:
        tmp=Path(tmp);(tmp/'probe.c').write_text(unit);outputs=[]
        for label,flags in (('optimized',['-O2']),('ubsan',['-O1','-g','-fsanitize=undefined','-fno-sanitize-recover=all'])):
            run([os.environ.get('CC','cc'),'-std=c11','-Wall','-Wextra','-Werror',*flags,'-I',str(root/'include'),str(tmp/'probe.c'),'-o',str(tmp/label)])
            outputs.append(run([str(tmp/label)]))
    assert outputs[0]==outputs[1], 'Optimized/UBSan divergence'
    rows=[json.loads(line) for line in outputs[0].splitlines()];validate(rows)
    snap={'schema':'firered-charge-source-vectors-v1','sourceCommit':PIN,'sourceFiles':{path:sha(c[path]) for path in paths},'extractedFunctions':{name:sha(body) for name,body in functions.items()},'scriptAssertions':checks,'moves':metadata,'vectors':rows}
    if a.capture:
        with a.vectors.open('x') as f:json.dump(snap,f,indent=2);f.write('\n')
    else:assert snap==json.loads(a.vectors.read_text()), 'Immutable source vector mismatch'
    report={'passed':True,'mode':'capture' if a.capture else 'verify','sourceCommit':PIN,'vectors':len(rows),'scriptAssertions':len(checks),'extractedFunctions':len(functions),'builds':['-O2','-O1 -fsanitize=undefined -fno-sanitize-recover=all'],'vectorsSha256':hashlib.sha256(a.vectors.read_bytes()).hexdigest()}
    if a.report:a.report.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))


if __name__=='__main__': main()
