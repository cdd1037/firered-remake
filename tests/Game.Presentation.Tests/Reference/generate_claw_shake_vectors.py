#!/usr/bin/env python3
"""Extract pinned Claw Slash and shake-controller functions into bounded C.

Expected values come only from original C, not product code. Use the existing
reference_c revision guard and Bone generator's balanced-function/data extraction.
Two independent authored harnesses are compiled; no upstream scripts are run.
The shake adapter replaces only the three unsafe 32-bit data[6]/data[7] host
pointer dereferences with a four-entry, checked pointer-storage lookup. Original
u16 reads/writes, signed data fields, arithmetic, branching and cleanup remain.
"""
import argparse
import importlib.util
import json
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CLAW_CONFIGS = [[-10, -10, 0], [-10, 10, 0], [10, -10, 1], [10, 10, 1],
                [-7, 9, 0], [7, -9, 1]]
CLAW_COLUMNS = ['tick', 'x', 'y', 'x2', 'y2', 'tile', 'hflip', 'vflip',
                'active', 'callback', 'animNum', 'animEnded', 'animCmdIndex',
                'animDelay']
SHAKE_CONFIGS = [[amplitude, 1, 10, mode, participant]
                 for amplitude in (-4, 5) for mode in range(4)
                 for participant in range(3)] + [
                     [0, 0, 0, 2, 2], [3, 0, 1, 0, 0],
                     [-32768, 0, 3, 3, 1], [32767, 0, 3, 2, 0],
                     [4, -1, 4, 1, 0], [-4, 2, 7, 2, 2]]
# raw BG3 X/Y, signed sprite X/Y, initial coordinate-enabled mask.
SHAKE_INITIALS = [[0, 0, 0, 0, 0], [65534, 1, 32766, -32767, 15]]
SHAKE_COLUMNS = ['tick', 'bg3X', 'bg3Y', 'spriteOffsetX', 'spriteOffsetY',
                 'coordOffsetMask', 'active', 'invisible', 'nextDelta',
                 'delay', 'delayReset', 'remainingSteps', 'savedU16',
                 'mode', 'boundGlobal', 'selectedU16']
for _actor in range(4):
    SHAKE_COLUMNS += [f'actor{_actor}X2', f'actor{_actor}Y2',
                      f'actor{_actor}ProjectedX', f'actor{_actor}ProjectedY']


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def compile_run(ref, name, harness):
    with tempfile.TemporaryDirectory(prefix=name + '-source-c-') as tmp:
        c, exe = Path(tmp) / (name + '.c'), Path(tmp) / name
        c.write_text(harness)
        ref.compile_c(['cc', '-std=gnu11', '-O0', str(c), '-o', str(exe)])
        return subprocess.check_output([str(exe)], text=True)


def array(name, rows):
    return 'static const int ' + name + '[][' + str(len(rows[0])) + '] = {' + ','.join(
        '{' + ','.join(map(str, row)) + '}' for row in rows) + '};\n'


def prototypes(functions):
    return '\n'.join(f.split('\n{', 1)[0] + ';' for f in functions) + '\n'


def parse_rows(output, columns, key_width):
    cases = {}
    for line in output.splitlines():
        values = list(map(int, line.split(',')))
        key, row = values[:key_width], values[key_width:]
        if len(row) != len(columns):
            raise ValueError('Unexpected source-vector width')
        cases.setdefault(':'.join(map(str, key)), []).append(row)
    return cases


COMMON = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int8_t s8; typedef int16_t s16; typedef int32_t s32;
typedef uint8_t bool8;
#define FALSE 0
#define TRUE 1
static s16 gBattleAnimArgs[8];
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static const s16 sX[] = {32, 200, 90, 152};
static const s16 sY[] = {88, 40, 96, 32};
'''


def build_claw(reference, ref, extract):
    sprite = (reference / 'src/sprite.c').read_text()
    dark = (reference / 'src/battle_anim_dark.c').read_text()
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    header = (reference / 'include/sprite.h').read_text()
    commands = header[header.index('struct AnimFrameCmd\n'):header.index('struct AffineAnimFrameCmd\n')]
    names = ['AnimateSprites', 'AnimateSprite', 'BeginAnim', 'ContinueAnim',
             'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop',
             'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop',
             'DecrementAnimDelayCounter', 'SetSpriteOamFlipBits', 'StartSpriteAnim']
    functions = [extract.function(sprite, name) for name in names]
    functions += [extract.function(mons, 'RunStoredCallbackWhenAnimEnds'),
                  extract.function(dark, 'AnimClawSlash')]
    harness = COMMON + commands + r'''
