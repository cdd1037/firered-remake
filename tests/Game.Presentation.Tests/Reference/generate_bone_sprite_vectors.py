#!/usr/bin/env python3
"""Extract the pinned Bone Club/Bone Rush sprite callbacks into bounded C.

All callback, fixed-point movement, frame and affine-interpreter bodies come
verbatim from pret/pokefirered, never from the product implementation. The GBA
BIOS is external to that repository: ObjAffineSet uses the same explicit Q1.14
adapter as generate_rotation_task_vectors.py. The inactive row retains the last
visible state rather than emulating destruction's unrelated sprite-slot reset.
Only the authored/extracted harness is compiled; no upstream scripts are run.
"""
import argparse
import importlib.util
import json
import math
import re
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CONFIGS = [[-42, -25, 0, 0, 15], [-42, -25, 13, 0, 15],
           [-42, -25, 0, 0, 1], [-42, -25, 13, 0, 1]]
COLUMNS = ['tick', 'x', 'y', 'x2', 'y2', 'screenX', 'screenY',
           'mutatedArg2', 'rotation', 'matrixA', 'matrixB', 'matrixC',
           'matrixD', 'active', 'callback', 'remainingSteps', 'data1',
           'data2', 'data3', 'data4', 'sourceXScale', 'sourceYScale',
           'animEnded', 'affineAnimEnded', 'animCmdIndex', 'affineCmdIndex',
           'animDelay', 'affineDelay']


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
    ground = (reference / 'src/battle_anim_ground.c').read_text()
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    header = (reference / 'include/sprite.h').read_text()
    battle_header = (reference / 'include/battle_anim.h').read_text()
    commands = header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]
    translation_macros = '\n'.join(re.findall(r'^#define sTransl_.*$', battle_header, re.M))
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
             'GetAffineAnimFrame', 'ApplyAffineAnimFrame']
    functions = [function(sprite, name) for name in names]
    functions += [function(mons, name) for name in (
        'InitSpritePosToAnimTarget', 'SetAnimSpriteInitialXOffset',
        'InitAnimLinearTranslation', 'StartAnimLinearTranslation',
        'AnimTranslateLinear', 'AnimTranslateLinear_WithFollowup')]
    functions += [function(ground, 'AnimBoneHitProjectile')]
    # BIOS has a 256-entry Q1.14 table; quadrant reduction avoids floating-point
    # differences around exact zero/quarter-turn boundaries.
    quarter = [int(math.sin(i * math.pi / 128) * 16384) for i in range(65)]
    quarter[0], quarter[64] = 0, 16384
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
#define SAFE_DIV(a,b) ((b) ? (a)/(b) : 0)
#define ANIM_END 0xFFFF
#define AFFINE_ANIM_END 0x7FFF
#define sAnchorX data[6]
#define sAnchorY data[7]
'''
    harness += translation_macros + '\n' + commands + r'''
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
// Host pointers cannot be packed into the original two 16-bit sprite data slots.
// Only pointer storage is adapted; the extracted callback chooses the transition.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {s->storedCallback = cb;}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void RequestSpriteFrameImageCopy(int image, int tile, int images) {abort();}
static void UpdateSpriteMatrixAnchorPos(struct Sprite *s, s32 x, s32 y) {abort();}
'''
    harness += 'static const s16 quarter[] = {' + ','.join(map(str, quarter)) + '};\n'
    harness += r'''
