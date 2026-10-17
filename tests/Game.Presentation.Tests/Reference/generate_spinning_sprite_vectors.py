#!/usr/bin/env python3
"""Extract pinned spinning punch/kick callbacks into a bounded, standalone C oracle.

The callback bodies, frame programs, affine programs and both sprite interpreters
are extracted verbatim from pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14.
Only authored C and these extracted bodies are compiled; upstream scripts and
product assemblies are never executed. The external BIOS ObjAffineSet adapter is
integer Q1.14, with an explicit quarter-wave table and signed shift/negation order
matching VisualBoyAdvance-M's BIOS_ObjAffineSet:
https://github.com/visualboyadvance-m/visualboyadvance-m/blob/master/src/core/gba/internal/gbaBios.cpp
No floating-point approximation or game Q8.8 sine table supplies matrix values.

Tick -1 is the creation callback plus AnimateSprite. Later ticks use the extracted
AnimateSprites callback-before-animation ordering. For D=max(0,args[3]), the
handoff tick D still spins; Finish runs on D+1 and begins affine animation zero
while paused; destruction runs on D+23. The positive-scale domain is D<=30 for
SpinningHandOrFoot and D<=62 for MegaPunchKick. Negative signed16 durations are
source-defined immediate handoffs and included. Frame selection is bounded to
0/1. Offsets in this corpus are bounded to signed8. Inactive rows retain last
visible values instead of simulating unrelated sprite-slot clearing.
"""
import argparse
import importlib.util
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
# [family, xOffset, yOffset, frame, spinDuration]; 0=Spinning, 1=Mega.
CONFIGS = []
for family, nominal, maximum in [(0, 30, 30), (1, 50, 62)]:
    for frame in (0, 1):
        CONFIGS += [[family, 0, 0, frame, nominal],
                    [family, 20, -12, frame, nominal],
                    [family, -20, 12, frame, nominal],
                    [family, -128, 127, frame, 0],
                    [family, 127, -128, frame, 1],
                    [family, 9, -7, frame, maximum],
                    [family, 0, 0, frame, -1],
                    [family, 0, 0, frame, -32768]]
COLUMNS = [
    'tick', 'x', 'y', 'x2', 'y2', 'tile', 'selectedFrame',
    'sourceXScale', 'sourceYScale', 'rotation',
    'matrixA', 'matrixB', 'matrixC', 'matrixD',
    'active', 'callback', 'storedCallback', 'remaining', 'affinePaused',
    'animEnded', 'affineAnimEnded', 'animCmdIndex', 'affineCmdIndex',
    'animDelay', 'affineDelay', 'animBeginning', 'affineBeginning',
    'invokedCallback', 'matrixWrites', 'matrixObservedTile', 'matrixObservedPaused',
]
QUARTER = [
    0, 402, 803, 1205, 1605, 2005, 2404, 2801, 3196, 3589, 3980,
    4369, 4756, 5139, 5519, 5896, 6269, 6639, 7005, 7366, 7723,
    8075, 8423, 8765, 9102, 9434, 9759, 10079, 10393, 10701,
    11002, 11297, 11585, 11866, 12139, 12406, 12665, 12916, 13159,
    13395, 13622, 13842, 14053, 14255, 14449, 14634, 14810, 14978,
    15136, 15286, 15426, 15557, 15678, 15790, 15892, 15985, 16069,
    16142, 16206, 16260, 16305, 16339, 16364, 16379, 16384,
]


