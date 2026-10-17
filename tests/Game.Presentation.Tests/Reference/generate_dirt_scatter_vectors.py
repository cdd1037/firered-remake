#!/usr/bin/env python3
"""Extract pinned DirtScatter movement and primary Random into a tiny C oracle.

The callback and linear helper bodies, Random body, and LCG/translation macros
are extracted verbatim from pret/pokefirered 037335f4. Only authored C plus those
bodies are compiled. No upstream script or product assembly is executed.

Tick -1 runs AnimDirtScatter (two draws, no movement). Tick 0 invokes its selected
StartAnimLinearTranslation callback, which initializes and immediately moves.
D movement ticks are 0..D-1; tick D only installs DestroySpriteAndMatrix; D+1
calls destruction. Inactive rows retain last visible fields. Frame animation and
slot/matrix clearing are outside this bounded movement/RNG oracle.

Source sprite words are s16, but translation reads velocities/accumulators as
u16. The numerator wraps to u16 BEFORE division and bit zero carries direction.
The fixture samples long cases; every intervening row is executed and validated.
Initial RNG values are complete u32 gRngValue states, not SeedRng(u16) arguments.
Each primaryRng entry carries 60 sequential draws from 30 source callbacks,
followed by the next particle's two continuation draws, without reseeding.
Their particle rows capture tick 0 movement for actor 0, target 1, args 15,15,1,
0,0 so runtime tests can compare slot-ordered positions directly to source.
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
SEEDS = [0, 1, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x12345678]
# [initialState, xOffset, yOffset, duration, unusedArg3, unusedArg4]
CONFIGS = [[seed, 15, 15, duration, 0, 0]
           for seed in (0, 1, 0x80000000, 0xFFFFFFFF)
           for duration in (1, 2, 10, 255)]
CONFIGS += [[0, 15, 15, 20, 0, 0], [0, 15, 15, 20, -32768, 32767]]
# Together with seed zero, these exercise both axes' masks 0/16/17/31.
CONFIGS += [[seed, -128, 127, 10, 32767, -32768]
            for seed in (13, 28, 7, 18, 32, 5, 25)]
CONFIGS += [[1, 127, -128, 255, -1, 1]]
COLUMNS = ['tick', 'x', 'y', 'x2', 'y2', 'active', 'callback',
           'remaining', 'data1', 'data2', 'data3', 'data4']
CALLBACKS = ['StartAnimLinearTranslation', 'AnimTranslateLinear_WithFollowup',
             'DestroySpriteAndMatrix']
COORDS = [[32, 88], [200, 40], [90, 96], [152, 32]]


def function(source, name):
    match = re.search(r'^(?:static )?(?:void|s16|u8|u16|bool8) ' + name +
                      r'\([^;]+?\)\n\{', source, re.M)
    if not match:
        raise ValueError('Missing original function ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


def s16(value):
    return ((value + 32768) & 65535) - 32768


def random_step(state):
    # Used only to validate the independently executed extracted C output.
    state = (state * 1103515245 + 24691) & 0xFFFFFFFF
    return state, state >> 16


def sampled_ticks(duration, rows):
    if duration <= 20:
        return set(range(-1, duration + 2))
    ticks = {-1, 0, 1, 2, 3, 31, 63, 127, duration - 2,
             duration - 1, duration, duration + 1}
    # Keep both sides of any signed storage crossing or unsigned accumulator
    # wrap, rather than relying on arbitrary middle-of-lifetime samples.
    for previous, current in zip(rows[1:], rows[2:]):
        if any((previous[i] < 0) != (current[i] < 0)
               or (current[i] & 65535) < (previous[i] & 65535)
               for i in (10, 11)):
            ticks.update((previous[0], current[0]))
    return ticks


def validate(all_cases, rng, draws):
    if len(all_cases) != 8 * len(CONFIGS) or len(rng) != len(SEEDS):
        raise ValueError('Incomplete position/config/seed coverage')
    mask_coverage = [set(), set()]
    wrapped_numerator = signed_storage = unsnapped_endpoint = False
    for key, rows in all_cases.items():
        actor, target, config = map(int, key.split(':'))
        seed, offset_x, offset_y, duration, unused3, unused4 = CONFIGS[config]
        if target not in (actor ^ 1, actor ^ 2):
            raise ValueError('Unexpected target relation')
        state, rx = random_step(seed)
        state, ry = random_step(state)
        if draws[key] != [rx, ry, state, 2]:
            raise ValueError('Creation must consume exactly X then Y draws: ' + key)
        masks = [rx & 31, ry & 31]
        for axis, mask in enumerate(masks):
            mask_coverage[axis].add(mask)
        scatter = [16 - mask if mask > 16 else mask for mask in masks]
        x = COORDS[actor][0] + offset_x * (1 if COORDS[actor][0] < COORDS[target][0] else -1)
        y = COORDS[actor][1] + offset_y
        destination = [(COORDS[target][i] & 255) + scatter[i] for i in range(2)]
        delta = [destination[0] - x, destination[1] - y]
        velocity = [((((abs(v) << 8) & 65535) // duration) & ~1) | (v < 0) for v in delta]
        wrapped_numerator |= any(abs(v) >= 256 for v in delta)
        if [r[0] for r in rows] != list(range(-1, duration + 2)):
            raise ValueError('Unexpected callback lifetime: ' + key)
        for row in rows:
            tick = row[0]
            if tick == -1:
                expected = [-1, x, y, 0, 0, 1, 0, duration, 0, destination[0], 0, destination[1]]
            else:
                count = min(tick + 1, duration)
                accumulator = [(v * count) & 65535 for v in velocity]
                offsets = [(accumulator[i] >> 8) * (-1 if velocity[i] & 1 else 1) for i in range(2)]
                expected = [tick, x, y, *offsets, int(tick <= duration),
                            2 if tick >= duration else 1, duration - count,
                            *map(s16, velocity), *map(s16, accumulator)]
                signed_storage |= any(v < 0 for v in expected[8:])
            if row != expected:
                raise ValueError('Unexpected C movement/state: ' + key + ' tick ' + str(tick))
        if rows[-1][1:5] != rows[-2][1:5] or rows[-2][1:5] != rows[-3][1:5]:
            raise ValueError('Handoff/destruction changed last visible position: ' + key)
        endpoint = [rows[-1][1] + rows[-1][3], rows[-1][2] + rows[-1][4]]
        unsnapped_endpoint |= endpoint != destination
    if not all({0, 16, 17, 31} <= masks for masks in mask_coverage):
        raise ValueError('Missing scatter branch boundaries on either axis')
    if not all((wrapped_numerator, signed_storage, unsnapped_endpoint)):
        raise ValueError('Missing wrap/signed-word/no-snap coverage')
    for seed in SEEDS:
        state = seed
        entry = rng[str(seed)]
        expected = []
        for draw in range(1, 63):
            state, value = random_step(state)
            expected.append([value, state])
        if entry['draws'] != expected[:60] or entry['continuation'] != expected[60:]:
            raise ValueError('Primary RNG continuation differs: ' + str(seed))
        particles = entry['particles'] + [entry['continuationParticle']]
        if len(particles) != 31:
            raise ValueError('Missing callback-driven particle rows')
        for index, row in enumerate(particles):
            masks = [expected[index * 2 + axis][0] & 31 for axis in range(2)]
            offsets = [16 - mask if mask > 16 else mask for mask in masks]
            # For this duration-one input, the source's first movement reaches
            # its destination exactly; both displacements fit one u8 pixel.
            if row != [index, 47, 103, 200 + offsets[0] - 47, 40 + offsets[1] - 103]:
                raise ValueError('Callback-driven first movement differs')
    # Equal input args 0..2 and seed, arbitrarily different unused args 3/4.
    for actor in range(4):
        for target in (actor ^ 1, actor ^ 2):
            if all_cases[f'{actor}:{target}:16'] != all_cases[f'{actor}:{target}:17']:
                raise ValueError('Source unexpectedly used args 3 or 4')


def build(reference, helper):
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('reference_c', helper)
    ref = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ref)
    ref.verify_reference(reference)
    ground = (reference / 'src/battle_anim_ground.c').read_text()
    mons = (reference / 'src/battle_anim_mons.c').read_text()
    random = (reference / 'src/random.c').read_text()
    random_header = (reference / 'include/random.h').read_text()
    battle_header = (reference / 'include/battle_anim.h').read_text()
    macros = '\n'.join(re.findall(r'^#define (?:RAND_MULT|ISO_RANDOMIZE1)\b.*$', random_header, re.M))
    macros += '\n' + '\n'.join(re.findall(r'^#define sTransl_.*$', battle_header, re.M))
    functions = [function(mons, name) for name in (
        'InitSpritePosToAnimAttacker', 'SetAnimSpriteInitialXOffset',
        'InitAnimLinearTranslation', 'StartAnimLinearTranslation',
        'AnimTranslateLinear', 'AnimTranslateLinear_WithFollowup')]
    functions += [function(ground, 'AnimDirtScatter')]
    harness = r'''
#include <assert.h>
#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int16_t s16; typedef int32_t s32; typedef uint8_t bool8;
#define FALSE 0
#define TRUE 1
#define B_SIDE_PLAYER 0
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    s16 x, y, x2, y2, data[8];
    bool8 inUse;
    SpriteCallback callback, storedCallback;
};
static u32 gRngValue;
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static s16 gBattleAnimArgs[8];
static const u8 sX[] = {32, 200, 90, 152};
static const u8 sY[] = {88, 40, 96, 32};
static u8 GetBattlerSide(u8 battler) {assert(battler < 4); return battler & 1;}
static u8 GetBattlerSpriteCoord(u8 battler, u8 coord) {
    assert(battler < 4 && coord < 4);
    return coord == BATTLER_COORD_X || coord == BATTLER_COORD_X_2 ? sX[battler] : sY[battler];
}
static u8 GetBattlerSpriteCoord2(u8 battler, u8 coord) {return GetBattlerSpriteCoord(battler, coord);}
// Only host pointer storage and destruction's unrelated slot clearing are
// adapted. The extracted callbacks own all timing and movement arithmetic.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {s->storedCallback = cb;}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroySpriteAndMatrix(struct Sprite *s) {s->inUse = FALSE;}
static int drawCount;
static u16 observedDraws[64];
static u32 observedStates[64];
'''
    harness += macros + '\n' + function(random, 'Random') + r'''
static u16 ObservedRandom(void) {
    u16 result = Random();
    assert(drawCount < 64);
    observedDraws[drawCount] = result;
    observedStates[drawCount++] = gRngValue;
    return result;
}
// Instrument source calls without changing the extracted function bodies.
#define Random ObservedRandom
'''
    harness += '\n'.join(f.split('\n{', 1)[0] + ';' for f in functions) + '\n'
    harness += '\n'.join(functions) + '\n#undef Random\n'
    harness += 'static const u32 seeds[] = {' + ','.join(str(seed) + 'u' for seed in SEEDS) + '};\n'
    harness += 'struct Config {u32 seed; s16 args[5];};\n'
    harness += 'static const struct Config configs[] = {' + ','.join(
        '{' + str(config[0]) + 'u,{' + ','.join(map(str, config[1:])) + '}}' for config in CONFIGS) + '};\n'
    harness += r'''
static int callbackCode(SpriteCallback callback) {
    if (callback == StartAnimLinearTranslation) return 0;
    if (callback == AnimTranslateLinear_WithFollowup) return 1;
    if (callback == DestroySpriteAndMatrix) return 2;
    abort();
}
static void emit(int config, int tick, const struct Sprite *s) {
    printf("C,%d,%d,%d", gBattleAnimAttacker, gBattleAnimTarget, config);
    const int row[] = {tick, s->x, s->y, s->x2, s->y2, s->inUse,
        callbackCode(s->callback), s->data[0], s->data[1], s->data[2], s->data[3], s->data[4]};
    for (unsigned i = 0; i < sizeof(row) / sizeof(row[0]); i++) printf(",%d", row[i]);
    printf("\n");
}
static void run(int actor, int target, int config) {
    struct Sprite s = {0};
    gBattleAnimAttacker = actor; gBattleAnimTarget = target;
    memset(gBattleAnimArgs, 0, sizeof(gBattleAnimArgs));
    memcpy(gBattleAnimArgs, configs[config].args, sizeof(configs[config].args));
    gRngValue = configs[config].seed; drawCount = 0;
    s.inUse = TRUE;
    // Original creation coordinates are overwritten by the extracted init.
    s.x = sX[target]; s.y = sY[target];
    AnimDirtScatter(&s);
    assert(drawCount == 2);
    assert(s.storedCallback == DestroySpriteAndMatrix);
    printf("D,%d,%d,%d,%u,%u,%" PRIu32 ",%d\n", actor, target, config,
           observedDraws[0], observedDraws[1], gRngValue, drawCount);
    emit(config, -1, &s);
    for (int tick = 0; tick <= configs[config].args[2] + 1; tick++) {
        assert(s.inUse);
        s.callback(&s);
        assert(drawCount == 2);
        assert(memcmp(gBattleAnimArgs, configs[config].args, sizeof(configs[config].args)) == 0);
        emit(config, tick, &s);
    }
    assert(!s.inUse);
}
int main(void) {
    for (int actor = 0; actor < 4; actor++)
        for (int relation = 1; relation <= 2; relation++)
            for (unsigned config = 0; config < sizeof(configs) / sizeof(configs[0]); config++)
                run(actor, actor ^ relation, config);
    for (unsigned seed = 0; seed < sizeof(seeds) / sizeof(seeds[0]); seed++) {
        gRngValue = seeds[seed]; drawCount = 0;
        gBattleAnimAttacker = 0; gBattleAnimTarget = 1;
        memcpy(gBattleAnimArgs, configs[16].args, sizeof(configs[16].args));
        gBattleAnimArgs[2] = 1;
        // Each actual root creates five particles per helper call, six calls.
        // A 31st creation must continue the same stream, not re-seed a sprite.
        for (int particle = 0; particle < 31; particle++) {
            struct Sprite s = {0};
            s.inUse = TRUE;
            AnimDirtScatter(&s);
            assert(drawCount == 2 * (particle + 1));
            for (int draw = 2 * particle; draw < drawCount; draw++)
                printf("R,%" PRIu32 ",%d,%u,%" PRIu32 "\n", seeds[seed], draw + 1,
                       observedDraws[draw], observedStates[draw]);
            s.callback(&s);
            assert(drawCount == 2 * (particle + 1));
            assert(s.data[0] == 0 && s.callback == AnimTranslateLinear_WithFollowup);
            printf("P,%" PRIu32 ",%d,%d,%d,%d,%d\n", seeds[seed], particle,
                   s.x, s.y, s.x2, s.y2);
        }
    }
}
'''
    with tempfile.TemporaryDirectory(prefix='dirt-scatter-c-') as tmp:
        c, exe = Path(tmp) / 'dirt_scatter.c', Path(tmp) / 'dirt_scatter'
        c.write_text(harness)
        ref.compile_c(['cc', '-std=c11', '-O0', str(c), '-o', str(exe)])
        output = subprocess.check_output([str(exe)], text=True)
    cases, rng, draws = {}, {}, {}
    for line in output.splitlines():
        kind, *fields = line.split(',')
        values = list(map(int, fields))
        if kind == 'C':
            actor, target, config, *row = values
            if len(row) != len(COLUMNS):
                raise ValueError('Unexpected C vector width')
            cases.setdefault(f'{actor}:{target}:{config}', []).append(row)
        elif kind == 'D':
            actor, target, config, *row = values
            draws[f'{actor}:{target}:{config}'] = row
        elif kind == 'R':
            seed, draw, value, state = values
            entry = rng.setdefault(str(seed), {'draws': [], 'continuation': []})
            entry['draws' if draw <= 60 else 'continuation'].append([value, state])
        elif kind == 'P':
            seed, particle, *position = values
            entry = rng[str(seed)]
            if particle < 30:
                entry.setdefault('particles', []).append([particle, *position])
            else:
                entry['continuationParticle'] = [particle, *position]
        else:
            raise ValueError('Unexpected C output record')
    validate(cases, rng, draws)
    # Keep full short lifetimes; long cases retain wrap and terminal milestones.
    cases = {key: [row for row in rows if row[0] in sampled_ticks(CONFIGS[int(key.split(':')[2])][3], rows)]
             for key, rows in cases.items()}
    return {'columns': COLUMNS, 'configColumns': ['initialState', 'xOffset', 'yOffset',
             'duration', 'unusedArg3', 'unusedArg4'], 'callbacks': CALLBACKS,
            'battlerCenters': COORDS, 'configs': CONFIGS, 'cases': cases,
            'primaryRngColumns': ['random', 'stateAfterDraw'],
            'particleColumns': ['particle', 'x', 'y', 'x2', 'y2'],
            'particleInput': {'actor': 0, 'target': 1, 'args': [15, 15, 1, 0, 0], 'tick': 0},
            'primaryRng': rng}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper', type=Path, default=ROOT / 'tests/Reference/reference_c.py')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference, args.helper), separators=(',', ':')) + '\n')