#define MAX_SPRITES 1
#define ST_OAM_AFFINE_ON_MASK 1
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    u8 animCmdIndex, animLoopCounter, animNum;
    u8 animDelayCounter:6;
    bool8 animEnded, animBeginning, animPaused, usingSheet, hFlip, vFlip;
    bool8 affineAnimBeginning, inUse;
    s16 x, y, x2, y2, data[8];
    int sheetTileStart, images;
    const union AnimCmd *const *anims;
    struct {u8 affineMode, matrixNum; int tileNum;} oam;
    SpriteCallback callback, storedCallback;
};
static struct Sprite gSprites[MAX_SPRITES];
// The Claw template has affine mode off; the bounded fixture only exercises
// the frame interpreter. Both affine dispatch entries are unreachable.
static bool8 gAffineAnimsDisabled = TRUE;
static void UnreachableAffine(struct Sprite *s) {abort();}
static void (*sAffineAnimFuncs[])(struct Sprite *) = {UnreachableAffine, UnreachableAffine};
// Adapt callback-pointer storage only; original callback decides transition.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {s->storedCallback = cb;}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void RequestSpriteFrameImageCopy(int image, int tile, int images) {abort();}
'''
    harness += prototypes(functions) + r'''
static void (*sAnimFuncs[])(struct Sprite *) = {ContinueAnim, BeginAnim};
static void (*sAnimCmdFuncs[])(struct Sprite *) = {AnimCmd_loop, AnimCmd_jump, AnimCmd_end, AnimCmd_frame};
'''
    harness += '\n'.join(functions) + '\n'
    harness += extract.declaration(dark, 'sAnim_ClawSlash_0') + '\n'
    harness += extract.declaration(dark, 'sAnim_ClawSlash_1') + '\n'
    harness += r'''
static const union AnimCmd *const clawAnims[] = {sAnim_ClawSlash_0, sAnim_ClawSlash_1};
'''
    harness += array('configs', CLAW_CONFIGS)
    harness += r'''