def function(source, name):
    match = re.search(r'^(?:static )?(?:void|s16|u8|bool8) ' + name +
                      r'\([^;]+?\)\n\{', source, re.M)
    if not match:
        raise ValueError('Missing original function ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


def declaration(source, name):
    match = re.search(r'^static const union (?:AffineAnimCmd|AnimCmd) ' +
                      name + r'(?:\[\])?\s*=\s*\{.*?\};', source, re.M | re.S)
    if not match:
        raise ValueError('Missing original animation ' + name)
    return match[0]


def sine(angle):
    angle &= 255
    quadrant, index = angle >> 6, angle & 63
    magnitude = QUARTER[64 - index if quadrant & 1 else index]
    return magnitude if quadrant < 2 else -magnitude


def validate(cases):
    if len(cases) != 8 * len(CONFIGS):
        raise ValueError('Incomplete actor/target/config coverage')
    for key, rows in cases.items():
        actor, target, config = map(int, key.split(':'))
        family, x, y, frame, supplied_duration = CONFIGS[config]
        duration = max(0, supplied_duration)
        delta = 4 if family else 8
        x_coords, y_coords = [32, 200, 90, 152], [88, 40, 96, 32]
        expected_x = x_coords[target] + (x if x_coords[actor] < x_coords[target] else -x)
        if target not in (actor ^ 1, actor ^ 2):
            raise ValueError('Unexpected target relation')
        if [row[0] for row in rows] != list(range(-1, duration + 24)):
            raise ValueError('Unexpected lifecycle duration: ' + key)
        for row in rows:
            values = dict(zip(COLUMNS, row))
            tick = values['tick']
            finished = tick >= duration + 1
            steps = 0 if finished else tick + 1
            scale = 256 - delta * steps
            rotation = ((steps * 20) & 255) << 8
            angle = rotation >> 8
            reciprocal = 65536 // scale
            expected_matrix = [
                reciprocal * sine(angle + 64) >> 14,
                -(reciprocal * sine(angle) >> 14),
                reciprocal * sine(angle) >> 14,
                reciprocal * sine(angle + 64) >> 14,
            ]
            expected_callback = 2 if tick >= duration + 22 else 1 if tick == duration else 0
            expected_invoked = (3 if tick == -1 else 1 if tick == duration + 1
                                else 2 if tick == duration + 23 else 0)
            expected_remaining = (supplied_duration if tick == -1 else
                                  max(0, duration + 21 - tick) if finished else
                                  max(0, supplied_duration - tick - 1))
            if supplied_duration < 0 and tick == 0:
                expected_remaining = supplied_duration
            if (row[1:7] != [expected_x, y_coords[target] + y, 0, 0, 16 * frame, frame]
                    or row[7:10] != [scale, scale, rotation]
                    or row[10:14] != expected_matrix
                    or values['active'] != (tick < duration + 23)
                    or values['callback'] != expected_callback
                    or values['storedCallback'] != (2 if finished else 1)
                    or values['remaining'] != expected_remaining
                    or values['affinePaused'] != finished
                    or values['animEnded'] != (tick >= 0)
                    or values['affineAnimEnded'] != 0
                    or values['animCmdIndex'] != 0
                    or values['affineCmdIndex'] != (0 if finished or tick == -1 else 1)
                    or row[23:27] != [0, 0, 0, 0]
                    or values['invokedCallback'] != expected_invoked
                    or values['matrixWrites'] != (tick <= duration + 1)
                    or values['matrixObservedTile'] != 16 * frame
                    or values['matrixObservedPaused'] != finished):
                raise ValueError('Unexpected callback/frame/affine ordering: ' + key + ' tick ' + str(tick))
            if scale <= 0:
                raise ValueError('A fixture reached forbidden zero/negative scale')
        if rows[-1][1:14] != rows[-2][1:14]:
            raise ValueError('Destruction advanced visible state')


def build(reference, helper):
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('reference_c', helper)
    ref = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ref)
    ref.verify_reference(reference)
    sprite = (reference / 'src/sprite.c').read_text()
    fight = (reference / 'src/battle_anim_fight.c').read_text()
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    header = (reference / 'include/sprite.h').read_text()
    commands = header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
    names = ['AnimateSprites', 'AnimateSprite', 'BeginAnim', 'ContinueAnim',
             'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop',
             'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop',
             'DecrementAnimDelayCounter', 'BeginAffineAnim', 'ContinueAffineAnim',
             'AffineAnimDelay', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop',
             'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop',
             'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame',
             'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits',
             'AffineAnimStateRestartAnim', 'AffineAnimStateStartAnim',
             'AffineAnimStateReset', 'ApplyAffineAnimFrameAbsolute',
             'DecrementAffineAnimDelayCounter',
             'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam',
             'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'StartSpriteAnim',
             'StartSpriteAffineAnim']
    functions = [function(sprite, name) for name in names]
    functions += [function(mons, name) for name in (
        'InitSpritePosToAnimTarget', 'SetAnimSpriteInitialXOffset', 'WaitAnimForDuration')]
    functions += [function(fight, name) for name in (
        'AnimSpinningKickOrPunch', 'AnimSpinningKickOrPunchFinish')]
    harness = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int16_t s16; typedef int32_t s32; typedef uint8_t bool8;
