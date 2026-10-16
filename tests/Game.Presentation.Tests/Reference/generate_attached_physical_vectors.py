#!/usr/bin/env python3
"""Extract pinned FireRed attached-sprite callbacks into a bounded C harness.

Only authored harness code and extracted functions/data are compiled; no upstream
scripts or product binaries are run. No product output supplies expected values.
An inactive row retains the last visible fields rather than emulating slot reset.
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
COLUMNS = ('tick', 'x', 'y', 'mutatedArg1', 'tile', 'hflip', 'vflip',
           'sourceXScale', 'sourceYScale', 'matrixA', 'matrixD', 'active',
           'animEnded', 'affineAnimEnded', 'callback', 'animDelay',
           'affineDelay', 'animCmdIndex', 'affineCmdIndex')


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


def build(reference, helper):
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('reference_c', helper)
    ref = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ref)
    ref.verify_reference(reference)
    sprite = (reference / 'src/sprite.c').read_text()
    effects = (reference / 'src/battle_anim_effects_2.c').read_text()
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    header = (reference / 'include/sprite.h').read_text()
    # Preserve original command/state widths and initializer macros.
    commands = header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
    names = ['AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame',
             'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop',
             'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'DecrementAnimDelayCounter',
             'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay',
             'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop',
             'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end',
             'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum',
             'SetSpriteOamFlipBits', 'AffineAnimStateRestartAnim',
             'AffineAnimStateStartAnim', 'AffineAnimStateReset',
             'ApplyAffineAnimFrameAbsolute', 'DecrementAffineAnimDelayCounter',
             'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam',
             'GetAffineAnimFrame', 'ApplyAffineAnimFrame']
    functions = [function(sprite, name) for name in names]
    functions += [function(mons, name) for name in (
        'InitSpritePosToAnimAttacker', 'SetAnimSpriteInitialXOffset',
        'RunStoredCallbackWhenAffineAnimEnds')]
    functions += [function(effects, name) for name in (
        'AnimEyeSparkle', 'AnimEyeSparkle_Step', 'AnimAngerMark')]
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
#define ST_OAM_AFFINE_ON_MASK 1
#define B_SIDE_PLAYER 0
#define B_SIDE_OPPONENT 1
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define SAFE_DIV(a,b) ((b) ? (a)/(b) : 0)
#define ANIM_END 0xFFFF
#define AFFINE_ANIM_END 0x7FFF
#define sAnchorX data[6]
#define sAnchorY data[7]
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
    s16 x, y, data[8];
    int sheetTileStart, images;
    const union AnimCmd *const *anims;
    const union AffineAnimCmd *const *affineAnims;
    struct {u8 affineMode, matrixNum; int tileNum;} oam;
    SpriteCallback callback, storedCallback;
};
struct OamMatrix {s16 a, b, c, d;};
struct ObjAffineSrcData {s16 xScale, yScale; u16 rotation;};
static struct AffineAnimState sAffineAnimStates[1];
static struct OamMatrix gOamMatrices[1];
static bool8 gAffineAnimsDisabled;
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static s16 gBattleAnimArgs[8];
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
// Host callback pointers cannot be packed into two original 16-bit data slots.
// Storage is adapted; the original wait callback still controls the transition.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {s->storedCallback = cb;}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void DestroySpriteAndMatrix(struct Sprite *s) {s->inUse = FALSE;}
static void RequestSpriteFrameImageCopy(int image, int tile, int images) {abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite *s, s32 x, s32 y) {abort();}
// This family has zero rotation. ConvertScaleParam has already calculated the
// reciprocal 8.8 values, so the BIOS's zero-angle result is exactly A/D here.
static void ObjAffineSet(const struct ObjAffineSrcData *src, struct OamMatrix *dst, int count, int stride) {
    assert(src->rotation == 0 && count == 1 && stride == 2);
    dst->a = src->xScale; dst->b = 0; dst->c = 0; dst->d = src->yScale;
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
    harness += declaration(effects, 'sEyeSparkleAnimCmds') + '\n'
    harness += declaration(effects, 'sAngerMarkAffineAnimCmds') + '\n'
    harness += declaration(sprite, 'sDummyAnim') + '\n'
    harness += declaration(sprite, 'sDummyAffineAnim') + '\n'
    harness += r'''
static const union AnimCmd *const eyeAnims[] = {sEyeSparkleAnimCmds};
static const union AnimCmd *const dummyAnims[] = {&sDummyAnim};
static const union AffineAnimCmd *const angerAnims[] = {sAngerMarkAffineAnimCmds};
static const union AffineAnimCmd *const dummyAffineAnims[] = {&sDummyAffineAnim};
static void emit(int family, int participant, int tick, const struct Sprite *s) {
    const struct AffineAnimState *a = &sAffineAnimStates[0];
    int callback = s->callback == AnimEyeSparkle_Step ? 0 :
                   s->callback == RunStoredCallbackWhenAffineAnimEnds ? 1 :
                   s->callback == DestroySpriteAndMatrix ? 2 : -1;
    assert(callback >= 0);
    printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",
           family, gBattleAnimAttacker, gBattleAnimTarget, participant,
           tick, s->x, s->y, gBattleAnimArgs[1], s->oam.tileNum,
           (s->oam.matrixNum >> 3) & 1, (s->oam.matrixNum >> 4) & 1,
           a->xScale, a->yScale, gOamMatrices[0].a, gOamMatrices[0].d,
           s->inUse, s->animEnded, s->affineAnimEnded, callback,
           s->animDelayCounter, a->delayCounter, s->animCmdIndex, a->animCmdIndex);
}
static void run(int family, int actor, int target, int participant) {
    struct Sprite s;
    memset(&s, 0, sizeof(s));
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    AffineAnimStateReset(0);
    gOamMatrices[0] = (struct OamMatrix){256, 0, 0, 256};
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    s.inUse = s.usingSheet = s.animBeginning = s.affineAnimBeginning = TRUE;
    s.anims = family ? dummyAnims : eyeAnims;
    s.affineAnims = family ? angerAnims : dummyAffineAnims;
    s.oam.affineMode = family;
    if (family) {
        gBattleAnimArgs[0] = participant;
        gBattleAnimArgs[1] = 20; gBattleAnimArgs[2] = -28;
        AnimAngerMark(&s);
    } else {
        gBattleAnimArgs[0] = -16; gBattleAnimArgs[1] = -8;
        AnimEyeSparkle(&s);
    }
    AnimateSprite(&s);
    emit(family, participant, -1, &s);
    for (int tick = 0; tick < 100; tick++) {
        s.callback(&s);
        if (s.inUse) AnimateSprite(&s);
        emit(family, participant, tick, &s);
        if (!s.inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; actor++) {
        for (int relation = 1; relation <= 2; relation++) {
            int target = actor ^ relation;
            run(0, actor, target, 0);
            run(1, actor, target, 0);
            run(1, actor, target, 1);
        }
    }
}
'''
    with tempfile.TemporaryDirectory(prefix='attached-physical-c-') as tmp:
        c, exe = Path(tmp) / 'attached.c', Path(tmp) / 'attached'
        c.write_text(harness)
        ref.compile_c(['cc', '-std=gnu11', '-O0', str(c), '-o', str(exe)])
        output = subprocess.check_output([str(exe)], text=True)
    result = {'EyeSparkle': {}, 'AngerMark': {}}
    for line in output.splitlines():
        family, actor, target, participant, *row = map(int, line.split(','))
        if len(row) != len(COLUMNS):
            raise ValueError('Unexpected reference vector width')
        name = 'AngerMark' if family else 'EyeSparkle'
        key = f'{actor}:{target}:{participant}' if family else f'{actor}:{target}'
        result[name].setdefault(key, []).append(row)
    if len(result['EyeSparkle']) != 8 or len(result['AngerMark']) != 16:
        raise ValueError('Incomplete reference cases')
    for cases in result.values():
        for rows in cases.values():
            if rows[0][0] != -1 or rows[-1][11] != 0 or not all(r[11] for r in rows[:-1]):
                raise ValueError('Missing creation or terminal row')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference, args.helper), separators=(',', ':')) + '\n')