static s16 sine(u8 angle) {
    int q = angle >> 6, i = angle & 63;
    int value = quarter[(q & 1) ? 64 - i : i];
    return q < 2 ? value : -value;
}
// GBA BIOS is external to the pinned repository. This bounded adapter matches
// the established rotation-task fixture's Q1.14 sampling and signed shifts.
static void ObjAffineSet(const struct ObjAffineSrcData *src, struct OamMatrix *dst, int count, int stride) {
    assert(count == 1 && stride == 2);
    assert(src->xScale == 256 && src->yScale == 256);
    u8 angle = src->rotation >> 8;
    int s = sine(angle), c = sine(angle + 64);
    dst->a = (src->xScale * c) >> 14;
    dst->b = -((src->xScale * s) >> 14);
    dst->c = (src->yScale * s) >> 14;
    dst->d = (src->yScale * c) >> 14;
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
    harness += declaration(ground, 'sAffineAnim_SpinningBone') + '\n'
    harness += declaration(sprite, 'sDummyAnim') + '\n'
    harness += r'''
static const union AnimCmd *const dummyAnims[] = {&sDummyAnim};
static const union AffineAnimCmd *const boneAnims[] = {sAffineAnim_SpinningBone};
'''
    harness += 'static const s16 configs[][5] = {' + ','.join(
        '{' + ','.join(map(str, args)) + '}' for args in CONFIGS) + '};\n'
    harness += r'''
static void emit(int config, int tick) {
    const struct Sprite *s = &gSprites[0];
    const struct AffineAnimState *a = &sAffineAnimStates[0];
    const struct OamMatrix *m = &gOamMatrices[0];
    int callback = s->callback == StartAnimLinearTranslation ? 0 :
                   s->callback == AnimTranslateLinear_WithFollowup ? 1 :
                   s->callback == DestroyAnimSprite ? 2 : -1;
    assert(callback >= 0);
    printf("%d,%d,%d", gBattleAnimAttacker, gBattleAnimTarget, config);
    const int row[] = {
        tick, s->x, s->y, s->x2, s->y2, s->x + s->x2, s->y + s->y2,
        gBattleAnimArgs[2], a->rotation, m->a, m->b, m->c, m->d, s->inUse,
        callback, s->data[0], s->data[1], s->data[2], s->data[3], s->data[4],
        a->xScale, a->yScale, s->animEnded, s->affineAnimEnded,
        s->animCmdIndex, a->animCmdIndex, s->animDelayCounter, a->delayCounter
    };
    for (unsigned i = 0; i < sizeof(row) / sizeof(row[0]); i++) printf(",%d", row[i]);
    printf("\n");
}
static void run(int actor, int target, int config) {
    struct Sprite *s = &gSprites[0];
    memset(gSprites, 0, sizeof(gSprites));
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    memcpy(gBattleAnimArgs, configs[config], sizeof(configs[config]));
    AffineAnimStateReset(0);
    gOamMatrices[0] = (struct OamMatrix){256, 0, 0, 256};
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    s->inUse = s->usingSheet = s->animBeginning = s->affineAnimBeginning = TRUE;
    s->anims = dummyAnims;
    s->affineAnims = boneAnims;
    s->oam.affineMode = 1;
    // Battle animation creation supplies target center before callback execution.
    s->x = sX[target]; s->y = sY[target];
    s->callback = AnimBoneHitProjectile;
    AnimateSprites();
    emit(config, -1);
    for (int tick = 0; tick < 100; tick++) {
        AnimateSprites();
        emit(config, tick);
        if (!s->inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; actor++)
        for (int relation = 1; relation <= 2; relation++)
            for (int config = 0; config < 4; config++)
                run(actor, actor ^ relation, config);
}
'''
    with tempfile.TemporaryDirectory(prefix='bone-sprite-c-') as tmp:
        c, exe = Path(tmp) / 'bone.c', Path(tmp) / 'bone'
        c.write_text(harness)
        ref.compile_c(['cc', '-std=gnu11', '-O0', str(c), '-o', str(exe)])
        output = subprocess.check_output([str(exe)], text=True)
    cases = {}
    for line in output.splitlines():
        actor, target, config, *row = map(int, line.split(','))
        if len(row) != len(COLUMNS):
            raise ValueError('Unexpected reference vector width')
        cases.setdefault(f'{actor}:{target}:{config}', []).append(row)
    if len(cases) != 32:
        raise ValueError('Incomplete reference cases')
    for key, rows in cases.items():
        actor, _, config = map(int, key.split(':'))
        duration = CONFIGS[config][4]
        if (rows[0][0] != -1 or rows[-1][0] != duration + 1
                or len(rows) != duration + 3 or rows[-1][13] != 0
                or not all(r[13] for r in rows[:-1])):
            raise ValueError('Missing creation, movement, transition or terminal row')
        if (rows[0][3:5] != [0, 0] or rows[0][14] != 0
                or rows[duration][14] != 1 or rows[duration + 1][14] != 2
                or rows[-1][14] != 2):
            raise ValueError('Unexpected callback transition timing')
        expected_arg2 = CONFIGS[config][2] * (-1 if actor & 1 else 1)
        if not all(r[7] == expected_arg2 for r in rows):
            raise ValueError('Unexpected source argument mutation')
    return {
        'columns': COLUMNS,
        'configs': CONFIGS,
        'cases': cases,
    }


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference, args.helper), separators=(',', ':')) + '\n')