static void emit(int config, int tick) {
    const struct Sprite *s = &gSprites[0];
    int callback = s->callback == RunStoredCallbackWhenAnimEnds ? 0 :
                   s->callback == DestroyAnimSprite ? 1 : -1;
    assert(callback >= 0);
    const int row[] = {tick, s->x, s->y, s->x2, s->y2, s->oam.tileNum,
        (s->oam.matrixNum >> 3) & 1, (s->oam.matrixNum >> 4) & 1,
        s->inUse, callback, s->animNum, s->animEnded,
        s->animCmdIndex, s->animDelayCounter};
    printf("%d,%d,%d", gBattleAnimAttacker, gBattleAnimTarget, config);
    for (unsigned i = 0; i < sizeof(row)/sizeof(row[0]); i++) printf(",%d", row[i]);
    puts("");
}
static void run(int actor, int target, int config) {
    memset(gSprites, 0, sizeof(gSprites));
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    for (int i = 0; i < 3; i++) gBattleAnimArgs[i] = configs[config][i];
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    struct Sprite *s = &gSprites[0];
    s->inUse = s->usingSheet = s->animBeginning = TRUE;
    s->anims = clawAnims;
    // createsprite ANIM_TARGET supplies this center before AnimClawSlash.
    s->x = sX[target]; s->y = sY[target];
    s->callback = AnimClawSlash;
    AnimateSprites(); emit(config, -1);
    for (int tick = 0; tick < 40; tick++) {
        AnimateSprites(); emit(config, tick);
        if (!s->inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; actor++)
        for (int relation = 1; relation <= 2; relation++)
            for (unsigned config = 0; config < sizeof(configs)/sizeof(configs[0]); config++)
                run(actor, actor ^ relation, config);
}
'''
    cases = parse_rows(compile_run(ref, 'claw', harness), CLAW_COLUMNS, 3)
    if len(cases) != 8 * len(CLAW_CONFIGS):
        raise ValueError('Incomplete claw cases')
    for key, rows in cases.items():
        _, target, config = map(int, key.split(':'))
        args = CLAW_CONFIGS[config]
        if [r[0] for r in rows] != list(range(-1, 22)):
            raise ValueError('Claw lifecycle rows are incomplete')
        if any(r[1:5] != [[32, 200, 90, 152][target] + args[0],
                          [88, 40, 96, 32][target] + args[1], 0, 0] for r in rows):
            raise ValueError('Claw placement unexpectedly mirrors or drifts')
        if not (all(r[8] == 1 for r in rows[:-1]) and rows[-1][8] == 0
                and rows[20][11] == 1 and rows[20][9] == 0
                and rows[21][9] == 1 and rows[22][9] == 1):
            raise ValueError('Claw END/swap/retirement timing changed')
        if any(r[6:8] != [args[2], 0] for r in rows):
            raise ValueError('Claw frame-selected flip changed')
    return {'columns': CLAW_COLUMNS, 'configs': CLAW_CONFIGS, 'cases': cases}


def build_shake(reference, ref, extract):
    normal = (reference / 'src/battle_anim_normal.c').read_text()
    sprite = (reference / 'src/sprite.c').read_text()
    functions = [extract.function(normal, name) for name in (
        'AnimShakeMonOrBattleTerrain', 'AnimShakeMonOrBattleTerrain_Step',
        'AnimShakeMonOrBattleTerrain_UpdateCoordOffsetEnabled')]
    expression = '*(u16 *)(sprite->data[6] | (sprite->data[7] << 16))'
    if sum(f.count(expression) for f in functions) != 3:
        raise ValueError('Original shake pointer expressions changed')
    functions = [f.replace(expression, '*StoredGlobalU16(sprite)') for f in functions]
    functions += [extract.function(sprite, 'UpdateOamCoords')]
    harness = COMMON + r'''
#define MAX_SPRITES 5
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    bool8 inUse, invisible, coordOffsetEnabled;
    s16 x, y, x2, y2, data[8];
    s8 centerToCornerVecX, centerToCornerVecY;
    // Unwrapped center coordinates are the presentation seam: corner offsets
    // are zero, and these output fields do not apply GBA OAM 9/8-bit wrapping.
    struct {int x, y;} oam;
    SpriteCallback callback;
};
static struct Sprite gSprites[MAX_SPRITES];
static u8 gBattlersCount = 4;
static const u8 gBattlerSpriteIds[] = {0, 1, 2, 3};
static u16 gBattle_BG3_X, gBattle_BG3_Y;
static s16 gSpriteCoordOffsetX, gSpriteCoordOffsetY;
static const s16 initialX2[] = {3, -5, 7, -9};
static const s16 initialY2[] = {-2, 4, -6, 8};
// Host storage adapter: source uses data6/7 as a 32-bit pointer. A bounded
// slot plus a zero high word refers only to these exact four u16 storage cells.
// Reading/writing a corresponding unsigned type preserves signed global bits.
static u16 *const globals[] = {&gBattle_BG3_X, &gBattle_BG3_Y,
    (u16 *)&gSpriteCoordOffsetX, (u16 *)&gSpriteCoordOffsetY};
static void StoreSpriteCallbackInData6(struct Sprite *s, void *ptr) {
    for (int i = 0; i < 4; i++) {
        if (ptr == (void *)globals[i]) {s->data[6] = i + 1; s->data[7] = 0; return;}
    }
    abort();
}
static u16 *StoredGlobalU16(const struct Sprite *s) {
    assert(s->data[6] >= 1 && s->data[6] <= 4 && s->data[7] == 0);
    return globals[s->data[6] - 1];
}
// Keep the final observed state, rather than unrelated destroyed-slot reset.
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
'''
    harness += prototypes(functions) + '\n'.join(functions) + '\n'
    harness += array('configs', SHAKE_CONFIGS) + array('initials', SHAKE_INITIALS)
    harness += r'''