#define FALSE 0
#define TRUE 1
#define MAX_SPRITES 1
#define ST_OAM_AFFINE_ON_MASK 1
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
// Every admitted case avoids zero; fail rather than inventing its matrix.
#define SAFE_DIV(a,b) (assert((b) != 0), (a)/(b))
#define ANIM_END 0xFFFF
#define AFFINE_ANIM_END 0x7FFF
#define sAnchorX data[6]
#define sAnchorY data[7]
_Static_assert((-1 >> 1) == -1, "BIOS adapter needs arithmetic right shift");
'''
    harness += commands + r'''
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    u8 animCmdIndex, animLoopCounter, animNum;
    u8 animDelayCounter:6;
    bool8 animEnded, animBeginning, animPaused, usingSheet, hFlip, vFlip;
    bool8 affineAnimBeginning, affineAnimEnded, affineAnimPaused, anchored;
    bool8 inUse;
    s16 x, y, x2, y2, data[8];
    int sheetTileStart, images;
    const union AnimCmd *const *anims;
    const union AffineAnimCmd *const *affineAnims;
    struct {u8 affineMode, matrixNum; int tileNum;} oam;
    SpriteCallback callback, storedCallback;
};
struct OamMatrix {s16 a, b, c, d;};
struct ObjAffineSrcData {s16 xScale, yScale; u16 rotation;};
static struct Sprite gSprites[MAX_SPRITES];
static struct AffineAnimState sAffineAnimStates[1];
static struct OamMatrix gOamMatrices[1];
static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static s16 gBattleAnimArgs[8];
static int matrixWrites, matrixObservedTile, matrixObservedPaused;
static const s16 sX[] = {32, 200, 90, 152};
static const s16 sY[] = {88, 40, 96, 32};
static u8 GetBattlerSide(u8 battler) {assert(battler < 4); return battler & 1;}
static s16 GetBattlerSpriteCoord(u8 battler, u8 coord) {
    assert(battler < 4 && coord < 4);
    return (coord == BATTLER_COORD_X || coord == BATTLER_COORD_X_2) ? sX[battler] : sY[battler];
}
static s16 GetBattlerSpriteCoord2(u8 battler, u8 coord) {
    return GetBattlerSpriteCoord(battler, coord);
}
// Host callback addresses cannot fit the two source data words. Only pointer
// storage is adapted; extracted WaitAnimForDuration still owns all scheduling.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {s->storedCallback = cb;}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void RequestSpriteFrameImageCopy(int image, int tile, int images) {abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite *s, s32 x, s32 y) {abort();}
'''
    harness += 'static const s16 quarter[] = {' + ','.join(map(str, QUARTER)) + '};\n'
    harness += r'''
static s16 sine(u8 angle) {
    int quadrant = angle >> 6, index = angle & 63;
    int magnitude = quarter[(quadrant & 1) ? 64 - index : index];
    return quadrant < 2 ? magnitude : -magnitude;
}
// Integer BIOS Q1.14 products are arithmetic-shifted before B is negated.
static void ObjAffineSet(const struct ObjAffineSrcData *src, struct OamMatrix *dst, int count, int stride) {
    assert(count == 1 && stride == 2);
    u8 angle = src->rotation >> 8;
    int sn = sine(angle), cs = sine(angle + 64);
    dst->a = (src->xScale * cs) >> 14;
    dst->b = -((src->xScale * sn) >> 14);
    dst->c = (src->yScale * sn) >> 14;
    dst->d = (src->yScale * cs) >> 14;
    matrixWrites++;
    matrixObservedTile = gSprites[0].oam.tileNum;
    matrixObservedPaused = gSprites[0].affineAnimPaused;
}
'''
    harness += '\n'.join(f.split('\n{', 1)[0] + ';' for f in functions) + '\n'
    harness += r'''
static void (*sAnimFuncs[])(struct Sprite *) = {ContinueAnim, BeginAnim};
static void (*sAffineAnimFuncs[])(struct Sprite *) = {ContinueAffineAnim, BeginAffineAnim};
static void (*sAnimCmdFuncs[])(struct Sprite *) = {AnimCmd_loop, AnimCmd_jump, AnimCmd_end, AnimCmd_frame};
static void (*sAffineAnimCmdFuncs[])(u8, struct Sprite *) = {AffineAnimCmd_loop, AffineAnimCmd_jump, AffineAnimCmd_end, AffineAnimCmd_frame};
'''
    harness += '\n'.join(functions) + '\n'
    for name in ('sAnim_Fist', 'sAnim_FootWide', 'sAffineAnim_SpinningHandOrFoot', 'sAffineAnim_MegaPunchKick'):
        harness += declaration(fight, name) + '\n'
    harness += r'''
