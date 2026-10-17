#!/usr/bin/env python3
"""Extract pinned BulletSeed callbacks and affine scheduling into a tiny C oracle.

Only authored C plus verbatim functions/tables from pret/pokefirered 037335f4 are
compiled. No upstream script or product assembly runs. Tick -1 is creation's
AnimBulletSeed callback followed by AnimateSprite. Later ticks execute extracted
AnimateSprites: callback first, then interpreter only while inUse. Movement is
0..19, callback handoff is 20, sound/RNG/rebound setup is 21, rebound is 22..37.
BeginAffineAnim applies the first +20 rotation despite affineAnimPaused; Continue
honors pause. Destruction's unrelated slot clearing and normal frame animation
are outside this oracle. BIOS matrix output is not asserted; source rotation and
matrix-write timing are asserted. Inactive rows retain final callback fields.

All four actor and target positions, including same-side doubles and equal-X
self-target fallback, are covered. Initial RNG values are complete u32 states.
The compact fixture samples lifetimes only after validating every executed row;
all 16 rebound updates remain for actor0/target1. Step2-only probes exercise
signed16 accumulator overflow unreachable from this callback's fresh zero state.
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
SEEDS = [0, 1, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x12345678,
         2, 3, 5, 11, 13, 38, 160, 357]
# [initialState, xOffset, yOffset, unusedArg2, unusedArg3]
CONFIGS = [[seed, 20, 0, 0, 0] for seed in SEEDS]
CONFIGS += [[1, -128, 127, -32768, 32767], [1, 127, -128, 32767, -32768],
            [0, 20, 0, -32768, 32767], [1, -32768, 32767, 0, 0],
            [1, 32767, -32768, 0, 0]]
COLUMNS = ['tick', 'x', 'y', 'x2', 'y2', 'active', 'callback',
           'data0', 'data1', 'data2', 'data3', 'data4', 'data5', 'data6', 'data7',
           'affinePaused', 'rotation', 'affineWrites', 'drawCount', 'soundCount',
           'affineBeginning', 'affineCmdIndex', 'affineDelay', 'soundPan']
CALLBACKS = ['AnimBulletSeed', 'StartAnimLinearTranslation',
             'AnimTranslateLinear_WithFollowup', 'AnimBulletSeed_Step1', 'AnimBulletSeed_Step2']
COORDS = [[32, 88], [200, 40], [90, 96], [152, 32]]
# [initialData0, initialAngle, amplitude, speed, initialData2, initialData3]
PROBES = [[32760, 0, -19, 319, -32767, 0], [-32768, 0, -12, 160, 32767, 0],
          [-1, 120, -19, 161, -3, 0], [0, 120, -12, 318, 3, 0],
          [32767, 0, -12, 160, 0, 0], [-32768, 120, -19, -319, -3, 0]]


def function(source, name):
    match = re.search(r'^(?:static )?(?:void|s8|s16|u8|u16|bool8) ' + name +
                      r'\([^;]+?\)\n\{', source, re.M)
    if not match:
        raise ValueError('Missing source function ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


def declaration(source, name):
    match = re.search(r'^(?:static )?const (?:union AffineAnimCmd|s16) ' + name +
                      r'\[\]\s*=\s*\{.*?\};', source, re.M | re.S)
    if not match:
        raise ValueError('Missing source declaration ' + name)
    return match[0]


def s16(value):
    return ((value + 32768) & 65535) - 32768


def random_step(state):
    state = (state * 1103515245 + 24691) & 0xFFFFFFFF
    return state, state >> 16


def validate(cases, observations, probes, rng, sine):
    if len(cases) != 16 * len(CONFIGS) or len(probes) != len(PROBES):
        raise ValueError('Incomplete actor/target/config/probe coverage')
    masks, moduli = set(), set()
    signed_storage = unsnapped = wrapped_numerator = False
    for key, rows in cases.items():
        actor, target, config = map(int, key.split(':'))
        seed, ox, oy, _, _ = CONFIGS[config]
        direction = (1 if COORDS[actor][0] < COORDS[target][0] else
                     -1 if COORDS[actor][0] > COORDS[target][0] else
                     -1 if actor & 1 else 1)
        x, y = s16(COORDS[actor][0] + ox * direction), s16(COORDS[actor][1] + oy)
        delta = [COORDS[target][0] - x, COORDS[target][1] - y]
        velocity = [((((abs(d) << 8) & 65535) // 20) & ~1) | (d < 0) for d in delta]
        wrapped_numerator |= any(abs(d) >= 256 for d in delta)
        state1, r1 = random_step(seed)
        state2, r2 = random_step(state1)
        masks.add(r1 & 7); moduli.add(r2 % 160)
        amplitude, speed = -12 - (r1 & 7), 160 + r2 % 160
        sound_pan = 63 if target & 1 else -63 if actor & 1 else -64
        if observations[key] != [21, 159, sound_pan, 0, seed, r1, state1, r2, state2]:
            raise ValueError('Sound must precede both ordered RNG draws: ' + key)
        if [row[0] for row in rows] != list(range(-1, 38)):
            raise ValueError('Unexpected callback lifetime: ' + key)
        final_offset = [(v * 20 >> 8) * (-1 if v & 1 else 1) for v in velocity]
        end_x, end_y = s16(x + final_offset[0]), s16(y + final_offset[1])
        unsnapped |= [end_x, end_y] != COORDS[target]
        for row in rows:
            tick = row[0]
            if tick == -1:
                # The pointer adapter deliberately writes sentinels to both
                # source callback words so Step1's eight-word clear is proven.
                data = [20, 0, COORDS[target][0], 0, COORDS[target][1], 0, 4660, 22136]
                expected = [tick, x, y, 0, 0, 1, 1, *data, 1, 20 << 8, 1, 0, 0, 0, 0, 0, 0]
            elif tick <= 20:
                count = min(tick + 1, 20)
                accum = [(v * count) & 65535 for v in velocity]
                offsets = [(a >> 8) * (-1 if v & 1 else 1) for a, v in zip(accum, velocity)]
                data = [20-count, *map(s16, velocity), *map(s16, accum), 0, 4660, 22136]
                expected = [tick, x, y, *offsets, 1, 3 if tick == 20 else 2,
                            *data, 1, 20 << 8, 0, 0, 0, 0, 0, 0, 0]
                signed_storage |= any(d < 0 for d in data[1:5])
            else:
                updates = tick - 21
                angle = (updates * 8) if updates < 16 else 0
                data = [s16(updates * speed), angle, 0, int(updates == 16), 0, 0, amplitude, speed]
                x2 = s16(updates * speed) >> 8
                if speed & 1:
                    x2 = -x2
                y2 = 0 if not updates else (sine[(updates - 1) * 8] * amplitude) >> 8
                rotation = ((20 * (2 + min(updates, 15))) & 255) << 8
                expected = [tick, end_x, end_y, x2, y2, int(updates < 16), 4,
                            *data, 0, rotation, int(updates < 16), 2, 1, 0, 0, 0, sound_pan]
            if row != expected:
                raise ValueError('Unexpected C state: ' + key + ' tick ' + str(tick) +
                                 '\n' + repr(row) + '\n' + repr(expected))
        if rows[22][7:13] != [0] * 6:
            raise ValueError('Step1 did not clear data0..5')
    if masks != set(range(8)) or not {0, 1, 158, 159} <= moduli:
        raise ValueError('Missing R&7 or R%160 boundaries')
    if not (signed_storage and unsnapped and wrapped_numerator):
        raise ValueError('Missing signed-word/wrapped-numerator/no-snap coverage')
    for actor in range(4):
        for target in range(4):
            if cases[f'{actor}:{target}:0'] != cases[f'{actor}:{target}:16']:
                raise ValueError('Unused args 2/3 unexpectedly affected output')
    for key, rows in probes.items():
        accumulator, angle, amplitude, speed, half, rounds = PROBES[int(key)]
        active = True
        for tick, row in enumerate(rows):
            accumulator = s16(accumulator + speed)
            x2 = accumulator >> 8
            if speed & 1:
                x2 = -x2
            y2 = (sine[angle] * amplitude) >> 8
            angle = s16(angle + 8)
            if angle > 126:
                angle = 0
                half = int(half / 2)
                rounds = s16(rounds + 1)
                active = rounds != 1
            if row != [tick, x2, y2, int(active), accumulator, angle, half, rounds]:
                raise ValueError('Incorrect signed16/negative-division Step2 probe')
        if active:
            raise ValueError('Probe did not reach destruction')
    for seed in SEEDS:
        state = seed
        expected = []
        for particle in range(11):
            state, r1 = random_step(state)
            state, r2 = random_step(state)
            expected.append([particle, r1, r2, state, 2 * (particle + 1)])
        if rng[str(seed)] != expected:
            raise ValueError('Primary RNG was reseeded between particles')


def build(reference, helper):
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location('reference_c', helper)
    ref = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ref)
    ref.verify_reference(reference)
    sources = {name: (reference / ('src/' + name + '.c')).read_text()
               for name in ('battle_anim_effects_2', 'battle_anim_mons', 'battle_anim', 'sprite', 'trig', 'random')}
    header = (reference / 'include/sprite.h').read_text()
    affine_commands = header[header.index('struct AffineAnimFrameCmd\n'):header.index('\nenum\n')]
    global_header = (reference / 'include/global.h').read_text()
    macro_sources = [(reference / ('include/' + name)).read_text() for name in
                    ('random.h', 'battle_anim.h', 'constants/battle_anim.h', 'constants/songs.h')]
    patterns = [r'RAND_MULT|ISO_RANDOMIZE1', r'sTransl_\w+',
                r'SOUND_PAN_TARGET|SOUND_PAN_ATTACKER', r'SE_M_HORN_ATTACK']
    macros = '\n'.join(line for source, pattern in zip(macro_sources, patterns)
                       for line in re.findall(r'^#define (?:' + pattern + r')\b.*$', source, re.M))
    macros += '\n' + re.search(r'^#define Q_8_8\(.*$', global_header, re.M)[0]
    funcs = [function(sources['battle_anim_mons'], name) for name in
             ('InitSpritePosToAnimAttacker', 'SetAnimSpriteInitialXOffset', 'InitAnimLinearTranslation',
              'StartAnimLinearTranslation', 'AnimTranslateLinear', 'AnimTranslateLinear_WithFollowup')]
    funcs += [function(sources['battle_anim_effects_2'], name) for name in
              ('AnimBulletSeed', 'AnimBulletSeed_Step1', 'AnimBulletSeed_Step2')]
    funcs += [function(sources['battle_anim'], 'BattleAnimAdjustPanning')]
    funcs += [function(sources['trig'], 'Sin')]
    funcs += [function(sources['sprite'], name) for name in
              ('AnimateSprites', 'AnimateSprite', 'BeginAffineAnim', 'ContinueAffineAnim',
               'AffineAnimDelay', 'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame',
               'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'ApplyAffineAnimFrameAbsolute',
               'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix',
               'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame')]
    sine_decl = declaration(sources['trig'], 'gSineTable')
    sine = [round(float(v) * 256) for v in re.findall(r'Q_8_8\(([-\d.]+)\)', sine_decl)]
    if len(sine) != 320:
        raise ValueError('Missing source Q8.8 sine table')
    harness = r'''
#include <assert.h>
#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int8_t s8; typedef int16_t s16; typedef int32_t s32; typedef uint8_t bool8;
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
#define SAFE_DIV(a,b) (assert((b) != 0), (a)/(b))
#define sAnchorX data[6]
#define sAnchorY data[7]
_Static_assert((-1 >> 1) == -1, "Source Sin and signed16 shifts require arithmetic right shift");
'''
    harness += affine_commands + '\n' + macros + '\n' + sine_decl + r'''
struct Sprite;
typedef void (*SpriteCallback)(struct Sprite *);
struct Sprite {
    s16 x, y, x2, y2, data[8];
    bool8 inUse, affineAnimBeginning, affineAnimEnded, affineAnimPaused, anchored, animBeginning;
    struct {u8 affineMode;} oam;
    const union AffineAnimCmd *const *affineAnims;
    SpriteCallback callback, storedCallback;
};
struct ObjAffineSrcData {s16 xScale, yScale; u16 rotation;};
struct OamMatrix {s16 a, b, c, d;};
static struct Sprite gSprites[MAX_SPRITES];
static struct AffineAnimState sAffineAnimStates[1];
static bool8 gAffineAnimsDisabled;
static u32 gRngValue;
static u8 gBattleAnimAttacker, gBattleAnimTarget;
static s16 gBattleAnimArgs[8];
static struct {struct {bool8 statusAnimActive;} healthBoxesData[4];} battleSpritesData;
#define gBattleSpritesDataPtr (&battleSpritesData)
static const u8 sX[] = {32, 200, 90, 152}, sY[] = {88, 40, 96, 32};
static int currentTick, matrixWrites, soundCount, soundPan, soundTick, soundId, drawCount, soundDrawCount;
static u32 soundState;
static u16 observedDraws[32];
static u32 observedStates[32];
static u8 GetBattlerSide(u8 b) {assert(b < 4); return b & 1;}
static u8 GetBattlerSpriteCoord(u8 b, u8 c) {
    assert(b < 4 && c < 4); return c == BATTLER_COORD_X || c == BATTLER_COORD_X_2 ? sX[b] : sY[b];
}
static u8 GetBattlerSpriteCoord2(u8 b, u8 c) {return GetBattlerSpriteCoord(b,c);}
// Native pointer bits are represented with nonzero sentinels; Step1 still
// clears all eight source data words with its actual extracted loop.
static void StoreSpriteCallbackInData6(struct Sprite *s, SpriteCallback cb) {
    s->storedCallback = cb; s->data[6] = 0x1234; s->data[7] = 0x5678;
}
static void SetCallbackToStoredInData6(struct Sprite *s) {s->callback = s->storedCallback;}
static void DestroyAnimSprite(struct Sprite *s) {s->inUse = FALSE;}
static void PlaySE12WithPanning(u16 song, s8 pan) {
    soundCount++; soundTick=currentTick; soundId=song; soundPan=pan;
    soundDrawCount=drawCount; soundState=gRngValue;
}
static u8 GetSpriteMatrixNum(struct Sprite *s) {return 0;}
static void UpdateSpriteMatrixAnchorPos(struct Sprite *s, s32 x, s32 y) {abort();}
static void CopyOamMatrix(u8 n, struct OamMatrix *matrix) {assert(n == 0);}
// The BIOS is outside this bounded rotation/timing oracle. Do not claim these
// unused zeroed matrix values as geometry evidence.
static void ObjAffineSet(const struct ObjAffineSrcData *src, struct OamMatrix *dst, int n, int stride) {
    assert(n == 1 && stride == 2 && src->xScale == 256 && src->yScale == 256);
    memset(dst, 0, sizeof(*dst)); matrixWrites++;
}
static void DummyFrameAnimation(struct Sprite *s) {}
static void UnsupportedAffineLoop(u8 n, struct Sprite *s) {abort();}
'''
    harness += function(sources['random'], 'Random') + r'''
static u16 ObservedRandom(void) {
    // Observe state before either draw: the first must see all eight source
    // words cleared; the second sees only the first draw's new amplitude.
    for (int i=0;i<8;i++) {
        if ((drawCount & 1) && i==6) assert(gSprites[0].data[i]>=-19 && gSprites[0].data[i]<=-12);
        else assert(gSprites[0].data[i]==0);
    }
    u16 result = Random(); assert(drawCount < 32);
    observedDraws[drawCount]=result; observedStates[drawCount++]=gRngValue;
    return result;
}
#define Random ObservedRandom
'''
    harness += '\n'.join(f.split('\n{', 1)[0] + ';' for f in funcs) + '\n'
    harness += r'''
static void (*sAnimFuncs[])(struct Sprite *) = {DummyFrameAnimation,DummyFrameAnimation};
static void (*sAffineAnimFuncs[])(struct Sprite *) = {ContinueAffineAnim,BeginAffineAnim};
static void (*sAffineAnimCmdFuncs[])(u8,struct Sprite *) = {
    UnsupportedAffineLoop,AffineAnimCmd_jump,AffineAnimCmd_end,AffineAnimCmd_frame};
'''
    harness += '\n'.join(funcs) + '\n#undef Random\n'
    harness += declaration(sources['battle_anim_effects_2'], 'sBulletSeedAffineAnimCmds') + '\n'
    harness += 'static const union AffineAnimCmd *const bulletAnims[] = {sBulletSeedAffineAnimCmds};\n'
    harness += 'struct Config {u32 seed; s16 args[4];};\n'
    harness += 'static const struct Config configs[] = {' + ','.join(
        '{' + str(c[0]) + 'u,{' + ','.join(map(str,c[1:])) + '}}' for c in CONFIGS) + '};\n'
    harness += 'static const u32 seeds[] = {' + ','.join(str(s)+'u' for s in SEEDS) + '};\n'
    harness += 'static const s16 probes[][6] = {' + ','.join(
        '{' + ','.join(map(str,p)) + '}' for p in PROBES) + '};\n'
    harness += r'''
static int callbackCode(SpriteCallback cb) {
    if (cb == AnimBulletSeed) return 0;
    if (cb == StartAnimLinearTranslation) return 1;
    if (cb == AnimTranslateLinear_WithFollowup) return 2;
    if (cb == AnimBulletSeed_Step1) return 3;
    if (cb == AnimBulletSeed_Step2) return 4;
    abort();
}
static void emit(int config) {
    struct Sprite *s = &gSprites[0]; struct AffineAnimState *a = &sAffineAnimStates[0];
    printf("C,%d,%d,%d",gBattleAnimAttacker,gBattleAnimTarget,config);
    int row[]={currentTick,s->x,s->y,s->x2,s->y2,s->inUse,callbackCode(s->callback),
      s->data[0],s->data[1],s->data[2],s->data[3],s->data[4],s->data[5],s->data[6],s->data[7],
      s->affineAnimPaused,a->rotation,matrixWrites,drawCount,soundCount,
      s->affineAnimBeginning,a->animCmdIndex,a->delayCounter,soundPan};
    for (unsigned i=0;i<sizeof(row)/sizeof(row[0]);i++) printf(",%d",row[i]);
    puts("");
}
static void initialize(void) {
    struct Sprite *s=&gSprites[0]; memset(s,0,sizeof(*s));
    s->inUse=TRUE; s->callback=AnimBulletSeed; s->oam.affineMode=1;
    s->affineAnimBeginning=TRUE; s->affineAnims=bulletAnims;
    AffineAnimStateReset(0);
}
static void run(int actor,int target,int config) {
    gBattleAnimAttacker=actor;gBattleAnimTarget=target;
    memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));
    memcpy(gBattleAnimArgs,configs[config].args,sizeof(configs[config].args));
    gRngValue=configs[config].seed;drawCount=0;soundCount=0;soundPan=0;
    initialize();
    for (currentTick=-1;currentTick<38;currentTick++) {
        assert(gSprites[0].inUse); matrixWrites=0; AnimateSprites(); emit(config);
        assert(memcmp(gBattleAnimArgs,configs[config].args,sizeof(configs[config].args))==0);
        assert(drawCount==(currentTick<21?0:2));
    }
    assert(!gSprites[0].inUse && soundCount==1);
    printf("O,%d,%d,%d,%d,%d,%d,%d,%" PRIu32 ",%u,%" PRIu32 ",%u,%" PRIu32 "\n",
      actor,target,config,soundTick,soundId,soundPan,soundDrawCount,soundState,
      observedDraws[0],observedStates[0],observedDraws[1],observedStates[1]);
}
int main(void) {
    for(int actor=0;actor<4;actor++) for(int target=0;target<4;target++)
      for(unsigned config=0;config<sizeof(configs)/sizeof(configs[0]);config++) run(actor,target,config);
    for(unsigned p=0;p<sizeof(probes)/sizeof(probes[0]);p++) {
        struct Sprite s={0}; s.inUse=TRUE; s.data[0]=probes[p][0];s.data[1]=probes[p][1];
        s.data[6]=probes[p][2];s.data[7]=probes[p][3];s.data[2]=probes[p][4];s.data[3]=probes[p][5];
        for(int tick=0;s.inUse;tick++) {
            assert(tick<16); AnimBulletSeed_Step2(&s);
            printf("P,%u,%d,%d,%d,%d,%d,%d,%d,%d\n",p,tick,s.x2,s.y2,s.inUse,
              s.data[0],s.data[1],s.data[2],s.data[3]);
        }
    }
    for(unsigned seed=0;seed<sizeof(seeds)/sizeof(seeds[0]);seed++) {
        gRngValue=seeds[seed];drawCount=0;gBattleAnimAttacker=0;gBattleAnimTarget=1;
        memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));gBattleAnimArgs[0]=20;
        for(int particle=0;particle<11;particle++) {
            initialize();
            for(currentTick=-1;gSprites[0].inUse;currentTick++) {
                assert(currentTick<38);AnimateSprites();
            }
            assert(drawCount==2*(particle+1));
            printf("R,%" PRIu32 ",%d,%u,%u,%" PRIu32 ",%d\n",seeds[seed],particle,
              observedDraws[2*particle],observedDraws[2*particle+1],gRngValue,drawCount);
        }
    }
}
'''
    with tempfile.TemporaryDirectory(prefix='bullet-seed-c-') as tmp:
        c, exe = Path(tmp)/'bullet_seed.c', Path(tmp)/'bullet_seed'
        c.write_text(harness)
        ref.compile_c(['cc','-std=c11','-O0',str(c),'-o',str(exe)])
        output = subprocess.check_output([str(exe)],text=True)
    cases, observations, probes, rng = {}, {}, {}, {}
    for line in output.splitlines():
        kind, *fields = line.split(',')
        values = list(map(int,fields))
        if kind in ('C','O'):
            actor,target,config,*row = values
            key=f'{actor}:{target}:{config}'
            if kind == 'C':
                if len(row)!=len(COLUMNS): raise ValueError('Wrong vector width')
                cases.setdefault(key,[]).append(row)
            else: observations[key]=row
        elif kind == 'P':
            probe,*row=values;probes.setdefault(str(probe),[]).append(row)
        elif kind == 'R':
            seed,*row=values;rng.setdefault(str(seed),[]).append(row)
        else: raise ValueError('Unexpected C output record')
    validate(cases,observations,probes,rng,sine)
    milestones={-1,0,1,18,19,20,21,22,23,29,30,36,37}
    cases={key:[row for row in rows if row[0] in milestones or key.startswith('0:1:')]
           for key,rows in cases.items()}
    return {'columns':COLUMNS,'configColumns':['initialState','xOffset','yOffset','unusedArg2','unusedArg3'],
            'configs':CONFIGS,'callbacks':CALLBACKS,'battlerCenters':COORDS,'cases':cases,
            'observationColumns':['soundTick','soundId','soundPan','drawCountAtSound','stateAtSound',
                                  'random1','stateAfterRandom1','random2','stateAfterRandom2'],
            'observations':observations,'step2ProbeInputs':PROBES,
            'step2ProbeInputColumns':['data0','angle','amplitude','speed','data2','data3'],
            'step2ProbeColumns':['tick','x2','y2','active','data0','angle','data2','data3'],
            'step2Probes':probes,'primaryRngColumns':['particle','random1','random2','stateAfter','totalDraws'],
            'primaryRng':rng}


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py')
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    if args.output.exists(): parser.error('Refusing to overwrite an existing fixture; choose a new candidate output.')
    args.output.write_text(json.dumps(build(args.reference,args.helper),separators=(',',':'))+'\n')