static void emit(int config, int initial, int tick) {
    const struct Sprite *s = &gSprites[4];
    int mask = 0;
    for (int i = 0; i < 4; i++) if (gSprites[i].coordOffsetEnabled) mask |= 1 << i;
    const int row[] = {tick, gBattle_BG3_X, gBattle_BG3_Y,
        gSpriteCoordOffsetX, gSpriteCoordOffsetY, mask, s->inUse,
        s->invisible, s->data[0], s->data[1], s->data[2], s->data[3],
        (u16)s->data[4], s->data[5], s->data[6] - 1, *StoredGlobalU16(s)};
    printf("%d,%d,%d,%d", gBattleAnimAttacker, gBattleAnimTarget, config, initial);
    for (unsigned i = 0; i < sizeof(row)/sizeof(row[0]); i++) printf(",%d", row[i]);
    for (int i = 0; i < 4; i++)
        printf(",%d,%d,%d,%d", gSprites[i].x2, gSprites[i].y2, gSprites[i].oam.x, gSprites[i].oam.y);
    puts("");
}
static void run(int actor, int target, int config, int initial) {
    memset(gSprites, 0, sizeof(gSprites));
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    for (int i = 0; i < 5; i++) gBattleAnimArgs[i] = configs[config][i];
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    gBattle_BG3_X = initials[initial][0]; gBattle_BG3_Y = initials[initial][1];
    gSpriteCoordOffsetX = initials[initial][2]; gSpriteCoordOffsetY = initials[initial][3];
    for (int i = 0; i < 4; i++) {
        struct Sprite *s = &gSprites[i];
        s->inUse = TRUE;
        s->x = sX[i]; s->y = sY[i];
        s->x2 = initialX2[i]; s->y2 = initialY2[i];
        s->coordOffsetEnabled = (initials[initial][4] >> i) & 1;
    }
    struct Sprite *s = &gSprites[4];
    s->inUse = TRUE;
    s->callback = AnimShakeMonOrBattleTerrain;
    s->callback(s); UpdateOamCoords(); emit(config, initial, -1);
    for (int tick = 0; tick < 40; tick++) {
        s->callback(s); UpdateOamCoords(); emit(config, initial, tick);
        if (!s->inUse) return;
    }
    abort();
}
int main(void) {
    for (int actor = 0; actor < 4; actor++)
        for (int relation = 1; relation <= 2; relation++)
            for (unsigned config = 0; config < sizeof(configs)/sizeof(configs[0]); config++)
                for (unsigned initial = 0; initial < sizeof(initials)/sizeof(initials[0]); initial++)
                    run(actor, actor ^ relation, config, initial);
}
'''
    cases = parse_rows(compile_run(ref, 'shake', harness), SHAKE_COLUMNS, 4)
    if len(cases) != 8 * len(SHAKE_CONFIGS) * len(SHAKE_INITIALS):
        raise ValueError('Incomplete shake cases')
    for key, rows in cases.items():
        actor, target, config, initial = map(int, key.split(':'))
        args, baseline = SHAKE_CONFIGS[config], SHAKE_INITIALS[initial]
        mode, participant = args[3:5]
        duration = max(0, args[2])
        if [r[0] for r in rows] != list(range(-1, duration + 1)):
            raise ValueError('Missing initialization, oscillation, or retirement row')
        if not (all(r[6] == 1 for r in rows[:-1]) and rows[-1][6] == 0):
            raise ValueError('Unexpected shake retirement timing')
        if rows[-1][15] != (baseline[mode] & 65535):
            raise ValueError('Shake did not restore the exact u16 value')
        if mode >= 2:
            mask = baseline[4] & ~(1 << actor | 1 << target)
            if participant in (0, 2): mask |= 1 << actor
            if participant in (1, 2): mask |= 1 << target
            if not (all(r[5] == mask for r in rows[:-1]) and rows[-1][5] == 0):
                raise ValueError('Shake participant selection or global mask cleanup changed')
        elif any(r[5] != baseline[4] for r in rows):
            raise ValueError('BG shake unexpectedly changed the coordinate-enable mask')
        for row in rows:
            for actor in range(4):
                start = 16 + actor * 4
                if row[start:start + 2] != [[3, -5, 7, -9][actor], [-2, 4, -6, 8][actor]]:
                    raise ValueError('Coordinate projection changed source pos2')
    return {'columns': SHAKE_COLUMNS, 'configs': SHAKE_CONFIGS,
            'initials': SHAKE_INITIALS, 'cases': cases}


def build(reference, helper):
    sys.dont_write_bytecode = True
    ref = load_module('reference_c', helper)
    ref.verify_reference(reference)
    # Reuse the existing generic extraction helpers; do not copy runtime logic.
    extract = load_module('bone_source_extract', helper.parent.parent /
                          'Game.Presentation.Tests/Reference/generate_bone_sprite_vectors.py')
    return {'claw': build_claw(reference, ref, extract),
            'shake': build_shake(reference, ref, extract)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference, args.helper), separators=(',', ':')) + '\n')