static const union AnimCmd *const handsAndFeet[] = {sAnim_Fist, sAnim_FootWide};
static const union AffineAnimCmd *const spinningAnims[] = {sAffineAnim_SpinningHandOrFoot};
static const union AffineAnimCmd *const megaAnims[] = {sAffineAnim_MegaPunchKick};
'''
    harness += 'static const s16 configs[][5] = {' + ','.join(
        '{' + ','.join(map(str, args)) + '}' for args in CONFIGS) + '};\n'
    harness += r'''
static int callbackCode(SpriteCallback callback) {
    if (callback == WaitAnimForDuration) return 0;
    if (callback == AnimSpinningKickOrPunchFinish) return 1;
    if (callback == DestroyAnimSprite) return 2;
    if (callback == AnimSpinningKickOrPunch) return 3;
    abort();
}
static void emit(int config, int tick, int invokedCallback) {
    const struct Sprite *s = &gSprites[0];
    const struct AffineAnimState *a = &sAffineAnimStates[0];
    const struct OamMatrix *m = &gOamMatrices[0];
    printf("%d,%d,%d", gBattleAnimAttacker, gBattleAnimTarget, config);
    const int row[] = {
        tick, s->x, s->y, s->x2, s->y2, s->oam.tileNum, s->animNum,
        a->xScale, a->yScale, a->rotation, m->a, m->b, m->c, m->d,
        s->inUse, callbackCode(s->callback), callbackCode(s->storedCallback),
        s->data[0], s->affineAnimPaused, s->animEnded, s->affineAnimEnded,
        s->animCmdIndex, a->animCmdIndex, s->animDelayCounter, a->delayCounter,
        s->animBeginning, s->affineAnimBeginning, invokedCallback,
        matrixWrites, matrixObservedTile, matrixObservedPaused,
    };
    for (unsigned i = 0; i < sizeof(row) / sizeof(row[0]); i++) printf(",%d", row[i]);
    printf("\n");
}
static void run(int actor, int target, int config) {
    struct Sprite *s = &gSprites[0];
    memset(gSprites, 0, sizeof(gSprites));
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    memcpy(gBattleAnimArgs, configs[config] + 1, 4 * sizeof(s16));
    AffineAnimStateReset(0);
    gOamMatrices[0] = (struct OamMatrix){256, 0, 0, 256};
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    s->inUse = s->usingSheet = s->animBeginning = s->affineAnimBeginning = TRUE;
    s->anims = handsAndFeet;
    s->affineAnims = configs[config][0] ? megaAnims : spinningAnims;
    s->oam.affineMode = 3;
    // Cmd_createsprite always supplies target pic center; ANIM_ATTACKER or
    // ANIM_TARGET in the script selects priority, not initial coordinates.
    s->x = sX[target]; s->y = sY[target];
    s->callback = AnimSpinningKickOrPunch;
    matrixWrites = 0; matrixObservedTile = matrixObservedPaused = -1;
    // Exactly the callback/AnimateSprite order from CreateSpriteAndAnimate.
    AnimateSprites();
    emit(config, -1, 3);
    for (int tick = 0; tick < 100; tick++) {
        int invoked = callbackCode(s->callback);
        matrixWrites = 0;
        AnimateSprites();
        emit(config, tick, invoked);
        if (!s->inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; actor++)
        for (int relation = 1; relation <= 2; relation++)
            for (unsigned config = 0; config < sizeof(configs) / sizeof(configs[0]); config++)
                run(actor, actor ^ relation, config);
}
'''
    with tempfile.TemporaryDirectory(prefix='spinning-sprite-c-') as tmp:
        c, exe = Path(tmp) / 'spinning.c', Path(tmp) / 'spinning'
        c.write_text(harness)
        ref.compile_c(['cc', '-std=gnu11', '-O0', str(c), '-o', str(exe)])
        output = subprocess.check_output([str(exe)], text=True)
    cases = {}
    for line in output.splitlines():
        actor, target, config, *row = map(int, line.split(','))
        if len(row) != len(COLUMNS):
            raise ValueError('Unexpected reference vector width')
        cases.setdefault(f'{actor}:{target}:{config}', []).append(row)
    validate(cases)
    return {'columns': COLUMNS, 'configColumns': ['family', 'xOffset', 'yOffset', 'frame', 'spinDuration'],
            'families': ['SpinningHandOrFoot', 'MegaPunchKick'],
            'callbacks': ['WaitAnimForDuration', 'AnimSpinningKickOrPunchFinish',
                          'DestroyAnimSprite', 'AnimSpinningKickOrPunch'],
            'configs': CONFIGS, 'cases': cases}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference, args.helper), separators=(',', ':')) + '\n')
