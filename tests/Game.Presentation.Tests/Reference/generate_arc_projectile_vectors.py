#!/usr/bin/env python3
"""Run the pinned missile-arc callbacks in an extracted, bounded C oracle.

AnimMissileArc, its step callback, initial positioning, arc/linear movement,
rotation assignment, Sin, and the source Q8.8 sine table are extracted verbatim
from pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14. Only external
BIOS angles/matrices and minimal battle/sprite services are adapted. No product
implementation or upstream build scripts are executed. The inactive final row
retains the last sprite state instead of modeling unrelated slot destruction.

The step callback deliberately keeps the second translation's look-ahead x2/y2,
while restoring all eight data words only when that second translation succeeds.
The first completed translation destroys immediately. Initial matrix state is
explicit dummy-affine identity; duration-one cases never set a missile rotation.
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
CONFIGS = [
    [20, -8, -8, -8, 20, -32],
    [20, -8, 8, 8, 20, -40],
    [20, -8, 0, 0, 20, -32],
    [20, -8, -8, -8, 1, -32],
    [20, -8, -8, -8, 2, -32],
    [20, -8, -8, -8, 255, -32],
    [20, -8, 8, 8, 20, 40],
    [20, -8, 0, 0, 2, 32],
    [0, 0, 0, 0, 4, 0],
    # Actor 0 -> target 1 produces |delta X| and |delta Y| 255/256/257.
    [0, 100, 87, -107, 4, -32],
    [0, 100, 88, -108, 4, -32],
    [0, 100, 89, -109, 4, -32],
    [-128, 127, 127, -128, 4, -128],
    [127, -128, -128, 127, 4, 127],
    [127, 127, 127, 127, 2, 127],
    [-128, -128, -128, -128, 1, -128],
]
COLUMNS = [
    'tick', 'x', 'y', 'x2', 'y2', 'arg0', 'arg1', 'arg2', 'arg3',
    'arg4', 'arg5', 'rotation', 'matrixA', 'matrixB', 'matrixC', 'matrixD',
    'active', 'invisible', 'remaining', 'angle', 'velocityX', 'velocityY',
    'fractionX', 'fractionY', 'phaseStep', 'affinePaused',
]


def function(source, name):
    match = re.search(r'^(?:static )?(?:void|s16|u16|u8|bool8) ' + name +
                      r'\([^;]+?\)\n\{', source, re.M)
    if not match:
        raise ValueError('Missing original function ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


def signed16(value):
    return ((value + 32768) & 65535) - 32768


def validate(cases):
    if len(cases) != 8 * len(CONFIGS):
        raise ValueError('Incomplete actor/target/config coverage')
    if any(not all(-128 <= args[i] <= 127 for i in (0, 1, 2, 3, 5))
           or not 1 <= args[4] <= 255 for args in CONFIGS):
        raise ValueError('Configuration outside admitted source argument bounds')
    for key, rows in cases.items():
        actor, target, config = map(int, key.split(':'))
        args = CONFIGS[config]
        duration = args[4]
        if (target not in (actor ^ 1, actor ^ 2)
                or len(rows) != duration + 2
                or [row[0] for row in rows] != list(range(-1, duration + 1))
                or rows[-1][16] != 0
                or any(row[16] != 1 for row in rows[:-1])):
            raise ValueError('Unexpected immediate-destruction lifecycle: ' + key)
        if (rows[0][3:5] != [0, 0] or rows[0][11:16] != [0, 256, 0, 0, 256]
                or rows[0][17] != 1 or rows[0][25] != 0
                or any(row[17] != 0 for row in rows[1:])):
            raise ValueError('Unexpected invisible/identity initialization: ' + key)
        expected_args = args.copy()
        if actor & 1:
            expected_args[2] = signed16(-expected_args[2])
        if any(row[5:11] != expected_args for row in rows):
            raise ValueError('Unexpected argument mutation: ' + key)
        step = signed16(32768 // duration)
        for row in rows:
            moves = min(row[0] + 1, duration)
            if (row[18] != duration - moves or row[19] != signed16(moves * step)
                    or row[24] != step
                    or row[20:22] != rows[0][20:22]
                    or row[22:24] != [(moves * row[i]) & 65535 for i in (20, 21)]):
                raise ValueError('Unexpected restored phase/data timing: ' + key)
        # Destruction never computes another position, phase, or matrix.
        if rows[-1][1:16] != rows[-2][1:16] or rows[-1][18:] != rows[-2][18:]:
            raise ValueError('Terminal callback unexpectedly advanced state: ' + key)
        if duration == 1:
            if any(row[11:16] != [0, 256, 0, 0, 256] or row[25] != 0 for row in rows):
                raise ValueError('One-step arc should retain dummy identity: ' + key)
        elif any(row[25] != 1 for row in rows[1:]):
            raise ValueError('Successful look-ahead should pause affine animation: ' + key)


def build(reference, helper, angle_adapter):
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('reference_c', helper)
    ref = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ref)
    ref.verify_reference(reference)
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    bug = (reference / 'src/battle_anim_bug.c').read_text()
    trig = (reference / 'src/trig.c').read_text()
    header = (reference / 'include/battle_anim.h').read_text()
    global_header = (reference / 'include/global.h').read_text()
    functions = [function(mons, name) for name in (
        'InitSpritePosToAnimAttacker', 'SetAnimSpriteInitialXOffset',
        'InitAnimArcTranslation', 'TranslateAnimHorizontalArc',
        'InitAnimLinearTranslation', 'AnimTranslateLinear',
        'TrySetSpriteRotScale', 'ShouldRotScaleSpeciesBeFlipped')]
    functions += [function(trig, 'Sin'), function(bug, 'AnimMissileArc'),
                  function(bug, 'AnimMissileArc_Step')]
    sine_table = re.search(r'^const s16 gSineTable\[\] =\n\{.*?\n\};', trig, re.M | re.S)
    q8_macro = re.search(r'^#define Q_8_8\(n\).*$', global_header, re.M)
    if not sine_table or not q8_macro:
        raise ValueError('Missing original Q8.8 sine table or macro')
    harness = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef int16_t s16;
typedef int32_t s32; typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Sprite; typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    s16 x, y, x2, y2, data[8];
    bool8 inUse, invisible, affineAnimPaused;
    struct {int affineMode, matrixNum, shape, size;} oam;
    SpriteCallback callback;
};
struct ObjAffineSrcData {s16 xScale, yScale; u16 rotation;};
struct OamMatrix {s16 a, b, c, d;};
static struct OamMatrix gOamMatrices[1];
static u16 sRotation;
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static s16 gBattleAnimArgs[8];
static const s16 sX[] = {32, 200, 90, 152}, sY[] = {88, 40, 96, 32};
static u8 GetBattlerSide(u8 b) {assert(b < 4); return b & 1;}
static s16 GetBattlerSpriteCoord(u8 b, u8 c) {
    assert(b < 4 && c < 4); return (c == 0 || c == 2) ? sX[b] : sY[b];
}
static s16 GetBattlerSpriteCoord2(u8 b, u8 c) {return GetBattlerSpriteCoord(b, c);}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void CalcCenterToCornerVec(struct Sprite *s, int shape, int size, int mode) {abort();}
// External BIOS Q1.14 matrix adapter, shared with the established needle oracle.
// The arc itself uses the distinct verbatim source Q8.8 sine table below.
static const s16 quarter[] = {0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384};
static s16 biosSine(u8 angle) {
    int q = angle >> 6, i = angle & 63;
    int value = quarter[(q & 1) ? 64 - i : i]; return q < 2 ? value : -value;
}
static void ObjAffineSet(const struct ObjAffineSrcData *src, struct OamMatrix *dst, int count, int stride) {
    assert(count == 1 && stride == 2);
    sRotation = src->rotation;
    u8 angle = src->rotation >> 8;
    int sn = biosSine(angle), cs = biosSine(angle + 64);
    dst->a = (src->xScale * cs) >> 14; dst->b = -((src->xScale * sn) >> 14);
    dst->c = (src->yScale * sn) >> 14; dst->d = (src->yScale * cs) >> 14;
}
'''
    harness += '\n'.join(re.findall(r'^#define sTransl_.*$', header, re.M)) + '\n'
    harness += q8_macro[0] + '\n' + sine_table[0] + '\n'
    harness += angle_adapter.read_text() + '\n'
    harness += '\n'.join(f.split('\n{', 1)[0] + ';' for f in functions) + '\n'
    harness += '\n'.join(functions) + '\n'
    harness += 'static const s16 configs[][6] = {' + ','.join(
        '{' + ','.join(map(str, args)) + '}' for args in CONFIGS) + '};\n'
    harness += r'''
static void emit(struct Sprite *s, int config, int tick) {
    const struct OamMatrix *m = &gOamMatrices[0];
    assert(s->callback == AnimMissileArc_Step);
    const int row[] = {tick, s->x, s->y, s->x2, s->y2,
        gBattleAnimArgs[0], gBattleAnimArgs[1], gBattleAnimArgs[2],
        gBattleAnimArgs[3], gBattleAnimArgs[4], gBattleAnimArgs[5],
        sRotation, m->a, m->b, m->c, m->d, s->inUse, s->invisible,
        s->data[0], s->data[7], (u16)s->data[1], (u16)s->data[2],
        (u16)s->data[3], (u16)s->data[4], s->data[6], s->affineAnimPaused};
    printf("%d:%d:%d", gBattleAnimAttacker, gBattleAnimTarget, config);
    for (unsigned i = 0; i < sizeof(row) / sizeof(row[0]); ++i) printf(",%d", row[i]);
    puts("");
}
static void run(int actor, int target, int config) {
    struct Sprite s = {0};
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    memcpy(gBattleAnimArgs, configs[config], sizeof(configs[config]));
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    // Source gDummySpriteAffineAnimTable ends immediately, preserving identity.
    // Reset every case so duration one cannot inherit an earlier missile matrix.
    gOamMatrices[0] = (struct OamMatrix){256, 0, 0, 256}; sRotation = 0;
    s.x = sX[target]; s.y = sY[target]; s.inUse = TRUE; s.oam.affineMode = 1;
    AnimMissileArc(&s); emit(&s, config, -1);
    for (int tick = 0; tick < 260; ++tick) {
        s.callback(&s); emit(&s, config, tick); if (!s.inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; ++actor)
        for (int relation = 1; relation <= 2; ++relation)
            for (int config = 0; config < sizeof(configs) / sizeof(configs[0]); ++config)
                run(actor, actor ^ relation, config);
}
'''
    with tempfile.TemporaryDirectory(prefix='arc-projectile-source-c-') as tmp:
        c, exe = Path(tmp) / 'arc.c', Path(tmp) / 'arc'
        c.write_text(harness)
        ref.compile_c(['cc', '-std=gnu11', '-O0', str(c), '-o', str(exe)])
        output = subprocess.check_output([str(exe)], text=True)
    cases = {}
    for line in output.splitlines():
        key, *row = line.split(',')
        values = list(map(int, row))
        if len(values) != len(COLUMNS):
            raise ValueError('Unexpected original vector width')
        cases.setdefault(key, []).append(values)
    validate(cases)
    return dict(columns=COLUMNS, configs=CONFIGS, cases=cases)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--angle-adapter', type=Path, default=Path(__file__).with_name('bios_angles_adapter.c'))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Choose a new candidate output; refusing to overwrite an original vector fixture.')
    args.output.write_text(json.dumps(build(args.reference, args.helper, args.angle_adapter),
                                     separators=(',', ':')) + '\n')
