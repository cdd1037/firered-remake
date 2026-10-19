#!/usr/bin/env python3
"""Extract bounded original FireRed C callbacks and retain every native frame."""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
import tempfile
from pathlib import Path

KINDS = ['eruption-launch', 'eruption-falling', 'water-spout-launch', 'water-spout-rain']
ROOTS = [284, 284, 323, 323]
TEMPLATES = ['gEruptionLaunchRockSpriteTemplate', 'gEruptionFallingRockSpriteTemplate', 'gSmallWaterOrbSpriteTemplate', 'gWaterHitSplatSpriteTemplate']

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

def build(root, reference, out, full=False):
    helper = module('reference_c', root / 'tests/Reference/reference_c.py')
    helper.verify_reference(reference)
    extract = module('source_extract', root / 'tests/Reference/source_extract.py')
    here = Path(__file__).parent
    pins = json.loads((here / 'pinned-inputs.json').read_text())
    source = {p: (reference / p).read_text() for p in pins}
    for path, digest in pins.items():
        if hashlib.sha256((reference / path).read_bytes()).hexdigest() != digest:
            raise ValueError('Pinned source changed: ' + path)
    header = source['include/sprite.h']
    types = header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
    types = types.replace('union AffineAnimCmd\n{', 'union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
    oam = source['include/gba/types.h']
    types += '\n' + oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
    engine = ['AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits', 'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'AffineAnimStateStartAnim', 'ApplyAffineAnimFrameAbsolute', 'DecrementAnimDelayCounter', 'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'AnimateSprites', 'CreateSprite', 'CreateSpriteAndAnimate', 'SetSpriteSheetFrameTileNum', 'StartSpriteAnim', 'StartSpriteAffineAnim', 'SpriteCallbackDummy', 'CalcCenterToCornerVec', 'AllocOamMatrix', 'FreeOamMatrix', 'InitSpriteAffineAnim']
    names = {
        'src/battle_anim_fire.c': ['AnimTask_EruptionLaunchRocks', 'AnimTask_EruptionLaunchRocks_Step', 'CreateEruptionLaunchRocks', 'AnimEruptionLaunchRock', 'GetEruptionLaunchRockInitialYPos', 'InitEruptionLaunchRockCoordData', 'UpdateEruptionLaunchRockPos', 'AnimEruptionFallingRock', 'AnimEruptionFallingRock_Step'],
        'src/battle_anim_water.c': ['AnimTask_WaterSpoutLaunch', 'AnimTask_WaterSpoutLaunch_Step', 'GetWaterSpoutPowerForAnim', 'CreateWaterSpoutLaunchDroplets', 'AnimSmallWaterOrb', 'AnimTask_WaterSpoutRain', 'AnimTask_WaterSpoutRain_Step', 'CreateWaterSpoutRainDroplet', 'AnimWaterSpoutRain', 'AnimWaterSpoutRainHit'],
        'src/battle_anim_mons.c': ['GetBattlerSide', 'GetBattlerPosition', 'GetAnimBattlerSpriteId', 'BattleAnimHelper_SetSpriteSquashParams', 'BattleAnimHelper_RunSpriteSquash', 'SetSpriteRotScale', 'ShouldRotScaleSpeciesBeFlipped', 'PrepareBattlerSpriteForRotScale', 'ResetSpriteRotScale', 'SetBattlerSpriteYOffsetFromYScale'],
        'src/battle_anim_ground.c': ['AnimTask_HorizontalShake', 'AnimTask_ShakeTerrain', 'AnimTask_ShakeBattlers', 'SetBattlersXOffsetForShake'],
        'src/task.c': ['ResetTasks', 'CreateTask', 'InsertTask', 'DestroyTask', 'RunTasks', 'FindFirstActiveTask', 'TaskDummy', 'GetTaskCount'],
        'src/battle_anim.c': ['DestroyAnimSprite', 'DestroyAnimVisualTask'],
        'src/trig.c': ['Sin', 'Cos'],
        'src/sprite.c': engine,
    }
    funcs, identities = [], []
    for path, functions in names.items():
        for name in functions:
            body = extract.function(source[path], name)
            funcs.append(body)
            identities.append(dict(path=path, name=name, sha256=hashlib.sha256(body.encode()).hexdigest()))
    prototypes = '\n'.join(f[:f.index('{')].strip() + ';' for f in funcs)
    # Preserve the original macro scopes around the fire and ground callbacks.
    fire = source['src/battle_anim_fire.c']
    fire_scope = fire[fire.index('#define IDX_ACTIVE_SPRITES'):fire.index('#undef sTargetY') + len('#undef sTargetY')]
    ground = source['src/battle_anim_ground.c']
    ground_scope = ground[ground.index('#define tState               data[0]'):ground.index('#undef tInitHorizOffset') + len('#undef tInitHorizOffset')]
    bodies = fire_scope + '\n' + ground_scope + '\n' + '\n'.join(extract.function(source[p], n) for p, ns in names.items() if p not in ['src/battle_anim_fire.c', 'src/battle_anim_ground.c'] for n in ns)
    bodies = bodies.replace('sprite->callback(sprite);', 'currentSprite = i; sprite->callback(sprite); currentSprite = -1;')
    for identity in identities:
        if identity['name'] in ['AnimateSprites', 'CreateSpriteAndAnimate']:
            identity['adaptation'] = 'Observer-only current sprite slot around callback dispatch; no callback state or scheduling changes.'
    arrays = '\n'.join(extract.array(source['src/sprite.c'], n) for n in ['sAnimFuncs', 'sAffineAnimFuncs', 'sAnimCmdFuncs', 'sAffineAnimCmdFuncs', 'sCenterToCornerVecTable'])
    arrays += '\n' + extract.array(source['src/trig.c'], 'gSineTable')
    # The dummy commands are scalar unions rather than arrays. Extract exactly.
    for name in ['sDummyAnim', 'sDummyAffineAnim']:
        arrays += '\n' + re.search(r'static const union \w+ ' + name + r'\s*=\s*\{[^;]+;', source['src/sprite.c'])[0]
    arrays += '\n' + extract.array(source['src/sprite.c'], 'gDummySpriteAnimTable') + extract.array(source['src/sprite.c'], 'gDummySpriteAffineAnimTable')
    arrays += extract.array(fire, 'sEruptionLaunchRockSpeeds')
    for n in ['sAffineAnim_HitSplat_0', 'sAffineAnim_HitSplat_1', 'sAffineAnim_HitSplat_2', 'sAffineAnim_HitSplat_3', 'sAffineAnims_HitSplat']:
        arrays += extract.array(source['src/battle_anim_normal.c'], n)
    for n in ['gOamData_AffineOff_ObjNormal_8x8', 'gOamData_AffineOff_ObjNormal_16x16', 'gOamData_AffineOff_ObjNormal_32x32', 'gOamData_AffineNormal_ObjBlend_32x32']:
        arrays += re.search(r'const struct OamData ' + n + r'\s*=\s*\{[\s\S]*?\n\};', source['src/data/battle_anim.h'])[0] + '\n'
    for n in TEMPLATES:
        for p in ['src/battle_anim_fire.c', 'src/battle_anim_water.c', 'src/battle_anim_normal.c']:
            match = re.search(r'(?:static )?const struct SpriteTemplate ' + n + r'\s*=\s*\{[\s\S]*?\n\};', source[p])
            if match:
                arrays += match[0] + '\n'
                break
        else:
            raise ValueError(n)
    definitions = extract.macros(source.values(), ['Q_8_8', 'ANIM_END', 'AFFINE_ANIM_END', 'ST_OAM_AFFINE_ON_MASK', 'ANIM_TAG_WARM_ROCK', 'ANIM_TAG_GLOWY_BLUE_ORB', 'ANIM_TAG_WATER_IMPACT', 'B_SIDE_PLAYER', 'B_SIDE_OPPONENT', 'BATTLE_PARTNER', 'BIT_SIDE', 'BATTLE_TYPE_DOUBLE', 'B_POSITION_PLAYER_LEFT', 'B_POSITION_OPPONENT_LEFT', 'GET_BATTLER_SIDE2', 'GET_BATTLER_POSITION', 'ANIM_ATTACKER', 'ANIM_TARGET', 'ANIM_ATK_PARTNER', 'ANIM_DEF_PARTNER', 'SPRITE_NONE', 'MON_PIC_HEIGHT', 'ISO_RANDOMIZE2', 'MON_DATA_HP', 'MON_DATA_MAX_HP', 'MAX_BATTLERS_COUNT'])
    definitions += extract.enum_containing(source['include/battle_anim.h'], 'BATTLER_COORD_X') + '\n' + oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
    c = (here / 'harness.c.in').read_text().replace('@TYPES@', types).replace('@DEFINES@', definitions).replace('@PROTOTYPES@', prototypes).replace('@ARRAYS@', arrays).replace('@FUNCTIONS@', bodies)
    cpath = out / 'squash-water-fire.c'
    cpath.write_text(c)
    exes = []
    for opt in ['O0', 'O2']:
        exe = out / ('squash-water-fire-' + opt)
        helper.compile_c(['cc', '-std=c11', '-' + opt, '-fwrapv', '-fno-strict-aliasing', str(cpath), '-o', str(exe)])
        exes.append(exe)
    cases = []
    def add(kind, doubles, actor, target, hp=100, maxhp=100, args=(), power=150, y=0, elevation=0, shift=0, hidden=0, label='authored-root-exact'):
        arguments = list(args) + [2000 + i for i in range(len(args), 8)]
        argv = [kind, doubles, actor, target, hp, maxhp, power, y, elevation, shift, hidden, *arguments]
        outputs = [subprocess.check_output([str(exe), *map(str, argv)], text=True) for exe in exes]
        if outputs[0] != outputs[1]:
            raise ValueError('Native optimization mismatch: ' + str(argv))
        rows = [json.loads(row) for row in outputs[0].splitlines()]
        if rows[-1]['alive'] or rows[-1]['activeTasks']:
            raise ValueError('Incomplete native lifetime: ' + str(argv))
        cases.append(dict(kind=kind, name=KINDS[kind], root=ROOTS[kind], doubles=bool(doubles), actor=actor, target=target, hp=hp, maxHp=maxhp, tier=rows[0]['tier'], movePower=power, yOffset=y, elevation=elevation, shift=bool(shift), hiddenMask=hidden, arguments=arguments, subpriority=2, branch=label, rows=rows))
    falling_source = re.findall(r'createsprite gEruptionFallingRockSpriteTemplate, ANIM_ATTACKER, 40, ([^\n]+)', source['data/battle_anim_scripts.s'])
    falling_arguments = [[int(v.strip()) for v in line.split(',')] for line in falling_source]
    if len(falling_arguments) != 6:
        raise ValueError('Expected all six pinned Eruption falling-rock callback recipes')
    layouts = [(0, 0, 1), (0, 1, 0), *((1, a, a ^ 1) for a in range(4))]
    if full:
        layouts = [(d, a, t) for d in [0, 1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a != t]
    for d, a, t in layouts:
        add(0, d, a, t)
        for hp in [24, 25, 50, 75]:
            add(2, d, a, t, hp)
            add(3, d, a, t, hp)
        for args in falling_arguments:
            add(1, d, a, t, args=args, label='falling-authored')
    # Strict source comparison uses hp < floor(maxHp/4)*(i+1), even when maxHp<4.
    boundaries = [(hp, 100) for hp in [0, 1, 23, 24, 25, 26, 49, 50, 51, 74, 75, 76, 99, 100]]
    boundaries += [(hp, maximum) for maximum in [1, 2, 3] for hp in range(maximum + 1)]
    boundaries += [(hp, 101) for hp in [24, 25, 49, 50, 74, 75, 101]]
    boundaries += [(hp, 65535) for hp in [16382, 16383, 32765, 32766, 49148, 49149, 65535]]
    for hp, maximum in boundaries:
        for kind in [2, 3]:
            add(kind, 0, 0, 1, hp, maximum, label='hp-quarter-boundary')
    for d, a, t in [(0, 1, 0), (1, 2, 3)]:
        for kind in [0, 2, 3]:
            add(kind, d, a, t, 75, y=20, elevation=48, shift=1, label='shifted-base-and-picture-geometry')
    for mask in [4, 8, 12]:
        add(3, 1, 0, 1, 75, hidden=mask, label='hidden-partner')
    for power in [1, 9, 10, 19, 20, 99, 100, 150]:
        add(3, 1, 3, 2, 75, power=power, label='power-quantization')
    for delay in [0, 1]:
        for y, target in [(-128, 120), (0, 0), (0, 1), (0, 8), (0, 9), (120, 120)]:
            for tile in range(4):
                add(1, 0, 0, 1, args=[-32 if tile & 1 else 272, y, delay, target, tile], label='falling-boundary')
    proof = next(c for c in cases if c['kind'] == 3 and c['doubles'] and c['actor'] == 0 and c['tier'] == 3 and c['branch'] == 'authored-root-exact')
    births = [b for r in proof['rows'] for b in r['births'] if b['template'] == 3]
    if not any(b['slot'] < b['parentSlot'] for b in births) or not any(b['slot'] > b['parentSlot'] for b in births):
        raise ValueError('Missing lower/higher-slot splash handoff proof')
    shake_row = next(r for r in proof['rows'] if r['shakes'])
    if [s['data'][1] for s in shake_row['shakes']] != [1, 1]:
        raise ValueError('Later-priority child tasks did not run in their birth frame')
    for actor, prefix in [(0, [185, 116, 97, 124]), (1, [65, -34, 35, -44])]:
        case = next(c for c in cases if c['kind'] == 3 and not c['doubles'] and c['actor'] == actor and c['tier'] == 3 and c['branch'] == 'authored-root-exact')
        got = [b['x'] for r in case['rows'] for b in r['births'] if b['template'] == 2][:4]
        if got != prefix:
            raise ValueError('Signed32 recurrence changed: ' + str(got))
    return dict(schema='firered-squash-water-fire-source-v1', profile='full' if full else 'representative', sourceSha256=pins, extractedFunctions=identities, optimizationParity=['O0', 'O2'], semantics='Frame0 initializes the original callback. Later frames run extracted AnimateSprites followed by linked-priority RunTasks. Every frame is retained until the root, raw sprites and shake child tasks terminate. Explicit battle geometry and HP fields replace species/party hardware boundaries. Signed32 source ISO_RANDOMIZE2 uses -fwrapv. Original affine-double center vectors, squash helpers, task insertion/traversal and sprite animation bodies are executed unchanged. Sprite sheet tile bases are zero; raw slot/matrix allocation is actual source allocation. No product output is used.', cases=cases)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--build-dir', type=Path)
    parser.add_argument('--full', action='store_true')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    out = args.build_dir or Path(tempfile.mkdtemp(prefix='squash-water-fire-reference-'))
    out.mkdir(exist_ok=True, parents=True)
    result = build(args.root, args.reference or args.root / 'content/raw/pokefirered/037335f4', out, args.full)
    args.output.parent.mkdir(exist_ok=True, parents=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(json.dumps(dict(cases=len(result['cases']), rows=sum(len(c['rows']) for c in result['cases']), terminalFrames={n: sorted(set(c['rows'][-1]['frame'] for c in result['cases'] if c['name'] == n)) for n in KINDS})))
