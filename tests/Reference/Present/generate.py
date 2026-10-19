#!/usr/bin/env python3
"""Extract pinned Present callbacks and the native sprite/frame/affine interpreter.

Explicit author-run generation only. This does not import or execute upstream
scripts, read managed implementation code, or overwrite a reviewed fixture.
"""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
import tempfile
from pathlib import Path


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def declaration(source, type_name, name):
    match = re.search(r'(?:static )?const ' + re.escape(type_name) + r' ' + re.escape(name)
                      + r'\s*=\s*\{[\s\S]*?\n\};', source)
    if match is None:
        raise ValueError('Missing source declaration: ' + name)
    return match[0] + '\n'


def build(root, reference, out):
    helper = module('present_reference_c', root / 'tests/Reference/reference_c.py')
    helper.verify_reference(reference)
    extract = module('present_source_extract', root / 'tests/Reference/source_extract.py')
    pins = json.loads((Path(__file__).parent / 'pinned-inputs.json').read_text())
    sources = {}
    for path, digest in pins.items():
        value = (reference / path).read_bytes()
        if hashlib.sha256(value).hexdigest() != digest:
            raise ValueError('Pinned source changed: ' + path)
        sources[path] = value.decode()
    header = sources['include/sprite.h']
    types = header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
    oam = sources['include/gba/types.h']
    types += '\n' + oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
    engine = [
        'AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end',
        'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop',
        'JumpToTopOfAnimLoop', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay',
        'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop',
        'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end',
        'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits',
        'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'AffineAnimStateStartAnim',
        'ApplyAffineAnimFrameAbsolute', 'DecrementAnimDelayCounter',
        'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix',
        'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'AnimateSprites',
        'CreateSprite', 'CreateSpriteAndAnimate', 'SetSpriteSheetFrameTileNum',
        'StartSpriteAnim', 'StartSpriteAffineAnim', 'SpriteCallbackDummy',
    ]
    names = {
        'src/battle_anim_effects_1.c': [
            'AnimPresent', 'InitItemBagData', 'MoveAlongLinearPath',
            'AnimItemSteal_Step1', 'AnimItemSteal_Step2', 'AnimPresentHealParticle',
        ],
        'src/battle_anim_effects_3.c': ['AnimTask_IsHealingMove'],
        'src/battle_anim_mons.c': [
            'GetBattlerSide', 'GetBattlerPosition', 'SetAnimSpriteInitialXOffset',
            'InitSpritePosToAnimAttacker', 'InitSpritePosToAnimTarget',
        ],
        'src/battle_anim.c': ['DestroyAnimSprite', 'DestroyAnimVisualTask'],
        'src/trig.c': ['Sin'],
        'src/sprite.c': engine,
    }
    functions = [extract.function(sources[path], name) for path, values in names.items() for name in values]
    prototypes = '\n'.join(value[:value.index('{')].strip() + ';' for value in functions)
    arrays = '\n'.join(extract.array(sources['src/sprite.c'], name) for name in [
        'sAnimFuncs', 'sAffineAnimFuncs', 'sAnimCmdFuncs', 'sAffineAnimCmdFuncs',
    ])
    arrays += '\n' + extract.array(sources['src/trig.c'], 'gSineTable')
    arrays += '\n' + extract.array(sources['src/battle_anim_mons.c'], 'sBattlerCoords')
    # This exact source block includes both sentinel objects and their tables.
    sprites = sources['src/sprite.c']
    begin = sprites.index('static const union AnimCmd sDummyAnim =')
    end = sprites.index(';', sprites.index('const union AffineAnimCmd * const gDummySpriteAffineAnimTable[] =', begin)) + 1
    arrays += '\n' + sprites[begin:end] + '\n'
    for name in ['gOamData_AffineNormal_ObjNormal_32x32', 'gOamData_AffineOff_ObjNormal_16x16']:
        arrays += declaration(sources['src/data/battle_anim.h'], 'struct OamData', name)
    for name in [
        'sFallingBagAnimCmds', 'sFallingBagAnimTable', 'sFallingBagAffineAnimCmds1',
        'sFallingBagAffineAnimCmds2', 'sFallingBagAffineAnimTable',
        'sPresentHealParticleAnimCmds', 'sPresentHealParticleAnimTable',
    ]:
        arrays += extract.array(sources['src/battle_anim_effects_1.c'], name)
    for name in ['gPresentSpriteTemplate', 'gPresentHealParticleSpriteTemplate']:
        arrays += declaration(sources['src/battle_anim_effects_1.c'], 'struct SpriteTemplate', name)
    defines = extract.macros(sources.values(), [
        'Q_8_8', 'ST_OAM_AFFINE_ON_MASK', 'ANIM_TAG_ITEM_BAG', 'ANIM_TAG_GREEN_SPARKLE',
        'B_SIDE_PLAYER', 'B_SIDE_OPPONENT', 'BATTLE_PARTNER', 'BIT_SIDE',
        'BATTLE_TYPE_DOUBLE', 'GET_BATTLER_SIDE2', 'GET_BATTLER_POSITION',
        'MAX_BATTLERS_COUNT', 'SPRITE_NONE', 'ARG_RET_ID', 'ANIM_END', 'AFFINE_ANIM_END',
    ])
    defines += extract.enum_containing(sources['include/battle_anim.h'], 'BATTLER_COORD_X')
    defines += oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
    harness = (Path(__file__).parent / 'harness.c.in').read_text()
    for token, value in {'TYPES': types, 'DEFINES': defines, 'PROTOTYPES': prototypes,
                         'ARRAYS': arrays, 'FUNCTIONS': '\n'.join(functions)}.items():
        harness = harness.replace('@' + token + '@', value)
    if re.search(r'@[A-Z]+@', harness):
        raise ValueError('Unfilled native harness token')
    c_file = out / 'present.c'
    c_file.write_text(harness)
    executables = []
    for opt in ['O0', 'O2']:
        executable = out / ('present-' + opt)
        helper.compile_c(['cc', '-std=c11', '-' + opt, '-fwrapv', '-fno-strict-aliasing', str(c_file), '-o', str(executable)])
        executables.append(executable)

    # Parse the actual authored arguments, retaining duplicate heal births.
    script = sources['data/battle_anim_scripts.s']
    bag_match = re.search(r'^\s*createsprite gPresentSpriteTemplate, ANIM_TARGET, 2, (.+)$', script, re.M)
    if bag_match is None:
        raise ValueError('Missing authored Present bag')
    bag_args = [int(value.strip()) for value in bag_match[1].split(',')]
    heal_body = re.search(r'^PresentHeal:\n(.*?)(?=^\w+:)', script, re.M | re.S)
    if heal_body is None:
        raise ValueError('Missing PresentHeal script')
    heal_args = [[int(value.strip()) for value in match.split(',')] for match in re.findall(
        r'^\s*createsprite gPresentHealParticleSpriteTemplate, ANIM_TARGET, 4, (.+)$', heal_body[1], re.M)]
    if len(heal_args) != 9:
        raise ValueError('Unexpected PresentHeal authored particle count')
    cases = []
    layouts = [(0, 0, 1), (0, 1, 0), (1, 0, 3), (1, 1, 2), (1, 2, 0), (1, 3, 1)]

    def add(kind, layout, args, *, damage=0, picture_offset=19, label='authored'):
        doubles, actor, target = layout
        arguments = list(args) + [2000 + i for i in range(len(args), 8)]
        argv = [kind, doubles, actor, target, picture_offset, damage, *arguments]
        outputs = [subprocess.check_output([str(executable), *map(str, argv)], text=True) for executable in executables]
        if outputs[0] != outputs[1]:
            raise ValueError('Native O0/O2 mismatch: ' + str(argv))
        rows = [json.loads(line) for line in outputs[0].splitlines()]
        if not rows or rows[-1]['alive'] or rows[-1]['visualCount'] != 0:
            raise ValueError('Empty/incomplete native Present case: ' + str(argv))
        if [row['frame'] for row in rows] != list(range(len(rows))):
            raise ValueError('Noncontiguous native Present trajectory')
        if kind == 0 and not any(row['affine'][0] == 1 for row in rows):
            raise ValueError('Bag never reached its settling affine restart')
        if kind == 1 and not any(row['sprite'][8] for row in rows):
            raise ValueError('Heal particle never reached frame END')
        cases.append(dict(kind=kind, doubles=bool(doubles), actor=actor, target=target,
                          yOffset=picture_offset, damage=damage, arguments=arguments,
                          branch=label, rows=rows))

    for layout in layouts:
        add(0, layout, bag_args)
        for x, y in [(-12, 4), (12, -8)]:
            add(0, layout, [x, y, 10, 2, -1], label='signed-birth-offset')
        for args in heal_args:
            add(1, layout, args)
        for velocity in [0, 1, -1, 32767, -32768]:
            add(1, layout, [-7, 3, velocity, 123], label='signed16-vertical-velocity')
        for x, y in [(-32768, 32767), (32767, -32768)]:
            add(1, layout, [x, y, -3, -1], label='signed16-birth-wrap')
        for damage in [-2147483648, -65536, -32768, -1, 0, 1, 32767, 65536, 2147483647]:
            add(2, layout, [-32768, 32767, -123, 0, 1, 345, -234, -999], damage=damage,
                label='signed32-damage-predicate')
    if len(cases) != 168:
        raise ValueError('Incomplete Present native corpus')
    return dict(
        schema='firered-present-source-v1', sourceSha256=pins, sourceFunctions=names,
        optimizationParity=['O0', 'O2'], cases=cases,
        spriteColumns=['x', 'y', 'x2', 'y2', 'tile', 'visible', 'oamPriority', 'subpriority',
                       'animEnded', 'animDelayCounter', 'xScale', 'yScale', 'rotation', 'affineAnimEnded'],
        affineColumns=['animNum', 'animCmdIndex', 'delayCounter', 'loopCounter', 'affineAnimBeginning'],
        frameStateColumns=['animNum', 'animCmdIndex', 'animLoopCounter', 'animBeginning'],
        callbackCodes={'travel': 0, 'settle': 1, 'heal': 2, 'predicate': 3},
        semantics='Frame0 runs original creation callback then AnimateSprite, or the immediate predicate once. Later frames run original AnimateSprites. Native initializer, bag motion, heal callback, frame/affine interpreter, sine table, coordinates table and signed-s32 damage predicate are extracted unchanged. Resource allocation, coordinate lookup into the extracted table, picture-offset input and BIOS output are authored boundaries. Raw coordinate requests are independent of picture offset. Matrix output is stubbed; exact native affine scale/rotation state is observed. Destruction retains inactive memory for terminal inspection. No original scripts or managed implementation execute to obtain expectations.'
    )


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--build-dir', type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Refusing to overwrite reviewed fixture: ' + str(args.output))
    out = args.build_dir or Path(tempfile.mkdtemp(prefix='present-reference-'))
    out.mkdir(parents=True, exist_ok=True)
    root = args.root.resolve()
    result = build(root, args.reference or root / 'content/raw/pokefirered/037335f4', out)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(json.dumps(dict(cases=len(result['cases']), rows=sum(len(case['rows']) for case in result['cases']))))
